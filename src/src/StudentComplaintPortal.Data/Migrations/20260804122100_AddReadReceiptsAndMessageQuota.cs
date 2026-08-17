using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudentComplaintPortal.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddReadReceiptsAndMessageQuota : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ReadAt already exists from previous migration, only add ReadByUserId
            migrationBuilder.AddColumn<string>(
                name: "ReadByUserId",
                table: "Messages",
                type: "nvarchar(450)",
                nullable: true);

            // Create Categories table
            migrationBuilder.CreateTable(
                name: "Categories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Categories", x => x.Id);
                });

            // Create MessageQuotas table
            migrationBuilder.CreateTable(
                name: "MessageQuotas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ComplaintId = table.Column<int>(type: "int", nullable: false),
                    StudentId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    MessagesRemaining = table.Column<int>(type: "int", nullable: false, defaultValue: 10),
                    LastStaffMessageAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MessageQuotas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MessageQuotas_Complaints_ComplaintId",
                        column: x => x.ComplaintId,
                        principalTable: "Complaints",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MessageQuotas_AspNetUsers_StudentId",
                        column: x => x.StudentId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            // Create CategoryAttachmentRules table
            migrationBuilder.CreateTable(
                name: "CategoryAttachmentRules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CategoryId = table.Column<int>(type: "int", nullable: false),
                    FileType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MaxFileCount = table.Column<int>(type: "int", nullable: false),
                    MaxFileSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    IsRequired = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CategoryAttachmentRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CategoryAttachmentRules_Categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "Categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // Create CategoryAssignees table
            migrationBuilder.CreateTable(
                name: "CategoryAssignees",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CategoryId = table.Column<int>(type: "int", nullable: false),
                    AppUserId = table.Column<string>(type: "nvarchar(450)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CategoryAssignees", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CategoryAssignees_Categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "Categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CategoryAssignees_AspNetUsers_AppUserId",
                        column: x => x.AppUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // Create indices
            migrationBuilder.CreateIndex(
                name: "IX_Messages_ReadByUserId",
                table: "Messages",
                column: "ReadByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_MessageQuotas_ComplaintId_StudentId",
                table: "MessageQuotas",
                columns: new[] { "ComplaintId", "StudentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MessageQuotas_StudentId",
                table: "MessageQuotas",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_Categories_IsActive",
                table: "Categories",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_CategoryAttachmentRules_CategoryId",
                table: "CategoryAttachmentRules",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_CategoryAssignees_CategoryId",
                table: "CategoryAssignees",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_CategoryAssignees_AppUserId",
                table: "CategoryAssignees",
                column: "AppUserId");

            // Add foreign key for ReadBy
            migrationBuilder.AddForeignKey(
                name: "FK_Messages_AspNetUsers_ReadByUserId",
                table: "Messages",
                column: "ReadByUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Messages_AspNetUsers_ReadByUserId",
                table: "Messages");

            migrationBuilder.DropTable(name: "CategoryAssignees");
            migrationBuilder.DropTable(name: "CategoryAttachmentRules");
            migrationBuilder.DropTable(name: "MessageQuotas");
            migrationBuilder.DropTable(name: "Categories");

            migrationBuilder.DropIndex(
                name: "IX_Messages_ReadByUserId",
                table: "Messages");

            migrationBuilder.DropColumn(name: "ReadByUserId", table: "Messages");
        }
    }
}
