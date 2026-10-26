# Before the first upload — administrator

## Preconditions

Target: nexus.garnetnation.lk / Azure SQL NexusGarnetProd on versalapps.database.windows.net. The hosting name says production; this specific handoff is the authorized client pilot replay.

1. Retain a pre-pilot recovery point. Confirm the previously discussed disposable test-data cleanup has completed and been checked. This package does not perform cleanup and does not include a destructive reset script.
2. Keep ordinary invoice, receipt, payment, cheque and stock entry paused until this baseline is finalized. Existing operational balances, stock layers or another finalized migration are a stop condition for a fresh replay.
3. Confirm the deployed app includes the tested migration performance and dashboard fixes from the current source. ZIP data does not deploy application code. The authenticated hosted UI/build was not accessible to the preparation tools; verification there remains required.
4. Confirm an open financial period covers 2026-10-01. Confirm active destination masters and the account hierarchy below.
5. Sign in with actual assigned permissions. Migration Centre requires Permissions.Migration.View. Upload/mappings/validation require Prepare; approval/release require Approve; execute/resume require Execute; finalization requires Finalize. Destination role names are Needs business confirmation; do not assume every user is an administrator.
6. Open /Migration and select Download Template Pack. Headers must agree with the supplied canonical files. A different deployed contract needs review before import.

## Clean target and master checks

An administrator can run reports/Target_Readiness_ReadOnly.sql in Azure Data Studio against the confirmed target. It is a read-only audit, not a pass certificate. Review its results; missing masters must be configured in their existing master pages.

- No existing migration project, financial journal, payment, opening item or live inventory layer for a fresh cycle. Confirm the reset result separately.
- Active posting account types: 1120 AR = Asset; 2110 AP = Liability; 4110 sales = Revenue; 5110/6210/6220 = Expense; 1131/1132 inventory, 1151 cheques and 1112 bank = Asset; 3200 Retained Earnings = Equity. Select posting leaves, not folder accounts.
- The tested cash/bank hierarchy is under 1110. Do not move inventory/AR/safe into the cash/bank group.
- Compare the complete destination active posting-account list with Accounts.csv / TrialBalance.csv. This pilot TB covers 39 accounts. Extra active accounts require reviewed complete coverage, not fabricated balances.
- Check WH_MAIN, base unit pcs, brand UNBRANDED, all used categories and the required lookup/bank-branch masters. The stock references cover 938 rows and 28 categories.
- The 33 lower-case existing SKU targets must resolve to the intended products. If a target is absent, review the product identity in Stock_Setup_Reference.csv before creating or mapping anything.

## Create the project

1. In Migration Centre select New Project.
2. Project name: NexusFlow Pilot 2026-10-15.
3. Stable source system identifier: TESTTTT (exactly; the stage manifests use this).
4. Actual go-live opening date: 2026-10-01. For this replay it is the test opening; closing is 2026-09-30.
5. Leave Existing customer batch adoption evidence blank for a clean target.
6. Select Create Project. Use this one project for all seven packages.

A Finalized project is read-only. Do not create another migration or change dates to bypass existing imported activity. If a preparing project already belongs to this same replay, verify its source/date/revisions before continuing.

The pilot is ready to post only when destination validation has no errors and the reviewer accepts the disclosed fixtures. Real financial cutover needs refreshed, common-date exports and business approval.
