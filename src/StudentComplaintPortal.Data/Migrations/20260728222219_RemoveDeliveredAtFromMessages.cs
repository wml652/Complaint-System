using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudentComplaintPortal.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemoveDeliveredAtFromMessages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DeliveredAt",
                table: "Messages");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DeliveredAt",
                table: "Messages",
                type: "datetime2",
                nullable: true);
        }
    }
}
