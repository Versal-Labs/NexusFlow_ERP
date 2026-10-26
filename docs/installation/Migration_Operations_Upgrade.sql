BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261021032811_AddMigrationOperations'
)
BEGIN
    CREATE TABLE [Migration].[MigrationOperations] (
        [Id] int NOT NULL IDENTITY,
        [ProjectId] int NOT NULL,
        [ClientRequestId] uniqueidentifier NOT NULL,
        [Kind] nvarchar(24) NOT NULL,
        [Status] nvarchar(24) NOT NULL,
        [InitiatedBy] nvarchar(450) NOT NULL,
        [InitiatedByName] nvarchar(256) NOT NULL,
        [InputFingerprint] nvarchar(64) NOT NULL,
        [InputVersion] int NOT NULL,
        [JobId] nvarchar(100) NULL,
        [RunId] int NULL,
        [StartedAt] datetime2 NULL,
        [UpdatedAt] datetime2 NOT NULL,
        [CompletedAt] datetime2 NULL,
        [Phase] nvarchar(160) NOT NULL,
        [CurrentItem] nvarchar(200) NULL,
        [Total] int NULL,
        [Processed] int NOT NULL,
        [Succeeded] int NOT NULL,
        [NeedsAttention] int NOT NULL,
        [Error] nvarchar(4000) NULL,
        [ResultHash] nvarchar(64) NULL,
        [CanApprove] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [LastModifiedAt] datetime2 NULL,
        [LastModifiedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_MigrationOperations] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_MigrationOperations_MigrationProjects_ProjectId] FOREIGN KEY ([ProjectId]) REFERENCES [Migration].[MigrationProjects] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_MigrationOperations_MigrationRuns_RunId] FOREIGN KEY ([RunId]) REFERENCES [Migration].[MigrationRuns] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261021032811_AddMigrationOperations'
)
BEGIN
    CREATE TABLE [Migration].[MigrationRunPayloads] (
        [Id] int NOT NULL IDENTITY,
        [RunId] int NOT NULL,
        [PayloadJson] nvarchar(max) NOT NULL,
        CONSTRAINT [PK_MigrationRunPayloads] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_MigrationRunPayloads_MigrationRuns_RunId] FOREIGN KEY ([RunId]) REFERENCES [Migration].[MigrationRuns] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261021032811_AddMigrationOperations'
)
BEGIN
    CREATE TABLE [Migration].[MigrationOperationItems] (
        [Id] int NOT NULL IDENTITY,
        [OperationId] int NOT NULL,
        [Ordinal] int NOT NULL,
        [Module] nvarchar(40) NOT NULL,
        [Kind] nvarchar(24) NOT NULL,
        [Code] nvarchar(160) NOT NULL,
        [ScopeId] int NULL,
        [Status] nvarchar(24) NOT NULL,
        [Message] nvarchar(4000) NULL,
        [Rows] int NOT NULL,
        [CompletedAt] datetime2 NULL,
        CONSTRAINT [PK_MigrationOperationItems] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_MigrationOperationItems_MigrationOperations_OperationId] FOREIGN KEY ([OperationId]) REFERENCES [Migration].[MigrationOperations] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261021032811_AddMigrationOperations'
)
BEGIN
    CREATE TABLE [Migration].[MigrationOperationPayloads] (
        [Id] int NOT NULL IDENTITY,
        [OperationId] int NOT NULL,
        [RequestJson] nvarchar(max) NOT NULL,
        CONSTRAINT [PK_MigrationOperationPayloads] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_MigrationOperationPayloads_MigrationOperations_OperationId] FOREIGN KEY ([OperationId]) REFERENCES [Migration].[MigrationOperations] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261021032811_AddMigrationOperations'
)
BEGIN
    CREATE TABLE [Migration].[MigrationOperationIssues] (
        [Id] int NOT NULL IDENTITY,
        [OperationId] int NOT NULL,
        [ItemId] int NULL,
        [Severity] nvarchar(12) NOT NULL,
        [Module] nvarchar(40) NOT NULL,
        [Code] nvarchar(160) NOT NULL,
        [Row] int NOT NULL,
        [AffectedRows] int NOT NULL,
        [Message] nvarchar(2000) NOT NULL,
        CONSTRAINT [PK_MigrationOperationIssues] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_MigrationOperationIssues_MigrationOperationItems_ItemId] FOREIGN KEY ([ItemId]) REFERENCES [Migration].[MigrationOperationItems] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_MigrationOperationIssues_MigrationOperations_OperationId] FOREIGN KEY ([OperationId]) REFERENCES [Migration].[MigrationOperations] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261021032811_AddMigrationOperations'
)
BEGIN
    CREATE TABLE [Migration].[MigrationOperationItemPayloads] (
        [Id] int NOT NULL IDENTITY,
        [ItemId] int NOT NULL,
        [DetailJson] nvarchar(max) NOT NULL,
        CONSTRAINT [PK_MigrationOperationItemPayloads] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_MigrationOperationItemPayloads_MigrationOperationItems_ItemId] FOREIGN KEY ([ItemId]) REFERENCES [Migration].[MigrationOperationItems] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261021032811_AddMigrationOperations'
)
BEGIN
    CREATE INDEX [IX_MigrationOperationIssues_ItemId] ON [Migration].[MigrationOperationIssues] ([ItemId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261021032811_AddMigrationOperations'
)
BEGIN
    CREATE INDEX [IX_MigrationOperationIssues_OperationId_Severity_Id] ON [Migration].[MigrationOperationIssues] ([OperationId], [Severity], [Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261021032811_AddMigrationOperations'
)
BEGIN
    CREATE UNIQUE INDEX [IX_MigrationOperationItemPayloads_ItemId] ON [Migration].[MigrationOperationItemPayloads] ([ItemId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261021032811_AddMigrationOperations'
)
BEGIN
    CREATE UNIQUE INDEX [IX_MigrationOperationItems_OperationId_Ordinal] ON [Migration].[MigrationOperationItems] ([OperationId], [Ordinal]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261021032811_AddMigrationOperations'
)
BEGIN
    CREATE INDEX [IX_MigrationOperationItems_OperationId_Status_Id] ON [Migration].[MigrationOperationItems] ([OperationId], [Status], [Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261021032811_AddMigrationOperations'
)
BEGIN
    CREATE UNIQUE INDEX [IX_MigrationOperationPayloads_OperationId] ON [Migration].[MigrationOperationPayloads] ([OperationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261021032811_AddMigrationOperations'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_MigrationOperations_ProjectId] ON [Migration].[MigrationOperations] ([ProjectId]) WHERE [Status] IN (''Queued'',''Running'')');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261021032811_AddMigrationOperations'
)
BEGIN
    CREATE UNIQUE INDEX [IX_MigrationOperations_ProjectId_ClientRequestId] ON [Migration].[MigrationOperations] ([ProjectId], [ClientRequestId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261021032811_AddMigrationOperations'
)
BEGIN
    CREATE INDEX [IX_MigrationOperations_RunId] ON [Migration].[MigrationOperations] ([RunId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261021032811_AddMigrationOperations'
)
BEGIN
    CREATE UNIQUE INDEX [IX_MigrationRunPayloads_RunId] ON [Migration].[MigrationRunPayloads] ([RunId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261021032811_AddMigrationOperations'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261021032811_AddMigrationOperations', N'10.0.5');
END;

COMMIT;
GO

