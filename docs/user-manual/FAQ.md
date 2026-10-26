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
## Can we reuse the functional UAT upload package in production?

No. The October UAT package includes synthetic bank/reconciliation/TB entries, UAT master names, substituted branches and test dates. Obtain certified production closing data and follow the [production migration guide](../migration/Production_Migration_Guide_20261015.md). Clearing staging tables alone does not clear operational journals or stock. A confirmed test-position cleanup is a separate backed-up database operation; it is not a Migration Centre button or permission to remove genuine trading history.

## Client pilot replay — 15 October 2026

The user has requested a replay of the finalized local test data for a client pilot, including its disclosed fixtures. Use the [updated pilot package and stages](../migration/pilot_upload_20261015/README_START_HERE.md). Its bank, issued cheque and TB assumptions are for functional testing; the real financial cutover guidance above still requires certified source balances. Preparing the package does not clear or import the hosted database.

## Background progress questions — 2026-10-21

**Can I refresh during release, validation or ledger checking?** Yes, after the operation is accepted and the page says **Running on the server. You can safely refresh or leave this page.** Reopen the saved project to recover its current operation. Keep upload pages open until the server confirms the file was staged.

**Does “1 released; 0 unresolved” mean the whole project is finished?** Only if it is the project overview. **This operation** counts only its frozen selection. Check the separate project pending/released totals.

**What does Completed With Issues mean?** The operation finished its targets, but some need attention. Successful releases stay committed. Select **View unresolved scopes** to inspect reasons; missing controls and mismatched totals leave scopes pending.

**What if connectivity is lost?** The last confirmed progress remains visible with **Connection lost—checking progress**. This is not a job-failure message. Wait for reconnection or reopen the page. **Retry start request** reuses the original request ID if acceptance was uncertain.

**Why is Resume unavailable?** The server must confirm the old job stopped and acquire its worker lock. An active worker or unavailable/uncertain job service blocks Resume. Changed files, mappings or scope requirements require fresh review and validation/approval. Completed validation/ledger checks with issues should be run afresh after correction.

**Can I pause or stop work?** Pause/Stop controls are outside this version. Safe recovery uses saved progress and guarded Retry/Resume.

**Does changing import modules change release selection?** No. Release uses its own Customer/Supplier/Stock/Bank view, selected pending scopes or all matching pending scopes. All matching includes other pages; the action displays the exact count.

**Where are the long lists?** Search/filter each stage and choose 25, 50 or 100 rows. Open source/journal details and grouped-warning affected rows as needed. Operation history contains older activity; the live feed keeps the latest 20 items.

**Does this reopen project 3?** No. The finalized hosted opening remains read-only. The additive upgrade preserves posted data, released scopes, datasets, legacy run payloads and actual temporal settings; it does not purge history.
