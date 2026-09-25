using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace demanda_service.Migrations
{
    /// <inheritdoc />
    public partial class PeAcessoPapeis : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_pedido_acesso_modulo",
                table: "pedido_acesso");

            migrationBuilder.DropCheckConstraint(
                name: "ck_acesso_modulo_modulo",
                table: "acesso_modulo");

            migrationBuilder.CreateTable(
                name: "pe_papel_usuario",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    papel = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    concedido_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    concedido_por = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    alterado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    alterado_por = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_pe_papel_usuario", x => x.user_id);
                    table.CheckConstraint("ck_pe_papel_usuario_papel", "papel IN ('pe_admin','pe_sgdi','pe_cgtic','pe_orgao','pe_orgao_consulta')");
                    table.ForeignKey(
                        name: "fk_pe_papel_usuario_user",
                        column: x => x.user_id,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "pe_papel_usuario_historico",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    papel_anterior = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    papel_novo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    origem = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    pedido_acesso_id = table.Column<long>(type: "bigint", nullable: true),
                    alterado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    alterado_por = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_pe_papel_usuario_historico", x => x.id);
                    table.CheckConstraint("ck_pe_papel_usuario_historico_origem", "origem IN ('pessoas','pedido','gestao_acessos','modo_local')");
                    table.ForeignKey(
                        name: "fk_pe_papel_usuario_historico_user",
                        column: x => x.user_id,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.AddCheckConstraint(
                name: "ck_pedido_acesso_modulo",
                table: "pedido_acesso",
                sql: "modulo IN ('demandas','pgia','contratacoes','planejamento')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_acesso_modulo_modulo",
                table: "acesso_modulo",
                sql: "modulo IN ('demandas','pgia','contratacoes','planejamento','administracao')");

            migrationBuilder.CreateIndex(
                name: "ix_pe_papel_usuario_historico_user",
                table: "pe_papel_usuario_historico",
                columns: new[] { "user_id", "alterado_em" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "pe_papel_usuario");

            migrationBuilder.DropTable(
                name: "pe_papel_usuario_historico");

            migrationBuilder.DropCheckConstraint(
                name: "ck_pedido_acesso_modulo",
                table: "pedido_acesso");

            migrationBuilder.DropCheckConstraint(
                name: "ck_acesso_modulo_modulo",
                table: "acesso_modulo");

            migrationBuilder.AddCheckConstraint(
                name: "ck_pedido_acesso_modulo",
                table: "pedido_acesso",
                sql: "modulo IN ('demandas','pgia','contratacoes')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_acesso_modulo_modulo",
                table: "acesso_modulo",
                sql: "modulo IN ('demandas','pgia','contratacoes','administracao')");
        }
    }
}
