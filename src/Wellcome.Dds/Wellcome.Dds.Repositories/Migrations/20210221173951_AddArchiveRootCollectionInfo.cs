using Microsoft.EntityFrameworkCore.Migrations;

namespace Wellcome.Dds.Repositories.Migrations
{
    public partial class AddArchiveRootCollectionInfo : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "collection_title",
                table: "manifestations",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "collection_work_id",
                table: "manifestations",
                type: "text",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "collection_title",
                table: "manifestations");

            migrationBuilder.DropColumn(
                name: "collection_work_id",
                table: "manifestations");
        }
    }
}
