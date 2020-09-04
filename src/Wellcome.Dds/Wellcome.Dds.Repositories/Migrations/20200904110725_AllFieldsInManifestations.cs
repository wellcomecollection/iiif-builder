using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Wellcome.Dds.Repositories.Migrations
{
    public partial class AllFieldsInManifestations : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "asset_type",
                table: "manifestations",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "dip_status",
                table: "manifestations",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "dlcs_asset_type",
                table: "manifestations",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "error_message",
                table: "manifestations",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "file_count",
                table: "manifestations",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "first_file_extension",
                table: "manifestations",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "first_file_name",
                table: "manifestations",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "first_file_storage_identifier",
                table: "manifestations",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "index",
                table: "manifestations",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "is_all_open",
                table: "manifestations",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "manifestation_file",
                table: "manifestations",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "manifestation_file_modified",
                table: "manifestations",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "package_file",
                table: "manifestations",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "package_file_modified",
                table: "manifestations",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "package_label",
                table: "manifestations",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "permitted_operations",
                table: "manifestations",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "poster_image",
                table: "manifestations",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "root_section_access_condition",
                table: "manifestations",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "root_section_type",
                table: "manifestations",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "supports_search",
                table: "manifestations",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "volume_identifier",
                table: "manifestations",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "asset_type",
                table: "manifestations");

            migrationBuilder.DropColumn(
                name: "dip_status",
                table: "manifestations");

            migrationBuilder.DropColumn(
                name: "dlcs_asset_type",
                table: "manifestations");

            migrationBuilder.DropColumn(
                name: "error_message",
                table: "manifestations");

            migrationBuilder.DropColumn(
                name: "file_count",
                table: "manifestations");

            migrationBuilder.DropColumn(
                name: "first_file_extension",
                table: "manifestations");

            migrationBuilder.DropColumn(
                name: "first_file_name",
                table: "manifestations");

            migrationBuilder.DropColumn(
                name: "first_file_storage_identifier",
                table: "manifestations");

            migrationBuilder.DropColumn(
                name: "index",
                table: "manifestations");

            migrationBuilder.DropColumn(
                name: "is_all_open",
                table: "manifestations");

            migrationBuilder.DropColumn(
                name: "manifestation_file",
                table: "manifestations");

            migrationBuilder.DropColumn(
                name: "manifestation_file_modified",
                table: "manifestations");

            migrationBuilder.DropColumn(
                name: "package_file",
                table: "manifestations");

            migrationBuilder.DropColumn(
                name: "package_file_modified",
                table: "manifestations");

            migrationBuilder.DropColumn(
                name: "package_label",
                table: "manifestations");

            migrationBuilder.DropColumn(
                name: "permitted_operations",
                table: "manifestations");

            migrationBuilder.DropColumn(
                name: "poster_image",
                table: "manifestations");

            migrationBuilder.DropColumn(
                name: "root_section_access_condition",
                table: "manifestations");

            migrationBuilder.DropColumn(
                name: "root_section_type",
                table: "manifestations");

            migrationBuilder.DropColumn(
                name: "supports_search",
                table: "manifestations");

            migrationBuilder.DropColumn(
                name: "volume_identifier",
                table: "manifestations");
        }
    }
}
