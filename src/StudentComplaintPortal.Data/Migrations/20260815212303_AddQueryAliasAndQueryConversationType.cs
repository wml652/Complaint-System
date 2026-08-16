using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudentComplaintPortal.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddQueryAliasAndQueryConversationType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "QueryAlias",
                table: "AspNetUsers",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "QueryAlias",
                table: "AspNetUsers");
        }
    }
}
