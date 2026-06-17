using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NexusFlow.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MoveDocumentRenderingToSystemSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
IF SCHEMA_ID(N'System') IS NULL
    EXEC(N'CREATE SCHEMA [System]');

IF OBJECT_ID(N'[dbo].[DocumentTemplates]', N'U') IS NOT NULL
BEGIN
    IF EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID(N'[dbo].[DocumentTemplates]') AND temporal_type = 2)
        EXEC(N'ALTER TABLE [dbo].[DocumentTemplates] SET (SYSTEM_VERSIONING = OFF)');

    EXEC(N'ALTER SCHEMA [System] TRANSFER [dbo].[DocumentTemplates]');

    IF OBJECT_ID(N'[dbo].[DocumentTemplates_History]', N'U') IS NOT NULL
        EXEC(N'ALTER SCHEMA [System] TRANSFER [dbo].[DocumentTemplates_History]');
END;

IF OBJECT_ID(N'[System].[DocumentTemplates]', N'U') IS NOT NULL
BEGIN
    IF EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID(N'[System].[DocumentTemplates]') AND temporal_type = 2)
        EXEC(N'ALTER TABLE [System].[DocumentTemplates] SET (SYSTEM_VERSIONING = OFF)');

    IF OBJECT_ID(N'[dbo].[DocumentTemplates_History]', N'U') IS NOT NULL
        EXEC(N'ALTER SCHEMA [System] TRANSFER [dbo].[DocumentTemplates_History]');

    EXEC(N'ALTER TABLE [System].[DocumentTemplates] ALTER COLUMN [TemplateName] nvarchar(150) NOT NULL');
    EXEC(N'ALTER TABLE [System].[DocumentTemplates] ALTER COLUMN [BlobUrl] nvarchar(500) NOT NULL');

    IF OBJECT_ID(N'[System].[DocumentTemplates_History]', N'U') IS NOT NULL
    BEGIN
        EXEC(N'ALTER TABLE [System].[DocumentTemplates_History] ALTER COLUMN [TemplateName] nvarchar(150) NOT NULL');
        EXEC(N'ALTER TABLE [System].[DocumentTemplates_History] ALTER COLUMN [BlobUrl] nvarchar(500) NOT NULL');
    END;

    IF OBJECT_ID(N'[System].[DocumentTemplates_History]', N'U') IS NOT NULL
        EXEC(N'ALTER TABLE [System].[DocumentTemplates] SET (SYSTEM_VERSIONING = ON (HISTORY_TABLE = [System].[DocumentTemplates_History], DATA_CONSISTENCY_CHECK = ON))');
END;

IF OBJECT_ID(N'[dbo].[CompanyProfiles]', N'U') IS NOT NULL
BEGIN
    IF EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID(N'[dbo].[CompanyProfiles]') AND temporal_type = 2)
        EXEC(N'ALTER TABLE [dbo].[CompanyProfiles] SET (SYSTEM_VERSIONING = OFF)');

    EXEC(N'ALTER SCHEMA [System] TRANSFER [dbo].[CompanyProfiles]');

    IF OBJECT_ID(N'[dbo].[CompanyProfiles_History]', N'U') IS NOT NULL
        EXEC(N'ALTER SCHEMA [System] TRANSFER [dbo].[CompanyProfiles_History]');
END;

IF OBJECT_ID(N'[System].[CompanyProfiles]', N'U') IS NOT NULL
BEGIN
    IF EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID(N'[System].[CompanyProfiles]') AND temporal_type = 2)
        EXEC(N'ALTER TABLE [System].[CompanyProfiles] SET (SYSTEM_VERSIONING = OFF)');

    IF OBJECT_ID(N'[dbo].[CompanyProfiles_History]', N'U') IS NOT NULL
        EXEC(N'ALTER SCHEMA [System] TRANSFER [dbo].[CompanyProfiles_History]');

    EXEC(N'ALTER TABLE [System].[CompanyProfiles] ALTER COLUMN [CompanyName] nvarchar(200) NULL');
    EXEC(N'ALTER TABLE [System].[CompanyProfiles] ALTER COLUMN [TaxRegistrationNumber] nvarchar(100) NULL');
    EXEC(N'ALTER TABLE [System].[CompanyProfiles] ALTER COLUMN [BusinessRegistrationNumber] nvarchar(100) NULL');
    EXEC(N'ALTER TABLE [System].[CompanyProfiles] ALTER COLUMN [ContactEmail] nvarchar(200) NULL');
    EXEC(N'ALTER TABLE [System].[CompanyProfiles] ALTER COLUMN [ContactPhone] nvarchar(50) NULL');
    EXEC(N'ALTER TABLE [System].[CompanyProfiles] ALTER COLUMN [LogoBlobUrl] nvarchar(500) NULL');

    IF OBJECT_ID(N'[System].[CompanyProfiles_History]', N'U') IS NOT NULL
    BEGIN
        EXEC(N'ALTER TABLE [System].[CompanyProfiles_History] ALTER COLUMN [CompanyName] nvarchar(200) NULL');
        EXEC(N'ALTER TABLE [System].[CompanyProfiles_History] ALTER COLUMN [TaxRegistrationNumber] nvarchar(100) NULL');
        EXEC(N'ALTER TABLE [System].[CompanyProfiles_History] ALTER COLUMN [BusinessRegistrationNumber] nvarchar(100) NULL');
        EXEC(N'ALTER TABLE [System].[CompanyProfiles_History] ALTER COLUMN [ContactEmail] nvarchar(200) NULL');
        EXEC(N'ALTER TABLE [System].[CompanyProfiles_History] ALTER COLUMN [ContactPhone] nvarchar(50) NULL');
        EXEC(N'ALTER TABLE [System].[CompanyProfiles_History] ALTER COLUMN [LogoBlobUrl] nvarchar(500) NULL');
    END;

    IF OBJECT_ID(N'[System].[CompanyProfiles_History]', N'U') IS NOT NULL
        EXEC(N'ALTER TABLE [System].[CompanyProfiles] SET (SYSTEM_VERSIONING = ON (HISTORY_TABLE = [System].[CompanyProfiles_History], DATA_CONSISTENCY_CHECK = ON))');
END;

IF OBJECT_ID(N'[System].[DocumentTemplates]', N'U') IS NOT NULL
   AND NOT EXISTS (
        SELECT 1
        FROM sys.indexes
        WHERE name = N'IX_DocumentTemplates_DefaultPerTypeTax'
          AND object_id = OBJECT_ID(N'[System].[DocumentTemplates]'))
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_DocumentTemplates_DefaultPerTypeTax] ON [System].[DocumentTemplates] ([DocumentType], [TaxProfile], [IsDefault]) WHERE [IsDefault] = 1');
END;
""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
IF OBJECT_ID(N'[System].[DocumentTemplates]', N'U') IS NOT NULL
   AND EXISTS (
        SELECT 1
        FROM sys.indexes
        WHERE name = N'IX_DocumentTemplates_DefaultPerTypeTax'
          AND object_id = OBJECT_ID(N'[System].[DocumentTemplates]'))
BEGIN
    EXEC(N'DROP INDEX [IX_DocumentTemplates_DefaultPerTypeTax] ON [System].[DocumentTemplates]');
END;

IF OBJECT_ID(N'[System].[DocumentTemplates]', N'U') IS NOT NULL
BEGIN
    IF EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID(N'[System].[DocumentTemplates]') AND temporal_type = 2)
        EXEC(N'ALTER TABLE [System].[DocumentTemplates] SET (SYSTEM_VERSIONING = OFF)');

    EXEC(N'ALTER TABLE [System].[DocumentTemplates] ALTER COLUMN [TemplateName] nvarchar(max) NOT NULL');
    EXEC(N'ALTER TABLE [System].[DocumentTemplates] ALTER COLUMN [BlobUrl] nvarchar(max) NOT NULL');

    IF OBJECT_ID(N'[System].[DocumentTemplates_History]', N'U') IS NOT NULL
    BEGIN
        EXEC(N'ALTER TABLE [System].[DocumentTemplates_History] ALTER COLUMN [TemplateName] nvarchar(max) NOT NULL');
        EXEC(N'ALTER TABLE [System].[DocumentTemplates_History] ALTER COLUMN [BlobUrl] nvarchar(max) NOT NULL');
    END;

    EXEC(N'ALTER SCHEMA [dbo] TRANSFER [System].[DocumentTemplates]');

    IF OBJECT_ID(N'[System].[DocumentTemplates_History]', N'U') IS NOT NULL
        EXEC(N'ALTER SCHEMA [dbo] TRANSFER [System].[DocumentTemplates_History]');

    IF OBJECT_ID(N'[dbo].[DocumentTemplates_History]', N'U') IS NOT NULL
        EXEC(N'ALTER TABLE [dbo].[DocumentTemplates] SET (SYSTEM_VERSIONING = ON (HISTORY_TABLE = [dbo].[DocumentTemplates_History], DATA_CONSISTENCY_CHECK = ON))');
END;

IF OBJECT_ID(N'[System].[CompanyProfiles]', N'U') IS NOT NULL
BEGIN
    IF EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID(N'[System].[CompanyProfiles]') AND temporal_type = 2)
        EXEC(N'ALTER TABLE [System].[CompanyProfiles] SET (SYSTEM_VERSIONING = OFF)');

    EXEC(N'ALTER TABLE [System].[CompanyProfiles] ALTER COLUMN [CompanyName] nvarchar(max) NULL');
    EXEC(N'ALTER TABLE [System].[CompanyProfiles] ALTER COLUMN [TaxRegistrationNumber] nvarchar(max) NULL');
    EXEC(N'ALTER TABLE [System].[CompanyProfiles] ALTER COLUMN [BusinessRegistrationNumber] nvarchar(max) NULL');
    EXEC(N'ALTER TABLE [System].[CompanyProfiles] ALTER COLUMN [ContactEmail] nvarchar(max) NULL');
    EXEC(N'ALTER TABLE [System].[CompanyProfiles] ALTER COLUMN [ContactPhone] nvarchar(max) NULL');
    EXEC(N'ALTER TABLE [System].[CompanyProfiles] ALTER COLUMN [LogoBlobUrl] nvarchar(max) NULL');

    IF OBJECT_ID(N'[System].[CompanyProfiles_History]', N'U') IS NOT NULL
    BEGIN
        EXEC(N'ALTER TABLE [System].[CompanyProfiles_History] ALTER COLUMN [CompanyName] nvarchar(max) NULL');
        EXEC(N'ALTER TABLE [System].[CompanyProfiles_History] ALTER COLUMN [TaxRegistrationNumber] nvarchar(max) NULL');
        EXEC(N'ALTER TABLE [System].[CompanyProfiles_History] ALTER COLUMN [BusinessRegistrationNumber] nvarchar(max) NULL');
        EXEC(N'ALTER TABLE [System].[CompanyProfiles_History] ALTER COLUMN [ContactEmail] nvarchar(max) NULL');
        EXEC(N'ALTER TABLE [System].[CompanyProfiles_History] ALTER COLUMN [ContactPhone] nvarchar(max) NULL');
        EXEC(N'ALTER TABLE [System].[CompanyProfiles_History] ALTER COLUMN [LogoBlobUrl] nvarchar(max) NULL');
    END;

    EXEC(N'ALTER SCHEMA [dbo] TRANSFER [System].[CompanyProfiles]');

    IF OBJECT_ID(N'[System].[CompanyProfiles_History]', N'U') IS NOT NULL
        EXEC(N'ALTER SCHEMA [dbo] TRANSFER [System].[CompanyProfiles_History]');

    IF OBJECT_ID(N'[dbo].[CompanyProfiles_History]', N'U') IS NOT NULL
        EXEC(N'ALTER TABLE [dbo].[CompanyProfiles] SET (SYSTEM_VERSIONING = ON (HISTORY_TABLE = [dbo].[CompanyProfiles_History], DATA_CONSISTENCY_CHECK = ON))');
END;
""");
        }
    }
}
