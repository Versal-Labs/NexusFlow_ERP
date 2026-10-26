# NexusFlow client pilot package — 15 October 2026

Use this package to replay the successfully finalized local migration for the client pilot at https://nexus.garnetnation.lk/Migration. All amounts are LKR.

**This is the user-approved pilot dataset.** It includes the same synthetic bank, issued cheque and trial balance used locally. Original exports have different snapshot dates; the pilot closing label is not certification of a real 30 September closing position. Review reports/Pilot_Assumptions.csv before accepting the pilot baseline.

## Start here

1. Extract NexusFlow_Pilot_Package_20261015.zip to a folder. This full handoff ZIP is not a Migration Centre upload.
2. Administrator: follow guides/00_Readiness.md and guides/01_Setup_and_Mappings.md.
3. Upload only the seven ZIPs inside stage_packages, in numeric order. Follow guides/02_All_Upload_Stages.md. Each stage has its own short guide as well.
4. At each stage: select the intended modules, leave code filters blank, validate, review, approve and execute; then wait for completion.
5. Import Control Totals, release all scopes, import Trial Balance, check the full ledger and finalize the pilot.
6. Use guides/03_Pilot_Run_and_Acceptance.md for client testing. Record Pass/Fail in review_workbooks/Pilot_Review.xlsx or reports/Pilot_Acceptance_Tests.csv.

## Package contents

| Folder/file | Purpose |
|---|---|
| stage_packages | Seven uploadable ZIPs, containing only manifest.json and exact UTF-8 template CSVs |
| upload_csv | Twelve alternative individual upload files; upload each module once, using ZIP or CSV |
| review_workbooks | Twelve rebuilt matching values-only template workbooks plus Pilot_Review.xlsx |
| reports | Mappings by code, 33 SKU aliases, stock setup, reconciliations, GL, exceptions and tests |
| source_archive | Unchanged original source archive and manifest |
| guides | Readiness, mappings, every upload stage, acceptance and troubleshooting |
| reports/Verification_Report.md | Actual checks performed and destination checks still required |
| Package_SHA256.csv | Integrity list for every delivered file except the checksum file itself |

## Corrections included

- Added the missing ACCESSORIES category mapping and all 33 case-sensitive existing-SKU aliases, including SDASF -> sdasf.
- Documented separate stock posting accounts: raw materials 1131; finished goods 1132. WH_MAIN must not force a single account.
- Replaced local numeric-ID references with destination master codes/names.
- Clarified that NET30 must explicitly map to a 30-day credit term; COD is not suitable.
- Split the original opening bundle into seven manageable uploads and added baseline checks, controls/release and finalization instructions.
- Preserved all twelve successful local CSVs byte-for-byte. Rebuilt their twelve matching Excel workbooks with importer-safe ISO date text after the current reader rejected the older date-cell representation. No new quantity, price, balance or identity was invented.

Prepared for destination validation. No database was cleared, application deployed, file uploaded or import executed in producing this package.
