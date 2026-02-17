using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace demanda_service.Migrations
{
    /// <inheritdoc />
    public partial class EtapasWithAtividades : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DIAS_PREVISTOS",
                table: "Etapas");

            migrationBuilder.DropColumn(
                name: "DT_INICIO_PREVISTO",
                table: "Etapas");

            migrationBuilder.DropColumn(
                name: "DT_INICIO_REAL",
                table: "Etapas");

            migrationBuilder.DropColumn(
                name: "DT_TERMINO_PREVISTO",
                table: "Etapas");

            migrationBuilder.DropColumn(
                name: "DT_TERMINO_REAL",
                table: "Etapas");

            migrationBuilder.DropColumn(
                name: "PERCENT_EXEC_ETAPA",
                table: "Etapas");

            migrationBuilder.DropColumn(
                name: "PERCENT_TOTAL_ETAPA",
                table: "Etapas");

            migrationBuilder.RenameColumn(
                name: "titulo",
                table: "Atividades",
                newName: "Titulo");

            migrationBuilder.RenameColumn(
                name: "descricao",
                table: "Atividades",
                newName: "Descricao");

            migrationBuilder.RenameColumn(
                name: "categoria",
                table: "Atividades",
                newName: "Categoria");

            migrationBuilder.RenameColumn(
                name: "situacao",
                table: "Atividades",
                newName: "Order");

            migrationBuilder.RenameColumn(
                name: "data_termino",
                table: "Atividades",
                newName: "DT_TERMINO_PREVISTO");

            migrationBuilder.RenameColumn(
                name: "NM_PROJETO",
                table: "Atividades",
                newName: "EtapaProjetoId");

            migrationBuilder.AddColumn<DateTime>(
                name: "DT_INICIO",
                table: "Projetos",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "DT_TERMINO",
                table: "Projetos",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AlterColumn<string>(
                name: "RESPONSAVEL_ETAPA",
                table: "Etapas",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<string>(
                name: "ANALISE",
                table: "Etapas",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500);

            migrationBuilder.AlterColumn<string>(
                name: "Titulo",
                table: "Atividades",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Descricao",
                table: "Atividades",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(300)",
                oldMaxLength: 300);

            migrationBuilder.AlterColumn<string>(
                name: "Categoria",
                table: "Atividades",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AddColumn<DateTime>(
                name: "DT_INICIO_PREVISTO",
                table: "Atividades",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "DT_INICIO_REAL",
                table: "Atividades",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DT_TERMINO_REAL",
                table: "Atividades",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RESPONSAVEL_ATIVIDADE",
                table: "Atividades",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Atividades_EtapaProjetoId",
                table: "Atividades",
                column: "EtapaProjetoId");

            migrationBuilder.AddForeignKey(
                name: "FK_Atividades_Etapas_EtapaProjetoId",
                table: "Atividades",
                column: "EtapaProjetoId",
                principalTable: "Etapas",
                principalColumn: "EtapaProjetoId",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Atividades_Etapas_EtapaProjetoId",
                table: "Atividades");

            migrationBuilder.DropIndex(
                name: "IX_Atividades_EtapaProjetoId",
                table: "Atividades");

            migrationBuilder.DropColumn(
                name: "DT_INICIO",
                table: "Projetos");

            migrationBuilder.DropColumn(
                name: "DT_TERMINO",
                table: "Projetos");

            migrationBuilder.DropColumn(
                name: "DT_INICIO_PREVISTO",
                table: "Atividades");

            migrationBuilder.DropColumn(
                name: "DT_INICIO_REAL",
                table: "Atividades");

            migrationBuilder.DropColumn(
                name: "DT_TERMINO_REAL",
                table: "Atividades");

            migrationBuilder.DropColumn(
                name: "RESPONSAVEL_ATIVIDADE",
                table: "Atividades");

            migrationBuilder.RenameColumn(
                name: "Titulo",
                table: "Atividades",
                newName: "titulo");

            migrationBuilder.RenameColumn(
                name: "Descricao",
                table: "Atividades",
                newName: "descricao");

            migrationBuilder.RenameColumn(
                name: "Categoria",
                table: "Atividades",
                newName: "categoria");

            migrationBuilder.RenameColumn(
                name: "Order",
                table: "Atividades",
                newName: "situacao");

            migrationBuilder.RenameColumn(
                name: "EtapaProjetoId",
                table: "Atividades",
                newName: "NM_PROJETO");

            migrationBuilder.RenameColumn(
                name: "DT_TERMINO_PREVISTO",
                table: "Atividades",
                newName: "data_termino");

            migrationBuilder.AlterColumn<string>(
                name: "RESPONSAVEL_ETAPA",
                table: "Etapas",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ANALISE",
                table: "Etapas",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500,
                oldNullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DIAS_PREVISTOS",
                table: "Etapas",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "DT_INICIO_PREVISTO",
                table: "Etapas",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DT_INICIO_REAL",
                table: "Etapas",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DT_TERMINO_PREVISTO",
                table: "Etapas",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DT_TERMINO_REAL",
                table: "Etapas",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PERCENT_EXEC_ETAPA",
                table: "Etapas",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PERCENT_TOTAL_ETAPA",
                table: "Etapas",
                type: "numeric",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "titulo",
                table: "Atividades",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<string>(
                name: "descricao",
                table: "Atividades",
                type: "character varying(300)",
                maxLength: 300,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "categoria",
                table: "Atividades",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100,
                oldNullable: true);
        }
    }
}
