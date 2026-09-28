using System.Collections;
using System.Reflection;
using Models.Planejamento;
using Xunit;

namespace test.planejamento;

/// <summary>
/// Migration PeReferenciaisRegistros (E3) por INSPEÇÃO das operações, lidas por reflexão
/// (o provider InMemory não executa migration), no padrão das migrations da E1 e da E2. O que
/// se prova: só cria as 6 tabelas pe_ da E3 e os índices delas; as FKs só apontam para
/// tabelas pe_ (nenhuma tabela existente é tocada); o Down só apaga essas tabelas.
/// </summary>
public class PeMigrationReferenciaisTest
{
    private const string NomeMigration = "demanda_service.Migrations.PeReferenciaisRegistros";

    private static readonly string[] Tabelas =
        { "pe_arquivo", "pe_deliberacao", "pe_petic", "pe_registro", "pe_registro_sequencia", "pe_vinculo" };

    private static List<object> Operacoes(string lado = "UpOperations")
    {
        var tipo = typeof(PeRegistro).Assembly.GetType(NomeMigration);
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
    public void Migration_SoCriaAsTabelasPeDaE3_EOsIndicesDelas()
    {
        var operacoes = Operacoes();

        Assert.All(operacoes, o => Assert.Contains(Tipo(o), new[] { "CreateTableOperation", "CreateIndexOperation" }));
        Assert.Equal(Tabelas, operacoes.Where(o => Tipo(o) == "CreateTableOperation").Select(o => Texto(o, "Name")).OrderBy(n => n));
        Assert.All(operacoes.Where(o => Tipo(o) == "CreateIndexOperation"), i =>
        {
            Assert.Contains(Texto(i, "Table"), Tabelas);
            Assert.Matches("^(ux|ix)_pe_", Texto(i, "Name"));
        });
    }

    [Fact]
    public void Down_SoApagaAsTabelasDaE3()
    {
        var operacoes = Operacoes("DownOperations");

        Assert.All(operacoes, o => Assert.Equal("DropTableOperation", Tipo(o)));
        Assert.Equal(Tabelas, operacoes.Select(o => Texto(o, "Name")).OrderBy(n => n));
    }

    [Fact]
    public void ForeignKeys_SoEntreTabelasPe_ComCascataSoNaOrigemENoRascunho()
    {
        var fks = Operacoes().Where(o => Tipo(o) == "CreateTableOperation")
            .SelectMany(t => Lista(t, "ForeignKeys"))
            .ToDictionary(fk => Texto(fk, "Name")!, fk => (Principal: Texto(fk, "PrincipalTable")!, OnDelete: Valor(fk, "OnDelete")!.ToString()));

        Assert.All(fks.Keys, n => Assert.StartsWith("fk_pe_", n));
        Assert.All(fks.Values, fk => Assert.StartsWith("pe_", fk.Principal));
        Assert.Equal(("pe_registro", "Cascade"), fks["fk_pe_vinculo_origem"]);
        // O destino ligado não sai: NO ACTION (confere no fim do comando)
        Assert.Equal(("pe_registro", "NoAction"), fks["fk_pe_vinculo_destino"]);
        Assert.Equal(("pe_petic", "Cascade"), fks["fk_pe_registro_petic"]);
        Assert.Equal(("pe_secao", "Restrict"), fks["fk_pe_registro_secao"]);
        Assert.Equal(("pe_campo", "Restrict"), fks["fk_pe_vinculo_campo"]);
        Assert.Equal(("pe_petic", "Restrict"), fks["fk_pe_petic_anterior"]);
        Assert.Equal(("pe_secao", "Restrict"), fks["fk_pe_registro_sequencia_secao"]);
    }

    [Fact]
    public void Checks_DosDominiosEDasRegras()
    {
        string Check(string tabela, string nome) =>
            Texto(Lista(Tabela(tabela), "CheckConstraints").Single(c => Texto(c, "Name") == nome), "Sql")!;

        Assert.Equal("situacao IN ('rascunho','em_deliberacao','aprovado','substituido')", Check("pe_petic", "ck_pe_petic_situacao"));
        Assert.Equal("(situacao IN ('aprovado','substituido')) = (aprovado_em IS NOT NULL)", Check("pe_petic", "ck_pe_petic_aprovado_em"));
        Assert.Equal("objeto_tipo IN ('petic','pdtic')", Check("pe_deliberacao", "ck_pe_deliberacao_objeto"));
        Assert.Equal("situacao IN ('aguardando','aprovado','devolvido')", Check("pe_deliberacao", "ck_pe_deliberacao_situacao"));
        Assert.Contains("ato_numero IS NOT NULL AND ato_data IS NOT NULL", Check("pe_deliberacao", "ck_pe_deliberacao_aprovado"));
        Assert.Contains("observacao IS NOT NULL", Check("pe_deliberacao", "ck_pe_deliberacao_devolvido"));
        Assert.Equal("dono_tipo IS NULL OR dono_tipo IN ('registro')", Check("pe_arquivo", "ck_pe_arquivo_dono_tipo"));
        Assert.Equal("conteudo IS NOT NULL AND octet_length(conteudo) = tamanho", Check("pe_arquivo", "ck_pe_arquivo_conteudo"));
        Assert.Equal("registro_origem_id <> registro_destino_id", Check("pe_vinculo", "ck_pe_vinculo_origem_destino"));
        Assert.All(Operacoes().Where(o => Tipo(o) == "CreateTableOperation").SelectMany(t => Lista(t, "CheckConstraints")),
            c => Assert.StartsWith("ck_pe_", Texto(c, "Name")));
    }

    [Fact]
    public void Indices_UnicosEFiltrados()
    {
        var indices = Operacoes().Where(o => Tipo(o) == "CreateIndexOperation")
            .ToDictionary(o => Texto(o, "Name")!, o => (
                Colunas: string.Join(",", (string[])Valor(o, "Columns")!),
                Unico: (bool)Valor(o, "IsUnique")!,
                Filtro: Texto(o, "Filter")));

        Assert.Equal(("situacao", true, "situacao IN ('rascunho','em_deliberacao','aprovado')"), indices["ux_pe_petic_situacao"]);
        Assert.Equal(("versao", true, (string?)null), indices["ux_pe_petic_versao"]);
        Assert.Equal(("objeto_tipo,objeto_id", true, "situacao = 'aguardando'"), indices["ux_pe_deliberacao_aguardando"]);
        Assert.Equal(("objeto_tipo,objeto_id", false, (string?)null), indices["ix_pe_deliberacao_objeto"]);
        Assert.Equal(("petic_id,secao_id,codigo", true, (string?)null), indices["ux_pe_registro_petic_codigo"]);
        Assert.Equal(("registro_origem_id,campo_id,registro_destino_id", true, (string?)null), indices["ux_pe_vinculo"]);
        Assert.Equal(("registro_destino_id", false, (string?)null), indices["ix_pe_vinculo_destino"]);
    }
}
