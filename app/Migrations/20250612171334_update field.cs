using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace demanda_service.Migrations
{
    /// <inheritdoc />
    public partial class updatefield : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Users_Unidades_Unidadeid",
                table: "Users");

            migrationBuilder.AlterColumn<Guid>(
                name: "Unidadeid",
                table: "Users",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddForeignKey(
                name: "FK_Users_Unidades_Unidadeid",
                table: "Users",
                column: "Unidadeid",
                principalTable: "Unidades",
                principalColumn: "id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Users_Unidades_Unidadeid",
                table: "Users");

            migrationBuilder.AlterColumn<Guid>(
                name: "Unidadeid",
                table: "Users",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Users_Unidades_Unidadeid",
                table: "Users",
                column: "Unidadeid",
                principalTable: "Unidades",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
