# Preparing the supplied Excel files for Migration Centre

Reviewed 8 October 2026 against the eight supplied workbooks and the current local Migration Centre implementation. This is a preparation and reconciliation guide. The original files were read without changing them; no balances were imported. An attempt to open the hosted `/Migration` page through @Browser returned `net::ERR_BLOCKED_BY_CLIENT`; hosted UAT deployment, permissions and screen availability therefore still require verification in the running system.

## Decision before preparing upload copies

Use one actual legacy closing date for all financial schedules. NexusFlow opens on the following day. The earlier proposal was 1 October 2026, but October transactions have continued in the old system. Either retain 1 October with complete 30 September closing exports and a separately controlled transfer of every subsequent transaction, or agree a later actual switch and obtain fresh closing exports including those transactions. The current wizard imports closing positions, not a general historical transaction replay. Actual dates are **Needs business confirmation**.

The supplied customer ageing implies 28 September; the cheque report explicitly says 28 September; raw stock says 13 September. Every supplier document date plus `grndays` points to 7 October. The supplier report date is therefore inferred, not explicitly certified. Neither the trial balance nor finished-stock workbook certifies its closing date. Changing a date cell does not make an old balance current.

Keep the original files as evidence. Make separate upload copies using [the version 1.0 template pack](NexusFlow-migration-templates-v1.0.zip). The six earlier report layouts have built-in readers; the two new supplier-outstanding and trial-balance layouts need canonical headers or explicit source-column mappings. Canonical files are preferable where missing fields must be added or rows combined.

## Financial reconciliation required

All values below are LKR and are comparisons of the supplied files, not approved opening balances. Different reporting dates, account coverage, valuation methods or legacy posting defects may explain differences; obtain a reconciled explanation rather than forcing the files to agree.

| Check | Detail schedule | Relevant trial-balance balance | Detail less GL |
|---|---:|---:|---:|
| Customer outstanding, signed net `creamt` | 162,114,985.14 | Debtors - Trade: 154,029,322.14 Dr | 8,085,663.00 |
| Supplier outstanding, provisional signed net `dtfbal` | 94,190,250.55 | Creditors - Local Purchase: 78,068,405.85 Cr | 16,121,844.70 |
| Cheques in hand | 5,328,628.00 | Cheques in Hand: 14,323,736.00 Dr | 8,995,108.00 |
| Raw plus finished stock, using finished `avar × totqty` | 114,417,245.74 | Closing Stock: 97,626,087.76 Dr | 16,791,157.98 |

The complete trial balance has **debits 686,897,710.78 and credits 687,510,830.78: credits exceed debits by 613,120.00**. The supplied file contains 188 distinct account rows, including 102 zero-balance accounts. Its row-level signed balances agree with the debit/credit columns, so simply swapping columns or changing headers will not repair the overall difference. Request the complete corrected closing trial balance and an explanation of the difference. Do not add an unexplained opening-equity/suspense line.

Stock comparison assumes the GL Closing Stock account covers both raw and finished inventory. Confirm coverage and account mappings. Using finished `costval` instead gives combined stock 117,591,064.16, a different discrepancy; neither basis is approved merely because it appears in the source.

## Common Excel rules

1. One dataset per file; put the data worksheet first. Canonical files have one unique, nonblank header row and detail rows only. Remove titles, subtotals, group totals and blank separator rows from canonical copies. Preserve the unedited originals separately.
2. CSV must be UTF-8; XLSX must contain values only, with no formulas/macros. Calculate helper columns in a working copy, then paste values into the upload copy. Renaming `.xls` to `.xlsx` is not conversion. Binary `.xls` is already supported.
3. Store legacy party codes, account codes, SKUs, references, phone numbers and cheque numbers as text. Preserve leading zeros. `000186` is a real cheque identifier, not the number 186.
4. Use dates `yyyy-MM-dd`, including the actual common `ClosingDate`. Keep original document dates. Cheque face dates and due dates may legitimately be later than closing; receipt/issue/document dates must represent the true legacy transactions.
5. Use numeric values without currency symbols or thousands separators in CSV: money at most 2 decimals, quantity at most 4, unit cost at most 6. Negative amounts must retain their business meaning; never take absolute values of all amounts.
6. Use stable source `LegacyRecordId` values where available. Otherwise retain a stable unique document identity. Row numbers or a new random ID on every export are unsuitable. Stock identity must distinguish warehouse and layer/batch as applicable.
7. Map every nonblank legacy classification to an actual active NexusFlow record. Defaults apply to missing values, not to unrecognized nonblank codes. Blank contact details are allowed and flagged for later completion; do not invent email addresses or phone numbers.
8. Old account codes contain internal spaces, for example `B450    -001`. Preserve them for exact mapping, or normalize them consistently in every related export and mapping table. Trimming only the trial balance leaves the supplier payable-account references unmatched.
9. Each dataset is limited to 20 MB and 20,000 normalized rows. One current revision is retained per module. Later uploads replace that module's staged source; they do not append a second file. Keep the complete relevant closing dataset in each revision.

## 1. Customer master

Source: `System uploads/New Data/customermasterfile.xls`, sheet `customermasterfile`, rows 2–893: **892 customers**. The current legacy layout can be read directly.

| Existing column | Canonical field |
|---|---|
| `debcode` | `Code` — preserve the old customer code |
| `debname` | `Name` |
| `debcper` | `Contact` |
| `debmail` | `Email` |
| `debtel1`, `debtel2` | `Phone`, `Mobile` |
| `debadd1`, `debadd2` | `Address1`, `Address2` |

Add or select approved defaults/mappings for `GroupCode`, `PaymentTermCode`, `PriceLevelCode`, `ReceivableAccountCode` and `RevenueAccountCode`. Genuine contact blanks remain blank. If retaining the third address line, deliberately combine it into a supported address field within its length limit. Old sales-representative/area/fax flags are not automatically imported by this reader.

The outstanding file contains **GA735, TK1039, UP1038 and VI1041**, which are absent from the supplied customer master. Obtain their genuine masters or confirm exact existing legacy-code matches in NexusFlow. Do not drop those outstanding rows or match a similarly named customer.

## 2. Supplier master

Source: `System uploads/New Data/supliermasterfile.xls`, sheet `supliermasterfile`, rows 2–214: **213 suppliers**. The current layout can be read directly.

| Existing column | Canonical field |
|---|---|
| `supcode` | `Code` — use this, not `debcode` |
| `supname` | `Name` |
| `supcontact` | `Contact` |
| `supemail`, `suptele` | `Email`, `Phone` |
| `supadd1`, `supadd2` | `Address1`, `Address2` |
| `tinno` | `TaxNumber` |
| `supgroup` | `GroupCode` |
| `supacc` | `PayableAccountCode` |

Map supplier groups **A, B, C, G and S** to approved groups. Supply `PaymentTermCode` and `ExpenseAccountCode` through canonical fields or approved defaults. Confirm tax-field meaning before substituting `vatno` for `tinno`. Genuine contact blanks are allowed.

All 213 `supacc` values are `B450    -001`, requiring one explicit legacy-account mapping unless the business supplies a corrected allocation. All master `supbal` values are zero; do not treat those zeros as the real supplier opening balance.

Add genuine masters for **SH331 — SHEYAHS GARMENTS** and **WL329 — WASH LOOK WASHING PLANT - AKURESSA**, or confirm exact existing legacy-code matches. They occur in the new AP schedule but are absent from this master.

## 3. Customer outstanding

Source: `imports new/cusout.xls`, sheet `cusout`, rows 2–1151: **1,150 opening documents across 331 customers**. Original layout supported.

| Existing value | Canonical field/treatment |
|---|---|
| `debcode` | `Code` |
| `ref` | `Reference` |
| `tdate` | `DocumentDate` |
| `creamt` | `Amount`: confirmed amount still owed |
| Positive remaining amount | `Kind=Invoice` for the opening debt |
| Negative returned-goods amount | `Kind=ReturnCredit`, negative `Amount` |
| Original document date + 30 days | `DueDate`; the wizard also applies this when blank |

Do **not** calculate `debamt - creamt`, and do not upload the original debit amount as the remaining amount. Keep one row per open item and preserve credits even for customers whose net balance is zero. Opening returned-goods credits do not generate another stock return.

Current gross debts: **162,436,582.24**; credits: **321,597.10**; net: **162,114,985.14**. Refresh the closing export, reconcile it to the debtor GL control and resolve the four missing customer masters. Invoice/opening balances and collectible cheques remain separate schedules; do not add cheque face values back into every invoice balance.

## 4. Supplier outstanding — new file

Source: `imports new/supplier outstanding.xlsx`, sheet `suplier`, rows 2–181: **180 documents across 52 suppliers**. Use the `SupplierOpenings` template; there is no automatic adapter for these new legacy column names. A source-column profile can map equivalent fields, but does not resolve ambiguous business signs or invent `Kind`.

| Existing column | Canonical field | Preparation |
|---|---|---|
| `supcode` | `Code` | Exact supplier legacy code |
| `dtfref` | `Reference` | Original document reference |
| `dtfdate` | `DocumentDate` | Actual document date |
| `dtfbal` | `Amount` | **Needs business confirmation:** remaining signed payable |
| `paydate` | `DueDate` | Confirm this means contractual due date; remove `  -   -` placeholders |
| Confirmed obligation/credit type | `Kind` | `Bill` for positive debts; `Credit` or `Advance` for negative available credits |
| Stable document ID | `LegacyRecordId` | Source ID or stable unique identity |
| Certified snapshot date | `ClosingDate` | Same closing date as all other financial datasets |

Do not use `dtfamt` (original amount), repeated `total`, `duedays`, `grndays` or cheque columns as remaining payable. `dtftype` values 1/2 alone do not establish the NexusFlow `Kind`.

Using `dtfbal` provisionally gives positive debts **96,073,265.55**, negative credits **1,883,015.00**, net **94,190,250.55**. Confirm the meaning/sign with the old-system developer before producing the upload.

Specific corrections/confirmations:

- **75 currently positive rows have no valid `paydate`**. Obtain due dates or approve supplier terms. The customer 30-day agreement must not silently become the supplier agreement.
- **Excel row 49, LH109 / GR00001448:** document date 20 March 2026 but `paydate` 19 March 2026; `crdays=-1`. Correct/confirm the source because NexusFlow rejects a due date preceding its document date.
- Seven rows have negative balances. AD287 has cash advances and an `RT CHQ` remark; MA197 and NA200 have positive original bills but negative remaining balances. Preserve confirmed credits, but investigate the returned-cheque row rather than automatically classifying it as an advance.
- Some positive balances are labelled `OVER PAID` or `ADVANCE`, for example **AH143 / CD00000255: +9,999.48**, and **TI248 / CD00001196: +100,000.00**. Determine whether these are actual liabilities or available supplier credits. Do not flip signs solely from remarks.
- Resolve missing masters SH331 and WL329 and reconcile the agreed payable-account coverage to GL. Their current remaining amounts are 402,400.00 and 758,970.00 respectively.
- All `chqamt` values are zero and cheque references are blank. This workbook does **not** supply an issued-cheque register and does not prove that no issued cheques exist.

## 5. Cheques in hand

Source: `imports new/cheque in hand.xlsx`, sheet `Sheet`: **49 instruments across 17 customers, total 5,328,628.00**, report dated 28 September. The original formatted report is supported; a canonical copy must contain instrument rows only, without subtotal/grand-total rows.

| Report location | Canonical field |
|---|---|
| Customer Code, column E | `Code` |
| Ref No, column C | `Reference` |
| Inv No, column D | `DocumentReferences` |
| Cheque Number, column H | `ChequeNumber`, text |
| Received Date, column B | `ReceivedDate` |
| Bank Code, column G | `BankBranchCode`, mapped to the real branch |
| Amount, column I | `Amount`, positive |
| Actual date on cheque | `ChequeDate` |

**Needs business confirmation:** column A is titled `To be Dep Date`. The existing adapter carries that date forward to grouped instrument rows and treats it as `ChequeDate`. Confirm it equals the date written on each cheque; a planned deposit date must not replace the actual face date. A canonical file can supply the verified face date explicitly.

Confirm physical custody and ledger treatment at the actual closing. For genuine cheques held in the safe whose receipt has already reduced customer AR: `Status=InSafe`, `PartyReflected=true`, `BankReflected=false`, with the mapped cheque-safe asset account. Deposited or endorsed instruments require their actual status and bank/supplier information and must not be classified as InSafe merely to use the report adapter. Obtain returned/dishonoured and transferred instruments in the appropriate corrected schedules. No second historical customer receipt is posted.

## 6. Raw-material stock

Source: `Stock/Raw Material Stcok.xlsx`, sheet `Sheet1`, rows 2–445: **444 SKUs**, quantity **6,306,283**, value **29,727,743.70**. The layout is supported, but every `OpeningDate` is 13 September 2026. Refresh quantities and costs at actual closing.

| Existing column | Canonical Stock field |
|---|---|
| `SKU`, `ProductName` | `Code`, `Name` |
| Literal `RawMaterial` | `ProductType` |
| `Brand`, `Category`, `Unit` | `BrandCode`, `CategoryCode`, `UnitCode` |
| `WarehouseCode`, `Quantity`, `UnitCost` | Same fields |
| Genuine receipt/layer date | `LayerDate`, if original layers are supplied |
| Base-unit quantity already supplied | `ConversionFactor=1` |

The current source uses `WH-MAIN`, `Pcs`, `UNBRANDED` and `ACCOSORIES NEW`; map these to real approved records. Confirm that the category and warehouse post to the correct raw-inventory account. Do not select a category solely by its name. Snapshot `OpeningDate` is not proof of original FIFO receipt date.

Prefer actual remaining FIFO layers. If those are unavailable, obtain signed approval for a summarized opening layer and its valuation; retain that evidence in the wizard. For the summarized treatment, leave missing `LayerDate` blank and use the explicit approval rather than manufacturing historical dates.

## 7. Finished-goods stock

Source: `Stock/stock.xls`, sheet `stock`, rows 2–495: **494 SKUs**, total quantity **68,986**. This review uses the latest supplied `Stock` folder file, not a second copy from `imports new`.

| Existing column | Canonical Stock treatment |
|---|---|
| `itemcode`, `itemname` | `Code`, `Name` |
| Literal `FinishedGood` | `ProductType` |
| `type` | `CategoryCode`, approved mapping |
| `brand` | `BrandCode`; currently blank, supply an approved brand for new SKUs |
| `loc1` through `loc8` | Separate SKU/warehouse/layer rows with `Quantity` from each location |
| Confirmed location-to-warehouse mapping | `WarehouseCode` |
| Actual base unit | `UnitCode`, missing in source |
| Approved genuine valuation | `UnitCost` |
| `selprice`, `size`, `color`, `lotno` | `SellingPrice`, `Size`, `Color`, `Batch`, where applicable |

Do not upload `totqty` in addition to the separate location quantities: this doubles stock. `loc1` totals **68,206** and `loc3` **780**; other locations are zero. Confirm real warehouses for both nonzero locations.

Valuation needs resolution: `avar × totqty` totals **84,689,502.04**, while `costval` totals **87,863,320.46**. **202 rows disagree**, and `costval` is negative on Excel rows **7, 99 and 253**. Do not take absolute values, use selling price as cost, or silently choose whichever basis matches GL. Request correct remaining FIFO layers or an accountant-approved summarized valuation. The legacy reader requires explicit AverageCost evidence when selecting `avar`; a canonical file must still retain the external approved reconciliation evidence.

## One combined Stock upload

Transform the refreshed raw and finished sources into **one `Stock.csv` or values-only XLSX** using the canonical Stock headers. Include `ProductType` on every row. Preserve all layers and warehouses. There is no SKU overlap between the two supplied sources, but continue checking new exports for duplicate identities and conflicting classification.

Do not put raw materials and finished goods on separate tabs expecting both to be read: only the first worksheet is read. Do not put two Stock entries in a package manifest. If a later stock revision is uploaded, include the complete relevant closing dataset rather than only the new batch.

## 8. Trial balance and chart of accounts — new file

Source: `imports new/trial balance.xlsx`, sheet `tb`, rows 2–189. No automatic adapter for these header names; use canonical fields or the explicit column profile.

| Existing column | Canonical TrialBalance field |
|---|---|
| `acccod` | `Code` |
| `cfamtdr` | `Debit` |
| `cfamtcr` | `Credit` |
| Certified closing date | `ClosingDate` |
| Stable account identity | `LegacyRecordId`, if supplied |

Use nonnegative debit/credit values with only one side nonzero per posting account. Retain zero rows; add an explicit zero row for any active NexusFlow posting account absent after approved mapping. If multiple old accounts legitimately map to one target account, produce one reviewed net closing balance for that target and preserve the mapping breakdown as evidence.

Do not also import `bfamt`, `xdatdr`, `xdatcr`, `xdatamt` or signed `cfamt` as additional balances. Supply the **complete corrected closing TB**, including the AR/AP/inventory/cheque/bank control totals. Do not manually deduct those controls: after scope reconciliation, the wizard calculates the residual GL opening without duplicating their opening journals.

Prepare a separate `Accounts` file only if account masters are needed: `acccod → Code`, `accdes → Name`, with an accountant-approved `AccountType` (`Asset`, `Liability`, `Equity`, `Revenue`, `Expense`) and existing `ParentCode` where required. Source `acctyp=B/P` and `accsc1` are legacy classifications, not valid replacement values for `AccountType`. Create/map account folders through normal maintenance before importing posting accounts.

Besides the 613,120.00 overall imbalance, review the petty-cash credit balance 113,540.00, capital debit balance 20,902,014.90 and the Profit & Loss account alongside current-year revenue/expense balances. They may have legitimate explanations; do not reverse signs or transfer profit to equity without the accountant's agreed closing treatment. Keep mid-year balances and fiscal year-end closing decisions distinct.

## Remaining data before a complete financial migration

- Corrected, certified closing trial balance plus exact chart/control-account mappings and reconciled subledger totals.
- Fresh AR/AP/inventory/cheque schedules at the same closing, including the missing masters and the resolved supplier balance/due-date meanings.
- `IssuedCheques`: every outstanding issued cheque, actual supplier, issue date, face date, cheque number, amount, bank account and ledger-reflection flags. Issued opening cheques in this release must already be reflected in supplier and bank book balances; otherwise obtain a corrected source position.
- `BankOpening`: signed bank/cash book positions and statements per applicable mapped account. Named bank GL accounts being zero is not sufficient evidence of empty bank registers.
- `BankRecon`: other uncleared items, excluding any cheque already included in the received/issued registers, with reference/date/amount and reflection information. Reconcile book to statement through outstanding deposits/payments.
- Independent `ControlTotals` and row counts per customer, supplier, SKU/warehouse and bank, including genuinely zero components. These must come from approved legacy reports/counts; copying the wizard's proposed totals is not independent verification.
- Actual remaining stock cost layers, or documented approval for summarized layers/valuation, plus warehouse counts and unit conversions.
- Supporting fixed-asset/depreciation, loan, tax, payroll/accrual and work-in-progress schedules where the TB has such balances. This release brings their approved GL balances through the full TB; detailed operational register importers are not part of these twelve modules. Preserve the agreed read-only legacy archive.

Unknown data stays **Awaiting Data**. Use **Not Applicable** only with independent evidence that the component is genuinely zero/not applicable. Missing spreadsheets must not be replaced with invented zero rows.

## Upload, rehearse and complete

1. Confirm actual freeze, closing date and next-day opening; collect and approve the corrected source package. Rehearse on a clean UAT opening ledger and retain a recoverable backup.
2. Use **Administration → Settings & Security → Migration Centre**, `/Migration`. Deploy/verify the implemented wizard and schema before uploading; local source verification alone does not establish hosted availability.
3. Select **New Project** and the actual opening date, then **Create Project**. Resume the same project for later supplies. Already imported or traded balances need the supported adoption/correction process, not another opening project.
4. **Download Template Pack**; prepare all copies using its exact headers. `ClosingDate` is the common legacy closing date, not each original document date.
5. **Upload Dataset** for each available module, or **Upload Package** with one manifest entry per module. A package stages files; it does not mean all modules should be posted together. Omit empty/missing templates from the manifest.
6. In **Map and confirm source treatment**, add exact legacy-to-target mappings, approve the necessary defaults/treatment evidence, then **Save Mappings and Evidence**.
7. Import account/master dependencies first. For financial imports choose complete customer/supplier/SKU/bank scopes; a selected code includes every closing row for that code. For a customer, reconcile both opening documents and received cheques, including a genuine zero-cheque component.
8. **Validate Selected Import**, review rows, warnings and proposed journals, fix errors, enter business evidence, then **Approve Selected Import** and **Execute Approved Import**. Missing modules may be supplied later at the same original closing date; a new-date closing export belongs to a different controlled correction strategy.
9. **Reconcile** against independent amounts/counts and **Release Reconciled Scope** (or use the independent control schedule and bulk release). Release permits that complete scope to operate while others remain pending.
10. Only after all required financial schedules/scopes reconcile, validate/approve/execute the full corrected `TrialBalance` separately. Select **Check Full Ledger** and resolve every account difference through the correct owning module.
11. Enter **Final financial sign-off evidence** and **Finalize Migration** when all required data, release checks and ledger comparisons pass. Retain source exports, transformed copies, mappings, differences, approvals and run results.

This preparation approach uses open-document balances, explicit master mappings, dependency order, migration rehearsal and independent ledger reconciliation, consistent with [Microsoft's data-migration guidance](https://learn.microsoft.com/en-us/dynamics365/guidance/resources/migrate-data) and [customer/vendor-to-GL reconciliation guidance](https://learn.microsoft.com/en-au/dynamics365/business-central/reports/report-33). It is not a claim of an industry certification.

For exact field headers and detailed reconciliation components, see [Migration Centre operating guide](Migration_Centre_Runbook.md). Final date, supplier signs/terms, cheque face-date meaning and approved inventory valuation remain **Needs business confirmation** until answered and evidenced.
