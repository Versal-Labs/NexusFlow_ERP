# Customer outstanding migration assessment

Implementation follow-up (5 October 2026): see [Customer Opening Migration Runbook](Customer_Opening_Migration_Runbook.md) and [verification](../uat/Customer_Migration_Verification.md). The findings below describe the earlier assessment; the implementation adds the reviewed workflow. The hosted site now reaches Sign In, but authenticated hosted UAT remains pending.

Reviewed 29 September 2026. This is an assessment and proposed migration design, not an executed import.

## Evidence and limitations

- Source: `cusout.xls`, worksheet `cusout`, range A1:AM1151. Original file was read without modification.
- Reviewed the local import handler, CSV preview, customer model, receipt allocation, customer statement, AR ageing, sales register and trial balance import code.
- Browser verification of `https://nexus.garnetnation.lk/` failed with `net::ERR_NAME_NOT_RESOLVED`. Deployed behavior and role assignments remain unverified.
- No application source or live records were changed. Existing UAT/manual files contain unrelated local changes and were left intact.
- The legacy XLS reader reported container/encoding warnings but read all 1,150 data rows and 39 columns. Totals reconcile as described below. Check representative customer names against the original system before final import.

## What the file contains

| Measure | Result |
| --- | ---: |
| Data rows | 1,150 |
| Distinct customer codes | 331 |
| Distinct customer names | 331 |
| Positive `creamt` rows | 1,130 |
| Positive `creamt` total | 162,436,582.24 |
| Negative `creamt` rows | 20, across 12 customers |
| Negative `creamt` total | -321,597.10 |
| Net `creamt` total | 162,114,985.14 |
| Sum of `total` | 162,114,985.14 |
| Document date range | 2019-01-02 to 2026-09-28 |

Currency is LKR, confirmed by the user. Cutover is 1 October, interpreted as 2026-10-01 in the current migration context.

The user confirmed `debamt` means debit amount and `creamt` means credit amount, and then confirmed that the amount still owed for GT00011762 is 18,751.50, the `creamt` value. Therefore use signed `creamt` directly as the opening amount for this export; do not calculate `debamt - creamt`. Numerically, `creamt` agrees with the sum of ageing columns on every row and with `total` aggregated for every customer. Negative values are confirmed goods-return credits. `total` is a customer summary value embedded in the detail and must not be imported as another transaction. The source totals still need refreshing/reconciliation to the cutover date.

Confirmed example: row 2, customer AD694, document GT00011762, dated 2025-05-27, has debit amount 128,880.00 and credit amount 18,751.50. The user confirmed the amount still owed is 18,751.50. Import that amount as the opening receivable, subject to changes between this export and cutover. Retain the debit amount as source reference information without fabricating a payment for the difference.

| Legacy type | Rows | Net `creamt` | Interpretation |
| --- | ---: | ---: | --- |
| `xtyp=1`, GT/GN/VT references | 883 | 120,639,246.21 | Appears to be invoice balances; includes all 20 negative amounts. Needs business confirmation. |
| `xtyp=2`, DE references | 75 | 19,870,674.91 | Appears to be debit entries/adjustments; descriptions vary. Needs business confirmation. |
| `xtyp=8`, RT references | 192 | 21,605,064.02 | Appears to be returned-cheque debt, supported by cheque references and descriptions. Needs business confirmation. |

There are no duplicate document references or missing customer codes, names, references or document dates in this file. Each code maps to one name and each name to one code within the export; this does not establish a match to customers already in NexusFlow.

All 958 type 1/type 2 rows imply 2026-09-28 when adding `days` to `tdate`. Returned-cheque rows use a different or inconsistent ageing basis. All `crdays` values are zero. The user has now specified 30-day due dates: use document date plus 30 days for the migration, rather than deriving terms from `crdays` or resetting all due dates to 30 days after cutover. This is a migration rule, not proof of historical payment terms. The report date remains inferred. The values in `chqamt`, `advamt` and `pdamt` are all zero; this does not prove no separate advances or pending cheques exist outside this export.

## Confirmed business rules and requested customer matching

- Cutover: 1 October (2026 assumed from context). Refresh/reconcile the source through 30 September before loading opening balances on 1 October.
- Currency: LKR.
- Negative amounts represent goods returned by customers. Preserve these as opening customer return credits. Do not replay a historical stock return or tax adjustment already reflected in the opening accounts and stock.
- Due dates: document date plus 30 days.
- Cheque balances are separate from invoice balances. Preserve both as separately identifiable opening items.
- Customer table must retain a legacy customer code. Customer import must accept the old code, and outstanding import must match `debcode` against it instead of relying on names. This is a recorded implementation requirement; it has not yet been implemented.
- Proposed validation: preserve codes as text, reject duplicate nonblank codes, report unmatched codes before posting, and prevent an import from silently creating an unrelated customer.

## Customers with negative entries

Amounts are LKR. Negative-entry amounts sum only negative `creamt` rows, classified by the user as customer goods returns. Customer report totals sum the source `total` column for that customer and independently equal the sum of all signed `creamt` rows. A customer with a return can still have a positive report total.

| Legacy code | Customer | Negative entries | Return entries total | Customer report total |
| --- | --- | ---: | ---: | ---: |
| BE885 | BELLA BOUTIQUE- KALUTARA | 5 | -38,700.00 | 675,550.00 |
| CC375 | CEYLON CITY  MART HIDAYAH - GALLE | 1 | -500.00 | -500.00 |
| DC969 | DEEP CORNER - EHALIYAGODA | 1 | -29,880.00 | 0.00 |
| FA111 | FASHIONN CONNECTION - KILINOCHI | 1 | -34.00 | 0.00 |
| FC605 | FASHION CITY - KULIYAPITIYA | 1 | -1,617.00 | 65,203.00 |
| JY308 | JEYLING-CHUNNAHAM | 1 | -0.60 | 0.00 |
| LA869 | LADY J - BORELLA | 1 | -2,190.00 | 2,741,120.00 |
| ME795 | NEST LINE - BADULLA | 1 | -21,935.00 | -21,935.00 |
| SA280 | SANKA DIVULAPITIYA | 4 | -89,025.00 | 0.00 |
| SL442 | SLIM FIT - DANGKOTUWA | 1 | -21,585.50 | -21,585.50 |
| VI852 | VIDUMINI FASHION - KATTANA | 2 | -65,100.00 | 0.00 |
| WT608 | WELCOME TEX - VAVUNIYA | 1 | -51,030.00 | -51,030.00 |

Total: 20 negative entries across 12 customers, LKR -321,597.10. Four customers have negative customer report totals: CC375, ME795, SL442 and WT608; together LKR -95,050.50. Other customers' negative entries are offset by positive entries. These totals describe the supplied report and must be refreshed/reconciled for cutover.

## Recommended migration treatment

Use opening receivable items, retaining individual document references, rather than one total per customer. A customer-total import is a fallback only if the business accepts losing document allocation and historical ageing detail.

1. Use the agreed 1 October cutover and reconcile this export with the old receivables ledger and trial balance. Refresh the report through 30 September or reconcile intervening invoices, receipts, credits and cheque returns.
2. Import and retain each old customer code in the customer table, then match each `debcode` to that customer. Resolve missing or ambiguous customers before import. Verify receivable account and collection owner.
3. Classify invoices, debit adjustments, returned-cheque debt and negative balances. Preserve original reference, type, date, amount, remaining balance, customer code, representative, remarks and cheque details where present.
4. Import outstanding amounts as opening entries. Keep original document/due dates for collections and a separate cutover posting date for the general ledger. Retain original amounts as historical metadata rather than replaying historical sales or payments.
5. Preserve the confirmed goods-return credits as opening credit items that can later be allocated. Do not discard negative amounts, convert them to positive debt, or replay the original goods-return inventory/tax workflow.
6. Preserve cheque debt separately from invoice balances, as confirmed by the user, without creating a fresh bank deposit. Retain cheque references and apply the agreed due-date rule.
7. Post opening receivables against the agreed migration clearing/opening balance account. Do not book revenue, VAT, stock movement or cash again. Coordinate the remaining opening trial balance so AR is not loaded twice and the migration clearing balance reconciles.
8. Pilot representative cases in UAT, reconcile the full batch, then sign off and import once. Preserve a source checksum, batch reference, mapping and row-level results. Retrying the same batch must not duplicate balances.

For illustration, if all signed amounts are confirmed as AR, the positive opening items debit AR 162,436,582.24 and credit migration clearing by the same amount. The opening credits debit migration clearing 321,597.10 and credit AR by the same amount. Net AR is 162,114,985.14. The accountant must confirm classifications, control accounts and the offset within the complete opening trial balance; the offset is not new income.

After cutover, staff record actual collections and allocate them to the opening documents. For example, an actual 10,000.00 receipt against the 18,751.50 example leaves 8,751.50 due.

This approach is consistent with [ERPNext's official opening-invoice migration guidance](https://docs.frappe.io/erpnext/opening-invoice-creation-tool), which recommends preserving open documents, loading the outstanding portion, separating credits and avoiding duplicate AR in opening journals.

## Existing NexusFlow workflow and gaps

Code defines the **AR & AP Migration Wizard** at `/Finance/ImportOpenInvoices`, guarded by `Permissions.Finance.ManageAccounts`. Actual assigned role names require deployed verification.

The existing CSV fields are `Type,PartyName,DocumentNo,Date,OutstandingAmount`. The screen offers **Parse & Preview Data**, followed by **Commit to General Ledger**. It accepts CSV, not this XLS directly. These labels/routes are source-verified only.

| Source | Existing field | Qualification |
| --- | --- | --- |
| Customer receivable | `Type=AR` | This is not the legacy `xtyp` classification. |
| `debnam` | `PartyName` | Current importer matches names; a reviewed `debcode` to customer-ID mapping is preferable. |
| `ref` | `DocumentNo` | Preserve text exactly; check against existing NexusFlow numbers. |
| `tdate` | `Date` | Convert Excel serial to an ISO date. Current import has no separate cutover date. |
| `creamt` | `OutstandingAmount` | Confirmed by the user as the amount still owed in this export; negative goods-return credits need additional support. |

The existing importer is a starting point, but is not suitable for this complete file unchanged:

- The browser validation rejects amounts at or below zero; the handler filters them out. Removing the 20 negatives would increase the reported net balance by 321,597.10.
- It represents every AR entry as a posted sales invoice with no items, zero tax and the outstanding amount as `GrandTotal`. It does not preserve the legacy document classification or original amount separately.
- It sets due dates to document date plus 30 days and dates the journal with the current UTC time. It needs an explicit cutover date and verified ageing inputs.
- It matches customers by uppercase name and automatically creates a minimal customer when no match is found. Those new customers lack reviewed financial/master-data mappings. Receipts use the customer's receivable account, whereas this importer uses the global AR mapping; these must agree or be reconciled by account.
- It lacks a migration batch key and a row-level duplicate preview. The database's unique invoice-number index is a last-line constraint, not an import retry/reporting workflow.
- `SalesInvoice` has no dedicated opening-entry flag. The Sales Register includes all posted invoices in the selected date range, so imported amounts can appear as sales there despite the journal posting to opening balance equity.
- AR Aging currently reads invoice balances only and does not net separate credit/payment records or customer debit memos. Customer Statement also reads those other types. Credit/debit migration must therefore address report and allocation consistency, not just insert records into existing tables.
- The **Download CSV Template** link points to `/templates/AR_AP_Migration_Template.csv`, but that file is absent from the local checkout. Hosted availability is unverified.
- Trial Balance import rejects accounts whose names contain Receivable, Payable or Inventory and requires the submitted file to balance. A deliberately balanced opening trial balance using the migration offset is needed; simply deleting AR from a balanced export is insufficient.

Recommendation: extend the existing migration workflow to handle signed opening items and legacy types, with legacy-code matching to customer IDs, cutover date, due-date handling, batch validation and consistent reports/allocations. These are requirements for development, not existing verified features. No application implementation has been performed in this assessment.

## UAT checks for the eventual implementation

Preconditions: a disposable UAT dataset/backup, approved cutover and currency, mapped customer masters/control accounts, approved document classifications, and the proposed changes deployed. Finance/admin performs migration; collection staff validate receipt allocation and statements using their assigned permissions.

| Test | Test data and steps | Expected result | Pass/fail |
| --- | --- | --- | --- |
| Part-paid legacy invoice | Import row 2 as an opening item, inspect balance and GL. | Due 18,751.50; original 128,880.00 retained as reference; no new sales/tax/stock/cash posting. | Not run |
| Negative amount | Review and import row 97 (-10,740.00) using approved classification. | Customer balance reduced correctly; credit remains traceable/allocatable; no fictitious cash movement. | Not run |
| Returned cheque | Import row 13, RT00000116, remaining balance 5,000.00, after linkage confirmation. | Debt appears once, cheque reference retained, no new cheque deposit. | Not run |
| Debit adjustment | Classify and import row 4, DE00001594, 350,000.00. | Correct opening debt, clear document type, consistent statement/ageing/collection behavior. | Not run |
| Collection | Record a real test cash/bank receipt of 10,000.00 against row 2's opening item. | Remaining due 8,751.50; customer account and GL agree. | Not run |
| Reconciliation | Load the approved complete file and compare signed totals by all 331 customer codes and overall. | Net 162,114,985.14 if unchanged and fully approved; positive and credit totals separately agree; no double-counted AR. | Not run |
| Date and ageing | Compare invoice, debit and returned-cheque samples to the agreed due-date rule and opening posting date. | Due date is document date plus 30 days; GL opening date is 1 October; original document dates retained. | Not run |
| Retry | Preview/retry the same completed batch. | No duplicated documents or journals; explicit row/batch outcome. | Not run |
| Reporting | Review `/Reporting/CustomerStatement`, `/Reporting/ArAging`, `/Reporting/SalesRegister` and `/Finance/TrialBalance`. | Opening items and credits reconcile; operational sales can distinguish migration items; no duplicate revenue or AR. | Not run |

## Needs business confirmation

1. Report date (inferred 2026-09-28), final refreshed export through 30 September, and meaning of `xtyp=2`/DE entries.
2. Any AR/opening journals already loaded into NexusFlow, and availability of legacy codes for existing customers.
3. Whether separate unapplied receipts, advances, pending/post-dated cheques or additional credit exports must also be migrated.
4. Which old balances remain collectible; any write-offs require a separate approved accounting decision rather than automatic removal because of age.

## Local code evidence

- `NexusFlow.AppCore/Features/Finance/Commands/ImportOpenInvoicesHandler.cs`
- `NexusFlow.AppCore/Features/Finance/Commands/ImportTrialBalanceHandler.cs`
- `NexusFlow.Web/Controllers/FinanceController.cs`
- `NexusFlow.Web/Controllers/Api/FinanceController.cs`
- `NexusFlow.Web/Views/Finance/ImportOpenInvoices.cshtml`
- `NexusFlow.Web/wwwroot/js/finance/import-arap.js`
- `NexusFlow.Domain/Entities/Sales/Customer.cs`
- `NexusFlow.Domain/Entities/Sales/SalesInvoice.cs`
- `NexusFlow.Infrastructure/Persistence/Configurations/SalesConfiguration.cs`
- `NexusFlow.AppCore/Features/Sales/Queries/GetUnpaidInvoicesHandler.cs`
- `NexusFlow.AppCore/Features/Treasury/Commands/RecordPaymentHandler.cs`
- `NexusFlow.AppCore/Features/Reporting/Queries/GetCustomerStatementQuery.cs`
- `NexusFlow.AppCore/Features/Reporting/Queries/GetArAgingQuery.cs`
- `NexusFlow.AppCore/Features/Reporting/Queries/GetSalesRegisterQuery.cs`
