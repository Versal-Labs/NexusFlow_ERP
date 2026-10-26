# Upload 02 — Party Openings

File: stage_packages/02_Party_Openings.zip

Select only: **Customer Openings, Supplier Openings**.

Expected: 1,150 customer documents; 180 supplier documents.

Upload 02_Party_Openings.zip and use the import cycle for Customer Openings and Supplier Openings only.
Customer Amount is creamt. Positive documents are Invoice; negatives are ReturnCredit. No debamt-minus-creamt calculation is used. All unsettled documents remain, including offsetting debts and credits on zero-net parties.
Supplier positives remain Bill, including the confirmed OVER PAID/ADVANCE rows; negative returns are Credit. Thirty-day due dates are populated. Four earlier UAT date adjustments remain disclosed in Pilot_Assumptions.csv.
Expected customer debts 162,436,582.24 less credits 321,597.10 = 162,114,985.14.
Expected supplier debts 96,073,265.55 less credits 1,883,015.00 = 94,190,250.55.
These totals exclude received cheques.

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
