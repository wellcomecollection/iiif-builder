using Microsoft.EntityFrameworkCore.Migrations;

namespace Wellcome.Dds.AssetDomainRepositories.Migrations
{
    public partial class AddJobOptionsFlagsInt : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "workflow_options",
                table: "workflow_jobs",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "workflow_options",
                table: "workflow_jobs");
        }
    }
}
