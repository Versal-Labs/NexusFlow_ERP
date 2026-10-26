# Seven upload packages — operator stages

Readiness and mapping setup come first. These package numbers are upload order; the web wizard separately labels its screens 1–7.

| Order | ZIP | Select only | Expected rows |
|---|---|---|---|
| 01 | 01_Masters.zip | Accounts, Customer Master, Supplier Master | 39 accounts; 896 customers; 215 suppliers |
| 02 | 02_Party_Openings.zip | Customer Openings, Supplier Openings | 1,150 customer documents; 180 supplier documents |
| 03 | 03_Cheques.zip | Received Cheques, Issued Cheques | 49 received cheques; 1 issued pilot cheque |
| 04 | 04_Combined_Stock.zip | Stock | 938 stock layers = 494 finished goods + 444 raw materials |
| 05 | 05_Banks.zip | Bank Opening, Bank Recon | 1 book opening; 1 non-cheque reconciliation item |
| 06 | 06_Control_Totals.zip | Control Totals | 5,212 control rows; 2,050 opening scopes |
| 07 | 07_Trial_Balance.zip | Trial Balance | 39 account rows; debit = credit 282,960,858.88 |

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

## Upload 01 — Masters

Complete the setup and mappings guide first. Upload 01_Masters.zip, select only Accounts, Customer Master and Supplier Master, then use the import cycle below.
Review NET30 on party records, TRADE/DOM groups and STANDARD TIER. Account rows link the exact active posting accounts; they do not create account folders.
The four customer and two supplier UAT fixture names are retained. Blank contact details remain flagged for later completion.
Expected: all selected master units imported; no opening financial journal yet.

## Upload 02 — Party Openings

Upload 02_Party_Openings.zip and use the import cycle for Customer Openings and Supplier Openings only.
Customer Amount is creamt. Positive documents are Invoice; negatives are ReturnCredit. No debamt-minus-creamt calculation is used. All unsettled documents remain, including offsetting debts and credits on zero-net parties.
Supplier positives remain Bill, including the confirmed OVER PAID/ADVANCE rows; negative returns are Credit. Thirty-day due dates are populated. Four earlier UAT date adjustments remain disclosed in Pilot_Assumptions.csv.
Expected customer debts 162,436,582.24 less credits 321,597.10 = 162,114,985.14.
Expected supplier debts 96,073,265.55 less credits 1,883,015.00 = 94,190,250.55.
These totals exclude received cheques.

## Upload 03 — Cheques

Upload 03_Cheques.zip and use the import cycle for Received Cheques and Issued Cheques only.
Expected received: 49 instruments, LKR 5,328,628.00, InSafe, PartyReflected=true, BankReflected=false. They establish cheques in hand without reducing receivables a second time.
Expected issued fixture: supplier AH143, reference UAT-ISSUED-001, cheque 000001, LKR 15,000.00, bank 1112; both reflection flags true. It is already reflected in supplier and bank book balances.
Keep cheque numbers as text. Do not enter these instruments again through ordinary receipt/payment entry.

## Upload 04 — Combined Stock

Before uploading, save ACCESSORIES -> Accessories and the 33 existing-SKU aliases. See 01_Setup_and_Mappings.md.
Verify WH_MAIN has no inventory-account override for this pilot. Accessories posts to 1131; finished-goods categories post to 1132. The CSV InventoryAccountCode must match the resolved warehouse/category posting account.
Upload 04_Combined_Stock.zip and select Stock only. Leave SKU codes blank, including for comma-containing codes. Use the import cycle once for the whole file.
Inspect SDASF -> sdasf, category Accessories, account 1131. Inspect one finished-goods row, account 1132.
Expected RM value 29,727,743.70 plus FG value 84,689,502.04 = 114,417,245.74.
The 81 new FG fixtures use MSH / UNBRANDED / pcs. Unit conversion factor is 1; the summarized pilot layer date is 2026-09-30.
Another Stock upload replaces the current complete dataset revision. Do not upload FG and RM separately.

## Upload 05 — Banks

Upload 05_Banks.zip and use the import cycle for Bank Opening and Bank Recon only.
Bank account 1112: book opening 100,000.00; statement 112,500.00; pending deposit +2,500.00; issued cheque pending -15,000.00 from stage 03.
Require statement bridge 112,500 + 2,500 - 15,000 = 100,000.
Do not repeat the issued cheque as another Bank Recon row.
All bank values in this stage are retained pilot fixtures.

## Upload 06 — Control Totals

Upload 06_Control_Totals.zip. Select Control Totals only and use the import cycle. Validation alone is insufficient: the controls must be approved and executed.
After completion, uncheck all modules and select Customer Master, Supplier Master, Stock and Bank Opening. Leave all four code filters blank.
In Signed independent schedule evidence enter a truthful pilot review reference, for example: Pilot 2026-10-15; prepared controls checked against the supplied pilot schedules and reconciliation report by [reviewer/date]. Functional test only.
Select Release Selected Reconciled Scopes. Refresh Progress. If some scopes were already released, the newly released count may be less than 2,050; the final scope list must contain all 2,050 as Released and none Pending.
Resolve any pending reason before continuing. The controls are an acceptance baseline reproduced from the test schedules, not independent certification of the original legacy closing reports.

## Upload 07 — Trial Balance

Only after every opening scope is released, upload 07_Trial_Balance.zip. Select Trial Balance only and use the import cycle.
The file is the complete tested pilot closing TB. Do not manually subtract AR, AP, stock or bank balances: the importer posts the residual.
Select Check Full Ledger. Require zero Difference for every account; compare reports/Expected_GL.csv.
Confirm all source rows are imported, all opening scopes released and no failed/pending work remains.
Before ordinary pilot transactions begin, capture the baseline reports and dashboard. Local reference: 2,455 Posted units and 2,050 Released scopes for the complete unchanged package. Different destination master resolution can alter unit grouping; investigate any mismatch.
For Final financial sign-off evidence enter truthful pilot acceptance evidence, e.g. Client pilot baseline reviewed on [date] by [name]; synthetic bank/issued cheque/TB and documented test assumptions accepted for functional testing only.
Select Finalize Migration. The project becomes read-only. This action closes the pilot migration workflow; it does not certify these fixtures as live financial balances.
