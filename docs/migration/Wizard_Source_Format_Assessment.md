# Migration wizard: supplied source formats

Reviewed 6 October 2026. These six files are **structure references**, as instructed by the business. They are not approved final closing snapshots. Source workbooks were read without modification; no application source, business records or balances were changed.

## Decisions retained for implementation

- One Migration Centre with complete-package, module and selected-customer/supplier imports.
- Opening at the actual go-live date; closing data from the preceding day. The precise operational freeze remains a project input.
- Missing datasets remain **Awaiting data**. A missing trial balance prevents final GL reconciliation and project completion, while independent valid datasets can be prepared and eligible scopes can be imported/released under the planned controls.
- Pre-cutover history remains in a read-only legacy archive.
- One authorized administrator can prepare, approve and post.
- Affected parties/items remain locked until their required opening components are complete.
- **Confirmed after this file review:** genuine missing customer/supplier contact details may remain blank, with a visible **Details incomplete** flag and a completion list. Do not insert fictitious emails, contacts, telephone numbers, addresses or tax identifiers. Party name, unique legacy code and required accounting/master mappings remain mandatory. The current customer CSV importer uses stricter normal-customer validation; the new wizard requires a dedicated migration validation path rather than pretending these files already pass it.

## File inventory

| Source file and worksheet | Structure | Detail records | Key observations |
|---|---|---:|---|
| `System uploads/New Data/customermasterfile.xls`, `customermasterfile` | Binary XLS; row 1 header; 15 columns | 892 | 892 unique, nonblank customer codes; contact and address fields frequently blank |
| `System uploads/New Data/supliermasterfile.xls`, `supliermasterfile` | Binary XLS; row 1 header; 25 columns | 213 | 213 unique, nonblank supplier codes; all `supbal` values zero; supplier opening invoice schedule still missing |
| `Stock/Raw Material Stcok.xlsx`, `Sheet1` | Flat XLSX; row 1 header; 10 columns | 444 | 444 unique SKUs; all rows specify Pcs, WH-MAIN and 13 September 2026 |
| `Stock/stock.xls`, `stock` | Binary XLS; row 1 header; 34 columns | 494 | 494 unique item codes; stock quantities spread across `loc1` and `loc3`; valuation fields need explanation |
| `imports new/cheque in hand.xlsx`, `Sheet` | Formatted/grouped XLSX report; titles, group dates and totals | 49 cheque rows | 17 customers; cheque value LKR 5,328,628.00; report dated 28 September 2026 |
| `imports new/cusout.xls`, `cusout` | Binary XLS; row 1 header; 39 columns | 1,150 outstanding rows | 331 customers; net `creamt` LKR 162,114,985.14; source ageing identifies 28 September 2026 |

The four XLS files are actual binary Excel workbooks, not CSV files with an XLS extension. The two XLSX files contain no formula cells in the inspected worksheets. Binary XLS values were read without evaluating formulas or macros; absence of binary formula origins is not asserted.

## Adapter profiles

The wizard should detect these known formats by header signature and report structure, not filename alone. Both original workbooks and user-transformed canonical CSV/XLSX files are supported paths. Save the original source, detected profile/version, operator mappings and normalized preview separately. Transformations must not modify the originals.

### 1. Legacy customer master

Original header:

```csv
actual,debcode,debname,debadd1,debadd2,debadd3,debcper,debtel1,debtel2,debfax,debmail,hedref,heddate,srepcode,areacode
```

| Source field | Canonical destination / handling |
|---|---|
| `debcode` | LegacyCustomerCode; trim surrounding spaces, preserve original source text and leading zeros |
| `debname` | Name; preserve the real name |
| `debadd1`, `debadd2` | AddressLine1, AddressLine2 |
| `debadd3` | Preserve as third legacy address line; do not infer province/district/city automatically |
| `debcper` | ContactPerson; genuine blank allowed with completion flag |
| `debtel1`, `debtel2`, `debfax` | Preserve separate phone/alternate/fax fields. Default primary phone to first nonblank telephone, showing that rule in preview |
| `debmail` | Email; blank allowed; nonblank invalid values remain visible for correction rather than being replaced |
| `srepcode` | Legacy sales-representative code requiring an approved mapping; not a NexusFlow numeric ID |
| `areacode` | Legacy area code requiring a lookup dictionary; not automatically a city/district |
| `actual` | Source flag (`Y` for all sample rows); meaning **Needs business confirmation** before active-status mapping |
| `hedref`, `heddate` | Retain for audit; meaning **Needs business confirmation**. All references are blank and dates are placeholder text in this sample |

The sample has 883 blank emails, 877 blank contact-person fields and 660 blank first address lines. No tax, payment-term, price-level, credit-limit or AR/revenue-account fields are supplied. These require real additional data or explicitly approved project mappings/defaults; they cannot be recovered by renaming columns.

### 2. Legacy supplier master

Original header:

```csv
supacc,supcode,supname,supadd1,supadd2,supadd3,supadd4,supemail,suptele,supfax,suptype,supgroup,supflg,supbal,debcode,supcontact,areacode,actual,vattype,vatno,paycode,grnmode,spaddusr,spaddtme,tinno
```

| Source field | Canonical destination / handling |
|---|---|
| `supcode`, `supname` | LegacySupplierCode, Name |
| `supadd1`–`supadd4` | Preserve separate address lines; geographic classification requires confirmed mapping |
| `supemail`, `suptele`, `supfax`, `supcontact` | Email, Phone, legacy fax, ContactPerson; genuine blanks allowed with completion flags |
| `supgroup` | Supplier group code; sample values A, B, C, G and S need a dictionary and approved NexusFlow mapping |
| `supacc` | Legacy account code requiring explicit chart-of-accounts mapping; preserve internal formatting |
| `vatno`, `tinno`, `vattype` | Separate legacy VAT number, TIN and VAT classification; do not infer meanings or collapse identifiers without confirmation |
| `paycode` | Payment-term code; blank throughout this sample; approved supplier terms still required |
| `actual`, `supflg`, `suptype`, `grnmode` | Retain source flags; meanings **Need business confirmation** |
| `debcode` | Possible customer cross-reference; retain without automatically merging customer and supplier ledgers |
| `supbal` | Audit reference only; not a document-level opening invoice import |
| `spaddusr`, `spaddtme` | Legacy creation/audit metadata; not evidence of the export closing date |

All 213 `supbal` values are zero. This is not proof that supplier AP is zero or that an AP dataset is unnecessary. Request the invoice-level AP schedule or an independently approved zero declaration. There are 208 blank emails, 202 blank contacts and 170 blank first address lines. Both tax-number fields are blank throughout.

### 3. Raw-material stock

Original header:

```csv
ProductGroup,ProductName,SKU,Category,Brand,Unit,Quantity,UnitCost,WarehouseCode,OpeningDate
```

This is close to the intended canonical item/stock format. Use ProductName/SKU for the master, Category/Brand/Unit for mapped lookups, WarehouseCode for the stock scope and Quantity/UnitCost for the opening valuation preview. Preserve ProductGroup even though it is blank in every sample row.

Normalize `Pcs` through an approved `PCS` unit mapping and parse `13/9/2026` as 13 September 2026. Category is `ACCOSORIES NEW`, brand is `UNBRANDED`, and warehouse is `WH-MAIN` throughout the sample; preview and approve mappings rather than creating or renaming shared masters silently.

Reference quantities total 6,306,283 PCS, with quantity × unit cost value LKR 29,727,743.70. Costs have at most two decimal places and quantities are positive whole numbers. There are 86 rows where quantity or cost equals 1; show a confirmation warning, without declaring those figures wrong.

The existing raw-material importer reads only ProductName, SKU, Quantity and UnitCost and applies one setup selection to all rows. The new wizard must also consume the supplied unit, warehouse, category, brand and date fields. This source needs no forced loss of those columns. Its date is a reference date, not the approved go-live opening date; request a fresh closing export rather than silently relabelling balances.

### 4. Legacy stock report

Original header:

```csv
type,type1,scatcode,avar,itemcode,totqty,loc1,loc2,loc3,loc4,loc5,loc6,loc7,loc8,packing,costpic,costl1,costl2,costl3,costl4,costl5,costl6,costl7,totcost,costval,msize,lotno,mtype,itemname,size,brand,color,selprice,minsellpri
```

- Map `itemcode` to SKU/LegacyItemCode, `itemname` to description, brand/colour/size to their corresponding master attributes, and selling-price fields to price fields.
- Map `type`, `type1` and `scatcode` through supplied category dictionaries. They are not automatically the NexusFlow ProductType enum. Item classification needs an approved source profile or per-category mapping.
- Preserve `lotno` and other native grouping fields. Confirm whether lotno is a product grouping/style reference or an actual inventory batch before creating stock layers from it.
- Unpivot `loc1`–`loc8` into warehouse stock rows using an explicit legacy-location mapping. Do not map every row to Main Warehouse.
- In this sample, loc1 totals 68,206 and loc3 totals 780; all other location totals are zero. Every row's location quantities reconcile to `totqty`; total quantity is 68,986. Loc3 has stock in 156 SKU rows.
- No explicit stock UOM or source closing date is supplied. Both need approved metadata; the wizard must not silently infer them.
- `avar` is a candidate unit-cost field; its precise valuation meaning needs confirmation. `costval`, `totcost`, `costpic` and `costl*` must retain their original meanings until the developer supplies a dictionary.

Valuation checks show:

| Check | Reference value |
|---|---:|
| Sum of `totqty × avar` | LKR 84,689,502.04 |
| Sum of `costval` | LKR 87,863,320.46 |
| Difference | LKR 3,173,818.42 |
| Rows differing by more than LKR 0.01 | 202 |
| Negative `costval` rows | 3 |
| Nonzero `totcost` rows | 0 |

These differences require an explanation or corrected valuation data. They may reflect different field meanings or accounting conditions; do not label them as data errors without confirmation. The adapter can stage the records and display both valuations, but opening-value approval remains blocked until the authoritative basis is established and reconciled to the source accounts. Do not select selling price as inventory cost or simply replace the source value with quantity × cost.

The 494 stock SKUs and 444 raw-material SKUs have no normalized code overlap in these samples. Continue checking overlap on every later upload.

### 5. Customer outstanding

Use the existing legacy-report adapter for customer code, reference, document date, type, ageing and signed remaining amount. Preserve supplementary fields separately.

- `debcode` matches customer master `debcode`; outstanding `debnam` corresponds to master `debname`.
- `creamt` is the confirmed remaining amount. `debamt` is retained for audit, not deducted from creamt.
- Types 1, 2 and 8 represent the established invoice, debit-adjustment and returned-cheque debt mappings. Negative amounts retain the confirmed goods-return-credit treatment.
- The sample contains 20 negative items totalling LKR -321,597.10, and net AR of LKR 162,114,985.14.
- The composite `(debcode, xtyp, ref, tdate)` is nonblank and unique in this sample. It can be the documented fallback identity when no stable legacy entry ID is supplied; test uniqueness on every file and reject collisions. Do not use row number alone as the cross-upload business identity.
- `chqamt`, `advamt`, `pdamt` and `supamt` are zero throughout this sample; this does not establish that their separate schedules are globally zero or complete.

Four outstanding customer codes do not occur in the supplied customer master: **GA735, TK1039, UP1038 and VI1041**. Stage those rows as missing master mappings and keep their scopes unresolved. The operator may upload authentic master rows later or map to an explicitly verified existing customer. No placeholder party or name-based merge is permitted.

### 6. Cheques in hand

The source has report titles, a closing date, group-level cheque dates, receipt references, raw invoice references, customer codes/names, bank/branch codes, cheque numbers, amounts and subtotal/grand-total rows.

Use the existing grouped-report parsing rules: inherit the verified cheque date within each group, retain the separate received date, preserve CH receipt references and cheque numbers as text, and exclude titles/totals from detail records. Reconcile both group subtotals and the grand total. The source **To be Dep Date** remains the previously confirmed actual cheque date.

All cheque customer codes occur in the supplied customer master. This report describes the previously agreed InSafe received-cheque opening, already deducted from customer AR. It does not supply issued-cheque, deposited, endorsed or supplier-payment schedules. Do not use its aggregate to fabricate historical invoice allocations or reduce customer debt again.

## Missing datasets and meanings

| Missing input / confirmation | Wizard handling |
|---|---|
| Full source trial balance and chart/account dictionary | Awaiting data; final GL reconciliation/completion unavailable |
| Supplier document-level AP, credits and advances | Awaiting data; supplier financial scopes not released merely because supbal is zero |
| Customer advances and other cheque states/issued cheques | Awaiting data or explicit independently approved not-applicable declaration |
| Bank/cash opening and reconciliation schedules | Awaiting data; affected treasury scopes remain incomplete |
| Location dictionary for loc1–loc8 and stock UOM/classification | Stage stock, require mapping before approval |
| Stock valuation-field definitions and discrepancy explanation | Show source and calculated values; block opening-value approval |
| Party groups, areas, sales reps, payment terms and account mappings | Stage masters; require actual dictionaries or explicit approved mappings/defaults |
| Immutable source IDs and actual closing/export metadata | Retain supplied keys; validate any documented composite fallback; require actual closing evidence |
| Common fresh closing snapshots for actual go-live | Required before financial acceptance; present files are structural references |

## Changes to the wizard plan

1. Add six source profiles: Legacy Customer Master, Legacy Supplier Master, Raw Material Stock, Legacy Stock Locations, Legacy Customer Outstanding and Legacy Cheques In Hand. Keep canonical CSV/XLSX as the common target.
2. Show a transformation preview with source-to-target fields, selected defaults, inherited report values, unpivoted locations, warnings and untouched original values.
3. Preserve sparse party data with the approved completion flag; enforce identity and financial mappings independently of contact-field completeness.
4. Provide pending-mapping queues for missing parties, bank branches, groups, areas, locations and valuation decisions. Later files resolve only their affected scopes, invalidate relevant approvals and undergo the same duplicate checks.
5. Separate source-date validation from structural parsing. A valid adapter profile is not approval to post a stale or inconsistent opening snapshot.
6. Reconcile full-file, selected-scope, already-imported and pending totals separately. The operator must see what was included and what still awaits data.
7. Keep trial balance and other missing modules explicitly pending. Prepare/import eligible independent scopes under the planned trading locks, then complete final GL only when the project meets all reconciliation controls.

No transformed financial upload was produced or posted during this assessment. It supplies adapter requirements for the agreed wizard implementation.

## Implementation follow-up — 6 October 2026

The assessment remains evidence about the supplied reference structures, not authorization to load those balances. The current central wizard includes structural adapters for the six reference files, exact-code/column mappings and canonical twelve-dataset templates. Missing suppliers/AP, bank positions, issued cheques and complete TB must still be supplied; sparse contacts are allowed and flagged. Stale snapshots, missing customers, negative stock costs and FG valuation differences remain errors/confirmation gates. See [Migration Centre operating guide](Migration_Centre_Runbook.md) for the implemented boundary.
