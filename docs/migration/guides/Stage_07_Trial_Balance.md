# Upload 07 — Trial Balance

File: stage_packages/07_Trial_Balance.zip

Select only: **Trial Balance**.

Expected: 39 account rows; debit = credit 282,960,858.88.

Only after every opening scope is released, upload 07_Trial_Balance.zip. Select Trial Balance only and use the import cycle.
The file is the complete tested pilot closing TB. Do not manually subtract AR, AP, stock or bank balances: the importer posts the residual.
Select Check Full Ledger. Require zero Difference for every account; compare reports/Expected_GL.csv.
Confirm all source rows are imported, all opening scopes released and no failed/pending work remains.
Before ordinary pilot transactions begin, capture the baseline reports and dashboard. Local reference: 2,455 Posted units and 2,050 Released scopes for the complete unchanged package. Different destination master resolution can alter unit grouping; investigate any mismatch.
For Final financial sign-off evidence enter truthful pilot acceptance evidence, e.g. Client pilot baseline reviewed on [date] by [name]; synthetic bank/issued cheque/TB and documented test assumptions accepted for functional testing only.
Select Finalize Migration. The project becomes read-only. This action closes the pilot migration workflow; it does not certify these fixtures as live financial balances.

## Import cycle used at every stage

1. At 1. Upload closing data choose the stage ZIP under Or upload a ZIP package with manifest.json and select Upload Package. Uploading only stages data.
2. At 3. Select this import uncheck other modules; select only the modules listed for this stage. Uploading or saving mappings can reselect previously Provided modules.
3. Leave all four code filters blank.
4. Select Validate Selected Import. Check counts, signs, dates, mappings and proposed journals. Resolve every error. Genuine missing-contact and supported stable-composite warnings are disclosed and may be accepted for this pilot.
5. Enter truthful Approval evidence, e.g. Pilot 2026-10-15 / stage [number], schedules and documented fixtures reviewed by [name/date]; functional testing only.
6. Select Approve Selected Import, then Execute Approved Import.
7. Use Refresh Progress until the run is completed. Record the result. A queued run requires a working background worker. For a failed run, read the error, correct the cause and use Resume Run for its unfinished work. Do not repeatedly press Execute.
8. Any changed data, mappings or selection invalidates approval; validate and approve again before posting.

Already imported means committed units were recognized; Upload Package by itself does not mean balances are posted. Do not upload both XLSX and CSV as separate batches.
