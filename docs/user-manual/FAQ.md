# Frequently Asked Questions

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

## How are damages handled?

Normal waste is included in finished-goods cost. Abnormal company loss goes to the production-loss expense mapping. Contractor recoverable loss creates a supplier claim. Tax treatment of contractor claims is **Needs business confirmation**.
