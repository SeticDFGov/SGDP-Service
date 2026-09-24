using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace demanda_service.Migrations
{
    /// <inheritdoc />
    public partial class PeDocumento : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_pe_modelo_historico_entidade",
                table: "pe_modelo_historico");

            migrationBuilder.DropCheckConstraint(
                name: "ck_pe_arquivo_dono_tipo",
                table: "pe_arquivo");

            migrationBuilder.CreateTable(
                name: "pe_doc_modelo",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    tipo = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ativo = table.Column<bool>(type: "boolean", nullable: false),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    criado_por = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    alterado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    alterado_por = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_pe_doc_modelo", x => x.id);
                    table.CheckConstraint("ck_pe_doc_modelo_tipo", "tipo IN ('pdtic')");
                });

            migrationBuilder.CreateTable(
                name: "pe_doc_versao",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    pdtic_id = table.Column<long>(type: "bigint", nullable: false),
                    numero = table.Column<int>(type: "integer", nullable: false),
                    situacao = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    arquivo_id = table.Column<long>(type: "bigint", nullable: false),
                    hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    paginas = table.Column<int>(type: "integer", nullable: false),
                    gerado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    gerado_por = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_pe_doc_versao", x => x.id);
                    table.CheckConstraint("ck_pe_doc_versao_numero", "numero >= 1");
                    table.CheckConstraint("ck_pe_doc_versao_paginas", "paginas >= 1");
                    table.CheckConstraint("ck_pe_doc_versao_situacao", "situacao IN ('minuta','enviada','aprovada','publicada')");
                    table.ForeignKey(
                        name: "fk_pe_doc_versao_arquivo",
                        column: x => x.arquivo_id,
                        principalTable: "pe_arquivo",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_pe_doc_versao_pdtic",
                        column: x => x.pdtic_id,
                        principalTable: "pe_pdtic",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "pe_doc_capitulo",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    modelo_id = table.Column<long>(type: "bigint", nullable: false),
                    pai_id = table.Column<long>(type: "bigint", nullable: true),
                    chave = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    titulo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    numerado = table.Column<bool>(type: "boolean", nullable: false),
                    ordem = table.Column<int>(type: "integer", nullable: false),
                    obrigatorio = table.Column<bool>(type: "boolean", nullable: false),
                    travado = table.Column<bool>(type: "boolean", nullable: false),
                    inciso_decreto = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    passo_chave = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    sistema = table.Column<bool>(type: "boolean", nullable: false),
                    excluido_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    criado_por = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    alterado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    alterado_por = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_pe_doc_capitulo", x => x.id);
                    table.CheckConstraint("ck_pe_doc_capitulo_chave", "chave ~ '^[a-z][a-z0-9_]*$'");
                    table.CheckConstraint("ck_pe_doc_capitulo_inciso", "inciso_decreto IS NULL OR inciso_decreto ~ '^(I|II|III|IV|V|VI|VII|VIII|IX)(,(I|II|III|IV|V|VI|VII|VIII|IX))*$'");
                    table.CheckConstraint("ck_pe_doc_capitulo_pai", "pai_id IS NULL OR pai_id <> id");
                    table.CheckConstraint("ck_pe_doc_capitulo_travado", "NOT travado OR obrigatorio");
                    table.ForeignKey(
                        name: "fk_pe_doc_capitulo_modelo",
                        column: x => x.modelo_id,
                        principalTable: "pe_doc_modelo",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_pe_doc_capitulo_pai",
                        column: x => x.pai_id,
                        principalTable: "pe_doc_capitulo",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "pe_doc_bloco",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    capitulo_id = table.Column<long>(type: "bigint", nullable: false),
                    ordem = table.Column<int>(type: "integer", nullable: false),
                    tipo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    config = table.Column<string>(type: "jsonb", nullable: false),
                    sistema = table.Column<bool>(type: "boolean", nullable: false),
                    excluido_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    criado_por = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    alterado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    alterado_por = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_pe_doc_bloco", x => x.id);
                    table.CheckConstraint("ck_pe_doc_bloco_tipo", "tipo IN ('texto','tabela_secao','lista_tema','matriz_swot','fluxo','quebra_pagina')");
                    table.ForeignKey(
                        name: "fk_pe_doc_bloco_capitulo",
                        column: x => x.capitulo_id,
                        principalTable: "pe_doc_capitulo",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "pe_doc_orgao",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    pdtic_id = table.Column<long>(type: "bigint", nullable: false),
                    capitulo_id = table.Column<long>(type: "bigint", nullable: false),
                    oculto = table.Column<bool>(type: "boolean", nullable: false),
                    titulo_proprio = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    criado_por = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    alterado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    alterado_por = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_pe_doc_orgao", x => x.id);
                    table.ForeignKey(
                        name: "fk_pe_doc_orgao_capitulo",
                        column: x => x.capitulo_id,
                        principalTable: "pe_doc_capitulo",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_pe_doc_orgao_pdtic",
                        column: x => x.pdtic_id,
                        principalTable: "pe_pdtic",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "pe_doc_orgao_bloco",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    pdtic_id = table.Column<long>(type: "bigint", nullable: false),
                    bloco_id = table.Column<long>(type: "bigint", nullable: false),
                    texto = table.Column<string>(type: "jsonb", nullable: false),
                    modelo_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    editado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    editado_por = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_pe_doc_orgao_bloco", x => x.id);
                    table.ForeignKey(
                        name: "fk_pe_doc_orgao_bloco_bloco",
                        column: x => x.bloco_id,
                        principalTable: "pe_doc_bloco",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_pe_doc_orgao_bloco_pdtic",
                        column: x => x.pdtic_id,
                        principalTable: "pe_pdtic",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.AddCheckConstraint(
                name: "ck_pe_modelo_historico_entidade",
                table: "pe_modelo_historico",
                sql: "entidade IN ('nivel','etapa','passo','secao','campo','opcao','orgao_nivel','orgao_ajuste','doc_capitulo','doc_bloco')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_pe_arquivo_dono_tipo",
                table: "pe_arquivo",
                sql: "dono_tipo IS NULL OR dono_tipo IN ('registro','pdtic','doc_modelo')");

            migrationBuilder.CreateIndex(
                name: "ix_pe_doc_bloco_capitulo",
                table: "pe_doc_bloco",
                columns: new[] { "capitulo_id", "ordem" });

            migrationBuilder.CreateIndex(
                name: "ix_pe_doc_capitulo_pai",
                table: "pe_doc_capitulo",
                column: "pai_id");

            migrationBuilder.CreateIndex(
                name: "ux_pe_doc_capitulo_chave",
                table: "pe_doc_capitulo",
                columns: new[] { "modelo_id", "chave" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_pe_doc_modelo_ativo",
                table: "pe_doc_modelo",
                column: "tipo",
                unique: true,
                filter: "ativo");

            migrationBuilder.CreateIndex(
                name: "ix_pe_doc_orgao_capitulo",
                table: "pe_doc_orgao",
                column: "capitulo_id");

            migrationBuilder.CreateIndex(
                name: "ux_pe_doc_orgao",
                table: "pe_doc_orgao",
                columns: new[] { "pdtic_id", "capitulo_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_pe_doc_orgao_bloco_bloco",
                table: "pe_doc_orgao_bloco",
                column: "bloco_id");

            migrationBuilder.CreateIndex(
                name: "ux_pe_doc_orgao_bloco",
                table: "pe_doc_orgao_bloco",
                columns: new[] { "pdtic_id", "bloco_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_pe_doc_versao_arquivo",
                table: "pe_doc_versao",
                column: "arquivo_id");

            migrationBuilder.CreateIndex(
                name: "ux_pe_doc_versao_numero",
                table: "pe_doc_versao",
                columns: new[] { "pdtic_id", "numero" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // O modelo do documento sai junto com as tabelas: a versão do conteúdo semeado volta
            // a 2 (a da E4), para que a E5, aplicada de novo, carregue o modelo do documento
            migrationBuilder.Sql(
                "UPDATE pe_configuracao SET valor = '2' WHERE chave = 'seed_modelo_versao' " +
                "AND CASE WHEN (valor #>> '{}') ~ '^[0-9]+$' THEN (valor #>> '{}')::int > 2 ELSE false END;");

            migrationBuilder.DropTable(
                name: "pe_doc_orgao");

            migrationBuilder.DropTable(
                name: "pe_doc_orgao_bloco");

            migrationBuilder.DropTable(
                name: "pe_doc_versao");

            migrationBuilder.DropTable(
                name: "pe_doc_bloco");

            migrationBuilder.DropTable(
                name: "pe_doc_capitulo");

            migrationBuilder.DropTable(
                name: "pe_doc_modelo");

            // O que era só da E5 nas tabelas que ficam (sem isso, os CHECKs antigos não voltam):
            // o histórico do modelo do documento e os arquivos do documento (PDFs e imagens dos textos)
            migrationBuilder.Sql("DELETE FROM pe_modelo_historico WHERE entidade IN ('doc_capitulo','doc_bloco');");
            migrationBuilder.Sql("DELETE FROM pe_arquivo WHERE dono_tipo IN ('pdtic','doc_modelo');");

            migrationBuilder.DropCheckConstraint(
                name: "ck_pe_modelo_historico_entidade",
                table: "pe_modelo_historico");

            migrationBuilder.DropCheckConstraint(
                name: "ck_pe_arquivo_dono_tipo",
                table: "pe_arquivo");

            migrationBuilder.AddCheckConstraint(
                name: "ck_pe_modelo_historico_entidade",
                table: "pe_modelo_historico",
                sql: "entidade IN ('nivel','etapa','passo','secao','campo','opcao','orgao_nivel','orgao_ajuste')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_pe_arquivo_dono_tipo",
                table: "pe_arquivo",
                sql: "dono_tipo IS NULL OR dono_tipo IN ('registro')");
        }
    }
}
