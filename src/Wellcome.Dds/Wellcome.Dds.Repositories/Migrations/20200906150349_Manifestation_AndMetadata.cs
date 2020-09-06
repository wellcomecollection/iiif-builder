using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

namespace Wellcome.Dds.Repositories.Migrations
{
    public partial class Manifestation_AndMetadata : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "error_message",
                table: "manifestations");

            migrationBuilder.DropColumn(
                name: "first_file_name",
                table: "manifestations");

            migrationBuilder.DropColumn(
                name: "poster_image",
                table: "manifestations");

            migrationBuilder.AddColumn<string>(
                name: "calm_alt_ref",
                table: "manifestations",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "calm_alt_ref_parent",
                table: "manifestations",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "calm_ref",
                table: "manifestations",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "calm_ref_parent",
                table: "manifestations",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "catalogue_thumbnail",
                table: "manifestations",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "catalogue_thumbnail_dimensions",
                table: "manifestations",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "first_file_thumbnail",
                table: "manifestations",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "first_file_thumbnail_dimensions",
                table: "manifestations",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "work_id",
                table: "manifestations",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "work_type",
                table: "manifestations",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "metadata",
                columns: table => new
                {
                    id = table.Column<int>(nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    manifestation_id = table.Column<string>(nullable: true),
                    label = table.Column<string>(nullable: true),
                    string_value = table.Column<string>(nullable: true),
                    identifier = table.Column<string>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_metadata", x => x.id);
                });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "metadata");

            migrationBuilder.DropColumn(
                name: "calm_alt_ref",
                table: "manifestations");

            migrationBuilder.DropColumn(
                name: "calm_alt_ref_parent",
                table: "manifestations");

            migrationBuilder.DropColumn(
                name: "calm_ref",
                table: "manifestations");

            migrationBuilder.DropColumn(
                name: "calm_ref_parent",
                table: "manifestations");

            migrationBuilder.DropColumn(
                name: "catalogue_thumbnail",
                table: "manifestations");

            migrationBuilder.DropColumn(
                name: "catalogue_thumbnail_dimensions",
                table: "manifestations");

            migrationBuilder.DropColumn(
                name: "first_file_thumbnail",
                table: "manifestations");

            migrationBuilder.DropColumn(
                name: "first_file_thumbnail_dimensions",
                table: "manifestations");

            migrationBuilder.DropColumn(
                name: "work_id",
                table: "manifestations");

            migrationBuilder.DropColumn(
                name: "work_type",
                table: "manifestations");

            migrationBuilder.AddColumn<string>(
                name: "error_message",
                table: "manifestations",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "first_file_name",
                table: "manifestations",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "poster_image",
                table: "manifestations",
                type: "text",
                nullable: true);
        }
    }
}
