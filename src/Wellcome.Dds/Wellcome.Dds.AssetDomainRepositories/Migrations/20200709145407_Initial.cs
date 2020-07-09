using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

namespace Wellcome.Dds.AssetDomainRepositories.Migrations
{
    public partial class Initial : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "dlcs_ingest_jobs",
                columns: table => new
                {
                    id = table.Column<int>(nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    created = table.Column<DateTime>(nullable: false),
                    identifier = table.Column<string>(nullable: true),
                    sequence_index = table.Column<int>(nullable: false),
                    volume_part = table.Column<string>(nullable: true),
                    issue_part = table.Column<string>(nullable: true),
                    image_count = table.Column<int>(nullable: false),
                    start_processed = table.Column<DateTime>(nullable: true),
                    end_processed = table.Column<DateTime>(nullable: true),
                    asset_type = table.Column<string>(nullable: true),
                    succeeded = table.Column<bool>(nullable: false),
                    data = table.Column<string>(nullable: true),
                    ready_image_count = table.Column<int>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_dlcs_ingest_jobs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "ingest_actions",
                columns: table => new
                {
                    id = table.Column<int>(nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    manifestation_id = table.Column<string>(nullable: true),
                    job_id = table.Column<int>(nullable: true),
                    username = table.Column<string>(nullable: true),
                    description = table.Column<string>(nullable: true),
                    action = table.Column<string>(nullable: true),
                    performed = table.Column<DateTime>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ingest_actions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "workflow_jobs",
                columns: table => new
                {
                    identifier = table.Column<string>(nullable: false),
                    force_text_rebuild = table.Column<bool>(nullable: false),
                    waiting = table.Column<bool>(nullable: false),
                    finished = table.Column<bool>(nullable: false),
                    created = table.Column<DateTime>(nullable: true),
                    taken = table.Column<DateTime>(nullable: true),
                    first_dlcs_job_id = table.Column<int>(nullable: false),
                    dlcs_job_count = table.Column<int>(nullable: false),
                    expected_texts = table.Column<int>(nullable: false),
                    texts_already_on_disk = table.Column<int>(nullable: false),
                    texts_built = table.Column<int>(nullable: false),
                    annos_already_on_disk = table.Column<int>(nullable: false),
                    annos_built = table.Column<int>(nullable: false),
                    package_build_time = table.Column<long>(nullable: false),
                    text_and_anno_build_time = table.Column<long>(nullable: false),
                    total_time = table.Column<long>(nullable: false),
                    error = table.Column<string>(nullable: true),
                    words = table.Column<int>(nullable: false),
                    text_pages = table.Column<int>(nullable: false),
                    time_spent_on_text_pages = table.Column<int>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_workflow_jobs", x => x.identifier);
                });

            migrationBuilder.CreateTable(
                name: "dlcs_batches",
                columns: table => new
                {
                    id = table.Column<int>(nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    dlcs_ingest_job_id = table.Column<int>(nullable: false),
                    request_sent = table.Column<DateTime>(nullable: true),
                    request_body = table.Column<string>(nullable: true),
                    finished = table.Column<DateTime>(nullable: true),
                    response_body = table.Column<string>(nullable: true),
                    error_code = table.Column<int>(nullable: false),
                    error_text = table.Column<string>(nullable: true),
                    batch_size = table.Column<int>(nullable: false),
                    content_length = table.Column<int>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_dlcs_batches", x => x.id);
                    table.ForeignKey(
                        name: "fk_dlcs_batches_dlcs_ingest_jobs_dlcs_ingest_job_id",
                        column: x => x.dlcs_ingest_job_id,
                        principalTable: "dlcs_ingest_jobs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_dlcs_batches_dlcs_ingest_job_id",
                table: "dlcs_batches",
                column: "dlcs_ingest_job_id");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "dlcs_batches");

            migrationBuilder.DropTable(
                name: "ingest_actions");

            migrationBuilder.DropTable(
                name: "workflow_jobs");

            migrationBuilder.DropTable(
                name: "dlcs_ingest_jobs");
        }
    }
}
