using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudentComplaintPortal.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPriorityToComplaint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Priority",
                table: "Complaints",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Priority",
                table: "Complaints");
        }
    }
}
