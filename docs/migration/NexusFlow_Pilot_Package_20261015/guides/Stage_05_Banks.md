# Upload 05 — Banks

File: stage_packages/05_Banks.zip

Select only: **Bank Opening, Bank Recon**.

Expected: 1 book opening; 1 non-cheque reconciliation item.

Upload 05_Banks.zip and use the import cycle for Bank Opening and Bank Recon only.
Bank account 1112: book opening 100,000.00; statement 112,500.00; pending deposit +2,500.00; issued cheque pending -15,000.00 from stage 03.
Require statement bridge 112,500 + 2,500 - 15,000 = 100,000.
Do not repeat the issued cheque as another Bank Recon row.
All bank values in this stage are retained pilot fixtures.

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
