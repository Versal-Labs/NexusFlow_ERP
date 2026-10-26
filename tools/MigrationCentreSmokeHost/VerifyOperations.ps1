param([string]$BaseUrl = 'http://127.0.0.1:5191')
$ErrorActionPreference = 'Stop'
$target = [Uri]$BaseUrl
if (-not $target.IsLoopback -or $target.Scheme -ne 'http' -or $target.Port -ne 5191) { throw 'This verifier only targets the owned loopback smoke host on port 5191.' }
function Assert-That($condition, [string]$description) { if (-not $condition) { throw $description }; Write-Host "PASS: $description" }
function Login([string]$mode = '') {
    $response = Invoke-WebRequest "$BaseUrl/smoke-login?mode=$mode" -SessionVariable cookie
    $match = [regex]::Match($response.Content, 'name="__RequestVerificationToken"[^>]*value="([^"]+)"')
    if (-not $match.Success) { throw 'No antiforgery token from the synthetic login.' }
    return @{ Cookie = $cookie; Token = [System.Net.WebUtility]::HtmlDecode($match.Groups[1].Value) }
}
$admin = Login
$state = Invoke-RestMethod "$BaseUrl/smoke-state" -WebSession $admin.Cookie
Assert-That ($state.syntheticSmokeHost -eq $true) 'Target explicitly identifies the disposable smoke host'
$projects = @(Invoke-RestMethod "$BaseUrl/api/migration/projects" -WebSession $admin.Cookie)
Assert-That ($projects.Count -eq 1 -and $projects[0].sourceSystem -eq 'SYNTHETIC-SMOKE') 'Only the opt-in synthetic fixture is selected'
$project = $projects[0].id
$apiRoot = "$BaseUrl/api/migration/projects/$project"
function Read-Api([string]$suffix) { return Invoke-RestMethod "$apiRoot$suffix" -WebSession $admin.Cookie }
function Post-Api([string]$suffix, $body, $identity = $admin) {
    return Invoke-WebRequest "$apiRoot$suffix" -Method Post -WebSession $identity.Cookie -Headers @{ RequestVerificationToken = $identity.Token } -ContentType 'application/json' -Body ($body | ConvertTo-Json -Depth 20 -Compress)
}
function Start-Operation([string]$kind, $selection, [string]$hash = '', $release = $null) {
    $request = @{ kind = $kind; clientRequestId = [Guid]::NewGuid().ToString(); selection = $selection; hash = $hash; evidence = 'Synthetic HTTP smoke signed evidence' }
    if ($null -ne $release) { $request.release = $release }
    $response = Post-Api '/operations' $request
    Assert-That ($response.StatusCode -eq 202) "$kind returns 202 Accepted"
    $accepted = $response.Content | ConvertFrom-Json
    $repeated = (Post-Api '/operations' $request).Content | ConvertFrom-Json
    Assert-That ($repeated.operationId -eq $accepted.operationId) "$kind same request ID returns the original operation"
    for ($attempt = 0; $attempt -lt 300; $attempt++) {
        $progress = Read-Api "/operations/$($accepted.operationId)"
        if ($progress.operation.status -notin @('Queued', 'Running')) { return $progress.operation }
        Start-Sleep -Milliseconds 100
    }
    throw "Operation $($accepted.operationId) timed out in the synthetic host."
}
$scopes = Read-Api '/scopes?page=2&pageSize=25&kind=Customer&status=Pending'
Assert-That ($scopes.page -eq 2 -and $scopes.items.Count -eq 25 -and $scopes.total -eq 130) 'HTTP scope pagination binds page 2 and returns only 25 rows'
$searched = Read-Api '/scopes?page=1&pageSize=25&kind=Customer&status=Pending&search=C001'
Assert-That ($searched.total -eq 1 -and $searched.items[0].code -eq 'C001') 'HTTP scope kind/status/search filters bind together'
$all = @{ modules = @('CustomerOpenings', 'ControlTotals'); customers = @(); suppliers = @(); skus = @(); banks = @() }
$validation = Start-Operation 'Validate' $all
Assert-That ($validation.status -eq 'CompletedWithIssues') 'Invalid synthetic masters produce saved validation issues'
$errors = Read-Api "/operations/$($validation.id)/items?page=2&pageSize=25&status=NeedsAttention&module=CustomerOpenings"
Assert-That ($errors.page -eq 2 -and $errors.total -eq 128 -and $errors.items.Count -eq 25 -and @($errors.items | Where-Object status -ne 'NeedsAttention').Count -eq 0) 'HTTP validation filters and page 2 are applied together'
$capped = Read-Api "/operations/$($validation.id)/items?page=1&pageSize=1000&status=NeedsAttention"
Assert-That ($capped.pageSize -eq 100 -and $capped.items.Count -eq 100) 'HTTP result size is capped at 100'
$issues = Read-Api "/operations/$($validation.id)/issues?page=1&pageSize=25&status=Error&grouped=true"
Assert-That ($issues.total -eq 3 -and @($issues.items | Where-Object severity -ne 'Error').Count -eq 0) 'Grouped HTTP errors exclude warnings and retain affected-row counts'
$one = @{ modules = @('CustomerOpenings', 'ControlTotals'); customers = @('C001'); suppliers = @(); skus = @(); banks = @() }
$valid = Start-Operation 'Validate' $one
Assert-That ($valid.canApprove -eq $true) 'Selected valid customer is independent of unrelated invalid masters'
$validItems = Read-Api "/operations/$($valid.id)/items?page=1&pageSize=25&module=CustomerOpenings"
$detail = Read-Api "/operations/$($valid.id)/items/$($validItems.items[0].id)?page=1&pageSize=25"
Assert-That ($detail.rows.total -eq 1 -and $detail.journal.Count -eq 2) 'HTTP source and proposed-journal details are available on demand'
$approval = Start-Operation 'Approve' $one $valid.resultHash
Assert-That ($approval.status -eq 'Completed') 'Background approval completes with the current selected hash'
$import = Start-Operation 'Import' $one
Assert-That ($import.status -eq 'Completed' -and $import.total -eq 2 -and $import.succeeded -eq 2) 'Background import counts only selected customer and control units'
$importedItems = Read-Api "/operations/$($import.id)/items?page=1&pageSize=25&module=CustomerOpenings"
$importedDetail = Read-Api "/operations/$($import.id)/items/$($importedItems.items[0].id)?page=1&pageSize=25"
Assert-That ($importedDetail.rows.total -eq 1 -and $importedDetail.journalCount -eq 2) 'Imported outcomes reuse immutable run source/journal details on demand'
$completedRuns = Read-Api '/lists/runs?page=1&pageSize=25&status=Completed'
$failedRuns = Read-Api '/lists/runs?page=1&pageSize=25&status=Failed'
Assert-That ($completedRuns.total -eq 1 -and $completedRuns.items[0].operationId -eq $import.id -and $failedRuns.total -eq 0) 'Posting run filters bind and link operation-owned runs to their progress'
$release = Start-Operation 'Release' @{} '' @{ kind = 'Customer'; scopeIds = @($searched.items[0].id); allMatching = $false; expectedCount = 1 }
Assert-That ($release.status -eq 'Completed' -and $release.total -eq 1 -and $release.succeeded -eq 1) 'Dedicated release freezes and completes exactly one selected pending scope'
$summary = Read-Api '/summary'
Assert-That ($summary.releasedScopes -eq 1 -and $summary.pendingScopes -eq 129) 'Project totals remain separate from one-scope operation totals'
$audit = Read-Api '/lists/audit?page=1&pageSize=100'
Assert-That (@($audit.items | Where-Object action -eq 'Released').Count -eq 1 -and @($audit.items | Where-Object { $_.action -eq 'Approved' -and $_.createdBy -eq 'migration-tester' }).Count -eq 1) 'Release is exactly once and background approval preserves initiating identity'
foreach ($mode in @('viewonly', 'prepareonly')) {
    $identity = Login $mode
    foreach ($suffix in @('/operations', "/operations/$($release.id)/resume")) {
        $denied = 0
        try { Post-Api $suffix @{ kind = 'Release'; clientRequestId = [Guid]::NewGuid().ToString(); evidence = 'Unauthorized synthetic attempt' } $identity | Out-Null }
        catch { $denied = [int]$_.Exception.Response.StatusCode }
        Assert-That ($denied -eq 403) "$mode cannot start or resume Release ($suffix)"
    }
}
$noToken = 0
$freshCookie = Login
try { Invoke-WebRequest "$apiRoot/operations" -Method Post -WebSession $freshCookie.Cookie -ContentType 'application/json' -Body '{"kind":"Validate","clientRequestId":"5b681ffd-7466-4d32-829e-5fbd897b5a38","selection":{"modules":["Accounts"]}}' | Out-Null }
catch { $noToken = [int]$_.Exception.Response.StatusCode }
Assert-That ($noToken -eq 400) "Cookie mutation without antiforgery is rejected (HTTP $noToken)"
Write-Output 'Synthetic HTTP operation verification completed. The smoke host remains available for browser checks.'

