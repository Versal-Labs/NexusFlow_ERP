# Source to template mapping

This is the final UAT transformation guide. Every supported field is listed in [the complete column mapping](reports/Source_column_mappings.csv). [Exact Migration Centre selections](GUIDE_02_Mappings_and_Defaults.md) contains every required master mapping and default.

Legacy financial amounts and signed outstanding documents are unchanged. The separate fixture changes are listed in [UAT changes](reports/UAT_changes.csv); original source-to-output tracing and subtotal exclusions are in [legacy reference](reports/legacy_reference/README.md). Four missing customer and two missing supplier masters use explicit UAT names and genuine blanks. The synthetic bank/issued-cheque/TB records are test-only additions.

## Accounts

| Source column or rule | Template column | Final UAT transformation |
|---|---|---|
| Current verified active posting accounts | LegacyRecordId | Synthetic fixture IDs are semantic and permanent; other blank IDs use supported code/composite. Never based on Excel row number. |
| Current verified active posting accounts | Code | Mirror all 39 active posting account codes, names and types; ParentCode blank because existing masters are linked. No folders created. |
| Current verified active posting accounts | Name | Mirror all 39 active posting account codes, names and types; ParentCode blank because existing masters are linked. No folders created. |
| Current verified active posting accounts | AccountType | Mirror all 39 active posting account codes, names and types; ParentCode blank because existing masters are linked. No folders created. |
| Current verified active posting accounts | ParentCode | Mirror all 39 active posting account codes, names and types; ParentCode blank because existing masters are linked. No folders created. |
| Current verified active posting accounts | ClosingDate | Explicit UAT 2026-09-30; original dates preserved. |

## CustomerMaster

| Source column or rule | Template column | Final UAT transformation |
|---|---|---|
| Permanent source transaction ID if available; otherwise supported composite | LegacyRecordId | Legacy rows keep permanent ID if present, else blank and supported stable composite; never row-number identity. Synthetic issued/bank rows use permanent semantic UAT IDs. |
| debcode | Code | Trim surrounding whitespace only; preserve identifiers as text. |
| debname | Name | Trim surrounding whitespace only; preserve identifiers as text. |
| debcper | Contact | Trim surrounding whitespace only; preserve identifiers as text. |
| debmail | Email | Trim surrounding whitespace only; preserve identifiers as text. BA366 email is blank by explicit user confirmation. |
| debtel1 | Phone | Trim surrounding whitespace only; preserve identifiers as text. |
| debtel2 | Mobile | Trim surrounding whitespace only; preserve identifiers as text. |
| debadd1 | Address1 | Trim surrounding whitespace only; preserve identifiers as text. |
| debadd2 + debadd3 | Address2 | Join nonblank original address lines with semicolon and space; no truncation or inferred contact number. |
| Not supplied / not applicable | TaxNumber | Blank, preserved as missing. Do not fabricate a value. |
| User confirmation / current configuration | GroupCode | TRADE; actual master code, never database ID. |
| User confirmation / current configuration | PaymentTermCode | NET30; actual master code, never database ID. |
| User confirmation / current configuration | PriceLevelCode | STANDARD TIER; actual master code, never database ID. |
| User confirmation / current configuration | ReceivableAccountCode | 1120; actual master code, never database ID. |
| User confirmation / current configuration | RevenueAccountCode | 4110; actual master code, never database ID. |
| Explicit UAT metadata | ClosingDate | 2026-09-30 test closing. Original snapshot dates remain archived; not certified production balances. |

## SupplierMaster

| Source column or rule | Template column | Final UAT transformation |
|---|---|---|
| Permanent source transaction ID if available; otherwise supported composite | LegacyRecordId | Legacy rows keep permanent ID if present, else blank and supported stable composite; never row-number identity. Synthetic issued/bank rows use permanent semantic UAT IDs. |
| supcode | Code | Trim outer whitespace only. Preserve original codes/internal spaces; A/B/C/G/S explicitly map to DOM. |
| supname | Name | Trim outer whitespace only. Preserve original codes/internal spaces; A/B/C/G/S explicitly map to DOM. |
| supcontact | Contact | Trim outer whitespace only. Preserve original codes/internal spaces; A/B/C/G/S explicitly map to DOM. |
| supemail | Email | Trim outer whitespace only. Preserve original codes/internal spaces; A/B/C/G/S explicitly map to DOM. |
| suptele | Phone | Trim outer whitespace only. Preserve original codes/internal spaces; A/B/C/G/S explicitly map to DOM. |
| Not supplied / not applicable | Mobile | Blank, preserved as missing. Do not fabricate a value. |
| supadd1 | Address1 | Trim outer whitespace only. Preserve original codes/internal spaces; A/B/C/G/S explicitly map to DOM. |
| supadd2 + supadd3 + supadd4 | Address2 | Join nonblank lines with semicolon and space. |
| tinno | TaxNumber | Trim outer whitespace only. Preserve original codes/internal spaces; A/B/C/G/S explicitly map to DOM. |
| supgroup A/B/C/G/S; confirmed UAT default | GroupCode | Canonical DOM for every supplier; save SupplierGroup DOM explicitly. |
| User confirmed 30 days | PaymentTermCode | NET30; explicitly map to 30 Days. |
| Current active posting account | PayableAccountCode | 2110 AP, verified Liability. Legacy account values remain in original archive. |
| Clear supplier purpose / temporary UAT expense | ExpenseAccountCode | RE187 and RS149 use 6210 Rent; EL264 uses 6220 Electricity; all remaining rows use temporary UAT 5110. This is not production expense sign-off. |
| Explicit UAT metadata | ClosingDate | 2026-09-30 test closing. Original snapshot dates remain archived; not certified production balances. |

## CustomerOpenings

| Source column or rule | Template column | Final UAT transformation |
|---|---|---|
| Permanent source transaction ID if available; otherwise supported composite | LegacyRecordId | Legacy rows keep permanent ID if present, else blank and supported stable composite; never row-number identity. Synthetic issued/bank rows use permanent semantic UAT IDs. |
| debcode | Code | Preserve actual value/sign; ISO date or numeric LKR amount. No original-minus-paid calculation. |
| ref | Reference | Preserve actual value/sign; ISO date or numeric LKR amount. No original-minus-paid calculation. |
| creamt sign | Kind | Invoice / ReturnCredit for customers; Bill / Credit for suppliers. Negative amount preserved. |
| tdate | DocumentDate | Preserve actual value/sign; ISO date or numeric LKR amount. No original-minus-paid calculation. |
| tdate + 30 days | DueDate | Confirmed calendar-day terms. Original customer document date retained. |
| creamt | Amount | Preserve actual value/sign; ISO date or numeric LKR amount. No original-minus-paid calculation. |
| Explicit UAT metadata | ClosingDate | 2026-09-30 test closing. Original snapshot dates remain archived; not certified production balances. |

## SupplierOpenings

| Source column or rule | Template column | Final UAT transformation |
|---|---|---|
| Permanent source transaction ID if available; otherwise supported composite | LegacyRecordId | Legacy rows keep permanent ID if present, else blank and supported stable composite; never row-number identity. Synthetic issued/bank rows use permanent semantic UAT IDs. |
| supcode | Code | Preserve actual value/sign; ISO date or numeric LKR amount. No original-minus-paid calculation. |
| dtfref | Reference | Preserve actual value/sign; ISO date or numeric LKR amount. No original-minus-paid calculation. |
| dtfbal sign | Kind | Invoice / ReturnCredit for customers; Bill / Credit for suppliers. Negative amount preserved. |
| dtfdate | DocumentDate | Original date except four post-closing PU131 documents moved to 2026-09-30 for UAT only; exact original dates in UAT_changes.csv. |
| Final UAT document date + 30 calendar days | DueDate | ISO date; four shifted documents due 2026-10-30. Original paydate/crdays remain in source archive. |
| dtfbal | Amount | Preserve actual value/sign; ISO date or numeric LKR amount. No original-minus-paid calculation. |
| Explicit UAT metadata | ClosingDate | 2026-09-30 test closing. Original snapshot dates remain archived; not certified production balances. |

## ReceivedCheques

| Source column or rule | Template column | Final UAT transformation |
|---|---|---|
| Permanent source transaction ID if available; otherwise supported composite | LegacyRecordId | Legacy rows keep permanent ID if present, else blank and supported stable composite; never row-number identity. Synthetic issued/bank rows use permanent semantic UAT IDs. |
| E / Customer Code | Code | Only instrument detail rows recognized by CH receipt reference; retain text references/leading zeros. |
| C / Ref No | Reference | Only instrument detail rows recognized by CH receipt reference; retain text references/leading zeros. |
| D / Inv No | DocumentReferences | Only instrument detail rows recognized by CH receipt reference; retain text references/leading zeros. |
| H / Cheque Number | ChequeNumber | Only instrument detail rows recognized by CH receipt reference; retain text references/leading zeros. |
| A / To be Dep Date, within report group | ChequeDate | Carry forward only until Total for subtotal; user confirmed it is cheque face date. |
| B / Received Date | ReceivedDate | Only instrument detail rows recognized by CH receipt reference; retain text references/leading zeros. |
| G / Bank Code | BankBranchCode | Original bank-branch code text retained; save the 17 explicit BankBranch mappings. Three exact matches, 14 documented substitute UAT branches. |
| I / Amount | Amount | Only instrument detail rows recognized by CH receipt reference; retain text references/leading zeros. |
| User confirmation / current configuration | Status | InSafe |
| User confirmation / current configuration | PartyReflected | true |
| User confirmation / current configuration | BankReflected | false |
| Not supplied / not applicable | BankAccountCode | Blank, preserved as missing. Do not fabricate a value. |
| Not supplied / not applicable | SupplierCode | Blank, preserved as missing. Do not fabricate a value. |
| User confirmation / current configuration | SafeAccountCode | 1151 |
| Explicit UAT metadata | ClosingDate | 2026-09-30 test closing. Original snapshot dates remain archived; not certified production balances. |

## IssuedCheques

| Source column or rule | Template column | Final UAT transformation |
|---|---|---|
| Explicit semantic UAT issued-cheque fixture | LegacyRecordId | Synthetic fixture IDs are semantic and permanent; other blank IDs use supported code/composite. Never based on Excel row number. |
| Explicit semantic UAT issued-cheque fixture | Code | AH143; UAT-ISSUED-001; cheque 000001; date 2026-09-29; document 2026-09-25; 15000.00; bank 1112; both reflection flags true. Already reflected; no second AP/bank movement. |
| Explicit semantic UAT issued-cheque fixture | Reference | AH143; UAT-ISSUED-001; cheque 000001; date 2026-09-29; document 2026-09-25; 15000.00; bank 1112; both reflection flags true. Already reflected; no second AP/bank movement. |
| Explicit semantic UAT issued-cheque fixture | ChequeNumber | AH143; UAT-ISSUED-001; cheque 000001; date 2026-09-29; document 2026-09-25; 15000.00; bank 1112; both reflection flags true. Already reflected; no second AP/bank movement. |
| Explicit semantic UAT issued-cheque fixture | ChequeDate | AH143; UAT-ISSUED-001; cheque 000001; date 2026-09-29; document 2026-09-25; 15000.00; bank 1112; both reflection flags true. Already reflected; no second AP/bank movement. |
| Explicit semantic UAT issued-cheque fixture | DocumentDate | AH143; UAT-ISSUED-001; cheque 000001; date 2026-09-29; document 2026-09-25; 15000.00; bank 1112; both reflection flags true. Already reflected; no second AP/bank movement. |
| Explicit semantic UAT issued-cheque fixture | Amount | AH143; UAT-ISSUED-001; cheque 000001; date 2026-09-29; document 2026-09-25; 15000.00; bank 1112; both reflection flags true. Already reflected; no second AP/bank movement. |
| Explicit semantic UAT issued-cheque fixture | BankAccountCode | AH143; UAT-ISSUED-001; cheque 000001; date 2026-09-29; document 2026-09-25; 15000.00; bank 1112; both reflection flags true. Already reflected; no second AP/bank movement. |
| Explicit semantic UAT issued-cheque fixture | PartyReflected | AH143; UAT-ISSUED-001; cheque 000001; date 2026-09-29; document 2026-09-25; 15000.00; bank 1112; both reflection flags true. Already reflected; no second AP/bank movement. |
| Explicit semantic UAT issued-cheque fixture | BankReflected | AH143; UAT-ISSUED-001; cheque 000001; date 2026-09-29; document 2026-09-25; 15000.00; bank 1112; both reflection flags true. Already reflected; no second AP/bank movement. |
| Explicit semantic UAT issued-cheque fixture | ClosingDate | Explicit UAT 2026-09-30; original dates preserved. |

## Stock

| Source column or rule | Template column | Final UAT transformation |
|---|---|---|
| Permanent source transaction ID if available; otherwise supported composite | LegacyRecordId | Legacy rows keep permanent ID if present, else blank and supported stable composite; never row-number identity. Synthetic issued/bank rows use permanent semantic UAT IDs. |
| itemcode / SKU | Code | Finished goods / raw materials respectively. Preserve source code meanings; new product classifications need mapping. |
| itemname / ProductName | Name | Finished goods / raw materials respectively. Preserve source code meanings; new product classifications need mapping. |
| Existing exact SKU product type / source population | ProductType | Use actual current existing master type; new FG FinishedGood. Approved test factor does not alter original quantity. |
| Existing exact SKU brand / new FG UAT default | BrandCode | Existing Brand.Name; new FG UNBRANDED. |
| Existing exact SKU category / new FG UAT default | CategoryCode | Existing Category.Code; 81 new FG use MSH. Raw legacy type/category retained in archive. |
| Existing exact SKU base unit / new FG UAT default | UnitCode | Existing Unit.Symbol; new FG pcs; save Unit PCS mapping. |
| Confirmed WH_MAIN | WarehouseCode | All loc1/loc3 FG and all RM consolidated into one WH_MAIN dataset. No separate second Stock upload. |
| loc1..loc8 / Quantity | Quantity | Sum FG locations per SKU in one approved WH_MAIN layer; use raw Quantity unchanged. Do not also add totqty. |
| avar / UnitCost | UnitCost | User-approved FG avar; raw supplied cost unchanged. Six decimal places. costval is retained only for reconciliation. |
| Approved summarized UAT stock layer | LayerDate | 2026-09-30 for every row. Not an original FIFO receipt date. |
| lotno / absent | Batch | Finished goods / raw materials respectively. Preserve source code meanings; new product classifications need mapping. |
| Explicit UAT base-unit assumption | ConversionFactor | 1 for all rows. Final quantities and approved source costs retained. |
| Warehouse override or existing category / new FG | InventoryAccountCode | Existing category or warehouse inventory Asset account; new FG 1132. Current exact master accounts verified. |
| selprice / absent | SellingPrice | Finished goods / raw materials respectively. Preserve source code meanings; new product classifications need mapping. |
| size / absent | Size | Finished goods / raw materials respectively. Preserve source code meanings; new product classifications need mapping. |
| color / absent | Color | Finished goods / raw materials respectively. Preserve source code meanings; new product classifications need mapping. |
| Explicit UAT metadata | ClosingDate | 2026-09-30 test closing. Original snapshot dates remain archived; not certified production balances. |

## BankOpening

| Source column or rule | Template column | Final UAT transformation |
|---|---|---|
| Explicit UAT bank book / statement fixture | LegacyRecordId | Synthetic fixture IDs are semantic and permanent; other blank IDs use supported code/composite. Never based on Excel row number. |
| Explicit UAT bank book / statement fixture | Code | Code1112; book100000.00; statement112500.00; test closing2026-09-30. |
| Explicit UAT bank book / statement fixture | Amount | Code1112; book100000.00; statement112500.00; test closing2026-09-30. |
| Explicit UAT bank book / statement fixture | StatementBalance | Code1112; book100000.00; statement112500.00; test closing2026-09-30. |
| Explicit UAT bank book / statement fixture | ClosingDate | Explicit UAT 2026-09-30; original dates preserved. |

## BankRecon

| Source column or rule | Template column | Final UAT transformation |
|---|---|---|
| Explicit UAT pending-deposit fixture | LegacyRecordId | Synthetic fixture IDs are semantic and permanent; other blank IDs use supported code/composite. Never based on Excel row number. |
| Explicit UAT pending-deposit fixture | Code | UAT-DEPOSIT-001; bank1112; 2026-09-28; +2500.00; OutstandingDeposit; BankReflected=true. |
| Explicit UAT pending-deposit fixture | Reference | UAT-DEPOSIT-001; bank1112; 2026-09-28; +2500.00; OutstandingDeposit; BankReflected=true. |
| Explicit UAT pending-deposit fixture | DocumentDate | UAT-DEPOSIT-001; bank1112; 2026-09-28; +2500.00; OutstandingDeposit; BankReflected=true. |
| Explicit UAT pending-deposit fixture | Amount | UAT-DEPOSIT-001; bank1112; 2026-09-28; +2500.00; OutstandingDeposit; BankReflected=true. |
| Explicit UAT pending-deposit fixture | Kind | UAT-DEPOSIT-001; bank1112; 2026-09-28; +2500.00; OutstandingDeposit; BankReflected=true. |
| Explicit UAT pending-deposit fixture | BankReflected | UAT-DEPOSIT-001; bank1112; 2026-09-28; +2500.00; OutstandingDeposit; BankReflected=true. |
| Explicit UAT pending-deposit fixture | ClosingDate | Explicit UAT 2026-09-30; original dates preserved. |

## ControlTotals

| Source column or rule | Template column | Final UAT transformation |
|---|---|---|
| Per-scope totals calculated from final UAT CSVs | LegacyRecordId | Synthetic fixture IDs are semantic and permanent; other blank IDs use supported code/composite. Never based on Excel row number. |
| Per-scope totals calculated from final UAT CSVs | Kind | 5212 components for 2050 scopes. AR/AP debt/credit positive magnitudes and full-document count for both; separate cheque counts; per-SKU quantity/value; bank bridge count2. Zero components cover missing schedules without fabricated documents. |
| Per-scope totals calculated from final UAT CSVs | Code | 5212 components for 2050 scopes. AR/AP debt/credit positive magnitudes and full-document count for both; separate cheque counts; per-SKU quantity/value; bank bridge count2. Zero components cover missing schedules without fabricated documents. |
| Per-scope totals calculated from final UAT CSVs | Component | 5212 components for 2050 scopes. AR/AP debt/credit positive magnitudes and full-document count for both; separate cheque counts; per-SKU quantity/value; bank bridge count2. Zero components cover missing schedules without fabricated documents. |
| Per-scope totals calculated from final UAT CSVs | Amount | 5212 components for 2050 scopes. AR/AP debt/credit positive magnitudes and full-document count for both; separate cheque counts; per-SKU quantity/value; bank bridge count2. Zero components cover missing schedules without fabricated documents. |
| Per-scope totals calculated from final UAT CSVs | RowCount | 5212 components for 2050 scopes. AR/AP debt/credit positive magnitudes and full-document count for both; separate cheque counts; per-SKU quantity/value; bank bridge count2. Zero components cover missing schedules without fabricated documents. |
| Per-scope totals calculated from final UAT CSVs | ClosingDate | Explicit UAT 2026-09-30; original dates preserved. |

## TrialBalance

| Source column or rule | Template column | Final UAT transformation |
|---|---|---|
| Expected opening contributions plus balanced UAT rent / sales pair | LegacyRecordId | Synthetic fixture IDs are semantic and permanent; other blank IDs use supported code/composite. Never based on Excel row number. |
| Expected opening contributions plus balanced UAT rent / sales pair | Code | All 39 current account codes. Opening module expected journal values plus 6210 Debit1000000 / 4110 Credit1000000; remaining accounts explicit test zeros. Debit=Credit282960858.88. Full original TB preserved. |
| Expected opening contributions plus balanced UAT rent / sales pair | Debit | All 39 current account codes. Opening module expected journal values plus 6210 Debit1000000 / 4110 Credit1000000; remaining accounts explicit test zeros. Debit=Credit282960858.88. Full original TB preserved. |
| Expected opening contributions plus balanced UAT rent / sales pair | Credit | All 39 current account codes. Opening module expected journal values plus 6210 Debit1000000 / 4110 Credit1000000; remaining accounts explicit test zeros. Debit=Credit282960858.88. Full original TB preserved. |
| Expected opening contributions plus balanced UAT rent / sales pair | ClosingDate | Explicit UAT 2026-09-30; original dates preserved. |

