# NexusFlow ERP Admin User Manual

> Current opening-import workflow: [Migration Centre](../migration/Migration_Centre_Runbook.md), `/Migration`. The earlier separate raw-material/customer-import sections describe historical releases; their page routes redirect to Migration Centre and their posting APIs retire once a central project exists.

## Financial Periods

Open `/FinancialPeriod` with `Permissions.Finance.ManagePeriods`. Create non-overlapping periods and close only after review. Closed and missing periods block transaction commands before document number generation or database changes. Master data, configuration, reporting, and period management remain available.

## Configuration Health

1. Open **System Configuration**.
2. Review the Number Sequences warning, or call `GET /api/config/health`.
3. Select **Repair Missing Sequences** when required.
4. The repair creates missing defaults only. It does not alter an existing prefix, suffix, delimiter, or counter.

The Admin dashboard also lists missing sequences. The production tolerance setting is `Production.OverproductionTolerancePercent` and defaults to 5.

## Cheque Controls

- Use `OwnCheque` for company-issued cheques.
- Use `EndorsedCustomerCheque` only through Treasury Payments or Cheque Vault endorsement actions.
- Fully cheque-covered documents remain `PendingClearance` until deposit reconciliation or **Mark Cleared**.
- On dishonor, select the dishonor date and enter reason. Optional bank fee and customer recovery create visible accounting entries.
- Customer recovery is linked to a Customer Debit Memo.
- Paid commission is reversed through a clawback entry.

Whether a separate permission is required for **Mark Cleared** beyond `Permissions.Treasury.ManageCheques` is **Needs business confirmation**.

## Print Audit

Final Print and Download actions retain the PDF in secure storage and record document type, ID, number, SHA-256, allowed overrides, user, timestamp, and action. Preview is temporary. Users must hold the source document's existing view permission.

## BOM Administration

Saving an approved BOM edit creates a new approved revision. Enter a basis quantity and effective date. Never change a released order's recipe by editing the BOM; revise the production order if its authorized quantity or tolerance changes.

The BOM lookup requires at least two characters before it queries the catalog. This avoids loading every product when the modal opens. Target lookup results are active `FinishedGood` variants only; component lookup results are active `RawMaterial` variants only. If existing material records were created as Finished Goods, recategorizing them is not enough: review and correct their **Type** in Item Master before they can be selected as BOM components.

## Raw Material Import Administration

The new page is `/Inventory/RawMaterialImport`. Both `Permissions.MasterData.ManageProducts` and `Permissions.Inventory.AdjustStock` are required for its page and all API routes, including preview, setup and template download. Cookie requests require antiforgery validation. No permissions are granted by this feature.

Before import, ensure an active warehouse, one uniquely named brand, one `PCS` unit, an open financial period, and the `OpeningStock` and `JOURNAL` number sequences exist. The screen selects `WH-MAIN` and `UNBRANDED` only if they already exist; it never silently creates substitutes.

Review these installed system configurations. Values are account **codes**, not database IDs:

| Configuration | Required account | Use |
| --- | --- | --- |
| `Account.Inventory.RawMaterial` | Active postable asset | Opening stock debit; category inventory mapping |
| `Account.Equity.OpeningBalance` | Active postable equity | Opening stock credit |
| `Account.Cost.COGS` | Active postable expense | Category cost mapping; no expense posted on import |
| `Account.Sales.Revenue` | Active postable revenue | Required category mapping; no revenue posted on import |

Account names and codes appear in preview. Confirm that the installed cost mapping is appropriate for the business; no account ID is hardcoded. A missing category is created inside the same transaction as products, FIFO layers, stock transactions and journal. An existing category is reused only if all three mappings match; otherwise import is blocked and it remains unchanged. Do not repurpose a shared finished-goods category solely to clear validation.

Upload limits: 2 MB, 2,000 rows, SKU length 50, product name length 200, category name length 100. Numbers use a decimal point without thousands separators. The server reparses the file and revalidates setup on posting; an altered file/date/setup requires a fresh preview. Failed transactions roll back catalog and accounting writes. The normal number-sequence service may leave unused numbers after failure; this does not mean stock was posted.

No schema migration is required for this feature. Deploy the application and static assets together. It does not enable the legacy finished-goods importer or modify its behavior. Complete hosted UAT before importing real opening balances. Visual verification is still pending because the browser-control tool failed to initialize during local development.

Run opening imports sequentially during cutover. Execution uses a serializable transaction to keep setup, category and SKU checks stable through posting. A concurrent database conflict produces an error; check **Opening Stock**, then preview again rather than repeatedly submitting.

## Customer Opening Migration

Open **Accounting & GL → Customer Opening Migration** (`/Finance/CustomerMigration`). This requires ManageAccounts and ManageCheques; customer CSV operations also require ManageCustomers. Full deployment and cutover steps are in the [migration runbook](../migration/Customer_Opening_Migration_Runbook.md).

1. Prepare complete customer masters and unique **Legacy Customer Code** values. Download **Customer CSV Template**; Id 0 creates a customer, an explicit existing Id updates it. Fill every template column using verified master IDs. Choose the CSV, **Preview Customers**, check the details, confirm and **Import Customers**.
2. Choose cutover and source closing date, customer outstanding and cheque reports; select **Validate and Preview**. The closing date is the day before cutover. Reports must agree with that date.
3. Fix every error. Verify bank/branch mappings, then **Revalidate Mappings**. Review debts, return credits, net AR, physical cheque detail and opening journal. The financial import does not create missing masters.
4. After UAT approval, confirm **UAT for this migration has been approved** and select **Enable Migration Posting**. Revalidate; physically verify each cheque and confirm the checklist checkbox.
5. Select **Post Customer Opening**, then **Reconcile Batch**. Download customer totals and retained source files. Resolve all differences before approval.
6. Reconcile the rest of the opening balance sheet and exclude imported AR/Undeposited controls from **Import Trial Balance**. When approved, **Sign Off and Lock Migration**. This disables further opening posting and reversal.

Before sign-off, an entirely unused batch may be reversed using **Reverse Batch**, a valid date and reason. Downstream activity prevents this. A reversal after cutover cannot be followed by a replacement backdated to cutover. Use an approved correction. Never delete posted opening records or downgrade schema after import.

## Migration Centre — current opening-import workflow

The separate Raw Material Import and Customer Opening Migration instructions above describe their original releases. Use **Administration → Settings & Security → Migration Centre**, `/Migration`, for this release. Old opening page links redirect here; old posting APIs are retired once a project exists. Do not run both import paths.

Preconditions: deployed application/schema, an authorized migration account, clean opening ledger, open financial period, master dictionaries, original-file storage and an operating background worker. Obtain fresh exports for the evening before the actual switch. Missing schedules remain **Awaiting Data**; they must not be entered as zero.

1. Select **New Project**, enter the source identifier and actual go-live date, then **Create Project**. Use **Saved migration project** for all later uploads.
2. Select **Download Template Pack**. Upload individual files using **Upload Dataset**, or a matching manifest ZIP using **Upload Package**. Nothing posts on upload.
3. Map exact old codes and approved defaults. Use **Change source column mapping** for different headings. Supply evidence for zero modules, supplier due terms and any summarized stock valuation; select **Save Mappings and Evidence**.
4. Choose available modules and whole customer/supplier/SKU/bank codes, then **Validate Selected Import**. Resolve errors; review warnings, original rows, totals and journals. Genuine blank contacts are permitted and flagged **Details incomplete**.
5. Enter **Approval evidence**, select **Approve Selected Import**, then **Execute Approved Import**. Monitor saved runs. Use **Resume Run** after a failure has been resolved and its worker has stopped. Do not change source files to bypass duplicate detection.
6. Compare each scope with independent old-system controls and counts. Select **Reconcile**, enter complete evidence and **Release Reconciled Scope**. For bulk release import an independent `ControlTotals` schedule and choose **Release Selected Reconciled Scopes**.
7. Upload missing components later against the original closing date. Pending scopes stay blocked for financial/stock transactions; released scopes can trade. Use **Add a new-system scope** only with evidence that an existing master has no legacy opening.
8. After all subledgers are complete/released, import the complete **TrialBalance**, then **Check Full Ledger**. Correct control differences in their source module. Enter final evidence and **Finalize Migration** only when every difference is zero.

The system displays provisional balances until finalization. Finalized projects are read-only. **Review Reversal → Reverse Unused Unit** is limited to eligible unused, unreleased financial units; later corrections require a dated approved adjustment. Keep the legacy archive read-only. Detailed registers, open orders and BOM/WIP imports are outside this release.

Use the [Migration Centre operating guide](../migration/Migration_Centre_Runbook.md) for exact file meanings, independent control keys and cheque treatment, and [UAT deployment steps](../installation/UAT_Update_Migration_Centre.md) before hosted use.

## Checking finalized migration and dashboard totals

The dashboard correction requires an application rebuild/restart and no database migration or re-import. For the standard installed chart, cash/bank grouping uses account 1110 and its hierarchy. A custom chart can set Dashboard.CashBankAccountRoot through System Configuration using an active Asset folder code. The configured Account.Asset.UndepositedFunds account is excluded. Invalid grouping or mapping displays Unavailable; do not replace missing classification by summing all assets.

Before broader UAT, review clearing/payroll mappings against their intended account roles. Current local unbilled receipts point to AP control 2110, and salary advances point to Cheques In Hand 1151; both need business confirmation for their affected workflows. No mappings were changed by the dashboard correction.

See [finalization verification and the administrator checklist](../migration/Post_Finalization_Check_20261015.md).