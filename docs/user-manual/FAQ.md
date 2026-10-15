# Frequently Asked Questions

> Current opening-import workflow: [Migration Centre](../migration/Migration_Centre_Runbook.md), `/Migration`. The earlier separate raw-material/customer-import sections describe historical releases; their page routes redirect to Migration Centre and their posting APIs retire once a central project exists.

## Why can I view a page but not save a transaction?

The selected date may be outside an open financial period. Read the persistent warning or ask a user with `Permissions.Finance.ManagePeriods` to review `/FinancialPeriod`.

## A number sequence is missing. Should I create it manually?

Use **Repair Missing Sequences** in System Configuration. It creates only missing defaults and preserves existing counters.

## Why is a paid-looking invoice marked Pending Clearance?

A cheque covers it but has not cleared. Bank reconciliation clears deposited cheques; endorsed cheques use the controlled **Mark Cleared** action.

## What happens if an endorsed customer cheque bounces?

The original receipt and supplier payment are voided, AR and AP are recalculated, commissions are reset or clawed back, and a dated reversal journal is posted. Optional fees and customer recovery are recorded separately.

## Can I add purchased finished goods through Opening Stock?

No. Opening Stock is for initial cutover only. Use PO and GRN for purchased stock, or Production Orders for manufactured/job-work stock.

## Why can I not close a production order?

Contractor-held material or WIP cost may remain, output may not reconcile, sewing accruals may be unbilled, or a contractor claim may still be open. The detail page shows these balances.

## Can I edit an approved BOM?

Yes. Saving creates the next approved revision; released orders retain their frozen snapshot.

## Why can’t I see a raw material in the BOM component search?

Type at least two characters of its name or SKU. The item must be active and have Type **Raw Material (Non-Sellable Stock)**. An item placed in a raw-material category but saved as **Finished Good** does not qualify; ask an administrator to review its type before using it in a BOM.

## How are damages handled?

Normal waste is included in finished-goods cost. Abnormal company loss goes to the production-loss expense mapping. Contractor recoverable loss creates a supplier claim. Tax treatment of contractor claims is **Needs business confirmation**.

## Can I bulk-import raw-material opening stock?

After deployment, use **Inventory & Mfg → Raw Material Import**, not **Opening Balances Wizard**. Upload a CSV with `ProductName,SKU,Quantity,UnitCost`, review the setup and preview, then confirm and post. Maximum 2,000 rows and 2 MB per file.

## Why is my CSV's category/date ignored?

The settings on the import screen apply to every row, including opening date, warehouse, category, brand and `PCS`. This is explicitly shown before preview. The corrected stock file's extra columns are accepted but do not override those settings.

## Why does the category need a sales account for non-sellable items?

The application's category structure includes this mapping. Imported products remain **Raw Material (Non-Sellable Stock)** with selling price zero. Opening stock posts only an inventory debit and opening-equity credit, not a sale or consumption expense.

## Can I import the same SKU again?

No. Any existing SKU blocks the import, including an earlier successful upload. Check the opening-stock reference if a response was lost. Use the appropriate existing-stock workflow rather than renaming SKUs to get around the check.

## Are quantities or costs of 1 allowed?

Yes. Preview warns about them so the operator can confirm they are real values. The importer does not replace missing/invalid values with 1 or silently skip invalid rows.

## Customer opening migration questions

**Which source amount is imported?** `creamt` is the remaining debt. The confirmed GT00011762 example imports 18,751.50, not 110,128.50.

**What does a negative amount mean?** A goods-return opening credit. It reduces net customer AR and can be applied to debt through Receipts. No stock return or new tax credit note is created during migration.

**Why are cheques separate from AR?** The old system already reduced AR when it received these cheques. They enter Undeposited Funds against Opening Balance Equity; importing another receipt would reduce AR again.

**Why is the attached report blocked for 1 October?** Its report date is 28 September. A 1 October cutover requires refreshed reports closed at 30 September.

**What happens if an opening cheque bounces?** The full amount becomes a collectible debit memo due on bounce date. Legacy references are retained, but the system does not guess historical invoice allocations.

**Can I import the same file twice?** A repeated posted batch returns its existing result. Identical active source hashes, duplicate references and cheque identifiers are checked. Changing a reviewed mapping or master requires revalidation.

**Can I use the old opening invoice importer?** Customer AR must use Customer Opening Migration. The existing opening invoice route now accepts supplier opening bills only.

**Can we reverse after sign-off?** No. Unused batches may be reversed before sign-off; used balances require an approved correction. Audit records and dated history are retained.

## Migration Centre questions — current release

**Where do we import opening data now?** Use **Administration → Settings & Security → Migration Centre**, `/Migration`. The earlier separate-import answers describe older releases. Old pages redirect; after creating a project their posting APIs are retired.

**Must we have every file on day one?** No. Upload the available modules/whole parties and keep missing schedules **Awaiting Data**. Return to the same project for later files describing the original closing date. Affected scopes remain pending; final sign-off waits for complete data and reconciliation.

**Can we upload everything together?** Yes, using the template pack's `manifest.json` and a ZIP. Upload is staging only. Dependencies must be valid/imported before dependent financial rows; the full TB is a later stage after scope release.

**Can we leave contact details blank?** Genuine blanks are allowed and flagged **Details incomplete**. Real names, unique legacy codes and financial mappings are still required.

**What does a negative customer outstanding mean?** The supplied report's negative returned-goods amount is an opening credit. `creamt` is the confirmed remaining amount; do not subtract it from `debamt` or recreate a stock return.

**Why are cheques separate?** Their party-ledger effect is already included in the closing debt. InSafe adds cheque-safe assets; deposited/issued reflected instruments become pending bank items. Re-entering a receipt/payment would double-count.

**Why is an import blocked after changing a warehouse/default?** Approval covers resolved mappings, source and journals. Validate and approve the corrected position again.

**Why can one customer trade while another is blocked?** Whole scopes are independently reconciled/released. Missing/incomplete scopes remain locked; user permission alone cannot bypass the lock.

**Can we use a balancing GL entry to hide a difference?** No. AR/AP/stock/cheque/bank differences must be fixed in their owning component. The residual TB journal covers remaining closing accounts after opening contributions, excluding subsequent trading.

**Does this load complete historical accounts and production?** It loads closing masters, debts/credits, cheques, FIFO stock, bank positions and GL. Detailed history stays in the agreed read-only archive. Open orders, BOM/WIP, detailed asset/loan/tax registers and a searchable archive interface are future work.

**Can we undo an import?** Eligible unused, unreleased units can be reversed with a date/reason. Shared masters/bank book/TB or released/used units need a reviewed correction. Never delete audit records or downgrade the database after posting.

Full steps and formats: [Migration Centre operating guide](../migration/Migration_Centre_Runbook.md).

## Why did the dashboard show zero AR/AP after successful migration?

The earlier calculation counted new sales invoices and supplier bills while migration opening documents were stored separately. The corrected calculation includes those opening documents and follows the same dated balances as aging/statements. Rebuild/restart the updated application and refresh the dashboard; no re-import is needed. Cash & Bank also now follows its account hierarchy instead of summing every Asset account. See [verified figures and UAT next steps](../migration/Post_Finalization_Check_20261015.md).