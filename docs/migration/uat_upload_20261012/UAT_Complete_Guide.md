# Complete NexusFlow UAT import guide

Use the files from uat_upload_20261012. Work through Stages 1–7 in order. Test fixture assumptions and expected balances are recorded in UAT_Changes_and_Reconciliation.md.

## Stage 1 — project and staging

Operator needs Permissions.Migration.View and Permissions.Migration.Prepare. Use an isolated UAT company with the required account and lookup codes. Current local saved project: test, ID 3, SourceSystem TESTTTT, Preparing. Its older staged customer revisions will be replaced by these complete UAT inputs.

1. Sign in and open Administration → Settings & Security → Migration Centre (/Migration).
2. Choose test under Saved migration project. Verify opening 2026-10-01 and closing 2026-09-30. Upload Dataset uses the saved project closing automatically; there is no separate closing-date field on this page.
3. For a fresh UAT company without a project, select New Project. Enter Project name NexusFlow UAT 2026-10-12, Stable source system identifier TESTTTT and Actual go-live opening date 2026-10-01, then Create Project. The importer permits one company project. A Finalized project is read-only; use a separately provisioned UAT company for a new cycle. This package does not reset a database.
4. Select Download Template Pack if the destination version differs and compare the headers. Confirm the Stage 2 master targets exist.
5. Stage stage_packages/01_Masters.zip through Or upload a ZIP package with manifest.json → choose ZIP → Upload Package. Alternatively select Dataset and File, then Upload Dataset for Accounts.csv, CustomerMaster.csv and SupplierMaster.csv individually.
6. Use Stage 2 to save mappings, then Stage 3 to import masters. Stage 4 uses 02_Openings_and_Controls.zip. Stage 6 uses 03_TrialBalance.zip only after all scopes are released.
7. Uploading stages rows and changes no ledger balances. Do not upload the full delivery ZIP, report CSVs or UAT_Review.xlsx as a dataset. Do not upload both CSV and Excel as two batches.

Expected: Accounts 39, Customer Master 896, Supplier Master 215 staged. Ledger unchanged. Included modules become Provided on upload. Trial Balance remains Awaiting data until Stage 6. All financial modules are supplied in this UAT case; no empty-file or NotApplicable declaration is needed. The ZIP SourceSystem must equal the saved project identifier. If it differs, use the individual CSVs.


## Stage 2 — exact mappings and defaults

Select destination masters by code/name. Local numeric IDs are reference values and may differ in another UAT environment. Template Code fields contain actual master/legacy codes, never IDs.

## Required mappings

For each row select Mapping type, enter Exact legacy code, choose NexusFlow master and select Add Mapping. Code comparison is uppercase; Unit PCS covers both Pcs and pcs.

| Mapping type | Exact legacy code | Local target ID | Target label | Basis |
|---|---|---|---|---|
| CustomerGroup | TRADE | 13 | TRADE — Trade | Required explicit mapping. |
| SupplierGroup | DOM | 15 | DOM — Domestic Suppliers | Required explicit mapping. |
| PaymentTerm | NET30 | 27 | NET30 — 30 Days | Required explicit mapping. |
| PriceLevel | STANDARD TIER | 10 | STANDARD TIER — Standard Tier | Required explicit mapping. |
| Warehouse | WH_MAIN | 3 | WH_MAIN — Main Garnet | Required on stock rows and controls. |
| Unit | PCS | 1 | pcs — Piece | PCS covers pcs and Pcs after code normalization. |
| Category | MSH | 1 | MSH — Men's Shirts | Temporary new-FG UAT classification. |
| Brand | UNBRANDED | 5 | UNBRANDED | Required for new FG fixture SKUs. |
| BankBranch | 7010-058 | 1 | 7010-001 — Bank of Ceylon / Head Office | UAT-only substitute branch; bank retained where available. Not physical-cheque verification. |
| BankBranch | 7010-501 | 1 | 7010-001 — Bank of Ceylon / Head Office | UAT-only substitute branch; bank retained where available. Not physical-cheque verification. |
| BankBranch | 7056-006 | 84 | 7056-006 — Commercial Bank of Ceylon / Kurunegala | Exact current bank/branch code. |
| BankBranch | 7056-152 | 79 | 7056-001 — Commercial Bank of Ceylon / Head Office | UAT-only substitute branch; bank retained where available. Not physical-cheque verification. |
| BankBranch | 7083-064 | 109 | 7083-001 — Hatton National Bank / Head Office | UAT-only substitute branch; bank retained where available. Not physical-cheque verification. |
| BankBranch | 7083-094 | 109 | 7083-001 — Hatton National Bank / Head Office | UAT-only substitute branch; bank retained where available. Not physical-cheque verification. |
| BankBranch | 7135-040 | 41 | 7135-001 — People's Bank / Head Office | UAT-only substitute branch; bank retained where available. Not physical-cheque verification. |
| BankBranch | 7135-123 | 41 | 7135-001 — People's Bank / Head Office | UAT-only substitute branch; bank retained where available. Not physical-cheque verification. |
| BankBranch | 7135-200 | 41 | 7135-001 — People's Bank / Head Office | UAT-only substitute branch; bank retained where available. Not physical-cheque verification. |
| BankBranch | 7135-329 | 41 | 7135-001 — People's Bank / Head Office | UAT-only substitute branch; bank retained where available. Not physical-cheque verification. |
| BankBranch | 7162-012 | 224 | 7162-012 — Nations Trust Bank / Kurunegala | Exact current bank/branch code. |
| BankBranch | 7278-929 | 159 | 7278-001 — Sampath Bank / Head Office | UAT-only substitute branch; bank retained where available. Not physical-cheque verification. |
| BankBranch | 7287-085 | 189 | 7287-001 — Seylan Bank / Head Office | UAT-only substitute branch; bank retained where available. Not physical-cheque verification. |
| BankBranch | 7311-085 | 287 | 7311-001 — Pan Asia Banking Corporation / Head Office | UAT-only substitute branch; bank retained where available. Not physical-cheque verification. |
| BankBranch | 7454-026 | 272 | 7454-001 — DFCC Bank / Head Office | UAT-only substitute branch; bank retained where available. Not physical-cheque verification. |
| BankBranch | 7463-008 | 319 | 7463-008 — Amana Bank / Kurunegala | Exact current bank/branch code. |
| BankBranch | 7463-021 | 312 | 7463-001 — Amana Bank / Head Office | UAT-only substitute branch; bank retained where available. Not physical-cheque verification. |

The MSH / UNBRANDED category/brand choices apply to the 81 new UAT FG SKUs. Existing SKUs keep their current category, brand, type and base unit. Fourteen bank-branch substitutes are explicitly UAT choices, not confirmation of physical cheque branches.

## Defaults and evidence

| UI field | Selection | Reason |
|---|---|---|
| Opening equity account | 3200 — Retained Earnings | Verified active posting account |
| Default customer control | 1120 — Accounts Receivable (AR) | Verified active posting account |
| Default customer revenue | 4110 — Sales Revenue - FG | Verified active posting account |
| Default supplier control | 2110 — Accounts Payable (AP) | Verified active posting account |
| Default supplier expense | 5110 — Raw Material Consumption | Temporary UAT expense |
| Undeposited cheque account | 1151 — Cheques In Hand | Verified active posting account |
| Default customer group | TRADE — Trade | Also map TRADE explicitly |
| Default supplier group | DOM — Domestic Suppliers | Also map DOM explicitly |
| Default price level | STANDARD TIER — Standard Tier | Also map explicitly |
| Default customer payment term | NET30 — 30 Days | Do not use COD |
| Default supplier payment term | NET30 — 30 Days | Do not use COD |
| Approved base unit when source unit is missing | pcs — Piece | Temporary UAT unit for new FG |
| Approved warehouse when source location is missing | WH_MAIN — Main Garnet | All stock rows populated |
| Approved supplier terms in days (optional) | 30 | Due dates already populated |
| Summarized stock layer approval | UAT fixture: approved summarized layer per SKU in WH_MAIN at 2026-09-30; original FIFO dates not claimed. | Paste evidence |
| Legacy finished goods valuation | Average cost (avar × warehouse quantity) | Final UnitCost already populated |
| Valuation discrepancy resolution evidence | UAT uses approved avar; original costval difference -3173818.42 LKR retained in archive; not production sign-off. | Paste evidence |

1. Select each default above. Supplier expense 5110 is temporary UAT treatment for unresolved suppliers. The two rent suppliers retain 6210 and the electricity supplier retains 6220.
2. Save explicit CustomerGroup TRADE, SupplierGroup DOM, PaymentTerm NET30 and PriceLevel STANDARD TIER mappings, even though codes match masters. NET30 is 30 Days. COD means cash on delivery and is not used.
3. Account codes auto-resolve to matching active posting accounts: 1120 AR Asset; 2110 AP Liability; 4110 Sales Revenue; 5110/6210/6220 Expense; 1131/1132 Inventory Asset; 1151 Safe Asset; 1112 Bank Asset; 3200 Equity. Do not select the 5000 folder or 5130 Expense as a revenue account.
4. Open Change source column mapping only if an old column profile exists. Remove conflicting entries or use Map Column with Required field and Source column both set to the identical canonical header. Do not let old debcode/creamt profiles replace populated canonical values.
5. Paste summarized-layer and valuation evidence from the table. Enter Approved supplier terms in days 30. Select Save Mappings and Evidence.
6. reports/UAT_Settings_reference.json shows the final local mapping/default reference. It was not sent to the application. Save settings through the wizard and validate.

Expected: all required code mappings resolve; default accounts have the right types; no COD mapping is used by these files.


## Stage 3 — validate and import masters

Precondition: Stage 1 staged and Stage 2 saved. Prepare permission permits validation; Approve permits approval; Execute permits execution. Actual role names must be checked in the destination.

1. In Select this import select only Accounts, Customer Master and Supplier Master. Uncheck financial modules and Control Totals.
2. Leave Customer codes, Supplier codes, SKU codes and Bank account codes blank to include every record.
3. Select Validate Selected Import. Expected rows: 39 accounts, 896 customers, 215 suppliers. Missing-contact and supported-composite-identity warnings are expected. Mapping, account-type, duplicate, invalid-email and field-length errors must be resolved.
4. Review missing-master UAT fixtures GA735, TK1039, UP1038, VI1041, SH331 and WL329. Names begin UAT; original codes remain text. No contact data is fabricated.
5. In Approval evidence enter: UAT fixture import only, package 2026-10-12. Temporary masters and expense defaults reviewed for functional testing; no production financial sign-off.
6. Select Approve Selected Import, then Execute Approved Import. Select Refresh Progress until completion. If a run fails, inspect the error; fix its cause and use Resume Run for unfinished work. Do not blindly post a second opening.
7. Confirm NET30, TRADE/DOM and STANDARD TIER. Accounts links existing current posting accounts; it does not import folders. Current customers/suppliers without legacy bindings are not matched by similar name.

Expected: all selected masters imported; no opening financial journal yet. Genuine blanks remain blank. Continue to Stage 4. Changes to files/mappings/selection require validation and approval again.


## Stage 4 — documents, cheques, stock and banks

Precondition: masters imported; all mappings saved; an open financial period covers 1 October 2026; no existing operational party/SKU activity or live stock layers. The checked local test database has none.

1. Upload 02_Openings_and_Controls.zip with Upload Package, or stage its eight individual CSVs with Upload Dataset. This includes all opening schedules and Control Totals. Trial Balance is saved for Stage 6.
2. Uncheck automatically selected modules. Select Customer Openings and Supplier Openings only, with all code filters blank. Validate Selected Import; review signs/dates/amounts; enter UAT evidence; Approve Selected Import; Execute Approved Import; Refresh Progress until complete.
3. Select Received Cheques and Issued Cheques only and repeat the validation/approval/execution cycle. The 49 received instruments are InSafe, PartyReflected=true, BankReflected=false, total 5328628.00. They add the safe opening without another AR receipt or bank movement. Cheque numbers, including leading zeros, remain text.
4. The issued fixture is AH143 / UAT-ISSUED-001 / cheque 000001 / 15000.00 / bank 1112. Both reflection flags are true. It creates a pending bank item already reflected in party and bank book; it does not pay AP again.
5. Select Stock only. Validate, approve and execute the complete 938-row Stock file: 494 FG plus 444 RM, all in WH_MAIN. Existing exact SKU masters are used. The 81 new FG SKUs use temporary MSH / UNBRANDED / pcs classification. LayerDate 2026-09-30 is the UAT summarized date. Another Stock file replaces this complete revision.
6. Select Bank Opening and Bank Recon only. Validate, approve and execute. Bank 1112: book 100000.00; statement 112500.00; deposit pending +2500.00; issued cheque pending -15000.00. Statement bridge: 112500 +2500 -15000 =100000.
7. Import Control Totals in Stage 5. Keep Trial Balance unselected until all opening scopes are released.

## Expected amounts

| Dataset | Positive LKR | Negative LKR | Net LKR |
|---|---|---|---|
| CustomerOpenings | 162436582.24 | -321597.10 | 162114985.14 |
| SupplierOpenings | 96073265.55 | -1883015.00 | 94190250.55 |
| ReceivedCheques | 5328628.00 | 0.00 | 5328628.00 |
| IssuedCheques | 15000.00 | 0.00 | 15000.00 |

Stock value 114417245.74 LKR. FG quantity 68986.0000; RM quantity 6306283.0000. These are different item populations, not a single physical unit measure. All legacy document amounts are retained. Five zero-net customer cases retain both debt and credit rows. Four post-closing supplier documents have test date 2026-09-30 and due date 2026-10-30; original dates are logged.

Do not bypass date/type/mapping/duplicate/live-activity errors. Review the exact cause. Warnings for genuine blanks and composite identities are expected; they are not errors.


## Stage 5 — controls and scope release

ControlTotals contains fixed expected UAT fixture controls. It is not an independent production closing certificate. Zero invoice/cheque components cover master-only parties without fabricating financial documents.

1. Select only Control Totals. Leave filters blank. Validate Selected Import, review 5212 rows, record UAT fixture control evidence, Approve Selected Import and Execute Approved Import. Wait for completion.
2. In Select this import select Customer Master, Supplier Master, Stock and Bank Opening. Uncheck Trial Balance. Leave all code filters blank. These modules select all four scope kinds for bulk release.
3. Under Release multiple reconciled scopes enter Signed independent schedule evidence: UAT-only fixed fixture controls, package 2026-10-12. All test positions and zero components reviewed; not production independent closing evidence.
4. Select Release Selected Reconciled Scopes. Expected: 2050 scopes Released and zero Pending. Read any pending reason and compare its current source, posted rows, control amount and count with ControlTotals.csv.
5. For a manual check select Reconcile for a displayed scope. Difference must be zero. Enter control amounts and row counts from the fixture, record Complete scope and control evidence, then Release Reconciled Scope.
6. Customer/Supplier Debit and Credit components use positive magnitudes, and BOTH use the entire party document count. Cheques are separate components. Stock uses Stock:WH_MAIN:Quantity and Stock:WH_MAIN:Value. BankOpening row count is 1; BankStatementBridge count is 2 (issued cheque plus deposit); BankRecon count is 1.
7. Released entities can trade. Complete initial TB/finalization checks before ordinary transactions for a straightforward first test cycle.

Expected scopes: 896 Customer, 215 Supplier, 938 Stock, 1 Bank. A master-only customer/supplier has zero expected amounts and RowCount 0 for absent components. No unsettled legacy document was removed to create those zeros.


## Stage 6 — test trial balance, ledger check and finalization

Precondition: every opening scope Released; opening modules and controls posted; financial period open; no new source/mapping changes. This UAT TB deliberately replaces the original unbalanced 188-account report with a balanced full 39-account test position.

1. Upload 03_TrialBalance.zip or TrialBalance.csv.
2. Select Trial Balance only; leave filters blank. Validate Selected Import. All 39 current active posting accounts are present, including 30 deliberate test-zero accounts. Debit = Credit = 282960858.88 LKR.
3. The TB includes preceding opening contributions. Do not manually subtract AR, AP, stock, safe or bank. The importer subtracts its own opening contributions. Expected residual: 6210 Rent Expense Debit 1000000.00 and 4110 Sales Revenue - FG Credit 1000000.00. Managed-control residuals must be zero.
4. Record Approval evidence: UAT synthetic TB from opening journal expectations plus balanced rent/sales residual pair; no production sign-off. Approve Selected Import, Execute Approved Import, Refresh Progress.
5. Select Check Full Ledger. All 39 account differences must be zero. Compare reports/Expected_GL.csv and UAT_Review.xlsx.
6. Check all twelve datasets Provided, every current row Already imported and all 2050 scopes Released. Run retry/idempotency checks before finalization.
7. Enter Final financial sign-off evidence: UAT functional acceptance only, package 2026-10-12; synthetic fixtures checked; no production migration approval. Select Finalize Migration after completeness and zero differences pass.
8. Expected: Finalized; migration editing/execution disabled; ordinary operations remain available for released entities. Use a separate UAT company for another full import cycle. This preparation does not reset or finalize the application.

The original trial balance and its -613120.00 imbalance remain archived unchanged. Production still requires certified closing data and approved legacy account mappings.


## Stage 7 — ordinary operations and acceptance checks

Record Pass / Fail, tester/date and evidence in UAT_Review.xlsx → UAT tests or reports/UAT_tests.csv. Preparation checks do not mean these application tests passed. Admin migration operators need Migration permissions. Normal users need actual sales/payment/stock/cheque permissions and released scopes. Destination role names are Needs business confirmation.

After the opening/TB tests:
- Receive a normal customer payment against a positive imported Invoice. Confirm outstanding reduces and a receipt posts once.
- Apply an imported ReturnCredit through the supported customer credit workflow. A credit is not collected as an invoice. Zero-net documents remain traceable.
- Pay a positive supplier opening Bill and apply a negative supplier Credit separately. Confirm no historical bill is recreated.
- Use normal stock issue/sale/receipt with a released imported SKU. Confirm on-hand, FIFO remaining quantity and journal accounts.
- Deposit an imported InSafe cheque. Confirm safe reduces and bank changes once, with no second AR reduction.
- Clear the synthetic issued cheque and deposit item through the supported opening-bank-item workflow. Confirm already-reflected bank book is not posted twice.
- Check /Migration access and API permission enforcement for a user without migration permissions.

| Test ID | Stage | Precondition | Steps | Expected result | Pass / Fail | Evidence / tester |
|---|---|---|---|---|---|---|
| UAT-01 | Project | Isolated UAT project | Select saved test project | Preparing; opening 2026-10-01; closing 2026-09-30 |  |  |
| UAT-02 | Staging | Preparing project | Upload Stage 1 package/CSVs | 39 accounts, 896 customers, 215 suppliers; no ledger posting |  |  |
| UAT-03 | Mappings | Masters staged | Save mappings/defaults | NET30 means 30 Days; DOM/TRADE mapped; no COD |  |  |
| UAT-04 | Masters | Clean validation | Validate, approve, execute masters | All masters imported; genuine missing-contact warnings remain |  |  |
| UAT-05 | Customer documents | Masters imported | Import CustomerOpenings | 1150 items, positive Invoice / negative ReturnCredit; net 162114985.14 |  |  |
| UAT-06 | Zero-net customer | Opening items posted | Inspect DC969, FA111, JY308, SA280, VI852 | Debt and credit documents both retained |  |  |
| UAT-07 | Supplier documents | Masters imported | Import SupplierOpenings | 180 items; positive Bill / negative Credit; net 94190250.55 |  |  |
| UAT-08 | Received cheques | Customer masters imported | Import and inspect cheque numbers | 49 InSafe instruments; zeros retained; safe 5328628.00; AR unchanged |  |  |
| UAT-09 | Issued cheque | AH143 imported | Import UAT-ISSUED-001 | 000001 / 15000 pending bank item; no second AP/bank payment |  |  |
| UAT-10 | Stock | No prior SKU activity; mappings saved | Import one complete Stock dataset | 938 WH_MAIN layers; quantities/costs preserved |  |  |
| UAT-11 | Bank | 1112 active Asset | Import bank opening/recon | 100000 =112500 +2500 -15000 |  |  |
| UAT-12 | Controls | Opening modules posted | Import ControlTotals | 5212 expected components, including master-only zero controls |  |  |
| UAT-13 | Release | Controls imported | Bulk release all scope kinds | 2050 scopes Released; zero Pending |  |  |
| UAT-14 | TB | All scopes released | Import complete test TB | 39 accounts; balanced; residual rent debit / revenue credit 1000000 |  |  |
| UAT-15 | Ledger | TB imported | Check Full Ledger | All 39 differences zero |  |  |
| UAT-16 | Retry | Already imported selection | Validate same unchanged selection again | Already imported; no duplicate openings |  |  |
| UAT-17 | Finalize | All rows imported and scopes released | Enter UAT evidence; Finalize Migration | Finalized; migration edits/execution disabled |  |  |
| UAT-18 | Customer payment | Customer scope Released | Receive payment against positive opening Invoice | Outstanding reduces and receipt posts once |  |  |
| UAT-19 | Supplier payment | Supplier scope Released | Pay positive opening Bill; apply separate Credit | Balances update without recreating historical bill |  |  |
| UAT-20 | Stock operation | Stock scope Released | Normal issue/sale/receipt for imported SKU | On-hand/layer quantities and journal accounts update |  |  |
| UAT-21 | Cheque deposit | Customer/bank scopes Released | Deposit one imported InSafe cheque | Safe reduces, bank movement once, no second AR reduction |  |  |

Ordinary workflow screen/route details: [User Manual](../../user-manual/User_Manual.md), [Admin User Manual](../../user-manual/Admin_User_Manual.md), [UAT test cases](../../uat/UAT_Test_Cases.md). Hosted authenticated UI was not accessible in this preparation; record actual deployed results and screenshots when you perform the tests.
