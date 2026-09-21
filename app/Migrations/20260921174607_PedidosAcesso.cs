using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace demanda_service.Migrations
{
    /// <inheritdoc />
    public partial class PedidosAcesso : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "pedido_acesso",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    modulo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    justificativa = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    situacao = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    decidido_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    decidido_por = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    papel_pgia = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    motivo_recusa = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_pedido_acesso", x => x.id);
                    table.CheckConstraint("ck_pedido_acesso_modulo", "modulo IN ('demandas','pgia','contratacoes')");
                    table.CheckConstraint("ck_pedido_acesso_situacao", "situacao IN ('pendente','aprovado','recusado')");
                    table.ForeignKey(
                        name: "fk_pedido_acesso_user",
                        column: x => x.user_id,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_pedido_acesso_situacao",
                table: "pedido_acesso",
                columns: new[] { "situacao", "criado_em" });

            migrationBuilder.CreateIndex(
                name: "ux_pedido_acesso_pendente",
                table: "pedido_acesso",
                columns: new[] { "user_id", "modulo" },
                unique: true,
                filter: "situacao = 'pendente'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "pedido_acesso");
        }
    }
}
