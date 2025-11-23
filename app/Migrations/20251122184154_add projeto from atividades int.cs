using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace demanda_service.Migrations
{
    /// <inheritdoc />
    public partial class addprojetofromatividadesint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Atividades_Reports_ReportId",
                table: "Atividades");

            migrationBuilder.AlterColumn<int>(
                name: "ReportId",
                table: "Atividades",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<int>(
                name: "NM_PROJETO",
                table: "Atividades",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddForeignKey(
                name: "FK_Atividades_Reports_ReportId",
                table: "Atividades",
                column: "ReportId",
                principalTable: "Reports",
                principalColumn: "ReportId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Atividades_Reports_ReportId",
                table: "Atividades");

            migrationBuilder.DropColumn(
                name: "NM_PROJETO",
                table: "Atividades");

            migrationBuilder.AlterColumn<int>(
                name: "ReportId",
                table: "Atividades",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Atividades_Reports_ReportId",
                table: "Atividades",
                column: "ReportId",
                principalTable: "Reports",
                principalColumn: "ReportId",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
