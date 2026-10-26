# Upload 04 — Combined Stock

File: stage_packages/04_Combined_Stock.zip

Select only: **Stock**.

Expected: 938 stock layers = 494 finished goods + 444 raw materials.

Before uploading, save ACCESSORIES -> Accessories and the 33 existing-SKU aliases. See 01_Setup_and_Mappings.md.
Verify WH_MAIN has no inventory-account override for this pilot. Accessories posts to 1131; finished-goods categories post to 1132. The CSV InventoryAccountCode must match the resolved warehouse/category posting account.
Upload 04_Combined_Stock.zip and select Stock only. Leave SKU codes blank, including for comma-containing codes. Use the import cycle once for the whole file.
Inspect SDASF -> sdasf, category Accessories, account 1131. Inspect one finished-goods row, account 1132.
Expected RM value 29,727,743.70 plus FG value 84,689,502.04 = 114,417,245.74.
The 81 new FG fixtures use MSH / UNBRANDED / pcs. Unit conversion factor is 1; the summarized pilot layer date is 2026-09-30.
Another Stock upload replaces the current complete dataset revision. Do not upload FG and RM separately.

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
