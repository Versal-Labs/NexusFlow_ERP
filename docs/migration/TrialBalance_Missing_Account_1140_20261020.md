# Check Full Ledger: missing account 1140

The user reported **Closing trial balance is missing account 1140, including its zero balance if appropriate** for the fresh migration on 20 October 2026. The user confirmed that **1140 / Input VAT Receivable has a zero closing balance** for this pilot at 2026-09-30, and was unsure whether Trial Balance had already executed. No hosted change has been performed by the assistant for this issue.

**Read-only result supplied subsequently:** project **3 / Data Migrate / Preparing / version 105** has Trial Balance dataset **15 / revision 1**, with 39 rows against 40 active posting accounts. Only 1140 (account ID 5025) is missing; no Trial Balance units or unresolved source rows were returned. Runs 2–7 are Completed. This resolves the earlier posting-status uncertainty: the Trial Balance is staged and has not posted. Use the [corrected stage-7 package and instructions](corrections_20261020/README.md), which add only the confirmed zero row and retain all original balances.

## Source findings

`ReconcileLedgerAsync` normalizes the current Trial Balance and checks every active transaction account in the destination chart. It requires a source row even for a zero-balance account. The supplied pilot `TrialBalance.csv` has 39 rows and includes **1141**, but no **1140**. The source Account upload likewise contains 1141. These account codes are distinct; a row for 1141 does not cover 1140 without an explicit reviewed mapping. Do not replace 1141 to hide the missing account.

The user's confirmed zero row for 1140 would be:

```csv
LegacyRecordId,Code,Debit,Credit,ClosingDate
,1140,0.00,0.00,2026-09-30
```

This is an addition to the full file, not a standalone Trial Balance replacement. The supplied coverage check confirms no other destination account is missing. The zero is based on the user's explicit closing-balance confirmation; current application balances alone are not evidence of original source closing balances.

## Required read-only check

Run [Check_Project3_TrialBalance_Coverage_20261020.sql](Check_Project3_TrialBalance_Coverage_20261020.sql) against NexusGarnetProd. Return all result sets, particularly **TRIAL_BALANCE_UNITS**, **MISSING_ACTIVE_POSTING_ACCOUNTS** and **UNRESOLVED_SOURCE_ROWS**. It checks canonical parsed rows and explicit Account mappings, and stops for review if a Trial Balance column profile exists. It does not change application data, balances, approvals or posted units.

If the Trial Balance unit result is empty, no Trial Balance unit is recorded for this project. If a unit is Posted, retain it and review its hash/source before changing a file. The current source groups Trial Balance into one **ALL** unit, rejects changed posted snapshots and explicitly disallows normal migration reversal of Trial Balance (`ReverseUnitAsync`). Therefore do not advise another execution or a generic unit reversal for an already posted Trial Balance. A dated correction or an explicitly reviewed source-evidence reconciliation would need separate preparation.

If it is unposted, the full corrected file must retain every existing row and amount, add every missing destination account with its confirmed closing balance, remain balanced, and retain the project's closing date. Upload the corrected full Trial Balance, validate Trial Balance only, resolve errors, approve and execute before final reconciliation. Scope release and all prior financial-stage prerequisites still apply. A new upload alone does not post the ledger.

Browser access was unavailable in the preceding workflow, so this finding is cross-checked against the source, supplied files and the user's current message rather than a newly observed UI session. Application source has not been changed.
