# Customer opening migration runbook

> Historical customer-import release: the current implementation uses [Migration Centre](../migration/Migration_Centre_Runbook.md) and [its UAT update steps](../installation/UAT_Update_Migration_Centre.md). Preserve old audit history; use the central workflow for new cutover work.

Implemented 5 October 2026. Route: `/Finance/CustomerMigration`, page **Customer Opening Migration**, under **Accounting & GL**. This implementation is in the local source; production deployment and authenticated hosted UAT have not been performed. Stock is outside this customer migration.

## Agreed opening position

- Cutover: 1 October 2026. Both closing exports must represent 30 September 2026, before new-system trading starts. The attached samples represent 28 September and are deliberately blocked for this cutover.
- LKR only. Remaining debt is `creamt`, not `debamt - creamt`; `debamt` is retained for audit. GT00011762 must import 18,751.50 owed.
- Positive type 1 entries are legacy invoice debts; type 2 entries are legacy debit adjustments; type 8 entries are returned-cheque debts. Negative entries become goods-return opening credits. Debt due date is original document date plus 30 days. Unknown types and inconsistent totals are rejected.
- Cheques in hand have already reduced legacy AR. Import only physically verified InSafe cheques. “To be Dep Date” is the actual cheque date. Preserve cheque number as text, received date, CH receipt reference and raw legacy document references. Do not invent historical allocations.
- No historical sales invoices, receipts, inventory movements, revenue, tax or commissions are created. Customer advances and deposited/endorsed opening schedules are outside the agreed input scope.

## Accounting and subsequent work

| Opening component | Debit | Credit |
|---|---|---|
| Customer debt | Customer mapped AR | Opening Balance Equity |
| Goods-return credit | Opening Balance Equity | Customer mapped AR |
| Cheques physically in hand | Undeposited Funds | Opening Balance Equity |

The balanced opening journal nets signed amounts per mapped AR account. Cheques remain a separate asset, so they do not reduce customer debt again. Original AR and Undeposited Funds mappings are retained on opening records.

Receipts can allocate to new invoices, positive opening items or debit memos. Apply an opening return credit to eligible debt using the allocations in **Receipts**; this transfers settlement between existing balances without another journal or receipt. Unallocated credit remains visible. Dated receipt voids, credit reversals and batch reversals retain the earlier report position.

Deposit, endorsement, clearing and bounce use the existing treasury workflow. Opening cheque deposit moves its original Undeposited Funds to the selected bank account. Clearing links the exact deposit journal. An opening cheque bounce creates a collectible debit memo for its full amount, due immediately on bounce date, and credits the relevant cheque asset or reverses the endorsement. Legacy references remain available. No historical invoice is reopened by guessing an allocation.

## Before deployment

1. Back up the database, verify restore capability, and capture the current migration history. Rehearse on a restored UAT database, with outgoing notifications disabled by the environment operator.
2. Build and run the verification commands in `../uat/Customer_Migration_Verification.md`. Review and deploy the application and both schema migrations: `20261005011038_AddCustomerOpeningMigration` and `20261005014028_HardenCustomerMigrationControls`.
3. Generate a forward SQL script from the target database’s actual last applied migration to `20261005014028_HardenCustomerMigrationControls`. Example, when the database is at `20260622171142_FinancialControlsDocumentsAndProduction`:

```powershell
dotnet ef migrations script 20260622171142_FinancialControlsDocumentsAndProduction 20261005014028_HardenCustomerMigrationControls --project NexusFlow.Infrastructure --startup-project NexusFlow.Web --output docs/migration/customer-opening-upgrade.sql
```

4. Review existing PaymentAllocations before applying the new constraint: every row must have a positive amount and exactly one target. Existing cheques must have a valid original receipt. Correct invalid legacy application data through an approved repair; the upgrade does not silently discard it. Schema changes preserve SQL Server temporal history.
5. Confirm an open financial period covers cutover. Configure `Account.Equity.OpeningBalance` and `Account.Asset.UndepositedFunds` using the application’s account configuration. OBE must be an active transaction Equity account; Undeposited Funds an active transaction Asset account. Verify customer AR mappings are active transaction Asset accounts and distinct from Undeposited Funds. Imported control accounts must have zero opening balances and no prior financial postings.
6. Assign the permissions below and complete hosted UAT. Posting is off when `Migration.Customer.Enabled` is absent or false. Do not enable by editing a production database before approval.

## Operator sequence

1. Freeze the old system at the agreed closing point. Export refreshed outstanding and physical InSafe cheque reports for the same closing date. Record signed source totals and keep the old system read-only.
2. In **Customer Master**, maintain unique **Legacy Customer Code** values. In **1. Prepare customer masters and legacy codes**, download **Customer CSV Template**. Use Id 0 for a new customer, or the explicit existing Id for an update. Fill real contact, address, group, price level, sales rep, payment terms and account mappings. Use the displayed master IDs. **Preview Customers**, review every field and error, confirm, then **Import Customers**. The financial importer never creates placeholder customers.
3. Select cutover and source closing dates, both files, and **Validate and Preview**. Files are retained with a source hash in a saved draft; preview creates no financial transactions.
4. Resolve customer errors and verify every legacy bank/branch code against the correct active branch. Create missing banks/branches through the existing master workflow. Select verified branch mappings and **Revalidate Mappings**. No fuzzy customer matching or automatic bank creation occurs.
5. Review per-customer debts, return credits, net AR, cheque totals, item detail, physical checklist and opening journal. Resolve all errors and explain warnings for cheques dated before cutover. Changing files, dates, mappings or underlying masters requires a new preview or revalidation.
6. After approved UAT, open **Posting activation after UAT**, confirm approval and **Enable Migration Posting**. Revalidate. Independently check every cheque in the safe and confirm the physical-verification checkbox.
7. **Post Customer Opening** once. Posting is atomic; retries of the same posted batch return its existing result. Database constraints, serializable transactions and review hashes prevent duplicate or stale posting. A failed journal rolls the complete posting back.
8. Use **Reconcile Batch**. Every imported customer position, item count, cheque count and original opening journal must match the source. Download customer totals and both retained sources for the signed migration pack. Reconciliation checks the imported opening position; use statements/ageing and the GL to reconcile later trading separately.
9. Confirm total AR, Undeposited Funds and the rest of the opening balance sheet. Exclude the already imported AR control accounts and Undeposited Funds from **Import Trial Balance**; the backend rejects those account IDs. The old opening-invoice route now handles supplier bills only. Import the remaining opening trial balance and resolve Opening Balance Equity against the full migration, never force it to zero without explanation.
10. When the migration pack is approved, **Sign Off and Lock Migration**. Sign-off disables further migration posting and prevents batch reversal. Normal receipts, credit application and cheque processing continue.

## Reversal and recovery

Before sign-off, **Reverse unused migration batch** requires a date, reason and open period. It is blocked after downstream customer finance or cheque activity. Reversal posts an opposite journal and retains source and opening records. To replace an unused opening at the same cutover date, reverse at cutover before any operations, then preview a corrected batch. A later-dated reversal cannot be followed by a backdated replacement; use an approved dated correction to preserve historical reports.

Do not downgrade schema after importing balances: opening cheques intentionally have no original receipt, and new tables contain audit and settlement history. Use application reversal when eligible, a reviewed forward repair, or a coordinated backup restore before operations resume. Never restore only the customer tables or delete posted opening records. Schema Down scripts are not the production rollback path.

## Access and audit

The migration page and API require both `Permissions.Finance.ManageAccounts` and `Permissions.Treasury.ManageCheques`. CSV master operations additionally require `Permissions.MasterData.ManageCustomers`. SuperAdmin bypasses permission checks. Exact business role assignments in the hosted tenant: **Needs business confirmation**. Credit history needs ViewReceipts; application needs CreateReceipt; application reversal needs VoidReceipt.

Cookie writes require an antiforgery token. A fabricated Authorization header does not bypass cookie CSRF checks. Retained originals, hashes, source-row references, temporal records, posted journal IDs, user/time fields, reconciliation and sign-off support audit. Access to source downloads is subject to the same migration permissions. Do not commit source exports to Git.

## Sample verification baseline, not the final closing balance

| Attached 28 September sample | Count | LKR |
|---|---:|---:|
| Outstanding items | 1,150 | 162,114,985.14 net AR |
| Positive debts | 1,130 | 162,436,582.24 |
| Return-credit items | 20 | -321,597.10 |
| Customers with outstanding rows | 331 | — |
| In-hand cheques | 49 across 17 customers | 5,328,628.00 |

The combined customer-code set contains 333 customers. MI129 and SR871 occur only in the cheque schedule and still require customer masters. The 17 bank/branch codes need verified mappings; the sample includes codes absent from the seeded master. The negative-balance customer table remains in `Customer_Outstanding_Import_Assessment.md`. No real source file was posted during implementation.
