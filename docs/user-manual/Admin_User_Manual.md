# NexusFlow ERP Admin User Manual

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
