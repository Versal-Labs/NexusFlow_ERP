# Data request to the legacy-system developer

Prepared 6 October 2026 for NexusFlow ERP. This document specifies the exports to request; it does not authorize posting or claim that every export already has a NexusFlow importer.

## Agreed migration boundary

Opening date: **the actual go-live date**, as selected for the central migration wizard. Export the closing position at **23:59:59 Asia/Colombo on the preceding day**. The precise freeze/go-live date remains to be scheduled. All agreed amounts are **LKR**. Export posted transactions through that boundary, excluding cancelled/unposted documents from balances; retain their statuses in the audit archive. This supersedes the earlier proposed 1 October opening.

The existing customer/cheque samples represent 28 September and the raw-material reference specifies 13 September. These are structure references, not approved final closing balances. Produce genuine as-of exports with ageing and subtotals recalculated by the old system. Do not relabel sample balances with a new date.

Confirmed by the business: **all transactions from 1 October onward are in the old system only**, and **pre-cutover history will remain in a read-only legacy archive**. Their financial effects must be included in the fresh closing position at the actual freeze. Archive those transactions and any backdated corrections; the approved first wizard release does not replay October history as normal operational documents in NexusFlow.

Agreed first-release scope: migrate masters and approved opening balances; collect operationally open documents as supporting schedules for future dedicated imports; retain pre-cutover transaction history in a searchable, read-only archive. Importing full history into NexusFlow is outside that agreed scope. Normal invoices, receipts or purchases must not be recreated merely to establish opening balances, since they can post revenue, taxes, payments or stock a second time.

Freeze the old system the evening before actual go-live and request every closing file at that same boundary. Later-arriving files must describe that original closing position, not their later upload date. The wizard supports partial module/party completion with affected-scope trading locks, and one authorized administrator may prepare, approve and execute imports.

Confirmed master-data policy: real missing customer/supplier contact details may remain blank and be flagged for later completion. Do not fabricate emails, telephone numbers, contacts, addresses or tax identifiers. Unique legacy codes, real party names and required financial/master mappings remain mandatory. See [the supplied format assessment](Wizard_Source_Format_Assessment.md) for actual fields, missing codes and valuation questions in the six reference workbooks.

## Delivery format

Supply a ZIP containing one **UTF-8 CSV per dataset**, plus a values-only **XLSX review copy** and signed closing reports. Use XLSX rather than legacy XLS for new structured exports. Preserve the original `cusout.xls` and `cheque in hand.xlsx` report formats as separate files for the existing customer importer.

- First row: column names. One detail record per row, with no merged cells, report banners, subtotals or grand-total rows in the structured CSVs. Separate related datasets into separate files/sheets.
- Dates: `YYYY-MM-DD`. Timestamps: ISO 8601 with offset, for example `2026-09-30T23:59:59+05:30`. The existing legacy reports retain their original supported date representation/layout.
- Money: plain decimal values, such as `18751.50`; no `Rs`, currency symbols or thousands separators. Financial amounts retain LKR cents. Preserve source precision for quantities, exchange rates and unit costs; do not round unit costs before calculating valuation.
- All identifiers are text: customer/supplier codes, account codes, SKUs, barcodes, cheque numbers, bank/branch codes, phone numbers and bank account numbers. Preserve leading zeros.
- CSV quoting must preserve commas, quotes and Sinhala/Tamil text. Empty optional values stay empty; missing values must not be replaced with invented names, costs or dates.
- No formulas, macros or external links in the data files. Add a data dictionary explaining every field, status/type code, sign convention, stock-cost method and balance calculation.
- Every row needs its immutable legacy identifier. If codes are unique only within a branch/company, include those keys everywhere and document their scope.
- Use legacy codes/names in the source export. NexusFlow IDs are assigned during mapping; the old developer must not guess new database IDs.
- For absent schedules, provide an explicit `NOT_APPLICABLE` statement or a header-only file and zero control total. Silence is not evidence of a zero balance.

## Required export datasets

These are proposed source-export column names, not universal upload headers. Fields describing a concept absent from the old system may be blank only with an explanation. Keep additional native fields when they aid traceability.

| File | Row represents | Required business information |
|---|---|---|
| `01_chart_of_accounts.csv` | One legacy account | LegacyAccountCode, AccountName, AccountType, ParentAccountCode, IsPostingAccount, IsActive, Currency, BranchCode/CostCentreCode if used |
| `02_trial_balance.csv` | One account and relevant dimension at closing | AsOfDate, LegacyAccountCode, AccountName, OpeningDebit, OpeningCredit, PeriodDebit, PeriodCredit, ClosingDebit, ClosingCredit, Currency, BranchCode/CostCentreCode if used |
| `03_customers.csv` | One customer, including inactive customers with balances | LegacyCustomerCode, Name, TradeName, TaxRegNo, BusinessRegNo, ContactPerson, Email, Phone, Mobile, AddressLine1, AddressLine2, Province, District, City, Country, CustomerGroupCode, PriceLevelCode, SalesRepCode, PaymentTermCode, CreditPeriodDays, CreditLimit, ReceivableAccountCode, RevenueAccountCode, IsActive; bank/branch/account details if maintained |
| `04_customer_open_items.csv` | One unsettled customer document or return credit | LegacyEntryId, LegacyCustomerCode, DocumentType, DocumentNo, DocumentDate, PostingDate, DueDate, OriginalAmount, SettledAmount, CreditAppliedAmount, SignedOutstandingAmount, Currency, ReceivableAccountCode, SalesRepCode, RelatedDocumentNo, Remarks, AsOfDate |
| `05_customer_advances.csv` | One remaining customer advance/unallocated receipt | LegacyAdvanceId, LegacyCustomerCode, ReceiptNo, ReceiptDate, PaymentMethod, OriginalAmount, AppliedAmount, UnappliedAmount, Currency, ControlAccountCode, BankAccountCode, Remarks, AsOfDate |
| `06_suppliers.csv` | One supplier, including inactive suppliers with balances | LegacySupplierCode, Name, TradeName, TaxRegNo, BusinessRegNo, SupplierGroupCode, ContactPerson, Email, AccountsEmail, Phone, Mobile, AddressLine1, AddressLine2, Province, District, City, ZipCode, Country, PaymentTermCode, CreditPeriodDays, CreditLimit, Currency, PayableAccountCode, ExpenseAccountCode, BankCode, BranchCode, BankAccountNumber, IsActive |
| `07_supplier_open_items.csv` | One unpaid supplier invoice or credit/debit adjustment | LegacyEntryId, LegacySupplierCode, DocumentType, DocumentNo, SupplierInvoiceNo, DocumentDate, PostingDate, DueDate, OriginalAmount, SettledAmount, CreditAppliedAmount, SignedOutstandingAmount, Currency, PayableAccountCode, RelatedDocumentNo, PurchaseOrderNo, GoodsReceiptNo, Remarks, AsOfDate |
| `08_supplier_advances.csv` | One remaining supplier deposit/unallocated payment | LegacyAdvanceId, LegacySupplierCode, PaymentNo, PaymentDate, PaymentMethod, OriginalAmount, AppliedAmount, UnappliedAmount, Currency, ControlAccountCode, BankAccountCode, Remarks, AsOfDate |
| `09_cheques_received.csv` | One cheque received from a customer | LegacyChequeId, LegacyCustomerCode, ChequeNumber, BankCode, BranchCode, BankName, BranchName, Amount, Currency, ChequeDate, ReceivedDate, ReceiptNo, Status, CustodyLocation, DepositDate, DepositReference, CompanyBankAccountCode, EndorsedSupplierCode, EndorsementDate, ClearedDate, BounceDate, IsAlreadyDeductedFromCustomerBalance, LegacyDocumentReferences, AsOfDate |
| `10_cheques_issued.csv` | One company cheque issued/outstanding at closing | LegacyChequeId, LegacySupplierCode/PayeeCode, PayeeName, ChequeNumber, CompanyBankAccountCode, BankCode, BranchCode, Amount, Currency, ChequeDate, IssuedDate, PaymentNo, Status, ClearedDate, CancelledDate, IsAlreadyDeductedFromSupplierBalance, IsAlreadyDeductedFromBookBankBalance, LegacyDocumentReferences, AsOfDate |
| `11_items.csv` | One distinct SKU/variant, including raw materials | LegacyItemCode, ParentProductCode, ItemName, ItemType, CategoryCode, SubcategoryCode, BrandCode, Colour, Size, Barcode, BaseUomCode, PurchaseUomCode, PurchaseToBaseFactor, SalesUomCode, SalesToBaseFactor, CostMethod, UnitCost, SellingPrice, MinimumSellingPrice, InventoryAccountCode, CogsAccountCode, SalesAccountCode, IsSellable, IsActive, LotTracked, ExpiryTracked |
| `12_stock_balances.csv` | One SKU × warehouse × location × ownership/condition, plus lot if tracked | AsOfDate, LegacyItemCode, WarehouseCode, LocationCode, LotNo, SerialNo if tracked, ManufactureDate, ExpiryDate, StockCondition, Ownership, BaseUomCode, OnHandQuantity, ReservedQuantity, AvailableQuantity, UnitCost, StockValue, InventoryAccountCode |
| `13_stock_cost_layers.csv` | One remaining stock cost layer, where FIFO/layer costing is used | LegacyLayerId, LegacyItemCode, WarehouseCode, LocationCode, LotNo, OriginalReceiptNo, ReceivedDate, BaseUomCode, RemainingQuantity, UnitCost, RemainingValue, Ownership, AsOfDate |
| `14_banks_and_cash.csv` | One company bank/cash account | AsOfDate, LegacyAccountCode, BankName, BranchName, BankCode, BranchCode, BankAccountNumber, Currency, BookBalance, StatementBalance, StatementDate, Remarks |
| `15_bank_reconciliation_items.csv` | One uncleared bank reconciling item | LegacyReconciliationItemId, CompanyBankAccountCode, ItemType, ReferenceNo, LegacyChequeId if relevant, DocumentDate, ValueDate, SignedAmount, Currency, IsInBookBalance, IsInStatementBalance, Status, AsOfDate |
| `16_other_opening_schedules.csv` | One separately explainable balance component | LegacyScheduleId, ScheduleType, LegacyAccountCode, PartyCode, ReferenceNo, DocumentDate, DueDate, SignedBalance, Currency, Remarks, AsOfDate; use separate detailed files for assets/loans/taxes where needed |
| `17_open_orders_headers.csv` and `17_open_orders_lines.csv` | One outstanding order and its remaining lines | Order type/ID/number, CustomerCode or SupplierCode, dates, status, currency; LineId, LegacyItemCode, OrderedQuantity, DeliveredOrReceivedQuantity, CancelledQuantity, RemainingQuantity, UomCode, UnitPrice, Discount, TaxCode, WarehouseCode, linked deposit references |
| `18_bom_headers.csv`, `18_bom_lines.csv`, `18_wip.csv` | One current BOM and its component lines; one unfinished production position | BOM/version IDs, finished SKU, effective dates, output quantity/UOM; component SKU, quantity/UOM, wastage; WIP production ID, stage, warehouse, quantities, issued materials, accumulated material/labour/overhead cost, value, status, AsOfDate |
| `19_lookup_codes.csv` | One lookup code | LookupType, LegacyCode, Name, ParentCode, Meaning, IsActive; include branches, warehouses/locations, UOMs/conversions, banks/branches, groups, price levels, payment terms, sales reps, categories, brands, colours/sizes, tax codes and document/status codes |
| `20_legacy_transaction_archive/` | Historical operational documents/events through actual freeze, including October; separate headers/lines/allocations/events | Immutable document/line IDs, document/posting/event dates, party/item/account/warehouse codes, quantities/UOMs, unit costs/prices, discounts/taxes, amounts, currency, statuses, links to receipts/payments/cheques/production, CreatedAt, ModifiedAt, CancelledAt/ReversedAt and GL/stock effects. This is an archive export, not an operational replay input for the first wizard release |

Request the open-order, BOM/WIP and other schedules only where the business uses them. They are still part of a complete migration assessment; if not used, record that explicitly. Historical archive exports are described below.

## Balance meanings and exceptions

### Customers

- Preserve every unpaid invoice/debit adjustment, returned-cheque debt and unapplied return credit separately, even when their customer net balance is zero. Do not net different documents into one customer row.
- Agreed signed convention: positive means the customer owes us; negative means a goods-return credit. Export advances separately rather than labelling every customer credit a goods return.
- The confirmed old outstanding field `creamt` is the amount still owed. `debamt` is audit information and **must not** be subtracted from `creamt`. For GT00011762 the confirmed remaining balance is **18,751.50**.
- Current customer opening debt due dates are document date plus **30 days**. Request original due dates anyway for audit and flag differences for business confirmation.
- Include every customer referenced by an outstanding item, return credit, advance or cheque, not only customers with a positive net balance. Codes must identify the same party in every file.

### Suppliers

- Request positive unpaid invoices, supplier credit notes/purchase returns, debit adjustments and supplier advances independently. Proposed supplier signed convention: positive means we owe the supplier; negative means the supplier owes us. The developer must explain their actual convention, rather than changing signs without documentation.
- Keep both the old internal bill number and the supplier's invoice number. Retain supplier codes and invoice-level balances even if a current wizard only accepts party names.
- Actual supplier payment terms and due dates need business confirmation; the agreed customer 30-day rule is not automatically a supplier rule.
- Include receipts not yet billed, bills with goods not received, goods in transit and disputed balances in separate schedules where they exist. Do not treat a purchase order as an unpaid invoice merely because it remains open.

### Cheques

- Received and issued cheques are different datasets. Within each, distinguish physically in hand, deposited but uncleared, endorsed, cleared, bounced and cancelled/voided positions. Supply a status dictionary and event/reference dates.
- The agreed received cheques in hand have **already reduced customer AR**. Their opening value is a separate cheque asset; they must not reduce AR again. Preserve receipt and document references without inventing missing legacy allocations.
- A deposited but uncleared cheque must be assigned to the correct book/bank reconciliation position, rather than being imported again as cash or an in-hand cheque. For endorsed/issued cheques, explicitly state whether AP and the bank book balance have already been affected.
- Request an allocation file if reliable links exist: `LegacyAllocationId,PartyType,PartyCode,ReceiptOrPaymentNo,LegacyChequeId,DocumentNo,AllocationDate,AllocationAmount`. It is supporting evidence, not an instruction to manufacture historical receipts in NexusFlow.
- Do not put a cheque already represented as a bounced customer debt into the collectible InSafe cheque schedule as well. Closed/cleared/bounced historical cheque records remain in the archive unless an unresolved operational position needs migration.

### Stock and raw materials

- Separate finished goods, raw materials, WIP, services and non-stock supplies. Buttons, zippers, thread, fabric and packing materials need distinct SKUs where they are stock-controlled. Services have no opening stock quantity.
- Request **physical on-hand** quantity, reserved quantity and available quantity separately. Do not import available quantity as if it were on-hand stock.
- Each balance must identify its unit, warehouse, stock condition and ownership. Report company-owned, consignment/customer-owned, damaged/quarantined and goods-in-transit positions separately so valuation ownership is correct.
- Costs must be inventory carrying costs, not selling prices. Supply quantity, unit cost and total carrying value, plus the costing method and rounding policy. Reconcile the value to the inventory GL accounts. Explain negative quantities/values and zero-cost stock; do not silently remove or replace them with 1.
- If preserving FIFO is required, supply the remaining cost layers. A single average-cost opening layer preserves an approved opening total but does not preserve historical FIFO cost behaviour; using that simplification needs business confirmation.
- For fabric or other materials in metres, kilograms or fractions, retain decimal quantities and UOM conversions. Do not convert them to whole PCS merely to satisfy a limited importer.
- If the same SKU exists in multiple warehouses/lots, request all those rows. Do not create fake SKUs solely to bypass an importer restriction.

### Accounts beyond AR/AP/inventory

Request closing cash, bank and reconciliation schedules, fixed assets with accumulated depreciation, loans/interest, customer deposits, supplier prepayments, tax balances, accrued expenses, payroll/other liabilities, retained earnings and any suspense balances that exist. For fixed assets include asset ID, acquisition/in-service dates, original cost, accumulated depreciation, net book value, depreciation method/rate/life and GL account codes. For loans include lender, agreement/reference, principal, accrued interest, repayments/due dates and GL accounts.

For a cutover during the financial year, also request the fiscal-year start/end dates and year-to-date/monthly GL balances through the actual closing date. Decide how opening income/expense balances and historical comparison reports will be presented; do not automatically close them into retained earnings as if the migration date were the financial year end.

## What NexusFlow can accept today

The following formats are verified from the current source. Hosted authenticated UAT is still required; the browser check during preparation timed out. These capabilities do not constitute acceptance of the full migration.

| Workflow | Current input | Important boundary |
|---|---|---|
| **Customer Opening Migration**, `/Finance/CustomerMigration` | Original-layout outstanding XLS/XLSX and cheque-in-hand XLS/XLSX together | Customer debts, goods-return opening credits and physically verified InSafe received cheques. Both reports must represent the day before the selected opening date. Generic normalized CSVs are not directly accepted by this reader. Customer advances, deposited/endorsed opening cheques and issued-cheque migration require further work. |
| Customer master operations on that page | Exact downloaded **Customer CSV Template** | Requires NexusFlow lookup/account IDs after mapping. Includes `LegacyCustomerCode`; no guessed or placeholder customers. |
| **Raw Material Import**, `/Inventory/RawMaterialImport` | `ProductName,SKU,Quantity,UnitCost` CSV | Creates new SKUs only; whole positive PCS quantities; positive unit costs with at most two decimal places; one warehouse/brand/category/date selection applies to every row. Maximum 2,000 rows and 2 MB per file. Extra CSV columns are ignored. Existing SKUs, decimal/UOM-rich materials, multiple locations/layers and shared SKUs across warehouses need another reviewed path. |
| Finished-goods legacy product import, `/InventoryImport/Index` | Legacy CSV headers below | Includes product/stock migration, but defaults to PCS/Main Warehouse and uses current-time posting in the inspected handler. Account mappings and cutover/warehouse controls need review before claiming complete production migration coverage. Do not also post the same quantities through a second stock-opening path. |
| `/Finance/ImportOpenInvoices` | `Type,PartyName,DocumentNo,Date,OutstandingAmount`; `Type=AP` | Only positive supplier AP rows; name matching, due date +30 days and current-time posting. Does not preserve supplier legacy-code mapping or import credits/advances. Duplicate-safe migration and cutover controls need further work before a production supplier migration. |
| `/Finance/ImportTrialBalance` | `AccountCode,Debit,Credit`, with cutover date selected on the page | Maps to NexusFlow account codes. Must balance; already imported customer AR/cheque controls and AR/AP/inventory accounts must not be posted again. An approved balanced residual file is prepared from the full old TB, not by deleting rows arbitrarily. |

Exact current customer master upload header (obtain the template from the installed target version before loading):

```csv
Id,LegacyCustomerCode,Name,TaxRegNo,CustomerGroupId,PriceLevelId,SalesRepId,ContactPerson,Email,Phone,AddressLine1,Province,District,City,Country,DefaultReceivableAccountId,DefaultRevenueAccountId,PaymentTermId,CreditLimit,CreditPeriodDays,BankId,BankBranchId,BankAccountNumber
```

`Id=0` is for a new customer; an update needs the explicit existing NexusFlow customer ID. The legacy developer supplies codes and real master details; the migration team maps `...Id` columns after preparing the new masters.

Existing outstanding report requires its original layout/semantics and at least these headers:

```csv
debcode,debnam,ref,tdate,xtyp,creamt,debamt,total,days,today,first30,second30,third30,fourth30,fifth30
```

Keep genuine legacy `total`/ageing conventions. The reader reconciles per-customer totals, signed ageing buckets and a consistent source date derived from document dates and ageing days. Preserve optional remarks, sales rep and returned-cheque references.

For `cheque in hand.xlsx`, preserve the existing grouped report title/date, receipt references, columns, subtotals and grand total. Column meanings in the current reader are cheque date, received date, CH receipt reference, raw document references, customer code, customer name, bank/branch code, cheque number and amount. The old **To be Dep Date** has been confirmed as the actual cheque date. A newly designed flat cheque export is useful as supplementary data but needs an adapter before direct upload.

Exact legacy finished-goods CSV fields read by the current preview parser:

```csv
type,type1,itemname,itemcode,brand,color,size,msize,lotno,avar,selprice,minsellpri,totqty
```

Request full original item/warehouse/UOM/value information in addition to this compatibility copy. These abbreviated fields alone cannot describe multi-warehouse, fractional raw-material or complete cost-layer migration.

## Reconciliation pack and historical archive

Alongside the detail files provide:

1. Full closing trial balance with total debit = total credit; balance sheet and year-to-date profit and loss.
2. Customer ageing and per-customer debt, return-credit, net AR and advance totals; explain any difference between the schedule and each AR control account.
3. Supplier ageing and per-supplier debts, credits, net AP and advance totals; explain differences to AP control accounts.
4. Cheque counts/amounts by direction, status, company bank account and party; physical safe count signed by the custodian. Identify amounts already included in AR/AP/bank balances.
5. Stock quantities and values by SKU, warehouse, UOM, ownership/condition and inventory account, plus a physical stock count. Do not add quantities across incompatible units into one meaningful grand quantity.
6. Bank statements and closing reconciliations, plus signed cash counts and the supporting asset/loan/tax/WIP schedules.
7. `export_manifest.csv`: FileName, DatasetName, AsOfDate, ExportedAt, CompanyCode, BranchScope, RowCount, ControlQuantity, ControlDebit, ControlCredit, ControlNetAmount, Currency, Sha256. Use separate grouped control totals where units/accounts differ. A checksum proves file integrity, not financial correctness.
8. `data_dictionary.xlsx` or Markdown: field meanings, required/optional fields, types, relationships, status codes, cost methods, sign conventions, example rows and reconciliation rules. Document meaning of legacy `debamt`, `creamt`, item `avar` and each cheque state.
9. A vendor-supported database backup and schema/restore instructions, plus readable historical sales/purchases/returns, receipts/payments/allocations, GL entries, stock movements and cheque events. Keep these in a restricted, read-only archive. NexusFlow does not directly ingest a foreign database backup.

The agreed pre-cutover archive should preserve readable reports and searchable references, including October transactions through the actual freeze. Export headers and detail lines separately with immutable document/line IDs, original dates, party/item/account codes, quantities, tax breakdowns, debit/credit amounts, status, links, audit timestamps and cancelled/reversed references. Keep this archive outside normal NexusFlow operational posting. October effects appear in the fresh opening balances; do not post both the balances and a replay of the underlying transactions.

## Loading and accounting controls

1. Collect sample files and dictionaries first; approve mappings without altering originals. Identify missing supplier, cheque, stock/UOM, WIP and advance migration support before final cutover approval.
2. Set up and map accounts, lookups, bank branches, warehouses, UOMs and party/item masters using stable legacy keys. Supplier legacy-code mapping needs implementation or a durable reviewed mapping table; it is not present in the inspected supplier entity.
3. Rehearse on a restored UAT database with approved controls, then freeze/re-export every closing snapshot at the agreed actual go-live boundary and retain the historical archive.
4. Load approved subledger openings once. Customer debts debit AR; return credits credit AR; in-hand cheques debit the cheque asset; supplier debt credits AP; company-owned stock debits the appropriate inventory account. Use reviewed opening-equity offsets and dedicated treatment for other schedules.
5. Prepare the balanced residual GL import by accounting for every journal already posted by those openings. Do not import AR/AP/inventory/cheque control amounts again. Derive the opening-equity bridge from the source TB and posted subledger totals, document it and have the accountant approve it; do not force a plug into equity to hide unexplained differences.
6. Match every control total and opening GL account to the full old TB, test settlements/returns/cheque bounce and stock consumption, and obtain sign-off. Start normal NexusFlow transactions only in released scopes; incomplete scopes remain locked under the approved staged-migration policy.

## Decisions requiring business confirmation

- Confirmed: transactions are in the old system only through actual go-live, the opening uses that actual go-live date, and earlier history remains in a read-only archive. Still confirm the exact operational freeze timestamp.
- Are there customer advances, supplier credits/advances, deposited/endorsed received cheques or issued uncleared cheques at closing? Obtain an explicit zero declaration if absent.
- Are all materials counted in PCS, or are metres/kilograms/fractional units involved? How many warehouses, lots and ownership conditions exist?
- Which inventory costing method and fiscal-year boundary apply? Must historical FIFO layers and year-to-date comparison reporting be retained?
- Are there open orders, WIP, assets, loans, taxes, GRNI or foreign-currency exceptions? The agreed opening amounts are LKR; flag exceptions instead of silently converting them.

These decisions do not prevent requesting the full export pack now.

## Message to send to the developer

> Please provide a complete migration export for our company in LKR, with all closing balances as at the end of the day before our agreed actual NexusFlow go-live. We will confirm the exact freeze date. Supply UTF-8 CSV datasets with immutable legacy codes, a values-only XLSX review copy, a field/status dictionary and reconciled closing reports. We need the chart of accounts and full trial balance; customers and document-level outstanding debts/return credits/advances; suppliers and document-level unpaid invoices/credits/advances; received and issued cheque positions with status, dates, bank/branch details and whether AR/AP/bank balances already include them; item/raw-material masters with UOM conversions; stock by SKU/warehouse/lot with quantity, cost and carrying value, including remaining cost layers where used; bank/cash and reconciliation schedules; and supporting balances for other accounts. Export operational history, including October transactions through the freeze, into a read-only archive with a supported backup. Preserve cusout and cheque-in-hand report layouts as compatibility exports regenerated for the actual closing date. Real missing contact details may remain blank; explain missing identity or financial fields. Please also identify the stock loc1-loc8 warehouse meanings, the avar/costval valuation basis and the missing outstanding-customer master codes listed in the attached format assessment. Supply later files for the same original closing date, provide sample files first, and explain unavailable/non-applicable schedules. Open-order/BOM/WIP exports may also be retained for later migration phases.

## Basis and verification

This request follows the common separation of masters, open customer/vendor/item entries and GL balances described in [Microsoft's migration documentation](https://learn.microsoft.com/en-us/dynamics365/business-central/ui-extensions-c5-data-migration); it also describes avoiding double posting. The specific NexusFlow input formats and limitations above come from the local source, not from Microsoft product capabilities. [Microsoft's configuration-package guidance](https://learn.microsoft.com/en-us/dynamics365/business-central/dev-itpro/administration/apply-company-configuration-packages) also distinguishes export structure and relationship mapping.

Local evidence: `CustomerMasterImportService.cs`, `CustomerMigrationFileReader.cs`, `ImportRawMaterialStockCommand.cs`, `LegacyProductCsvRecord.cs`, `ExecuteLegacyProductsCommand.cs`, `ImportOpenInvoicesHandler.cs`, `ImportTrialBalanceHandler.cs` and their page/controller templates. Customer migration acceptance remains subject to [the migration runbook](Customer_Opening_Migration_Runbook.md) and hosted UAT. No application source was changed while preparing this request.

## Implemented wizard contract

The current loading contract is the twelve datasets in [Migration Centre operating guide](Migration_Centre_Runbook.md) and its [blank templates](templates/README.txt). Use those exact headers for files sent to the wizard. The broader schedules requested below support financial sign-off, the legacy archive or future operational imports; they are not all direct wizard modules. Select an actual common closing date and transform the old reports without changing their economic meaning. Accounts/master dependencies may need their own run; post the full TB only after subledger/stock/bank scopes are released.
