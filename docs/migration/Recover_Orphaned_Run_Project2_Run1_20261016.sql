/*
  Target: https://nexus.garnetnation.lk/ — Data Migrate, project 2, run 1.
  Observed 2026-10-16: run Running, JobId 44 missing in Hangfire,
  500 posted units (1 Accounts + 499 CustomerMaster), 1112 approved units.

  Default is READ-ONLY inspection. No changes are made unless @Apply = 1.
  Read the companion recovery guide before applying. Stop ALL application/
  worker processes serving this installation first and keep them stopped
  until this transaction commits. An empty dashboard is not proof that an
  old process has stopped. Do not clear JobId, delete runs, or reset units.

  Run against the HOSTED APPLICATION database, not a local copy or a separate
  Hangfire database. This script was checked against repository schema and
  observed UI data; it has NOT been executed against the hosted database.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @Apply bit = 0;
DECLARE @ExpectedDatabase sysname = N''; -- Exact hosted application DB name.
DECLARE @AllWorkerHostsStopped bit = 0; -- Confirm processes have exited.
DECLARE @MissingJob44Confirmed bit = 0; -- Confirm in the installation's job storage.
DECLARE @Operator nvarchar(450) = N'';
DECLARE @Evidence nvarchar(2000) = N''; -- Who stopped which hosts, when, and job check.

DECLARE @ProjectId int = 2, @RunId int = 1;
DECLARE @JobId nvarchar(100) = N'44';
DECLARE @ApprovedHash nvarchar(64) = N'6D98B942542541F7CFB2E9487EDFA8346BBE0C93BFB7C0EA1F12CD06D06E04F1';
DECLARE @ObservedModifiedAt datetime2(7) = '2026-10-15T16:15:24.3060948';

SELECT @@SERVERNAME AS ServerName, DB_NAME() AS ApplicationDatabase;
SELECT p.Id AS ProjectId, p.Name, p.SourceSystem, p.Status AS ProjectStatus,
       r.Id AS RunId, r.Status AS RunStatus, r.JobId, r.CommittedUnits,
       r.LastModifiedAt, r.Error, r.CompletedAt
FROM [Migration].[MigrationProjects] AS p
JOIN [Migration].[MigrationRuns] AS r ON r.ProjectId = p.Id
WHERE p.Id = @ProjectId AND r.Id = @RunId;
SELECT Module, Status, COUNT(*) AS UnitCount
FROM [Migration].[MigrationUnits]
WHERE ProjectId = @ProjectId
GROUP BY Module, Status;

IF @Apply = 0
BEGIN
    PRINT 'Inspection only. No changes made. Follow the companion guide before applying.';
    RETURN;
END;

IF NULLIF(LTRIM(RTRIM(@ExpectedDatabase)), N'') IS NULL OR DB_NAME() <> @ExpectedDatabase
    THROW 51000, 'Set and verify the exact hosted application database name.', 1;
IF @AllWorkerHostsStopped <> 1 OR @MissingJob44Confirmed <> 1
    THROW 51000, 'Confirm ALL worker processes have exited and the exact Hangfire job 44 is missing.', 1;
IF NULLIF(LTRIM(RTRIM(@Operator)), N'') IS NULL OR NULLIF(LTRIM(RTRIM(@Evidence)), N'') IS NULL
    THROW 51000, 'Record the recovery operator and shutdown/job verification evidence.', 1;
IF @@TRANCOUNT <> 0
    THROW 51000, 'Run this recovery in a session without an existing transaction.', 1;

BEGIN TRY
    BEGIN TRANSACTION;
    DECLARE @LockResult int;
    EXEC @LockResult = sys.sp_getapplock
        @Resource = N'NexusFlow.Migration.2', @LockMode = 'Exclusive',
        @LockOwner = 'Transaction', @LockTimeout = 30000;
    IF @LockResult < 0
        THROW 51000, 'The migration project is busy. No recovery was applied.', 1;

    IF NOT EXISTS (
        SELECT 1 FROM [Migration].[MigrationProjects] WITH (UPDLOCK, HOLDLOCK)
        WHERE Id = @ProjectId AND Name = N'Data Migrate' AND SourceSystem = N'TESTTTT'
          AND Status = N'Preparing' AND ApprovedHash = @ApprovedHash AND FinalizedAt IS NULL
    )
        THROW 51000, 'Project identity, state or approval changed. Diagnose again.', 1;

    IF NOT EXISTS (
        SELECT 1 FROM [Migration].[MigrationRuns] WITH (UPDLOCK, HOLDLOCK)
        WHERE Id = @RunId AND ProjectId = @ProjectId AND Status = N'Running'
          AND JobId = @JobId AND ApprovedHash = @ApprovedHash AND CommittedUnits = 500
          AND LastModifiedAt = @ObservedModifiedAt AND CompletedAt IS NULL AND Error IS NULL
    )
        THROW 51000, 'Run no longer matches the observed interrupted run. Diagnose again.', 1;

    IF EXISTS (
        SELECT 1 FROM [Migration].[MigrationRuns] WITH (UPDLOCK, HOLDLOCK)
        WHERE ProjectId = @ProjectId AND Id <> @RunId AND Status IN (N'Queued', N'Running')
    )
        THROW 51000, 'Another active run exists. No recovery was applied.', 1;

    IF (SELECT COUNT(*) FROM [Migration].[MigrationUnits] WITH (HOLDLOCK) WHERE ProjectId = @ProjectId) <> 500
       OR (SELECT COUNT(*) FROM [Migration].[MigrationUnits] WITH (HOLDLOCK) WHERE ProjectId = @ProjectId AND Status = N'Posted' AND Module = N'Accounts') <> 1
       OR (SELECT COUNT(*) FROM [Migration].[MigrationUnits] WITH (HOLDLOCK) WHERE ProjectId = @ProjectId AND Status = N'Posted' AND Module = N'CustomerMaster') <> 499
        THROW 51000, 'Committed units changed. Preserve them and diagnose again.', 1;

    DECLARE @Now datetime2(7) = SYSUTCDATETIME();
    DECLARE @Detail nvarchar(max) = CONCAT(
        N'Run 1 recovered from Running to Failed after all worker hosts stopped; Hangfire job 44 confirmed missing. ',
        N'500 committed units retained. Original job reference, approval and selection retained. Operator: ',
        @Operator, N'. Evidence: ', @Evidence);

    UPDATE [Migration].[MigrationRuns]
    SET Status = N'Failed',
        Error = N'Interrupted run recovered after all workers stopped and Hangfire job 44 was confirmed missing. 500 committed units retained. Use Resume Run to continue the original approved selection.',
        LastModifiedAt = @Now, LastModifiedBy = @Operator
    WHERE Id = @RunId AND ProjectId = @ProjectId AND Status = N'Running'
      AND JobId = @JobId AND LastModifiedAt = @ObservedModifiedAt;
    IF @@ROWCOUNT <> 1
        THROW 51000, 'Expected exactly one run update. Recovery rolled back.', 1;

    INSERT INTO [Migration].[MigrationAudit]
        (ProjectId, Action, Detail, CreatedAt, CreatedBy, LastModifiedAt, LastModifiedBy)
    VALUES (@ProjectId, N'Execution stopped', @Detail, @Now, @Operator, NULL, NULL);
    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;

SELECT Id AS RunId, ProjectId, Status, JobId, CommittedUnits, Error, LastModifiedAt
FROM [Migration].[MigrationRuns] WHERE Id = @RunId AND ProjectId = @ProjectId;
PRINT 'Recovery committed. Restart the worker hosts, refresh Migration Centre, and use Resume Run for run 1.';
