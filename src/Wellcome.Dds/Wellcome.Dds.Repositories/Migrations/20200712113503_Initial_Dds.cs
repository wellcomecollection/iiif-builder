using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Wellcome.Dds.Repositories.Migrations
{
    public partial class Initial_Dds : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "manifestations",
                columns: table => new
                {
                    id = table.Column<string>(nullable: false),
                    package_identifier = table.Column<string>(nullable: true),
                    manifestation_identifier = table.Column<string>(nullable: true),
                    processed = table.Column<DateTime>(nullable: false),
                    package_short_b_number = table.Column<int>(nullable: false),
                    label = table.Column<string>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_manifestations", x => x.id);
                });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "manifestations");
        }
    }
}
