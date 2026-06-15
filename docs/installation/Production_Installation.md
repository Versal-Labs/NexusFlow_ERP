# NexusFlow Production Installation

## Release Package

Create a framework-dependent IIS release:

```powershell
.\deploy\Build-Release.ps1 -Version 1.0.0
```

The generated ZIP does not contain customer secrets or a database.

## Server Deployment

Before deployment:

- Install IIS and the .NET 10 Hosting Bundle.
- Create an empty SQL Server database, or identify a compatible existing NexusFlow database.
- Grant the IIS app-pool identity or SQL login access to the database, including `CREATE TABLE` and `ALTER` during installation and upgrades.
- Install a valid HTTPS certificate in `LocalMachine\My`.

Run from elevated PowerShell:

```powershell
.\Deploy-NexusFlow.ps1 `
  -InstanceId customer-a `
  -PackagePath .\NexusFlow-1.0.0-iis.zip `
  -CertificateThumbprint CERTIFICATE_THUMBPRINT `
  -SiteName NexusFlow-CustomerA `
  -AppPoolName NexusFlow-CustomerA `
  -PhysicalPath C:\inetpub\NexusFlow-CustomerA `
  -HostName erp.customer.example
```

The script creates isolated instance directories, ACLs, an IIS app pool/site, and prints a one-time setup key in the PowerShell output. Capture that output securely. It never creates, drops, backs up, or restores a SQL database.

Open the HTTPS site and complete `/install`. The ERP remains unavailable until every readiness check passes. After installation, the setup key is permanently consumed.

## Upgrades

Deploy the new release over the site. When pending EF migrations are detected, NexusFlow enters `UpgradeRequired` mode. A SuperAdmin or server setup authorization must acknowledge that a database backup exists before applying the upgrade at `/maintenance/upgrade`.

To explicitly issue a replacement setup or maintenance key from the server:

```powershell
.\New-NexusFlowSetupKey.ps1 -InstanceId customer-a
```

For the default Visual Studio development instance, run:

```powershell
.\deploy\New-NexusFlowSetupKey.ps1 -InstanceId default
```

Restart the app, open `/install`, and enter the key printed once by the command. Do not put setup keys in source control, launch settings, logs, or screenshots.
