# NexusFlow Migration Centre — local UAT review kit

Built 6 October 2026 from the current workspace. This is a **local dirty-workspace review snapshot**, not a verified CI/main release. It includes the existing local BOM, raw-material and deployment-related source changes as well as Migration Centre. `release-manifest.json` records the actual base commit, changed-file hashes, payload hashes and local verification; there is no fabricated CI run.

## Contents

- `application.zip`: framework-dependent .NET 10 IIS application, binaries/static assets/default document templates only. Configuration, `web.config`, secrets, PDBs, uploaded data, spreadsheets and test hosts are excluded.
- `database/01-Check-UAT.sql`: readonly database identity/baseline preflight; edit the expected database name before running.
- `database/02-Migration-Centre-Upgrade.sql`: idempotent forward upgrade from `20260622171142_FinancialControlsDocumentsAndProduction` through `20261006160821_AddMigrationCentre`, including the two customer-opening migrations. Do not use on earlier/unknown baselines without reviewing the full path.
- `NexusFlow-migration-templates-v1.0.zip`: twelve blank CSV datasets, versioned manifest and README; no business balances. Edit source/date, fill actual rows and remove unavailable modules from the manifest.
- `docs/`: current migration operating guide, data request, seven UAT/user manuals, local verification and synthetic browser screenshot.
- `release-manifest.json`, `FILES.sha256`: provenance and SHA-256 integrity checks. Outer kit has a separate `.sha256` file.

## Deployment sequence

1. Review this complete local source snapshot and local evidence. For the normal Garnet release process, commit/review it and use actual successful CI/main provenance; this kit is not compatible with the CI-only release scripts' provenance gate.
2. Identify the exact hosted UAT IIS site/pool, physical path, instance root and database. Do not assume the workstation's mapped publish path is the hosted target.
3. Back up SQL and verify restoration. Preserve hosted `appsettings*.json`, `web.config`, protected secrets, installation state, environment settings, original-file storage and data-protection keys. Record the current application release.
4. Rehearse this upgrade on a restored disposable database and run the MC acceptance cases. A target containing training/unrelated journals cannot receive another company opening position.
5. During maintenance stop the app pool, extract `application.zip` to a fresh staging directory, inspect it and copy only its application files into the site. Preserve the existing configuration/instance state. Do not mirror/delete tenant-owned files and do not copy `application-build*` directories.
6. Start the app in controlled upgrade mode. Open `/maintenance/upgrade`, confirm recovery backup and select **Run controlled upgrade**. Alternatively, use the reviewed preflight/SQL script against the confirmed database with the app stopped. Do not use both upgrade methods concurrently.
7. Restart/recycle after success. Confirm `20261006160821_AddMigrationCentre` in migration history, secure original-file storage, Hangfire SQL persistence and the running default-queue worker.
8. Sign in with the authorized migration account. Open **Administration → Settings & Security → Migration Centre**, `/Migration`; verify actual permission claims and **Download Template Pack**. The older customer-only 1.0.6 artifact does not contain this feature.
9. Run **MC-01 to MC-22** and record hosted pass/fail, evidence and business approval. The local harness used synthetic data and substituted background/file adapters; it is not hosted acceptance.
10. Only then arrange the actual business freeze and collect fresh closing exports for the evening before actual go-live. Stale September reference reports are not final balances. Missing AP, bank, trial balance and supporting schedules remain unresolved until supplied/evidenced zero.
11. On failure preserve logs/evidence and use the verified recovery point. Do not downgrade EF schema after imports or delete audit records. Runbook contains eligible unused-unit reversal and subsequent dated-correction rules.

Local verification: full Release suite 97 passed, 0 failed, one optional private-file check skipped; all opted-in SQL import checks ran. Seven JS client checks passed, local MVC/HTTP workflow passed, local browser final ledger showed zero differences/read-only controls, and idempotent schema execution passed from both supported baselines. Existing nullable/analyzer/NU1903 dependency warnings remain recorded in the verification report. No hosted deployment or real legacy import was performed.

Detailed steps: [UAT update](UAT_Update_Migration_Centre.md), [Migration Centre runbook](../migration/Migration_Centre_Runbook.md), [local verification](../uat/Migration_Centre_Verification.md).
