# NexusFlow ERP UAT Test Plan

> Current opening-import workflow: [Migration Centre](../migration/Migration_Centre_Runbook.md), `/Migration`. The earlier separate raw-material/customer-import sections describe historical releases; their page routes redirect to Migration Centre and their posting APIs retire once a central project exists.

## Objective

Validate financial-period controls, number-sequence health, cheque lifecycle accounting, controlled document output, supplier-bill payment routing, and job-work production before release.

## Environment

- Application: hosted UAT/staging system
- Local verification route: `/Inventory/ProductionOrders`
- Database: restored UAT copy with migration `FinancialControlsDocumentsAndProduction` applied
- Browsers: current Chrome and Edge, desktop and mobile-width checks
- Roles: `SuperAdmin`, `Admin`, `Accountant`, `StoreKeeper`, `SalesRep`

## Entry Criteria

- A current open financial period and one closed period exist.
- Required account mappings and number sequences pass `/api/config/health`.
- UAT users have only the permissions listed in `UAT_Roles_And_Permissions.md`.
- Test products, warehouses, contractor, customers, supplier bills, and cheques are loaded.
- No test uses production customer, banking, or payroll data.

## Execution Order

1. Financial periods and number-sequence health
2. Cheque receipt, endorsement, clearance, and dishonor
3. Print, PDF, history, and supplier-payment deep link
4. BOM revisions and production orders
5. Full regression: PO to GRN to bill, SO to invoice, inventory, treasury, and reporting

## Evidence

For each case retain the document number, screenshots, generated PDF hash, journal reference, and pass/fail result. Accounting cases must include debit and credit totals.

## Exit Criteria

- All critical and high-risk cases pass.
- No unbalanced journal is created.
- No transaction posts outside an open financial period.
- No issued WIP cost is capitalized more than once.
- Any item marked **Needs business confirmation** has a signed business decision.

## Automated Baseline

`dotnet test NexusFlow_ERP.sln -c Release` passes 30 tests. Authenticated browser smoke testing still requires a UAT login; the unauthenticated route and return URL were verified.

## Raw Material Import Addendum — 2026-09-15

Scope: `/Inventory/RawMaterialImport` and `/api/inventory/raw-material-import/{setup,template,preview,execute}`. Existing legacy import behavior is unchanged. No application migration, SQL insert script, deployment, or live stock import is part of this implementation.

The previously inspected hosted Product Catalog had no CSV importer; `WH-MAIN`, `UNBRANDED`, and Pieces were present. The hosted `ACCOSORIES NEW` category used Finished Goods inventory and must not be reused without an authorized accounting review. The new route has not been deployed or accepted on that host.

Local evidence: 23 parser/handler tests, three opt-in SQL Server LocalDB tests (including forced journal-failure rollback and concurrent same-file posting), four client state tests, and direct HTTP smoke tests of actual Razor rendering, multipart upload, CSRF, permissions, preview, posting and replay rejection. The corrected user CSV passed a read-only local endpoint preview: 444 rows, 6,306,283 PCS, value 29,727,743.70. Only a synthetic two-row fixture was posted to the disposable test database.

Outstanding gate: browser/visual and hosted acceptance tests. Browser control failed to initialize twice; HTTP and client state checks do not substitute for visual verification. Execute the RM cases in `UAT_Test_Cases.md` after deployment, record reference numbers and screenshots, and verify totals before real import.

Developer commands:

```powershell
dotnet test NexusFlow.IntegrationTests/NexusFlow.IntegrationTests.csproj --filter FullyQualifiedName~RawMaterialImportTests
$env:NEXUSFLOW_RUN_LOCALDB_IMPORT_TESTS='1'
dotnet test NexusFlow.IntegrationTests/NexusFlow.IntegrationTests.csproj --filter FullyQualifiedName~RawMaterialImportLocalDbTests
node --test NexusFlow.IntegrationTests/raw-material-import.client.test.cjs
```

The opt-in LocalDB tests create and remove GUID-named synthetic databases on `(localdb)\MSSQLLocalDB`; they do not use application connection settings. See `docs/uat/Raw_Material_Import_Verification.md` for the reusable local HTTP host.

## Customer opening migration — 5 October 2026

Scope: `/Finance/CustomerMigration`, customer legacy-code CSV import, opening debt/return credits, InSafe cheques, receipt settlement, cheque processing, statements and AR ageing. Stock is excluded. See [cutover runbook](../migration/Customer_Opening_Migration_Runbook.md).

Preconditions: deployed schema and application; restored disposable UAT database; valid customer/account/bank masters; open period; no customer finance postings or preloaded AR/Undeposited control balances. Test with synthetic data first. Posting must remain disabled until authorized UAT approval; outgoing notifications must be controlled by the UAT environment operator.

Entry: closing reports use the same day immediately before cutover; reviewers have both migration permissions. Exit: every case MIG-CUST-01 through MIG-CUST-15 passes, customer totals and cheque custody reconcile, no sales/revenue/tax/stock duplication, and business sign-off is recorded. Failure: keep migration posting disabled, retain evidence and repair/retest the affected path.

Local implementation verification is recorded separately in [Customer_Migration_Verification.md](Customer_Migration_Verification.md). The hosted URL reached **Sign In**; authenticated hosted UAT and production-role assignment remain **Needs business confirmation**. Local automated passes are not hosted business approval.

## Migration Centre acceptance — 6 October 2026

This release replaces separate opening-import navigation with **Administration → Settings & Security → Migration Centre**, `/Migration`. Earlier RM/MIG-CUST sections describe their original releases. Once a central project exists, legacy posting APIs are retired; use the MC cases below for new cutover work. Existing readonly batch history is retained.

Scope: saved company project, twelve datasets, ZIP/individual uploads, exact mappings, sparse masters, selected whole-party/SKU/bank imports, later closing files, approvals, durable runs/resume, independent scope release, residual trial balance, final read-only sign-off, and normal settlement/stock use. Detailed historical transactions, open orders, BOM/WIP and detailed asset/loan/tax registers are outside this release.

Preconditions: application plus `20261006160821_AddMigrationCentre` deployed; clean disposable ledger or supported same-date customer-batch adoption; open period; real account/master dictionaries and secure original-file storage; working Hangfire default queue; synthetic data from `UAT_Test_Data.md`; migration permission test accounts. Control outgoing messages in UAT through existing environment settings.

Execute MC-01 to MC-22, then normal receipts, supplier payments, cheque clearing/dishonour and FIFO consumption against released scopes. Retain normalized previews, originals/revisions, approval evidence/hash, run/unit/origin IDs, journal totals, independent reports/counts and screenshots. Rehearse backup restoration and schema upgrade before hosted deployment.

Exit: every critical MC case passes, each financial scope has a complete independent opening reconciliation, all closing GL accounts reconcile, no historical revenue/payment/stock is duplicated, and the business signs the actual closing data and date. Awaiting Data may support partial work but cannot satisfy final financial sign-off. Local automated verification is documented in [Migration_Centre_Verification.md](Migration_Centre_Verification.md); hosted authenticated acceptance remains pending.

## Continue after finalized opening migration

After the administrator verifies the finalized baseline and starts the updated dashboard build, execute ordinary receipts, payments, credit applications, stock operations and cheque/bank tests against the opening documents. Keep source evidence read-only. The measured baseline, sequencing, expected results and mapping issues needing business confirmation are in [Post_Finalization_Check_20261015.md](../migration/Post_Finalization_Check_20261015.md). A finalized UAT project does not approve a production closing.