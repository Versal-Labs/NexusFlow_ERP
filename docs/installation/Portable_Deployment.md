# NexusFlow Portable And Cloud Deployment

NexusFlow now supports three deployment profiles. The in-app installer remains the authority for first-run provisioning, setup key consumption, migration readiness, and upgrade mode.

## Deployment Profiles

| Profile | Best for | State store | Secret store | Storage |
| --- | --- | --- | --- | --- |
| `WindowsIis` | Existing Windows Server/IIS installs | Local JSON file | Windows DPAPI | Local or hybrid |
| `PortableVm` | Linux/Windows VM, Docker, on-prem container host | Local JSON file on mounted storage | Cross-platform encrypted file | Local, Azure Blob, or hybrid |
| `AzureAppService` | App Service or Web App for Containers | Azure Blob by default | App settings/Key Vault read-through | Azure Blob |

Use `NEXUSFLOW_INSTANCE_ID` as the instance boundary. Azure Blob deployments use one tenant-style container per instance, normally `tenant-{NEXUSFLOW_INSTANCE_ID}`, with module folders inside it such as `branding/...` and `templates/...`.

## Common Environment Variables

| Variable | Purpose |
| --- | --- |
| `NEXUSFLOW_INSTANCE_ID` | Stable instance id, for example `customer-a` or `prod-erp`. |
| `NEXUSFLOW_INSTANCE_ROOT` | Persistent local state path. Use a mounted volume for VM/container deployments. |
| `NEXUSFLOW_DEPLOYMENT_PROFILE` | `WindowsIis`, `PortableVm`, or `AzureAppService`. |
| `NEXUSFLOW_STATE_STORE` | `File` or `AzureBlob`. |
| `NEXUSFLOW_SECRET_STORE` | `Dpapi`, `EncryptedFile`, or `Environment`. |
| `NEXUSFLOW_DATA_PROTECTION_STORE` | `File` or `AzureBlob`. |
| `NEXUSFLOW_STORAGE_MODE` | `Local`, `AzureBlob`, or `Hybrid`. |
| `NEXUSFLOW_STORAGE_CONTAINER` | Optional Azure container override. Defaults to `tenant-{instanceId}`. |
| `NEXUSFLOW_SETUP_KEY` | One-time setup key for first-run installer unlock, mainly for cloud/container first boot. |
| `ConnectionStrings__DefaultConnection` | SQL Server or Azure SQL connection string. Supports managed identity strings such as `Authentication=Active Directory Default`. |
| `ConnectionStrings__AzureBlobStorage` | Azure Storage connection string for cloud state, Data Protection, and file storage. |

## Azure Fully Managed

1. Build and push a container image:

```powershell
.\deploy\Build-Container.ps1 -ImageName nexusflow-erp -Tag 1.0.0 -Registry myregistry.azurecr.io -Push
```

2. Provision Azure resources with Bicep:

```powershell
az deployment group create `
  --resource-group rg-nexusflow-prod `
  --template-file infra/azure/main.bicep `
  --parameters appName=nexusflow instanceId=prod containerImage=myregistry.azurecr.io/nexusflow-erp:1.0.0 sqlAdminLogin=sqladmin sqlAdminPassword='<secure>'
```

3. Create a database user for the App Service managed identity, or replace the `DefaultConnection` App Service connection string with a SQL auth or Key Vault-referenced connection string.

4. Set a one-time setup key if it was not already configured:

```powershell
.\deploy\Deploy-NexusFlow.Azure.ps1 `
  -ResourceGroup rg-nexusflow-prod `
  -WebAppName nexusflow-web-xxxxx `
  -ImageName myregistry.azurecr.io/nexusflow-erp:1.0.0 `
  -InstanceId prod `
  -SetupKey '<one-time-key>'
```

5. Open `https://{web-app}/install`, unlock with the setup key, and complete provisioning.

For production, prefer Key Vault references for SQL and Storage connection strings. The deployment script accepts raw connection strings for convenience but warns because command-line secrets can be captured in shell history.

## Hybrid Azure SQL + VM Or On-Prem App

Use the `PortableVm` profile when the app runs on a VM or on-prem server but the database is Azure SQL.

Recommended settings:

```text
NEXUSFLOW_DEPLOYMENT_PROFILE=PortableVm
NEXUSFLOW_SECRET_STORE=EncryptedFile
NEXUSFLOW_STATE_STORE=File
NEXUSFLOW_DATA_PROTECTION_STORE=File
NEXUSFLOW_STORAGE_MODE=Local
NEXUSFLOW_INSTANCE_ROOT=/var/lib/nexusflow/prod
ConnectionStrings__DefaultConnection=Server=tcp:server.database.windows.net,1433;Database=NexusFlow;User ID=...;Password=...;Encrypt=True;TrustServerCertificate=False;MultipleActiveResultSets=True;
```

Set `NEXUSFLOW_STORAGE_MODE=AzureBlob` or `Hybrid` plus `ConnectionStrings__AzureBlobStorage` if document templates, logos, and other files must live in Azure Blob Storage.

## Generic VM Or Container

For Docker Compose:

```powershell
docker compose up -d --build
```

Before first run, set a real `NEXUSFLOW_SETUP_KEY` and a SQL connection string in `docker-compose.yml`, environment variables, or your container orchestrator secret store. Keep `/app/state` mounted so installation state, encrypted secrets, Data Protection keys, logs, and local storage survive container replacement.

For a direct VM process without containers, publish normally and set:

```text
NEXUSFLOW_DEPLOYMENT_PROFILE=PortableVm
NEXUSFLOW_SECRET_STORE=EncryptedFile
NEXUSFLOW_STATE_STORE=File
NEXUSFLOW_DATA_PROTECTION_STORE=File
NEXUSFLOW_STORAGE_MODE=Local
NEXUSFLOW_INSTANCE_ROOT=<persistent path>
```

Then open `/install` and complete the first-run workflow.

## Windows IIS

The existing IIS scripts remain supported:

- `deploy/Build-Release.ps1`
- `deploy/Deploy-NexusFlow.ps1`
- `deploy/New-NexusFlowSetupKey.ps1`

This path defaults to local file installation state, DPAPI secrets, and file-based Data Protection keys.

## Upgrades And Recovery

- Always take a database backup before applying an upgrade.
- Deploy the new package/image.
- If pending EF migrations are detected, NexusFlow enters `UpgradeRequired` mode.
- Open `/maintenance/upgrade` and confirm the backup before migrations run.
- For file-backed state, use `deploy/New-NexusFlowSetupKey.ps1` to issue a replacement setup/maintenance key.
- For Azure Blob-backed state, set a new `NEXUSFLOW_SETUP_KEY` only before the state blob is initialized. After initialization, rotate maintenance access through normal SuperAdmin login or controlled state recovery.

## Storage Behavior

- `Local`: writes only to the configured local storage path.
- `AzureBlob`: writes only to Azure Blob and fails fast if Azure Blob is unavailable.
- `Hybrid`: tries Azure Blob first and falls back to local storage, with an audit log entry.

Fully cloud deployments should use `AzureBlob` to avoid accidental writes to ephemeral local disks.
