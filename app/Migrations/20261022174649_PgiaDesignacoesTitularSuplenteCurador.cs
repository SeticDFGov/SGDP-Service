using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace demanda_service.Migrations
{
    /// <inheritdoc />
    public partial class PgiaDesignacoesTitularSuplenteCurador : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_pgia_responsavel_ia_vigente",
                table: "pgia_responsavel_ia");

            migrationBuilder.DropIndex(
                name: "ux_pgia_encarregado_vigente",
                table: "pgia_encarregado_dados");

            migrationBuilder.AlterColumn<string>(
                name: "processo_sei_comunicacao",
                table: "pgia_responsavel_ia",
                type: "character varying(25)",
                maxLength: 25,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(25)",
                oldMaxLength: 25);

            migrationBuilder.AlterColumn<DateOnly>(
                name: "inicio_vigencia",
                table: "pgia_responsavel_ia",
                type: "date",
                nullable: true,
                oldClrType: typeof(DateOnly),
                oldType: "date");

            migrationBuilder.AlterColumn<DateOnly>(
                name: "data_comunicacao_sgdi",
                table: "pgia_responsavel_ia",
                type: "date",
                nullable: true,
                oldClrType: typeof(DateOnly),
                oldType: "date");

            migrationBuilder.AlterColumn<string>(
                name: "ato_tipo",
                table: "pgia_responsavel_ia",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(30)",
                oldMaxLength: 30);

            migrationBuilder.AlterColumn<string>(
                name: "ato_numero",
                table: "pgia_responsavel_ia",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(30)",
                oldMaxLength: 30);

            migrationBuilder.AlterColumn<DateOnly>(
                name: "ato_data",
                table: "pgia_responsavel_ia",
                type: "date",
                nullable: true,
                oldClrType: typeof(DateOnly),
                oldType: "date");

            migrationBuilder.AddColumn<bool>(
                name: "ato_pendente",
                table: "pgia_responsavel_ia",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "posicao",
                table: "pgia_responsavel_ia",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "Titular");

            migrationBuilder.AlterColumn<string>(
                name: "processo_sei_comunicacao",
                table: "pgia_encarregado_dados",
                type: "character varying(25)",
                maxLength: 25,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(25)",
                oldMaxLength: 25);

            migrationBuilder.AlterColumn<DateOnly>(
                name: "inicio_vigencia",
                table: "pgia_encarregado_dados",
                type: "date",
                nullable: true,
                oldClrType: typeof(DateOnly),
                oldType: "date");

            migrationBuilder.AlterColumn<DateOnly>(
                name: "data_comunicacao_sgdi",
                table: "pgia_encarregado_dados",
                type: "date",
                nullable: true,
                oldClrType: typeof(DateOnly),
                oldType: "date");

            migrationBuilder.AlterColumn<string>(
                name: "ato_tipo",
                table: "pgia_encarregado_dados",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(30)",
                oldMaxLength: 30);

            migrationBuilder.AlterColumn<string>(
                name: "ato_numero",
                table: "pgia_encarregado_dados",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(30)",
                oldMaxLength: 30);

            migrationBuilder.AlterColumn<DateOnly>(
                name: "ato_data",
                table: "pgia_encarregado_dados",
                type: "date",
                nullable: true,
                oldClrType: typeof(DateOnly),
                oldType: "date");

            migrationBuilder.AddColumn<bool>(
                name: "ato_pendente",
                table: "pgia_encarregado_dados",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "posicao",
                table: "pgia_encarregado_dados",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "Titular");

            migrationBuilder.CreateTable(
                name: "pgia_curador_dados",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    orgao_id = table.Column<long>(type: "bigint", nullable: false),
                    agente_id = table.Column<Guid>(type: "uuid", nullable: false),
                    posicao = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false, defaultValue: "Titular"),
                    ato_pendente = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    ato_tipo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    ato_numero = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    ato_data = table.Column<DateOnly>(type: "date", nullable: true),
                    processo_sei_comunicacao = table.Column<string>(type: "character varying(25)", maxLength: 25, nullable: true),
                    data_comunicacao_sgdi = table.Column<DateOnly>(type: "date", nullable: true),
                    documento_id = table.Column<long>(type: "bigint", nullable: true),
                    inicio_vigencia = table.Column<DateOnly>(type: "date", nullable: true),
                    fim_vigencia = table.Column<DateOnly>(type: "date", nullable: true),
                    ativo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    criado_por = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    alterado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    alterado_por = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_pgia_curador_dados", x => x.id);
                    table.CheckConstraint("ck_pgia_curador_dados_ato_pendente", "ato_pendente OR (ato_tipo IS NOT NULL AND ato_numero IS NOT NULL AND ato_data IS NOT NULL AND processo_sei_comunicacao IS NOT NULL AND data_comunicacao_sgdi IS NOT NULL AND inicio_vigencia IS NOT NULL)");
                    table.CheckConstraint("ck_pgia_curador_dados_posicao", "posicao IN ('Titular','Suplente')");
                    table.ForeignKey(
                        name: "fk_pgia_curador_dados_agente",
                        column: x => x.agente_id,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_pgia_curador_dados_doc",
                        column: x => x.documento_id,
                        principalTable: "pgia_documento",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_pgia_curador_dados_orgao",
                        column: x => x.orgao_id,
                        principalTable: "pgia_orgao",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ux_pgia_responsavel_ia_vigente",
                table: "pgia_responsavel_ia",
                columns: new[] { "orgao_id", "posicao" },
                unique: true,
                filter: "ativo");

            migrationBuilder.AddCheckConstraint(
                name: "ck_pgia_responsavel_ia_ato_pendente",
                table: "pgia_responsavel_ia",
                sql: "ato_pendente OR (ato_tipo IS NOT NULL AND ato_numero IS NOT NULL AND ato_data IS NOT NULL AND processo_sei_comunicacao IS NOT NULL AND data_comunicacao_sgdi IS NOT NULL AND inicio_vigencia IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_pgia_responsavel_ia_posicao",
                table: "pgia_responsavel_ia",
                sql: "posicao IN ('Titular','Suplente')");

            migrationBuilder.CreateIndex(
                name: "ux_pgia_encarregado_vigente",
                table: "pgia_encarregado_dados",
                columns: new[] { "orgao_id", "posicao" },
                unique: true,
                filter: "ativo");

            migrationBuilder.AddCheckConstraint(
                name: "ck_pgia_encarregado_dados_ato_pendente",
                table: "pgia_encarregado_dados",
                sql: "ato_pendente OR (ato_tipo IS NOT NULL AND ato_numero IS NOT NULL AND ato_data IS NOT NULL AND processo_sei_comunicacao IS NOT NULL AND data_comunicacao_sgdi IS NOT NULL AND inicio_vigencia IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_pgia_encarregado_dados_posicao",
                table: "pgia_encarregado_dados",
                sql: "posicao IN ('Titular','Suplente')");

            migrationBuilder.CreateIndex(
                name: "ix_pgia_curador_dados_agente",
                table: "pgia_curador_dados",
                column: "agente_id");

            migrationBuilder.CreateIndex(
                name: "ix_pgia_curador_dados_doc",
                table: "pgia_curador_dados",
                column: "documento_id");

            migrationBuilder.CreateIndex(
                name: "ux_pgia_curador_dados_vigente",
                table: "pgia_curador_dados",
                columns: new[] { "orgao_id", "posicao" },
                unique: true,
                filter: "ativo");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // O esquema anterior não representa duas coisas desta migration; antes de
            // desfazê-la, os dados são ajustados para caber nele (sem apagar linha):
            // 1) um vigente por órgão: o suplente vigente de um órgão que também tem
            //    titular vigente é encerrado (fica no histórico, com o fim da vigência);
            // 2) os campos do ato pendente (carga da planilha) não aceitam nulo: as
            //    linhas recebem "A completar" e a data de cadastro (dia de Brasília).
            // A tabela do Curador de Dados de Negócio sai inteira (é só desta migration).
            foreach (var tabela in new[] { "pgia_responsavel_ia", "pgia_encarregado_dados" })
            {
                migrationBuilder.Sql($@"
UPDATE {tabela} AS s
   SET ativo = false,
       fim_vigencia = COALESCE(s.fim_vigencia, (NOW() AT TIME ZONE 'America/Sao_Paulo')::date),
       alterado_em = NOW(),
       alterado_por = 'migration-down'
 WHERE s.ativo AND s.posicao = 'Suplente'
   AND EXISTS (SELECT 1 FROM {tabela} t
                WHERE t.orgao_id = s.orgao_id AND t.ativo AND t.posicao = 'Titular');

UPDATE {tabela}
   SET ato_tipo = COALESCE(ato_tipo, 'A completar'),
       ato_numero = COALESCE(ato_numero, 'A completar'),
       processo_sei_comunicacao = COALESCE(processo_sei_comunicacao, 'A completar'),
       ato_data = COALESCE(ato_data, (criado_em AT TIME ZONE 'America/Sao_Paulo')::date),
       data_comunicacao_sgdi = COALESCE(data_comunicacao_sgdi, (criado_em AT TIME ZONE 'America/Sao_Paulo')::date),
       inicio_vigencia = COALESCE(inicio_vigencia, (criado_em AT TIME ZONE 'America/Sao_Paulo')::date)
 WHERE ato_pendente;");
            }

            migrationBuilder.DropTable(
                name: "pgia_curador_dados");

            migrationBuilder.DropIndex(
                name: "ux_pgia_responsavel_ia_vigente",
                table: "pgia_responsavel_ia");

            migrationBuilder.DropCheckConstraint(
                name: "ck_pgia_responsavel_ia_ato_pendente",
                table: "pgia_responsavel_ia");

            migrationBuilder.DropCheckConstraint(
                name: "ck_pgia_responsavel_ia_posicao",
                table: "pgia_responsavel_ia");

            migrationBuilder.DropIndex(
                name: "ux_pgia_encarregado_vigente",
                table: "pgia_encarregado_dados");

            migrationBuilder.DropCheckConstraint(
                name: "ck_pgia_encarregado_dados_ato_pendente",
                table: "pgia_encarregado_dados");

            migrationBuilder.DropCheckConstraint(
                name: "ck_pgia_encarregado_dados_posicao",
                table: "pgia_encarregado_dados");

            migrationBuilder.DropColumn(
                name: "ato_pendente",
                table: "pgia_responsavel_ia");

            migrationBuilder.DropColumn(
                name: "posicao",
                table: "pgia_responsavel_ia");

            migrationBuilder.DropColumn(
                name: "ato_pendente",
                table: "pgia_encarregado_dados");

            migrationBuilder.DropColumn(
                name: "posicao",
                table: "pgia_encarregado_dados");

            migrationBuilder.AlterColumn<string>(
                name: "processo_sei_comunicacao",
                table: "pgia_responsavel_ia",
                type: "character varying(25)",
                maxLength: 25,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(25)",
                oldMaxLength: 25,
                oldNullable: true);

            migrationBuilder.AlterColumn<DateOnly>(
                name: "inicio_vigencia",
                table: "pgia_responsavel_ia",
                type: "date",
                nullable: false,
                oldClrType: typeof(DateOnly),
                oldType: "date",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateOnly>(
                name: "data_comunicacao_sgdi",
                table: "pgia_responsavel_ia",
                type: "date",
                nullable: false,
                oldClrType: typeof(DateOnly),
                oldType: "date",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ato_tipo",
                table: "pgia_responsavel_ia",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(30)",
                oldMaxLength: 30,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ato_numero",
                table: "pgia_responsavel_ia",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(30)",
                oldMaxLength: 30,
                oldNullable: true);

            migrationBuilder.AlterColumn<DateOnly>(
                name: "ato_data",
                table: "pgia_responsavel_ia",
                type: "date",
                nullable: false,
                oldClrType: typeof(DateOnly),
                oldType: "date",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "processo_sei_comunicacao",
                table: "pgia_encarregado_dados",
                type: "character varying(25)",
                maxLength: 25,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(25)",
                oldMaxLength: 25,
                oldNullable: true);

            migrationBuilder.AlterColumn<DateOnly>(
                name: "inicio_vigencia",
                table: "pgia_encarregado_dados",
                type: "date",
                nullable: false,
                oldClrType: typeof(DateOnly),
                oldType: "date",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateOnly>(
                name: "data_comunicacao_sgdi",
                table: "pgia_encarregado_dados",
                type: "date",
                nullable: false,
                oldClrType: typeof(DateOnly),
                oldType: "date",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ato_tipo",
                table: "pgia_encarregado_dados",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(30)",
                oldMaxLength: 30,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ato_numero",
                table: "pgia_encarregado_dados",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(30)",
                oldMaxLength: 30,
                oldNullable: true);

            migrationBuilder.AlterColumn<DateOnly>(
                name: "ato_data",
                table: "pgia_encarregado_dados",
                type: "date",
                nullable: false,
                oldClrType: typeof(DateOnly),
                oldType: "date",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "ux_pgia_responsavel_ia_vigente",
                table: "pgia_responsavel_ia",
                column: "orgao_id",
                unique: true,
                filter: "ativo");

            migrationBuilder.CreateIndex(
                name: "ux_pgia_encarregado_vigente",
                table: "pgia_encarregado_dados",
                column: "orgao_id",
                unique: true,
                filter: "ativo");
        }
    }
}
