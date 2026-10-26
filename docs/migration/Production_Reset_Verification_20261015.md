# Production reset SQL verification — 15 October 2026

The production target was not connected or modified by this test. The user supplied the production audit and classified the 12 September journals and one payment as disposable test data. The SQL is an operator-run, source-reviewed artifact, not evidence of a completed Azure cleanup.

Test environment: a uniquely named disposable SQL LocalDB database created from the current EF model, with the repository's integration-test fixture. The private test harness substitutes only that temporary target into the script and supplies test control values. It seeds the reviewed module counts (3 Inventory, 6 Production, 1 Purchasing, 1 Sales, 1 Treasury), 12 balanced journals/24 lines, one receipt with an allocation to a sales invoice, a supplier bill, stock layer/movement and retained customer/product/settings masters. It deletes its own temporary database after testing. Neither the actual local working database nor Azure production is reset.

| Check | Result |
|---|---|
| Unmodified production target guards reject execution on the temporary database | Pass |
| Missing recovery reference prevents rehearsal | Pass |
| PREVIEW leaves current business records unchanged | Pass |
| REHEARSE clears dependent rows, validates retained fingerprints and restores original rows by rollback | Pass |
| Incorrect/stale rehearsal hash rejects COMMIT and preserves original rows | Pass |
| COMMIT clears current transaction/balance positions; settings, customers, SKUs and temporal history remain | Pass |
| Repeating the reset after cleanup rejects the changed 12/1 baseline | Pass |

The retained-table comparison uses row counts and SHA-256 fingerprints, excluding only the deliberate account Balance/system-period/rowversion changes. All foreign keys remain enabled; deletion order follows actual foreign keys. Histories expected to grow from normal temporal writes are retained and excluded from equality comparison. Empty JSON sets are normalized for fingerprinting. The script acquires its reset lock and the application's migration-project creation lock. It contains no credentials and defaults to PREVIEW.

This validates the current local model and representative linked data, not the unknown complete Azure data/schema position. The operator must review PREVIEW, confirm a completed recovery copy, stop all writers, pass rehearsal against the actual target and use its returned hash for COMMIT. Unsupported schemas, retained dependencies, pending jobs and unexplained account balances intentionally stop execution.

Evidence: `artifacts/production-reset-20261015/test-results.txt` and the disposable test harness in the same ignored directory. Build emitted existing NU1902/NU1903 advisories for System.Security.Cryptography.Pkcs 9.0.13; no application/dependency source changes were made for this operation.
Verified SQL SHA-256: 319390F51411E8BEBCF951821E974BBDAE4E37B550E91EDEB79CA8E6C5BC8209
