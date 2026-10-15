# Local development migration reset — 8 October 2026

Completed for the user-confirmed **local development database**, `NexusERPLocal`, on local SQL Server `SAKEELPC`. The hosted UAT system was not reset.

The user confirmed removal of **all business transactions and opening balances**, including cheques, stock quantities/layers/movements, while preserving customers, suppliers, account definitions, mappings, settings and inventory masters. This superseded the earlier ambiguous instruction to delete only journals.

## Execution and evidence

1. Resolved the application's existing protected local connection without displaying or exporting its credentials. Checked both configured SQL target and actual SQL machine identity. No other database sessions were connected.
2. Created a unique native **COPY_ONLY** database backup with **CHECKSUM**, ran **RESTORE VERIFYONLY WITH CHECKSUM**, and retained a second workspace copy with the same SHA-256.
3. Reviewed actual table definitions and foreign-key links. Used an explicit transaction-table allowlist and child-before-parent deletion order. Foreign keys/check constraints were not disabled, identities were not reseeded, and document numbering was not reset.
4. Rehearsed the complete reset in a serializable transaction, verified preservation and clean-opening conditions, rolled back, and verified that the original position returned.
5. Rechecked that the original position matched the rehearsal, then committed the authorized reset in one transaction. For transaction tables with temporal history, temporarily disabled versioning, cleared current/history rows, and restored the original history-table links and retention settings inside the transaction.
6. Verified the committed result through a fresh read-only database connection. All **117 cleared current/history tables** were empty; all **77 retained tables or settings groups** matched their pre-reset row counts and SHA-256 fingerprints. Account comparisons excluded only their deliberately reset balance and system-managed temporal timestamps.

Account-definition temporal history was retained. Resetting current nonzero balances added its normal history entries. **Nine pre-existing temporal diagnostics in account history remained unchanged**; no new diagnostics were introduced, and foreign-key and non-temporal check validations passed. Those pre-existing history diagnostics were not repaired as part of this reset.

## Verified current position

| Cleared item | Before | After |
|---|---:|---:|
| Journal entries | 31 | 0 |
| Journal lines | 85 | 0 |
| Sales invoices | 5 | 0 |
| Cheque register | 2 | 0 |
| Accounts with nonzero balances | 11 | 0 |
| Stock layers | 1,068 | 0 |
| Stock transactions | 480 | 0 |

Related sales/purchase orders, goods receipts, supplier bills, credit notes, receipts/payments, allocations, reconciliations, production records, payroll/attendance transactions and old opening/migration records were also cleared. Old queued/completed job records and their console data were cleared while recurring job definitions were retained. Transaction-generated document metadata was cleared; physical storage files were not deleted.

| Selected retained masters/settings | Unchanged rows |
|---|---:|
| Customers | 2 |
| Suppliers | 4 |
| Account definitions | 55 |
| Products | 795 |
| Product variants / SKUs | 1,055 |
| BOM definitions | 2 |
| System configurations | 40 |
| System lookups | 24 |
| Number sequences | 24 |

The preservation checks also covered brands, categories, units, warehouses, bank/branch dictionaries, financial periods, account/category/customer/supplier mappings, employees and payroll configuration, company/installation data, users/roles/permissions, master history and system audit records. There was no saved Migration Centre project to retain; its project table was already empty before reset.

## Recovery artifacts

Full pre-reset backup: [workspace backup copy](../../artifacts/local-migration-reset-20261008/NexusERPLocal_pre_migration_reset_20261008-220134_9034a68d.bak).

- SQL Server copy: `C:\Program Files\Microsoft SQL Server\MSSQL16.MSSQLSERVER\MSSQL\Backup\NexusERPLocal_pre_migration_reset_20261008-220134_9034a68d.bak`.
- Size: **30,531,584 bytes**.
- SHA-256: `A0C95CA00D90E7E921D4B8C631640A89E95FDDF8F31E0072CBBDA062D14AA5A5`.
- [Backup manifest](../../artifacts/local-migration-reset-20261008/backup-manifest.json).
- [Rollback rehearsal report](../../artifacts/local-migration-reset-20261008/rehearsal-report.json).
- [Committed reset report](../../artifacts/local-migration-reset-20261008/reset-report.json).
- [Fresh-connection verification](../../artifacts/local-migration-reset-20261008/post-reset-verification.json).

The backup contains the complete previous database, including credentials/identity records and private business data; it is a local recovery artifact, not a source-controlled deliverable. Operational helpers and raw evidence are retained in the ignored `artifacts/local-migration-reset-20261008/` directory. No application source code was modified for this operation.

## Next step

Restart the local application, open **Administration → Settings & Security → Migration Centre**, select **New Project**, enter the agreed source identifier and actual opening date, and select **Create Project**. The current database satisfies the clean-journal/account-balance prerequisites. Project creation was not submitted because the final closing/opening dates remain a business decision.

The Excel balances have not been imported by this reset. Prepare and reconcile them using [the file preparation guide](Excel_Upload_Preparation_20261008.md) and [Migration Centre operating guide](Migration_Centre_Runbook.md), including one combined Stock dataset. Financial posting still requires the corrected common-date source position and its independent controls.
