using System.Collections;
using System.Reflection;
using Models.Planejamento;
using Xunit;

namespace test.planejamento;

/// <summary>
/// Migration PePaineis (E8) por INSPEÇÃO das operações, lidas por reflexão (o provider InMemory
/// não executa migration), no padrão das migrations anteriores do módulo. O que se prova: só cria
/// pe_inadimplencia (com a PK, os seis CHECKs, a FK RESTRICT para pgia_orgao e o índice por órgão
/// e situação); nenhuma tabela existente é tocada (pgia_orgao só é apontada pela FK); o Down só
/// apaga a tabela nova.
/// </summary>
public class PeMigrationPaineisTest
{
    private const string NomeMigration = "demanda_service.Migrations.PePaineis";

    private static List<object> Operacoes(string lado = "UpOperations")
    {
        var tipo = typeof(PeInadimplencia).Assembly.GetType(NomeMigration);
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

    [Fact]
    public void Up_SoCriaPeInadimplencia_ComOsChecksAFkEOIndice()
    {
        var operacoes = Operacoes();

        Assert.Equal(new[] { "CreateTableOperation", "CreateIndexOperation" }, operacoes.Select(Tipo));
        var tabela = operacoes[0];
        Assert.Equal("pe_inadimplencia", Texto(tabela, "Name"));

        var colunas = Lista(tabela, "Columns").ToDictionary(c => Texto(c, "Name")!, c => (Tipo: Texto(c, "ColumnType"), Nula: (bool)Valor(c, "IsNullable")!));
        Assert.Equal(new[]
        {
            "alterado_em", "alterado_por", "comunicado_controle_em", "criado_em", "criado_por", "documento", "id", "justificativa", "motivo",
            "nota_motivacao", "notificado_em", "obrigacao", "observacao", "orgao_id", "prazo", "prazo_descumprido", "registrado_em",
            "registrado_por", "saneado_em", "sei", "situacao"
        }, colunas.Keys.OrderBy(k => k, StringComparer.Ordinal));
        Assert.Equal(("date", false), colunas["notificado_em"]);
        Assert.Equal(("date", false), colunas["prazo"]);
        Assert.Equal(("date", true), colunas["saneado_em"]);
        Assert.Equal(("character varying(20)", false), colunas["situacao"]);
        Assert.Equal(("character varying(30)", true), colunas["motivo"]);

        var checks = Lista(tabela, "CheckConstraints").ToDictionary(c => Texto(c, "Name")!, c => Texto(c, "Sql"));
        Assert.Equal(new[]
        {
            "ck_pe_inadimplencia_justificado", "ck_pe_inadimplencia_motivo", "ck_pe_inadimplencia_prazo", "ck_pe_inadimplencia_registro",
            "ck_pe_inadimplencia_saneado", "ck_pe_inadimplencia_situacao"
        }, checks.Keys.OrderBy(k => k, StringComparer.Ordinal));
        Assert.Equal("situacao IN ('notificado','justificado','inadimplente','saneado')", checks["ck_pe_inadimplencia_situacao"]);
        Assert.Equal("motivo IS NULL OR motivo IN ('descumprimento_prazo','omissao_reiterada','recusa_injustificada')", checks["ck_pe_inadimplencia_motivo"]);
        Assert.Equal("prazo > notificado_em", checks["ck_pe_inadimplencia_prazo"]);
        Assert.StartsWith("(situacao = 'saneado') = (saneado_em IS NOT NULL)", checks["ck_pe_inadimplencia_saneado"]);
        Assert.Contains("(situacao <> 'inadimplente' OR registrado_em IS NOT NULL)", checks["ck_pe_inadimplencia_registro"]);

        var fk = Assert.Single(Lista(tabela, "ForeignKeys"));
        Assert.Equal(("fk_pe_inadimplencia_orgao", "pgia_orgao", "Restrict"),
            (Texto(fk, "Name"), Texto(fk, "PrincipalTable"), Valor(fk, "OnDelete")!.ToString()));

        var indice = operacoes[1];
        Assert.Equal(("ix_pe_inadimplencia_orgao", "pe_inadimplencia", "orgao_id,situacao", false),
            (Texto(indice, "Name"), Texto(indice, "Table"), string.Join(",", (string[])Valor(indice, "Columns")!), (bool)Valor(indice, "IsUnique")!));
    }

    [Fact]
    public void Down_SoApagaATabelaNova()
    {
        var operacao = Assert.Single(Operacoes("DownOperations"));
        Assert.Equal(("DropTableOperation", "pe_inadimplencia"), (Tipo(operacao), Texto(operacao, "Name")));
    }
}
