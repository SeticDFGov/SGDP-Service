using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace demanda_service.Migrations
{
    /// <inheritdoc />
    public partial class PgiaQuestionarioRiscosDeclarados : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "pgia_risco_outro",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    classificacao_risco_id = table.Column<long>(type: "bigint", nullable: false),
                    descricao_risco = table.Column<string>(type: "text", nullable: false),
                    acao_mitigacao = table.Column<string>(type: "text", nullable: false),
                    responsavel_nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    responsavel_email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    probabilidade = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false),
                    consequencia = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    criado_por = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_pgia_risco_outro", x => x.id);
                    table.CheckConstraint("ck_pgia_risco_outro_consequencia", "consequencia IN ('Desprezível','Menor','Moderada')");
                    table.CheckConstraint("ck_pgia_risco_outro_probabilidade", "probabilidade IN ('Improvável','Raro','Possível')");
                    table.ForeignKey(
                        name: "fk_pgia_risco_outro_classificacao",
                        column: x => x.classificacao_risco_id,
                        principalTable: "pgia_classificacao_risco",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_pgia_risco_outro_classificacao",
                table: "pgia_risco_outro",
                column: "classificacao_risco_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "pgia_risco_outro");
        }
    }
}
