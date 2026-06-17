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
    [int]$HttpsPort = 443,

    [string]$DefaultConnectionString = "",
    [string]$AzureBlobStorageConnectionString = "",

    [ValidateSet("WindowsIis", "PortableVm", "AzureAppService")]
    [string]$DeploymentProfile = "WindowsIis",

    [ValidateSet("Local", "AzureBlob", "Hybrid")]
    [string]$StorageMode = "Local",

    [ValidateSet("Dpapi", "EncryptedFile", "Environment")]
    [string]$SecretStore = "Dpapi",

    [ValidateSet("", "File", "AzureBlob")]
    [string]$StateStore = "",

    [ValidateSet("", "File", "AzureBlob")]
    [string]$DataProtectionStore = ""
)

$ErrorActionPreference = "Stop"

$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
$principal = [Security.Principal.WindowsPrincipal]::new($identity)
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw "Run this deployment script from an elevated PowerShell session."
}

Import-Module WebAdministration

function Get-OrCreate-XmlElement {
    param(
        [Parameter(Mandatory = $true)]
        [xml]$Document,

        [Parameter(Mandatory = $true)]
        [System.Xml.XmlNode]$Parent,

        [Parameter(Mandatory = $true)]
        [string]$Name
    )

    $child = $Parent.SelectSingleNode($Name)
    if ($child) {
        return $child
    }

    $child = $Document.CreateElement($Name)
    [void]$Parent.AppendChild($child)
    return $child
}

function Set-WebConfigEnvironmentVariable {
    param(
        [Parameter(Mandatory = $true)]
        [xml]$Document,

        [Parameter(Mandatory = $true)]
        [System.Xml.XmlElement]$EnvironmentVariables,

        [Parameter(Mandatory = $true)]
        [string]$Name,

        [Parameter(Mandatory = $true)]
        [AllowEmptyString()]
        [string]$Value
    )

    $escapedName = $Name.Replace("'", "&apos;")
    $existing = $EnvironmentVariables.SelectSingleNode("environmentVariable[@name='$escapedName']")
    if ($existing) {
        $existing.SetAttribute("value", $Value)
        return
    }

    $variable = $Document.CreateElement("environmentVariable")
    $variable.SetAttribute("name", $Name)
    $variable.SetAttribute("value", $Value)
    [void]$EnvironmentVariables.AppendChild($variable)
}

function New-NexusFlowWebConfig {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,

        [Parameter(Mandatory = $true)]
        [hashtable]$EnvironmentVariables
    )

    $document = [System.Xml.XmlDocument]::new()
    $declaration = $document.CreateXmlDeclaration("1.0", "utf-8", $null)
    [void]$document.AppendChild($declaration)

    $configuration = $document.CreateElement("configuration")
    [void]$document.AppendChild($configuration)

    $systemWebServer = $document.CreateElement("system.webServer")
    [void]$configuration.AppendChild($systemWebServer)

    $handlers = $document.CreateElement("handlers")
    [void]$systemWebServer.AppendChild($handlers)

    $handler = $document.CreateElement("add")
    $handler.SetAttribute("name", "aspNetCore")
    $handler.SetAttribute("path", "*")
    $handler.SetAttribute("verb", "*")
    $handler.SetAttribute("modules", "AspNetCoreModuleV2")
    $handler.SetAttribute("resourceType", "Unspecified")
    [void]$handlers.AppendChild($handler)

    $aspNetCore = $document.CreateElement("aspNetCore")
    $aspNetCore.SetAttribute("processPath", "dotnet")
    $aspNetCore.SetAttribute("arguments", ".\NexusFlow.Web.dll")
    $aspNetCore.SetAttribute("stdoutLogEnabled", "false")
    $aspNetCore.SetAttribute("stdoutLogFile", ".\logs\stdout")
    $aspNetCore.SetAttribute("hostingModel", "inprocess")
    [void]$systemWebServer.AppendChild($aspNetCore)

    $environmentVariablesElement = $document.CreateElement("environmentVariables")
    [void]$aspNetCore.AppendChild($environmentVariablesElement)

    foreach ($item in $EnvironmentVariables.GetEnumerator() | Sort-Object Name) {
        if ($null -eq $item.Value -or [string]::IsNullOrWhiteSpace([string]$item.Value)) {
            continue
        }

        $variable = $document.CreateElement("environmentVariable")
        $variable.SetAttribute("name", [string]$item.Name)
        $variable.SetAttribute("value", [string]$item.Value)
        [void]$environmentVariablesElement.AppendChild($variable)
    }

    $settings = [System.Xml.XmlWriterSettings]::new()
    $settings.Encoding = [System.Text.UTF8Encoding]::new($false)
    $settings.Indent = $true
    $settings.NewLineOnAttributes = $false

    $writer = [System.Xml.XmlWriter]::Create($Path, $settings)
    try {
        $document.Save($writer)
    }
    finally {
        $writer.Dispose()
    }
}

$runtimeFound = dotnet --list-runtimes | Select-String -Pattern "^Microsoft\.AspNetCore\.App 10\."
if (-not $runtimeFound) {
    throw "The .NET 10 Hosting Bundle is required before deploying NexusFlow."
}

$aspNetCoreModule = Get-WebGlobalModule -Name AspNetCoreModuleV2 -ErrorAction SilentlyContinue
if (-not $aspNetCoreModule) {
    throw "IIS is missing AspNetCoreModuleV2. Install or repair the .NET 10 Hosting Bundle after IIS is installed, then run iisreset."
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
$webConfigEnvironmentVariables = @{
    "NEXUSFLOW_INSTANCE_ID" = $InstanceId
    "NEXUSFLOW_DEPLOYMENT_PROFILE" = $DeploymentProfile
    "NEXUSFLOW_STORAGE_MODE" = $StorageMode
    "NEXUSFLOW_SECRET_STORE" = $SecretStore
    "NEXUSFLOW_STATE_STORE" = $StateStore
    "NEXUSFLOW_DATA_PROTECTION_STORE" = $DataProtectionStore
    "ConnectionStrings__DefaultConnection" = $DefaultConnectionString
    "ConnectionStrings__AzureBlobStorage" = $AzureBlobStorageConnectionString
}
New-NexusFlowWebConfig -Path $webConfigPath -EnvironmentVariables $webConfigEnvironmentVariables

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
icacls $bootstrapPath /inheritance:r /grant:r "${appPoolIdentity}:(M)" /grant:r "BUILTIN\Administrators:(F)" | Out-Null

if (-not (Test-Path "IIS:\Sites\$SiteName")) {
    New-Website -Name $SiteName -PhysicalPath $PhysicalPath -ApplicationPool $AppPoolName -Port 80 -HostHeader $HostName | Out-Null
}
Set-ItemProperty "IIS:\Sites\$SiteName" -Name applicationPool -Value $AppPoolName
Set-ItemProperty "IIS:\Sites\$SiteName" -Name physicalPath -Value $PhysicalPath

$binding = Get-WebBinding -Name $SiteName -Protocol https -Port $HttpsPort -HostHeader $HostName -ErrorAction SilentlyContinue
$sslFlags = if ([string]::IsNullOrWhiteSpace($HostName)) { 0 } else { 1 }
if (-not $binding) {
    New-WebBinding -Name $SiteName -Protocol https -Port $HttpsPort -HostHeader $HostName -SslFlags $sslFlags | Out-Null
    $binding = Get-WebBinding -Name $SiteName -Protocol https -Port $HttpsPort -HostHeader $HostName -ErrorAction Stop
}

$certificate = Get-Item -LiteralPath "Cert:\LocalMachine\My\$CertificateThumbprint"
if (-not $certificate.HasPrivateKey) {
    throw "Certificate $CertificateThumbprint does not have a private key. Import the Cloudflare origin certificate as a PFX into LocalMachine\My."
}

$binding = @($binding)[0]
$binding.AddSslCertificate($certificate.Thumbprint, "My")

Start-WebAppPool -Name $AppPoolName
Start-Website -Name $SiteName

Write-Host ""
Write-Host "NexusFlow instance deployed. The SQL database was not created or modified."
Write-Host "Instance ID: $InstanceId"
Write-Host "Instance data: $instanceRoot"
Write-Host "One-time setup key: $setupKey"
Write-Host "Open the HTTPS site and complete /install. Store the key securely until setup succeeds."
