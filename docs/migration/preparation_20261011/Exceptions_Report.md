# Exceptions and import readiness

Opening date confirmed as 1 October 2026; required legacy closing is 30 September. The files are populated for review. Financial CSVs are held because supplied snapshots are stale or uncertified; none is ready to post. Both master CSVs are structurally ready to stage. CustomerMaster needs saved mappings and destination validation. SupplierMaster additionally needs appropriate supplier Expense posting accounts.

## Outstanding gates

| Module | Field | Observed value | Issue | Required action |
|---|---|---|---|---|
| CustomerOpenings | ClosingDate | 2026-09-28 | Actual ageing snapshot is two days before approved closing | Request complete 30 September open-item export, retaining zero-net offsets. |
| ReceivedCheques | ClosingDate | 2026-09-28 | Cheque report is two days before approved closing | Refresh full physical/ledger cheque schedule at 30 September; user status confirmation does not refresh amount snapshot. |
| Stock | ClosingDate | Raw 2026-09-13 / FG unknown | Combined file is not a certified 30 September stock position | Refresh both datasets and warehouse counts/costs. One complete combined replacement revision only. |
| SupplierOpenings | ClosingDate | Inferred 2026-10-07 | Uniform ageing identifies 7 October, and post-closing documents are present | Request a complete 30 September outstanding export; do not merely remove later documents because earlier items may have settled during October. |
| TrialBalance | ClosingDate |  | Source has no certified snapshot date | Request full corrected 30 September TB, including every zero account. |
| TrialBalance | Debit,Credit | Credits exceed debits by 613120.00 | Source TB is unbalanced | Accountant must correct/explain source. Do not create an opening-equity or suspense plug. |
| All financial | October activity |  | Transactions since 1 October exist only in the old system | Transfer every October transaction separately under controlled normal posting after opening scopes release; the Migration Centre cannot replay full history. |
| Archive | Historical transactions |  | Supplied files are snapshots and masters, not a complete pre-cutover transaction history | Retain full old-system backup and transaction/document exports through 30 September in a controlled read-only archive. |
| Financial dependencies | Missing files | BankOpening, BankRecon, IssuedCheques, ControlTotals | These schedules were not supplied | Obtain full closing schedules and independent controls or signed true zero declarations. Do not treat missing files as zero. |
| Accounts | Mapping | 188 legacy accounts / 39 active transaction accounts | A complete approved legacy-to-current account mapping/allocation is absent | Create missing genuine accounts in normal maintenance or approve complete mappings and inventory split. Cover every target active posting account. |
| ReceivedCheques | SafeAccountCode | 1151 | Cheques In Hand and SalaryAdvancesReceivable are configured to the same account | Confirm all balances included under 1151 and reconcile independent safe asset balance. |
| CustomerMaster | PaymentTermCode | COD saved in project 3 | Current saved payment-term mapping is cash on delivery, not confirmed 30-day credit | Add NET30 → 30 Days mapping; use prepared NET30 rows. Do not preserve COD as the master term. |

Other row-level gates: four missing customer codes, two missing supplier codes, 81 unmatched FG SKUs with missing unit/brand and unresolved category codes, and actual product type/unit/category-account conflicts listed in reports/Exceptions.csv. Every problem row remains in its template workbook/CSV. Source rows after closing are retained in the held supplier file; removing only those rows cannot recreate an older 30 September balance snapshot.

Supplier due dates are document+30, confirmed by the user. Original paydate differences, including LH109 / GR00001448 (original 19 March before document 20 March), remain visible in Supplier date changes. Negative supplier rows are Credit returns and positive ADVANCE/OVER PAID rows stay positive by explicit instruction. BA366's invalid email was explicitly declared unknown and replaced with a blank. Genuine missing contact/tax details are permitted and flagged for later maintenance.

## Exception states

| State | Rows |
|---|---|
| Blocks posting | 14 |
| Blocks validation | 314 |
| Later completion | 1105 |
| Required action | 2 |
| Resolved | 326 |
| Review | 39 |

Counts are issue records, not distinct source rows. Later completion and resolved records are retained for traceability. Blocks validation and Blocks posting require action before execution. The audit workbook and reports/Exceptions.csv contain exact source row, code, reference, value, reason and action. Final source/output comparison found no duplicate supported identities. Do not manufacture LegacyRecordId from row numbers to bypass a future duplicate.

The user authorized all supplier groups to DOM and requested appropriate expense accounts. Current existing supplier defaults include the ineligible 5000 folder and specialist 5110/5120 accounts. Their applicability to each legacy supplier cannot be established from supplied data. RE187/RS149 explicitly identify rent and use 6210 Rent Expense. EL264 explicitly identifies electricity and uses 6220 Electricity & Utilities. The remaining 210 assignments are **Needs business confirmation**; generic trading/garment supplier names do not establish expense treatment.

## Refreshed exports required

Obtain complete certified 30 September customer unsettled documents (including offsetting debt/credit items), supplier unsettled documents, physical cheques with actual closing status/reflection, raw and finished stock quantities/valuations, full balanced TB including zeros, every bank book/statement/reconciliation position and issued cheques. Rebuild the complete combined Stock revision. Keep originals and report dates unchanged. Obtain independent control schedules and full historical backup/export for the read-only archive. October transactions remain a separate transfer, with reconciliation to the opening and subsequent activity.
