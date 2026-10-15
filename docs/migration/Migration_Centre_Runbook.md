# Migration Centre operating guide

Implemented 6 October 2026. Entry: **Administration → Settings & Security → Migration Centre**, `/Migration`. This guide supersedes the separate opening-import page instructions for this release. Keep their original batch history for audit.

## Boundary and preparation

Use one saved company migration project. Enter the **actual go-live opening date**; the legacy closing snapshot is the previous evening, Asia/Colombo. All values are LKR. The earlier 1 October proposal is not a hard-coded production cutover. The actual freeze date, complete fresh closing exports and financial sign-off are **Needs business confirmation**.

October transactions remain in the old system until the agreed switch. Include their financial effects in the fresh closing position. Keep earlier documents in the agreed read-only legacy archive; the wizard does not replay historical sales, purchases, revenue, taxes, receipts or stock movements.

Start against a clean opening ledger with master/configuration data. Existing unrelated financial journals or unexplained account balances prevent project creation. This protects against adding opening balances to training or previously imported balances. Do not delete live financial records to bypass it. An already posted customer migration batch can be adopted only with explicit evidence, the same original opening/closing dates, its supported journal position and unprocessed InSafe cheques. Adoption retains originals and posts no second opening journal. Other existing financial history requires a separately reviewed migration target/reconciliation strategy.

Before creating the project:

1. Back up and rehearse restoration of the target database, installed application, configuration, keys and file storage.
2. Deploy the application and its schema upgrade together; see [UAT update instructions](../installation/UAT_Update_Migration_Centre.md).
3. Prepare posting accounts/account folders, financial periods, party groups, payment terms, price levels, warehouses, brands, categories, inventory accounts, base units and real bank branches through their normal maintenance pages. Importing `Accounts` creates posting accounts, not account folders. Map a different legacy chart to the chosen NexusFlow chart explicitly.
4. Arrange the business freeze and collect independent closing reports, document counts, physical cheque verification and warehouse counts/values. Perform at least one complete migration rehearsal and business review before the real freeze. This follows the preparation, data dependency and sign-off approach in [Microsoft's migration guidance](https://learn.microsoft.com/en-us/dynamics365/guidance/resources/migrate-data) and [go-live checklist](https://learn.microsoft.com/en-us/dynamics365/guidance/implementation-guide/prepare-go-live-checklist); it is not a claim of certification.

## Files and template contract

Select **Download Template Pack**. It contains twelve header-only UTF-8 CSV templates, `manifest.json` and `README.txt`. The same headers are accepted in values-only XLSX. The six supplied legacy reports have structural adapters; canonical files are preferable when information must be added or corrected. Do not change a stale report's date to make it appear current.

| Dataset | Required business content |
|---|---|
| `Accounts` | Posting account code, name, `AccountType`; existing folder `ParentCode` if needed |
| `CustomerMaster` | Unique old `Code`, actual `Name`; genuine contacts where available, group/terms/price/account codes or approved defaults |
| `SupplierMaster` | Unique old `Code`, actual `Name`; genuine contacts where available, group/terms/payable/expense codes or approved defaults |
| `CustomerOpenings` | Every unsettled invoice, goods-return credit and advance, including offsetting zero-net parties; original reference/date and **remaining** signed `Amount` |
| `SupplierOpenings` | Every unpaid bill, supplier credit and advance; original reference/date, actual due date or explicitly approved supplier terms, remaining signed `Amount` |
| `ReceivedCheques` | Customer, legacy receipt/reference, cheque number/date, receipt date, real branch code, amount, custody/status and explicit ledger-reflection flags; bank/supplier details for Deposited/Endorsed instruments |
| `IssuedCheques` | Supplier, reference, text cheque number, cheque face date, issue `DocumentDate`, amount, bank book account and reflection flags |
| `Stock` | All SKU/warehouse FIFO layers, item type/classification, base-unit mapping/conversion, quantity, unit cost and original layer date; approved summarized-layer evidence if original layers are unavailable |
| `BankOpening` | One bank/cash book position per mapped account: signed book `Amount` and signed `StatementBalance` |
| `BankRecon` | Other uncleared book items, with original date/reference and explicit signs; exclude instruments already supplied as received/issued cheques |
| `TrialBalance` | Complete closing chart position, including zero balances for every active NexusFlow posting account after mapping; nonnegative one-sided `Debit`/`Credit` |
| `ControlTotals` | Independent legacy control schedule for bulk release: scope `Kind`, `Code`, exact reconciliation `Component`, `Amount`, `RowCount` |

The downloaded templates are the exact field contract. Code, cheque number and document references must be text, preserving leading zeros. Use ISO dates `yyyy-MM-dd`, dot decimals without currency symbols, amount precision 2, quantity precision 4 and unit cost precision 6. `LegacyRecordId` should be a stable source ID; where absent, the displayed composite identity must be stable and unique. No merged cells/subtotals in canonical files. Export formulas as values; XLSX formula/macro workbooks are rejected. Workbooks are parsed without executing spreadsheet formulas or macros. Individual files: 20 MB and 20,000 normalized rows maximum. ZIP: 20 MB compressed, 100 MB expanded, up to 25 entries, unique plain filenames.

For a ZIP, edit `manifest.json` to the project's source identifier and closing date. Its version is `1.0`; each file entry has `Module` and `File`. Include only files with actual rows. Omit missing and not-applicable templates from the manifest, then set their states in the wizard. An empty template is not zero-balance evidence. ZIP upload stages files, with no financial posting; after a staging interruption inspect retained revisions and retry the affected files.

## Administrator workflow

1. Select **New Project**, enter **Project name**, **Stable source system identifier**, and **Actual go-live opening date**, then **Create Project**. For supported prior customer batches enter **Existing customer batch adoption evidence** first. Later work must resume this project.
2. In **1. Upload closing data**, use **Upload Dataset** for separate files or **Upload Package** for the ZIP. The file list retains revisions, dates and downloadable originals. Uploading does not change balances.
3. In **2. Map and confirm source treatment**, choose exact masters and approved defaults. Use **Add Mapping**, or **Change source column mapping** for different headers. Resolve all missing identity/account/warehouse/unit/branch mappings; do not match parties by similar names. **Save Mappings and Evidence** persists the settings. A column profile creates the corresponding party/SKU/bank scopes from the mapped codes.
4. Leave missing schedules **Awaiting Data**. Use **Not Applicable** only with independent signed zero/not-applicable evidence. Supplied rows cannot be declared not applicable. Contacts may remain genuinely blank; created customer/supplier masters show **Details incomplete** in their normal lists. Normal maintenance validation applies when details are completed later.
5. Confirm customer due dates: a blank due date becomes original document date plus 30 days. Supplier due dates require the source date or **Approved supplier terms in days**; no customer-term assumption is made. Choose an approved missing-source stock unit/warehouse only when the business confirms it. Defaults never replace a nonblank source code.
6. Missing FIFO dates require **Summarized stock layer approval**. Legacy finished-goods average cost needs explicit valuation evidence; discrepancies must still reconcile to inventory in the closing trial balance. Negative quantity/cost is blocked. Correct canonical costs or resolve the legacy valuation before final sign-off.
7. In **3. Select this import**, choose available modules. Blank filters include all codes; otherwise enter complete customer/supplier/SKU/bank codes. A code selects all its rows, never individual invoices. Import accounts/master dependencies first when needed, then the available financial components. The full trial balance is posted in a later reconciliation stage, after all scopes are released.
8. Select **Validate Selected Import**. Expand each scope to review normalized source rows, warnings, resolved accounts and proposed journals. Correct every error. Review the opening position and physical instruments, enter **Approval evidence**, then **Approve Selected Import**. Settings, source revisions and selection changes invalidate approval.
9. Select **Execute Approved Import**. Follow **5. Execute and monitor**. Hangfire posts in the background. One party's selected financial components, one complete SKU across warehouses, or one bank group commits atomically. Foundations can commit before dependent finance. A failed group rolls back; earlier completed groups remain recorded. Use **Resume Run** after its worker is confirmed stopped/failed and the cause is resolved. Do not edit the project during an active run. Replays recognize committed source identities and never add duplicate openings.
10. In **6. Reconcile and release scopes**, select **Reconcile**. Independently enter every control total and row count from the old-system reports, plus complete-position evidence, then **Release Reconciled Scope**. Do not copy displayed totals and call them independent verification. Missing required components, changed posted snapshots or differences prevent release.
11. For many scopes, upload, approve and execute a separate independent `ControlTotals` schedule. Choose the relevant modules/code filters and **Release Selected Reconciled Scopes**. Each passing scope releases; failures remain pending and are listed. Include zero components.
12. A genuinely new-system customer/supplier/SKU/bank with no legacy opening can use **Add a new-system scope** with an exact existing master, code and evidence. This cannot bypass uploaded or posted legacy balances. Pending scopes otherwise remain locked for financial/stock writes; master maintenance and drafts remain possible. Released scopes can trade while other migration work continues.
13. Supply the complete closing `TrialBalance`, validate/approve/execute it separately, and select **Check Full Ledger**. The residual journal subtracts migration opening journals only; it excludes later trading. AR, AP, inventory, cheque-safe and bank control differences must be corrected through their owning module, never hidden by a GL plug.
14. After every required financial schedule is supplied or evidenced zero, every current row is imported, every scope is released/classified, and all account differences are zero, enter **Final financial sign-off evidence** and select **Finalize Migration**. The saved project becomes read-only. Until then the application and shared PDF/XLSX exports identify the opening position as provisional. Finalization releases scope locks but retains the prohibition on backdating operational finance into the legacy period.

## Independent control schedule

`Kind` is exactly `Customer`, `Supplier`, `Stock` or `Bank`. `Code` is the same legacy scope code used for imports.

| Scope | `Component` keys | Independent amount and count |
|---|---|---|
| Customer | `CustomerOpenings:Debit`, `CustomerOpenings:Credit`, `ReceivedCheques` | Gross remaining positive debts; absolute credits; cheque face total. Both opening debit/credit components use the **total document-row count**, including credits. Cheques use instrument count. |
| Supplier | `SupplierOpenings:Debit`, `SupplierOpenings:Credit`, `IssuedCheques` | Gross bills, absolute credits and positive issued-cheque total; opening count includes all bill/credit rows. |
| Stock | `Stock:<warehouse-code>:Quantity`, `Stock:<warehouse-code>:Value` | Converted base quantity and sum of individually rounded layer values, per warehouse; FIFO layer-row count. Map the warehouse code. |
| Bank | `BankOpening`, `BankRecon`, `BankStatementBridge` | Signed book total, signed other pending-item total and book/statement bridge. BankOpening count is 1; BankRecon count excludes cheque datasets; bridge count includes pending received/issued/other instruments at legacy closing. |

For example, one customer debt of 100 and no cheques has debit control 100/count 1, credit control 0/count 1, received-cheque control 0/count 0. Zero declarations do not remove these independent release checks. Bank bridge: **book = statement + outstanding deposits − uncleared payments**, using the original closing snapshot. Later clearing/cancellation does not rewrite that opening comparison.

## Subsequent customer, supplier and cheque work

Opening items participate in unpaid-document selection, statements, AR/AP ageing and settlement. Positive opening amounts are debts; negative values are usable credits/advances. `cusout.creamt` is the confirmed amount still owed. Do not calculate `debamt - creamt`. Customer returned-goods values remain negative credits, not new sales returns that would move inventory again.

Received opening cheques already reduced customer AR. InSafe cheques add only the separate cheque-safe asset; Deposited cheques already in bank book add pending clearing items without another bank journal; Endorsed instruments retain customer and supplier links. Issued opening cheques already reduced supplier AP and bank book. Explicit false reflection flags that conflict with this closing position require a corrected source/legacy adjustment, not a silently guessed accounting treatment.

Use normal Receipts and cheque workflows for customer settlement, endorsement and dishonour after scope release. A deposited opening cheque bounce also resolves its pending bank item and creates the dated customer debt without recreating a historical receipt. Supplier **Payments**, `/Treasury/Payments`, includes opening debts and **Apply Opening Credit**. Applying an already posted opening credit reallocates the subledger; it does not create another GL credit.

**Opening uncleared bank items** appears in `/Finance/BankReconciliation` and `/Treasury/Payments` for cheque managers. **Mark Cleared** records clearing without a second payment/journal and cannot predate a cheque's face date. **Cancel Issued Cheque** requires a reason and open-period date; it restores bank and supplier debt with a dated journal. Received cheque dishonour uses the existing cheque workflow.

## Correction and completion boundaries

Use **Review Reversal → Reverse Unused Unit** only for an unused, unreleased financial unit with a valid date/reason. Originals, source identities and the opposite journal remain in the audit. A corrected unused scope can then be revalidated/imported. Released/settled/consumed units require reviewed dated corrections. Account/master units, trial balance, bank book openings and adopted shared-journal positions are not reversed through this action. Do not run schema Down migrations after importing business data; restore a verified recovery point when rollback is required.

This release imports closing masters/subledgers/stock/bank/GL. It does not migrate open orders, BOMs, work in progress, fixed-asset registers, loan schedules, tax-return registers or a searchable historical archive into NexusFlow. Obtain those supporting schedules for financial sign-off and retain the agreed archive. Their detailed operational importers are separate future work. Actual role assignments, warehouse/UOM dictionary, finished-goods cost meaning, supplier due dates, bank reflection flags, supporting schedules and final closing exports remain **Needs business confirmation** where not yet supplied.
