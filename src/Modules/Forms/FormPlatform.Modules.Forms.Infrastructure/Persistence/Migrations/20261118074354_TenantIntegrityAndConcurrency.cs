using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FormPlatform.Modules.Forms.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TenantIntegrityAndConcurrency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FormVersions_Forms_FormId",
                table: "FormVersions");

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Forms",
                type: "rowversion",
                rowVersion: true,
                nullable: false);

            migrationBuilder.AddUniqueConstraint(
                name: "AK_Forms_TenantId_Id",
                table: "Forms",
                columns: new[] { "TenantId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_FormVersions_TenantId_FormId",
                table: "FormVersions",
                columns: new[] { "TenantId", "FormId" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_FormVersions_PublicationState",
                table: "FormVersions",
                sql: "([Status] = 0 AND [PublishedAtUtc] IS NULL) OR ([Status] IN (1, 2) AND [PublishedAtUtc] IS NOT NULL)");

            migrationBuilder.AddForeignKey(
                name: "FK_FormVersions_Forms_TenantId_FormId",
                table: "FormVersions",
                columns: new[] { "TenantId", "FormId" },
                principalTable: "Forms",
                principalColumns: new[] { "TenantId", "Id" },
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FormVersions_Forms_TenantId_FormId",
                table: "FormVersions");

            migrationBuilder.DropIndex(
                name: "IX_FormVersions_TenantId_FormId",
                table: "FormVersions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_FormVersions_PublicationState",
                table: "FormVersions");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_Forms_TenantId_Id",
                table: "Forms");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Forms");

            migrationBuilder.AddForeignKey(
                name: "FK_FormVersions_Forms_FormId",
                table: "FormVersions",
                column: "FormId",
                principalTable: "Forms",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
