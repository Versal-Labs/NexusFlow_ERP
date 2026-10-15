# NexusFlow UAT upload package

Prepared 12 October 2026. LKR. Opening 2026-10-01, test closing 2026-09-30. These are final inputs for functional UAT. The original production review and archive are preserved. Test dates, missing-master fixtures, branch substitutes and synthetic trial balance are documented in the change log. This package is not certified production opening data.

## All files and import order

| Stage | Dataset | Rows | CSV | Excel |
|---|---|---|---|---|
| 1 — Masters | Accounts | 39 | [CSV](upload_csv/Accounts.csv) | [Excel](review_workbooks/Accounts.xlsx) |
| 1 — Masters | CustomerMaster | 896 | [CSV](upload_csv/CustomerMaster.csv) | [Excel](review_workbooks/CustomerMaster.xlsx) |
| 1 — Masters | SupplierMaster | 215 | [CSV](upload_csv/SupplierMaster.csv) | [Excel](review_workbooks/SupplierMaster.xlsx) |
| 2 — Openings and controls | CustomerOpenings | 1150 | [CSV](upload_csv/CustomerOpenings.csv) | [Excel](review_workbooks/CustomerOpenings.xlsx) |
| 2 — Openings and controls | SupplierOpenings | 180 | [CSV](upload_csv/SupplierOpenings.csv) | [Excel](review_workbooks/SupplierOpenings.xlsx) |
| 2 — Openings and controls | ReceivedCheques | 49 | [CSV](upload_csv/ReceivedCheques.csv) | [Excel](review_workbooks/ReceivedCheques.xlsx) |
| 2 — Openings and controls | IssuedCheques | 1 | [CSV](upload_csv/IssuedCheques.csv) | [Excel](review_workbooks/IssuedCheques.xlsx) |
| 2 — Openings and controls | Stock | 938 | [CSV](upload_csv/Stock.csv) | [Excel](review_workbooks/Stock.xlsx) |
| 2 — Openings and controls | BankOpening | 1 | [CSV](upload_csv/BankOpening.csv) | [Excel](review_workbooks/BankOpening.xlsx) |
| 2 — Openings and controls | BankRecon | 1 | [CSV](upload_csv/BankRecon.csv) | [Excel](review_workbooks/BankRecon.xlsx) |
| 2 — Openings and controls | ControlTotals | 5212 | [CSV](upload_csv/ControlTotals.csv) | [Excel](review_workbooks/ControlTotals.xlsx) |
| 3 — Test TB | TrialBalance | 39 | [CSV](upload_csv/TrialBalance.csv) | [Excel](review_workbooks/TrialBalance.xlsx) |

Use the CSV or matching Excel once per module. All twelve CSVs have exact v1.0 template headers, UTF-8 encoding and final values. Codes/cheque numbers are text. Dates yyyy-MM-dd. Amounts numeric LKR. Stock is one complete dataset for both FG and RM; another Stock upload replaces the whole revision.

## Start here

1. [Project and staging](GUIDE_01_Project_and_Staging.md)
2. [Exact mappings and defaults](GUIDE_02_Mappings_and_Defaults.md)
3. [Validate and import masters](GUIDE_03_Masters.md)
4. [Opening documents, cheques, stock and banks](GUIDE_04_Openings_Stock_and_Banks.md)
5. [Import controls, reconcile and release](GUIDE_05_Reconciliation_and_Release.md)
6. [Full test TB, ledger check and finalization](GUIDE_06_Trial_Balance_and_Finalization.md)
7. [Operational UAT and pass/fail checklist](GUIDE_07_Operational_UAT_Tests.md)

[Complete guide in one file](UAT_Complete_Guide.md). [UAT review workbook](review_workbooks/UAT_Review.xlsx). [Intentional changes and reconciliation](UAT_Changes_and_Reconciliation.md). [File verification](Verification_Report.md).

Bulk staging ZIPs: [01_Masters](stage_packages/01_Masters.zip), [02_Openings_and_Controls](stage_packages/02_Openings_and_Controls.zip), [03_TrialBalance](stage_packages/03_TrialBalance.zip). Their manifests target local project SourceSystem TESTTTT. For a different source identifier, individual CSV uploads are easiest; otherwise change each manifest to the exact saved project identifier. The complete download ZIP with guides and workbooks is a delivery bundle, not a Migration Centre upload package.

Local masters were checked SELECT-only at 2026-10-12T15:52:41 UTC. Project test / ID 3 is Preparing; opening 1 October and closing 30 September. No migration units/origins, operational invoices/bills/payments, stock transactions or layers were present. All 896 existing staged customer scopes are covered. Hosted browser access returned ERR_BLOCKED_BY_CLIENT, so authenticated deployed UI and configuration remain unverified. Button names and workflow rules were checked in current source. Validate in your actual UAT destination before execution.

The original source files and previous package were not changed. A read-only originals copy is included under source_archive. Existing production review: [preparation_20261011](../preparation_20261011/README.md). No import, approval, posting, database reset, finalization or application-code change was executed during this preparation.

[Complete source-column mapping](Source_to_Template_Mapping.md). [Source/UAT row reconciliation](reports/Source_to_UAT_reconciliation.csv). [UAT exceptions](reports/UAT_Exceptions.csv). [Preserved source evidence](reports/legacy_reference/README.md). Missing-contact warnings remain; destination validation is the execution gate.
