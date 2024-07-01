using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wellcome.Dds.AssetDomainRepositories.Migrations
{
    public partial class AddIngestJobStartedField : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ingest_job_started",
                table: "workflow_jobs",
                type: "timestamp with time zone",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ingest_job_started",
                table: "workflow_jobs");
        }
    }
}
