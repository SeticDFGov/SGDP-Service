using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace demanda_service.Migrations
{
    /// <inheritdoc />
    public partial class PgiaAnexosDesignacaoAuditoria : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "documento_id",
                table: "pgia_responsavel_ia",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "documento_id",
                table: "pgia_encarregado_dados",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "documento_id",
                table: "pgia_auditoria_tecnica",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_pgia_responsavel_ia_doc",
                table: "pgia_responsavel_ia",
                column: "documento_id");

            migrationBuilder.CreateIndex(
                name: "ix_pgia_encarregado_dados_doc",
                table: "pgia_encarregado_dados",
                column: "documento_id");

            migrationBuilder.CreateIndex(
                name: "ix_pgia_auditoria_doc",
                table: "pgia_auditoria_tecnica",
                column: "documento_id");

            migrationBuilder.AddForeignKey(
                name: "fk_pgia_auditoria_doc",
                table: "pgia_auditoria_tecnica",
                column: "documento_id",
                principalTable: "pgia_documento",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_pgia_encarregado_doc",
                table: "pgia_encarregado_dados",
                column: "documento_id",
                principalTable: "pgia_documento",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_pgia_responsavel_ia_doc",
                table: "pgia_responsavel_ia",
                column: "documento_id",
                principalTable: "pgia_documento",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_pgia_auditoria_doc",
                table: "pgia_auditoria_tecnica");

            migrationBuilder.DropForeignKey(
                name: "fk_pgia_encarregado_doc",
                table: "pgia_encarregado_dados");

            migrationBuilder.DropForeignKey(
                name: "fk_pgia_responsavel_ia_doc",
                table: "pgia_responsavel_ia");

            migrationBuilder.DropIndex(
                name: "ix_pgia_responsavel_ia_doc",
                table: "pgia_responsavel_ia");

            migrationBuilder.DropIndex(
                name: "ix_pgia_encarregado_dados_doc",
                table: "pgia_encarregado_dados");

            migrationBuilder.DropIndex(
                name: "ix_pgia_auditoria_doc",
                table: "pgia_auditoria_tecnica");

            migrationBuilder.DropColumn(
                name: "documento_id",
                table: "pgia_responsavel_ia");

            migrationBuilder.DropColumn(
                name: "documento_id",
                table: "pgia_encarregado_dados");

            migrationBuilder.DropColumn(
                name: "documento_id",
                table: "pgia_auditoria_tecnica");
        }
    }
}
