# Migration performance fix — 15 October 2026

The release delay came from repeated work in the application. The MARS message was a transaction warning, not evidence of a timeout or failed import.

## Verified local UAT position

Read-only inspection of `SAKEELPC / NexusERPLocal`, project **4 — NexusFlow UAT 2026-10-12**:

| Scope | Released | Pending |
| --- | ---: | ---: |
| Customer | 896 | 0 |
| Supplier | 215 | 0 |
| Stock | 483 | 455 |
| Bank | 0 | 1 |
| Total | 1,594 | 456 |

All six posting runs were Completed. All 938 stock units were already imported. Stock import run 5 took about 2 hours 58 minutes; the later release operation committed 1,594 scopes before it stopped. Release commits each scope separately, so the completed releases remain saved.

Project version remained **1608**, with **2,454 migration units**, **8,682 origins**, and **1,333 journal entries** after the diagnostic work. No imports, releases, resets, connection-string changes, or financial writes were executed against this database.

The bank's three reconciliation components (BankOpening, BankStatementBridge, BankRecon) each had zero difference. Read-only validation of BankOpening and BankRecon returned no errors. The pending stock scopes still require their normal release checks.

## Why it was slow

- `ReleaseAsync` loaded every current migration dataset for each individual scope. Those source payloads totalled **14,797,356 bytes**, including **6,582,188 bytes** of ControlTotals data that individual stock/party checks did not need.
- `ValidateAsync` repeatedly loaded and parsed unrelated datasets, fetched full posted-unit JSON, and scanned imported identities while recalculating hashes inside those scans.
- `ExecuteRunAsync` revalidated the entire selected import before every atomic party/SKU group. For a stock file with 938 SKUs, this repeatedly validated the other SKUs too.
- Bulk release is a synchronous web request that loops through scopes. Increasing Hangfire worker count would not accelerate that release request.

The 14 October log contained 6,380 MARS savepoint warnings. The same log and release audit show continuing progress. The warning explains that EF cannot create an automatic savepoint when MARS is enabled. It does not itself mean a SaveChanges call failed. The fix leaves the connection string and warning policy unchanged; the warning may still appear.

## Code changes

- Validation loads the selected modules and their required dependencies. Bank reconciliation still checks received/issued cheque files for duplicate references; party openings retain access to pending master data.
- Imported identities use a dictionary lookup. Source/content comparisons and duplicate-posting checks remain in place.
- Posted-unit payloads are fetched only when trial-balance reconciliation needs them. Ordinary scope checks read the needed metadata.
- Release loads only its required financial datasets, without tracking those large source payloads, and uses an existence query for the posted-unit check.
- Posting revalidates the current atomic group against fresh database state and its original approved unit hash.

Serializable transactions, project locks, source hashes, current master/account validation, independent totals and row-count matching, per-scope commits, and the audit trail remain in place. No schema migration is required.

## Verification

The same SELECT-only probe ran before and after the change against the unchanged UAT snapshot. It rejects non-SELECT SQL. These are individual validation timings, not a timing of a complete release:

| Validation | Before | After | Improvement |
| --- | ---: | ---: | ---: |
| One stock scope | 14.856 s | 3.280 s | 4.53 times |
| One customer opening scope | 14.043 s | 1.677 s | 8.37 times |
| Complete ControlTotals schedule | 41.318 s | 4.913 s | 8.41 times |

All three preview hashes and unit hashes matched the original results exactly; both versions returned zero validation errors. A later repeated stock check took 1.002 seconds after startup/query compilation. Timings depend on cache, SQL load, and machine load; they are not a guaranteed whole-release duration.

**23 migration tests passed, none failed or skipped**, using GUID-named disposable MSSQLLocalDB databases. Coverage includes unchanged posted hashes, stock warehouse/FIFO reconciliation, negative customer credits, supplier balances, rollback/resume, duplicate delivery, mismatched independent controls, and final ledger reconciliation. New regression cases verify:

1. BankRecon still rejects references in unselected received/issued cheque files.
2. New customer masters and their openings can post together with original codes, approved amounts, and matching scoped hashes.
3. Re-running bulk release after partial completion skips released scopes and creates no duplicate releases, opening items, or journals.

The updated web application also built successfully with zero errors. Build warnings already present in the repository remain outside this performance fix.

The real application was stopped during verification, so the continuation click was not executed in its browser. Existing route/button labels were checked against the source. No elapsed-time claim is made for a new complete release.

## Continue the existing UAT project

1. Build and start the updated application from this checkout.
2. Open **Migration Centre** and select **NexusFlow UAT 2026-10-12**.
3. In **3. Select this import**, select only **Control Totals** and choose **Validate Selected Import**. Confirm **Already imported** with no errors. Its data is already posted; another execution is unnecessary.
4. Select **Customer Master**, **Supplier Master**, **Stock**, and **Bank Opening**. Leave all code filters blank and Trial Balance unchecked.
5. Under **Release multiple reconciled scopes**, retain the UAT evidence in **Signed independent schedule evidence**, then click **Release Selected Reconciled Scopes** once.
6. The operation selects Pending scopes, so the 1,594 existing releases are skipped. It should attempt the remaining **455 Stock + 1 Bank** scopes. Read and resolve any returned pending reasons; successful controls are still required.
7. Refresh the project and confirm **2,050 Released / 0 Pending** before following the existing trial-balance and final-sign-off guide. This is a UAT fixture; production closing-date/evidence requirements still apply.

Do not reset the project or re-upload/re-import already posted stock to continue release. Release changes the trading lock after reconciliation; it does not import the stock a second time.