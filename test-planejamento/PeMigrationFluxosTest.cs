using System.Collections;
using System.Reflection;
using Models.Planejamento;
using Xunit;

namespace test.planejamento;

/// <summary>
/// Migration PeFluxos (E6) por INSPEÇÃO das operações, lidas por reflexão (o provider InMemory
/// não executa migration), no padrão das migrations anteriores do módulo. O que se prova: só
/// cria as 2 tabelas dos fluxos (pe_fluxo_modelo e pe_fluxo) e os índices delas; na tabela que
/// já existe só troca o CHECK do domínio do histórico do modelo (ganha os fluxos do modelo); as
/// FKs só apontam para tabelas pe_; o Down desfaz só isso: volta a versão do conteúdo semeado a 3
/// (sem isso, a E6 aplicada de novo acharia a versão 4 e não carregaria os fluxos do guia) e tira
/// do histórico o que era só da E6, para o CHECK antigo voltar.
/// </summary>
public class PeMigrationFluxosTest
{
    private const string NomeMigration = "demanda_service.Migrations.PeFluxos";

    private static readonly string[] Tabelas = { "pe_fluxo", "pe_fluxo_modelo" };

    private static List<object> Operacoes(string lado = "UpOperations")
    {
        var tipo = typeof(PeFluxoModelo).Assembly.GetType(NomeMigration);
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

    private static object Tabela(string nome) =>
        Operacoes().Single(o => Tipo(o) == "CreateTableOperation" && Texto(o, "Name") == nome);

    [Fact]
    public void Migration_SoCriaAsTabelasDosFluxos_ETrocaOCheckDoHistorico()
    {
        var operacoes = Operacoes();

        Assert.All(operacoes, o => Assert.Contains(Tipo(o), new[]
        {
            "CreateTableOperation", "CreateIndexOperation", "DropCheckConstraintOperation", "AddCheckConstraintOperation"
        }));
        Assert.Equal(Tabelas, operacoes.Where(o => Tipo(o) == "CreateTableOperation").Select(o => Texto(o, "Name")).OrderBy(n => n));
        Assert.All(operacoes, o => Assert.StartsWith("pe_", Texto(o, "Table") ?? Texto(o, "Name")));
        Assert.All(operacoes.Where(o => Tipo(o) == "CreateIndexOperation"), i => Assert.Matches("^(ux|ix)_pe_fluxo_", Texto(i, "Name")));

        var trocados = operacoes.Where(o => Tipo(o) is "DropCheckConstraintOperation" or "AddCheckConstraintOperation").ToList();
        Assert.Equal(2, trocados.Count);
        Assert.All(trocados, o => Assert.Equal(("ck_pe_modelo_historico_entidade", "pe_modelo_historico"), (Texto(o, "Name"), Texto(o, "Table"))));
        Assert.Equal("entidade IN ('nivel','etapa','passo','secao','campo','opcao','orgao_nivel','orgao_ajuste','doc_capitulo','doc_bloco','fluxo_modelo')",
            Texto(trocados.Single(o => Tipo(o) == "AddCheckConstraintOperation"), "Sql"));
    }

    [Fact]
    public void Down_DesfazSoAE6()
    {
        var operacoes = Operacoes("DownOperations");

        Assert.Equal(Tabelas, operacoes.Where(o => Tipo(o) == "DropTableOperation").Select(o => Texto(o, "Name")).OrderBy(n => n));
        var outras = operacoes.Where(o => Tipo(o) is not ("DropTableOperation" or "SqlOperation")).ToList();
        Assert.All(outras, o => Assert.Contains(Tipo(o), new[] { "DropCheckConstraintOperation", "AddCheckConstraintOperation" }));
        Assert.All(outras, o => Assert.Equal("pe_modelo_historico", Texto(o, "Table")));
        Assert.Equal("entidade IN ('nivel','etapa','passo','secao','campo','opcao','orgao_nivel','orgao_ajuste','doc_capitulo','doc_bloco')",
            Texto(outras.Single(o => Tipo(o) == "AddCheckConstraintOperation"), "Sql"));

        // SQL à mão, só em tabelas pe_: antes de tudo, a versão do conteúdo semeado volta a 3;
        // depois de apagar as tabelas e antes de voltar o CHECK antigo, sai do histórico o que
        // era só da E6 (as mudanças nos fluxos do modelo)
        var sqls = operacoes.Where(o => Tipo(o) == "SqlOperation").ToList();
        Assert.Equal(2, sqls.Count);
        Assert.Same(sqls[0], operacoes[0]);
        Assert.StartsWith("UPDATE pe_configuracao SET valor = '3' WHERE chave = 'seed_modelo_versao' ", Texto(sqls[0], "Sql"));
        Assert.Contains("::int > 3", Texto(sqls[0], "Sql"));
        Assert.Equal("DELETE FROM pe_modelo_historico WHERE entidade IN ('fluxo_modelo');", Texto(sqls[1], "Sql"));
        var ultimaTabela = operacoes.FindLastIndex(o => Tipo(o) == "DropTableOperation");
        var primeiroCheck = operacoes.FindIndex(o => Tipo(o) is "DropCheckConstraintOperation" or "AddCheckConstraintOperation");
        Assert.InRange(operacoes.IndexOf(sqls[1]), ultimaTabela + 1, primeiroCheck - 1);
    }

    [Fact]
    public void ForeignKeys_SoParaTabelasPe()
    {
        var fks = Operacoes().Where(o => Tipo(o) == "CreateTableOperation")
            .SelectMany(t => Lista(t, "ForeignKeys"))
            .ToDictionary(fk => Texto(fk, "Name")!, fk => (Principal: Texto(fk, "PrincipalTable")!, OnDelete: Valor(fk, "OnDelete")!.ToString()));

        Assert.Equal(new[] { "fk_pe_fluxo_modelo", "fk_pe_fluxo_pdtic" }, fks.Keys.OrderBy(n => n));
        Assert.Equal(("pe_pdtic", "Cascade"), fks["fk_pe_fluxo_pdtic"]);
        Assert.Equal(("pe_fluxo_modelo", "Restrict"), fks["fk_pe_fluxo_modelo"]);
    }

    [Fact]
    public void Checks_Colunas_EIndices()
    {
        var checks = Operacoes().Where(o => Tipo(o) == "CreateTableOperation").SelectMany(t => Lista(t, "CheckConstraints")).ToList();
        Assert.Equal("ck_pe_fluxo_modelo_chave", Texto(Assert.Single(checks), "Name"));
        Assert.Equal("chave ~ '^[a-z][a-z0-9_]*$'", Texto(checks[0], "Sql"));

        // A definição é jsonb nas duas tabelas; a cópia guarda o resumo (SHA-256) do modelo
        string TipoDaColuna(string tabela, string coluna) =>
            Texto(Lista(Tabela(tabela), "Columns").Single(c => Texto(c, "Name") == coluna), "ColumnType")!;
        Assert.Equal("jsonb", TipoDaColuna("pe_fluxo_modelo", "definicao"));
        Assert.Equal("jsonb", TipoDaColuna("pe_fluxo", "definicao"));
        Assert.Equal("character varying(64)", TipoDaColuna("pe_fluxo", "modelo_hash"));
        Assert.Equal("character varying(60)", TipoDaColuna("pe_fluxo_modelo", "chave"));

        var indices = Operacoes().Where(o => Tipo(o) == "CreateIndexOperation")
            .ToDictionary(o => Texto(o, "Name")!, o => (
                Colunas: string.Join(",", (string[])Valor(o, "Columns")!),
                Unico: (bool)Valor(o, "IsUnique")!,
                Filtro: Texto(o, "Filter")));
        Assert.Equal(3, indices.Count);
        Assert.Equal(("chave", true, (string?)null), indices["ux_pe_fluxo_modelo_chave"]);
        Assert.Equal(("pdtic_id,modelo_id", true, (string?)null), indices["ux_pe_fluxo_pdtic_modelo"]);
        Assert.Equal(("modelo_id", false, (string?)null), indices["ix_pe_fluxo_modelo"]);
    }
}
