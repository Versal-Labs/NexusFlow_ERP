import json,csv,io
from pathlib import Path
ROOT=Path('F:/source/NexusFlow_ERP/docs/migration/uat_upload_20261012')
s=json.loads((ROOT/'.work/summary.json').read_text(encoding='utf-8'));spec=json.loads((ROOT/'.work/workbooks.json').read_text(encoding='utf-8'))
rr={x['name']:(x['headers'],x['rows']) for x in spec[-1]['sheets']}
def table(head,rows):
    esc=lambda v:str(v).replace('|','\\|').replace('\n',' ').replace('\r',' ')
    return '\n'.join(['| '+' | '.join(head)+' |','|'+'---|'*len(head)]+['| '+' | '.join(esc(v) for v in row)+' |' for row in rows])
def doc(name,text):(ROOT/name).write_text(text.strip()+'\n',encoding='utf-8')
modules=list(s['rows'])
master=['Accounts','CustomerMaster','SupplierMaster']
doc('README_UAT.md',f'''
# NexusFlow UAT upload package
Prepared 12 October 2026. LKR. Opening 2026-10-01, test closing 2026-09-30. These are final inputs for functional UAT. The original production review and archive are preserved. Test dates, missing-master fixtures, branch substitutes and synthetic trial balance are documented in the change log. This package is not certified production opening data.

## All files and import order
{table(['Stage','Dataset','Rows','CSV','Excel'],[[('1 — Masters' if k in master else '3 — Test TB' if k=='TrialBalance' else '2 — Openings and controls'),k,s['rows'][k],f'[CSV](upload_csv/{k}.csv)',f'[Excel](review_workbooks/{k}.xlsx)'] for k in modules])}

Use the CSV or matching Excel once per module. All twelve CSVs have exact v1.0 template headers, UTF-8 encoding and final values. Codes/cheque numbers are text. Dates yyyy-MM-dd. Amounts numeric LKR. Stock is one complete dataset for both FG and RM; another Stock upload replaces the whole revision.

## Start here
1. [Project and staging](GUIDE_01_Project_and_Staging.md)
2. [Exact mappings and defaults](GUIDE_02_Mappings_and_Defaults.md)
3. [Validate and import masters](GUIDE_03_Masters.md)
4. [Opening documents, cheques, stock and banks](GUIDE_04_Openings_Stock_and_Banks.md)
5. [Import controls, reconcile and release](GUIDE_05_Reconciliation_and_Release.md)
6. [Full test TB, ledger check and finalization](GUIDE_06_Trial_Balance_and_Finalization.md)
7. [Operational UAT and pass/fail checklist](GUIDE_07_Operational_UAT_Tests.md)

[Complete guide in one file](UAT_Complete_Guide.md). [UAT review workbook](review_workbooks/UAT_Review.xlsx). [Intentional changes and reconciliation](UAT_Changes_and_Reconciliation.md). [File verification](Verification_Report.md).

Bulk staging ZIPs: [01_Masters](stage_packages/01_Masters.zip), [02_Openings_and_Controls](stage_packages/02_Openings_and_Controls.zip), [03_TrialBalance](stage_packages/03_TrialBalance.zip). Their manifests target local project SourceSystem {s['source_system']}. For a different source identifier, individual CSV uploads are easiest; otherwise change each manifest to the exact saved project identifier. The complete download ZIP with guides and workbooks is a delivery bundle, not a Migration Centre upload package.

Local masters were checked SELECT-only at {s['read_at_utc']} UTC. Project test / ID 3 is Preparing; opening 1 October and closing 30 September. No migration units/origins, operational invoices/bills/payments, stock transactions or layers were present. All 896 existing staged customer scopes are covered. Hosted browser access returned ERR_BLOCKED_BY_CLIENT, so authenticated deployed UI and configuration remain unverified. Button names and workflow rules were checked in current source. Validate in your actual UAT destination before execution.

The original source files and previous package were not changed. A read-only originals copy is included under source_archive. Existing production review: [preparation_20261011](../preparation_20261011/README.md). No import, approval, posting, database reset, finalization or application-code change was executed during this preparation.
''')
doc('GUIDE_01_Project_and_Staging.md',f'''
# Stage 1 — project and staging
Operator needs Permissions.Migration.View and Permissions.Migration.Prepare. Use an isolated UAT company with the required account and lookup codes. Current local saved project: test, ID 3, SourceSystem {s['source_system']}, Preparing. Its older staged customer revisions will be replaced by these complete UAT inputs.

1. Sign in and open Administration → Settings & Security → Migration Centre (/Migration).
2. Choose test under Saved migration project. Verify opening 2026-10-01 and closing 2026-09-30. Upload Dataset uses the saved project closing automatically; there is no separate closing-date field on this page.
3. For a fresh UAT company without a project, select New Project. Enter Project name NexusFlow UAT 2026-10-12, Stable source system identifier {s['source_system']} and Actual go-live opening date 2026-10-01, then Create Project. The importer permits one company project. A Finalized project is read-only; use a separately provisioned UAT company for a new cycle. This package does not reset a database.
4. Select Download Template Pack if the destination version differs and compare the headers. Confirm the Stage 2 master targets exist.
5. Stage stage_packages/01_Masters.zip through Or upload a ZIP package with manifest.json → choose ZIP → Upload Package. Alternatively select Dataset and File, then Upload Dataset for Accounts.csv, CustomerMaster.csv and SupplierMaster.csv individually.
6. Use Stage 2 to save mappings, then Stage 3 to import masters. Stage 4 uses 02_Openings_and_Controls.zip. Stage 6 uses 03_TrialBalance.zip only after all scopes are released.
7. Uploading stages rows and changes no ledger balances. Do not upload the full delivery ZIP, report CSVs or UAT_Review.xlsx as a dataset. Do not upload both CSV and Excel as two batches.

Expected: Accounts 39, Customer Master 896, Supplier Master 215 staged. Ledger unchanged. Included modules become Provided on upload. Trial Balance remains Awaiting data until Stage 6. All financial modules are supplied in this UAT case; no empty-file or NotApplicable declaration is needed. The ZIP SourceSystem must equal the saved project identifier. If it differs, use the individual CSVs.
''')
doc('GUIDE_02_Mappings_and_Defaults.md',f'''
# Stage 2 — exact mappings and defaults
Select destination masters by code/name. Local numeric IDs are reference values and may differ in another UAT environment. Template Code fields contain actual master/legacy codes, never IDs.

## Required mappings
For each row select Mapping type, enter Exact legacy code, choose NexusFlow master and select Add Mapping. Code comparison is uppercase; Unit PCS covers both Pcs and pcs.
{table(*rr['Mappings'])}

The MSH / UNBRANDED category/brand choices apply to the 81 new UAT FG SKUs. Existing SKUs keep their current category, brand, type and base unit. Fourteen bank-branch substitutes are explicitly UAT choices, not confirmation of physical cheque branches.

## Defaults and evidence
{table(*rr['Defaults'])}

1. Select each default above. Supplier expense 5110 is temporary UAT treatment for unresolved suppliers. The two rent suppliers retain 6210 and the electricity supplier retains 6220.
2. Save explicit CustomerGroup TRADE, SupplierGroup DOM, PaymentTerm NET30 and PriceLevel STANDARD TIER mappings, even though codes match masters. NET30 is 30 Days. COD means cash on delivery and is not used.
3. Account codes auto-resolve to matching active posting accounts: 1120 AR Asset; 2110 AP Liability; 4110 Sales Revenue; 5110/6210/6220 Expense; 1131/1132 Inventory Asset; 1151 Safe Asset; 1112 Bank Asset; 3200 Equity. Do not select the 5000 folder or 5130 Expense as a revenue account.
4. Open Change source column mapping only if an old column profile exists. Remove conflicting entries or use Map Column with Required field and Source column both set to the identical canonical header. Do not let old debcode/creamt profiles replace populated canonical values.
5. Paste summarized-layer and valuation evidence from the table. Enter Approved supplier terms in days 30. Select Save Mappings and Evidence.
6. reports/UAT_Settings_reference.json shows the final local mapping/default reference. It was not sent to the application. Save settings through the wizard and validate.

Expected: all required code mappings resolve; default accounts have the right types; no COD mapping is used by these files.
''')
doc('GUIDE_03_Masters.md','''
# Stage 3 — validate and import masters
Precondition: Stage 1 staged and Stage 2 saved. Prepare permission permits validation; Approve permits approval; Execute permits execution. Actual role names must be checked in the destination.

1. In Select this import select only Accounts, Customer Master and Supplier Master. Uncheck financial modules and Control Totals.
2. Leave Customer codes, Supplier codes, SKU codes and Bank account codes blank to include every record.
3. Select Validate Selected Import. Expected rows: 39 accounts, 896 customers, 215 suppliers. Missing-contact and supported-composite-identity warnings are expected. Mapping, account-type, duplicate, invalid-email and field-length errors must be resolved.
4. Review missing-master UAT fixtures GA735, TK1039, UP1038, VI1041, SH331 and WL329. Names begin UAT; original codes remain text. No contact data is fabricated.
5. In Approval evidence enter: UAT fixture import only, package 2026-10-12. Temporary masters and expense defaults reviewed for functional testing; no production financial sign-off.
6. Select Approve Selected Import, then Execute Approved Import. Select Refresh Progress until completion. If a run fails, inspect the error; fix its cause and use Resume Run for unfinished work. Do not blindly post a second opening.
7. Confirm NET30, TRADE/DOM and STANDARD TIER. Accounts links existing current posting accounts; it does not import folders. Current customers/suppliers without legacy bindings are not matched by similar name.

Expected: all selected masters imported; no opening financial journal yet. Genuine blanks remain blank. Continue to Stage 4. Changes to files/mappings/selection require validation and approval again.
''')
doc('GUIDE_04_Openings_Stock_and_Banks.md',f'''
# Stage 4 — documents, cheques, stock and banks
Precondition: masters imported; all mappings saved; an open financial period covers 1 October 2026; no existing operational party/SKU activity or live stock layers. The checked local test database has none.

1. Upload 02_Openings_and_Controls.zip with Upload Package, or stage its eight individual CSVs with Upload Dataset. This includes all opening schedules and Control Totals. Trial Balance is saved for Stage 6.
2. Uncheck automatically selected modules. Select Customer Openings and Supplier Openings only, with all code filters blank. Validate Selected Import; review signs/dates/amounts; enter UAT evidence; Approve Selected Import; Execute Approved Import; Refresh Progress until complete.
3. Select Received Cheques and Issued Cheques only and repeat the validation/approval/execution cycle. The 49 received instruments are InSafe, PartyReflected=true, BankReflected=false, total 5328628.00. They add the safe opening without another AR receipt or bank movement. Cheque numbers, including leading zeros, remain text.
4. The issued fixture is AH143 / UAT-ISSUED-001 / cheque 000001 / 15000.00 / bank 1112. Both reflection flags are true. It creates a pending bank item already reflected in party and bank book; it does not pay AP again.
5. Select Stock only. Validate, approve and execute the complete 938-row Stock file: 494 FG plus 444 RM, all in WH_MAIN. Existing exact SKU masters are used. The 81 new FG SKUs use temporary MSH / UNBRANDED / pcs classification. LayerDate 2026-09-30 is the UAT summarized date. Another Stock file replaces this complete revision.
6. Select Bank Opening and Bank Recon only. Validate, approve and execute. Bank 1112: book 100000.00; statement 112500.00; deposit pending +2500.00; issued cheque pending -15000.00. Statement bridge: 112500 +2500 -15000 =100000.
7. Import Control Totals in Stage 5. Keep Trial Balance unselected until all opening scopes are released.

## Expected amounts
{table(['Dataset','Positive LKR','Negative LKR','Net LKR'],[[k,v['positive'],v['negative'],v['net']] for k,v in s['totals'].items()])}

Stock value {s['stock_value']} LKR. FG quantity {s['fg_quantity']}; RM quantity {s['rm_quantity']}. These are different item populations, not a single physical unit measure. All legacy document amounts are retained. Five zero-net customer cases retain both debt and credit rows. Four post-closing supplier documents have test date 2026-09-30 and due date 2026-10-30; original dates are logged.

Do not bypass date/type/mapping/duplicate/live-activity errors. Review the exact cause. Warnings for genuine blanks and composite identities are expected; they are not errors.
''')
doc('GUIDE_05_Reconciliation_and_Release.md',f'''
# Stage 5 — controls and scope release
ControlTotals contains fixed expected UAT fixture controls. It is not an independent production closing certificate. Zero invoice/cheque components cover master-only parties without fabricating financial documents.

1. Select only Control Totals. Leave filters blank. Validate Selected Import, review {s['rows']['ControlTotals']} rows, record UAT fixture control evidence, Approve Selected Import and Execute Approved Import. Wait for completion.
2. In Select this import select Customer Master, Supplier Master, Stock and Bank Opening. Uncheck Trial Balance. Leave all code filters blank. These modules select all four scope kinds for bulk release.
3. Under Release multiple reconciled scopes enter Signed independent schedule evidence: UAT-only fixed fixture controls, package 2026-10-12. All test positions and zero components reviewed; not production independent closing evidence.
4. Select Release Selected Reconciled Scopes. Expected: {s['scopes']} scopes Released and zero Pending. Read any pending reason and compare its current source, posted rows, control amount and count with ControlTotals.csv.
5. For a manual check select Reconcile for a displayed scope. Difference must be zero. Enter control amounts and row counts from the fixture, record Complete scope and control evidence, then Release Reconciled Scope.
6. Customer/Supplier Debit and Credit components use positive magnitudes, and BOTH use the entire party document count. Cheques are separate components. Stock uses Stock:WH_MAIN:Quantity and Stock:WH_MAIN:Value. BankOpening row count is 1; BankStatementBridge count is 2 (issued cheque plus deposit); BankRecon count is 1.
7. Released entities can trade. Complete initial TB/finalization checks before ordinary transactions for a straightforward first test cycle.

Expected scopes: 896 Customer, 215 Supplier, 938 Stock, 1 Bank. A master-only customer/supplier has zero expected amounts and RowCount 0 for absent components. No unsettled legacy document was removed to create those zeros.
''')
doc('GUIDE_06_Trial_Balance_and_Finalization.md',f'''
# Stage 6 — test trial balance, ledger check and finalization
Precondition: every opening scope Released; opening modules and controls posted; financial period open; no new source/mapping changes. This UAT TB deliberately replaces the original unbalanced 188-account report with a balanced full 39-account test position.

1. Upload 03_TrialBalance.zip or TrialBalance.csv.
2. Select Trial Balance only; leave filters blank. Validate Selected Import. All 39 current active posting accounts are present, including 30 deliberate test-zero accounts. Debit = Credit = {s['tb_debit']} LKR.
3. The TB includes preceding opening contributions. Do not manually subtract AR, AP, stock, safe or bank. The importer subtracts its own opening contributions. Expected residual: 6210 Rent Expense Debit 1000000.00 and 4110 Sales Revenue - FG Credit 1000000.00. Managed-control residuals must be zero.
4. Record Approval evidence: UAT synthetic TB from opening journal expectations plus balanced rent/sales residual pair; no production sign-off. Approve Selected Import, Execute Approved Import, Refresh Progress.
5. Select Check Full Ledger. All 39 account differences must be zero. Compare reports/Expected_GL.csv and UAT_Review.xlsx.
6. Check all twelve datasets Provided, every current row Already imported and all {s['scopes']} scopes Released. Run retry/idempotency checks before finalization.
7. Enter Final financial sign-off evidence: UAT functional acceptance only, package 2026-10-12; synthetic fixtures checked; no production migration approval. Select Finalize Migration after completeness and zero differences pass.
8. Expected: Finalized; migration editing/execution disabled; ordinary operations remain available for released entities. Use a separate UAT company for another full import cycle. This preparation does not reset or finalize the application.

The original trial balance and its -613120.00 imbalance remain archived unchanged. Production still requires certified closing data and approved legacy account mappings.
''')
doc('GUIDE_07_Operational_UAT_Tests.md',f'''
# Stage 7 — ordinary operations and acceptance checks
Record Pass / Fail, tester/date and evidence in UAT_Review.xlsx → UAT tests or reports/UAT_tests.csv. Preparation checks do not mean these application tests passed. Admin migration operators need Migration permissions. Normal users need actual sales/payment/stock/cheque permissions and released scopes. Destination role names are Needs business confirmation.

After the opening/TB tests:
- Receive a normal customer payment against a positive imported Invoice. Confirm outstanding reduces and a receipt posts once.
- Apply an imported ReturnCredit through the supported customer credit workflow. A credit is not collected as an invoice. Zero-net documents remain traceable.
- Pay a positive supplier opening Bill and apply a negative supplier Credit separately. Confirm no historical bill is recreated.
- Use normal stock issue/sale/receipt with a released imported SKU. Confirm on-hand, FIFO remaining quantity and journal accounts.
- Deposit an imported InSafe cheque. Confirm safe reduces and bank changes once, with no second AR reduction.
- Clear the synthetic issued cheque and deposit item through the supported opening-bank-item workflow. Confirm already-reflected bank book is not posted twice.
- Check /Migration access and API permission enforcement for a user without migration permissions.

{table(*rr['UAT tests'])}

Ordinary workflow screen/route details: [User Manual](../../user-manual/User_Manual.md), [Admin User Manual](../../user-manual/Admin_User_Manual.md), [UAT test cases](../../uat/UAT_Test_Cases.md). Hosted authenticated UI was not accessible in this preparation; record actual deployed results and screenshots when you perform the tests.
''')
doc('UAT_Changes_and_Reconciliation.md',f'''
# UAT changes and reconciliation
The user requested functional UAT data on 12 October 2026. This separate package adapts values explicitly for testing. Original exports and the previous production preparation package are unchanged.

## Intentional fixture choices
- Financial ClosingDate is UAT metadata 2026-09-30. Original customer/cheque 28 September snapshots, RM 13 September snapshot and uncertified FG/supplier dates remain archived. They have not become certified closing balances.
- All 1150 customer and 180 supplier unsettled document amounts/signs remain. Customer Amount uses creamt only. Cheques remain separate. Four post-closing supplier documents use test date 2026-09-30 and due date 2026-10-30; original dates are logged.
- Four missing customers and two missing suppliers have explicit UAT names with retained legacy codes and blank contacts. BA366 email remains the confirmed blank.
- Suppliers use DOM, NET30 and AP 2110. Expense 5110 is temporary for unresolved suppliers and the two added fixtures. RE187/RS149 retain 6210, EL264 retains 6220.
- All 938 stock quantities and final approved unit costs remain. WH_MAIN, test LayerDate 2026-09-30 and ConversionFactor 1 are fixture choices. Existing SKU fields use their actual masters. The {s['new_skus']} new FG SKUs use MSH / UNBRANDED / pcs.
- {s['branch_substitutions']} cheque branches use documented substitute UAT mappings. Three exact source branch matches remain exact. InSafe / PartyReflected=true / BankReflected=false remain the user's confirmed received-cheque treatment.
- Synthetic bank book 100000, statement 112500, issued cheque 15000 and deposit 2500 exercise bank reconciliation; no real bank source was supplied.
- Accounts mirrors all 39 active posting accounts. The synthetic TB derives from opening journals plus rent Debit / sales Credit 1000000 each; all 39 current accounts covered, 30 test zeros. Original 188 TB rows and -613120 imbalance remain unchanged.
- ControlTotals contains {s['rows']['ControlTotals']} fixed expected UAT controls, including master-only zeros. It is not independent production closing evidence.
- Named synthetic fixtures have explicit permanent fixture IDs. Legacy rows use supported stable code/composite identities. No identity is based on an Excel row number.

{table(*rr['Datasets'])}

{table(['Module','Positive LKR','Negative LKR','Signed net LKR'],[[k,v['positive'],v['negative'],v['net']] for k,v in s['totals'].items()])}

Stock value {s['stock_value']} LKR. TB Debit = Credit = {s['tb_debit']} LKR. Expected TB residual Debit = Credit = 1000000.00. Supported duplicate identities: 0. All five zero-net customer cases retain original debt/credit rows.

reports/UAT_changes.csv records {s['changes']} field/fixture changes with original value, UAT value and reason. Reports/Expected_GL.csv and Expected_journals.csv show account-by-account expected import behavior. The UAT review workbook includes the same records. All ten source hashes were rechecked before creation.

Certified production closing exports, real expense/branch/new-SKU classification, complete historical archive and October activity transfer remain separate production work. No application imports, posting, finalization or database reset were executed during file preparation.
''')
guides=[ROOT/f'GUIDE_{i:02d}_{tail}.md' for i,tail in [(1,'Project_and_Staging'),(2,'Mappings_and_Defaults'),(3,'Masters'),(4,'Openings_Stock_and_Banks'),(5,'Reconciliation_and_Release'),(6,'Trial_Balance_and_Finalization'),(7,'Operational_UAT_Tests')]]
doc('UAT_Complete_Guide.md','# Complete NexusFlow UAT import guide\n\nUse the files from uat_upload_20261012. Work through Stages 1–7 in order. Test fixture assumptions and expected balances are recorded in UAT_Changes_and_Reconciliation.md.\n\n'+'\n\n'.join(p.read_text(encoding='utf-8').replace('# Stage','## Stage',1) for p in guides))
print(json.dumps({'guides':len(guides),'other_documents':4,'source_system':s['source_system']}))

