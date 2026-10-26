# Client pilot run after the opening is finalized

## Administrator baseline check

Before users start posting, save screenshots/exports for these LKR balances:

| Check | Expected |
|---|---:|
| Customer invoice/credit net receivables | 162,114,985.14 |
| Supplier bill/credit net payables | 94,190,250.55 |
| Stock valuation | 114,417,245.74 |
| Raw-material inventory account 1131 | 29,727,743.70 |
| Finished-goods inventory account 1132 | 84,689,502.04 |
| Cheques in hand, 49 InSafe received instruments | 5,328,628.00 |
| Cash and bank dashboard total | 100,000.00 |
| Pending issued cheque | 15,000.00 |
| Bank statement | 112,500.00 |
| Pending deposit | 2,500.00 |
| Trial balance total on each side | 282,960,858.88 |

Expect every full-ledger account difference to be zero. Revenue MTD can be zero before new invoices: imported opening/TB journals are not new sales. Overdue figures depend on the dashboard date. After pilot transactions, totals should change according to those transactions; do not keep comparing a used database with an untouched baseline.

If dashboard AR/AP are zero while imported opening reports are correct, verify that the tested dashboard fix is deployed. If cash/bank includes inventory/AR/safe, check the build and account hierarchy under 1110.

## Normal users

Use the assigned business role. Migration permissions are for the administrator/operator.

1. Receive part of a customer opening Invoice using Incoming Payments (AR) / Customer Receipts at /Treasury/Receipts. Select the customer, choose the positive imported opening, allocate the actual test receipt and confirm once. Check the remaining outstanding and AR change.
2. Test an ordinary supplier payment against a positive opening Bill. Check remaining AP. Review return credits separately; do not recreate an old bill or payment to establish opening balances.
3. Deposit one imported InSafe received cheque using the normal cheque workflow. Check safe/bank movement and confirm the customer is not credited a second time.
4. Run an ordinary stock issue/sale/receipt using a released SKU. Check on-hand quantity, FIFO layer use and inventory journals.
5. Record results and evidence in Pilot_Review.xlsx, sheet Pilot tests, or reports/Pilot_Acceptance_Tests.csv. Leave unrun tests blank; use Pass/Fail only after execution.

Exact destination role assignments, menu availability and business approval of test amounts are Needs business confirmation. Full manuals are in the project docs/user-manual folder.

## Completion and transition

Retain the pilot baseline, test evidence and unchanged source archive. The original snapshots are not a complete pre-cutover transaction history. A future live migration needs refreshed agreed-date closing exports, a complete read-only legacy history archive, actual bank/issued cheque schedules and reviewed genuine TB. It also needs a plan for transactions recorded in the old system after the chosen opening.

Do not post a real opening on top of the pilot data. A separately reviewed cleanup or separate target is needed for the real cutover.
