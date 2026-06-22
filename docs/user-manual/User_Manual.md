# NexusFlow ERP User Manual

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
