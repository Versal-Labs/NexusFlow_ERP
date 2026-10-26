# Save exact destination mappings and defaults

Use step 2, Map and confirm source treatment. Codes below are codes, not database IDs. Choose each destination NexusFlow master by code and name.

For every mapping: choose Mapping type, enter Exact legacy code, choose NexusFlow master, select Add Mapping. After entering the complete set, select Save Mappings and Evidence. Add Mapping alone does not persist it.

## Core and bank mappings

| Mapping type | Exact legacy code | Target master code | Target label |
|---|---|---|---|
| BankBranch | 7010-058 | 7010-001 | Bank of Ceylon / Head Office |
| BankBranch | 7010-501 | 7010-001 | Bank of Ceylon / Head Office |
| BankBranch | 7056-006 | 7056-006 | Commercial Bank of Ceylon / Kurunegala |
| BankBranch | 7056-152 | 7056-001 | Commercial Bank of Ceylon / Head Office |
| BankBranch | 7083-064 | 7083-001 | Hatton National Bank / Head Office |
| BankBranch | 7083-094 | 7083-001 | Hatton National Bank / Head Office |
| BankBranch | 7135-040 | 7135-001 | People's Bank / Head Office |
| BankBranch | 7135-123 | 7135-001 | People's Bank / Head Office |
| BankBranch | 7135-200 | 7135-001 | People's Bank / Head Office |
| BankBranch | 7135-329 | 7135-001 | People's Bank / Head Office |
| BankBranch | 7162-012 | 7162-012 | Nations Trust Bank / Kurunegala |
| BankBranch | 7278-929 | 7278-001 | Sampath Bank / Head Office |
| BankBranch | 7287-085 | 7287-001 | Seylan Bank / Head Office |
| BankBranch | 7311-085 | 7311-001 | Pan Asia Banking Corporation / Head Office |
| BankBranch | 7454-026 | 7454-001 | DFCC Bank / Head Office |
| BankBranch | 7463-008 | 7463-008 | Amana Bank / Kurunegala |
| BankBranch | 7463-021 | 7463-001 | Amana Bank / Head Office |
| Brand | UNBRANDED | UNBRANDED | UNBRANDED |
| Category | ACCESSORIES | Accessories | Accessories |
| Category | MSH | MSH | Men's Shirts |
| CustomerGroup | TRADE | TRADE | Trade |
| PaymentTerm | NET30 | NET 30 DAYS | Net 30 Days |
| PriceLevel | STANDARD TIER | STANDARD TIER | Standard Tier |
| SupplierGroup | DOM | DOM | Domestic Suppliers |
| Unit | PCS | pcs | Piece |
| Warehouse | WH_MAIN | WH_MAIN | Main Garnet |

Fourteen branch targets are the same-bank pilot substitutes used in the successful local test. They are explicitly retained for this replay; physical branch verification is still required for live balances.

## Existing SKU mappings — 33 rows

Use Mapping type Stock. Enter punctuation literally in Exact legacy code. Map to the existing SKU, not a similarly named product. Current source normalizes incoming codes to uppercase but its in-memory SKU lookup is case-sensitive; these mappings avoid duplicate/new-product handling.

| Exact legacy code | Target master code | Target label |
|---|---|---|
| ,JH,GH | ,jh,gh | H.DAIZ WAIST BAND TAG (SIZEWISE) |
| ,LIYUK | ,liyuk | CARE LABLE - H.DAIZ (SIDEWISE) |
| AFE | afe | H.DAIZ STRING - RED |
| AWE | awe | HUGO BOSS WAIST BAND TAG (SIZEWISE) |
| BFDB | bfdb | SCOPIO  CARE LABLE - TROUSER (SIZEWISE) |
| BRH | brh | MAGMA COMPOSITION LABLE (SIZEWISE) |
| ERT | ert | CARE LABLE COMMON (SIZEWISE) |
| FGFG | fgfg | MAGMA COLLAR TAG (SIZEWISE) |
| FSDF | fsdf | SCOPIO MAIN LABLE (SIZEWISE) |
| GDS | gds | CELLO TAPE CLEAR |
| GVC | gvc | POLY BAG |
| HDFG | hdfg | GARNET POLLY TWILL TAPE BLACK - BUNDLE |
| HGTJHGF | hgtjhgf | GARNET POLLY TWILL TAPE - BAG |
| HYT | hyt | H.DAIZ STRING - BLUE |
| JHTRGF | jhtrgf | GARNET POLLY TWILL TAPE (WHITE) |
| JKYT | jkyt | H.DAIZ MAIN LABLE TROUSER |
| JUJT | jujt | ZIPPER - SIZEWISE |
| LKUI | lkui | MAGMA MAIN LABLE - BLACK (SIZEWISE) |
| OKYUIK | okyuik | MAGMA CARE LABLE (SIDEWISE) |
| RFGRG | rfgrg | GARNET POLLY TWILL TAPE MAROON - BUNDLE |
| RTYER | rtyer | GARNET STRING |
| RYTJRT | rytjrt | MAGMA MAIN LABLE SHIRT MAROON (SIZEWISE) |
| SDASF | sdasf | GARNET POLLY TWILL TAPE BLUE - BUNDLE |
| SDF | sdf | GARNET SIDE LABLE - SHIRT |
| SRG | srg | BOSS SIZE LABLE (SIZEWISE) |
| STHFG | sthfg | MAGMA  MAIN LABLE SHIRT - WHITE (SIZEWISE) |
| STHSDFG | sthsdfg | MAGMA HANG TAG  (SIZEWISE) |
| VBCV | vbcv | SCOPIO LEATHER LABLE |
| WEFD | wefd | MAGMA COMPOSITION LABLE (SIZEWISE) |
| WEFWE | wefwe | ELASTIC |
| WER | wer | SIZE STICKER |
| WRE | wre | MAGMA MAIN LABLE - PINK (SIZEWISE) |
| YJT | yjt | BOSS WASIT BAND TAG (SIZEWISE) |

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

| UI field | Target code | Target label | Pilot note |
|---|---|---|---|
| Opening equity account | 3200 | Retained Earnings | Active Equity posting account; do not select a folder |
| Default customer control | 1120 | Accounts Receivable (AR) | Saved successful local default; select the destination master by code/name |
| Default customer revenue | 4110 | Sales Revenue - FG | Saved successful local default; select the destination master by code/name |
| Default supplier control | 2110 | Accounts Payable (AP) | Saved successful local default; select the destination master by code/name |
| Default supplier expense | 5110 | Raw Material Consumption | Temporary pilot expense default; explicit supplier row accounts remain authoritative |
| Undeposited cheque account | 1151 | Cheques In Hand | Saved successful local default; select the destination master by code/name |
| Default customer group | TRADE | Trade | Saved successful local default; select the destination master by code/name |
| Default supplier group | DOM | Domestic Suppliers | Saved successful local default; select the destination master by code/name |
| Default price level | STANDARD TIER | Standard Tier | Saved successful local default; select the destination master by code/name |
| Default customer payment term | NET 30 DAYS | Net 30 Days | 30-day credit. NET30 and NET 30 DAYS are both 30-day masters locally; never choose COD |
| Default supplier payment term | NET 30 DAYS | Net 30 Days | 30-day credit. NET30 and NET 30 DAYS are both 30-day masters locally; never choose COD |
| Approved base unit when source unit is missing | pcs | Piece | Saved successful local default; select the destination master by code/name |
| Approved warehouse when source location is missing | WH_MAIN | Main Garnet | Saved successful local default; select the destination master by code/name |
| Approved supplier terms in days | 30 | 30 calendar days | Original document date plus 30 |
| Summarized stock layer approval |  | UAT fixture: approved summarized layer per SKU in WH_MAIN at 2026-09-30; original FIFO dates not claimed. | Retained pilot fixture approval |
| Legacy finished goods valuation | AverageCost | Average cost (avar x warehouse quantity) | Prepared costs already populated |
| Valuation discrepancy resolution evidence |  | UAT uses approved avar; original costval difference -3173818.42 LKR retained in archive; not production sign-off. | No live financial sign-off implied |

The finalized local project uses NET 30 DAYS — Net 30 Days for both party defaults AND the explicit source NET30 mapping. The separate local NET30 — 30 Days master also represents 30-day credit, but the table above reproduces the final saved selection. On the destination choose the actual equivalent 30-day term and explicitly map source NET30 to it. Do not choose CASH ON DELIVERY (COD). Matching codes alone do not satisfy CustomerGroup, SupplierGroup, PaymentTerm or PriceLevel mapping requirements.

Account codes auto-resolve when active and of the right type. 3200 was verified locally as an Equity posting account; verify the current destination definition before selecting it. 5110 remains a temporary pilot supplier expense, with explicit rent/electricity row accounts retained.

## Column profiles and required data

1. The supplied files already use exact template headers. No legacy-column transformation is needed on upload.
2. If an old Change source column mapping profile exists, correct conflicting entries using Map Column: Required field and Source column must name the same canonical header. Do not map creamt/debcode over the prepared fields.
3. Supplied modules become Provided. Leave not-yet-uploaded modules Awaiting Data until their stage. All twelve modules are supplied in this pilot; no Not Applicable declaration is necessary.
4. Copy the summarized-layer approval and valuation evidence above. Enter Approved supplier terms in days = 30.
5. Select Save Mappings and Evidence, then Refresh Progress to verify persistence.
6. Leave Customer codes, Supplier codes, SKU codes and Bank account codes blank throughout this complete replay. Two literal SKU codes contain commas, which filter boxes interpret as separators.

Do not import a settings JSON from local: numeric master IDs differ between databases. The package provides code-based reference tables only.
