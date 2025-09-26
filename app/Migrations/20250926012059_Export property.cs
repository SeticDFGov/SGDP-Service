using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace demanda_service.Migrations
{
    /// <inheritdoc />
    public partial class Exportproperty : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Atividades_Exports_ExportId",
                table: "Atividades");

            migrationBuilder.AlterColumn<Guid>(
                name: "ExportId",
                table: "Atividades",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Atividades_Exports_ExportId",
                table: "Atividades",
                column: "ExportId",
                principalTable: "Exports",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Atividades_Exports_ExportId",
                table: "Atividades");

            migrationBuilder.AlterColumn<Guid>(
                name: "ExportId",
                table: "Atividades",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddForeignKey(
                name: "FK_Atividades_Exports_ExportId",
                table: "Atividades",
                column: "ExportId",
                principalTable: "Exports",
                principalColumn: "Id");
        }
    }
}
