# NexusFlow migration preparation package

Opening: **1 October 2026**. Required closing: **30 September 2026**. Prepared: 11 October 2026. Currency: LKR.

Start with [Migration review workbook](review_workbooks/Migration_Review.xlsx), [readiness and exceptions](Exceptions_Report.md), [reconciliation](Reconciliation_Report.md), [mapping guide](Mapping_Guide.md), and [upload instructions](Upload_Instructions.md).

| Module | Rows | Ready to upload | Ready to post | CSV folder |
|---|---|---|---|---|
| CustomerMaster | 892 | Yes, staging only | No: mapping validation not executed | staging_csv |
| SupplierMaster | 213 | Yes, staging only | No: supplier expense assignment unresolved | staging_csv |
| CustomerOpenings | 1150 | No: HOLD for refreshed closing export | No: stale/uncertified closing balances and unreconciled controls | held_csv |
| SupplierOpenings | 180 | No: HOLD for refreshed closing export | No: stale/uncertified closing balances and unreconciled controls | held_csv |
| ReceivedCheques | 49 | No: HOLD for refreshed closing export | No: stale/uncertified closing balances and unreconciled controls | held_csv |
| Stock | 938 | No: HOLD for refreshed closing export | No: stale/uncertified closing balances and unreconciled controls | held_csv |
| TrialBalance | 188 | No: HOLD for refreshed closing export | No: stale/uncertified closing balances and unreconciled controls | held_csv |

review_workbooks contains seven populated exact-template workbooks and a comprehensive review workbook. staging_csv contains the two master files. held_csv contains five financial datasets with actual source dates/unknown date blanks and all problematic rows retained. reports contains searchable CSV versions of review sheets; those extra-column reports are not upload templates. archive contains a values-only view of every supplied file, byte-identical originals in ZIP, and SHA256/source inventory. Only original source archive files are made read-only; originals at their supplied paths are untouched.

All original source columns are retained in the archive. Unsupported fields are not imported under invented meanings. All CSVs are UTF-8, dates are ISO and numeric values use decimal points. Excel values are final, without formulas. No application source changes, database resets, imports or external uploads were performed.

Master files are structurally ready for staging, not approved for posting. Financial files are held for certified refreshed 30 September exports, independent controls and the recorded exceptions. The supplied snapshots cannot become closing-ready by changing their labels. Supplier appropriate expense assignment and new FG base units/classifications still require business confirmation. The read-only archive preserves these supplied snapshots, but full pre-cutover history must be exported separately.

## Download and individual files

The NexusFlow_Migration_Review_20261011.zip file is a review/download bundle. It is not a Migration Centre bulk-upload package. Extract it and use only the correct dataset CSV after resolving its readiness gates.

| Dataset | Review workbook | Matching UTF-8 CSV | Status |
|---|---|---|---|
| CustomerMaster | [Excel](review_workbooks/CustomerMaster.xlsx) | [CSV](staging_csv/CustomerMaster.csv) | Staging only |
| SupplierMaster | [Excel](review_workbooks/SupplierMaster.xlsx) | [CSV](staging_csv/SupplierMaster.csv) | Staging only |
| CustomerOpenings | [Excel](review_workbooks/CustomerOpenings.xlsx) | [CSV](held_csv/CustomerOpenings.csv) | HOLD |
| SupplierOpenings | [Excel](review_workbooks/SupplierOpenings.xlsx) | [CSV](held_csv/SupplierOpenings.csv) | HOLD |
| ReceivedCheques | [Excel](review_workbooks/ReceivedCheques.xlsx) | [CSV](held_csv/ReceivedCheques.csv) | HOLD |
| Stock | [Excel](review_workbooks/Stock.xlsx) | [CSV](held_csv/Stock.csv) | HOLD |
| TrialBalance | [Excel](review_workbooks/TrialBalance.xlsx) | [CSV](held_csv/TrialBalance.csv) | HOLD |

See [file verification](Verification_Report.md) and [application verification](Source_Verification.md). The read-only [archive workbook](archive/Legacy_Archive.xlsx) and [original-files ZIP](archive/Legacy_Originals.zip) preserve the supplied source evidence. Every source file and row is accounted for in the review workbook.
