using System.Collections;
using System.Reflection;
using Models.Planejamento;
using Xunit;

namespace test.planejamento;

/// <summary>
/// Migration PeAcompanhamento (E7, rodada B) por INSPEÇÃO das operações, lidas por reflexão (o
/// provider InMemory não executa migration), no padrão das migrations anteriores do módulo. O
/// que se prova: só mexe em tabelas pe_; cria pe_ciclo (com os CHECKs, os dois índices únicos e
/// a FK em cascata para o PDTIC); as colunas novas nas tabelas que já existem são nuláveis (o
/// doc_tipo com o padrão constante 'pdtic', que o PostgreSQL grava sem reescrever a tabela, e o
/// CHECK que exige o valor); os índices únicos do documento passam a valer por documento (o do
/// PDTIC e o do RR filtrados, e os do RA por ciclo, em SQL, porque juntam colunas das duas
/// entidades que dividem a tabela); os CHECKs dos tipos de modelo e de bloco ganham os valores
/// novos; e o Down apaga o que é só da rodada B antes de tirar as colunas e volta a versão do
/// conteúdo semeado para 5.
/// </summary>
public class PeMigrationAcompanhamentoTest
{
    private const string NomeMigration = "demanda_service.Migrations.PeAcompanhamento";

    private static readonly string[] Tabelas =
    {
        "pe_ciclo", "pe_secao", "pe_registro", "pe_doc_versao", "pe_doc_orgao", "pe_doc_orgao_bloco", "pe_doc_modelo", "pe_doc_bloco"
    };

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

    private static string? Tabela(object operacao) => Texto(operacao, Tipo(operacao) is "CreateTableOperation" or "DropTableOperation" ? "Name" : "Table");

    [Fact]
    public void Up_SoTabelasPe_CriaOCiclo_ColunasNulaveis_EOsIndicesPorDocumento()
    {
        var operacoes = Operacoes();
        Assert.All(operacoes.Where(o => Tipo(o) != "SqlOperation"), o => Assert.Contains(Tabela(o), Tabelas));

        // pe_ciclo: PK, os CHECKs e a FK em cascata para o PDTIC
        var ciclo = Assert.Single(operacoes, o => Tipo(o) == "CreateTableOperation");
        Assert.Equal("pe_ciclo", Texto(ciclo, "Name"));
        var checks = ((IEnumerable)Valor(ciclo, "CheckConstraints")!).Cast<object>().Select(c => Texto(c, "Name")).OrderBy(n => n, StringComparer.Ordinal);
        Assert.Equal(new[] { "ck_pe_ciclo_fechado", "ck_pe_ciclo_numero", "ck_pe_ciclo_periodo", "ck_pe_ciclo_reaberto", "ck_pe_ciclo_situacao", "ck_pe_ciclo_tipo" }, checks);
        var fk = Assert.Single(((IEnumerable)Valor(ciclo, "ForeignKeys")!).Cast<object>());
        Assert.Equal(("fk_pe_ciclo_pdtic", "pe_pdtic", "Cascade"), (Texto(fk, "Name"), Texto(fk, "PrincipalTable"), Valor(fk, "OnDelete")!.ToString()));

        // As colunas nas tabelas que já existem: nuláveis; o doc_tipo com o padrão constante 'pdtic'
        var colunas = operacoes.Where(o => Tipo(o) == "AddColumnOperation")
            .ToDictionary(o => $"{Texto(o, "Table")}.{Texto(o, "Name")}", o => (Tipo: Texto(o, "ColumnType"), Nula: (bool)Valor(o, "IsNullable")!,
                Padrao: Valor(o, "DefaultValue"), Sql: Valor(o, "DefaultValueSql")));
        Assert.Equal(new[]
        {
            "pe_doc_orgao.ciclo_id", "pe_doc_orgao.doc_tipo", "pe_doc_orgao_bloco.ciclo_id", "pe_doc_orgao_bloco.doc_tipo", "pe_doc_versao.ciclo_id",
            "pe_doc_versao.doc_tipo", "pe_registro.ciclo_id", "pe_secao.por_ciclo"
        }, colunas.Keys.OrderBy(k => k, StringComparer.Ordinal));
        Assert.All(colunas.Values, c => Assert.True(c.Nula && c.Sql == null));
        Assert.All(colunas.Where(c => !c.Key.EndsWith(".doc_tipo")), c => Assert.Null(c.Value.Padrao));
        Assert.All(colunas.Where(c => c.Key.EndsWith(".doc_tipo")), c => Assert.Equal(("character varying(10)", (object?)"pdtic"), (c.Value.Tipo, c.Value.Padrao)));
        Assert.Equal("character varying(20)", colunas["pe_secao.por_ciclo"].Tipo);

        // Os CHECKs novos e os refeitos
        var adicionados = operacoes.Where(o => Tipo(o) == "AddCheckConstraintOperation").ToDictionary(o => Texto(o, "Name")!, o => Texto(o, "Sql"));
        Assert.Equal("por_ciclo IS NULL OR por_ciclo IN ('monitoramento','avaliacao')", adicionados["ck_pe_secao_por_ciclo"]);
        Assert.Equal("tipo IN ('pdtic','ra','rr')", adicionados["ck_pe_doc_modelo_tipo"]);
        Assert.Contains("'acoes_por_situacao','metas_por_resultado','riscos_ocorridos','medicoes'", adicionados["ck_pe_doc_bloco_tipo"]);
        foreach (var tabela in new[] { "pe_doc_versao", "pe_doc_orgao", "pe_doc_orgao_bloco" })
            Assert.Equal("doc_tipo IS NOT NULL AND doc_tipo IN ('pdtic','ra','rr') AND (doc_tipo = 'ra') = (ciclo_id IS NOT NULL)",
                adicionados[$"ck_{tabela}_documento"]);
        Assert.Equal(new[] { "ck_pe_doc_bloco_tipo", "ck_pe_doc_modelo_tipo" },
            operacoes.Where(o => Tipo(o) == "DropCheckConstraintOperation").Select(o => Texto(o, "Name")).OrderBy(n => n, StringComparer.Ordinal));

        // Os índices únicos do documento passam a valer por documento
        Assert.Equal(new[] { "ux_pe_doc_orgao", "ux_pe_doc_orgao_bloco", "ux_pe_doc_versao_numero" },
            operacoes.Where(o => Tipo(o) == "DropIndexOperation").Select(o => Texto(o, "Name")).OrderBy(n => n, StringComparer.Ordinal));
        var indices = operacoes.Where(o => Tipo(o) == "CreateIndexOperation")
            .ToDictionary(o => Texto(o, "Name")!, o => (Colunas: string.Join(",", (string[])Valor(o, "Columns")!), Unico: (bool)Valor(o, "IsUnique")!, Filtro: Texto(o, "Filter")));
        Assert.Equal(("pdtic_id,numero", true, "doc_tipo = 'pdtic'"), indices["ux_pe_doc_versao_numero"]);
        Assert.Equal(("pdtic_id,numero", true, "doc_tipo = 'rr'"), indices["ux_pe_doc_versao_numero_rr"]);
        Assert.Equal(("pdtic_id,capitulo_id", true, "doc_tipo <> 'ra'"), indices["ux_pe_doc_orgao"]);
        Assert.Equal(("pdtic_id,bloco_id", true, "doc_tipo <> 'ra'"), indices["ux_pe_doc_orgao_bloco"]);
        Assert.Equal(("pdtic_id,tipo,numero", true, (string?)null), indices["ux_pe_ciclo_numero"]);
        Assert.Equal(("pdtic_id", true, "tipo = 'avaliacao' AND situacao = 'aberto'"), indices["ux_pe_ciclo_avaliacao_aberta"]);
        Assert.Equal(("ciclo_id", false, (string?)null), indices["ix_pe_registro_ciclo"]);

        // As FKs do ciclo, em cascata
        var fks = operacoes.Where(o => Tipo(o) == "AddForeignKeyOperation").ToList();
        Assert.Equal(4, fks.Count);
        Assert.All(fks, f => Assert.Equal(("pe_ciclo", "Cascade"), (Texto(f, "PrincipalTable"), Valor(f, "OnDelete")!.ToString())));

        // Os índices do RA por ciclo, em SQL, só em tabelas pe_ e no fim
        var sql = Assert.Single(operacoes, o => Tipo(o) == "SqlOperation");
        Assert.Same(sql, operacoes[^1]);
        var comandos = Texto(sql, "Sql")!.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        Assert.Equal(new[]
        {
            "CREATE UNIQUE INDEX ux_pe_doc_orgao_ciclo ON pe_doc_orgao (ciclo_id, capitulo_id) WHERE ciclo_id IS NOT NULL",
            "CREATE UNIQUE INDEX ux_pe_doc_orgao_bloco_ciclo ON pe_doc_orgao_bloco (ciclo_id, bloco_id) WHERE ciclo_id IS NOT NULL",
            "CREATE UNIQUE INDEX ux_pe_doc_versao_ciclo ON pe_doc_versao (ciclo_id, numero) WHERE ciclo_id IS NOT NULL"
        }, comandos);
    }

    [Fact]
    public void Down_ApagaORaORrEOsDadosDosCiclos_AntesDasColunas_EVoltaAVersao5()
    {
        var operacoes = Operacoes("DownOperations");
        Assert.All(operacoes.Where(o => Tipo(o) != "SqlOperation"), o => Assert.Contains(Tabela(o), Tabelas));

        // Primeiro o SQL: os dados da rodada B; depois os índices do RA
        var sqls = operacoes.Where(o => Tipo(o) == "SqlOperation").Select(o => Texto(o, "Sql")!).ToList();
        Assert.Equal(2, sqls.Count);
        Assert.Equal(0, operacoes.FindIndex(o => Tipo(o) == "SqlOperation"));
        var limpeza = sqls[0].Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        Assert.All(limpeza, c => Assert.Matches(@"^(CREATE TEMP TABLE pe_rodada_b_pdfs |DELETE FROM pe_[a-z_]+ |UPDATE pe_configuracao SET valor = '5' )", c));
        Assert.Contains("DELETE FROM pe_registro WHERE ciclo_id IS NOT NULL", limpeza);
        Assert.Contains("DELETE FROM pe_doc_modelo WHERE tipo <> 'pdtic'", limpeza);
        Assert.StartsWith("UPDATE pe_configuracao SET valor = '5' WHERE chave = 'seed_modelo_versao'", limpeza[^1]);
        Assert.Equal("DROP INDEX IF EXISTS ux_pe_doc_orgao_ciclo; DROP INDEX IF EXISTS ux_pe_doc_orgao_bloco_ciclo; DROP INDEX IF EXISTS ux_pe_doc_versao_ciclo;",
            sqls[1]);

        // O ciclo sai e as colunas também; os índices e os CHECKs antigos voltam
        Assert.Equal("pe_ciclo", Texto(Assert.Single(operacoes, o => Tipo(o) == "DropTableOperation"), "Name"));
        Assert.Equal(8, operacoes.Count(o => Tipo(o) == "DropColumnOperation"));
        var recriados = operacoes.Where(o => Tipo(o) == "CreateIndexOperation").ToList();
        Assert.Equal(new[] { "ux_pe_doc_orgao", "ux_pe_doc_orgao_bloco", "ux_pe_doc_versao_numero" },
            recriados.Select(o => Texto(o, "Name")).OrderBy(n => n, StringComparer.Ordinal));
        Assert.All(recriados, o => Assert.Null(Texto(o, "Filter")));
        var antigos = operacoes.Where(o => Tipo(o) == "AddCheckConstraintOperation").ToDictionary(o => Texto(o, "Name")!, o => Texto(o, "Sql"));
        Assert.Equal("tipo IN ('pdtic')", antigos["ck_pe_doc_modelo_tipo"]);
        Assert.Equal("tipo IN ('texto','tabela_secao','lista_tema','matriz_swot','fluxo','quebra_pagina')", antigos["ck_pe_doc_bloco_tipo"]);
    }
}
