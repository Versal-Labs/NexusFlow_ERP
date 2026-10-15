# Stage 2 — exact mappings and defaults

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
