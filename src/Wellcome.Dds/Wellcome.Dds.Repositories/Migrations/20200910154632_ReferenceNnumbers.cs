using Microsoft.EntityFrameworkCore.Migrations;

namespace Wellcome.Dds.Repositories.Migrations
{
    public partial class ReferenceNnumbers : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "collection_reference_number",
                table: "manifestations",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "reference_number",
                table: "manifestations",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "collection_reference_number",
                table: "manifestations");

            migrationBuilder.DropColumn(
                name: "reference_number",
                table: "manifestations");
        }
    }
}
