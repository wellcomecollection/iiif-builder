using Microsoft.EntityFrameworkCore.Migrations;

namespace Wellcome.Dds.Repositories.Migrations
{
    public partial class Addindexes : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ix_metadata_manifestation_id",
                table: "metadata",
                column: "manifestation_id");

            migrationBuilder.CreateIndex(
                name: "ix_manifestations_package_identifier",
                table: "manifestations",
                column: "package_identifier");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_metadata_manifestation_id",
                table: "metadata");

            migrationBuilder.DropIndex(
                name: "ix_manifestations_package_identifier",
                table: "manifestations");
        }
    }
}
