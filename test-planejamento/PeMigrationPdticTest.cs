using System.Collections;
using System.Reflection;
using Models.Planejamento;
using Xunit;

namespace test.planejamento;

/// <summary>
/// Migration PePdticTrilha (E4) por INSPEÇÃO das operações, lidas por reflexão (o provider
/// InMemory não executa migration), no padrão das migrations anteriores do módulo. O que se
/// prova: só cria as 3 tabelas pe_ da E4 (pe_pdtic, pe_pdtic_passo, pe_comentario) e os
/// índices delas; a única tabela existente tocada é pe_registro (da E3), que ganha a coluna
/// nulável pdtic_id, o CHECK de dono, o índice único do código no PDTIC e a FK para
/// pe_pdtic; nenhuma tabela fora do módulo muda (pgia_orgao só é apontada por FK); o Down
/// desfaz só isso.
/// </summary>
public class PeMigrationPdticTest
{
    private const string NomeMigration = "demanda_service.Migrations.PePdticTrilha";

    private static readonly string[] Tabelas = { "pe_comentario", "pe_pdtic", "pe_pdtic_passo" };

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
    public void Migration_SoCriaAsTabelasDaE4_ETocaSoPeRegistro()
    {
        var operacoes = Operacoes();

        Assert.All(operacoes, o => Assert.Contains(Tipo(o), new[]
        {
            "CreateTableOperation", "CreateIndexOperation", "AddColumnOperation", "AddCheckConstraintOperation", "AddForeignKeyOperation"
        }));
        Assert.Equal(Tabelas, operacoes.Where(o => Tipo(o) == "CreateTableOperation").Select(o => Texto(o, "Name")).OrderBy(n => n));
        Assert.All(operacoes, o => Assert.StartsWith("pe_", Texto(o, "Table") ?? Texto(o, "Name")));

        // Na tabela da E3: a coluna nulável, o CHECK de dono, o índice e a FK, e nada mais
        var naE3 = operacoes.Where(o => Tipo(o) != "CreateTableOperation" && Texto(o, "Table") == "pe_registro").ToList();
        Assert.Equal(new[] { "AddCheckConstraintOperation", "AddColumnOperation", "AddForeignKeyOperation", "CreateIndexOperation" },
            naE3.Select(Tipo).OrderBy(t => t));
        var coluna = naE3.Single(o => Tipo(o) == "AddColumnOperation");
        Assert.Equal("pdtic_id", Texto(coluna, "Name"));
        Assert.True((bool)Valor(coluna, "IsNullable")!);
        Assert.Null(Valor(coluna, "DefaultValue"));
        Assert.Null(Valor(coluna, "DefaultValueSql"));
        var check = naE3.Single(o => Tipo(o) == "AddCheckConstraintOperation");
        Assert.Equal("ck_pe_registro_dono", Texto(check, "Name"));
        Assert.Equal("petic_id IS NULL OR pdtic_id IS NULL", Texto(check, "Sql"));
        var fk = naE3.Single(o => Tipo(o) == "AddForeignKeyOperation");
        Assert.Equal(("fk_pe_registro_pdtic", "pe_pdtic", "Cascade"), (Texto(fk, "Name"), Texto(fk, "PrincipalTable"), Valor(fk, "OnDelete")!.ToString()));

        Assert.All(operacoes.Where(o => Tipo(o) == "CreateIndexOperation"), i => Assert.Matches("^(ux|ix)_pe_", Texto(i, "Name")));
    }

    [Fact]
    public void Down_DesfazSoAE4()
    {
        var operacoes = Operacoes("DownOperations");

        Assert.Equal(Tabelas, operacoes.Where(o => Tipo(o) == "DropTableOperation").Select(o => Texto(o, "Name")).OrderBy(n => n));
        var naE3 = operacoes.Where(o => Tipo(o) != "DropTableOperation").ToList();
        Assert.All(naE3, o => Assert.Equal("pe_registro", Texto(o, "Table")));
        Assert.Equal(new[] { "DropCheckConstraintOperation", "DropColumnOperation", "DropForeignKeyOperation", "DropIndexOperation" },
            naE3.Select(Tipo).OrderBy(t => t));
    }

    [Fact]
    public void ForeignKeys_SoParaTabelasPe_EOOrgaoDoPgiaSemCascata()
    {
        var fks = Operacoes().Where(o => Tipo(o) == "CreateTableOperation")
            .SelectMany(t => Lista(t, "ForeignKeys"))
            .ToDictionary(fk => Texto(fk, "Name")!, fk => (Principal: Texto(fk, "PrincipalTable")!, OnDelete: Valor(fk, "OnDelete")!.ToString()));

        Assert.All(fks.Keys, n => Assert.StartsWith("fk_pe_", n));
        Assert.Equal(("pgia_orgao", "Restrict"), fks["fk_pe_pdtic_orgao"]);
        Assert.All(fks.Where(f => f.Key != "fk_pe_pdtic_orgao").Select(f => f.Value.Principal), p => Assert.StartsWith("pe_", p));
        Assert.Equal(("pe_pdtic", "Restrict"), fks["fk_pe_pdtic_anterior"]);
        Assert.Equal(("pe_pdtic", "Cascade"), fks["fk_pe_pdtic_passo_pdtic"]);
        Assert.Equal(("pe_passo", "Restrict"), fks["fk_pe_pdtic_passo_passo"]);
        Assert.Equal(("pe_pdtic", "Cascade"), fks["fk_pe_comentario_pdtic"]);
        Assert.Equal(("pe_passo", "Restrict"), fks["fk_pe_comentario_passo"]);
        Assert.Equal(("pe_comentario", "Cascade"), fks["fk_pe_comentario_pai"]);
    }

    [Fact]
    public void Checks_EIndices()
    {
        string Check(string tabela, string nome) =>
            Texto(Lista(Tabela(tabela), "CheckConstraints").Single(c => Texto(c, "Name") == nome), "Sql")!;

        Assert.Equal("situacao IN ('em_elaboracao','em_aprovacao','devolvido','aprovado','publicado','em_acompanhamento','encerrado','substituido')",
            Check("pe_pdtic", "ck_pe_pdtic_situacao"));
        Assert.Equal("vigencia_inicio IS NULL OR vigencia_fim IS NULL OR vigencia_fim >= vigencia_inicio", Check("pe_pdtic", "ck_pe_pdtic_vigencia"));
        Assert.Equal("nao_se_aplica = (justificativa IS NOT NULL)", Check("pe_pdtic_passo", "ck_pe_pdtic_passo_justificativa"));
        Assert.Equal("(resolvido_em IS NULL) = (resolvido_por IS NULL)", Check("pe_comentario", "ck_pe_comentario_resolvido"));
        Assert.Equal("pai_id IS NULL OR resolvido_em IS NULL", Check("pe_comentario", "ck_pe_comentario_resposta"));
        Assert.All(Operacoes().Where(o => Tipo(o) == "CreateTableOperation").SelectMany(t => Lista(t, "CheckConstraints")),
            c => Assert.StartsWith("ck_pe_", Texto(c, "Name")));

        var indices = Operacoes().Where(o => Tipo(o) == "CreateIndexOperation")
            .ToDictionary(o => Texto(o, "Name")!, o => (
                Colunas: string.Join(",", (string[])Valor(o, "Columns")!),
                Unico: (bool)Valor(o, "IsUnique")!,
                Filtro: Texto(o, "Filter")));
        Assert.Equal(("orgao_id", true, "situacao NOT IN ('encerrado','substituido')"), indices["ux_pe_pdtic_atual"]);
        Assert.Equal(("orgao_id,versao", true, (string?)null), indices["ux_pe_pdtic_orgao_versao"]);
        Assert.Equal(("pdtic_id,secao_id,codigo", true, (string?)null), indices["ux_pe_registro_pdtic_codigo"]);
        Assert.Equal(("pdtic_id,passo_id", false, (string?)null), indices["ix_pe_comentario_pdtic"]);
    }

    /// <summary>
    /// Intervalo do deploy da E4: a coluna nova de pe_registro ainda não existe (42703) e toda
    /// leitura de registro a seleciona; a tabela nova também não (42P01). Os dois viram 409 com
    /// corpo (PeRespostas), e o carregador tenta de novo; outro erro do banco não.
    /// </summary>
    [Theory]
    [InlineData("42703", true)]
    [InlineData("42P01", true)]
    [InlineData("23505", false)]
    public void IntervaloDoDeploy_ColunaOuTabelaAusente(string codigo, bool ausente)
    {
        var erro = new Npgsql.PostgresException("não existe", "ERROR", "ERROR", codigo);
        Assert.Equal(ausente, service.Planejamento.PeBanco.TabelaAusente(erro));
        Assert.Equal(ausente, service.Planejamento.PeBanco.TabelaAusente(
            new Microsoft.EntityFrameworkCore.DbUpdateException("falhou", erro)));
    }
}
