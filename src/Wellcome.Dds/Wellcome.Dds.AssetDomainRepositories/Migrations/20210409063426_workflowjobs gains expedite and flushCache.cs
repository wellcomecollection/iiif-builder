using Microsoft.EntityFrameworkCore.Migrations;

namespace Wellcome.Dds.AssetDomainRepositories.Migrations
{
    public partial class workflowjobsgainsexpediteandflushCache : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "expedite",
                table: "workflow_jobs",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "flush_cache",
                table: "workflow_jobs",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "expedite",
                table: "workflow_jobs");

            migrationBuilder.DropColumn(
                name: "flush_cache",
                table: "workflow_jobs");
        }
    }
}
