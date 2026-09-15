using System.Collections;
using System.Reflection;
using api.Contratacoes;
using Microsoft.EntityFrameworkCore.Metadata;
using Models.Contratacoes;
using Xunit;

namespace test.contratacoes;

/// <summary>
/// Migration da rodada da Supervisão Contínua (CtrSupervisaoClassificacaoRisco) por
/// INSPEÇÃO das operações e do modelo-alvo, lidos por reflexão — mesmo padrão e mesma
/// limitação do CtrMigrationRodadaChefiaTest: o provider InMemory não executa
/// migration, e o projeto de teste não referencia o EF Relational (seria dependência
/// nova). O que se prova aqui: o rename de pendencias_tcdf é RENAME (preserva os
/// dados) e não DROP+ADD, e o script só toca objetos ctr_*.
/// </summary>
public class CtrMigrationSupervisaoTest
{
    private const string NomeMigration = "demanda_service.Migrations.CtrSupervisaoClassificacaoRisco";

    private static object Migration()
    {
        var tipo = typeof(CtrProcesso).Assembly.GetType(NomeMigration);
        Assert.NotNull(tipo);

        var migration = Activator.CreateInstance(tipo!)!;
        tipo!.GetProperty("ActiveProvider")!.SetValue(migration, "Npgsql.EntityFrameworkCore.PostgreSQL");
        return migration;
    }

    private static List<object> Operacoes(string lado = "UpOperations")
    {
        var migration = Migration();
        return ((IEnumerable)migration.GetType().GetProperty(lado)!.GetValue(migration)!).Cast<object>().ToList();
    }

    private static object? Valor(object operacao, string propriedade) =>
        operacao.GetType().GetProperty(propriedade, BindingFlags.Public | BindingFlags.Instance)?.GetValue(operacao);

    private static string? Texto(object operacao, string propriedade) => Valor(operacao, propriedade) as string;

    private static string Tipo(object operacao) => operacao.GetType().Name;

    [Fact]
    public void Migration_SoTocaEmObjetosDoModulo()
    {
        var operacoes = Operacoes();
        Assert.NotEmpty(operacoes);

        foreach (var operacao in operacoes)
        {
            // Nenhum SQL cru nesta migration
            Assert.NotEqual("SqlOperation", Tipo(operacao));

            var tabela = Tipo(operacao) == "CreateTableOperation" ? Texto(operacao, "Name") : Texto(operacao, "Table");
            Assert.NotNull(tabela);
            Assert.StartsWith("ctr_", tabela);
        }
    }

    [Fact]
    public void Migration_RenomeiaPendenciasParaEsclarecimentosAdicionais_SemDropNemAdd()
    {
        var operacoes = Operacoes();

        var rename = Assert.Single(operacoes, o => Tipo(o) == "RenameColumnOperation");
        Assert.Equal("ctr_manifestacao_tcdf", Texto(rename, "Table"));
        Assert.Equal("pendencias_tcdf", Texto(rename, "Name"));
        Assert.Equal("esclarecimentos_adicionais", Texto(rename, "NewName"));

        // DROP+ADD perderia o texto gravado: nenhuma das duas pode aparecer
        Assert.DoesNotContain(operacoes, o => Tipo(o) == "DropColumnOperation");
        Assert.DoesNotContain(operacoes, o =>
            Tipo(o) == "AddColumnOperation" && Texto(o, "Name") == "esclarecimentos_adicionais");

        // O Down desfaz com outro RENAME (também sem perda)
        var volta = Assert.Single(Operacoes("DownOperations"), o => Tipo(o) == "RenameColumnOperation");
        Assert.Equal("esclarecimentos_adicionais", Texto(volta, "Name"));
        Assert.Equal("pendencias_tcdf", Texto(volta, "NewName"));
    }

    [Fact]
    public void Migration_AdicionaAsSeisColunasNullableDoProcessoEOsDoisChecks()
    {
        var operacoes = Operacoes();

        var colunas = operacoes.Where(o => Tipo(o) == "AddColumnOperation").ToList();
        Assert.All(colunas, c => Assert.Equal("ctr_processo", Texto(c, "Table")));
        Assert.Equal(
            new[]
            {
                "checklist_risco", "enquadramento_risco", "pontuacao_risco", "risco_classificado",
                "risco_classificado_em", "risco_classificado_por"
            },
            colunas.Select(c => Texto(c, "Name")).OrderBy(n => n, StringComparer.Ordinal));
        // Os 37 processos existentes nascem "Não classificado"
        Assert.All(colunas, c => Assert.Equal(true, Valor(c, "IsNullable")));
        Assert.Equal("jsonb", Texto(colunas.Single(c => Texto(c, "Name") == "checklist_risco"), "ColumnType"));

        var checks = operacoes.Where(o => Tipo(o) == "AddCheckConstraintOperation")
            .ToDictionary(o => Texto(o, "Name")!, o => Texto(o, "Sql")!);
        Assert.Equal(2, checks.Count);
        Assert.Equal(
            "risco_classificado IS NULL OR risco_classificado IN ('Risco Excessivo','Alto Risco','Risco Moderado','Baixo Risco')",
            checks["ck_ctr_processo_risco_classificado"]);
        Assert.Equal(
            "(checklist_risco IS NULL AND risco_classificado IS NULL AND pontuacao_risco IS NULL AND risco_classificado_em IS NULL) "
            + "OR (checklist_risco IS NOT NULL AND risco_classificado IS NOT NULL AND pontuacao_risco IS NOT NULL "
            + "AND risco_classificado_em IS NOT NULL)",
            checks["ck_ctr_processo_classificacao"]);
    }

    [Fact]
    public void Migration_CriaCtrRiscoDeclaradoComCascadeIndiceEChecksNaEscalaCompleta()
    {
        var operacoes = Operacoes();

        var tabela = Assert.Single(operacoes, o => Tipo(o) == "CreateTableOperation");
        Assert.Equal("ctr_risco_declarado", Texto(tabela, "Name"));
        Assert.Equal("pk_ctr_risco_declarado", Texto(Valor(tabela, "PrimaryKey")!, "Name"));

        var fk = Assert.Single(((IEnumerable)Valor(tabela, "ForeignKeys")!).Cast<object>());
        Assert.Equal("fk_ctr_risco_declarado_processo", Texto(fk, "Name"));
        Assert.Equal("ctr_processo", Texto(fk, "PrincipalTable"));
        Assert.Equal("Cascade", Valor(fk, "OnDelete")!.ToString());

        var checks = ((IEnumerable)Valor(tabela, "CheckConstraints")!).Cast<object>()
            .ToDictionary(c => Texto(c, "Name")!, c => Texto(c, "Sql")!);
        Assert.Equal("probabilidade IN ('Improvável','Raro','Possível','Provável','Quase certo')",
            checks["ck_ctr_risco_declarado_probabilidade"]);
        Assert.Equal("consequencia IN ('Desprezível','Menor','Moderada','Maior','Catastrófica')",
            checks["ck_ctr_risco_declarado_consequencia"]);

        var indice = Assert.Single(operacoes, o => Tipo(o) == "CreateIndexOperation");
        Assert.Equal("ix_ctr_risco_declarado_processo", Texto(indice, "Name"));
        Assert.Equal("ctr_risco_declarado", Texto(indice, "Table"));
    }

    [Fact]
    public void ModeloAlvo_MapeiaEsclarecimentosAdicionaisNaColunaRenomeada()
    {
        var migration = Migration();
        var modelo = (IModel)migration.GetType().GetProperty("TargetModel")!.GetValue(migration)!;

        var manifestacao = modelo.FindEntityType("Models.Contratacoes.CtrManifestacaoTcdf")!;
        Assert.Null(manifestacao.FindProperty("PendenciasTcdf"));
        Assert.Equal("esclarecimentos_adicionais",
            manifestacao.FindProperty("EsclarecimentosAdicionais")!.FindAnnotation("Relational:ColumnName")!.Value);
    }

    [Fact]
    public void Rename_DePontaAPonta_NenhumContratoCSharpGuardaONomeAntigo()
    {
        foreach (var tipo in new[]
                 {
                     typeof(CtrManifestacaoTcdf), typeof(CtrManifestacaoCreateDTO),
                     typeof(CtrManifestacaoUpdateDTO), typeof(CtrManifestacaoResponse)
                 })
        {
            Assert.Null(tipo.GetProperty("PendenciasTcdf"));
            Assert.NotNull(tipo.GetProperty("EsclarecimentosAdicionais"));
        }
    }
}
