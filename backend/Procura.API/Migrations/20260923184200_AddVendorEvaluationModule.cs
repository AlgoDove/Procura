using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Procura.API.Migrations
{
    /// <inheritdoc />
    public partial class AddVendorEvaluationModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "VendorEvaluations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProcurementRequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    VendorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Rank = table.Column<int>(type: "integer", nullable: false),
                    OverallScore = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    Reasoning = table.Column<string>(type: "text", nullable: false),
                    RiskFlags = table.Column<string>(type: "text", nullable: true),
                    GeneratedByAgent = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VendorEvaluations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "VendorQuotes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProcurementRequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    VendorId = table.Column<Guid>(type: "uuid", nullable: false),
                    VendorName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    QuotedPrice = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    EstimatedDeliveryDays = table.Column<int>(type: "integer", nullable: false),
                    ReliabilityRating = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    IsComplianceApproved = table.Column<bool>(type: "boolean", nullable: false),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VendorQuotes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "VendorEvaluationCriterionScores",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VendorEvaluationId = table.Column<Guid>(type: "uuid", nullable: false),
                    CriterionName = table.Column<string>(type: "text", nullable: false),
                    Score = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    Weight = table.Column<decimal>(type: "numeric(4,3)", precision: 4, scale: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VendorEvaluationCriterionScores", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VendorEvaluationCriterionScores_VendorEvaluations_VendorEva~",
                        column: x => x.VendorEvaluationId,
                        principalTable: "VendorEvaluations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_VendorEvaluationCriterionScores_VendorEvaluationId",
                table: "VendorEvaluationCriterionScores",
                column: "VendorEvaluationId");

            migrationBuilder.CreateIndex(
                name: "IX_VendorEvaluations_ProcurementRequestId",
                table: "VendorEvaluations",
                column: "ProcurementRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_VendorEvaluations_ProcurementRequestId_VendorId",
                table: "VendorEvaluations",
                columns: new[] { "ProcurementRequestId", "VendorId" });

            migrationBuilder.CreateIndex(
                name: "IX_VendorEvaluations_VendorId",
                table: "VendorEvaluations",
                column: "VendorId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "VendorEvaluationCriterionScores");

            migrationBuilder.DropTable(
                name: "VendorQuotes");

            migrationBuilder.DropTable(
                name: "VendorEvaluations");
        }
    }
}
