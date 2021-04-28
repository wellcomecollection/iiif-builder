using Microsoft.EntityFrameworkCore.Migrations;

namespace Wellcome.Dds.AssetDomainRepositories.Migrations
{
    public partial class Addindexes : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ix_workflow_jobs_created",
                table: "workflow_jobs",
                column: "created");

            migrationBuilder.CreateIndex(
                name: "ix_workflow_jobs_expedite",
                table: "workflow_jobs",
                column: "expedite");

            migrationBuilder.CreateIndex(
                name: "ix_dlcs_ingest_jobs_created",
                table: "dlcs_ingest_jobs",
                column: "created");

            migrationBuilder.CreateIndex(
                name: "ix_dlcs_ingest_jobs_identifier",
                table: "dlcs_ingest_jobs",
                column: "identifier");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_workflow_jobs_created",
                table: "workflow_jobs");

            migrationBuilder.DropIndex(
                name: "ix_workflow_jobs_expedite",
                table: "workflow_jobs");

            migrationBuilder.DropIndex(
                name: "ix_dlcs_ingest_jobs_created",
                table: "dlcs_ingest_jobs");

            migrationBuilder.DropIndex(
                name: "ix_dlcs_ingest_jobs_identifier",
                table: "dlcs_ingest_jobs");
        }
    }
}
