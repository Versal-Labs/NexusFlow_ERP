# Validation and posting issues

| Symptom | Check and correction |
|---|---|
| SDASF / Accessories category errors | Add Stock SDASF -> existing sdasf. Add Category ACCESSORIES -> Accessories. Save Mappings and Evidence. Existing SKU must resolve to RawMaterial / Accessories / 1131. |
| Inventory account must equal warehouse/category posting account | Check actual SKU category and WH_MAIN override. Pilot RM account is 1131, FG is 1132; both active Asset posting accounts. Use Category_Inventory_Setup.csv. |
| Map category to an active inventory account | Category mapping does not assign its account. Correct the category's inventory posting account in master setup. Revalidate. |
| Existing SKU treated as a new product/duplicate | Add the case alias in Required_SKU_Mappings_33.csv. Verify exact target product identity. Do not create a duplicate uppercase product. |
| Composite legacy identity warning | The files use supported permanent IDs/composites. Stable_Identities.csv has no duplicates; source row numbers are trace references only. Review changed identity fields before reuploading. A warning alone is not an import error. |
| Group/term/price level error despite matching code | Explicitly map TRADE, DOM, NET30 and STANDARD TIER, then save. Defaults alone are insufficient. |
| Wrong payment term | Select an actual 30-day credit term. COD is cash on delivery. Local NET30 and NET 30 DAYS are different records representing 30 days. |
| Source-system/closing-date package mismatch | Project must use TESTTTT / opening 2026-10-01. All manifests close 2026-09-30. Do not change dates merely to bypass existing data. |
| ZIP not a migration package | Upload a ZIP from stage_packages, not the full handoff ZIP. It needs manifest.json and its CSV files at the ZIP root. |
| No pending units / Already imported | Check posted units and run status. Recognized unchanged units do not need another execution. Validation is not evidence that a newly uploaded file was executed. |
| Scope still Pending | Ensure relevant financial modules and Control Totals were executed, not just staged. Review component totals/row counts, including zeros and cheque reflection flags. Select the correct scope modules for release. |
| Full-ledger differences / active account missing | Compare Expected_GL.csv and destination active account list. Pilot TB covers 39 accounts. Resolve account coverage and posting differences; do not plug managed AR/AP/inventory balances. |
| Cannot change a finalized project | Finalization makes it read-only. Do not create a second opening to work around it. Use a reviewed new-cycle plan. |
| Blank contacts warning | Expected genuine blanks are listed in Missing_Contact_Details.csv. BA366's invalid source email remains blank. Do not invent contact details. |
| Long run / queued job / missing progress | Confirm current performance fix is deployed, background processing is alive, and the error/run log is reviewed. Refresh Progress; use Resume Run only as appropriate. Do not run direct inserts or repeatedly start concurrent jobs. |
| Dashboard AR/AP zero, or cash/bank too high | Confirm the tested dashboard calculations are deployed and review the cash/bank account hierarchy. Compare the finalized opening reports before changing any data. |

Files or mappings changed after approval require a fresh validation and approval. Preserve completed units and original evidence.
