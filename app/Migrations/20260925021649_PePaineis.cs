using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace demanda_service.Migrations
{
    /// <inheritdoc />
    public partial class PePaineis : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "pe_inadimplencia",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    orgao_id = table.Column<long>(type: "bigint", nullable: false),
                    obrigacao = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    prazo_descumprido = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    notificado_em = table.Column<DateOnly>(type: "date", nullable: false),
                    documento = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    sei = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    prazo = table.Column<DateOnly>(type: "date", nullable: false),
                    situacao = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    justificativa = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    motivo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    nota_motivacao = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: true),
                    comunicado_controle_em = table.Column<DateOnly>(type: "date", nullable: true),
                    registrado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    registrado_por = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    saneado_em = table.Column<DateOnly>(type: "date", nullable: true),
                    observacao = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    criado_por = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    alterado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    alterado_por = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_pe_inadimplencia", x => x.id);
                    table.CheckConstraint("ck_pe_inadimplencia_justificado", "situacao <> 'justificado' OR justificativa IS NOT NULL");
                    table.CheckConstraint("ck_pe_inadimplencia_motivo", "motivo IS NULL OR motivo IN ('descumprimento_prazo','omissao_reiterada','recusa_injustificada')");
                    table.CheckConstraint("ck_pe_inadimplencia_prazo", "prazo > notificado_em");
                    table.CheckConstraint("ck_pe_inadimplencia_registro", "(registrado_em IS NULL) = (registrado_por IS NULL) AND (registrado_em IS NULL) = (motivo IS NULL) AND (registrado_em IS NULL) = (nota_motivacao IS NULL) AND (situacao <> 'inadimplente' OR registrado_em IS NOT NULL) AND (situacao IN ('inadimplente','saneado') OR registrado_em IS NULL) AND (comunicado_controle_em IS NULL OR (registrado_em IS NOT NULL AND comunicado_controle_em >= notificado_em))");
                    table.CheckConstraint("ck_pe_inadimplencia_saneado", "(situacao = 'saneado') = (saneado_em IS NOT NULL) AND (saneado_em IS NULL OR saneado_em >= notificado_em)");
                    table.CheckConstraint("ck_pe_inadimplencia_situacao", "situacao IN ('notificado','justificado','inadimplente','saneado')");
                    table.ForeignKey(
                        name: "fk_pe_inadimplencia_orgao",
                        column: x => x.orgao_id,
                        principalTable: "pgia_orgao",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_pe_inadimplencia_orgao",
                table: "pe_inadimplencia",
                columns: new[] { "orgao_id", "situacao" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "pe_inadimplencia");
        }
    }
}
