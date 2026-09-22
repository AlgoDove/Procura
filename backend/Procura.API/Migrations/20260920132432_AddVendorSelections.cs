using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Procura.API.Migrations
{
    /// <inheritdoc />
    public partial class AddVendorSelections : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "VendorSelections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProcurementRequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    VendorId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VendorSelections", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_VendorSelections_ProcurementRequestId",
                table: "VendorSelections",
                column: "ProcurementRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_VendorSelections_VendorId",
                table: "VendorSelections",
                column: "VendorId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "VendorSelections");
        }
    }
}
