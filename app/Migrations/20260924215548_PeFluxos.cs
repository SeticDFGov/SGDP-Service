using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace demanda_service.Migrations
{
    /// <inheritdoc />
    public partial class PeFluxos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_pe_modelo_historico_entidade",
                table: "pe_modelo_historico");

            migrationBuilder.CreateTable(
                name: "pe_fluxo_modelo",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    chave = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    figura_guia = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    ordem = table.Column<int>(type: "integer", nullable: false),
                    definicao = table.Column<string>(type: "jsonb", nullable: false),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    criado_por = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    alterado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    alterado_por = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_pe_fluxo_modelo", x => x.id);
                    table.CheckConstraint("ck_pe_fluxo_modelo_chave", "chave ~ '^[a-z][a-z0-9_]*$'");
                });

            migrationBuilder.CreateTable(
                name: "pe_fluxo",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    pdtic_id = table.Column<long>(type: "bigint", nullable: false),
                    modelo_id = table.Column<long>(type: "bigint", nullable: false),
                    nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    definicao = table.Column<string>(type: "jsonb", nullable: false),
                    modelo_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    criado_por = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    alterado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    alterado_por = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_pe_fluxo", x => x.id);
                    table.ForeignKey(
                        name: "fk_pe_fluxo_modelo",
                        column: x => x.modelo_id,
                        principalTable: "pe_fluxo_modelo",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_pe_fluxo_pdtic",
                        column: x => x.pdtic_id,
                        principalTable: "pe_pdtic",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.AddCheckConstraint(
                name: "ck_pe_modelo_historico_entidade",
                table: "pe_modelo_historico",
                sql: "entidade IN ('nivel','etapa','passo','secao','campo','opcao','orgao_nivel','orgao_ajuste','doc_capitulo','doc_bloco','fluxo_modelo')");

            migrationBuilder.CreateIndex(
                name: "ix_pe_fluxo_modelo",
                table: "pe_fluxo",
                column: "modelo_id");

            migrationBuilder.CreateIndex(
                name: "ux_pe_fluxo_pdtic_modelo",
                table: "pe_fluxo",
                columns: new[] { "pdtic_id", "modelo_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_pe_fluxo_modelo_chave",
                table: "pe_fluxo_modelo",
                column: "chave",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Os fluxos do guia saem junto com as tabelas: a versão do conteúdo semeado volta
            // a 3 (a da E5), para que a E6, aplicada de novo, carregue os fluxos do guia
            migrationBuilder.Sql(
                "UPDATE pe_configuracao SET valor = '3' WHERE chave = 'seed_modelo_versao' " +
                "AND CASE WHEN (valor #>> '{}') ~ '^[0-9]+$' THEN (valor #>> '{}')::int > 3 ELSE false END;");

            migrationBuilder.DropTable(
                name: "pe_fluxo");

            migrationBuilder.DropTable(
                name: "pe_fluxo_modelo");

            // O que era só da E6 na tabela que fica (sem isso, o CHECK antigo não volta):
            // o histórico dos fluxos do modelo
            migrationBuilder.Sql("DELETE FROM pe_modelo_historico WHERE entidade IN ('fluxo_modelo');");

            migrationBuilder.DropCheckConstraint(
                name: "ck_pe_modelo_historico_entidade",
                table: "pe_modelo_historico");

            migrationBuilder.AddCheckConstraint(
                name: "ck_pe_modelo_historico_entidade",
                table: "pe_modelo_historico",
                sql: "entidade IN ('nivel','etapa','passo','secao','campo','opcao','orgao_nivel','orgao_ajuste','doc_capitulo','doc_bloco')");
        }
    }
}
