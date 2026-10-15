# Stage 5 — controls and scope release

ControlTotals contains fixed expected UAT fixture controls. It is not an independent production closing certificate. Zero invoice/cheque components cover master-only parties without fabricating financial documents.

1. Select only Control Totals. Leave filters blank. Validate Selected Import, review 5212 rows, record UAT fixture control evidence, Approve Selected Import and Execute Approved Import. Wait for completion.
2. In Select this import select Customer Master, Supplier Master, Stock and Bank Opening. Uncheck Trial Balance. Leave all code filters blank. These modules select all four scope kinds for bulk release.
3. Under Release multiple reconciled scopes enter Signed independent schedule evidence: UAT-only fixed fixture controls, package 2026-10-12. All test positions and zero components reviewed; not production independent closing evidence.
4. Select Release Selected Reconciled Scopes. Expected: 2050 scopes Released and zero Pending. Read any pending reason and compare its current source, posted rows, control amount and count with ControlTotals.csv.
5. For a manual check select Reconcile for a displayed scope. Difference must be zero. Enter control amounts and row counts from the fixture, record Complete scope and control evidence, then Release Reconciled Scope.
6. Customer/Supplier Debit and Credit components use positive magnitudes, and BOTH use the entire party document count. Cheques are separate components. Stock uses Stock:WH_MAIN:Quantity and Stock:WH_MAIN:Value. BankOpening row count is 1; BankStatementBridge count is 2 (issued cheque plus deposit); BankRecon count is 1.
7. Released entities can trade. Complete initial TB/finalization checks before ordinary transactions for a straightforward first test cycle.

Expected scopes: 896 Customer, 215 Supplier, 938 Stock, 1 Bank. A master-only customer/supplier has zero expected amounts and RowCount 0 for absent components. No unsettled legacy document was removed to create those zeros.

## Continuing the interrupted local UAT release — 15 October 2026

A source-code performance fix is available in this checkout. Build and start the updated application before continuing. Local project 4 currently has 896 customers, 215 suppliers and 483 stock scopes Released; 455 stock scopes and one bank scope remain Pending. All posting runs completed, including all 938 stock units.

Validate the existing Control Totals schedule and confirm Already imported. Select the same four modules above and click Release Selected Reconciled Scopes once with the UAT evidence. Existing Released scopes are skipped automatically; this continues the remaining scopes without importing stock again. Review any returned pending reasons before finalization.

See [the measured diagnosis, test results, and continuation instructions](../Migration_Performance_20261015.md). The diagnostic work did not execute a release or change migration data.