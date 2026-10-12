param([string]$SourceCsv)
$ErrorActionPreference = 'Stop'
$base = 'http://127.0.0.1:5189'
function Assert-Import($condition, $description) {
    if (-not $condition) { throw "FAILED: $description" }
    Write-Output "PASS: $description"
}
$page = Invoke-WebRequest "$base/smoke-login" -SessionVariable importSession
Assert-Import ($page.StatusCode -eq 200 -and $page.Content.Contains('id="importFile"')) 'Actual Razor import page renders'
$token = [regex]::Match($page.Content, 'name="__RequestVerificationToken" type="hidden" value="([^"]+)"').Groups[1].Value
Assert-Import ($token.Length -gt 0) 'Page provides antiforgery token'
$headers = @{ RequestVerificationToken = $token }
$setup = Invoke-RestMethod "$base/api/inventory/raw-material-import/setup" -WebSession $importSession
Assert-Import ($setup.warehouses[0].code -eq 'WH-MAIN' -and $setup.brands[0] -eq 'UNBRANDED') 'Existing setup loads'
$script = Invoke-WebRequest "$base/js/inventory/raw-material-import.js"
Assert-Import ($script.StatusCode -eq 200 -and $script.Content.Contains('PreviewHash')) 'Production client script is served'
$fixture = Get-Item (Join-Path $PSScriptRoot '../../NexusFlow.IntegrationTests/Fixtures/raw-material-import.csv')
$form = @{ File = $fixture; OpeningDate = '2026-09-15'; WarehouseCode = 'WH-MAIN'; BrandName = 'UNBRANDED'; CategoryName = 'Accessories' }
$noCsrf = Invoke-WebRequest "$base/api/inventory/raw-material-import/preview" -Method Post -Form $form -WebSession $importSession -SkipHttpErrorCheck
Assert-Import ($noCsrf.StatusCode -eq 400) 'Missing CSRF token is rejected'
$preview = Invoke-RestMethod "$base/api/inventory/raw-material-import/preview" -Method Post -Form $form -WebSession $importSession -Headers $headers
Assert-Import ($preview.succeeded -and $preview.data.canImport -and $preview.data.totalValue -eq 26 -and $preview.data.rows.Count -eq 2) 'Multipart CSV preview validates and reconciles totals'
$state = Invoke-RestMethod "$base/smoke-state"
Assert-Import ($state.products -eq 0 -and $state.transactions -eq 0 -and $state.journals -eq 0) 'Preview makes no writes'
if ($SourceCsv) {
    $sourceForm = $form.Clone(); $sourceForm.File = Get-Item -LiteralPath $SourceCsv
    $source = Invoke-RestMethod "$base/api/inventory/raw-material-import/preview" -Method Post -Form $sourceForm -WebSession $importSession -Headers $headers
    Assert-Import ($source.succeeded -and $source.data.canImport) 'Supplied CSV passes actual endpoint preview (no posting)'
    Write-Output "SOURCE PREVIEW: rows=$($source.data.rows.Count), quantity=$($source.data.totalQuantity), value=$($source.data.totalValue)"
}
$form.PreviewHash = $preview.data.previewHash
$unconfirmed = Invoke-WebRequest "$base/api/inventory/raw-material-import/execute" -Method Post -Form $form -WebSession $importSession -Headers $headers -SkipHttpErrorCheck
Assert-Import ($unconfirmed.StatusCode -eq 400) 'Posting requires explicit confirmation'
$form.Confirmed = 'true'; $form.PreviewHash = 'tampered'
$stale = Invoke-WebRequest "$base/api/inventory/raw-material-import/execute" -Method Post -Form $form -WebSession $importSession -Headers $headers -SkipHttpErrorCheck
Assert-Import ($stale.StatusCode -eq 400) 'Posting requires unchanged preview'
$form.PreviewHash = $preview.data.previewHash
$posted = Invoke-RestMethod "$base/api/inventory/raw-material-import/execute" -Method Post -Form $form -WebSession $importSession -Headers $headers
Assert-Import ($posted.succeeded -and $posted.data.referenceNo.StartsWith('OBSTK') -and $posted.data.totalValue -eq 26) 'Synthetic CSV posts opening stock successfully'
$state = Invoke-RestMethod "$base/smoke-state"
Assert-Import ($state.products -eq 2 -and $state.layers -eq 2 -and $state.transactions -eq 2 -and $state.journals -eq 1) 'Catalog, FIFO, ledger and journal all persist'
$replay = Invoke-WebRequest "$base/api/inventory/raw-material-import/execute" -Method Post -Form $form -WebSession $importSession -Headers $headers -SkipHttpErrorCheck
Assert-Import ($replay.StatusCode -eq 400) 'Repeated upload cannot duplicate stock'
$readonly = Invoke-WebRequest "$base/smoke-login?mode=viewonly" -SessionVariable readonlySession -SkipHttpErrorCheck
Assert-Import ($readonly.StatusCode -eq 403) 'Read-only user cannot access import screen'
$denied = Invoke-WebRequest "$base/api/inventory/raw-material-import/setup" -WebSession $readonlySession -SkipHttpErrorCheck
Assert-Import ($denied.StatusCode -eq 403) 'Read-only user cannot access import API'
foreach ($mode in @('productonly', 'stockonly')) {
    $singlePermission = Invoke-WebRequest "$base/smoke-login?mode=$mode" -SessionVariable singleSession -SkipHttpErrorCheck
    Assert-Import ($singlePermission.StatusCode -eq 403) "$mode user cannot access import screen"
    $singleApi = Invoke-WebRequest "$base/api/inventory/raw-material-import/execute" -Method Post -Form $form -WebSession $singleSession -SkipHttpErrorCheck
    Assert-Import ($singleApi.StatusCode -eq 403) "$mode user cannot execute import API"
}
Write-Output 'HTTP smoke checks complete. Only the synthetic two-row fixture was posted to a disposable LocalDB database.'
