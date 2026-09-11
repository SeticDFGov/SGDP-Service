using System.Collections;
using System.Reflection;
using System.Text.RegularExpressions;
using Models.Contratacoes;
using Xunit;

namespace test.contratacoes;

/// <summary>
/// Migration da rodada da chefia (CtrRodadaPlanejamentoTcdf) por INSPEÇÃO das
/// operações que ela produz.
///
/// LIMITAÇÃO REGISTRADA: o backfill da criticidade é um <c>migrationBuilder.Sql</c>
/// em PostgreSQL (DISTINCT ON) e o provider InMemory dos testes não executa
/// migration nenhuma — não há como exercitá-lo de verdade aqui. A cobertura é a
/// que o desenho admite: conferir a ORDEM das operações (o backfill vem depois das
/// colunas novas e antes do DROP) e a lógica do SQL gerado.
///
/// A leitura é toda por REFLEXÃO de propósito: o projeto de teste não referencia
/// Microsoft.EntityFrameworkCore.Relational (versão diferente da que o app resolve),
/// e acrescentar o pacote seria dependência nova — o que as regras proíbem.
/// </summary>
public class CtrMigrationRodadaChefiaTest
{
    /// <summary>Uma operação da migration, lida por reflexão (sem tipos do EF Relational).</summary>
    private sealed record Operacao(string Tipo, string? Tabela, string? Nome, string? Sql);

    private static List<Operacao> Operacoes()
    {
        var tipo = typeof(CtrProcesso).Assembly
            .GetType("demanda_service.Migrations.CtrRodadaPlanejamentoTcdf");
        Assert.NotNull(tipo);

        var migration = Activator.CreateInstance(tipo!)!;
        tipo!.GetProperty("ActiveProvider")!
            .SetValue(migration, "Npgsql.EntityFrameworkCore.PostgreSQL");

        var operacoes = (IEnumerable)tipo.GetProperty("UpOperations")!.GetValue(migration)!;

        return operacoes.Cast<object>()
            .Select(o => new Operacao(
                o.GetType().Name,
                Texto(o, "Table"),
                Texto(o, "Name"),
                Texto(o, "Sql")))
            .ToList();
    }

    private static string? Texto(object operacao, string propriedade) =>
        operacao.GetType().GetProperty(propriedade, BindingFlags.Public | BindingFlags.Instance)?
            .GetValue(operacao) as string;

    private static object? Valor(object operacao, string propriedade) =>
        operacao.GetType().GetProperty(propriedade, BindingFlags.Public | BindingFlags.Instance)?
            .GetValue(operacao);

    [Fact]
    public void Migration_SoTocaEmObjetosDoModulo()
    {
        // Nenhuma tabela fora de ctr_* pode entrar no script (regra inviolável)
        foreach (var operacao in Operacoes())
        {
            if (operacao.Tipo == "SqlOperation")
            {
                // O único SQL cru é o backfill, conferido no teste próprio
                Assert.DoesNotContain("pgia_", operacao.Sql);
                Assert.DoesNotContain("\"Users\"", operacao.Sql);
                continue;
            }

            Assert.NotNull(operacao.Tabela);
            Assert.StartsWith("ctr_", operacao.Tabela);
        }
    }

    [Fact]
    public void Migration_CriaAsCincoColunasDoProcessoEOStatusDaManifestacao()
    {
        var adicionadas = Operacoes()
            .Where(o => o.Tipo == "AddColumnOperation")
            .Select(o => $"{o.Tabela}.{o.Nome}")
            .ToList();

        Assert.Equal(new[]
        {
            "ctr_processo.retorno_orgao_nao_se_aplica",
            "ctr_processo.etapa_planejamento",
            "ctr_processo.data_assinatura_contrato",
            "ctr_processo.criticidade",
            "ctr_processo.origem",
            "ctr_manifestacao_tcdf.status_tcdf"
        }, adicionadas);
    }

    [Fact]
    public void Migration_OrigemNasceNotNullComODefaultDoOrgaoComunicante()
    {
        var tipo = typeof(CtrProcesso).Assembly
            .GetType("demanda_service.Migrations.CtrRodadaPlanejamentoTcdf")!;
        var migration = Activator.CreateInstance(tipo)!;
        tipo.GetProperty("ActiveProvider")!.SetValue(migration, "Npgsql.EntityFrameworkCore.PostgreSQL");

        var origem = ((IEnumerable)tipo.GetProperty("UpOperations")!.GetValue(migration)!)
            .Cast<object>()
            .Single(o => o.GetType().Name == "AddColumnOperation" && Texto(o, "Name") == "origem");

        Assert.Equal(false, Valor(origem, "IsNullable"));
        Assert.Equal(CtrDominios.Origem.OrgaoComunicante, Valor(origem, "DefaultValue"));
    }

    [Fact]
    public void Migration_BackfillDaCriticidadeVemDepoisDasColunasEAntesDoDrop()
    {
        var operacoes = Operacoes();

        var indiceBackfill = operacoes.FindIndex(o => o.Tipo == "SqlOperation");
        Assert.Equal(1, operacoes.Count(o => o.Tipo == "SqlOperation"));

        var indiceColunaDestino = operacoes.FindIndex(o =>
            o.Tipo == "AddColumnOperation" && o.Tabela == "ctr_processo" && o.Nome == "criticidade");
        var indiceDrop = operacoes.FindIndex(o =>
            o.Tipo == "DropColumnOperation" && o.Tabela == "ctr_manifestacao_tcdf" && o.Nome == "criticidade");

        Assert.True(indiceColunaDestino >= 0 && indiceColunaDestino < indiceBackfill,
            "a coluna de destino tem de existir antes do backfill");
        Assert.True(indiceBackfill < indiceDrop,
            "o backfill tem de rodar ANTES de a coluna de origem ser removida");

        // O DROP é a última operação do script (nada mais depende da coluna)
        Assert.Equal(operacoes.Count - 1, indiceDrop);
    }

    [Fact]
    public void Migration_BackfillEscolheAManifestacaoMaisRecenteComCriticidade()
    {
        var sql = Operacoes().Single(o => o.Tipo == "SqlOperation").Sql!;
        var compacto = Regex.Replace(sql, @"\s+", " ").Trim();

        Assert.Contains("UPDATE ctr_processo p", compacto);
        Assert.Contains("SET criticidade = recente.criticidade", compacto);
        // Uma linha por processo: a mais recente entre as que TÊM criticidade
        Assert.Contains("SELECT DISTINCT ON (m.processo_id)", compacto);
        Assert.Contains("FROM ctr_manifestacao_tcdf m", compacto);
        Assert.Contains("WHERE m.criticidade IS NOT NULL", compacto);
        Assert.Contains("ORDER BY m.processo_id, m.data_oficio DESC, m.id DESC", compacto);
        Assert.Contains("WHERE recente.processo_id = p.id", compacto);
    }

    [Fact]
    public void Migration_RecriaOCheckDosIncisosSemACriticidadeERemoveODaCriticidade()
    {
        var operacoes = Operacoes();

        var incisoNovo = operacoes.Single(o =>
            o.Tipo == "AddCheckConstraintOperation" && o.Nome == "ck_ctr_manifestacao_inciso");
        Assert.DoesNotContain("criticidade", incisoNovo.Sql);

        // O CHECK antigo é derrubado antes de ser recriado
        var indiceDrop = operacoes.FindIndex(o =>
            o.Tipo == "DropCheckConstraintOperation" && o.Nome == "ck_ctr_manifestacao_inciso");
        Assert.True(indiceDrop >= 0 && indiceDrop < operacoes.IndexOf(incisoNovo));

        Assert.Contains(operacoes, o =>
            o.Tipo == "DropCheckConstraintOperation" && o.Nome == "ck_ctr_manifestacao_criticidade");

        var novos = operacoes
            .Where(o => o.Tipo == "AddCheckConstraintOperation")
            .Select(o => o.Nome)
            .ToList();

        Assert.Contains("ck_ctr_processo_retorno_orgao", novos);
        Assert.Contains("ck_ctr_processo_etapa_planejamento", novos);
        Assert.Contains("ck_ctr_processo_criticidade", novos);
        Assert.Contains("ck_ctr_processo_origem", novos);
        Assert.Contains("ck_ctr_manifestacao_status", novos);
    }
}
