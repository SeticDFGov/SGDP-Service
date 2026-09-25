using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace demanda_service.Migrations
{
    /// <inheritdoc />
    public partial class PeAprovacaoPublicacao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_pe_pdtic_atual",
                table: "pe_pdtic");

            migrationBuilder.AddColumn<DateTime>(
                name: "aprovado_em",
                table: "pe_pdtic",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "encerrado_em",
                table: "pe_pdtic",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "encerramento_motivo",
                table: "pe_pdtic",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "enviado_em",
                table: "pe_pdtic",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "publicado_em",
                table: "pe_pdtic",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "revisao_justificativa",
                table: "pe_pdtic",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "doc_versao_id",
                table: "pe_deliberacao",
                type: "bigint",
                nullable: true);

            // Antes dos CHECKs das datas: um PDTIC que já estivesse numa situação depois da
            // elaboração (só por fora do sistema; a E4 à E6 só abrem em elaboração) ganha a data
            // da situação a partir da última alteração
            migrationBuilder.Sql(
                "UPDATE pe_pdtic SET enviado_em = COALESCE(alterado_em, criado_em) WHERE situacao = 'em_aprovacao'; " +
                "UPDATE pe_pdtic SET aprovado_em = COALESCE(alterado_em, criado_em) WHERE situacao IN ('aprovado','publicado','em_acompanhamento'); " +
                "UPDATE pe_pdtic SET publicado_em = COALESCE(alterado_em, criado_em) WHERE situacao IN ('publicado','em_acompanhamento'); " +
                "UPDATE pe_pdtic SET encerrado_em = COALESCE(alterado_em, criado_em) WHERE situacao = 'encerrado';");

            migrationBuilder.CreateIndex(
                name: "ux_pe_pdtic_em_elaboracao",
                table: "pe_pdtic",
                column: "orgao_id",
                unique: true,
                filter: "situacao IN ('em_elaboracao','em_aprovacao','devolvido','aprovado')");

            migrationBuilder.CreateIndex(
                name: "ux_pe_pdtic_vigente",
                table: "pe_pdtic",
                column: "orgao_id",
                unique: true,
                filter: "situacao IN ('publicado','em_acompanhamento')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_pe_pdtic_aprovado",
                table: "pe_pdtic",
                sql: "situacao NOT IN ('aprovado','publicado','em_acompanhamento') OR aprovado_em IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_pe_pdtic_encerrado",
                table: "pe_pdtic",
                sql: "(situacao = 'encerrado') = (encerrado_em IS NOT NULL) AND (encerramento_motivo IS NULL OR situacao = 'encerrado')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_pe_pdtic_enviado",
                table: "pe_pdtic",
                sql: "situacao <> 'em_aprovacao' OR enviado_em IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_pe_pdtic_publicado",
                table: "pe_pdtic",
                sql: "situacao NOT IN ('publicado','em_acompanhamento') OR publicado_em IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_pe_deliberacao_doc_versao",
                table: "pe_deliberacao",
                column: "doc_versao_id");

            migrationBuilder.AddCheckConstraint(
                name: "ck_pe_deliberacao_doc_versao",
                table: "pe_deliberacao",
                sql: "doc_versao_id IS NULL OR objeto_tipo = 'pdtic'");

            migrationBuilder.AddForeignKey(
                name: "fk_pe_deliberacao_doc_versao",
                table: "pe_deliberacao",
                column: "doc_versao_id",
                principalTable: "pe_doc_versao",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // O índice antigo (um PDTIC atual por órgão) não volta se algum órgão tiver ao mesmo
            // tempo a versão vigente e a revisão em elaboração: encerre ou resolva a revisão antes.
            // O conteúdo semeado da versão 5 (blocos do documento) não depende desta migration e fica
            migrationBuilder.DropForeignKey(
                name: "fk_pe_deliberacao_doc_versao",
                table: "pe_deliberacao");

            migrationBuilder.DropIndex(
                name: "ux_pe_pdtic_em_elaboracao",
                table: "pe_pdtic");

            migrationBuilder.DropIndex(
                name: "ux_pe_pdtic_vigente",
                table: "pe_pdtic");

            migrationBuilder.DropCheckConstraint(
                name: "ck_pe_pdtic_aprovado",
                table: "pe_pdtic");

            migrationBuilder.DropCheckConstraint(
                name: "ck_pe_pdtic_encerrado",
                table: "pe_pdtic");

            migrationBuilder.DropCheckConstraint(
                name: "ck_pe_pdtic_enviado",
                table: "pe_pdtic");

            migrationBuilder.DropCheckConstraint(
                name: "ck_pe_pdtic_publicado",
                table: "pe_pdtic");

            migrationBuilder.DropIndex(
                name: "ix_pe_deliberacao_doc_versao",
                table: "pe_deliberacao");

            migrationBuilder.DropCheckConstraint(
                name: "ck_pe_deliberacao_doc_versao",
                table: "pe_deliberacao");

            migrationBuilder.DropColumn(
                name: "aprovado_em",
                table: "pe_pdtic");

            migrationBuilder.DropColumn(
                name: "encerrado_em",
                table: "pe_pdtic");

            migrationBuilder.DropColumn(
                name: "encerramento_motivo",
                table: "pe_pdtic");

            migrationBuilder.DropColumn(
                name: "enviado_em",
                table: "pe_pdtic");

            migrationBuilder.DropColumn(
                name: "publicado_em",
                table: "pe_pdtic");

            migrationBuilder.DropColumn(
                name: "revisao_justificativa",
                table: "pe_pdtic");

            migrationBuilder.DropColumn(
                name: "doc_versao_id",
                table: "pe_deliberacao");

            migrationBuilder.CreateIndex(
                name: "ux_pe_pdtic_atual",
                table: "pe_pdtic",
                column: "orgao_id",
                unique: true,
                filter: "situacao NOT IN ('encerrado','substituido')");
        }
    }
}
