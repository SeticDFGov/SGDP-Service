using System.Collections;
using System.Reflection;
using Controllers.Contratacoes;
using Microsoft.AspNetCore.Mvc;
using Models.Contratacoes;
using Xunit;

namespace test.contratacoes;

/// <summary>
/// Migration da lista revista e dos registros históricos (CtrListaSituacoesHistoricos) por
/// INSPEÇÃO das operações, lidas por reflexão (mesmo padrão e mesma limitação do
/// CtrMigrationSupervisaoTest: o InMemory não executa migration). O que se prova: só
/// ctr_processo, as quatro colunas nuláveis, os CHECKs da nota, do histórico e da origem
/// refeita, e o Down voltando o CHECK antigo da origem. Mais a rota nova do controller.
/// </summary>
public class CtrMigrationListaSituacoesTest
{
    private const string NomeMigration = "demanda_service.Migrations.CtrListaSituacoesHistoricos";

    private static List<object> Operacoes(string lado = "UpOperations")
    {
        var tipo = typeof(CtrProcesso).Assembly.GetType(NomeMigration);
        Assert.NotNull(tipo);

        var migration = Activator.CreateInstance(tipo!)!;
        tipo!.GetProperty("ActiveProvider")!.SetValue(migration, "Npgsql.EntityFrameworkCore.PostgreSQL");
        return ((IEnumerable)tipo.GetProperty(lado)!.GetValue(migration)!).Cast<object>().ToList();
    }

    private static object? Valor(object operacao, string propriedade) =>
        operacao.GetType().GetProperty(propriedade, BindingFlags.Public | BindingFlags.Instance)?.GetValue(operacao);

    private static string? Texto(object operacao, string propriedade) => Valor(operacao, propriedade) as string;

    private static string Tipo(object operacao) => operacao.GetType().Name;

    [Theory]
    [InlineData("UpOperations")]
    [InlineData("DownOperations")]
    public void Migration_SoTocaEmCtrProcesso_SemSqlCru(string lado)
    {
        var operacoes = Operacoes(lado);
        Assert.NotEmpty(operacoes);

        Assert.All(operacoes, o =>
        {
            Assert.NotEqual("SqlOperation", Tipo(o));
            Assert.Equal("ctr_processo", Texto(o, "Table"));
        });
    }

    [Fact]
    public void Migration_AdicionaAsQuatroColunasNulaveis()
    {
        var colunas = Operacoes().Where(o => Tipo(o) == "AddColumnOperation").ToList();

        Assert.Equal(
            new[] { "autorizacao_documento_sei", "nota_informativa_documento_sei", "nota_informativa_enviada_em", "numero_contrato" },
            colunas.Select(c => Texto(c, "Name")).OrderBy(n => n, StringComparer.Ordinal));
        Assert.All(colunas, c => Assert.Equal(true, Valor(c, "IsNullable")));
        Assert.Equal("date", Texto(colunas.Single(c => Texto(c, "Name") == "nota_informativa_enviada_em"), "ColumnType"));
        Assert.Equal(60, Valor(colunas.Single(c => Texto(c, "Name") == "numero_contrato"), "MaxLength"));
        Assert.Equal(25, Valor(colunas.Single(c => Texto(c, "Name") == "nota_informativa_documento_sei"), "MaxLength"));
        Assert.Equal(25, Valor(colunas.Single(c => Texto(c, "Name") == "autorizacao_documento_sei"), "MaxLength"));
        Assert.DoesNotContain(Operacoes(), o => Tipo(o) == "DropColumnOperation");
    }

    [Fact]
    public void Migration_ChecksDaNotaDoHistoricoEDaOrigemRefeita()
    {
        var operacoes = Operacoes();

        var checks = operacoes.Where(o => Tipo(o) == "AddCheckConstraintOperation")
            .ToDictionary(o => Texto(o, "Name")!, o => Texto(o, "Sql")!);
        Assert.Equal(3, checks.Count);
        Assert.Equal("nota_informativa_documento_sei IS NULL OR nota_informativa_enviada_em IS NOT NULL",
            checks["ck_ctr_processo_nota_informativa"]);
        Assert.Equal(
            "origem <> 'Anterior ao regime' OR (data_assinatura_contrato IS NOT NULL AND numero_contrato IS NOT NULL)",
            checks["ck_ctr_processo_anterior_regime"]);
        Assert.Equal("origem IN ('Órgão comunicante','TCDF','Anterior ao regime')", checks["ck_ctr_processo_origem"]);

        // O CHECK da origem é refeito: sai o antigo antes de entrar o novo
        var drop = Assert.Single(operacoes, o => Tipo(o) == "DropCheckConstraintOperation");
        Assert.Equal("ck_ctr_processo_origem", Texto(drop, "Name"));

        var volta = Assert.Single(Operacoes("DownOperations"), o => Tipo(o) == "AddCheckConstraintOperation");
        Assert.Equal("origem IN ('Órgão comunicante','TCDF')", Texto(volta, "Sql"));
    }

    [Fact]
    public void Rota_MarcarEDesfazer_EhPutNoProcesso()
    {
        var acao = typeof(CtrProcessoController).GetMethod(nameof(CtrProcessoController.MarcarAnteriorAoRegime))!;

        var put = Assert.Single(acao.GetCustomAttributes<HttpPutAttribute>());
        Assert.Equal("{id:long}/anterior-ao-regime", put.Template);
        Assert.Equal("api/contratacoes/processo",
            typeof(CtrProcessoController).GetCustomAttribute<RouteAttribute>()!.Template);
        Assert.NotNull(acao.GetParameters().Single(p => p.ParameterType == typeof(api.Contratacoes.CtrAnteriorAoRegimeDTO))
            .GetCustomAttribute<FromBodyAttribute>());
    }
}
