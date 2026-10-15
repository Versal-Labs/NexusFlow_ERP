# Source mapping and Migration Centre selections

Prepared for opening 2026-10-01, closing 2026-09-30, LKR. The seven workbooks use the existing v1.0 templates with exact ordered headers, including ClosingDate. Database IDs appear only as mapping target references in the review report; template Code fields always contain actual source/master codes.

## Exact source transformations

| Module | Source | Template field | Transformation | State |
|---|---|---|---|---|
| CustomerMaster | debcode | Code | Trim surrounding whitespace only; preserve identifiers as text. | Applied |
| CustomerMaster | debname | Name | Trim surrounding whitespace only; preserve identifiers as text. | Applied |
| CustomerMaster | debcper | Contact | Trim surrounding whitespace only; preserve identifiers as text. | Applied |
| CustomerMaster | debmail | Email | Trim surrounding whitespace only; preserve identifiers as text. BA366 email is blank by explicit user confirmation. | Applied |
| CustomerMaster | debtel1 | Phone | Trim surrounding whitespace only; preserve identifiers as text. | Applied |
| CustomerMaster | debtel2 | Mobile | Trim surrounding whitespace only; preserve identifiers as text. | Applied |
| CustomerMaster | debadd1 | Address1 | Trim surrounding whitespace only; preserve identifiers as text. | Applied |
| CustomerMaster | debadd2 + debadd3 | Address2 | Join nonblank original address lines with semicolon and space; no truncation or inferred contact number. | Applied |
| CustomerMaster | User confirmation / current configuration | GroupCode | TRADE; actual master code, never database ID. | Applied |
| CustomerMaster | User confirmation / current configuration | PaymentTermCode | NET30; actual master code, never database ID. | Applied |
| CustomerMaster | User confirmation / current configuration | PriceLevelCode | STANDARD TIER; actual master code, never database ID. | Applied |
| CustomerMaster | User confirmation / current configuration | ReceivableAccountCode | 1120; actual master code, never database ID. | Applied |
| CustomerMaster | User confirmation / current configuration | RevenueAccountCode | 4110; actual master code, never database ID. | Applied |
| SupplierMaster | supcode | Code | Trim outer whitespace only. Preserve original codes/internal spaces; A/B/C/G/S explicitly map to DOM. | Applied |
| SupplierMaster | supname | Name | Trim outer whitespace only. Preserve original codes/internal spaces; A/B/C/G/S explicitly map to DOM. | Applied |
| SupplierMaster | supcontact | Contact | Trim outer whitespace only. Preserve original codes/internal spaces; A/B/C/G/S explicitly map to DOM. | Applied |
| SupplierMaster | supemail | Email | Trim outer whitespace only. Preserve original codes/internal spaces; A/B/C/G/S explicitly map to DOM. | Applied |
| SupplierMaster | suptele | Phone | Trim outer whitespace only. Preserve original codes/internal spaces; A/B/C/G/S explicitly map to DOM. | Applied |
| SupplierMaster | supadd1 | Address1 | Trim outer whitespace only. Preserve original codes/internal spaces; A/B/C/G/S explicitly map to DOM. | Applied |
| SupplierMaster | tinno | TaxNumber | Trim outer whitespace only. Preserve original codes/internal spaces; A/B/C/G/S explicitly map to DOM. | Applied |
| SupplierMaster | supgroup | GroupCode | Trim outer whitespace only. Preserve original codes/internal spaces; A/B/C/G/S explicitly map to DOM. | Applied |
| SupplierMaster | supacc | PayableAccountCode | Trim outer whitespace only. Preserve original codes/internal spaces; A/B/C/G/S explicitly map to DOM. | Applied |
| SupplierMaster | supadd2 + supadd3 + supadd4 | Address2 | Join nonblank lines with semicolon and space. | Applied |
| SupplierMaster | User confirmed 30 days | PaymentTermCode | NET30; explicitly map to 30 Days. | Applied |
| SupplierMaster | No source / unresolved accounting assignment | ExpenseAccountCode | 6210 for RE187/RS149 (explicit rent suppliers), 6220 for EL264 (explicit electricity supplier), others blank. Active Expense posting accounts selected for clear source purposes. | Needs business confirmation |
| CustomerOpenings | debcode | Code | Preserve actual value/sign; ISO date or numeric LKR amount. No original-minus-paid calculation. | Applied |
| CustomerOpenings | ref | Reference | Preserve actual value/sign; ISO date or numeric LKR amount. No original-minus-paid calculation. | Applied |
| CustomerOpenings | tdate | DocumentDate | Preserve actual value/sign; ISO date or numeric LKR amount. No original-minus-paid calculation. | Applied |
| CustomerOpenings | creamt | Amount | Preserve actual value/sign; ISO date or numeric LKR amount. No original-minus-paid calculation. | Applied |
| CustomerOpenings | tdate + 30 days | DueDate | Confirmed calendar-day terms. Original customer document date retained. | Applied |
| CustomerOpenings | creamt sign | Kind | Invoice / ReturnCredit for customers; Bill / Credit for suppliers. Negative amount preserved. | Applied |
| SupplierOpenings | supcode | Code | Preserve actual value/sign; ISO date or numeric LKR amount. No original-minus-paid calculation. | Applied |
| SupplierOpenings | dtfref | Reference | Preserve actual value/sign; ISO date or numeric LKR amount. No original-minus-paid calculation. | Applied |
| SupplierOpenings | dtfdate | DocumentDate | Preserve actual value/sign; ISO date or numeric LKR amount. No original-minus-paid calculation. | Applied |
| SupplierOpenings | dtfbal | Amount | Preserve actual value/sign; ISO date or numeric LKR amount. No original-minus-paid calculation. | Applied |
| SupplierOpenings | dtfdate + 30 days | DueDate | Confirmed calendar-day terms. Original supplier paydate/crdays retained and discrepancies reported. | Applied |
| SupplierOpenings | dtfbal sign | Kind | Invoice / ReturnCredit for customers; Bill / Credit for suppliers. Negative amount preserved. | Applied |
| ReceivedCheques | E / Customer Code | Code | Only instrument detail rows recognized by CH receipt reference; retain text references/leading zeros. | Applied |
| ReceivedCheques | C / Ref No | Reference | Only instrument detail rows recognized by CH receipt reference; retain text references/leading zeros. | Applied |
| ReceivedCheques | D / Inv No | DocumentReferences | Only instrument detail rows recognized by CH receipt reference; retain text references/leading zeros. | Applied |
| ReceivedCheques | H / Cheque Number | ChequeNumber | Only instrument detail rows recognized by CH receipt reference; retain text references/leading zeros. | Applied |
| ReceivedCheques | B / Received Date | ReceivedDate | Only instrument detail rows recognized by CH receipt reference; retain text references/leading zeros. | Applied |
| ReceivedCheques | G / Bank Code | BankBranchCode | Only instrument detail rows recognized by CH receipt reference; retain text references/leading zeros. | Applied |
| ReceivedCheques | I / Amount | Amount | Only instrument detail rows recognized by CH receipt reference; retain text references/leading zeros. | Applied |
| ReceivedCheques | A / To be Dep Date, within report group | ChequeDate | Carry forward only until Total for subtotal; user confirmed it is cheque face date. | Applied |
| ReceivedCheques | User confirmation / current configuration | Status | InSafe | Applied |
| ReceivedCheques | User confirmation / current configuration | PartyReflected | true | Applied |
| ReceivedCheques | User confirmation / current configuration | BankReflected | false | Applied |
| ReceivedCheques | User confirmation / current configuration | SafeAccountCode | 1151 | Applied |
| Stock | itemcode / SKU | Code | Finished goods / raw materials respectively. Preserve source code meanings; new product classifications need mapping. | Applied |
| Stock | itemname / ProductName | Name | Finished goods / raw materials respectively. Preserve source code meanings; new product classifications need mapping. | Applied |
| Stock | type / Category | CategoryCode | Finished goods / raw materials respectively. Preserve source code meanings; new product classifications need mapping. | Applied |
| Stock | brand / Brand | BrandCode | Finished goods / raw materials respectively. Preserve source code meanings; new product classifications need mapping. | Applied |
| Stock | lotno / absent | Batch | Finished goods / raw materials respectively. Preserve source code meanings; new product classifications need mapping. | Applied |
| Stock | selprice / absent | SellingPrice | Finished goods / raw materials respectively. Preserve source code meanings; new product classifications need mapping. | Applied |
| Stock | size / absent | Size | Finished goods / raw materials respectively. Preserve source code meanings; new product classifications need mapping. | Applied |
| Stock | color / absent | Color | Finished goods / raw materials respectively. Preserve source code meanings; new product classifications need mapping. | Applied |
| Stock | Source file | ProductType | FinishedGood for stock.xls; RawMaterial for Raw Material Stcok.xlsx. | Applied |
| Stock | loc1..loc8 / Quantity | Quantity | Sum FG locations per SKU in one approved WH_MAIN layer; use raw Quantity unchanged. Do not also add totqty. | Applied |
| Stock | avar / UnitCost | UnitCost | User-approved FG avar; raw supplied cost unchanged. Six decimal places. costval is retained only for reconciliation. | Applied |
| Stock | Existing exact SKU base unit / Unit | UnitCode | FG exact-match base unit Symbol where available; new FG blank. Raw Unit retained; explicit Pcs mapping required. | Applied |
| Stock | User-approved common warehouse | WarehouseCode | WH_MAIN on all rows, replacing FG loc1/loc3 and raw WH-MAIN. Original locations retained in archive. | Applied |
| Stock | Existing exact SKU category posting account | InventoryAccountCode | Actual current account code where available; verify RM Asset 1131 / FG Asset 1132. | Applied |
| Stock | Original FIFO dates absent | LayerDate | Blank. Approved summarized layer; never use 13 September snapshot as an invented receipt date. | Applied |
| Stock | Source units not proven to differ | ConversionFactor | Blank, which validator treats as 1. Any actual unit difference blocks validation until a verified source-to-base factor is supplied. | Applied |
| TrialBalance | acccod | Code | Preserve actual closing balances, all zero rows and internal code spaces. cfamt checked independently against Debit-Credit. | Applied |
| TrialBalance | cfamtdr | Debit | Preserve actual closing balances, all zero rows and internal code spaces. cfamt checked independently against Debit-Credit. | Applied |
| TrialBalance | cfamtcr | Credit | Preserve actual closing balances, all zero rows and internal code spaces. cfamt checked independently against Debit-Credit. | Applied |
| CustomerMaster | No permanent transaction ID field present | LegacyRecordId | Blank. Use importer-supported code/composite identity; exact duplicate check included. Excel row number is provenance only. | Applied |
| CustomerMaster | Snapshot evidence | ClosingDate | 2026-09-30 project metadata only; master files contain no opening amounts. | Applied |
| CustomerMaster | Not supplied / not applicable | TaxNumber | Blank, preserved as missing. Do not fabricate a value. | Applied |
| SupplierMaster | No permanent transaction ID field present | LegacyRecordId | Blank. Use importer-supported code/composite identity; exact duplicate check included. Excel row number is provenance only. | Applied |
| SupplierMaster | Snapshot evidence | ClosingDate | 2026-09-30 project metadata only; master files contain no opening amounts. | Applied |
| SupplierMaster | Not supplied / not applicable | Mobile | Blank, preserved as missing. Do not fabricate a value. | Applied |
| CustomerOpenings | No permanent transaction ID field present | LegacyRecordId | Blank. Use importer-supported code/composite identity; exact duplicate check included. Excel row number is provenance only. | Applied |
| CustomerOpenings | Snapshot evidence | ClosingDate | 2026-09-28, actual report date. HOLD: importer rejects it for 30 September project. | Applied |
| SupplierOpenings | No permanent transaction ID field present | LegacyRecordId | Blank. Use importer-supported code/composite identity; exact duplicate check included. Excel row number is provenance only. | Applied |
| SupplierOpenings | Snapshot evidence | ClosingDate | Blank because uncertified/unknown. Supplier ageing separately infers 2026-10-07. HOLD; do not select 30 September in UI to disguise it. | Applied |
| ReceivedCheques | No permanent transaction ID field present | LegacyRecordId | Blank. Use importer-supported code/composite identity; exact duplicate check included. Excel row number is provenance only. | Applied |
| ReceivedCheques | Snapshot evidence | ClosingDate | 2026-09-28, actual report date. HOLD: importer rejects it for 30 September project. | Applied |
| ReceivedCheques | Not supplied / not applicable | BankAccountCode | Blank, preserved as missing. Do not fabricate a value. | Applied |
| ReceivedCheques | Not supplied / not applicable | SupplierCode | Blank, preserved as missing. Do not fabricate a value. | Applied |
| Stock | No permanent transaction ID field present | LegacyRecordId | Blank. Use importer-supported code/composite identity; exact duplicate check included. Excel row number is provenance only. | Applied |
| Stock | Snapshot evidence | ClosingDate | Raw rows 2026-09-13 from OpeningDate; FG blank because unknown. HOLD complete combined file. | Applied |
| TrialBalance | No permanent transaction ID field present | LegacyRecordId | Blank. Use importer-supported code/composite identity; exact duplicate check included. Excel row number is provenance only. | Applied |
| TrialBalance | Snapshot evidence | ClosingDate | Blank because the trial-balance snapshot date is uncertified. HOLD; obtain a certified 30 September export. | Applied |

Unsupported source fields remain in Legacy_Archive.xlsx and the original ZIP. They are not silently discarded or forced into unrelated fields. No permanent transaction-ID column exists in these supplied schedules. Leave LegacyRecordId blank and use the supported composite identity; Stable identities lists every identity and Row trace separately lists source row locations. Stock aggregation into the approved common warehouse avoids identity collisions between loc1 and loc3 for the same SKU. Stable identities records the uploaded Stock composite with a blank LayerDate; after summarized-layer approval the validator sets LayerDate to project closing before computing the final posted identity. This is a stable project-level date, never an Excel row number.

## Exact mappings to add

Use Mapping type → Exact legacy code → NexusFlow master → Add Mapping. These selections are for the current local database and must be rechecked in the destination environment. Every existing Stock match is listed in Migration_Review.xlsx and reports/Wizard_mappings.csv.

| Type | Source code | Local target ID | Master label | State | Reason |
|---|---|---|---|---|---|
| CustomerGroup | TRADE | 13 | Trade | Confirmed | Explicit mapping required even when code already matches. |
| PriceLevel | STANDARD TIER | 10 | Standard Tier | Confirmed | Explicit mapping required even when code already matches. |
| PaymentTerm | NET30 | 27 | 30 Days | Confirmed | Explicit mapping required even when code already matches. |
| SupplierGroup | A | 15 | Domestic Suppliers | Confirmed | User approved all source supplier groups to DOM. |
| SupplierGroup | B | 15 | Domestic Suppliers | Confirmed | User approved all source supplier groups to DOM. |
| SupplierGroup | C | 15 | Domestic Suppliers | Confirmed | User approved all source supplier groups to DOM. |
| SupplierGroup | G | 15 | Domestic Suppliers | Confirmed | User approved all source supplier groups to DOM. |
| SupplierGroup | S | 15 | Domestic Suppliers | Confirmed | User approved all source supplier groups to DOM. |
| Account | B450    -001 | 2001 | 2110 — Accounts Payable (AP) | Reconciliation candidate | Active posting account of type Liability. Preserve internal source-code spaces. |
| Account | 1120 | 1001 | 1120 — Accounts Receivable (AR) | Verified configuration | Active posting account of type Asset. Preserve internal source-code spaces. |
| Account | 4110 | 9 | 4110 — Sales Revenue - FG | Verified configuration | Active posting account of type Revenue. Preserve internal source-code spaces. |
| Account | 1151 | 5006 | 1151 — Cheques In Hand | Verified configuration | Active posting account of type Asset. Preserve internal source-code spaces. |
| Account | 6210 | 10 | 6210 — Rent Expense | Verified configuration | Active posting account of type Expense. Preserve internal source-code spaces. |
| Account | 6220 | 11 | 6220 — Electricity & Utilities | Verified configuration | Active posting account of type Expense. Preserve internal source-code spaces. |
| Warehouse | WH_MAIN | 3 | Main Garnet | Confirmed | All source locations combined into this approved warehouse. |
| Unit | Pcs | 1 | Piece | Exact label candidate | Required explicit mapping. Source quantities must be pieces; current exact matched base unit must agree. |
| Unit | pcs | 1 | Piece | Exact existing base unit | Copied from exact SKU master, without inventing source conversions. |
| Brand | UNBRANDED | 5 | UNBRANDED | Exact label candidate | Explicit mapping when needed for a new product. |
| BankBranch | 7010-058 |  |  | Unmatched | Create/verify actual bank branch; do not choose by similar bank name. |
| BankBranch | 7010-501 |  |  | Unmatched | Create/verify actual bank branch; do not choose by similar bank name. |
| BankBranch | 7056-006 | 84 | Commercial Bank of Ceylon / Kurunegala | Exact code match | Verify the branch on the physical instrument. |
| BankBranch | 7056-152 |  |  | Unmatched | Create/verify actual bank branch; do not choose by similar bank name. |
| BankBranch | 7083-064 |  |  | Unmatched | Create/verify actual bank branch; do not choose by similar bank name. |
| BankBranch | 7083-094 |  |  | Unmatched | Create/verify actual bank branch; do not choose by similar bank name. |
| BankBranch | 7135-040 |  |  | Unmatched | Create/verify actual bank branch; do not choose by similar bank name. |
| BankBranch | 7135-123 |  |  | Unmatched | Create/verify actual bank branch; do not choose by similar bank name. |
| BankBranch | 7135-200 |  |  | Unmatched | Create/verify actual bank branch; do not choose by similar bank name. |
| BankBranch | 7135-329 |  |  | Unmatched | Create/verify actual bank branch; do not choose by similar bank name. |
| BankBranch | 7162-012 | 224 | Nations Trust Bank / Kurunegala | Exact code match | Verify the branch on the physical instrument. |
| BankBranch | 7278-929 |  |  | Unmatched | Create/verify actual bank branch; do not choose by similar bank name. |
| BankBranch | 7287-085 |  |  | Unmatched | Create/verify actual bank branch; do not choose by similar bank name. |
| BankBranch | 7311-085 |  |  | Unmatched | Create/verify actual bank branch; do not choose by similar bank name. |
| BankBranch | 7454-026 |  |  | Unmatched | Create/verify actual bank branch; do not choose by similar bank name. |
| BankBranch | 7463-008 | 319 | Amana Bank / Kurunegala | Exact code match | Verify the branch on the physical instrument. |
| BankBranch | 7463-021 |  |  | Unmatched | Create/verify actual bank branch; do not choose by similar bank name. |

**Explicit CustomerGroup, PaymentTerm and PriceLevel mappings are mandatory.** Matching codes do not auto-resolve these lookups. Defaults apply only to blank codes. Supplier GroupCode stays A/B/C/G/S with five explicit mappings to Domestic Suppliers. PaymentTermCode is NET30 → 30 Days (local ID 27). Do not map it to CASH ON DELIVERY (COD), COD, or the saved erroneous TRADECASH ON DELIVERY (COD) alias. NET 30 DAYS (ID 9) is another current 30-day lookup, but the prepared file consistently uses NET30 and its exact mapping.

## Defaults and evidence to save

| Field | Selection | Reason |
|---|---|---|
| Opening equity account | 3200 — Retained Earnings | Verified local Equity posting account; project 3 currently selects it. |
| Default customer control | 1120 — Accounts Receivable (AR) | Configured Asset posting account. |
| Default customer revenue | 4110 — Sales Revenue - FG | Configured Revenue posting account. |
| Default supplier control | 2110 — Accounts Payable (AP) | Configured Liability posting account; map B450    -001 to it. |
| Default supplier expense | Leave unresolved | 5000 is a folder. 5110 Raw Material Consumption and 5120 Outsourced Job Work Costs are active Expense accounts, but supplier applicability is not established. |
| Undeposited cheque account | 1151 — Cheques In Hand | Configured Asset posting account; current SalaryAdvancesReceivable also uses 1151, so confirm GL coverage. |
| Default customer group | Trade (TRADE) | Confirmed, but also add explicit CustomerGroup mapping. |
| Default supplier group | Domestic Suppliers (DOM) | Confirmed; add explicit A/B/C/G/S mappings because source fields are nonblank. |
| Default price level | Standard Tier (STANDARD TIER) | Confirmed; add explicit PriceLevel mapping. |
| Default customer payment term | 30 Days (NET30) | Confirmed; explicit NET30 mapping to ID 27. Do not use COD. |
| Default supplier payment term | 30 Days (NET30) | Confirmed; explicit NET30 mapping to ID 27. |
| Approved base unit when source unit is missing | Leave unresolved for new FG SKUs | Source lacks unit for 81 unmatched finished goods. Existing matched base unit is retained. |
| Approved warehouse when source location is missing | Main Garnet (WH_MAIN) | Confirmed; populated on every Stock row with explicit mapping. |
| Approved supplier terms in days (optional) | 30 | Confirmed. Canonical DueDate is already document+30 on every supplier row. |
| Summarized stock layer approval | User approval recorded 11 October 2026 | Missing LayerDate deliberately left blank. The validator assigns closing only after approved summarized-layer evidence. |
| Legacy finished goods valuation | Average cost (avar × warehouse quantity) | User confirmed avar. Candidate discrepancy = 3,173,818.42 less than costval; retain signed financial review evidence. |

In Valuation discrepancy resolution evidence record: user confirmed 11 October 2026 to use avar, FG candidate value 84689502.04, costval 87863320.46, difference -3173818.42. Link this reconciliation and the accountant's final 30 September review. In Summarized stock layer approval record the user's summarized-layer and common-warehouse confirmation. No original layer dates have been invented. Canonical stock uses final UnitCost, so the legacy CandidateUnitCost guard is not carried into upload; external valuation evidence remains necessary.

## Account type checks

1120 AR = active Asset posting account. 2110 AP = active Liability posting account. 4110 FG sales = active Revenue posting account. 1131 RM and 1132 FG inventory = active Asset posting accounts. 1151 Cheques In Hand = active Asset posting account and configured undeposited funds. 3200 Retained Earnings = active Equity posting account, configured Account.Equity.OpeningBalance and already selected in current project 3. 5000 Cost of Goods Sold is an Expense **folder**, not an eligible posting account. 5130 Sales is typed Expense and cannot serve as customer revenue. 5110 Raw Material Consumption and 5120 Outsourced Job Work Costs are eligible Expense posting accounts, but applying either to every domestic supplier would invent supplier business treatment. Remaining supplier ExpenseAccountCode values and default expense remain unresolved pending actual supplier expense assignments or a genuine approved purchasing expense master.

The shared 1151 cheque/salary-advance configuration requires accounting review. InventoryAccountCode must agree with the actual product category and warehouse posting account. An explicit Stock row account cannot override a conflicting category. Raw CategoryCode ACCOSORIES NEW has no exact current category code; existing matched SKUs use their own verified category. A new SKU needs approved classification. Category RAW METERIAL NEW currently posts to FG inventory 1132 and must not be selected as raw inventory solely from its name.

## Complete trial-balance mapping

All 188 legacy codes, names and balances are retained. Internal spaces such as B450    -001 must be mapped exactly. Likely control-account correspondences are B201    -001 →1120, B450    -001 →2110, B525    -001 →1151, P001    -001 →4110 and B806    -001 →3200; accountant approval of account coverage is required. B150    -001 must be allocated between actual RM/FG controls before it can cover both target accounts. No automatic name-based 188-to-39 mapping or forced consolidation has been applied. The full TB must cover every active target transaction account, including genuine zero balances. Do not add invented zero rows or subtract stock/AR/AP/cheques manually: the importer computes the residual from posted migration contributions. If multiple legacy codes consolidate to one target account, approve and prepare a complete consolidated source schedule before residual posting; repeated per-row contribution subtraction must be reviewed against current importer behavior.
