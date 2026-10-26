import csv,json,shutil
from pathlib import Path
R=Path("F:/source/NexusFlow_ERP")
O=R/"docs/migration/pilot_upload_20261015"
def put(p,s): (O/p).write_text(s.strip()+"\n",encoding="utf-8")
def read(p): return list(csv.DictReader((O/p).open(encoding="utf-8-sig",newline="")))
def table(rows,headers):
 def cell(v): return str(v).replace("|","/").replace("\n"," ")
 return "| "+" | ".join(headers)+" |\n|"+"|".join(["---"]*len(headers))+"|\n"+"\n".join("| "+" | ".join(cell(r[h]) for h in headers)+" |" for r in rows)
stages=[
("01","Masters","Accounts, Customer Master, Supplier Master","39 accounts; 896 customers; 215 suppliers",
"""Complete the setup and mappings guide first. Upload 01_Masters.zip, select only Accounts, Customer Master and Supplier Master, then use the import cycle below.
Review NET30 on party records, TRADE/DOM groups and STANDARD TIER. Account rows link the exact active posting accounts; they do not create account folders.
The four customer and two supplier UAT fixture names are retained. Blank contact details remain flagged for later completion.
Expected: all selected master units imported; no opening financial journal yet."""),
("02","Party_Openings","Customer Openings, Supplier Openings","1,150 customer documents; 180 supplier documents",
"""Upload 02_Party_Openings.zip and use the import cycle for Customer Openings and Supplier Openings only.
Customer Amount is creamt. Positive documents are Invoice; negatives are ReturnCredit. No debamt-minus-creamt calculation is used. All unsettled documents remain, including offsetting debts and credits on zero-net parties.
Supplier positives remain Bill, including the confirmed OVER PAID/ADVANCE rows; negative returns are Credit. Thirty-day due dates are populated. Four earlier UAT date adjustments remain disclosed in Pilot_Assumptions.csv.
Expected customer debts 162,436,582.24 less credits 321,597.10 = 162,114,985.14.
Expected supplier debts 96,073,265.55 less credits 1,883,015.00 = 94,190,250.55.
These totals exclude received cheques."""),
("03","Cheques","Received Cheques, Issued Cheques","49 received cheques; 1 issued pilot cheque",
"""Upload 03_Cheques.zip and use the import cycle for Received Cheques and Issued Cheques only.
Expected received: 49 instruments, LKR 5,328,628.00, InSafe, PartyReflected=true, BankReflected=false. They establish cheques in hand without reducing receivables a second time.
Expected issued fixture: supplier AH143, reference UAT-ISSUED-001, cheque 000001, LKR 15,000.00, bank 1112; both reflection flags true. It is already reflected in supplier and bank book balances.
Keep cheque numbers as text. Do not enter these instruments again through ordinary receipt/payment entry."""),
("04","Combined_Stock","Stock","938 stock layers = 494 finished goods + 444 raw materials",
"""Before uploading, save ACCESSORIES -> Accessories and the 33 existing-SKU aliases. See 01_Setup_and_Mappings.md.
Verify WH_MAIN has no inventory-account override for this pilot. Accessories posts to 1131; finished-goods categories post to 1132. The CSV InventoryAccountCode must match the resolved warehouse/category posting account.
Upload 04_Combined_Stock.zip and select Stock only. Leave SKU codes blank, including for comma-containing codes. Use the import cycle once for the whole file.
Inspect SDASF -> sdasf, category Accessories, account 1131. Inspect one finished-goods row, account 1132.
Expected RM value 29,727,743.70 plus FG value 84,689,502.04 = 114,417,245.74.
The 81 new FG fixtures use MSH / UNBRANDED / pcs. Unit conversion factor is 1; the summarized pilot layer date is 2026-09-30.
Another Stock upload replaces the current complete dataset revision. Do not upload FG and RM separately."""),
("05","Banks","Bank Opening, Bank Recon","1 book opening; 1 non-cheque reconciliation item",
"""Upload 05_Banks.zip and use the import cycle for Bank Opening and Bank Recon only.
Bank account 1112: book opening 100,000.00; statement 112,500.00; pending deposit +2,500.00; issued cheque pending -15,000.00 from stage 03.
Require statement bridge 112,500 + 2,500 - 15,000 = 100,000.
Do not repeat the issued cheque as another Bank Recon row.
All bank values in this stage are retained pilot fixtures."""),
("06","Control_Totals","Control Totals","5,212 control rows; 2,050 opening scopes",
"""Upload 06_Control_Totals.zip. Select Control Totals only and use the import cycle. Validation alone is insufficient: the controls must be approved and executed.
After completion, uncheck all modules and select Customer Master, Supplier Master, Stock and Bank Opening. Leave all four code filters blank.
In Signed independent schedule evidence enter a truthful pilot review reference, for example: Pilot 2026-10-15; prepared controls checked against the supplied pilot schedules and reconciliation report by [reviewer/date]. Functional test only.
Select Release Selected Reconciled Scopes. Refresh Progress. If some scopes were already released, the newly released count may be less than 2,050; the final scope list must contain all 2,050 as Released and none Pending.
Resolve any pending reason before continuing. The controls are an acceptance baseline reproduced from the test schedules, not independent certification of the original legacy closing reports."""),
("07","Trial_Balance","Trial Balance","39 account rows; debit = credit 282,960,858.88",
"""Only after every opening scope is released, upload 07_Trial_Balance.zip. Select Trial Balance only and use the import cycle.
The file is the complete tested pilot closing TB. Do not manually subtract AR, AP, stock or bank balances: the importer posts the residual.
Select Check Full Ledger. Require zero Difference for every account; compare reports/Expected_GL.csv.
Confirm all source rows are imported, all opening scopes released and no failed/pending work remains.
Before ordinary pilot transactions begin, capture the baseline reports and dashboard. Local reference: 2,455 Posted units and 2,050 Released scopes for the complete unchanged package. Different destination master resolution can alter unit grouping; investigate any mismatch.
For Final financial sign-off evidence enter truthful pilot acceptance evidence, e.g. Client pilot baseline reviewed on [date] by [name]; synthetic bank/issued cheque/TB and documented test assumptions accepted for functional testing only.
Select Finalize Migration. The project becomes read-only. This action closes the pilot migration workflow; it does not certify these fixtures as live financial balances.""")
]
put("README_START_HERE.md","""
# NexusFlow client pilot package — 15 October 2026

Use this package to replay the successfully finalized local migration for the client pilot at https://nexus.garnetnation.lk/Migration. All amounts are LKR.

**This is the user-approved pilot dataset.** It includes the same synthetic bank, issued cheque and trial balance used locally. Original exports have different snapshot dates; the pilot closing label is not certification of a real 30 September closing position. Review reports/Pilot_Assumptions.csv before accepting the pilot baseline.

## Start here

1. Extract NexusFlow_Pilot_Package_20261015.zip to a folder. This full handoff ZIP is not a Migration Centre upload.
2. Administrator: follow guides/00_Readiness.md and guides/01_Setup_and_Mappings.md.
3. Upload only the seven ZIPs inside stage_packages, in numeric order. Follow guides/02_All_Upload_Stages.md. Each stage has its own short guide as well.
4. At each stage: select the intended modules, leave code filters blank, validate, review, approve and execute; then wait for completion.
5. Import Control Totals, release all scopes, import Trial Balance, check the full ledger and finalize the pilot.
6. Use guides/03_Pilot_Run_and_Acceptance.md for client testing. Record Pass/Fail in review_workbooks/Pilot_Review.xlsx or reports/Pilot_Acceptance_Tests.csv.

## Package contents

| Folder/file | Purpose |
|---|---|
| stage_packages | Seven uploadable ZIPs, containing only manifest.json and exact UTF-8 template CSVs |
| upload_csv | Twelve alternative individual upload files; upload each module once, using ZIP or CSV |
| review_workbooks | Twelve matching values-only template workbooks plus Pilot_Review.xlsx |
| reports | Mappings by code, 33 SKU aliases, stock setup, reconciliations, GL, exceptions and tests |
| source_archive | Unchanged original source archive and manifest |
| guides | Readiness, mappings, every upload stage, acceptance and troubleshooting |
| reports/Verification_Report.md | Actual checks performed and destination checks still required |
| Package_SHA256.csv | Integrity list for every delivered file except the checksum file itself |

## Corrections included

- Added the missing ACCESSORIES category mapping and all 33 case-sensitive existing-SKU aliases, including SDASF -> sdasf.
- Documented separate stock posting accounts: raw materials 1131; finished goods 1132. WH_MAIN must not force a single account.
- Replaced local numeric-ID references with destination master codes/names.
- Clarified that NET30 must explicitly map to a 30-day credit term; COD is not suitable.
- Split the original opening bundle into seven manageable uploads and added baseline checks, controls/release and finalization instructions.
- Preserved all twelve successful local CSVs byte-for-byte, and all twelve matching source workbooks. No new quantity, price, balance or identity was invented.

Prepared for destination validation. No database was cleared, application deployed, file uploaded or import executed in producing this package.
""")
put("guides/00_Readiness.md","""
# Before the first upload — administrator

## Preconditions

Target: nexus.garnetnation.lk / Azure SQL NexusGarnetProd on versalapps.database.windows.net. The hosting name says production; this specific handoff is the authorized client pilot replay.

1. Retain a pre-pilot recovery point. Confirm the previously discussed disposable test-data cleanup has completed and been checked. This package does not perform cleanup and does not include a destructive reset script.
2. Keep ordinary invoice, receipt, payment, cheque and stock entry paused until this baseline is finalized. Existing operational balances, stock layers or another finalized migration are a stop condition for a fresh replay.
3. Confirm the deployed app includes the tested migration performance and dashboard fixes from the current source. ZIP data does not deploy application code. The authenticated hosted UI/build was not accessible to the preparation tools; verification there remains required.
4. Confirm an open financial period covers 2026-10-01. Confirm active destination masters and the account hierarchy below.
5. Sign in with actual assigned permissions. Migration Centre requires Permissions.Migration.View. Upload/mappings/validation require Prepare; approval/release require Approve; execute/resume require Execute; finalization requires Finalize. Destination role names are Needs business confirmation; do not assume every user is an administrator.
6. Open /Migration and select Download Template Pack. Headers must agree with the supplied canonical files. A different deployed contract needs review before import.

## Clean target and master checks

An administrator can run reports/Target_Readiness_ReadOnly.sql in Azure Data Studio against the confirmed target. It is a read-only audit, not a pass certificate. Review its results; missing masters must be configured in their existing master pages.

- No existing migration project, financial journal, payment, opening item or live inventory layer for a fresh cycle. Confirm the reset result separately.
- Active posting account types: 1120 AR = Asset; 2110 AP = Liability; 4110 sales = Revenue; 5110/6210/6220 = Expense; 1131/1132 inventory, 1151 cheques and 1112 bank = Asset; 3200 Retained Earnings = Equity. Select posting leaves, not folder accounts.
- The tested cash/bank hierarchy is under 1110. Do not move inventory/AR/safe into the cash/bank group.
- Compare the complete destination active posting-account list with Accounts.csv / TrialBalance.csv. This pilot TB covers 39 accounts. Extra active accounts require reviewed complete coverage, not fabricated balances.
- Check WH_MAIN, base unit pcs, brand UNBRANDED, all used categories and the required lookup/bank-branch masters. The stock references cover 938 rows and 28 categories.
- The 33 lower-case existing SKU targets must resolve to the intended products. If a target is absent, review the product identity in Stock_Setup_Reference.csv before creating or mapping anything.

## Create the project

1. In Migration Centre select New Project.
2. Project name: NexusFlow Pilot 2026-10-15.
3. Stable source system identifier: TESTTTT (exactly; the stage manifests use this).
4. Actual go-live opening date: 2026-10-01. For this replay it is the test opening; closing is 2026-09-30.
5. Leave Existing customer batch adoption evidence blank for a clean target.
6. Select Create Project. Use this one project for all seven packages.

A Finalized project is read-only. Do not create another migration or change dates to bypass existing imported activity. If a preparing project already belongs to this same replay, verify its source/date/revisions before continuing.

The pilot is ready to post only when destination validation has no errors and the reviewer accepts the disclosed fixtures. Real financial cutover needs refreshed, common-date exports and business approval.
""")
maps=read("reports/Mappings_By_Code.csv")
defaults=read("reports/Defaults_By_Code.csv")
required=[r for r in maps if r["Mapping type"]!="Stock" and (r["Mapping type"]!="Category" or r["Exact legacy code"] in ["MSH","ACCESSORIES"])]
aliases=read("reports/Required_SKU_Mappings_33.csv")
put("guides/01_Setup_and_Mappings.md","""
# Save exact destination mappings and defaults

Use step 2, Map and confirm source treatment. Codes below are codes, not database IDs. Choose each destination NexusFlow master by code and name.

For every mapping: choose Mapping type, enter Exact legacy code, choose NexusFlow master, select Add Mapping. After entering the complete set, select Save Mappings and Evidence. Add Mapping alone does not persist it.

## Core and bank mappings

""" + table(required,["Mapping type","Exact legacy code","Target master code","Target label"])+"""

Fourteen branch targets are the same-bank pilot substitutes used in the successful local test. They are explicitly retained for this replay; physical branch verification is still required for live balances.

## Existing SKU mappings — 33 rows

Use Mapping type Stock. Enter punctuation literally in Exact legacy code. Map to the existing SKU, not a similarly named product. Current source normalizes incoming codes to uppercase but its in-memory SKU lookup is case-sensitive; these mappings avoid duplicate/new-product handling.

""" + table(aliases,["Exact legacy code","Target master code","Target label"])+"""

If every target exists, add all 33. If a target is absent, resolve its actual identity before creating a product. The reports/Mappings_By_Code.csv reference also lists every used category as a fallback when destination SKU masters differ. It contains 85 reference mappings in total; the additional 26 category rows are conditional, not extra mandatory mappings for correctly resolved existing SKUs.

## Stock posting setup

- Raw materials: category master Accessories (source ACCESSORIES), inventory account 1131 — Raw Materials (RM) Inventory.
- Finished goods: the actual product categories, inventory account 1132 — Finished Goods (FG) Inventory.
- WH_MAIN — Main Garnet: no inventory-account override for this two-account pilot.
- Both inventory accounts must be active Asset posting accounts.
- Category mapping and account setup are separate requirements. Map ACCESSORIES, then check its Inventory Account. The resolved account must equal each CSV InventoryAccountCode.
- Use reports/Category_Inventory_Setup.csv for all 28 categories and reports/Stock_Setup_Reference.csv for every row.

Never clear InventoryAccountCode, change every row to one account, or alter CategoryCode merely to hide validation errors. Correct destination posting setup against these references.

## Defaults and evidence

""" + table(defaults,["UI field","Target code","Target label","Pilot note"])+"""

The saved local defaults use NET 30 DAYS — Net 30 Days, while the explicit NET30 mapping targets NET30 — 30 Days. Both are 30-day credit masters locally. On the destination choose the actual equivalent 30-day term and explicitly map NET30 to it. Do not choose CASH ON DELIVERY (COD). Matching codes alone do not satisfy CustomerGroup, SupplierGroup, PaymentTerm or PriceLevel mapping requirements.

Account codes auto-resolve when active and of the right type. 3200 was verified locally as an Equity posting account; verify the current destination definition before selecting it. 5110 remains a temporary pilot supplier expense, with explicit rent/electricity row accounts retained.

## Column profiles and required data

1. The supplied files already use exact template headers. No legacy-column transformation is needed on upload.
2. If an old Change source column mapping profile exists, correct conflicting entries using Map Column: Required field and Source column must name the same canonical header. Do not map creamt/debcode over the prepared fields.
3. Supplied modules become Provided. Leave not-yet-uploaded modules Awaiting Data until their stage. All twelve modules are supplied in this pilot; no Not Applicable declaration is necessary.
4. Copy the summarized-layer approval and valuation evidence above. Enter Approved supplier terms in days = 30.
5. Select Save Mappings and Evidence, then Refresh Progress to verify persistence.
6. Leave Customer codes, Supplier codes, SKU codes and Bank account codes blank throughout this complete replay. Two literal SKU codes contain commas, which filter boxes interpret as separators.

Do not import a settings JSON from local: numeric master IDs differ between databases. The package provides code-based reference tables only.
""")
cycle="""
## Import cycle used at every stage

1. At 1. Upload closing data choose the stage ZIP under Or upload a ZIP package with manifest.json and select Upload Package. Uploading only stages data.
2. At 3. Select this import uncheck other modules; select only the modules listed for this stage. Uploading or saving mappings can reselect previously Provided modules.
3. Leave all four code filters blank.
4. Select Validate Selected Import. Check counts, signs, dates, mappings and proposed journals. Resolve every error. Genuine missing-contact and supported stable-composite warnings are disclosed and may be accepted for this pilot.
5. Enter truthful Approval evidence, e.g. Pilot 2026-10-15 / stage [number], schedules and documented fixtures reviewed by [name/date]; functional testing only.
6. Select Approve Selected Import, then Execute Approved Import.
7. Use Refresh Progress until the run is completed. Record the result. A queued run requires a working background worker. For a failed run, read the error, correct the cause and use Resume Run for its unfinished work. Do not repeatedly press Execute.
8. Any changed data, mappings or selection invalidates approval; validate and approve again before posting.

Already imported means committed units were recognized; Upload Package by itself does not mean balances are posted. Do not upload both XLSX and CSV as separate batches.
"""
allguide="# Seven upload packages — operator stages\n\nReadiness and mapping setup come first. These package numbers are upload order; the web wizard separately labels its screens 1–7.\n\n"
allguide+="| Order | ZIP | Select only | Expected rows |\n|---|---|---|---|\n"
for n,name,mods,count,body in stages: allguide+=f"| {n} | {n}_{name}.zip | {mods} | {count} |\n"
allguide+=cycle
for n,name,mods,count,body in stages:
 text=f"# Upload {n} — {name.replace('_',' ')}\n\nFile: stage_packages/{n}_{name}.zip\n\nSelect only: **{mods}**.\n\nExpected: {count}.\n\n"+body+"\n"+cycle
 put(f"guides/Stage_{n}_{name}.md",text)
 allguide+=f"\n## Upload {n} — {name.replace('_',' ')}\n\n{body}\n"
put("guides/02_All_Upload_Stages.md",allguide)
put("guides/03_Pilot_Run_and_Acceptance.md","""
# Client pilot run after the opening is finalized

## Administrator baseline check

Before users start posting, save screenshots/exports for these LKR balances:

| Check | Expected |
|---|---:|
| Customer invoice/credit net receivables | 162,114,985.14 |
| Supplier bill/credit net payables | 94,190,250.55 |
| Stock valuation | 114,417,245.74 |
| Raw-material inventory account 1131 | 29,727,743.70 |
| Finished-goods inventory account 1132 | 84,689,502.04 |
| Cheques in hand, 49 InSafe received instruments | 5,328,628.00 |
| Cash and bank dashboard total | 100,000.00 |
| Pending issued cheque | 15,000.00 |
| Bank statement | 112,500.00 |
| Pending deposit | 2,500.00 |
| Trial balance total on each side | 282,960,858.88 |

Expect every full-ledger account difference to be zero. Revenue MTD can be zero before new invoices: imported opening/TB journals are not new sales. Overdue figures depend on the dashboard date. After pilot transactions, totals should change according to those transactions; do not keep comparing a used database with an untouched baseline.

If dashboard AR/AP are zero while imported opening reports are correct, verify that the tested dashboard fix is deployed. If cash/bank includes inventory/AR/safe, check the build and account hierarchy under 1110.

## Normal users

Use the assigned business role. Migration permissions are for the administrator/operator.

1. Receive part of a customer opening Invoice using Incoming Payments (AR) / Customer Receipts at /Treasury/Receipts. Select the customer, choose the positive imported opening, allocate the actual test receipt and confirm once. Check the remaining outstanding and AR change.
2. Test an ordinary supplier payment against a positive opening Bill. Check remaining AP. Review return credits separately; do not recreate an old bill or payment to establish opening balances.
3. Deposit one imported InSafe received cheque using the normal cheque workflow. Check safe/bank movement and confirm the customer is not credited a second time.
4. Run an ordinary stock issue/sale/receipt using a released SKU. Check on-hand quantity, FIFO layer use and inventory journals.
5. Record results and evidence in Pilot_Review.xlsx, sheet Pilot tests, or reports/Pilot_Acceptance_Tests.csv. Leave unrun tests blank; use Pass/Fail only after execution.

Exact destination role assignments, menu availability and business approval of test amounts are Needs business confirmation. Full manuals are in the project docs/user-manual folder.

## Completion and transition

Retain the pilot baseline, test evidence and unchanged source archive. The original snapshots are not a complete pre-cutover transaction history. A future live migration needs refreshed agreed-date closing exports, a complete read-only legacy history archive, actual bank/issued cheque schedules and reviewed genuine TB. It also needs a plan for transactions recorded in the old system after the chosen opening.

Do not post a real opening on top of the pilot data. A separately reviewed cleanup or separate target is needed for the real cutover.
""")
put("guides/04_Troubleshooting.md","""
# Validation and posting issues

| Symptom | Check and correction |
|---|---|
| SDASF / Accessories category errors | Add Stock SDASF -> existing sdasf. Add Category ACCESSORIES -> Accessories. Save Mappings and Evidence. Existing SKU must resolve to RawMaterial / Accessories / 1131. |
| Inventory account must equal warehouse/category posting account | Check actual SKU category and WH_MAIN override. Pilot RM account is 1131, FG is 1132; both active Asset posting accounts. Use Category_Inventory_Setup.csv. |
| Map category to an active inventory account | Category mapping does not assign its account. Correct the category's inventory posting account in master setup. Revalidate. |
| Existing SKU treated as a new product/duplicate | Add the case alias in Required_SKU_Mappings_33.csv. Verify exact target product identity. Do not create a duplicate uppercase product. |
| Composite legacy identity warning | The files use supported permanent IDs/composites. Stable_Identities.csv has no duplicates; source row numbers are trace references only. Review changed identity fields before reuploading. A warning alone is not an import error. |
| Group/term/price level error despite matching code | Explicitly map TRADE, DOM, NET30 and STANDARD TIER, then save. Defaults alone are insufficient. |
| Wrong payment term | Select an actual 30-day credit term. COD is cash on delivery. Local NET30 and NET 30 DAYS are different records representing 30 days. |
| Source-system/closing-date package mismatch | Project must use TESTTTT / opening 2026-10-01. All manifests close 2026-09-30. Do not change dates merely to bypass existing data. |
| ZIP not a migration package | Upload a ZIP from stage_packages, not the full handoff ZIP. It needs manifest.json and its CSV files at the ZIP root. |
| No pending units / Already imported | Check posted units and run status. Recognized unchanged units do not need another execution. Validation is not evidence that a newly uploaded file was executed. |
| Scope still Pending | Ensure relevant financial modules and Control Totals were executed, not just staged. Review component totals/row counts, including zeros and cheque reflection flags. Select the correct scope modules for release. |
| Full-ledger differences / active account missing | Compare Expected_GL.csv and destination active account list. Pilot TB covers 39 accounts. Resolve account coverage and posting differences; do not plug managed AR/AP/inventory balances. |
| Cannot change a finalized project | Finalization makes it read-only. Do not create a second opening to work around it. Use a reviewed new-cycle plan. |
| Blank contacts warning | Expected genuine blanks are listed in Missing_Contact_Details.csv. BA366's invalid source email remains blank. Do not invent contact details. |
| Long run / queued job / missing progress | Confirm current performance fix is deployed, background processing is alive, and the error/run log is reviewed. Refresh Progress; use Resume Run only as appropriate. Do not run direct inserts or repeatedly start concurrent jobs. |
| Dashboard AR/AP zero, or cash/bank too high | Confirm the tested dashboard calculations are deployed and review the cash/bank account hierarchy. Compare the finalized opening reports before changing any data. |

Files or mappings changed after approval require a fresh validation and approval. Preserve completed units and original evidence.
""")
# Include existing read-only target audit; omit destructive operator reset scripts.
shutil.copyfile(R/"docs/migration/Production_Migration_Preflight_20261015.sql",O/"reports/Target_Readiness_ReadOnly.sql")
# Clear client-facing consolidated exceptions with disposition, rather than silently discarding anything.
exceptions=[
["PILOT-001","Pilot fixture","Synthetic bank/issued cheque/TB and disclosed date/master/classification assumptions","Accepted for requested pilot; replace for live cutover","Pilot_Assumptions.csv"],
["MAP-001","Destination check","33 existing-SKU aliases and ACCESSORIES mapping missing from old guide","Corrected reference; save on destination before stock validation","Required_SKU_Mappings_33.csv"],
["MAP-002","Destination check","WH_MAIN/category inventory-account agreement","Verify 1131 RM and 1132 FG; active Asset accounts","Category_Inventory_Setup.csv"],
["CONTACT-001","Completion later","Genuine missing contact/tax/address fields in 1111 party rows","Blanks retained and flagged; BA366 invalid email remains blank","Missing_Contact_Details.csv"],
["DATE-001","Live cutover blocker","Original reports have inconsistent/older snapshot dates","Pilot date retained transparently; refreshed exports required for real opening","Pilot_Assumptions.csv"],
["TARGET-001","Posting prerequisite","Azure master/account/period/build/current data state not certified by file preparation","Run read-only preflight and destination validation; confirm cleanup separately","Target_Readiness_ReadOnly.sql"],
["ARCHIVE-001","Live cutover blocker","Supplied original snapshots are not a full read-only transaction history","Archive unchanged; obtain full pre-cutover history","../source_archive/README.md"]
]
with (O/"reports/Exceptions.csv").open("w",encoding="utf-8",newline="") as f:
 w=csv.writer(f);w.writerow(["ID","Classification","Finding","Disposition","Evidence"]);w.writerows(exceptions)
# Code hashes identify the source contract; they do not assert what is deployed remotely.
import hashlib
evidence=[
"NexusFlow.AppCore/Features/Migration/MigrationModels.cs",
"NexusFlow.AppCore/Features/Migration/MigrationValidation.cs",
"NexusFlow.AppCore/Features/Migration/MigrationReconciliation.cs",
"NexusFlow.AppCore/Features/Migration/MigrationBulkRelease.cs",
"NexusFlow.AppCore/Features/Migration/MigrationPosting.cs",
"NexusFlow.Infrastructure/Services/MigrationFileReader.cs",
"NexusFlow.Web/Views/Migration/Index.cshtml",
"NexusFlow.Web/wwwroot/js/migration/centre.js"]
with (O/"reports/Source_Contract_SHA256.csv").open("w",encoding="utf-8",newline="") as f:
 w=csv.writer(f);w.writerow(["Repository file","SHA256"])
 for rel in evidence:
  if (R/rel).exists():w.writerow([rel,hashlib.sha256((R/rel).read_bytes()).hexdigest().upper()])
# Link new approved pilot handoff from existing admin guidance without overwriting earlier live-cutover advice.
for rel in ["docs/user-manual/Admin_User_Manual.md","docs/user-manual/FAQ.md","docs/migration/Production_Migration_Guide_20261015.md"]:
 path=R/rel;s=path.read_text(encoding="utf-8-sig")
 marker="Client pilot replay — 15 October 2026"
 if marker not in s:
  prefix="../migration/" if rel.startswith("docs/user-manual") else ""
  s+="\n\n## "+marker+"\n\nThe user has requested a replay of the finalized local test data for a client pilot, including its disclosed fixtures. Use the [updated pilot package and stages]("+prefix+"pilot_upload_20261015/README_START_HERE.md). Its bank, issued cheque and TB assumptions are for functional testing; the real financial cutover guidance above still requires certified source balances. Preparing the package does not clear or import the hosted database.\n"
  path.write_text(s,encoding="utf-8")
print("Wrote readiness, exact mappings, all stages, acceptance, troubleshooting and exceptions.")
