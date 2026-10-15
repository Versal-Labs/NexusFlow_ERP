# NexusFlow ERP User Manual

> Current opening-import workflow: [Migration Centre](../migration/Migration_Centre_Runbook.md), `/Migration`. The earlier separate raw-material/customer-import sections describe historical releases; their page routes redirect to Migration Centre and their posting APIs retire once a central project exists.

## Financial Period Warning

A warning appears at the top of authenticated pages when today is not in an open financial period. Transaction saves use the selected document date, not merely today's date. Contact an authorized finance user; do not change a document date only to bypass control.

## Paying a Supplier Bill

1. Open **Purchasing > Supplier Bills**.
2. Open a posted bill with an outstanding balance.
3. Select **Pay Bill**.
4. The system opens `/Treasury/Payments`, selects the supplier, loads unpaid bills, and allocates only the selected bill.
5. Choose Cash, Transfer, Own Cheque, or Endorsed Customer Cheque as applicable.
6. Check the payment amount and allocation, then post.

## Printing Documents

Sales Orders, Sales Invoices, Credit Notes, Purchase Orders, GRNs, Supplier Bills, and Purchase Debit Notes provide separate **Print** and **PDF** actions. The final file is generated from current server data. Preview permits only party name, address, shipping address, and notes changes. Finalized files appear in **Generated History** with a hash.

## Job-Work Production

1. Open **Inventory & Mfg > Production Orders** (`/Inventory/ProductionOrders`).
2. Select **New Production Order**.
3. Enter date, contractor, approved BOM revision, target whole quantity, warehouses, dates, and notes.
4. Save the draft, review it, then select **Release**. Release freezes the BOM quantities.
5. Use **Issue Material** one or more times. The system values issues by FIFO and shows contractor-held balances.
6. Use **Return Material** for unused material returned by the contractor.
7. Use **Production Receipt** for each partial delivery. Enter whole accepted/rejected quantities, sewing charge, batch number, and actual component usage.
8. Classify component differences as Normal Waste, Abnormal Loss, or Contractor Recoverable.
9. In Supplier Bills, select **Link Sewing Accruals** when the contractor invoice arrives.
10. Settle open supplier claims using the supplier credit/debit-note reference.
11. When output, material, claims, and sewing accruals reconcile, select **Close & Reconcile**.

Old **Material Issue** and **Production Receipt** URLs redirect to Production Orders. Their historical records remain under **Legacy History**.

## Create a Bill of Materials

1. Open **Inventory & Mfg → Bill of Materials (BOM)** and select **Create BOM**.
2. Enter the BOM name, basis quantity and effective date.
3. In **Target Finished Good**, type at least two characters from the item name or SKU, then choose a finished-good item.
4. Select **Add Component**. In each material row, type at least two characters from the raw-material name or SKU, choose the item, and enter the quantity required for the basis quantity.
5. Select **Save Configuration**.

Only active items with the correct item type appear: **Finished Good** for the target and **Raw Material** for components. If an expected material does not appear, open **Item Master** and confirm it is active and its Type is **Raw Material (Non-Sellable Stock)**. Category names alone do not control this list.

## Raw Material Import

Available after deployment of the new import screen. Local endpoint checks passed; hosted and visual acceptance testing remain pending.

1. Open **Inventory & Mfg → Raw Material Import** (`/Inventory/RawMaterialImport`). Your account needs both product-management and stock-adjustment permissions.
2. Select your corrected CSV. Required headers are `ProductName,SKU,Quantity,UnitCost`. You may use **Download example CSV**, but replace its example row before uploading.
3. Review **Opening date** (defaults to your browser's current date), **Warehouse**, **Category**, and **Brand**. Defaults select `WH-MAIN` and `UNBRANDED` when available; the category defaults to `Accessories`. Unit is always `PCS`.
4. Select **Validate and Preview**. Nothing is saved at this stage. All other CSV columns, including its date and category, are ignored in favor of the screen settings.
5. Review every row, total quantity, opening value, and account mappings. Correct any errors in the CSV or setup and preview again. Quantities must be positive whole numbers. Unit costs must be positive and have at most two decimal places. Values of 1 remain valid, but are highlighted in a warning.
6. Tick **I confirm the quantities, unit costs (including values of 1), date and account mappings above. Post this opening stock.**
7. Select **Import and Post Opening Stock** once. Keep the page open until a result appears. Record the `OBSTK` reference and totals.
8. Select **View Opening Stock** and reconcile the posted entry. Check the items in **Item Master** and quantities/value in **Stock Valuation**.

Each SKU becomes a separate **Raw Material (Non-Sellable Stock)** item. Matching names are not merged. Existing SKUs block the entire import; this screen does not update existing catalog items or add stock to them. No selling price or sale is posted.

If the connection is interrupted, check **Opening Stock** before retrying. Preview again; existing SKUs prevent posting the same file twice. Do not change SKUs just to bypass this protection.

## Collecting migrated customer balances

Precondition: an administrator has posted the opening migration. In **Treasury → Customer Receipts** (`/Treasury/Receipts`), choose the customer. The allocation table includes new invoices, legacy opening debts and debit memos, with their reference and due date. Enter the payment amount and allocation amounts, or use **Auto-Apply**, check the allocation and save through the existing receipt workflow. Amounts cannot exceed available debt, belong to another customer, or predate the opening cutover or later settlements.

For a goods-return opening credit, enter the amounts against eligible debts, choose **Apply return credit [reference]**, then confirm **Yes, Apply Credit**. The selected receipt date becomes the application date. This uses existing credit without recording new cash or reducing AR a second time. Application history appears below the credit banner. An authorized user may choose **Reverse Application**, enter a reason, and use the selected date; a closed period or date before application is rejected.

Opening cheques appear in the existing cheque vault with an opening-origin badge, original receipt reference and legacy references. Follow existing deposit, endorsement, bank reconciliation and bounce steps. Do not enter a second receipt for a cheque already imported. A bounced opening cheque generates debt for its full amount, due immediately, which can be collected through Receipts.

Use **Customer Statement** (`/Reporting/CustomerStatement`) to see opening items and subsequent activity. Use **AR Aging** (`/Reporting/ArAging`) and its **As of date** to see debt buckets, **Gross Debts**, **Unapplied Credits** and net outstanding. Opening debt ages from its original date plus 30 days, while its ledger posting starts at cutover. Applying return credit reduces the debt bucket and available credit equally, leaving net AR unchanged.

## Working while opening migration is in progress

Administrators manage closing uploads in **Migration Centre**, `/Migration`. The earlier separate opening-import instructions are historical. Normal users continue through the existing Receipts, Payments, inventory and reporting pages after their relevant scopes are released.

A **provisional** notice means the opening position is not yet finally signed off. A pending customer, supplier, SKU or bank may be blocked when saving a financial/stock transaction. Ask the administrator to reconcile and release that scope; do not recreate an old invoice, payment or stock receipt to work around the message. Drafts/master maintenance remain available. Financial dates must be on or after actual go-live, including after final sign-off.

1. To collect a migrated customer debt, open **Customer Receipts**, `/Treasury/Receipts`, choose the customer and allocate against the opening reference. Use the existing return-credit action for negative opening credits; it does not create another receipt.
2. To settle a supplier opening bill, open **Payments**, `/Treasury/Payments`, select the supplier and the opening debt. For an available opening credit choose **Apply Opening Credit**, enter the target/amount/date and save. This uses credit already in the opening position.
3. Cheque managers can review **Opening uncleared bank items** on `/Finance/BankReconciliation` and `/Treasury/Payments`. Use **Mark Cleared** only on/after the cheque face date. **Cancel Issued Cheque** requires a date/reason and restores supplier debt. Use the existing cheque dishonour workflow for received cheques.
4. Use Customer/Supplier Statements and AR/AP ageing to review opening references plus subsequent transactions. Do not enter duplicate historic invoices or supplier bills.
5. A **Details incomplete** badge on Customers/Suppliers identifies genuine missing contact information. An authorized master-data user completes it later using normal maintenance validation.

Administrator instructions: [Migration Centre operating guide](../migration/Migration_Centre_Runbook.md). Final closing balances and actual role access remain subject to hosted UAT and business sign-off.

## Dashboard after opening migration

After loading the dashboard correction, Executive Dashboard includes migrated customer/supplier opening balances, their credits and subsequent dated transactions. Compare A/R with AR Aging and A/P with AP Aging. Overdue receivables use unsettled debts that were due before today; unapplied credits remain separate until applied. Cash & Bank excludes inventory, receivables and cheques in hand. An Unavailable cash total means the finance administrator must review its account grouping/mapping.

A finalized migration remains the read-only opening reference. Continue ordinary receipts, payments, stock and cheque workflows without importing those openings again. See [the verified UAT baseline and next-step checklist](../migration/Post_Finalization_Check_20261015.md). Revenue and the sales/purchases chart require new invoices/bills; opening documents do not recreate sales history.