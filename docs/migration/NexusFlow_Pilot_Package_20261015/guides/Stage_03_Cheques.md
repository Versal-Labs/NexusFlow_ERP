# Upload 03 — Cheques

File: stage_packages/03_Cheques.zip

Select only: **Received Cheques, Issued Cheques**.

Expected: 49 received cheques; 1 issued pilot cheque.

Upload 03_Cheques.zip and use the import cycle for Received Cheques and Issued Cheques only.
Expected received: 49 instruments, LKR 5,328,628.00, InSafe, PartyReflected=true, BankReflected=false. They establish cheques in hand without reducing receivables a second time.
Expected issued fixture: supplier AH143, reference UAT-ISSUED-001, cheque 000001, LKR 15,000.00, bank 1112; both reflection flags true. It is already reflected in supplier and bank book balances.
Keep cheque numbers as text. Do not enter these instruments again through ordinary receipt/payment entry.

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
