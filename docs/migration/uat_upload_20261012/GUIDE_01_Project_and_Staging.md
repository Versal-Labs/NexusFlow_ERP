# Stage 1 — project and staging

Operator needs Permissions.Migration.View and Permissions.Migration.Prepare. Use an isolated UAT company with the required account and lookup codes. Current local saved project: test, ID 3, SourceSystem TESTTTT, Preparing. Its older staged customer revisions will be replaced by these complete UAT inputs.

1. Sign in and open Administration → Settings & Security → Migration Centre (/Migration).
2. Choose test under Saved migration project. Verify opening 2026-10-01 and closing 2026-09-30. Upload Dataset uses the saved project closing automatically; there is no separate closing-date field on this page.
3. For a fresh UAT company without a project, select New Project. Enter Project name NexusFlow UAT 2026-10-12, Stable source system identifier TESTTTT and Actual go-live opening date 2026-10-01, then Create Project. The importer permits one company project. A Finalized project is read-only; use a separately provisioned UAT company for a new cycle. This package does not reset a database.
4. Select Download Template Pack if the destination version differs and compare the headers. Confirm the Stage 2 master targets exist.
5. Stage stage_packages/01_Masters.zip through Or upload a ZIP package with manifest.json → choose ZIP → Upload Package. Alternatively select Dataset and File, then Upload Dataset for Accounts.csv, CustomerMaster.csv and SupplierMaster.csv individually.
6. Use Stage 2 to save mappings, then Stage 3 to import masters. Stage 4 uses 02_Openings_and_Controls.zip. Stage 6 uses 03_TrialBalance.zip only after all scopes are released.
7. Uploading stages rows and changes no ledger balances. Do not upload the full delivery ZIP, report CSVs or UAT_Review.xlsx as a dataset. Do not upload both CSV and Excel as two batches.

Expected: Accounts 39, Customer Master 896, Supplier Master 215 staged. Ledger unchanged. Included modules become Provided on upload. Trial Balance remains Awaiting data until Stage 6. All financial modules are supplied in this UAT case; no empty-file or NotApplicable declaration is needed. The ZIP SourceSystem must equal the saved project identifier. If it differs, use the individual CSVs.
