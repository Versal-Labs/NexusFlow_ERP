# Migration Centre UAT review artifact — 6 October 2026

Current kit: [NexusFlow Migration Centre review kit](../../artifacts/uat/migration-centre-20261006-215747/NexusFlow-migration-centre-20261006-uat-review-kit.zip). It contains this wizard; the customer-only 1.0.6 kit below does not.

- Size: 54,122,999 bytes (51.62 MiB).
- SHA-256: `78cc66d0bf06b70e5949f77ede330369233d4afa055881f137c11c451137e697`; [checksum file](../../artifacts/uat/migration-centre-20261006-215747/NexusFlow-migration-centre-20261006-uat-review-kit.zip.sha256).
- Steps: [hosted UAT update](UAT_Update_Migration_Centre.md), [distribution README](Migration_Centre_Review_Kit_README.md).
- Schema target: `20261006160821_AddMigrationCentre`; application: framework-dependent .NET 10.

This is a **local dirty-workspace review snapshot**, including existing local BOM/raw-material/deployment changes. It is not a verified CI/main release. No hosted deployment or real-data import was performed. For normal Garnet deployment, review/commit the source and obtain real CI provenance.

Release publish succeeded. Full suite: 97 passed, 0 failed, one optional private-file check skipped. Seven client tests, synthetic HTTP workflow and local browser checks passed. Idempotent schema checks passed twice from both supported baselines, retaining temporal tables and reporting no pending model changes. All outer/nested ZIP CRCs, retained kit hashes and 551 application file hashes/bytes match the fresh final publish; configuration/secrets/private spreadsheets/PDBs/test hosts are excluded. Blank template ZIP has twelve header-only CSVs. Existing build/dependency warnings and hosted acceptance limits are recorded in [verification](../uat/Migration_Centre_Verification.md).

Deploy only `application.zip` using the upgrade/preservation instructions. `application-build*` and `payload-*` are local packaging intermediates, not distribution packages. Full financial sign-off still requires fresh same-date closing exports, the missing schedules and zero account differences.

## Historical customer-only artifact

# Corrected UAT release artifact — updated 6 October 2026

Release: `1.0.6-uat.customer-opening`, framework-dependent .NET 10 for the existing Windows IIS UAT installation. Supersedes 1.0.5, which omitted the `CustomerMasterImportService` registration and caused customer migration API controller activation to fail.

- Kit: [NexusFlow-1.0.6-uat-customer-opening-release-kit.zip](../../artifacts/uat/customer-opening-di-fix-20261005-083232/NexusFlow-1.0.6-uat-customer-opening-release-kit.zip), 51.38 MiB.
- Checksum: [ZIP SHA-256](../../artifacts/uat/customer-opening-di-fix-20261005-083232/NexusFlow-1.0.6-uat-customer-opening-release-kit.zip.sha256).
- SHA-256: `939ad0ef012bcdb409a70678488400a9162fbf3f092123b7c7bd28f8914cd868`.
- Instructions: [Update hosted UAT](UAT_Update_Customer_Migration.md).

The kit includes application.zip, preflight/upgrade SQL, documentation, a clean web.config reference, release-manifest.json and FILES.sha256. Use application.zip for deployment; the raw local publish staging directory still contains generated development configuration and is not the distribution artifact.

Release publish succeeded; no pending EF model changes; complete kit CRC and all payload SHA-256 hashes verified. Application ZIP has 553 entries and excludes configuration, secrets, source spreadsheets and test-host files. Main application assembly hashes match the fresh publish. Existing build warnings include Microsoft.OpenApi 2.0.0 NU1903. Corrected integration evidence: 74 passing tests, 3 separate stock SQL tests skipped. The registration regression failed before the fix and passed afterward. Isolated MVC smoke now uses application registrations and passes page setup, batch listing, template downloads and the migration workflow. Included SQL is byte-for-byte unchanged from the previously rehearsed kit; that prior rehearsal ran the idempotent upgrade twice on a disposable database.

For a UAT installation where both customer migrations are already applied, replace the application files with the corrected package and restart the application pool. This service-registration correction requires no additional database migration. For local debugging, stop the running application, rebuild and start again before opening `/Finance/CustomerMigration`.

The build is a snapshot of the current dirty workspace at base commit ca84d80, including existing BOM and raw-material changes. Review the manifest and affected workflows before deploying. Hosted deployment, database upgrade and customer posting have not been performed. Authenticated hosted UAT remains pending. The SQL requires baseline 20260622171142_FinancialControlsDocumentsAndProduction; it is not a new-database installer.

The ZIP lives under ignored artifacts/ and is not added to Git. Retain it with its checksum in your release storage when transporting it to the UAT server.
