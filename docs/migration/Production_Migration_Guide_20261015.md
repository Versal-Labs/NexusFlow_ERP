# Production migration: NexusGarnetProd — 15 October 2026

Target confirmed by the user: `https://nexus.garnetnation.lk/`, Azure SQL server `versalapps.database.windows.net`, database `NexusGarnetProd`. This is production. Do not apply UAT fixture assumptions to this migration.

## Observed position and authorized cleanup

The user's read-only audit returned no Migration Centre projects, posted units or active runs. It returned 12 journals: Inventory 3 (13–16 September), Production 6, Purchasing 1, Sales 1, Treasury 1 (16 September), plus one payment. There were no allocations to imported openings, customer/supplier credit applications or cheque-register rows. The user explicitly confirmed these listed records are disposable test data.

The cleanup must remove the associated current sales/purchasing/production/stock/treasury positions as a coherent set, not only the 12 journal headers. The proposed full scope is shown by PREVIEW in [Production_Test_Data_Reset_20261015.sql](Production_Test_Data_Reset_20261015.sql). Review every nonempty row before proceeding: the earlier audit did not enumerate all draft documents or stock layers. Stop if any proposed record population is unrecognized or genuine business data.

The script preserves users, roles, company/system settings, account definitions, customers, suppliers, products/SKUs and their pricing/cost masters, BOM definitions, categories, units, warehouses, banks/branches, financial periods, number sequences, audit records, HR/payroll and Hangfire definitions/history. It resets only account balances and the explicitly listed current transaction tables. Temporal versioning stays enabled, existing histories remain, and deletes/updates add normal history. Physical uploads, instance credentials, keys and installation files are not deleted. Preserving masters means subsequent imports link exact existing codes; they do not silently overwrite existing contact details.

## Administrator: clear the confirmed test position

1. Open the full reset SQL in Azure Data Studio. Verify the connection is **versalapps.database.windows.net / NexusGarnetProd**. Run the entire file with `@Mode='PREVIEW'`. Set the query timeout to at least five minutes before rehearsal/commit. This changes no application rows. Review `CurrentRowsProposedForRemoval` for all 45 named tables and any job-state summary. Do not remove unexpected-table/schema guards.
2. Arrange a maintenance window and stop users, integrations, the existing IIS site **and its application pool**, and every other worker writing to the database. Keep them stopped through rehearsal, commit and verification.
3. Create and verify a recoverable copy of the actual production database. In Azure portal, open **SQL databases → NexusGarnetProd → Copy**, use a unique name such as `NexusGarnetProd_PreMigration_20261015_01`, and wait until the copy is complete/Online and can be opened. Record the exact database-copy/recovery reference. Azure copies are independent, transactionally consistent databases; they can incur additional Azure charges. [Microsoft database copy instructions](https://learn.microsoft.com/en-us/azure/azure-sql/database/database-copy?view=azuresql). Keep a protected backup of the existing IIS application/configuration, instance state, encrypted secrets, data-protection keys and actual storage roots. Do not replace or repoint the production database connection.
4. Edit the five control declarations at the top of the **full script**, then run the whole file:

```sql
DECLARE @Mode varchar(10) = 'REHEARSE';
DECLARE @BackupReference nvarchar(400) = N'YOUR_VERIFIED_COMPLETED_RECOVERY_REFERENCE';
DECLARE @WritersStopped bit = 1;
DECLARE @ReviewedTestData nvarchar(80) = N'ALL_REVIEWED_BUSINESS_ROWS_ARE_TEST_DATA';
DECLARE @RehearsalHash varchar(64) = '';
```

5. Require `Result = REHEARSED_ROLLED_BACK`, no errors, zero `RowCountAfter` for the cleared current tables and successful retained-table fingerprint verification. This is a rehearsal: the original rows are restored by rollback. Save its 64-character `SnapshotHash` and output. A failed guard is a stop point, not an instruction to bypass the guard. Pending/failed background jobs, account/ledger discrepancies, retained dependencies and schema differences need review.
6. In the same full file set `@Mode='COMMIT'` and paste that hash into `@RehearsalHash`. Keep the other three values. Run the **entire file once**. Require `COMMITTED` and successful preservation verification. A changed input position is rejected. The script intentionally refuses another reset after the reviewed 12/1 baseline has been removed.
7. Verify through a new query/connection:

```sql
SELECT COUNT_BIG(*) AS Journals FROM Finance.JournalEntries;
SELECT COUNT_BIG(*) AS JournalLines FROM Finance.JournalLines;
SELECT COUNT_BIG(*) AS Payments FROM Finance.PaymentTransactions;
SELECT COUNT_BIG(*) AS StockLayers FROM Inventory.StockLayers;
SELECT COUNT_BIG(*) AS StockTransactions FROM Inventory.StockTransactions;
SELECT COUNT_BIG(*) AS MigrationProjects FROM Migration.MigrationProjects;
SELECT COUNT_BIG(*) AS AccountsWithBalance FROM Finance.Accounts WHERE Balance<>0;
```

All seven counts must be zero. Review the reset's retained-table report and confirm settings/masters through the application after restarting the same site/pool. Keep ordinary users and integrations paused during preparation/import. The script has no production connection credentials and has not been executed against Azure by Codex. If a query is cancelled, check `SELECT @@TRANCOUNT;` in that same session and issue `ROLLBACK;` there if it is nonzero before doing anything else. Do not treat a cancelled query as a completed reset.

## Production file readiness

**The previous `uat_upload_20261012` package is not a production package.** It contains synthetic bank/statement/issued-cheque/reconciliation positions, a synthetic balanced trial balance and LKR 1,000,000 rent/revenue residual, fabricated UAT master names, substitute branches, test dates and temporary classifications. Do not upload its stage ZIPs or CSVs into production. A successful UAT run is functional evidence, not financial approval.

The last approved opening date was **2026-10-01**, requiring certified balances at **2026-09-30**, LKR, Asia/Colombo. Keep that date only with the full 1 October onward catch-up plan. A changed actual cutover requires refreshed exports and corresponding project/file dates; do not relabel old snapshots.

Current production preparation is held for these reasons:

| Position | Existing source issue | Required production input |
|---|---|---|
| Customer outstanding | Actual ageing snapshot 28 September | Complete certified 30 September unsettled items, including offsetting zero-net parties |
| Received cheques | Snapshot 28 September | Certified physical/ledger closing schedule, actual branches, statuses and reflection flags |
| Raw stock | Snapshot 13 September | Certified closing quantities, units, costs and warehouse count |
| Finished stock | Closing date uncertified | Certified closing quantity/value using the approved avar basis or reviewed corrected costs |
| Supplier outstanding | Ageing implies 7 October; later documents present | Complete 30 September remaining balances; filtering October rows alone cannot recreate the earlier balance |
| Trial balance | Uncertified date; credits exceed debits by LKR 613,120 | Corrected, full, balanced closing TB and approved chart mapping; no equity/suspense plug |
| Bank/issued cheques/reconciliation | Actual schedules not supplied | Real book/statement/uncleared schedules or signed genuine zero declarations |
| Control totals | UAT values are synthetic/derived | Independent signed closing control schedule and document counts |

Resolve the missing genuine party/SKU masters and supplier expense assignments. Head-office substitute branch mappings and generic MSH/UNBRANDED/PCS classifications used in UAT require actual production classification. Keep original reports and complete pre-cutover legacy transaction/document exports in the read-only legacy archive.

## Administrator: migrate through Migration Centre

1. Sign in to the production site with an account having the required **Migration View, Prepare, Approve, Execute and Finalize permissions**. Open **Administration → Settings & Security → Migration Centre**, `/Migration`. Select **Download Template Pack** and compare current deployed headers. The authenticated hosted workflow could not be browser-verified here; button names below are checked against source.
2. After reset verification, select **New Project**. Enter a meaningful production project name, a stable identifier for the real legacy source, and **Actual go-live opening date = 2026-10-01** under the confirmed plan. Select **Create Project**. Expected closing is 2026-09-30. Leave adoption evidence blank for this cleaned target. Do not use the old local project ID or assume the UAT ZIP's source identifier matches.
3. Upload genuine **Customer Master** and **Supplier Master** files. Upload **Accounts** only if missing posting accounts require an approved account dataset; folders belong in normal account maintenance. Use **Dataset → File → Upload Dataset**, one canonical CSV or values-only workbook per dataset. Uploading stages rows and changes no balances. UTF-8 CSVs use exact headers, text legacy codes, yyyy-MM-dd dates and numeric values.
4. In **Map and confirm source treatment**, add exact code-to-master mappings. These mappings must be explicitly saved even when codes match:

| Mapping type | Source code | Production selection |
|---|---|---|
| CustomerGroup | TRADE | Actual Trade lookup |
| SupplierGroup | DOM | Actual Domestic Suppliers lookup |
| SupplierGroup | A/B/C/G/S, if present in the production file | DOM, per the user's confirmed treatment |
| PaymentTerm | NET30 | Actual 30-day term; never COD |
| PriceLevel | STANDARD TIER | Actual Standard Tier lookup |
| Warehouse | WH_MAIN | Main Garnet, if the production code/master matches |
| Unit | Every source unit, PCS where applicable | Actual base unit and approved conversion |
| Category | Every source category, including ACCESSORIES/Accessories | Correct category with active inventory posting account |
| Brand | Every source brand, UNBRANDED only where approved | Correct production brand |
| BankBranch | Every actual source bank/branch code | Actual branch; do not reuse UAT head-office substitutes |

5. Verify active posting accounts and types in production: receivables Asset, payables Liability, sales Revenue, supplier expenses Expense, inventory/safe/bank Asset, opening equity Equity. Previous candidate codes were 1120 AR, 2110 AP, 4110 revenue, 1131/1132 inventory, 1151 safe, 3200 opening equity. These are candidates, not newly verified Azure mappings. Use **3200 — Retained Earnings** only if the current configuration and accountant approve it. Do not apply 5110 indiscriminately to every supplier. Confirm warehouse inventory overrides: each stock row's InventoryAccountCode must equal the account resolved from warehouse/category. A category mapping alone does not fix a missing/inconsistent posting account.
6. Select the customer/supplier defaults TRADE/DOM, the real 30-day payment term, STANDARD TIER and approved posting defaults. Enter supplier terms **30 days**, warehouse WH_MAIN, summarized-layer approval and the approved avar valuation/evidence where applicable. Missing source units require a real approved base unit/conversion. Select **Save Mappings and Evidence**. Canonical headers do not need old raw-column profiles; remove conflicting profiles. Numeric master IDs are local to the destination and never replace file Code fields.
7. For each import batch: select only the intended modules, leave code filters blank for all rows, select **Validate Selected Import**, resolve all errors, review warnings and proposed journals, enter **Approval evidence**, select **Approve Selected Import**, then **Execute Approved Import**. Use **Refresh Progress**. A failed run retains completed units; inspect the error and use **Resume Run** after fixing the cause. File/mapping/selection changes require validation and approval again.
8. Use this posting order:
   - Approved Accounts if needed, Customer Master, Supplier Master.
   - Customer Openings and Supplier Openings.
   - Received Cheques and Issued Cheques.
   - One complete **Stock** dataset containing both finished goods and raw materials; another Stock upload replaces the current dataset revision.
   - Actual Bank Opening and Bank Recon.
   - Independent Control Totals.
   - Trial Balance only after opening scopes are reconciled and released.
9. Preserve business treatments: customer Amount is **creamt**, never debamt minus creamt; positive Invoice and negative ReturnCredit retain signs; due dates are original document date plus 30 days. Supplier negatives are returns/Credit, positives remain Bill per confirmation, with approved 30-day due dates. Keep every unsettled document, including offsetting debts/credits for zero-net parties. Preserve permanent IDs or supported stable composite identities, never Excel row numbers. Genuine missing contacts may remain blank with completion flags.
10. Received cheques are a separate schedule. Apply InSafe / PartyReflected=true / BankReflected=false only where the refreshed closing schedule supports the confirmed treatment. Do not add cheque amounts to invoice outstanding or recreate historical receipts. Use actual bank book/statement and uncleared positions, without counting cheque items twice.
11. Upload and import the independent **Control Totals** schedule. Select **Customer Master, Supplier Master, Stock and Bank Opening** to cover all scope kinds. Enter **Signed independent schedule evidence**, then **Release Selected Reconciled Scopes**. Resolve every Pending reason. Individual **Reconcile** / **Release Reconciled Scope** controls must have zero differences. Master-only and absent components need genuine zero controls; missing schedules stay Awaiting Data unless a signed Not Applicable declaration is justified.
12. Upload the full corrected **Trial Balance**, including all active posting accounts and genuine zeros. Validate/approve/execute Trial Balance only. Do not manually subtract prior opening contributions; the importer calculates its residual. Select **Check Full Ledger** and require zero differences account by account. All current rows must be imported and scopes released. Enter actual financial sign-off evidence and select **Finalize Migration**. The project then becomes read-only.

## Ordinary operations after financial acceptance

Restart/resume normal entry only under the cutover plan. Reconcile AR/AP reports, stock valuation, physical cheques, bank book/statement bridge and GL to the signed closing sources. If opening remains 1 October, transfer and reconcile **every transaction recorded only in the old system from 1 October to the actual operational switch** through controlled normal workflows. Do not import a newer outstanding snapshot on top of the original opening and then replay the same movements.

Normal customer receipts use **Incoming Payments (AR)** / **Customer Receipts**, `/Treasury/Receipts`: select the customer and the imported positive opening Invoice, allocate the actual payment and confirm it once. Do not record an imported received cheque as another AR receipt. Supplier payments, cheque deposits and stock operations follow normal released-entity workflows. Keep test transactions in a separate UAT environment.

## Client pilot replay — 15 October 2026

The user has requested a replay of the finalized local test data for a client pilot, including its disclosed fixtures. Use the [updated pilot package and stages](pilot_upload_20261015/README_START_HERE.md). Its bank, issued cheque and TB assumptions are for functional testing; the real financial cutover guidance above still requires certified source balances. Preparing the package does not clear or import the hosted database.
