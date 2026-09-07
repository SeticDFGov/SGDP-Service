using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace demanda_service.Migrations
{
    /// <inheritdoc />
    public partial class PgiaFase5Transparencia : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "pgia_solicitacao_cidadao",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    protocolo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    sistema_ia_id = table.Column<long>(type: "bigint", nullable: false),
                    tipo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    solicitante_nome = table.Column<string>(type: "text", nullable: false),
                    solicitante_contato = table.Column<string>(type: "text", nullable: false),
                    referencia_decisao = table.Column<string>(type: "text", nullable: true),
                    descricao = table.Column<string>(type: "text", nullable: false),
                    data_abertura = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false, defaultValue: "Recebida"),
                    resposta = table.Column<string>(type: "text", nullable: true),
                    respondido_por = table.Column<Guid>(type: "uuid", nullable: true),
                    data_resposta = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    encaminhada_dpo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    criado_por = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    alterado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    alterado_por = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_pgia_solicitacao_cidadao", x => x.id);
                    table.CheckConstraint("ck_pgia_solicitacao_status", "status IN ('Recebida','Em análise','Respondida','Encaminhada ao Encarregado de Dados')");
                    table.CheckConstraint("ck_pgia_solicitacao_tipo", "tipo IN ('Informação sobre uso de IA','Revisão de decisão','Explicação da decisão','Impugnação por discriminação ou erro','Reclamação de titular de dados')");
                    table.ForeignKey(
                        name: "fk_pgia_solicitacao_agente",
                        column: x => x.respondido_por,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_pgia_solicitacao_sistema",
                        column: x => x.sistema_ia_id,
                        principalTable: "pgia_sistema_ia",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_pgia_solicitacao_agente",
                table: "pgia_solicitacao_cidadao",
                column: "respondido_por");

            migrationBuilder.CreateIndex(
                name: "ix_pgia_solicitacao_sistema",
                table: "pgia_solicitacao_cidadao",
                column: "sistema_ia_id");

            migrationBuilder.CreateIndex(
                name: "ux_pgia_solicitacao_protocolo",
                table: "pgia_solicitacao_cidadao",
                column: "protocolo",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "pgia_solicitacao_cidadao");
        }
    }
}
