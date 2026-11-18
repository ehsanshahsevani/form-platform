using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FormPlatform.Modules.Forms.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSubmissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_FormVersions_TenantId_FormId",
                table: "FormVersions");

            migrationBuilder.EnsureSchema(
                name: "submissions");

            migrationBuilder.AddUniqueConstraint(
                name: "AK_FormVersions_TenantId_FormId_Id",
                table: "FormVersions",
                columns: new[] { "TenantId", "FormId", "Id" });

            migrationBuilder.CreateTable(
                name: "Submissions",
                schema: "submissions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FormId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FormVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DataJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Submissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Submissions_FormVersions_TenantId_FormId_FormVersionId",
                        columns: x => new { x.TenantId, x.FormId, x.FormVersionId },
                        principalTable: "FormVersions",
                        principalColumns: new[] { "TenantId", "FormId", "Id" });
                    table.ForeignKey(
                        name: "FK_Submissions_Forms_TenantId_FormId",
                        columns: x => new { x.TenantId, x.FormId },
                        principalTable: "Forms",
                        principalColumns: new[] { "TenantId", "Id" });
                });

            migrationBuilder.CreateIndex(
                name: "IX_Submissions_TenantId_FormId_CreatedAtUtc",
                schema: "submissions",
                table: "Submissions",
                columns: new[] { "TenantId", "FormId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Submissions_TenantId_FormId_FormVersionId",
                schema: "submissions",
                table: "Submissions",
                columns: new[] { "TenantId", "FormId", "FormVersionId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Submissions",
                schema: "submissions");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_FormVersions_TenantId_FormId_Id",
                table: "FormVersions");

            migrationBuilder.CreateIndex(
                name: "IX_FormVersions_TenantId_FormId",
                table: "FormVersions",
                columns: new[] { "TenantId", "FormId" });
        }
    }
}
