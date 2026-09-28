using System.Collections;
using System.Reflection;
using Models.Planejamento;
using Xunit;

namespace test.planejamento;

/// <summary>
/// Migration PeModoLivreValidacao (F3) por INSPEÇÃO das operações, lidas por reflexão (o provider
/// InMemory não executa migration), no padrão das migrations anteriores do módulo. O que se prova:
/// só mexe em tabelas pe_ (pgia_orgao só é apontada por FK); cria pe_orgao_passo_detalhe (PK
/// composta, as três FKs RESTRICT e o índice por passo); acrescenta a pe_pdtic_passo as três colunas
/// nuláveis da validação com os dois CHECKs; troca o CHECK do histórico do modelo com
/// "configuracao"; e o Down apaga o histórico da configuração e volta a versão do conteúdo semeado a
/// 7 antes de desfazer o resto.
/// </summary>
public class PeMigrationModoLivreTest
{
    private const string NomeMigration = "demanda_service.Migrations.PeModoLivreValidacao";

    private static readonly string[] Tabelas = { "pe_orgao_passo_detalhe", "pe_pdtic_passo", "pe_modelo_historico" };

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

    private static List<object> Lista(object objeto, string propriedade) =>
        ((IEnumerable)Valor(objeto, propriedade)!).Cast<object>().ToList();

    private static string Tipo(object operacao) => operacao.GetType().Name;

    private static string? Tabela(object operacao) => Texto(operacao, Tipo(operacao) is "CreateTableOperation" or "DropTableOperation" ? "Name" : "Table");

    [Fact]
    public void Up_SoTabelasPe_ATabelaNova_AsColunasDaValidacao_EOCheckDoHistorico()
    {
        var operacoes = Operacoes();
        Assert.All(operacoes, o => Assert.Contains(Tabela(o), Tabelas));
        Assert.DoesNotContain(operacoes, o => Tipo(o) == "SqlOperation");

        // pe_orgao_passo_detalhe: PK (orgao_id, passo_id) e as três FKs RESTRICT
        var tabela = Assert.Single(operacoes, o => Tipo(o) == "CreateTableOperation");
        Assert.Equal("pe_orgao_passo_detalhe", Texto(tabela, "Name"));
        var colunas = Lista(tabela, "Columns").ToDictionary(c => Texto(c, "Name")!, c => (Tipo: Texto(c, "ColumnType"), Nula: (bool)Valor(c, "IsNullable")!));
        Assert.Equal(new[] { "alterado_em", "alterado_por", "nivel_id", "orgao_id", "passo_id" }, colunas.Keys.OrderBy(k => k, StringComparer.Ordinal));
        Assert.All(colunas.Values, c => Assert.False(c.Nula));
        var pk = Valor(tabela, "PrimaryKey")!;
        Assert.Equal(("pk_pe_orgao_passo_detalhe", "orgao_id,passo_id"), (Texto(pk, "Name"), string.Join(",", (string[])Valor(pk, "Columns")!)));
        var fks = Lista(tabela, "ForeignKeys")
            .Select(f => (Texto(f, "Name"), Texto(f, "PrincipalTable"), Valor(f, "OnDelete")!.ToString()))
            .OrderBy(f => f.Item1, StringComparer.Ordinal)
            .ToList();
        Assert.Equal(new[]
        {
            ("fk_pe_orgao_passo_detalhe_nivel", "pe_nivel", "Restrict"),
            ("fk_pe_orgao_passo_detalhe_orgao", "pgia_orgao", "Restrict"),
            ("fk_pe_orgao_passo_detalhe_passo", "pe_passo", "Restrict")
        }, fks.Select(f => (f.Item1!, f.Item2!, f.Item3!)));

        var indices = operacoes.Where(o => Tipo(o) == "CreateIndexOperation")
            .ToDictionary(o => Texto(o, "Name")!, o => (Tabela: Texto(o, "Table"), Colunas: string.Join(",", (string[])Valor(o, "Columns")!), Unico: (bool)Valor(o, "IsUnique")!));
        Assert.Equal(("pe_orgao_passo_detalhe", "passo_id", false), indices["ix_pe_orgao_passo_detalhe_passo"]);
        Assert.Equal(("pe_orgao_passo_detalhe", "nivel_id", false), indices["ix_pe_orgao_passo_detalhe_nivel"]);

        // As colunas da validação em pe_pdtic_passo: nuláveis e sem padrão (não reescrevem a tabela)
        var novas = operacoes.Where(o => Tipo(o) == "AddColumnOperation")
            .ToDictionary(o => $"{Texto(o, "Table")}.{Texto(o, "Name")}",
                o => (Tipo: Texto(o, "ColumnType"), Nula: (bool)Valor(o, "IsNullable")!, Padrao: Valor(o, "DefaultValue"), Sql: Valor(o, "DefaultValueSql")));
        Assert.Equal(new[] { "pe_pdtic_passo.validacao_alterada_em", "pe_pdtic_passo.validado_em", "pe_pdtic_passo.validado_por" },
            novas.Keys.OrderBy(k => k, StringComparer.Ordinal));
        Assert.All(novas.Values, c => Assert.True(c.Nula && c.Padrao == null && c.Sql == null));
        Assert.Equal("timestamp with time zone", novas["pe_pdtic_passo.validado_em"].Tipo);
        Assert.Equal("timestamp with time zone", novas["pe_pdtic_passo.validacao_alterada_em"].Tipo);
        Assert.Equal("character varying(200)", novas["pe_pdtic_passo.validado_por"].Tipo);

        // Os CHECKs: a validação junta, a mudança só com a validação e o histórico com "configuracao"
        var checks = operacoes.Where(o => Tipo(o) == "AddCheckConstraintOperation").ToDictionary(o => Texto(o, "Name")!, o => Texto(o, "Sql"));
        Assert.Equal("(validado_em IS NULL) = (validado_por IS NULL)", checks["ck_pe_pdtic_passo_validacao"]);
        Assert.Equal("validacao_alterada_em IS NULL OR validado_em IS NOT NULL", checks["ck_pe_pdtic_passo_validacao_alterada"]);
        Assert.EndsWith(",'fluxo_modelo','configuracao')", checks["ck_pe_modelo_historico_entidade"]);
        Assert.Equal("ck_pe_modelo_historico_entidade",
            Texto(Assert.Single(operacoes, o => Tipo(o) == "DropCheckConstraintOperation"), "Name"));
    }

    [Fact]
    public void Down_OHistoricoDaConfiguracaoEAVersao7_AntesDeDesfazerOResto()
    {
        var operacoes = Operacoes("DownOperations");
        Assert.All(operacoes.Where(o => Tipo(o) != "SqlOperation"), o => Assert.Contains(Tabela(o), Tabelas));

        // Primeiro o SQL: o histórico das trocas do modo e a versão do conteúdo semeado de volta a 7
        var sql = Assert.Single(operacoes, o => Tipo(o) == "SqlOperation");
        Assert.Same(sql, operacoes[0]);
        var comandos = Texto(sql, "Sql")!.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        Assert.Equal("DELETE FROM pe_modelo_historico WHERE entidade = 'configuracao'", comandos[0]);
        Assert.StartsWith("UPDATE pe_configuracao SET valor = '7' WHERE chave = 'seed_modelo_versao'", comandos[1]);
        Assert.Equal(2, comandos.Length);

        Assert.Equal("pe_orgao_passo_detalhe", Texto(Assert.Single(operacoes, o => Tipo(o) == "DropTableOperation"), "Name"));
        Assert.Equal(new[] { "validacao_alterada_em", "validado_em", "validado_por" },
            operacoes.Where(o => Tipo(o) == "DropColumnOperation").Select(o => Texto(o, "Name")).OrderBy(n => n, StringComparer.Ordinal));
        var antigo = Assert.Single(operacoes, o => Tipo(o) == "AddCheckConstraintOperation");
        Assert.Equal("ck_pe_modelo_historico_entidade", Texto(antigo, "Name"));
        Assert.EndsWith(",'fluxo_modelo')", Texto(antigo, "Sql"));
        Assert.DoesNotContain("configuracao", Texto(antigo, "Sql"));
    }
}
