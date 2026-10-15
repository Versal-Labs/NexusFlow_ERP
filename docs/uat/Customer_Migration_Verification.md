# Customer migration implementation verification

> Historical customer-import release: the current implementation uses [Migration Centre](../migration/Migration_Centre_Runbook.md) and [its UAT update steps](../installation/UAT_Update_Migration_Centre.md). Preserve old audit history; use the central workflow for new cutover work.

Verified locally on 5 October 2026. No live schema upgrade, production deployment or real balance import was performed. Existing unrelated workspace changes were retained. Stock import work is separate.

## Results

| Check | Result | Scope |
|---|---|---|
| Web build | Pass, 0 errors | Existing Microsoft.OpenApi 2.0.0 NU1903 advisory remains; not introduced by this change |
| Integration suite | 74 passed, 3 skipped, 77 total | Includes all 17 customer migration tests; skipped cases are the separately opt-in raw-material SQL tests |
| Private source parser | Pass | Original BIFF XLS and cheque XLSX read-only; amounts/counts match assessment |
| SQL forward upgrade | Pass | Disposable SQL Server LocalDB; recreate prior temporal schema, execute generated combined forward script, query restored current model |
| EF model snapshot | Pass | No pending model changes |
| JavaScript syntax | Pass | Migration, customer master, receipts, cheque vault and AR ageing scripts |
| HTTP controller smoke | Pass | Real MVC controllers, cookie permissions/CSRF, multipart upload, posting and sign-off; synthetic loopback database |
| Whitespace check | Pass | git diff --check |
| Local browser | Partial verification | Earlier synthetic draft/post/reconciliation showed 4 items, 2 cheques, 1 journal, no receipts and matched totals. Final screen displayed the signed-off saved batch; later browser interaction became unresponsive after a confirmation-dialog interruption. Final friendly reconciliation rendering and complete master CSV UI still need manual UAT. |
| Hosted UAT | Pending | https://nexus.garnetnation.lk/ reached Sign In; authenticated role/workflow checks were not performed |

## Customer regression coverage

The 17 tests cover application service registration, confirmed creamt mapping, original binary XLS readability, text cheque identifiers, report dates and subtotals, malformed/formula XLSX rejection, atomic posting and failed-journal rollback, idempotent/concurrent posting, missing/stale mappings and disabled posting, dated return-credit application/reversal, receipt allocation and void with statements/ageing, opening cheque bounce and memo collection, exact deposit journal clearing, endorsement bounce, unused-batch reversal/sign-off, CSV create/update validation, dated batch reversal and prevention of a backdated replacement, cross-customer/invalid allocation and duplicate control-account trial-balance rejection, normal new-system cheque receipt bounce, and temporal schema upgrade.

The initial release omitted `CustomerMasterImportService` from `AddAppCore`, causing API controller activation to fail when the page loaded. The harness previously registered this service independently and missed the defect. The corrected release registers it in `AddAppCore`; both SQL tests and the MVC smoke host now use those application registrations. A non-SQL regression test failed before the fix and passed afterward. The HTTP smoke explicitly requests page setup, saved batches and the customer CSV template before posting tests. No additional schema migration is required for this service-registration fix.

The parser reads report values and does not run macros or external links. XLSX formulas are rejected; legacy binary XLS formula-origin detection is not asserted. Final sources must be trusted exports with independently signed closing totals.

## Reproduce locally

Requirements: .NET 10 SDK, SQL Server MSSQLLocalDB, Node for syntax checks and Python for HTTP smoke. SQL tests use GUID-named disposable databases with validated cleanup, not the installed application connection. Private source verification is optional and reads originals without storing them in Git.

```powershell
$env:NEXUSFLOW_RUN_LOCALDB_CUSTOMER_TESTS = '1'
# Optional only: set NEXUSFLOW_CUSTOMER_SOURCE_FOLDER to the private reports folder.
dotnet test NexusFlow.IntegrationTests
dotnet build NexusFlow.Web --no-restore
dotnet ef migrations has-pending-model-changes --project NexusFlow.Infrastructure --startup-project NexusFlow.Web --no-build
node --check NexusFlow.Web/wwwroot/js/finance/customer-migration.js
node --check NexusFlow.Web/wwwroot/js/master/customer-master.js
node --check NexusFlow.Web/wwwroot/js/treasury/receipts.js
node --check NexusFlow.Web/wwwroot/js/finance/cheque-vault.js
node --check NexusFlow.Web/wwwroot/js/reporting/ar-aging.js
git diff --check
```

The opt-in HTTP/UI host uses a fresh synthetic database, real controllers/views and test-only authentication. It clears installation configuration, uses ephemeral cookie keys and binds only to `127.0.0.1:5190`. Never deploy this harness or run its tests against another URL.

```powershell
$env:NEXUSFLOW_RUN_LOCALDB_CUSTOMER_TESTS = '1'
dotnet run --project tools/CustomerMigrationSmokeHost
# In another terminal, with the fresh host running:
python tools/CustomerMigrationSmokeHost/verify_api.py
# Optional browser: http://127.0.0.1:5190/smoke-login
# Stop and delete the synthetic database:
Invoke-RestMethod -Uri http://127.0.0.1:5190/smoke-stop -Method Post
```

The HTTP smoke rejects finance-only, treasury-only and read-only access; missing CSRF and fabricated Authorization headers; missing UAT approval; missing physical confirmation; and stale review hashes. A valid multipart upload posts exactly 4 items, 2 cheques, 1 journal and 0 receipts even after retry. Reconciliation/downloads succeed, sign-off disables reactivation and reversal. Source-file fixtures contain no real business data.

## Production acceptance still required

Deploy and rehearse on a restored staging database, complete MIG-CUST-01 through MIG-CUST-15 using actual authorized roles, refresh both exports to 30 September, verify all customer and branch mappings, physically reconcile the cheques, and approve the closing balances. Posting remains disabled by default. Follow [Customer Opening Migration Runbook](../migration/Customer_Opening_Migration_Runbook.md).

The [saved screen](screenshots/customer-opening-migration.jpg) is from synthetic local testing, not the hosted production system.
