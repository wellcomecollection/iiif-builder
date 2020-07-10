using Microsoft.EntityFrameworkCore.Migrations;

namespace Wellcome.Dds.AssetDomainRepositories.Migrations
{
    public partial class DlcsIngestJob_Add_Label : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "label",
                table: "dlcs_ingest_jobs",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "label",
                table: "dlcs_ingest_jobs");
        }
    }
}
