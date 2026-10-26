# Missing Hangfire job blocks Resume Run

## Confirmed hosted observations

Checked through @Browser on 16 October 2026 at [NexusFlow](https://nexus.garnetnation.lk/), signed in as System Admin.

| Item | Observed value |
| --- | --- |
| Migration Centre project | Data Migrate; ID 2; Preparing |
| Run | ID 1; Running; no saved error; no completion date |
| Linked Hangfire job | 44 |
| Exact job detail | [Job 44](https://nexus.garnetnation.lk/hangfire/jobs/details/44): “Background job '44' has expired or could not be found on the server.” |
| Active dashboard states | Processing 0; Enqueued 0; Scheduled 0; Retries 0; Awaiting 0 |
| Posted units | 500: 1 Accounts unit and 499 CustomerMaster units |
| Approved run | 1,112 units; Accounts, CustomerMaster and SupplierMaster |
| Last saved run change | 2026-10-15 16:15:24.3060948 UTC (21:45:24 Asia/Colombo) |

The saved run is orphaned relative to the job storage shown by this installation. Why the job disappeared and why execution stopped still require hosting logs or job-storage history. Neither a server crash nor a specific expiry cause has been confirmed.

## Why both messages appear

- `NexusFlow.AppCore/Features/Migration/MigrationCentreService.cs`, `Editable`: any saved Queued or Running run blocks project changes. This check does not inspect Hangfire.
- `NexusFlow.Infrastructure/Hangfire/BackgroundJobService.cs`, `GetState`: a missing state record returns null.
- `NexusFlow.AppCore/Features/Migration/MigrationPosting.cs`, `DispatchAsync`: a Running run can resume only when its old job has a Failed, Succeeded or Deleted state. A missing job therefore produces the “active or its state cannot be confirmed” message.

An empty Processing page alone does not resolve the saved migration state. There is no dedicated stop/recover action in the inspected Migration Centre implementation for this missing-job case.

## Targeted recovery for the hosting administrator

Prepared script: [Recover_Orphaned_Run_Project2_Run1_20261016.sql](Recover_Orphaned_Run_Project2_Run1_20261016.sql).

The script has not been run against the hosted database. Its default mode only reads the project, run and unit counts. Its apply mode changes only run 1 from Running to Failed and adds an Execution stopped audit entry. It retains the existing job reference, approval, selection, 500 committed units and all master/financial records. Temporal history remains enabled.

1. Take a current backup of the hosted application database and retain the hosting logs around the last saved run change.
2. Confirm the exact job 44 is still missing from the Hangfire storage configured for this installation. If it has reappeared, stop and investigate its state.
3. Stop **all** NexusFlow application/worker hosts connected to this installation, including other IIS instances or worker services, and confirm their processes have exited. Keep them stopped through the database update. The application currently passes `CancellationToken.None` to migration execution; a missing dashboard job does not prove an old process has stopped executing.
4. In SQL Server Management Studio, connect to the **hosted application database** and run the script with `@Apply = 0`. Confirm project 2, run 1, JobId 44, Running and the exact counts above. A separate Hangfire database is not the target for this update.
5. Set `@ExpectedDatabase` to the exact hosted application database name. Record `@Operator` and `@Evidence`, confirm the two verification flags, and set `@Apply = 1`. Execute the complete script. Its project lock and observed-state checks reject changed data; do not weaken a failed check.
6. Confirm the returned run has Status Failed, JobId 44 and CommittedUnits 500. Restart the hosting services.
7. Open **Settings & Security → Migration Centre**, choose **Data Migrate — Preparing**, and select **Refresh Progress**. Run 1 should show Failed and the recorded recovery explanation.
8. To continue the original approved import, select **Resume Run** on run 1 before editing its source/mappings. Select **Refresh Progress** to monitor it. Check the new linked Hangfire job in its detail page. Resume uses the saved selection; the current checkboxes do not replace that selection.
9. Confirm the run completes and inspect **Posted units and unused-unit reversal** and **Migration audit**. If it stops with a new validation error, investigate that specific error. The code checks stored unit hashes and skips matching posted units; recovery must never delete units or legacy origins to force progress.

If business data or mappings must change instead, recovery first removes the stale active-run block. Review those changes through **Validate Selected Import**, **Approve Selected Import** and **Execute Approved Import**. Do not assume the old approved run remains valid after editing the project.

## Application improvement to consider

Provide an explicit authorized recovery action that records the exact job state and shutdown evidence. Recovery and subsequent workers need an execution ownership check for each atomic group, so an older worker cannot continue after a replacement starts. Preserve committed units and fail closed when the job service cannot be reached. Simply allowing every missing Running job to requeue is insufficient because the old worker may still be alive.

Application source was not changed, as required by AGENTS.md. The SQL was reviewed against the repository's entity/schema definitions and project lock; hosted execution and recovery verification remain pending with the hosting administrator.
