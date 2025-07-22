using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace demanda_service.Migrations
{
    /// <inheritdoc />
    public partial class moddifiedfield : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "COMPLEXIDADE",
                table: "Templates");

            migrationBuilder.AddColumn<int>(
                name: "DIAS_PREVISTOS",
                table: "Templates",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DIAS_PREVISTOS",
                table: "Templates");

            migrationBuilder.AddColumn<string>(
                name: "COMPLEXIDADE",
                table: "Templates",
                type: "text",
                nullable: false,
                defaultValue: "");
        }
    }
}
