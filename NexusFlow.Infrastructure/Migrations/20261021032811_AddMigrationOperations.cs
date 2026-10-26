using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NexusFlow.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMigrationOperations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MigrationOperations",
                schema: "Migration",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProjectId = table.Column<int>(type: "int", nullable: false),
                    ClientRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                    InitiatedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    InitiatedByName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    InputFingerprint = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    InputVersion = table.Column<int>(type: "int", nullable: false),
                    JobId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RunId = table.Column<int>(type: "int", nullable: true),
                    StartedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Phase = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    CurrentItem = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Total = table.Column<int>(type: "int", nullable: true),
                    Processed = table.Column<int>(type: "int", nullable: false),
                    Succeeded = table.Column<int>(type: "int", nullable: false),
                    NeedsAttention = table.Column<int>(type: "int", nullable: false),
                    Error = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    ResultHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    CanApprove = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MigrationOperations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MigrationOperations_MigrationProjects_ProjectId",
                        column: x => x.ProjectId,
                        principalSchema: "Migration",
                        principalTable: "MigrationProjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MigrationOperations_MigrationRuns_RunId",
                        column: x => x.RunId,
                        principalSchema: "Migration",
                        principalTable: "MigrationRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MigrationRunPayloads",
                schema: "Migration",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RunId = table.Column<int>(type: "int", nullable: false),
                    PayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MigrationRunPayloads", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MigrationRunPayloads_MigrationRuns_RunId",
                        column: x => x.RunId,
                        principalSchema: "Migration",
                        principalTable: "MigrationRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MigrationOperationItems",
                schema: "Migration",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OperationId = table.Column<int>(type: "int", nullable: false),
                    Ordinal = table.Column<int>(type: "int", nullable: false),
                    Module = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    ScopeId = table.Column<int>(type: "int", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                    Message = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    Rows = table.Column<int>(type: "int", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MigrationOperationItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MigrationOperationItems_MigrationOperations_OperationId",
                        column: x => x.OperationId,
                        principalSchema: "Migration",
                        principalTable: "MigrationOperations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MigrationOperationPayloads",
                schema: "Migration",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OperationId = table.Column<int>(type: "int", nullable: false),
                    RequestJson = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MigrationOperationPayloads", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MigrationOperationPayloads_MigrationOperations_OperationId",
                        column: x => x.OperationId,
                        principalSchema: "Migration",
                        principalTable: "MigrationOperations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MigrationOperationIssues",
                schema: "Migration",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OperationId = table.Column<int>(type: "int", nullable: false),
                    ItemId = table.Column<int>(type: "int", nullable: true),
                    Severity = table.Column<string>(type: "nvarchar(12)", maxLength: 12, nullable: false),
                    Module = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    Row = table.Column<int>(type: "int", nullable: false),
                    AffectedRows = table.Column<int>(type: "int", nullable: false),
                    Message = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MigrationOperationIssues", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MigrationOperationIssues_MigrationOperationItems_ItemId",
                        column: x => x.ItemId,
                        principalSchema: "Migration",
                        principalTable: "MigrationOperationItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MigrationOperationIssues_MigrationOperations_OperationId",
                        column: x => x.OperationId,
                        principalSchema: "Migration",
                        principalTable: "MigrationOperations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MigrationOperationItemPayloads",
                schema: "Migration",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ItemId = table.Column<int>(type: "int", nullable: false),
                    DetailJson = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MigrationOperationItemPayloads", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MigrationOperationItemPayloads_MigrationOperationItems_ItemId",
                        column: x => x.ItemId,
                        principalSchema: "Migration",
                        principalTable: "MigrationOperationItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MigrationOperationIssues_ItemId",
                schema: "Migration",
                table: "MigrationOperationIssues",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_MigrationOperationIssues_OperationId_Severity_Id",
                schema: "Migration",
                table: "MigrationOperationIssues",
                columns: new[] { "OperationId", "Severity", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_MigrationOperationItemPayloads_ItemId",
                schema: "Migration",
                table: "MigrationOperationItemPayloads",
                column: "ItemId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MigrationOperationItems_OperationId_Ordinal",
                schema: "Migration",
                table: "MigrationOperationItems",
                columns: new[] { "OperationId", "Ordinal" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MigrationOperationItems_OperationId_Status_Id",
                schema: "Migration",
                table: "MigrationOperationItems",
                columns: new[] { "OperationId", "Status", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_MigrationOperationPayloads_OperationId",
                schema: "Migration",
                table: "MigrationOperationPayloads",
                column: "OperationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MigrationOperations_ProjectId",
                schema: "Migration",
                table: "MigrationOperations",
                column: "ProjectId",
                unique: true,
                filter: "[Status] IN ('Queued','Running')");

            migrationBuilder.CreateIndex(
                name: "IX_MigrationOperations_ProjectId_ClientRequestId",
                schema: "Migration",
                table: "MigrationOperations",
                columns: new[] { "ProjectId", "ClientRequestId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MigrationOperations_RunId",
                schema: "Migration",
                table: "MigrationOperations",
                column: "RunId");

            migrationBuilder.CreateIndex(
                name: "IX_MigrationRunPayloads_RunId",
                schema: "Migration",
                table: "MigrationRunPayloads",
                column: "RunId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MigrationOperationIssues",
                schema: "Migration");

            migrationBuilder.DropTable(
                name: "MigrationOperationItemPayloads",
                schema: "Migration");

            migrationBuilder.DropTable(
                name: "MigrationOperationPayloads",
                schema: "Migration");

            migrationBuilder.DropTable(
                name: "MigrationRunPayloads",
                schema: "Migration");

            migrationBuilder.DropTable(
                name: "MigrationOperationItems",
                schema: "Migration");

            migrationBuilder.DropTable(
                name: "MigrationOperations",
                schema: "Migration");
        }
    }
}
