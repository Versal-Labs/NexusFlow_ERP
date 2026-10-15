-- Read-only preflight for the manually reviewed Migration Centre UAT upgrade.
-- Select the confirmed UAT database and replace the value below. Keep the restoration backup independently.
SET NOCOUNT ON;
DECLARE @ExpectedDatabase sysname = N'REPLACE_WITH_EXACT_UAT_DATABASE';
IF @ExpectedDatabase = N'REPLACE_WITH_EXACT_UAT_DATABASE' OR DB_NAME() <> @ExpectedDatabase
    THROW 51000, 'Set the exact UAT database name and select that database before proceeding.', 1;
SELECT DB_NAME() AS ConfirmedDatabase, @@SERVERNAME AS SqlServer;
IF OBJECT_ID(N'dbo.__EFMigrationsHistory', N'U') IS NULL
    THROW 51001, 'Migration history missing. This upgrade is not a new-database installer.', 1;
IF NOT EXISTS (SELECT 1 FROM dbo.__EFMigrationsHistory WHERE MigrationId = N'20260622171142_FinancialControlsDocumentsAndProduction')
    THROW 51002, 'Required financial baseline missing. Review the full older upgrade path separately.', 1;
IF EXISTS (SELECT 1 FROM dbo.__EFMigrationsHistory WHERE MigrationId > N'20261006160821_AddMigrationCentre')
    THROW 51003, 'Later schema migrations exist. Review release compatibility before applying this snapshot.', 1;
SELECT MigrationId, ProductVersion FROM dbo.__EFMigrationsHistory ORDER BY MigrationId;
SELECT s.name AS SchemaName, t.name AS TableName, t.temporal_type_desc
FROM sys.tables t JOIN sys.schemas s ON t.schema_id = s.schema_id
WHERE t.name IN ('Customers','Suppliers','StockLayers','ChequeRegister','MigrationProjects','MigrationUnits','SupplierOpeningItems','OpeningBankItems')
ORDER BY s.name, t.name;
SELECT N'Identity/baseline preflight passed. This does not replace restored-copy rehearsal, constraint/data review, backup verification or business acceptance.' AS Result;
