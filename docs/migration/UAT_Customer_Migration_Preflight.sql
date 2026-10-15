-- READ ONLY. Replace the database name; select that same UAT database in SSMS.
-- Do not run against production or use this script to repair records.
DECLARE @ExpectedDatabase sysname = N'REPLACE_WITH_UAT_DATABASE';
SELECT DB_NAME() AS SelectedDatabase, @@SERVERNAME AS SqlServer;
IF @ExpectedDatabase = N'REPLACE_WITH_UAT_DATABASE' OR DB_NAME() <> @ExpectedDatabase
    THROW 51000, 'Set @ExpectedDatabase to the exact UAT database and select it before running.', 1;
IF OBJECT_ID(N'dbo.__EFMigrationsHistory', N'U') IS NULL
    THROW 51001, 'No EF migration history. This customer upgrade is not a new-database installer.', 1;
SELECT MigrationId, ProductVersion FROM dbo.__EFMigrationsHistory ORDER BY MigrationId;
IF NOT EXISTS (SELECT 1 FROM dbo.__EFMigrationsHistory WHERE MigrationId = N'20260622171142_FinancialControlsDocumentsAndProduction')
    THROW 51002, 'Required baseline missing. Review older pending migrations before this release.', 1;
IF EXISTS (SELECT 1 FROM dbo.__EFMigrationsHistory WHERE MigrationId > N'20261005014028_HardenCustomerMigrationControls')
    THROW 51003, 'Database contains later migrations. Review release compatibility before proceeding.', 1;

IF COL_LENGTH(N'Finance.PaymentAllocations', N'CustomerOpeningItemId') IS NULL
    EXEC(N'SELECT Id, AmountAllocated, SalesInvoiceId, SupplierBillId
        FROM Finance.PaymentAllocations WHERE AmountAllocated <= 0 OR
        (CASE WHEN SalesInvoiceId IS NULL THEN 0 ELSE 1 END + CASE WHEN SupplierBillId IS NULL THEN 0 ELSE 1 END) <> 1;
        IF EXISTS (SELECT 1 FROM Finance.PaymentAllocations WHERE AmountAllocated <= 0 OR
        (CASE WHEN SalesInvoiceId IS NULL THEN 0 ELSE 1 END + CASE WHEN SupplierBillId IS NULL THEN 0 ELSE 1 END) <> 1)
        THROW 51004, ''Invalid payment allocations. Review and repair before upgrading.'', 1;');
ELSE
    EXEC(N'SELECT Id, AmountAllocated FROM Finance.PaymentAllocations WHERE AmountAllocated <= 0 OR
        (CASE WHEN SalesInvoiceId IS NULL THEN 0 ELSE 1 END + CASE WHEN SupplierBillId IS NULL THEN 0 ELSE 1 END +
        CASE WHEN CustomerOpeningItemId IS NULL THEN 0 ELSE 1 END + CASE WHEN CustomerDebitMemoId IS NULL THEN 0 ELSE 1 END) <> 1;
        IF EXISTS (SELECT 1 FROM Finance.PaymentAllocations WHERE AmountAllocated <= 0 OR
        (CASE WHEN SalesInvoiceId IS NULL THEN 0 ELSE 1 END + CASE WHEN SupplierBillId IS NULL THEN 0 ELSE 1 END +
        CASE WHEN CustomerOpeningItemId IS NULL THEN 0 ELSE 1 END + CASE WHEN CustomerDebitMemoId IS NULL THEN 0 ELSE 1 END) <> 1)
        THROW 51004, ''Invalid payment allocations. Review and repair before upgrading.'', 1;');

IF COL_LENGTH(N'Finance.ChequeRegister', N'MigrationBatchId') IS NULL
    EXEC(N'SELECT Id FROM Finance.ChequeRegister WHERE OriginalReceiptId IS NULL;
        IF EXISTS (SELECT 1 FROM Finance.ChequeRegister WHERE OriginalReceiptId IS NULL)
        THROW 51005, ''Existing cheque without original receipt. Review before upgrading.'', 1;');
ELSE
    EXEC(N'SELECT Id FROM Finance.ChequeRegister WHERE MigrationBatchId IS NULL AND OriginalReceiptId IS NULL;
        IF EXISTS (SELECT 1 FROM Finance.ChequeRegister WHERE MigrationBatchId IS NULL AND OriginalReceiptId IS NULL)
        THROW 51005, ''Existing ordinary cheque without original receipt. Review before upgrading.'', 1;');

IF COL_LENGTH(N'Sales.Customers', N'LegacyCustomerCode') IS NOT NULL
    EXEC(N'SELECT LegacyCustomerCode COLLATE Latin1_General_100_CI_AS AS LegacyCustomerCode, COUNT(*) AS CustomerCount FROM Sales.Customers
        WHERE LegacyCustomerCode IS NOT NULL GROUP BY LegacyCustomerCode COLLATE Latin1_General_100_CI_AS HAVING COUNT(*) > 1;
        IF EXISTS (SELECT 1 FROM Sales.Customers WHERE LegacyCustomerCode IS NOT NULL
        GROUP BY LegacyCustomerCode COLLATE Latin1_General_100_CI_AS HAVING COUNT(*) > 1)
        THROW 51006, ''Duplicate legacy customer codes. Review before upgrading.'', 1;');
SELECT N'Preflight passed. Preserve backups/configuration; apply only the reviewed UAT release.' AS Result;
