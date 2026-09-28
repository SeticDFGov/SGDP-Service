using System.Collections;
using System.Reflection;
using Models.Planejamento;
using Xunit;

namespace test.planejamento;

/// <summary>
/// Migration PeAprovacaoPublicacao (E7, rodada A) por INSPEÇÃO das operações, lidas por
/// reflexão (o provider InMemory não executa migration), no padrão das migrations anteriores
/// do módulo. O que se prova: só mexe em tabelas pe_ (pe_pdtic e pe_deliberacao); as colunas
/// novas são nuláveis e sem default (não reescrevem a tabela); o índice de "um PDTIC atual"
/// vira dois (uma versão em elaboração e uma vigente por órgão); os CHECKs das datas vêm depois
/// do acerto das linhas antigas (SQL só em pe_pdtic); a FK do PDF enviado aponta para
/// pe_doc_versao (RESTRICT); o Down desfaz só isso e volta o índice antigo.
/// </summary>
public class PeMigrationAprovacaoTest
{
    private const string NomeMigration = "demanda_service.Migrations.PeAprovacaoPublicacao";

    private static List<object> Operacoes(string lado = "UpOperations")
    {
        var tipo = typeof(PePdtic).Assembly.GetType(NomeMigration);
        Assert.NotNull(tipo);

        var migration = Activator.CreateInstance(tipo!)!;
        tipo!.GetProperty("ActiveProvider")!.SetValue(migration, "Npgsql.EntityFrameworkCore.PostgreSQL");
        return ((IEnumerable)tipo.GetProperty(lado)!.GetValue(migration)!).Cast<object>().ToList();
    }

    private static object? Valor(object objeto, string propriedade) =>
        objeto.GetType().GetProperty(propriedade, BindingFlags.Public | BindingFlags.Instance)?.GetValue(objeto);

    private static string? Texto(object objeto, string propriedade) => Valor(objeto, propriedade) as string;

    private static string Tipo(object operacao) => operacao.GetType().Name;

    [Fact]
    public void Up_SoTabelasPe_ColunasNulaveis_IndicesChecksEFk()
    {
        var operacoes = Operacoes();
        Assert.All(operacoes, o => Assert.Contains(Tipo(o), new[]
        {
            "DropIndexOperation", "AddColumnOperation", "SqlOperation", "CreateIndexOperation", "AddCheckConstraintOperation", "AddForeignKeyOperation"
        }));
        Assert.All(operacoes.Where(o => Tipo(o) != "SqlOperation"), o => Assert.Contains(Texto(o, "Table"), new[] { "pe_pdtic", "pe_deliberacao" }));

        // As colunas: nuláveis, sem default
        var colunas = operacoes.Where(o => Tipo(o) == "AddColumnOperation")
            .ToDictionary(o => $"{Texto(o, "Table")}.{Texto(o, "Name")}", o => (Tipo: Texto(o, "ColumnType"), Nula: (bool)Valor(o, "IsNullable")!,
                Padrao: Valor(o, "DefaultValue") ?? Valor(o, "DefaultValueSql")));
        Assert.Equal(new[]
        {
            "pe_deliberacao.doc_versao_id", "pe_pdtic.aprovado_em", "pe_pdtic.encerrado_em", "pe_pdtic.encerramento_motivo", "pe_pdtic.enviado_em",
            "pe_pdtic.publicado_em", "pe_pdtic.revisao_justificativa"
        }, colunas.Keys.OrderBy(k => k, StringComparer.Ordinal));
        Assert.All(colunas.Values, c => Assert.True(c.Nula && c.Padrao == null));
        Assert.Equal("timestamp with time zone", colunas["pe_pdtic.enviado_em"].Tipo);
        Assert.Equal("character varying(1000)", colunas["pe_pdtic.encerramento_motivo"].Tipo);
        Assert.Equal("character varying(1000)", colunas["pe_pdtic.revisao_justificativa"].Tipo);
        Assert.Equal("bigint", colunas["pe_deliberacao.doc_versao_id"].Tipo);

        // O índice de "um atual" sai; entram o da versão em elaboração e o da vigente
        Assert.Equal("ux_pe_pdtic_atual", Texto(Assert.Single(operacoes, o => Tipo(o) == "DropIndexOperation"), "Name"));
        var indices = operacoes.Where(o => Tipo(o) == "CreateIndexOperation")
            .ToDictionary(o => Texto(o, "Name")!, o => (Colunas: string.Join(",", (string[])Valor(o, "Columns")!), Unico: (bool)Valor(o, "IsUnique")!, Filtro: Texto(o, "Filter")));
        Assert.Equal(3, indices.Count);
        Assert.Equal(("orgao_id", true, "situacao IN ('em_elaboracao','em_aprovacao','devolvido','aprovado')"), indices["ux_pe_pdtic_em_elaboracao"]);
        Assert.Equal(("orgao_id", true, "situacao IN ('publicado','em_acompanhamento')"), indices["ux_pe_pdtic_vigente"]);
        Assert.Equal(("doc_versao_id", false, (string?)null), indices["ix_pe_deliberacao_doc_versao"]);

        // Os CHECKs das datas e o do PDF só no PDTIC
        var checks = operacoes.Where(o => Tipo(o) == "AddCheckConstraintOperation").ToDictionary(o => Texto(o, "Name")!, o => Texto(o, "Sql"));
        Assert.Equal(new[] { "ck_pe_deliberacao_doc_versao", "ck_pe_pdtic_aprovado", "ck_pe_pdtic_encerrado", "ck_pe_pdtic_enviado", "ck_pe_pdtic_publicado" },
            checks.Keys.OrderBy(k => k, StringComparer.Ordinal));
        Assert.Equal("doc_versao_id IS NULL OR objeto_tipo = 'pdtic'", checks["ck_pe_deliberacao_doc_versao"]);
        Assert.Equal("situacao <> 'em_aprovacao' OR enviado_em IS NOT NULL", checks["ck_pe_pdtic_enviado"]);
        Assert.Equal("(situacao = 'encerrado') = (encerrado_em IS NOT NULL) AND (encerramento_motivo IS NULL OR situacao = 'encerrado')",
            checks["ck_pe_pdtic_encerrado"]);

        // O acerto das linhas antigas vem antes dos CHECKs e só mexe em pe_pdtic
        var sql = Assert.Single(operacoes, o => Tipo(o) == "SqlOperation");
        Assert.True(operacoes.IndexOf(sql) < operacoes.FindIndex(o => Tipo(o) == "AddCheckConstraintOperation"));
        var comandos = Texto(sql, "Sql")!.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        Assert.Equal(4, comandos.Length);
        Assert.All(comandos, c => Assert.StartsWith("UPDATE pe_pdtic SET ", c));

        // A FK do PDF enviado
        var fk = Assert.Single(operacoes, o => Tipo(o) == "AddForeignKeyOperation");
        Assert.Equal(("fk_pe_deliberacao_doc_versao", "pe_deliberacao", "pe_doc_versao", "Restrict"),
            (Texto(fk, "Name"), Texto(fk, "Table"), Texto(fk, "PrincipalTable"), Valor(fk, "OnDelete")!.ToString()));
    }

    [Fact]
    public void Down_DesfazSoARodadaA_EVoltaOIndiceAntigo()
    {
        var operacoes = Operacoes("DownOperations");
        Assert.All(operacoes, o => Assert.Contains(Tipo(o), new[]
        {
            "DropForeignKeyOperation", "DropIndexOperation", "DropCheckConstraintOperation", "DropColumnOperation", "CreateIndexOperation"
        }));
        Assert.All(operacoes, o => Assert.Contains(Texto(o, "Table"), new[] { "pe_pdtic", "pe_deliberacao" }));
        Assert.Equal(7, operacoes.Count(o => Tipo(o) == "DropColumnOperation"));
        Assert.Equal(5, operacoes.Count(o => Tipo(o) == "DropCheckConstraintOperation"));
        var antigo = Assert.Single(operacoes, o => Tipo(o) == "CreateIndexOperation");
        Assert.Equal(("ux_pe_pdtic_atual", "situacao NOT IN ('encerrado','substituido')", true),
            (Texto(antigo, "Name"), Texto(antigo, "Filter"), (bool)Valor(antigo, "IsUnique")!));
        Assert.Same(antigo, operacoes[^1]);
    }
}
