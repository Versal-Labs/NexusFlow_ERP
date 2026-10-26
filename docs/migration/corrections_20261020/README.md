# Trial Balance correction for account 1140

Use **07_Trial_Balance_1140_Zero_20261020.zip** for project **3 / Data Migrate**. It contains only `manifest.json` and `TrialBalance.csv`. The separate CSV is an alternative individual dataset upload; choose one upload method.

The user's read-only results show the project is Preparing, version 105; Trial Balance dataset **15**, revision **1**, contains 39 source accounts, against 40 active posting accounts. Only **1140 / Input VAT Receivable / ID 5025** is missing. No Trial Balance unit was returned. Runs 2–7 were Completed. The user explicitly confirmed that account 1140 has a zero closing balance at **2026-09-30**.

The corrected file contains **40 rows**, adding only:

```csv
,1140,0.00,0.00,2026-09-30
```

All 39 original row values are retained, including account 1141. Debit and credit totals remain **LKR 282,960,858.88 each**. The source identifier remains **TESTTTT** and closing date **2026-09-30**. The original CSV's SHA256 matches the user's uploaded dataset 15. No other closing balance was assumed or changed.

## Upload and complete the Trial Balance stage

1. In **Migration Centre**, select saved project **Data Migrate** (ID 3).
2. Under **1. Upload closing data**, select the corrected ZIP and click **Upload Package**. The Trial Balance should become revision **2** if no other revision has been uploaded since the supplied results. For an individual upload instead, choose **Dataset: Trial Balance**, select the separate corrected CSV and click **Upload Dataset**. Upload once using one method.
3. Under **3. Select this import**, select **Trial Balance only** and leave all code filters blank.
4. Click **Validate Selected Import**. Expect 40 Trial Balance rows in one ALL unit. Resolve every error before approval; all opening scopes and prior financial-stage requirements must already be complete.
5. Review the proposed residual journal and enter truthful **Approval evidence**, referencing the confirmed zero for 1140 and unchanged original closing balances.
6. Click **Approve Selected Import**, then **Execute Approved Import** once. Use **Refresh Progress** until this Trial Balance run is Completed. The earlier completed import stages do not need to be repeated.
7. Click **Check Full Ledger**. Require zero Difference for all 40 active posting accounts.
8. Enter **Final financial sign-off evidence** and click **Finalize Migration** after accepting the pilot baseline.

This upload replaces the staged Trial Balance revision. It does not reset the project or delete prior posted work. No hosted upload, execution or financial change was performed by the assistant.

## If validation says to release all required opening scopes

The message **Complete, reconcile and release all required opening scopes before posting residual GL** blocks Trial Balance when any project scope is neither Released nor NoLegacyOpening, or another financial dataset is still AwaitingData. Completed posting runs alone do not release these scopes.

1. Under **3. Select this import**, clear the current selection and select **Customer Master**, **Supplier Master**, **Stock** and **Bank Opening**. Leave the Customer, Supplier, SKU and Bank account code filters blank so every scope of those four kinds is included.
2. Open **Release multiple reconciled scopes** near the bottom of Migration Centre. This action requires the independent **Control Totals** schedule to have been uploaded, validated, approved and imported. If the application reports that this is missing, complete that stage first.
3. Enter a truthful review reference in **Signed independent schedule evidence**, confirming that the pilot's complete opening positions and independent amounts/row counts have been checked. Click **Release Selected Reconciled Scopes** and wait for the result.
4. Review the released count and every pending reason. Resolve missing data, unposted rows, unmapped masters or control differences using the stated reason, then retry release. Do not change scope statuses directly in SQL. A financial dataset still marked Awaiting Data must be supplied or truthfully classified through the project settings with the required evidence.
5. Once all required scopes are released and no required financial dataset is Awaiting Data, select **Trial Balance only**, click **Validate Selected Import** again, review and approve the residual journal, then execute it. Use **Check Full Ledger** after that run completes.

The **using a composite legacy identity** messages for rows 2–41 are warnings caused by the blank LegacyRecordId column. For Trial Balance specifically, the application uses **Code** as the fallback identity. This corrected file has 40 unique account codes; keep those codes stable and retain the same corrected file. No extra identity values are needed to resolve the opening-scope error.

### If bulk release shows no result and many scopes remain pending

Bulk release executes inside the browser's request, separately from Hangfire posting. It commits each successful scope independently and sends the released/pending result only after the loop finishes. A request interruption can therefore leave partial saved progress without a final result. This is a possible explanation for a blank result; it has not been confirmed on the hosted site.

Run `../Check_Project3_Pending_Releases_20261020.sql` in SSMS against the hosted database. It is read-only and returns scope counts, financial dataset states, ControlTotals import status and the next 25 pending customer codes. Compare saved counts again after about 30 seconds. Counts that are still changing show saved progress; do not start an overlapping release request.

Once the earlier request has ended and the counts are stable, test one pending customer: select **Customer Master only**, put that exact code in **Customer codes**, provide the independently checked evidence and click **Release Selected Reconciled Scopes**. Read both the result near that button and any error at the top of the page. Resolve the actual reported reason. If the single-customer release succeeds, use small explicit code batches rather than an unfiltered selection, then refresh progress and recheck pending counts. Reselect **Customer Master only** before each batch because a full page-data reload can restore the other provided module checkboxes. A missing posted unit alone does not prove an error: an empty opening component can legitimately have no unit.

On 2026-10-20 the user reported 223 Released customers, 673 Pending customers, 215 Pending suppliers, 938 Pending stock scopes and 1 Pending bank scope (2,050 scopes total, 1,827 pending). The first pending customer returned was **FR914**. These results prove that some releases committed; they do not prove a timeout or a control mismatch. The local original package contains FR914's customer master, one customer invoice for **123,800.00**, and all three customer control components: debit **123,800.00 / 1 row**, credit **0.00 / 1 row**, received cheques **0.00 / 0 rows**. This checks the local package only; current hosted posted values still require application reconciliation. Do not reimport the completed opening stages based only on the Pending scope status.

The user subsequently released **FR914** successfully, receiving **1 scopes released; 0 remain pending** for that selection. This confirms that this customer's release checks passed. The pending count in the action result applies only to scopes attempted by that request, not to the entire project. Without other concurrent changes, that action would leave 672 pending customers and 1,826 pending scopes overall.

The diagnostic script also returns **NEXT_BATCH_PER_KIND_25**: at most 25 pending codes for each scope kind, with the exact module selection and filter field. Use one kind per request after the single-customer test succeeds, reselecting only that module each time. Continue until the scope counts show no Pending rows; separately resolve any financial dataset still Awaiting Data before revalidating Trial Balance. If a 25-code request cannot finish, reduce the Position limit to 10. Stop on a reported validation/control failure and resolve the actual reason.

## Verification

The correction preserves the exact canonical header and confirmed closing date. The new account is unique, and all 39 original records retain their values. Independent totals match both source totals. The ZIP uses the original stage-7 manifest and contains the same corrected CSV as the separate file. The file was authored, inspected and visually checked with the spreadsheet artifact tool. **The actual application file reader accepted all 40 rows with zero errors**, confirmed every original parsed record unchanged and verified the ZIP contents/manifest. The build had zero warnings and zero errors. Verification is recorded in `artifacts/trial-balance-1140-20261020/importer-verification.json`. These local file checks do not replace validation against the current hosted ledger and scope state.

| File | SHA256 |
| --- | --- |
| Original TrialBalance.csv / uploaded dataset 15 | E8354578985CDCB9CD4FF760CADB4B93BB056CE25E6C0B974E978826B0544479 |
| TrialBalance_1140_Zero_20261020.csv | 945440C4C8DB9651C5B23D9605EC5DA5E54979FF6BFFADB3060F7AE324BF75C3 |
| 07_Trial_Balance_1140_Zero_20261020.zip | DDA8C6DC083E38628ECB85E601B4BBC042A018041906EAA4F12D732A115D05D4 |
