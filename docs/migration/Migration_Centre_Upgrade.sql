BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005011038_AddCustomerOpeningMigration'
)
BEGIN
    ALTER TABLE [Finance].[ChequeRegister] DROP CONSTRAINT [FK_ChequeRegister_PaymentTransactions_OriginalReceiptId];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005011038_AddCustomerOpeningMigration'
)
BEGIN
    ALTER TABLE [Finance].[PaymentTransactions] ADD [VoidDate] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005011038_AddCustomerOpeningMigration'
)
BEGIN
    ALTER TABLE [Finance].[PaymentAllocations] ADD [CustomerDebitMemoId] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005011038_AddCustomerOpeningMigration'
)
BEGIN
    ALTER TABLE [Finance].[PaymentAllocations] ADD [CustomerOpeningItemId] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005011038_AddCustomerOpeningMigration'
)
BEGIN
    ALTER TABLE [Sales].[Customers] ADD [LegacyCustomerCode] nvarchar(50) COLLATE Latin1_General_100_CI_AS NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005011038_AddCustomerOpeningMigration'
)
BEGIN
    ALTER TABLE [Sales].[CustomerDebitMemos] ADD [DueDate] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005011038_AddCustomerOpeningMigration'
)
BEGIN
    ALTER TABLE [Sales].[CustomerDebitMemos] ADD [ReceivableAccountId] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005011038_AddCustomerOpeningMigration'
)
BEGIN
    DECLARE @var nvarchar(max);
    SELECT @var = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Finance].[ChequeRegister]') AND [c].[name] = N'OriginalReceiptId');
    IF @var IS NOT NULL EXEC(N'ALTER TABLE [Finance].[ChequeRegister] DROP CONSTRAINT ' + @var + ';');
    ALTER TABLE [Finance].[ChequeRegister] ALTER COLUMN [OriginalReceiptId] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005011038_AddCustomerOpeningMigration'
)
BEGIN
    ALTER TABLE [Finance].[ChequeRegister] ADD [DepositJournalEntryId] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005011038_AddCustomerOpeningMigration'
)
BEGIN
    ALTER TABLE [Finance].[ChequeRegister] ADD [DepositedDate] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005011038_AddCustomerOpeningMigration'
)
BEGIN
    ALTER TABLE [Finance].[ChequeRegister] ADD [LegacyDocumentReferences] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005011038_AddCustomerOpeningMigration'
)
BEGIN
    ALTER TABLE [Finance].[ChequeRegister] ADD [LegacyReceiptReference] nvarchar(100) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005011038_AddCustomerOpeningMigration'
)
BEGIN
    ALTER TABLE [Finance].[ChequeRegister] ADD [MigrationBatchId] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005011038_AddCustomerOpeningMigration'
)
BEGIN
    ALTER TABLE [Finance].[ChequeRegister] ADD [OpeningReceivableAccountId] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005011038_AddCustomerOpeningMigration'
)
BEGIN
    ALTER TABLE [Finance].[ChequeRegister] ADD [OpeningSourceRow] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005011038_AddCustomerOpeningMigration'
)
BEGIN
    ALTER TABLE [Finance].[ChequeRegister] ADD [ReceivedDate] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005011038_AddCustomerOpeningMigration'
)
BEGIN
    CREATE TABLE [Finance].[CustomerMigrationBatches] (
        [Id] int NOT NULL IDENTITY,
        [CutoverDate] datetime2 NOT NULL,
        [SourceAsOfDate] datetime2 NOT NULL,
        [Status] nvarchar(16) NOT NULL,
        [SourceHash] nvarchar(64) NOT NULL,
        [ReviewHash] nvarchar(64) NOT NULL,
        [OutstandingFileName] nvarchar(255) NOT NULL,
        [ChequeFileName] nvarchar(255) NOT NULL,
        [OutstandingFile] varbinary(max) NOT NULL,
        [ChequeFile] varbinary(max) NOT NULL,
        [SourceJson] nvarchar(max) NOT NULL,
        [ReviewJson] nvarchar(max) NOT NULL,
        [JournalEntryId] int NULL,
        [ReversalJournalEntryId] int NULL,
        [PostedAt] datetime2 NULL,
        [ReversedDate] datetime2 NULL,
        [ReversalReason] nvarchar(max) NULL,
        [IsSignedOff] bit NOT NULL,
        [SignedOffAt] datetime2 NULL,
        [SignedOffBy] nvarchar(max) NULL,
        [ValidFrom] datetime2 GENERATED ALWAYS AS ROW START HIDDEN NOT NULL,
        [ValidTo] datetime2 GENERATED ALWAYS AS ROW END HIDDEN NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [LastModifiedAt] datetime2 NULL,
        [LastModifiedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_CustomerMigrationBatches] PRIMARY KEY ([Id]),
        PERIOD FOR SYSTEM_TIME([ValidFrom], [ValidTo])
    ) WITH (SYSTEM_VERSIONING = ON (HISTORY_TABLE = [Finance].[CustomerMigrationBatches_History]));
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005011038_AddCustomerOpeningMigration'
)
BEGIN
    CREATE TABLE [Sales].[CustomerOpeningItems] (
        [Id] int NOT NULL IDENTITY,
        [MigrationBatchId] int NOT NULL,
        [CustomerId] int NOT NULL,
        [ReceivableAccountId] int NOT NULL,
        [LegacyCustomerCode] nvarchar(50) NOT NULL,
        [ReferenceNo] nvarchar(100) NOT NULL,
        [Kind] nvarchar(40) NOT NULL,
        [DocumentDate] datetime2 NOT NULL,
        [PostingDate] datetime2 NOT NULL,
        [DueDate] datetime2 NULL,
        [Amount] decimal(18,2) NOT NULL,
        [SourceDebitAmount] decimal(18,2) NOT NULL,
        [AmountSettled] decimal(18,2) NOT NULL,
        [SourceRow] int NOT NULL,
        [Notes] nvarchar(max) NOT NULL,
        [ValidFrom] datetime2 GENERATED ALWAYS AS ROW START HIDDEN NOT NULL,
        [ValidTo] datetime2 GENERATED ALWAYS AS ROW END HIDDEN NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [LastModifiedAt] datetime2 NULL,
        [LastModifiedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_CustomerOpeningItems] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_CustomerOpeningItems_CustomerMigrationBatches_MigrationBatchId] FOREIGN KEY ([MigrationBatchId]) REFERENCES [Finance].[CustomerMigrationBatches] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_CustomerOpeningItems_Customers_CustomerId] FOREIGN KEY ([CustomerId]) REFERENCES [Sales].[Customers] ([Id]) ON DELETE NO ACTION,
        PERIOD FOR SYSTEM_TIME([ValidFrom], [ValidTo])
    ) WITH (SYSTEM_VERSIONING = ON (HISTORY_TABLE = [Sales].[CustomerOpeningItems_History]));
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005011038_AddCustomerOpeningMigration'
)
BEGIN
    CREATE TABLE [Finance].[CustomerCreditApplications] (
        [Id] int NOT NULL IDENTITY,
        [CreditOpeningItemId] int NOT NULL,
        [SalesInvoiceId] int NULL,
        [CustomerOpeningItemId] int NULL,
        [CustomerDebitMemoId] int NULL,
        [Amount] decimal(18,2) NOT NULL,
        [Date] datetime2 NOT NULL,
        [ReversedDate] datetime2 NULL,
        [ReversalReason] nvarchar(max) NULL,
        [ValidFrom] datetime2 GENERATED ALWAYS AS ROW START HIDDEN NOT NULL,
        [ValidTo] datetime2 GENERATED ALWAYS AS ROW END HIDDEN NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [LastModifiedAt] datetime2 NULL,
        [LastModifiedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_CustomerCreditApplications] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_CustomerCreditApplication_Target] CHECK ((CASE WHEN SalesInvoiceId IS NULL THEN 0 ELSE 1 END + CASE WHEN CustomerOpeningItemId IS NULL THEN 0 ELSE 1 END + CASE WHEN CustomerDebitMemoId IS NULL THEN 0 ELSE 1 END) = 1 AND Amount > 0),
        CONSTRAINT [FK_CustomerCreditApplications_CustomerDebitMemos_CustomerDebitMemoId] FOREIGN KEY ([CustomerDebitMemoId]) REFERENCES [Sales].[CustomerDebitMemos] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_CustomerCreditApplications_CustomerOpeningItems_CreditOpeningItemId] FOREIGN KEY ([CreditOpeningItemId]) REFERENCES [Sales].[CustomerOpeningItems] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_CustomerCreditApplications_CustomerOpeningItems_CustomerOpeningItemId] FOREIGN KEY ([CustomerOpeningItemId]) REFERENCES [Sales].[CustomerOpeningItems] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_CustomerCreditApplications_SalesInvoices_SalesInvoiceId] FOREIGN KEY ([SalesInvoiceId]) REFERENCES [Sales].[SalesInvoices] ([Id]) ON DELETE NO ACTION,
        PERIOD FOR SYSTEM_TIME([ValidFrom], [ValidTo])
    ) WITH (SYSTEM_VERSIONING = ON (HISTORY_TABLE = [Finance].[CustomerCreditApplications_History]));
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005011038_AddCustomerOpeningMigration'
)
BEGIN
    CREATE INDEX [IX_PaymentAllocations_CustomerDebitMemoId] ON [Finance].[PaymentAllocations] ([CustomerDebitMemoId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005011038_AddCustomerOpeningMigration'
)
BEGIN
    CREATE INDEX [IX_PaymentAllocations_CustomerOpeningItemId] ON [Finance].[PaymentAllocations] ([CustomerOpeningItemId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005011038_AddCustomerOpeningMigration'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Customers_LegacyCustomerCode] ON [Sales].[Customers] ([LegacyCustomerCode]) WHERE [LegacyCustomerCode] IS NOT NULL;');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005011038_AddCustomerOpeningMigration'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_ChequeRegister_MigrationBatchId_LegacyReceiptReference] ON [Finance].[ChequeRegister] ([MigrationBatchId], [LegacyReceiptReference]) WHERE [MigrationBatchId] IS NOT NULL;');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005011038_AddCustomerOpeningMigration'
)
BEGIN
    EXEC(N'ALTER TABLE [Finance].[ChequeRegister] ADD CONSTRAINT [CK_Cheque_Origin] CHECK (([MigrationBatchId] IS NULL AND [OriginalReceiptId] IS NOT NULL) OR ([MigrationBatchId] IS NOT NULL AND [OriginalReceiptId] IS NULL AND [OpeningReceivableAccountId] IS NOT NULL));');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005011038_AddCustomerOpeningMigration'
)
BEGIN
    CREATE INDEX [IX_CustomerCreditApplications_CreditOpeningItemId] ON [Finance].[CustomerCreditApplications] ([CreditOpeningItemId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005011038_AddCustomerOpeningMigration'
)
BEGIN
    CREATE INDEX [IX_CustomerCreditApplications_CustomerDebitMemoId] ON [Finance].[CustomerCreditApplications] ([CustomerDebitMemoId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005011038_AddCustomerOpeningMigration'
)
BEGIN
    CREATE INDEX [IX_CustomerCreditApplications_CustomerOpeningItemId] ON [Finance].[CustomerCreditApplications] ([CustomerOpeningItemId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005011038_AddCustomerOpeningMigration'
)
BEGIN
    CREATE INDEX [IX_CustomerCreditApplications_SalesInvoiceId] ON [Finance].[CustomerCreditApplications] ([SalesInvoiceId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005011038_AddCustomerOpeningMigration'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_CustomerMigrationBatches_SourceHash] ON [Finance].[CustomerMigrationBatches] ([SourceHash]) WHERE [Status] <> ''Reversed''');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005011038_AddCustomerOpeningMigration'
)
BEGIN
    CREATE INDEX [IX_CustomerOpeningItems_CustomerId] ON [Sales].[CustomerOpeningItems] ([CustomerId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005011038_AddCustomerOpeningMigration'
)
BEGIN
    CREATE INDEX [IX_CustomerOpeningItems_LegacyCustomerCode_ReferenceNo] ON [Sales].[CustomerOpeningItems] ([LegacyCustomerCode], [ReferenceNo]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005011038_AddCustomerOpeningMigration'
)
BEGIN
    CREATE UNIQUE INDEX [IX_CustomerOpeningItems_MigrationBatchId_SourceRow] ON [Sales].[CustomerOpeningItems] ([MigrationBatchId], [SourceRow]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005011038_AddCustomerOpeningMigration'
)
BEGIN
    ALTER TABLE [Finance].[ChequeRegister] ADD CONSTRAINT [FK_ChequeRegister_CustomerMigrationBatches_MigrationBatchId] FOREIGN KEY ([MigrationBatchId]) REFERENCES [Finance].[CustomerMigrationBatches] ([Id]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005011038_AddCustomerOpeningMigration'
)
BEGIN
    ALTER TABLE [Finance].[ChequeRegister] ADD CONSTRAINT [FK_ChequeRegister_PaymentTransactions_OriginalReceiptId] FOREIGN KEY ([OriginalReceiptId]) REFERENCES [Finance].[PaymentTransactions] ([Id]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005011038_AddCustomerOpeningMigration'
)
BEGIN
    ALTER TABLE [Finance].[PaymentAllocations] ADD CONSTRAINT [FK_PaymentAllocations_CustomerDebitMemos_CustomerDebitMemoId] FOREIGN KEY ([CustomerDebitMemoId]) REFERENCES [Sales].[CustomerDebitMemos] ([Id]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005011038_AddCustomerOpeningMigration'
)
BEGIN
    ALTER TABLE [Finance].[PaymentAllocations] ADD CONSTRAINT [FK_PaymentAllocations_CustomerOpeningItems_CustomerOpeningItemId] FOREIGN KEY ([CustomerOpeningItemId]) REFERENCES [Sales].[CustomerOpeningItems] ([Id]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005011038_AddCustomerOpeningMigration'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261005011038_AddCustomerOpeningMigration', N'10.0.5');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005014028_HardenCustomerMigrationControls'
)
BEGIN
    ALTER TABLE [Finance].[ChequeRegister] DROP CONSTRAINT [CK_Cheque_Origin];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005014028_HardenCustomerMigrationControls'
)
BEGIN
    ALTER TABLE [Finance].[ChequeRegister] ADD [OpeningUndepositedAccountId] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005014028_HardenCustomerMigrationControls'
)
BEGIN
    EXEC(N'ALTER TABLE [Finance].[PaymentAllocations] ADD CONSTRAINT [CK_PaymentAllocation_Target] CHECK ((CASE WHEN SalesInvoiceId IS NULL THEN 0 ELSE 1 END + CASE WHEN SupplierBillId IS NULL THEN 0 ELSE 1 END + CASE WHEN CustomerOpeningItemId IS NULL THEN 0 ELSE 1 END + CASE WHEN CustomerDebitMemoId IS NULL THEN 0 ELSE 1 END) = 1 AND AmountAllocated > 0);');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005014028_HardenCustomerMigrationControls'
)
BEGIN
    EXEC(N'ALTER TABLE [Finance].[ChequeRegister] ADD CONSTRAINT [CK_Cheque_Origin] CHECK (([MigrationBatchId] IS NULL AND [OriginalReceiptId] IS NOT NULL) OR ([MigrationBatchId] IS NOT NULL AND [OriginalReceiptId] IS NULL AND [OpeningReceivableAccountId] IS NOT NULL AND [OpeningUndepositedAccountId] IS NOT NULL));');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005014028_HardenCustomerMigrationControls'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261005014028_HardenCustomerMigrationControls', N'10.0.5');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006160821_AddMigrationCentre'
)
BEGIN
    DROP INDEX [IX_Suppliers_TaxRegNo] ON [Purchasing].[Suppliers];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006160821_AddMigrationCentre'
)
BEGIN
    ALTER TABLE [Finance].[PaymentAllocations] DROP CONSTRAINT [CK_PaymentAllocation_Target];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006160821_AddMigrationCentre'
)
BEGIN
    DROP INDEX [IX_ChequeRegister_MigrationBatchId_LegacyReceiptReference] ON [Finance].[ChequeRegister];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006160821_AddMigrationCentre'
)
BEGIN
    IF SCHEMA_ID(N'Migration') IS NULL EXEC(N'CREATE SCHEMA [Migration];');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006160821_AddMigrationCentre'
)
BEGIN
    ALTER TABLE [Purchasing].[Suppliers] ADD [DetailsIncomplete] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006160821_AddMigrationCentre'
)
BEGIN
    ALTER TABLE [Purchasing].[Suppliers] ADD [LegacySupplierCode] nvarchar(50) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006160821_AddMigrationCentre'
)
BEGIN
    DECLARE @var1 nvarchar(max);
    SELECT @var1 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Inventory].[StockTransactions]') AND [c].[name] = N'UnitCost');
    IF @var1 IS NOT NULL EXEC(N'ALTER TABLE [Inventory].[StockTransactions] DROP CONSTRAINT ' + @var1 + ';');
    ALTER TABLE [Inventory].[StockTransactions] ALTER COLUMN [UnitCost] decimal(22,6) NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006160821_AddMigrationCentre'
)
BEGIN
    DECLARE @var2 nvarchar(max);
    SELECT @var2 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Inventory].[StockTakeItems]') AND [c].[name] = N'UnitCost');
    IF @var2 IS NOT NULL EXEC(N'ALTER TABLE [Inventory].[StockTakeItems] DROP CONSTRAINT ' + @var2 + ';');
    ALTER TABLE [Inventory].[StockTakeItems] ALTER COLUMN [UnitCost] decimal(22,6) NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006160821_AddMigrationCentre'
)
BEGIN
    DECLARE @var3 nvarchar(max);
    SELECT @var3 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Inventory].[StockLayers]') AND [c].[name] = N'UnitCost');
    IF @var3 IS NOT NULL EXEC(N'ALTER TABLE [Inventory].[StockLayers] DROP CONSTRAINT ' + @var3 + ';');
    ALTER TABLE [Inventory].[StockLayers] ALTER COLUMN [UnitCost] decimal(22,6) NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006160821_AddMigrationCentre'
)
BEGIN
    DECLARE @var4 nvarchar(max);
    SELECT @var4 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Purchasing].[PurchaseOrderItems]') AND [c].[name] = N'UnitCost');
    IF @var4 IS NOT NULL EXEC(N'ALTER TABLE [Purchasing].[PurchaseOrderItems] DROP CONSTRAINT ' + @var4 + ';');
    ALTER TABLE [Purchasing].[PurchaseOrderItems] ALTER COLUMN [UnitCost] decimal(22,6) NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006160821_AddMigrationCentre'
)
BEGIN
    DECLARE @var5 nvarchar(max);
    SELECT @var5 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Master].[ProductVariants]') AND [c].[name] = N'MovingAverageCost');
    IF @var5 IS NOT NULL EXEC(N'ALTER TABLE [Master].[ProductVariants] DROP CONSTRAINT ' + @var5 + ';');
    ALTER TABLE [Master].[ProductVariants] ALTER COLUMN [MovingAverageCost] decimal(22,6) NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006160821_AddMigrationCentre'
)
BEGIN
    DECLARE @var6 nvarchar(max);
    SELECT @var6 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Master].[ProductVariants]') AND [c].[name] = N'CostPrice');
    IF @var6 IS NOT NULL EXEC(N'ALTER TABLE [Master].[ProductVariants] DROP CONSTRAINT ' + @var6 + ';');
    ALTER TABLE [Master].[ProductVariants] ALTER COLUMN [CostPrice] decimal(22,6) NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006160821_AddMigrationCentre'
)
BEGIN
    DECLARE @var7 nvarchar(max);
    SELECT @var7 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Inventory].[ProductionMaterialMovementLines]') AND [c].[name] = N'UnitCost');
    IF @var7 IS NOT NULL EXEC(N'ALTER TABLE [Inventory].[ProductionMaterialMovementLines] DROP CONSTRAINT ' + @var7 + ';');
    ALTER TABLE [Inventory].[ProductionMaterialMovementLines] ALTER COLUMN [UnitCost] decimal(22,6) NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006160821_AddMigrationCentre'
)
BEGIN
    ALTER TABLE [Finance].[PaymentAllocations] ADD [SupplierOpeningItemId] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006160821_AddMigrationCentre'
)
BEGIN
    DECLARE @var8 nvarchar(max);
    SELECT @var8 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Purchasing].[GRNItems]') AND [c].[name] = N'UnitCost');
    IF @var8 IS NOT NULL EXEC(N'ALTER TABLE [Purchasing].[GRNItems] DROP CONSTRAINT ' + @var8 + ';');
    ALTER TABLE [Purchasing].[GRNItems] ALTER COLUMN [UnitCost] decimal(22,6) NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006160821_AddMigrationCentre'
)
BEGIN
    ALTER TABLE [Sales].[Customers] ADD [DetailsIncomplete] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006160821_AddMigrationCentre'
)
BEGIN
    ALTER TABLE [Finance].[ChequeRegister] ADD [OpeningEndorsedSupplierId] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006160821_AddMigrationCentre'
)
BEGIN
    CREATE TABLE [Migration].[MigrationProjects] (
        [Id] int NOT NULL IDENTITY,
        [Name] nvarchar(120) NOT NULL,
        [SourceSystem] nvarchar(80) NOT NULL,
        [OpeningDate] datetime2 NOT NULL,
        [ClosingDate] datetime2 NOT NULL,
        [Currency] nvarchar(3) NOT NULL,
        [Status] nvarchar(24) NOT NULL,
        [SettingsJson] nvarchar(max) NOT NULL,
        [Version] int NOT NULL,
        [ApprovedHash] nvarchar(64) NULL,
        [ApprovedBy] nvarchar(450) NULL,
        [ApprovedAt] datetime2 NULL,
        [FinalizedAt] datetime2 NULL,
        [FinalEvidence] nvarchar(max) NULL,
        [ValidFrom] datetime2 GENERATED ALWAYS AS ROW START HIDDEN NOT NULL,
        [ValidTo] datetime2 GENERATED ALWAYS AS ROW END HIDDEN NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [LastModifiedAt] datetime2 NULL,
        [LastModifiedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_MigrationProjects] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_Migration_Dates] CHECK ([OpeningDate] = DATEADD(day, 1, [ClosingDate]) AND [Currency] = 'LKR'),
        PERIOD FOR SYSTEM_TIME([ValidFrom], [ValidTo])
    ) WITH (SYSTEM_VERSIONING = ON (HISTORY_TABLE = [Migration].[MigrationProjects_History]));
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006160821_AddMigrationCentre'
)
BEGIN
    CREATE TABLE [Migration].[MigrationAudit] (
        [Id] int NOT NULL IDENTITY,
        [ProjectId] int NOT NULL,
        [Action] nvarchar(40) NOT NULL,
        [Detail] nvarchar(max) NOT NULL,
        [ValidFrom] datetime2 GENERATED ALWAYS AS ROW START HIDDEN NOT NULL,
        [ValidTo] datetime2 GENERATED ALWAYS AS ROW END HIDDEN NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [LastModifiedAt] datetime2 NULL,
        [LastModifiedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_MigrationAudit] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_MigrationAudit_MigrationProjects_ProjectId] FOREIGN KEY ([ProjectId]) REFERENCES [Migration].[MigrationProjects] ([Id]) ON DELETE NO ACTION,
        PERIOD FOR SYSTEM_TIME([ValidFrom], [ValidTo])
    ) WITH (SYSTEM_VERSIONING = ON (HISTORY_TABLE = [Migration].[MigrationAudit_History]));
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006160821_AddMigrationCentre'
)
BEGIN
    CREATE TABLE [Migration].[MigrationDatasets] (
        [Id] int NOT NULL IDENTITY,
        [ProjectId] int NOT NULL,
        [Module] nvarchar(40) NOT NULL,
        [Revision] int NOT NULL,
        [IsCurrent] bit NOT NULL,
        [FileName] nvarchar(255) NOT NULL,
        [FileHash] nvarchar(64) NOT NULL,
        [StorageReference] nvarchar(max) NOT NULL,
        [ClosingDate] datetime2 NOT NULL,
        [RowsJson] nvarchar(max) NOT NULL,
        [ErrorsJson] nvarchar(max) NOT NULL,
        [ValidFrom] datetime2 GENERATED ALWAYS AS ROW START HIDDEN NOT NULL,
        [ValidTo] datetime2 GENERATED ALWAYS AS ROW END HIDDEN NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [LastModifiedAt] datetime2 NULL,
        [LastModifiedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_MigrationDatasets] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_MigrationDatasets_MigrationProjects_ProjectId] FOREIGN KEY ([ProjectId]) REFERENCES [Migration].[MigrationProjects] ([Id]) ON DELETE NO ACTION,
        PERIOD FOR SYSTEM_TIME([ValidFrom], [ValidTo])
    ) WITH (SYSTEM_VERSIONING = ON (HISTORY_TABLE = [Migration].[MigrationDatasets_History]));
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006160821_AddMigrationCentre'
)
BEGIN
    CREATE TABLE [Migration].[MigrationRuns] (
        [Id] int NOT NULL IDENTITY,
        [ProjectId] int NOT NULL,
        [ApprovedHash] nvarchar(64) NOT NULL,
        [Status] nvarchar(24) NOT NULL,
        [SelectionJson] nvarchar(max) NOT NULL,
        [JobId] nvarchar(100) NULL,
        [CommittedUnits] int NOT NULL,
        [Error] nvarchar(max) NULL,
        [CompletedAt] datetime2 NULL,
        [ValidFrom] datetime2 GENERATED ALWAYS AS ROW START HIDDEN NOT NULL,
        [ValidTo] datetime2 GENERATED ALWAYS AS ROW END HIDDEN NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [LastModifiedAt] datetime2 NULL,
        [LastModifiedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_MigrationRuns] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_MigrationRuns_MigrationProjects_ProjectId] FOREIGN KEY ([ProjectId]) REFERENCES [Migration].[MigrationProjects] ([Id]) ON DELETE NO ACTION,
        PERIOD FOR SYSTEM_TIME([ValidFrom], [ValidTo])
    ) WITH (SYSTEM_VERSIONING = ON (HISTORY_TABLE = [Migration].[MigrationRuns_History]));
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006160821_AddMigrationCentre'
)
BEGIN
    CREATE TABLE [Migration].[MigrationScopes] (
        [Id] int NOT NULL IDENTITY,
        [ProjectId] int NOT NULL,
        [Kind] nvarchar(24) NOT NULL,
        [Code] nvarchar(100) NOT NULL,
        [EntityId] int NULL,
        [Status] nvarchar(24) NOT NULL,
        [RequiredModulesJson] nvarchar(max) NOT NULL,
        [Evidence] nvarchar(max) NOT NULL,
        [ReleasedAt] datetime2 NULL,
        [ValidFrom] datetime2 GENERATED ALWAYS AS ROW START HIDDEN NOT NULL,
        [ValidTo] datetime2 GENERATED ALWAYS AS ROW END HIDDEN NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [LastModifiedAt] datetime2 NULL,
        [LastModifiedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_MigrationScopes] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_MigrationScopes_MigrationProjects_ProjectId] FOREIGN KEY ([ProjectId]) REFERENCES [Migration].[MigrationProjects] ([Id]) ON DELETE NO ACTION,
        PERIOD FOR SYSTEM_TIME([ValidFrom], [ValidTo])
    ) WITH (SYSTEM_VERSIONING = ON (HISTORY_TABLE = [Migration].[MigrationScopes_History]));
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006160821_AddMigrationCentre'
)
BEGIN
    CREATE TABLE [Migration].[MigrationUnits] (
        [Id] int NOT NULL IDENTITY,
        [ProjectId] int NOT NULL,
        [Module] nvarchar(40) NOT NULL,
        [ScopeKey] nvarchar(160) NOT NULL,
        [Hash] nvarchar(64) NOT NULL,
        [Status] nvarchar(24) NOT NULL,
        [JournalEntryId] int NULL,
        [ReversalJournalEntryId] int NULL,
        [ReversedDate] datetime2 NULL,
        [RowsJson] nvarchar(max) NOT NULL,
        [ValidFrom] datetime2 GENERATED ALWAYS AS ROW START HIDDEN NOT NULL,
        [ValidTo] datetime2 GENERATED ALWAYS AS ROW END HIDDEN NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [LastModifiedAt] datetime2 NULL,
        [LastModifiedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_MigrationUnits] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_MigrationUnits_JournalEntries_JournalEntryId] FOREIGN KEY ([JournalEntryId]) REFERENCES [Finance].[JournalEntries] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_MigrationUnits_JournalEntries_ReversalJournalEntryId] FOREIGN KEY ([ReversalJournalEntryId]) REFERENCES [Finance].[JournalEntries] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_MigrationUnits_MigrationProjects_ProjectId] FOREIGN KEY ([ProjectId]) REFERENCES [Migration].[MigrationProjects] ([Id]) ON DELETE NO ACTION,
        PERIOD FOR SYSTEM_TIME([ValidFrom], [ValidTo])
    ) WITH (SYSTEM_VERSIONING = ON (HISTORY_TABLE = [Migration].[MigrationUnits_History]));
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006160821_AddMigrationCentre'
)
BEGIN
    CREATE TABLE [Migration].[MigrationOrigins] (
        [Id] int NOT NULL IDENTITY,
        [IsCurrent] bit NOT NULL,
        [ProjectId] int NOT NULL,
        [UnitId] int NOT NULL,
        [SourceSystem] nvarchar(80) NOT NULL,
        [RecordKind] nvarchar(40) NOT NULL,
        [IdentityHash] nvarchar(64) NOT NULL,
        [ContentHash] nvarchar(64) NOT NULL,
        [LegacyRecordId] nvarchar(300) NOT NULL,
        [EntityKind] nvarchar(60) NOT NULL,
        [EntityId] int NOT NULL,
        [ValidFrom] datetime2 GENERATED ALWAYS AS ROW START HIDDEN NOT NULL,
        [ValidTo] datetime2 GENERATED ALWAYS AS ROW END HIDDEN NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [LastModifiedAt] datetime2 NULL,
        [LastModifiedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_MigrationOrigins] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_MigrationOrigins_MigrationUnits_UnitId] FOREIGN KEY ([UnitId]) REFERENCES [Migration].[MigrationUnits] ([Id]) ON DELETE NO ACTION,
        PERIOD FOR SYSTEM_TIME([ValidFrom], [ValidTo])
    ) WITH (SYSTEM_VERSIONING = ON (HISTORY_TABLE = [Migration].[MigrationOrigins_History]));
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006160821_AddMigrationCentre'
)
BEGIN
    CREATE TABLE [Finance].[OpeningBankItems] (
        [Id] int NOT NULL IDENTITY,
        [ChequeNumber] nvarchar(50) NOT NULL,
        [ChequeDate] datetime2 NULL,
        [ReceivedChequeId] int NULL,
        [MigrationUnitId] int NOT NULL,
        [BankAccountId] int NOT NULL,
        [SupplierId] int NULL,
        [Reference] nvarchar(100) NOT NULL,
        [Kind] nvarchar(40) NOT NULL,
        [Date] datetime2 NOT NULL,
        [Amount] decimal(18,2) NOT NULL,
        [LedgerAlreadyReflected] bit NOT NULL,
        [ClearedDate] datetime2 NULL,
        [CancelledDate] datetime2 NULL,
        [ResolutionJournalId] int NULL,
        [ValidFrom] datetime2 GENERATED ALWAYS AS ROW START HIDDEN NOT NULL,
        [ValidTo] datetime2 GENERATED ALWAYS AS ROW END HIDDEN NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [LastModifiedAt] datetime2 NULL,
        [LastModifiedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_OpeningBankItems] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_OpeningBankItems_Accounts_BankAccountId] FOREIGN KEY ([BankAccountId]) REFERENCES [Finance].[Accounts] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_OpeningBankItems_ChequeRegister_ReceivedChequeId] FOREIGN KEY ([ReceivedChequeId]) REFERENCES [Finance].[ChequeRegister] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_OpeningBankItems_MigrationUnits_MigrationUnitId] FOREIGN KEY ([MigrationUnitId]) REFERENCES [Migration].[MigrationUnits] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_OpeningBankItems_Suppliers_SupplierId] FOREIGN KEY ([SupplierId]) REFERENCES [Purchasing].[Suppliers] ([Id]) ON DELETE NO ACTION,
        PERIOD FOR SYSTEM_TIME([ValidFrom], [ValidTo])
    ) WITH (SYSTEM_VERSIONING = ON (HISTORY_TABLE = [Finance].[OpeningBankItems_History]));
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006160821_AddMigrationCentre'
)
BEGIN
    CREATE TABLE [Purchasing].[SupplierOpeningItems] (
        [Id] int NOT NULL IDENTITY,
        [MigrationUnitId] int NOT NULL,
        [SupplierId] int NOT NULL,
        [ReferenceNo] nvarchar(100) NOT NULL,
        [Kind] nvarchar(40) NOT NULL,
        [DocumentDate] datetime2 NOT NULL,
        [PostingDate] datetime2 NOT NULL,
        [DueDate] datetime2 NULL,
        [PayableAccountId] int NOT NULL,
        [Amount] decimal(18,2) NOT NULL,
        [ValidFrom] datetime2 GENERATED ALWAYS AS ROW START HIDDEN NOT NULL,
        [ValidTo] datetime2 GENERATED ALWAYS AS ROW END HIDDEN NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [LastModifiedAt] datetime2 NULL,
        [LastModifiedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_SupplierOpeningItems] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_SupplierOpeningItems_Accounts_PayableAccountId] FOREIGN KEY ([PayableAccountId]) REFERENCES [Finance].[Accounts] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_SupplierOpeningItems_MigrationUnits_MigrationUnitId] FOREIGN KEY ([MigrationUnitId]) REFERENCES [Migration].[MigrationUnits] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_SupplierOpeningItems_Suppliers_SupplierId] FOREIGN KEY ([SupplierId]) REFERENCES [Purchasing].[Suppliers] ([Id]) ON DELETE NO ACTION,
        PERIOD FOR SYSTEM_TIME([ValidFrom], [ValidTo])
    ) WITH (SYSTEM_VERSIONING = ON (HISTORY_TABLE = [Purchasing].[SupplierOpeningItems_History]));
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006160821_AddMigrationCentre'
)
BEGIN
    CREATE TABLE [Finance].[SupplierCreditApplications] (
        [Id] int NOT NULL IDENTITY,
        [CreditOpeningItemId] int NOT NULL,
        [SupplierOpeningItemId] int NULL,
        [SupplierBillId] int NULL,
        [Amount] decimal(18,2) NOT NULL,
        [Date] datetime2 NOT NULL,
        [ValidFrom] datetime2 GENERATED ALWAYS AS ROW START HIDDEN NOT NULL,
        [ValidTo] datetime2 GENERATED ALWAYS AS ROW END HIDDEN NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [LastModifiedAt] datetime2 NULL,
        [LastModifiedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_SupplierCreditApplications] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_SupplierCredit_Target] CHECK ([Amount] > 0 AND (([SupplierOpeningItemId] IS NULL AND [SupplierBillId] IS NOT NULL) OR ([SupplierOpeningItemId] IS NOT NULL AND [SupplierBillId] IS NULL))),
        CONSTRAINT [FK_SupplierCreditApplications_SupplierBills_SupplierBillId] FOREIGN KEY ([SupplierBillId]) REFERENCES [Purchasing].[SupplierBills] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_SupplierCreditApplications_SupplierOpeningItems_CreditOpeningItemId] FOREIGN KEY ([CreditOpeningItemId]) REFERENCES [Purchasing].[SupplierOpeningItems] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_SupplierCreditApplications_SupplierOpeningItems_SupplierOpeningItemId] FOREIGN KEY ([SupplierOpeningItemId]) REFERENCES [Purchasing].[SupplierOpeningItems] ([Id]) ON DELETE NO ACTION,
        PERIOD FOR SYSTEM_TIME([ValidFrom], [ValidTo])
    ) WITH (SYSTEM_VERSIONING = ON (HISTORY_TABLE = [Finance].[SupplierCreditApplications_History]));
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006160821_AddMigrationCentre'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Suppliers_LegacySupplierCode] ON [Purchasing].[Suppliers] ([LegacySupplierCode]) WHERE [LegacySupplierCode] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006160821_AddMigrationCentre'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Suppliers_TaxRegNo] ON [Purchasing].[Suppliers] ([TaxRegNo]) WHERE [TaxRegNo] <> N''''');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006160821_AddMigrationCentre'
)
BEGIN
    CREATE INDEX [IX_PaymentAllocations_SupplierOpeningItemId] ON [Finance].[PaymentAllocations] ([SupplierOpeningItemId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006160821_AddMigrationCentre'
)
BEGIN
    EXEC(N'ALTER TABLE [Finance].[PaymentAllocations] ADD CONSTRAINT [CK_PaymentAllocation_Target] CHECK ((CASE WHEN SalesInvoiceId IS NULL THEN 0 ELSE 1 END + CASE WHEN SupplierBillId IS NULL THEN 0 ELSE 1 END + CASE WHEN CustomerOpeningItemId IS NULL THEN 0 ELSE 1 END + CASE WHEN CustomerDebitMemoId IS NULL THEN 0 ELSE 1 END + CASE WHEN SupplierOpeningItemId IS NULL THEN 0 ELSE 1 END) = 1 AND AmountAllocated > 0)');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006160821_AddMigrationCentre'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_ChequeRegister_MigrationBatchId_LegacyReceiptReference] ON [Finance].[ChequeRegister] ([MigrationBatchId], [LegacyReceiptReference]) WHERE [MigrationBatchId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006160821_AddMigrationCentre'
)
BEGIN
    CREATE INDEX [IX_ChequeRegister_OpeningEndorsedSupplierId] ON [Finance].[ChequeRegister] ([OpeningEndorsedSupplierId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006160821_AddMigrationCentre'
)
BEGIN
    CREATE INDEX [IX_MigrationAudit_ProjectId] ON [Migration].[MigrationAudit] ([ProjectId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006160821_AddMigrationCentre'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_MigrationDatasets_ProjectId_Module] ON [Migration].[MigrationDatasets] ([ProjectId], [Module]) WHERE [IsCurrent] = 1');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006160821_AddMigrationCentre'
)
BEGIN
    CREATE UNIQUE INDEX [IX_MigrationDatasets_ProjectId_Module_Revision] ON [Migration].[MigrationDatasets] ([ProjectId], [Module], [Revision]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006160821_AddMigrationCentre'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_MigrationOrigins_SourceSystem_RecordKind_IdentityHash] ON [Migration].[MigrationOrigins] ([SourceSystem], [RecordKind], [IdentityHash]) WHERE [IsCurrent] = 1');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006160821_AddMigrationCentre'
)
BEGIN
    CREATE INDEX [IX_MigrationOrigins_UnitId] ON [Migration].[MigrationOrigins] ([UnitId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006160821_AddMigrationCentre'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_MigrationProjects_Status] ON [Migration].[MigrationProjects] ([Status]) WHERE [Status] <> ''Finalized''');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006160821_AddMigrationCentre'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_MigrationRuns_ProjectId] ON [Migration].[MigrationRuns] ([ProjectId]) WHERE [Status] IN (''Queued'',''Running'')');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006160821_AddMigrationCentre'
)
BEGIN
    CREATE UNIQUE INDEX [IX_MigrationScopes_ProjectId_Kind_Code] ON [Migration].[MigrationScopes] ([ProjectId], [Kind], [Code]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006160821_AddMigrationCentre'
)
BEGIN
    CREATE INDEX [IX_MigrationUnits_JournalEntryId] ON [Migration].[MigrationUnits] ([JournalEntryId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006160821_AddMigrationCentre'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_MigrationUnits_ProjectId_Module_ScopeKey] ON [Migration].[MigrationUnits] ([ProjectId], [Module], [ScopeKey]) WHERE [Status] = ''Posted''');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006160821_AddMigrationCentre'
)
BEGIN
    CREATE INDEX [IX_MigrationUnits_ReversalJournalEntryId] ON [Migration].[MigrationUnits] ([ReversalJournalEntryId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006160821_AddMigrationCentre'
)
BEGIN
    CREATE INDEX [IX_OpeningBankItems_BankAccountId] ON [Finance].[OpeningBankItems] ([BankAccountId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006160821_AddMigrationCentre'
)
BEGIN
    CREATE INDEX [IX_OpeningBankItems_MigrationUnitId] ON [Finance].[OpeningBankItems] ([MigrationUnitId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006160821_AddMigrationCentre'
)
BEGIN
    CREATE INDEX [IX_OpeningBankItems_ReceivedChequeId] ON [Finance].[OpeningBankItems] ([ReceivedChequeId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006160821_AddMigrationCentre'
)
BEGIN
    CREATE INDEX [IX_OpeningBankItems_SupplierId] ON [Finance].[OpeningBankItems] ([SupplierId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006160821_AddMigrationCentre'
)
BEGIN
    CREATE INDEX [IX_SupplierCreditApplications_CreditOpeningItemId] ON [Finance].[SupplierCreditApplications] ([CreditOpeningItemId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006160821_AddMigrationCentre'
)
BEGIN
    CREATE INDEX [IX_SupplierCreditApplications_SupplierBillId] ON [Finance].[SupplierCreditApplications] ([SupplierBillId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006160821_AddMigrationCentre'
)
BEGIN
    CREATE INDEX [IX_SupplierCreditApplications_SupplierOpeningItemId] ON [Finance].[SupplierCreditApplications] ([SupplierOpeningItemId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006160821_AddMigrationCentre'
)
BEGIN
    CREATE INDEX [IX_SupplierOpeningItems_MigrationUnitId] ON [Purchasing].[SupplierOpeningItems] ([MigrationUnitId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006160821_AddMigrationCentre'
)
BEGIN
    CREATE INDEX [IX_SupplierOpeningItems_PayableAccountId] ON [Purchasing].[SupplierOpeningItems] ([PayableAccountId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006160821_AddMigrationCentre'
)
BEGIN
    CREATE INDEX [IX_SupplierOpeningItems_SupplierId] ON [Purchasing].[SupplierOpeningItems] ([SupplierId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006160821_AddMigrationCentre'
)
BEGIN
    ALTER TABLE [Finance].[ChequeRegister] ADD CONSTRAINT [FK_ChequeRegister_Suppliers_OpeningEndorsedSupplierId] FOREIGN KEY ([OpeningEndorsedSupplierId]) REFERENCES [Purchasing].[Suppliers] ([Id]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006160821_AddMigrationCentre'
)
BEGIN
    ALTER TABLE [Finance].[PaymentAllocations] ADD CONSTRAINT [FK_PaymentAllocations_SupplierOpeningItems_SupplierOpeningItemId] FOREIGN KEY ([SupplierOpeningItemId]) REFERENCES [Purchasing].[SupplierOpeningItems] ([Id]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006160821_AddMigrationCentre'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261006160821_AddMigrationCentre', N'10.0.5');
END;

COMMIT;
GO

