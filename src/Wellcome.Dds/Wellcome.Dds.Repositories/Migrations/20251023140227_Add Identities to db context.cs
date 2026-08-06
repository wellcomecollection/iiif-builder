using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wellcome.Dds.Repositories.Migrations
{
    public partial class AddIdentitiestodbcontext : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "identities",
                columns: table => new
                {
                    lower_case_value = table.Column<string>(type: "text", nullable: false),
                    value = table.Column<string>(type: "text", nullable: false),
                    package_identifier = table.Column<string>(type: "text", nullable: false),
                    package_identifier_path_element_safe = table.Column<string>(type: "text", nullable: false),
                    path_element_safe = table.Column<string>(type: "text", nullable: false),
                    storage_space = table.Column<string>(type: "text", nullable: true),
                    generator = table.Column<string>(type: "text", nullable: true),
                    source = table.Column<string>(type: "text", nullable: false),
                    catalogue_id = table.Column<string>(type: "text", nullable: true),
                    volume_part = table.Column<string>(type: "text", nullable: true),
                    issue_part = table.Column<string>(type: "text", nullable: true),
                    is_package_level_identifier = table.Column<bool>(type: "boolean", nullable: false),
                    level = table.Column<string>(type: "text", nullable: false),
                    created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    from_generator = table.Column<bool>(type: "boolean", nullable: false),
                    storage_space_validated = table.Column<bool>(type: "boolean", nullable: false),
                    source_validated = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_identities", x => x.lower_case_value);
                });

            migrationBuilder.CreateIndex(
                name: "ix_identities_package_identifier",
                table: "identities",
                column: "package_identifier");

            migrationBuilder.CreateIndex(
                name: "ix_identities_path_element_safe",
                table: "identities",
                column: "path_element_safe",
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "identities");
        }
    }
}
