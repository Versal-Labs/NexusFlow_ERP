# Upload 06 — Control Totals

File: stage_packages/06_Control_Totals.zip

Select only: **Control Totals**.

Expected: 5,212 control rows; 2,050 opening scopes.

Upload 06_Control_Totals.zip. Select Control Totals only and use the import cycle. Validation alone is insufficient: the controls must be approved and executed.
After completion, uncheck all modules and select Customer Master, Supplier Master, Stock and Bank Opening. Leave all four code filters blank.
In Signed independent schedule evidence enter a truthful pilot review reference, for example: Pilot 2026-10-15; prepared controls checked against the supplied pilot schedules and reconciliation report by [reviewer/date]. Functional test only.
Select Release Selected Reconciled Scopes. Refresh Progress. If some scopes were already released, the newly released count may be less than 2,050; the final scope list must contain all 2,050 as Released and none Pending.
Resolve any pending reason before continuing. The controls are an acceptance baseline reproduced from the test schedules, not independent certification of the original legacy closing reports.

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
