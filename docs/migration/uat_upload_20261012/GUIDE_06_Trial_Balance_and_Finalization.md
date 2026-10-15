# Stage 6 — test trial balance, ledger check and finalization

Precondition: every opening scope Released; opening modules and controls posted; financial period open; no new source/mapping changes. This UAT TB deliberately replaces the original unbalanced 188-account report with a balanced full 39-account test position.

1. Upload 03_TrialBalance.zip or TrialBalance.csv.
2. Select Trial Balance only; leave filters blank. Validate Selected Import. All 39 current active posting accounts are present, including 30 deliberate test-zero accounts. Debit = Credit = 282960858.88 LKR.
3. The TB includes preceding opening contributions. Do not manually subtract AR, AP, stock, safe or bank. The importer subtracts its own opening contributions. Expected residual: 6210 Rent Expense Debit 1000000.00 and 4110 Sales Revenue - FG Credit 1000000.00. Managed-control residuals must be zero.
4. Record Approval evidence: UAT synthetic TB from opening journal expectations plus balanced rent/sales residual pair; no production sign-off. Approve Selected Import, Execute Approved Import, Refresh Progress.
5. Select Check Full Ledger. All 39 account differences must be zero. Compare reports/Expected_GL.csv and UAT_Review.xlsx.
6. Check all twelve datasets Provided, every current row Already imported and all 2050 scopes Released. Run retry/idempotency checks before finalization.
7. Enter Final financial sign-off evidence: UAT functional acceptance only, package 2026-10-12; synthetic fixtures checked; no production migration approval. Select Finalize Migration after completeness and zero differences pass.
8. Expected: Finalized; migration editing/execution disabled; ordinary operations remain available for released entities. Use a separate UAT company for another full import cycle. This preparation does not reset or finalize the application.

The original trial balance and its -613120.00 imbalance remain archived unchanged. Production still requires certified closing data and approved legacy account mappings.
