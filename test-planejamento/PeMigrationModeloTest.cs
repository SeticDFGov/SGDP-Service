using System.Collections;
using System.Reflection;
using Models.Planejamento;
using Xunit;

namespace test.planejamento;

/// <summary>
/// Migration PeModeloConfiguravel (E2) por INSPEÇÃO das operações, lidas por reflexão
/// (o provider InMemory não executa migration), no padrão do PeMigrationTest da E1. O que
/// se prova: só cria as 13 tabelas pe_ do modelo e os índices delas; nenhuma tabela
/// existente é tocada (as FKs para pgia_orgao ficam nas tabelas novas, com RESTRICT); o
/// Down só apaga tabelas pe_.
/// </summary>
public class PeMigrationModeloTest
{
    private const string NomeMigration = "demanda_service.Migrations.PeModeloConfiguravel";

    private static readonly string[] Tabelas =
    {
        "pe_campo", "pe_campo_nivel", "pe_configuracao", "pe_etapa", "pe_modelo_historico", "pe_nivel", "pe_opcao",
        "pe_orgao_ajuste", "pe_orgao_config", "pe_passo", "pe_passo_nivel", "pe_secao", "pe_secao_nivel"
    };

    private static List<object> Operacoes(string lado = "UpOperations")
    {
        var tipo = typeof(PeNivel).Assembly.GetType(NomeMigration);
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
    public void Migration_SoCriaAsTabelasPeDoModelo_EOsIndicesDelas()
    {
        var operacoes = Operacoes();

        Assert.All(operacoes, o => Assert.Contains(Tipo(o), new[] { "CreateTableOperation", "CreateIndexOperation" }));
        Assert.Equal(Tabelas, operacoes.Where(o => Tipo(o) == "CreateTableOperation").Select(o => Texto(o, "Name")).OrderBy(n => n));
        Assert.All(operacoes.Where(o => Tipo(o) == "CreateIndexOperation"), i =>
        {
            Assert.StartsWith("pe_", Texto(i, "Table"));
            Assert.Matches("^(ux|ix)_pe_", Texto(i, "Name"));
        });
    }

    [Fact]
    public void Down_SoApagaTabelasPe()
    {
        var operacoes = Operacoes("DownOperations");

        Assert.All(operacoes, o => Assert.Equal("DropTableOperation", Tipo(o)));
        Assert.Equal(Tabelas, operacoes.Select(o => Texto(o, "Name")).OrderBy(n => n));
    }

    [Fact]
    public void ForeignKeys_ComPrefixo_EParaOPgiaSoComRestrict()
    {
        var fks = Operacoes().Where(o => Tipo(o) == "CreateTableOperation")
            .SelectMany(t => Lista(t, "ForeignKeys"))
            .ToList();

        Assert.All(fks, fk => Assert.StartsWith("fk_pe_", Texto(fk, "Name")));
        // Fora do módulo, a única tabela referenciada é pgia_orgao, e sem cascata
        var externas = fks.Where(fk => !Texto(fk, "PrincipalTable")!.StartsWith("pe_")).ToList();
        Assert.Equal(new[] { "fk_pe_orgao_ajuste_orgao", "fk_pe_orgao_config_orgao" }, externas.Select(fk => Texto(fk, "Name")).OrderBy(n => n));
        Assert.All(externas, fk => Assert.Equal("pgia_orgao", Texto(fk, "PrincipalTable")));
        Assert.All(externas, fk => Assert.Equal("Restrict", Valor(fk, "OnDelete")!.ToString()));
    }

    [Fact]
    public void Checks_DosDominios()
    {
        string Check(string tabela, string nome) =>
            Texto(Lista(Tabela(tabela), "CheckConstraints").Single(c => Texto(c, "Name") == nome), "Sql")!;

        Assert.Equal("tipo IN ('dados','documento','fluxo','aprovacao','envio','deliberacao','publicacao','conferencia_temas','monitoramento')",
            Check("pe_passo", "ck_pe_passo_tipo"));
        Assert.Equal("situacao IN ('obrigatorio','opcional','desligado')", Check("pe_passo_nivel", "ck_pe_passo_nivel_situacao"));
        Assert.Equal("escopo IN ('pdtic','petic','df')", Check("pe_secao", "ck_pe_secao_escopo"));
        Assert.Equal("tipo IN ('formulario','tabela')", Check("pe_secao", "ck_pe_secao_tipo"));
        Assert.Equal("(escopo = 'pdtic') = (situacao_geral IS NULL)", Check("pe_secao", "ck_pe_secao_escopo_situacao"));
        Assert.Equal("(escopo = 'pdtic') = (passo_id IS NOT NULL)", Check("pe_secao", "ck_pe_secao_escopo_passo"));
        Assert.Contains("'ligacao_catalogo'", Check("pe_campo", "ck_pe_campo_tipo"));
        Assert.Contains("'calculado'", Check("pe_campo", "ck_pe_campo_tipo"));
        Assert.Equal("alvo_tipo IN ('passo','secao','campo')", Check("pe_orgao_ajuste", "ck_pe_orgao_ajuste_alvo"));
        Assert.Equal("largura IS NULL OR largura IN ('estreita','media','larga')", Check("pe_campo", "ck_pe_campo_largura"));
        Assert.Equal("cor IS NULL OR cor IN ('verde','amarelo','laranja','vermelho','azul','roxo','cinza')", Check("pe_opcao", "ck_pe_opcao_cor"));
        Assert.All(Operacoes().Where(o => Tipo(o) == "CreateTableOperation").SelectMany(t => Lista(t, "CheckConstraints")),
            c => Assert.StartsWith("ck_pe_", Texto(c, "Name")));
    }

    [Fact]
    public void Indices_UnicosDasChaves()
    {
        var unicos = Operacoes().Where(o => Tipo(o) == "CreateIndexOperation" && (bool)Valor(o, "IsUnique")!)
            .ToDictionary(o => Texto(o, "Name")!, o => string.Join(",", (string[])Valor(o, "Columns")!));

        Assert.Equal("codigo", unicos["ux_pe_nivel_codigo"]);
        Assert.Equal("chave", unicos["ux_pe_etapa_chave"]);
        Assert.Equal("chave", unicos["ux_pe_passo_chave"]);
        Assert.Equal("chave", unicos["ux_pe_secao_chave"]);
        Assert.Equal("secao_id,chave", unicos["ux_pe_campo_secao_chave"]);
        Assert.Equal("campo_id,valor", unicos["ux_pe_opcao_campo_valor"]);
        Assert.Equal("orgao_id,alvo_tipo,alvo_id", unicos["ux_pe_orgao_ajuste_alvo"]);
    }
}
