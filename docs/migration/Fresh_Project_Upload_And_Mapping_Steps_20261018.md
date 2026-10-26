# Create the fresh pilot project, upload and save mappings

These steps replay the existing pilot package after the requested reset. They use the application source and the earlier authenticated hosted review. The browser tool could not reconnect on 18 October, so a fresh UI cross-check was unavailable. No hosted project, upload or setting was changed while preparing these instructions.

## 1. Create the project

After the reset returns `COMMITTED`, restart the application and its background workers. Sign in with the assigned Migration permissions and open [Migration Centre](https://nexus.garnetnation.lk/Migration).

Select **New Project** and enter:

| Field | Value for this same pilot package |
| --- | --- |
| Project name | NexusFlow Pilot Fresh Start |
| Stable source system identifier | TESTTTT |
| Actual go-live opening date | 2026-10-01 |
| Existing customer batch adoption evidence | Leave blank for the clean target |

Select **Create Project**. The summary must show opening **2026-10-01** and legacy closing **2026-09-30**. These are the package's test dates; changing them to today's date would conflict with its files. The source identifier must match the manifests. Use this one project for every stage; its ID can differ from 2.

## 2. Upload the masters, without executing

Extract [NexusFlow_Pilot_Package_20261015.zip](NexusFlow_Pilot_Package_20261015.zip). The full handoff ZIP is not an upload package. The current local upload ZIPs are also available directly in [stage_packages](stage_packages).

In **1. Upload closing data**, choose `stage_packages/01_Masters.zip` under **Or upload a ZIP package with manifest.json**, then select **Upload Package**. Expect Accounts, Customer Master and Supplier Master, revision 1. Uploaded source row counts are 39 accounts, 896 customers and 215 suppliers. Uploading stages records; it does not post balances.

Before the next step, upload only this first package. Do not execute an import or select **Save Mappings and Evidence** yet. A fresh project's three uploaded modules produce version 3, which the mapping SQL verifies.

## 3. Save the checked mappings using SQL

Open [Pilot_Save_Mappings_New_Project_20261018.sql](Pilot_Save_Mappings_New_Project_20261018.sql) in SSMS, connected to **versalapps.database.windows.net / NexusGarnetProd**, and execute the **whole file once**.

The script selects the sole fresh project's ID automatically; no edit to `@ProjectId` is needed. It checks source, dates, Preparing status, version 3, the three file hashes, no active run/posted unit, and the checked master IDs/codes/active states. If a check fails, retain the exact error and run [Pilot_New_Project_Mapping_Checks_20261018.sql](Pilot_New_Project_Mapping_Checks_20261018.sql); do not bypass its expected state.

The SQL saves mappings inside the existing project's `SettingsJson`. It saves 84 mappings: 17 pilot bank-branch mappings, 28 categories, 33 existing-SKU aliases, and Brand/CustomerGroup/SupplierGroup/PaymentTerm/PriceLevel/Unit. It also saves these ten defaults:

| Default | Checked target |
| --- | --- |
| ReceivableAccount | 1120 / ID 1001 |
| RevenueAccount | 4110 / ID 9 |
| PayableAccount | 2110 / ID 2001 |
| ExpenseAccount | 5110 / ID 5001, retained pilot expense treatment |
| CustomerGroup | TRADE / ID 11 |
| SupplierGroup | DOM / ID 12 |
| PriceLevel | STANDARD TIER / ID 13 |
| CustomerPaymentTerm | NET 30 DAYS / ID 14 |
| SupplierPaymentTerm | NET 30 DAYS / ID 14 |
| StockUnit | Pcs / ID 1 |

Source `NET30` explicitly maps to ID 14. Opening equity is account **3200 / ID 3002** and supplier terms are **30 days**. Existing column profiles, dataset states and unrelated settings are preserved. Fresh migration/system audit rows are inserted, prior approval is cleared and version advances from 3 to 4. The script does not create or edit master records or post anything.

Require result **SAVED FRESH PROJECT MAPPINGS**, `MappingsSaved=84` and `Version=4`. Repeated execution is refused to prevent duplicate audit writes. **Reload the whole browser page with F5 after the SQL succeeds** before saving any UI settings. The existing browser form otherwise contains the old settings.

## 4. Complete the unresolved choices and stock evidence

**20 October update from the user's SSMS results:** the fresh project is **3 / Data Migrate**, and the first mapping script returned **Version 4 / 84 mappings / MigrationAudit ID 516**. The current targets are **warehouse 2 / WH_MAIN / Main Warehouse / active / no inventory override** and **account 5027 / 1151 / Cheque in Hand**, returned by the active Asset posting-account query. These results supersede the earlier missing-warehouse and Salary Advances account-name observations below. Account 5023 / 1113 is a separate Undeposited Funds account; the supplied pilot files explicitly use 1151.

Use [Pilot_Complete_Project3_Warehouse_And_Cheque_Mappings_20261020.sql](Pilot_Complete_Project3_Warehouse_And_Cheque_Mappings_20261020.sql) once against the confirmed hosted database. It checks the reported project at Preparing/version 4, revalidates both masters, rejects active runs, financial-stage units and conflicting saved choices, and saves `Mappings.Warehouse.WH_MAIN=2`, `Defaults.StockWarehouse=2` and `Defaults.SafeAccount=5027`. Existing master-stage units may remain. It preserves all other settings and writes fresh migration/system audits. Expected result: **SAVED WAREHOUSE AND CHEQUE SETTINGS / ProjectId 3 / Version 5 / WarehouseMapping 2 / StockWarehouse 2 / SafeAccount 5027**. If the version has changed after other UI saves, retain the error and inspect the current project state instead of bypassing the guard. Reload the whole browser page with F5 after success and validate again before approval. The assistant has not executed this against the hosted database.

The following paragraph records the earlier blocker; it is resolved by the supplied 20 October master results and the follow-up save above. Category posting setup and evidence still need review before Stock.

Earlier hosted results found no `WH_MAIN` warehouse, and account `1151` was **Salary Advances Receivable**. The SQL leaves Warehouse, StockWarehouse and SafeAccount unset. These are **Needs business confirmation** using actual destination masters. The read-only check above lists warehouses with their overrides and active Asset posting accounts.

- Map source `WH_MAIN` to the intended actual warehouse and choose the same approved default warehouse. Check stock control component codes if the destination warehouse code differs from `WH_MAIN`.
- Select the genuine undeposited cheque account. The supplied ReceivedCheques and Accounts/TrialBalance files explicitly use `1151`; a different default alone does not correct those file references. Resolve these differences before Cheques/Stock/Trial Balance.
- Verify Accessories posts to **1131** and finished-goods categories to **1132**. The chosen warehouse must not override both into one inventory account for this pilot.

At **2. Map and confirm source treatment**, enter the accepted pilot evidence:

| Field | Retained pilot treatment |
| --- | --- |
| Summarized stock layer approval | UAT fixture: approved summarized layer per SKU in WH_MAIN at 2026-09-30; original FIFO dates not claimed. |
| Legacy finished goods valuation | Average cost (avar × warehouse quantity) |
| Valuation discrepancy resolution evidence | UAT uses approved avar; original costval difference -3173818.42 LKR retained in archive; not production sign-off. |
| Approved supplier terms in days | 30 |

The supplied canonical headers need no legacy column profile. Uploaded modules remain **Provided**; later modules remain **Awaiting Data** until uploaded. All twelve modules are supplied, so no Not Applicable declaration is needed. Select **Save Mappings and Evidence** after reviewing the actual choices and evidence. The known stage-1 masters can be validated first; unresolved cheque/stock/TB setup must be resolved before those stages are executed.

## 5. Import each stage in order

| Order | ZIP | Select only these modules |
| --- | --- | --- |
| 1 | 01_Masters.zip | Accounts, Customer Master, Supplier Master |
| 2 | 02_Party_Openings.zip | Customer Openings, Supplier Openings |
| 3 | 03_Cheques.zip | Received Cheques, Issued Cheques |
| 4 | 04_Combined_Stock.zip | Stock |
| 5 | 05_Banks.zip | Bank Opening, Bank Recon |
| 6 | 06_Control_Totals.zip | Control Totals |
| 7 | 07_Trial_Balance.zip | Trial Balance, only after all opening scopes are released |

For each stage, upload its ZIP once; stage 1 is already uploaded. At **3. Select this import**, uncheck other modules and select only the table's modules. Keep Customer codes, Supplier codes, SKU codes and Bank account codes blank. Select **Validate Selected Import**, resolve every error, review the result and enter truthful **Approval evidence**. Select **Approve Selected Import**, then **Execute Approved Import** once. Use **Refresh Progress** until completed before moving to the next stage. A changed selection/file/mapping requires validation and approval again.

Keep finished goods and raw materials in the one combined Stock file. Do not upload a separate Stock file for each; another upload replaces its current revision. The test bank/issued-cheque/Trial Balance fixtures retain the earlier approved pilot treatment.

After stage 6 completes, select **Customer Master, Supplier Master, Stock and Bank Opening**, enter the accepted **Signed independent schedule evidence**, and select **Release Selected Reconciled Scopes**. Resolve every pending scope. Then import stage 7, select **Check Full Ledger** and require zero differences. Enter **Final financial sign-off evidence** and select **Finalize Migration** after pilot acceptance.

## Verification of the new SQL

The new-project variant uses the previously checked pilot IDs and validates them again at execution. Its three uploaded-file hashes match the supplied CSVs. **All 13 local checks passed; the build had zero warnings and zero errors.** Local tests use a uniquely named disposable LocalDB database, including project ID **17** to verify automatic selection rather than the old ID 2. The tests cover 84 mappings/10 defaults, settings preservation, dual audit, repeated execution, changed source/version/uploads, multiple projects, finalized/active/posted states, inactive or wrong-type masters, conflicting settings and atomic rollback on audit failure. Output: `artifacts/pilot-mapping-sql-check-20261018/test-results.json` and `test-output.txt`.

No hosted SQL has been executed by the assistant. Application source files were not changed.

The 20 October follow-up SQL passed **10 local checks**, including preservation of all 84 existing mappings and evidence, acceptance after a completed master import, active-run/financial-posting/conflicting-choice/version guards, and rollback of the project plus both audit writes on an audit failure. The build had zero warnings and zero errors. The uniquely named disposable LocalDB fixture was removed after verification; no hosted database was contacted. Output: `artifacts/pilot-final-mappings-20261020/test-results.json` and `test-output.txt`.
