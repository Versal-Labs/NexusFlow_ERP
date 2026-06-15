[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern("^[A-Za-z0-9_.-]+$")]
    [string]$InstanceId,

    [Parameter(Mandatory = $true)]
    [string]$PackagePath,

    [Parameter(Mandatory = $true)]
    [string]$CertificateThumbprint,

    [string]$SiteName = "NexusFlow-ERP",
    [string]$AppPoolName = "NexusFlow-ERP",
    [string]$PhysicalPath = "C:\inetpub\NexusFlow-ERP",
    [string]$HostName = "",
    [int]$HttpsPort = 443
)

$ErrorActionPreference = "Stop"

$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
$principal = [Security.Principal.WindowsPrincipal]::new($identity)
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw "Run this deployment script from an elevated PowerShell session."
}

Import-Module WebAdministration

$runtimeFound = dotnet --list-runtimes | Select-String -Pattern "^Microsoft\.AspNetCore\.App 10\."
if (-not $runtimeFound) {
    throw "The .NET 10 Hosting Bundle is required before deploying NexusFlow."
}

$resolvedPackage = (Resolve-Path -LiteralPath $PackagePath).Path
$instanceRoot = Join-Path $env:ProgramData "NexusFlow\ERP\$InstanceId"
$appPoolIdentity = "IIS AppPool\$AppPoolName"

New-Item -ItemType Directory -Path $PhysicalPath -Force | Out-Null
New-Item -ItemType Directory -Path $instanceRoot -Force | Out-Null
@("data-protection", "logs", "storage") | ForEach-Object {
    New-Item -ItemType Directory -Path (Join-Path $instanceRoot $_) -Force | Out-Null
}

if ($resolvedPackage.EndsWith(".zip", [StringComparison]::OrdinalIgnoreCase)) {
    Expand-Archive -LiteralPath $resolvedPackage -DestinationPath $PhysicalPath -Force
}
else {
    Copy-Item -Path (Join-Path $resolvedPackage "*") -Destination $PhysicalPath -Recurse -Force
}

if (-not (Test-Path "IIS:\AppPools\$AppPoolName")) {
    New-WebAppPool -Name $AppPoolName | Out-Null
}
Set-ItemProperty "IIS:\AppPools\$AppPoolName" -Name managedRuntimeVersion -Value ""
Set-ItemProperty "IIS:\AppPools\$AppPoolName" -Name processModel.identityType -Value 4
Set-ItemProperty "IIS:\AppPools\$AppPoolName" -Name processModel.loadUserProfile -Value $true
Set-ItemProperty "IIS:\AppPools\$AppPoolName" -Name startMode -Value "AlwaysRunning"

icacls $PhysicalPath /grant "${appPoolIdentity}:(OI)(CI)(RX)" /T /C | Out-Null
icacls $instanceRoot /grant "${appPoolIdentity}:(OI)(CI)(M)" /T /C | Out-Null

$webConfigPath = Join-Path $PhysicalPath "web.config"
[xml]$webConfig = Get-Content -LiteralPath $webConfigPath
$aspNetCore = $webConfig.configuration.location."system.webServer".aspNetCore
if (-not $aspNetCore.environmentVariables) {
    $environmentVariables = $webConfig.CreateElement("environmentVariables")
    $aspNetCore.AppendChild($environmentVariables) | Out-Null
}

$existingVariable = $aspNetCore.environmentVariables.add | Where-Object { $_.name -eq "NEXUSFLOW_INSTANCE_ID" }
if ($existingVariable) {
    $existingVariable.value = $InstanceId
}
else {
    $variable = $webConfig.CreateElement("add")
    $variable.SetAttribute("name", "NEXUSFLOW_INSTANCE_ID")
    $variable.SetAttribute("value", $InstanceId)
    $aspNetCore.environmentVariables.AppendChild($variable) | Out-Null
}
$webConfig.Save($webConfigPath)

$setupKeyBytes = [byte[]]::new(32)
$random = [Security.Cryptography.RNGCryptoServiceProvider]::new()
try {
    $random.GetBytes($setupKeyBytes)
}
finally {
    $random.Dispose()
}
$setupKey = [Convert]::ToBase64String($setupKeyBytes).TrimEnd("=").Replace("+", "-").Replace("/", "_")
$bootstrapPath = Join-Path $instanceRoot "bootstrap.key"
[IO.File]::WriteAllText($bootstrapPath, $setupKey, [Text.UTF8Encoding]::new($false))
icacls $bootstrapPath /inheritance:r /grant:r "${appPoolIdentity}:(R,W)" /grant:r "BUILTIN\Administrators:(F)" | Out-Null

if (-not (Test-Path "IIS:\Sites\$SiteName")) {
    New-Website -Name $SiteName -PhysicalPath $PhysicalPath -ApplicationPool $AppPoolName -Port 80 -HostHeader $HostName | Out-Null
}
Set-ItemProperty "IIS:\Sites\$SiteName" -Name applicationPool -Value $AppPoolName
Set-ItemProperty "IIS:\Sites\$SiteName" -Name physicalPath -Value $PhysicalPath

$binding = Get-WebBinding -Name $SiteName -Protocol https -Port $HttpsPort -HostHeader $HostName -ErrorAction SilentlyContinue
$sslFlags = if ([string]::IsNullOrWhiteSpace($HostName)) { 0 } else { 1 }
if (-not $binding) {
    New-WebBinding -Name $SiteName -Protocol https -Port $HttpsPort -HostHeader $HostName -SslFlags $sslFlags | Out-Null
}

$certificate = Get-Item -LiteralPath "Cert:\LocalMachine\My\$CertificateThumbprint"
$sslBindingPath = if ([string]::IsNullOrWhiteSpace($HostName)) {
    "IIS:\SslBindings\0.0.0.0!$HttpsPort"
}
else {
    "IIS:\SslBindings\0.0.0.0!$HttpsPort!$HostName"
}
if (Test-Path $sslBindingPath) {
    Remove-Item $sslBindingPath -Force
}
New-Item $sslBindingPath -Value $certificate | Out-Null

Start-WebAppPool -Name $AppPoolName
Start-Website -Name $SiteName

Write-Host ""
Write-Host "NexusFlow instance deployed. The SQL database was not created or modified."
Write-Host "Instance ID: $InstanceId"
Write-Host "Instance data: $instanceRoot"
Write-Host "One-time setup key: $setupKey"
Write-Host "Open the HTTPS site and complete /install. Store the key securely until setup succeeds."
