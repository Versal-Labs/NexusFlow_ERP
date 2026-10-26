# Migration Centre: guided stages and durable operations

Local implementation, 2026-10-21. Route: `/Migration`. Existing Razor/Bootstrap theme, permission policies, accounting validations, independent controls, stored originals and evidence requirements remain in use. This feature has not been deployed or applied to hosted SQL.

## Administrator workflow

Use the seven stage buttons to revisit **Upload files**, **Map data**, **Validate and review**, **Approve and import**, **Reconcile and release**, **Trial Balance and ledger**, and **Final sign-off**. The overview reports saved project counts and a suggested next step. Stage labels describe saved progress; business prerequisites are checked again by every action. An import stage remains in progress until project finalization, because a successful selected batch does not prove every source has been imported.

1. Choose **Saved migration project**. Upload the closing files or ZIP. Keep the page open until the server confirms staging; visible transfer progress is not a posting result.
2. Set exact mappings/defaults, column profiles, data states and evidence. Search saved mappings using 25/50/100 rows. Save with **Save Mappings and Evidence**. Older mapping drafts are rejected if the project version changed.
3. Select import modules/codes and **Validate Selected Import**. Review the bounded results, source rows and proposed journals. Errors are selected by default for each new validation when present. Warning summaries show affected-row counts; their original rows and details remain accessible.
4. Enter approval evidence, **Approve Selected Import**, wait, then **Execute Approved Import**. Selection and current review hash must agree. All long operations are saved before dispatch.
5. In **Reconcile and release**, select Customer/Supplier/Stock/Bank and search pending scopes. Release has its own selection. **Select pending on this page** accumulates selections across pages; **Clear selection across pages** clears them. The two release actions show their exact target counts. **Release all matching pending scopes** includes other pages of the current kind/search. Imported ControlTotals readiness and independent schedule evidence sit beside the actions. The server freezes pending targets at acceptance and rejects a changed target count.
6. Review **This operation** results separately from the overview's project totals. **View unresolved scopes** opens unfinished reasons. An individual business error leaves that scope pending and allows other targets to continue. Infrastructure failures stop with already committed work retained.
7. After all opening scopes are released, upload/validate/approve/import TrialBalance, then **Check Full Ledger**. Inspect account details and correct differences through the source workflow. Enter final evidence and **Finalize Migration** only after review; the existing final checks are repeated on the server.

A single independently reconciled scope still offers **Reconcile** and **Release Reconciled Scope** with each total/count entered explicitly. Legacy API routes retain their previous response contracts and share active-operation guards. The guided workspace uses the new operations endpoints for durable long work; direct legacy clients should use those endpoints to obtain refresh-safe operation tracking. Old posting-run recovery remains available from Posting run history; operation-owned runs must be resumed through the owning operation.

## Progress, connectivity and recovery

On submission the page shows **Starting…** and prevents duplicate starts. An accepted operation shows **Running on the server. You can safely refresh or leave this page.** Its phase, current item, processed/total, successes, attention count, elapsed time and last update appear persistently. Preparation is indeterminate until the total is known. The activity feed retains the latest 20 outcomes; Operation history opens older, paginated outcomes.

The saved project, stage, filters, release selection, import selection and draft fields survive refresh in the same browser tab when session storage is available. Polling updates the overview/progress/activity without rebuilding forms, moving focus or scrolling. Active polling is every two seconds; hidden and idle pages slow down. Immutable requests/previews are fetched on demand, never on a timer.

A transport failure retains the last confirmed values and shows **Connection lost—checking progress**. It does not mark the job failed. An uncertain start can use **Retry start request**, which reuses the saved client request ID. Accepted operations awaiting enqueue remain Queued and are checked every 30 seconds by the dispatcher.

For interrupted/failed work, **Retry / Resume unfinished work** is offered only when job-service evidence indicates inactivity. POST Resume checks again and must acquire the worker's session lock. Active or unavailable/uncertain job state cannot enable a competing worker. Successful checkpoints are retained. Changes to settings, source revisions/hashes or scope requirements prevent Resume and require fresh validation/approval as applicable. Release's own Version/evidence/status changes do not invalidate its unchanged input fingerprint. Completed validation/ledger checks with issues should be run afresh after correction. Pause/Stop is outside this release.

## API and storage

All endpoints require existing authenticated Migration.View access, and writes require the corresponding action permission plus existing cookie antiforgery controls.

| Endpoint | Purpose |
|---|---|
| POST `/api/migration/projects/{id}/operations` | `kind`, `clientRequestId`, existing action selection/hash/evidence and release-specific targets; 202 with operationId/statusUrl. Same project/request ID returns the original operation. |
| GET `.../{id}/summary` | Compact project/stage/scoped totals and active operation/legacy run; no preview/run payloads. |
| GET `.../{id}/workspace` | On-demand initial settings and file metadata. |
| GET `.../{id}/operations/{operationId}` | Small progress and incremental latest-20 activity; optional `after` cursor. |
| GET `.../{id}/operations/{operationId}/items` | Bounded outcomes, search/module/kind/status filters. |
| GET `.../{id}/operations/{operationId}/items/{itemId}` | On-demand paginated source/error/warning/journal details or account control. |
| GET `.../{id}/operations/{operationId}/issues` | Grouped warnings/errors or bounded affected-row occurrences. |
| GET `.../{id}/operations/{operationId}/context` | Immutable accepted request, fetched on demand for restored selection/evidence context. |
| POST `.../{id}/operations/{operationId}/resume` | Stored-kind permission, confirmed inactive job, free worker lock and unchanged-input guard. |
| GET `.../{id}/scopes` | Paged reconciliation/release scopes. |
| GET `.../{id}/lists/{list}` | `mappings`, `units`, `runs`, `operations`, `audit`; bounded server responses. |

Kinds: Validate, Approve, Import, Release, LedgerCheck, Finalize. States: Queued, Running, Completed, CompletedWithIssues, Failed. Paging defaults to 25 and caps at 100. Release request carries `release.kind`, `search`, `allMatching`, `scopeIds` and `expectedCount`; it is independent of import modules.

Project transaction locks serialize acceptance/changes with legacy posting runs. A separate session application lock spans each worker's whole execution and is also required for Resume. Checkpoints and successful scope releases/posting groups commit in the same database transaction. Jobs enqueue only an integer operation ID. New financial jobs and existing posting jobs disable Hangfire automatic retry; explicit Resume owns recovery. The initiating identity is persisted and applied to background approval, financial and audit writes without needing HTTP context.

Six additive non-temporal tables separate small progress/outcomes/issues from immutable requests, preview details and new run payloads: MigrationOperations, MigrationOperationItems, MigrationOperationIssues, MigrationOperationPayloads, MigrationOperationItemPayloads and MigrationRunPayloads. The DbContext temporal convention explicitly excludes them. New MigrationRuns.SelectionJson remains `{}`; reads fall back to legacy SelectionJson for old runs. Failure counts match only that run's originally selected pending units and hashes. No legacy payload/history is rewritten or purged. Progress GETs do not call SaveChanges.

## Verification and hosted boundary

See [verification record](../uat/Migration_Operations_Verification.md), [business acceptance cases](../uat/UAT_Test_Cases.md), [test data](../uat/UAT_Test_Data.md), [permissions](../uat/UAT_Roles_And_Permissions.md), and [additive rollout](../installation/UAT_Update_Migration_Operations.md).

Hosted URL: https://nexus.garnetnation.lk/. @Browser reached its sign-in page on 2026-10-21; authenticated hosted workflow acceptance remains pending. Project **3 / Data Migrate**, source **TESTTTT**, opening **2026-10-01** / closing **2026-09-30**, is finalized. Preserve it. No hosted SQL, deployment, reset, reopening or hosted financial write was performed for this feature. Business role assignment, source certification and sign-off authority are **Needs business confirmation**.
