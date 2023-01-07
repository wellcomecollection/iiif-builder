using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wellcome.Dds.AssetDomainRepositories.Migrations
{
    public partial class nullablecheck : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "identifier",
                table: "dlcs_ingest_jobs",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "identifier",
                table: "dlcs_ingest_jobs",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");
        }
    }
}
