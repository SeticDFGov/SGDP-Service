using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace demanda_service.Migrations
{
    /// <inheritdoc />
    public partial class PePdticTrilha : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "pdtic_id",
                table: "pe_registro",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "pe_pdtic",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    orgao_id = table.Column<long>(type: "bigint", nullable: false),
                    versao = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    situacao = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    vigencia_inicio = table.Column<DateOnly>(type: "date", nullable: true),
                    vigencia_fim = table.Column<DateOnly>(type: "date", nullable: true),
                    registrado_externamente = table.Column<bool>(type: "boolean", nullable: false),
                    anterior_id = table.Column<long>(type: "bigint", nullable: true),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    criado_por = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    alterado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    alterado_por = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_pe_pdtic", x => x.id);
                    table.CheckConstraint("ck_pe_pdtic_situacao", "situacao IN ('em_elaboracao','em_aprovacao','devolvido','aprovado','publicado','em_acompanhamento','encerrado','substituido')");
                    table.CheckConstraint("ck_pe_pdtic_versao", "versao ~ '^[0-9]+\\.[0-9]+$'");
                    table.CheckConstraint("ck_pe_pdtic_vigencia", "vigencia_inicio IS NULL OR vigencia_fim IS NULL OR vigencia_fim >= vigencia_inicio");
                    table.ForeignKey(
                        name: "fk_pe_pdtic_anterior",
                        column: x => x.anterior_id,
                        principalTable: "pe_pdtic",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_pe_pdtic_orgao",
                        column: x => x.orgao_id,
                        principalTable: "pgia_orgao",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "pe_comentario",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    pdtic_id = table.Column<long>(type: "bigint", nullable: false),
                    passo_id = table.Column<long>(type: "bigint", nullable: false),
                    pai_id = table.Column<long>(type: "bigint", nullable: true),
                    texto = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    autor_email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    autor_nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    resolvido_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    resolvido_por = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_pe_comentario", x => x.id);
                    table.CheckConstraint("ck_pe_comentario_resolvido", "(resolvido_em IS NULL) = (resolvido_por IS NULL)");
                    table.CheckConstraint("ck_pe_comentario_resposta", "pai_id IS NULL OR resolvido_em IS NULL");
                    table.ForeignKey(
                        name: "fk_pe_comentario_pai",
                        column: x => x.pai_id,
                        principalTable: "pe_comentario",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_pe_comentario_passo",
                        column: x => x.passo_id,
                        principalTable: "pe_passo",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_pe_comentario_pdtic",
                        column: x => x.pdtic_id,
                        principalTable: "pe_pdtic",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "pe_pdtic_passo",
                columns: table => new
                {
                    pdtic_id = table.Column<long>(type: "bigint", nullable: false),
                    passo_id = table.Column<long>(type: "bigint", nullable: false),
                    nao_se_aplica = table.Column<bool>(type: "boolean", nullable: false),
                    justificativa = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    marcado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    marcado_por = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_pe_pdtic_passo", x => new { x.pdtic_id, x.passo_id });
                    table.CheckConstraint("ck_pe_pdtic_passo_justificativa", "nao_se_aplica = (justificativa IS NOT NULL)");
                    table.ForeignKey(
                        name: "fk_pe_pdtic_passo_passo",
                        column: x => x.passo_id,
                        principalTable: "pe_passo",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_pe_pdtic_passo_pdtic",
                        column: x => x.pdtic_id,
                        principalTable: "pe_pdtic",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ux_pe_registro_pdtic_codigo",
                table: "pe_registro",
                columns: new[] { "pdtic_id", "secao_id", "codigo" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_pe_registro_dono",
                table: "pe_registro",
                sql: "petic_id IS NULL OR pdtic_id IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_pe_comentario_pai",
                table: "pe_comentario",
                column: "pai_id");

            migrationBuilder.CreateIndex(
                name: "ix_pe_comentario_passo",
                table: "pe_comentario",
                column: "passo_id");

            migrationBuilder.CreateIndex(
                name: "ix_pe_comentario_pdtic",
                table: "pe_comentario",
                columns: new[] { "pdtic_id", "passo_id" });

            migrationBuilder.CreateIndex(
                name: "ix_pe_pdtic_anterior",
                table: "pe_pdtic",
                column: "anterior_id");

            migrationBuilder.CreateIndex(
                name: "ux_pe_pdtic_atual",
                table: "pe_pdtic",
                column: "orgao_id",
                unique: true,
                filter: "situacao NOT IN ('encerrado','substituido')");

            migrationBuilder.CreateIndex(
                name: "ux_pe_pdtic_orgao_versao",
                table: "pe_pdtic",
                columns: new[] { "orgao_id", "versao" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_pe_pdtic_passo_passo",
                table: "pe_pdtic_passo",
                column: "passo_id");

            migrationBuilder.AddForeignKey(
                name: "fk_pe_registro_pdtic",
                table: "pe_registro",
                column: "pdtic_id",
                principalTable: "pe_pdtic",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_pe_registro_pdtic",
                table: "pe_registro");

            migrationBuilder.DropTable(
                name: "pe_comentario");

            migrationBuilder.DropTable(
                name: "pe_pdtic_passo");

            migrationBuilder.DropTable(
                name: "pe_pdtic");

            migrationBuilder.DropIndex(
                name: "ux_pe_registro_pdtic_codigo",
                table: "pe_registro");

            migrationBuilder.DropCheckConstraint(
                name: "ck_pe_registro_dono",
                table: "pe_registro");

            migrationBuilder.DropColumn(
                name: "pdtic_id",
                table: "pe_registro");
        }
    }
}
