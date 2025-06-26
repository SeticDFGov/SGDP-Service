using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace demanda_service.Migrations
{
    /// <inheritdoc />
    public partial class appdbcontextunidade : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Users_Unidade_Unidadeid",
                table: "Users");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Unidade",
                table: "Unidade");

            migrationBuilder.RenameTable(
                name: "Unidade",
                newName: "Unidades");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Unidades",
                table: "Unidades",
                column: "id");

            migrationBuilder.AddForeignKey(
                name: "FK_Users_Unidades_Unidadeid",
                table: "Users",
                column: "Unidadeid",
                principalTable: "Unidades",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Users_Unidades_Unidadeid",
                table: "Users");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Unidades",
                table: "Unidades");

            migrationBuilder.RenameTable(
                name: "Unidades",
                newName: "Unidade");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Unidade",
                table: "Unidade",
                column: "id");

            migrationBuilder.AddForeignKey(
                name: "FK_Users_Unidade_Unidadeid",
                table: "Users",
                column: "Unidadeid",
                principalTable: "Unidade",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
