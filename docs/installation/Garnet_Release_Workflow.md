# Garnet releases: saved workflow and GitHub Actions setup

This is the deployment reference for the existing Garnet installation. Routine releases replace application files, preserve server configuration, and use the existing controlled database upgrade screen. The GitHub workflows are implemented in the repository; enabling them requires the one-time server setup below.

## Confirmed deployment target

| Setting | Value |
| --- | --- |
| Private repository | `Versal-Labs/NexusFlow_ERP` |
| Release branch | `main` |
| Website | `https://nexus.garnetnation.lk` |
| Existing application folder | `C:\inetpub\wwwroot\Nexus-Garnet-Prod` |
| Instance ID | `nexus-garnet-2026` |
| Manual package/staging base | `C:\NexusFlow_Setup` |
| Pipeline state | `C:\NexusFlow_Setup\GarnetDeployment` |
| Application/state/storage backups | `C:\NexusFlow\Backups` |

The instance root defaults to `C:\ProgramData\NexusFlow\ERP\nexus-garnet-2026`, but registration reads the effective existing `NEXUSFLOW_INSTANCE_ROOT`. Site and pool names are discovered from the application folder and saved locally. A missing or ambiguous site is an error. No script in this workflow creates an IIS site, pool, binding, tenant or setup key.

Keep the existing HTTPS binding and certificate. No thumbprint input is needed for releases. Certificate renewal alone does not invalidate the target registration.

Do not run `Deploy-NexusFlow.ps1` or `/install` for routine releases: that script provisions the instance, rewrites `web.config`, and generates a bootstrap key. Its default site/path previously created a second IIS site at `C:\inetpub\NexusFlow-ERP`.

Before the runner is enabled, the manual workflow follows the same rules: copy the actual release ZIP to **`C:\NexusFlow_Setup`**, confirm its filename, extract the application payload outside the web root, back up the database and existing files/state, stop only the discovered existing site/pool, copy application files while excluding server configuration and user data, then restart and complete any controlled upgrade. `C:\NexusFlow\_Setup` is a different directory and previously caused missing-ZIP errors. See the release-specific update guide for database preflight; do not confuse an older release-kit ZIP containing `application.zip` with the new CI artifact containing `release.zip` and its checksum.

## What is automated

**NexusFlow CI** runs on pull requests and pushes to `main`, using GitHub-hosted Windows 2022 and .NET 10. It restores/builds, runs the .NET tests with both disposable SQL LocalDB suites enabled, runs the JavaScript client tests and deployment regressions, and checks for EF model changes without a migration. Tests that require private business reports are intentionally excluded. Required SQL tests fail if LocalDB is unavailable; they are not silently skipped.

Only a successful push-to-main CI run supplies a releasable package. `garnet-release-<run-id>-<attempt>` contains `release.zip` and its SHA-256 checksum, retained by GitHub for 30 days. The ZIP contains `payload/` and `release-manifest.json` with the commit SHA, run/attempt, migration IDs and every payload hash. Configuration, credentials, installation state, uploads, storage, source spreadsheets, deployment scripts and debug symbols are excluded. The selected package is never rebuilt on the deployment server.

**Garnet Release** is manual. It validates the selected CI run on a GitHub-hosted runner before using the server runner. Only completed successful push-to-main runs of the expected CI workflow are accepted, and the artifact must match the successful run attempt. The server independently validates package provenance/hashes and its saved target.

## One-time setup on the server

1. Commit and push the application update together with the new workflow/scripts. CI builds committed files, not the developer's uncommitted workspace. Protect `main`, require the CI check before merging, and restrict write access to trusted release operators. Changes to workflow/deployment scripts deserve review because the server runner has deployment privileges.
2. Confirm the existing website is healthy, uses .NET 10 with the IIS Hosting Bundle, and has a dedicated pool with no child IIS applications. This version supports the existing `WindowsIis` deployment with file installation state and file data-protection keys. Cloud state/data protection requires a separate backup adapter and is rejected.
3. Create a dedicated Windows deployment account, for example `SERVERNAME\NexusFlowDeploy`. Its Windows PowerShell 5.1 service must be able to manage the existing IIS site/pool, query `root/WebAdministration`, read the published site and instance/storage, and write release files. In a standard non-delegated IIS installation this requires local administrator privileges. Do not change the **application pool identity**: existing encrypted secrets may depend on it. The deployment account and app-pool identity are separate.
4. In GitHub, open **Settings → Actions → Runners → New self-hosted runner**, choose **Windows / x64**, and use GitHub's current generated installation commands in `C:\actions-runner`. During configuration add the custom label **`garnet-deploy`**, install it as a service, and supply the dedicated account. Register the runner only to this private repository. Keep it updated and allow outbound HTTPS to GitHub's runner/artifact endpoints. No inbound deployment port is required. See [GitHub runner setup](https://docs.github.com/en/actions/how-tos/manage-runners/self-hosted-runners/add-runners) and [runner communication requirements](https://docs.github.com/en/actions/reference/runners/self-hosted-runners).
5. Determine every existing local storage/upload root from the application's **System Settings**, including the configured local storage path. This may differ from the default instance `storage` directory because the installer allows a custom storage path. Remote blob data requires a separately arranged backup. List every local root when registering the target; do not assume the default is the actual location.
6. On the server, obtain the trusted deployment scripts from `main` in a tools checkout outside the site, and run the following in **Windows PowerShell as Administrator**, replacing the deployment account and storage path with confirmed values:

```powershell
Set-Location C:\NexusFlow_Setup\Repository

.\deploy\Initialize-GarnetDeployment.ps1 `
    -DeploymentAccount "SERVERNAME\NexusFlowDeploy" `
    -LocalStoragePaths "C:\ProgramData\NexusFlow\ERP\nexus-garnet-2026\storage" `
    -ConfirmAllLocalStoragePaths
```

For multiple storage roots, pass a comma-separated array. Registration validates existing state and records site/pool/path, instance root, pool identity, binding and hashes of server configuration/environment. It protects deployment state/backups with ACLs for SYSTEM, administrators and the deployment account. It does not store SQL credentials in GitHub.

Refresh registration with the same command plus `-Refresh` only after reviewing an intentional configuration/identity change. An unresolved release prevents refresh. Certificate renewal does not require refresh. Broad backup roots, missing directories and reparse points are rejected.

7. Rehearse the update/recovery process on a disposable IIS installation configured with these scripts before enabling live deployment. Local regression tests simulate IIS and file operations; they are not evidence of a hosted IIS rehearsal. No existing database or live IIS site is touched by the regression suite.

No GitHub secrets or required-reviewer subscription feature is needed for v1: the manual workflow uses its read-only `GITHUB_TOKEN`. No pull-request workflow runs on the server. Runner labels select a machine; they are not a security boundary. Anyone able to change/run repository workflows must be trusted with the runner's privileges.

## Release an update

1. Merge the approved change into `main`. Wait for **NexusFlow CI** to succeed. Open that run and copy the numeric run ID from its URL (`.../actions/runs/<id>`). PR run IDs are not deployable.
2. Arrange a maintenance window and stop transaction entry. Back up the correct SQL database and establish that the backup is restorable. Keep its database name and backup file/job reference. This is an operator-confirmed prerequisite; the pipeline does not execute or automatically verify SQL backups.
3. For schema changes, review current migration history against the package's migration list. Complete any release-specific read-only database preflight. For the 5 October customer migration release, follow `UAT_Update_Customer_Migration.md`: check the required baseline and invalid legacy allocation/cheque rows before upgrading. The maintenance button applies **all** pending migrations, not just the new feature's migrations.
4. In **Actions → Garnet Release → Run workflow**, select branch **main** and provide:

| Input | Selection |
| --- | --- |
| action | `Deploy` |
| ci_run_id | Successful main CI run ID |
| ci_run_attempt | Optional successful attempt number; blank selects the latest attempt |
| database_backup_reference | Correct database name and backup file/job reference; no passwords |
| backup_confirmed | Checked only after confirming a current restorable backup |

5. The runner validates the target/package, takes an exclusive release lock, writes a pending record, stops the existing site/pool and waits up to 90 seconds for workers to exit. It backs up and hashes the application/configuration, instance state and registered local storage, then copies only manifest-listed application files. It preserves all server-only files and restarts the same site/pool. It does not run SQL migrations.
6. Review the workflow summary. A release is complete only when both health endpoints and installed file hashes pass for the expected instance. A cached public health response is discouraged with no-cache headers and unique query values. The existing endpoints do not expose a commit SHA: release identity is checked against local installed file hashes, while live/ready checks verify the served instance and readiness.
7. Sign in, refresh the browser and smoke-test the updated business workflow before letting users resume transaction entry.

If the deployment account cannot stop/read/write the exact resources, fix its access and retry only after reviewing the recorded state. Do not solve file locks with a server-wide `iisreset` or by killing unrelated processes.

## Releases requiring a database upgrade

The workflow reports **AwaitingUpgrade** and ends unsuccessfully so GitHub cannot present it as a completed release. The application files are already copied; the pending marker blocks another deployment.

1. Open `https://nexus.garnetnation.lk/maintenance/upgrade` and sign in with the existing authorized SuperAdmin or legacy Admin account.
2. Review **Pending migrations**, check **I confirm that a current, restorable database backup exists.**, and select **Run controlled upgrade**.
3. After the application restarts, run **Garnet Release** again with action **Verify**, the **same CI run ID and attempt** shown in the deployment summary, and branch **main**. Backup inputs are not required for Verify. Specify the deployed attempt explicitly if that CI run was subsequently rerun; a later attempt is a different artifact.
4. Verify validates the same artifact and installed hashes, then checks readiness without copying files or starting/stopping IIS. It marks the release complete and clears the pending record only on success. Repeating Verify for the latest successful release is supported while its artifact is still retained.

No automatic SQL migration, new upgrade API or business-data import is introduced. An `/install` redirect is a configuration failure to investigate, never a reason to reinstall.

## Failure and recovery

GitHub concurrency and a server-side exclusive file lock prevent overlapping updates. `pending-release.json` also prevents an unresolved release from being overwritten. `latest-release.json` records the last verified deployment. Each backup directory contains the package manifest and `recovery.json` with the database-backup reference and source paths. Server release records and backups are not automatically deleted; monitor disk space and remove old copies only after review.

- **Validation failure:** no site/pool changes. Check package/run identity, expected paths and configuration hashes. A backup confirmation or source validation error happens before copying.
- **Stop timeout:** no copy; a partial stop may have left the site unavailable. Inspect the site/pool and worker process before restarting the old application. The pending record intentionally blocks a blind retry.
- **Backup failure:** no application copy; the script attempts to restart the previous application. A partially created backup must not be treated as complete.
- **Copy/validation failure before startup:** the script restores only the manifest-listed old files and removes exact new payload files that had no predecessor, then restarts the previous application. It never recursively deletes site directories or overwrites instance state.
- **Startup/health failure:** no automatic rollback because the application may already have updated runtime state or the database. Keep users blocked, inspect IIS/application logs and review the stored release/backup. Prefer a reviewed forward repair or restore the coordinated application, database and instance/storage recovery point.
- **Runner interruption/recovery failure:** inspect the pending phase and backup directory. `Preparing`/`BackedUp` means the release has not recorded a startup attempt; verify hashes before restoring payload paths. `Started` means the new app may have run, even if startup returned an error. Do not infer that the database is untouched from a failed job.

After a reviewed manual recovery, archive the pending record outside the active state directory, establish readiness and refresh the target if configuration changed. Do not edit installation-state JSON to bypass readiness. If an artifact has expired, recover from the retained backup/manifest through the reviewed manual procedure rather than selecting a different CI run to verify old files.

## Developer checks

```powershell
.\deploy\tests\Test-GarnetRelease.ps1

$env:NEXUSFLOW_RUN_LOCALDB_IMPORT_TESTS = "1"
$env:NEXUSFLOW_RUN_LOCALDB_CUSTOMER_TESTS = "1"
dotnet test .\NexusFlow.IntegrationTests\NexusFlow.IntegrationTests.csproj -c Release
```

The deployment suite requires no live IIS, application database or administrator privileges. It creates GUID-named fixtures under the current user's temporary directory and removes only those owned fixtures after the checks. It exercises packaging, hashes, unsafe ZIP paths, provenance, discovery, configuration preservation, backup/copy recovery, startup failures, pending upgrades, storage protection, mocked already-stopped IIS and exclusive locks.

## Implementation verification — 8 October 2026

| Check | Result |
| --- | --- |
| Windows PowerShell 5.1 deployment regression suite | 23 passed; mocked IIS and HTTP, temporary file fixtures |
| Current .NET application/integration suite, both SQL opt-ins enabled | 97 passed; 1 optional private-report test skipped |
| JavaScript client tests | 4 passed |
| GitHub workflow validation | Both workflows passed actionlint 1.7.12 and YAML parsing |
| Release publish | Succeeded with existing project warnings |
| EF model/migration consistency | No pending model changes |
| Fresh application package round-trip | 551 payload files and 64 migration IDs; ZIP and per-file SHA-256 checks passed; server-owned paths excluded |

The local package round-trip uses synthetic CI run ID `0` and is **validation evidence only**, not a GitHub-approved distribution artifact. The legitimate `secret-vault.js` UI and Bootstrap assets are application files, not installation secrets/setup keys. The existing Microsoft.OpenApi NU1903 advisory warning remains; this change does not update application dependencies.

No GitHub workflow was dispatched and no live IIS deployment, server runner registration or hosted recovery rehearsal was performed. Commit/push the reviewed changes, register the runner and existing target, and complete the disposable IIS rehearsal before the first live deployment.
