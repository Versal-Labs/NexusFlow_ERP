# Upload 01 — Masters

File: stage_packages/01_Masters.zip

Select only: **Accounts, Customer Master, Supplier Master**.

Expected: 39 accounts; 896 customers; 215 suppliers.

Complete the setup and mappings guide first. Upload 01_Masters.zip, select only Accounts, Customer Master and Supplier Master, then use the import cycle below.
Review NET30 on party records, TRADE/DOM groups and STANDARD TIER. Account rows link the exact active posting accounts; they do not create account folders.
The four customer and two supplier UAT fixture names are retained. Blank contact details remain flagged for later completion.
Expected: all selected master units imported; no opening financial journal yet.

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
