using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Procura.API.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkflowInstances : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WorkflowInstances",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Objective = table.Column<string>(type: "text", nullable: false),
                    RequesterId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequesterRole = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CurrentStage = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ProcurementRequestId = table.Column<Guid>(type: "uuid", nullable: true),
                    RequestNumber = table.Column<string>(type: "text", nullable: true),
                    EstimatedTotal = table.Column<decimal>(type: "numeric", nullable: true),
                    PlanJson = table.Column<string>(type: "text", nullable: true),
                    ContextJson = table.Column<string>(type: "text", nullable: true),
                    AuditTrailJson = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkflowInstances", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowInstances_ProcurementRequestId",
                table: "WorkflowInstances",
                column: "ProcurementRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowInstances_RequesterId",
                table: "WorkflowInstances",
                column: "RequesterId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WorkflowInstances");
        }
    }
}
