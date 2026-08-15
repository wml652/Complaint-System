using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudentComplaintPortal.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddInternalAttachments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "InternalAttachments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    InternalMessageId = table.Column<int>(type: "int", nullable: false),
                    FileUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    FileType = table.Column<int>(type: "int", nullable: false),
                    FileSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    UploadedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InternalAttachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InternalAttachments_InternalMessages_InternalMessageId",
                        column: x => x.InternalMessageId,
                        principalTable: "InternalMessages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InternalAttachments_InternalMessageId",
                table: "InternalAttachments",
                column: "InternalMessageId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InternalAttachments");
        }
    }
}
