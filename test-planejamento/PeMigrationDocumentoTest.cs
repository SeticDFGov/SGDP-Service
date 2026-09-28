using System.Collections;
using System.Reflection;
using Models.Planejamento;
using Xunit;

namespace test.planejamento;

/// <summary>
/// Migration PeDocumento (E5) por INSPEÇÃO das operações, lidas por reflexão (o provider
/// InMemory não executa migration), no padrão das migrations anteriores do módulo. O que se
/// prova: só cria as 6 tabelas pe_doc_* e os índices delas; nas tabelas que já existem só troca
/// dois CHECKs do próprio módulo (o domínio do histórico do modelo ganha os capítulos e os
/// blocos do documento; o dono do arquivo ganha o PDTIC e o modelo do documento); as FKs só
/// apontam para tabelas pe_; o Down desfaz só isso: volta a versão do conteúdo semeado a 2
/// (sem isso, a E5 aplicada de novo acharia a versão 3 e não carregaria o modelo do documento)
/// e tira das tabelas que ficam o que era só da E5, para os CHECKs antigos voltarem.
/// </summary>
public class PeMigrationDocumentoTest
{
    private const string NomeMigration = "demanda_service.Migrations.PeDocumento";

    private static readonly string[] Tabelas =
        { "pe_doc_bloco", "pe_doc_capitulo", "pe_doc_modelo", "pe_doc_orgao", "pe_doc_orgao_bloco", "pe_doc_versao" };

    private static List<object> Operacoes(string lado = "UpOperations")
    {
        var tipo = typeof(PeDocModelo).Assembly.GetType(NomeMigration);
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
    public void Migration_SoCriaAsTabelasDoDocumento_ETrocaDoisChecksDoModulo()
    {
        var operacoes = Operacoes();

        Assert.All(operacoes, o => Assert.Contains(Tipo(o), new[]
        {
            "CreateTableOperation", "CreateIndexOperation", "DropCheckConstraintOperation", "AddCheckConstraintOperation"
        }));
        Assert.Equal(Tabelas, operacoes.Where(o => Tipo(o) == "CreateTableOperation").Select(o => Texto(o, "Name")).OrderBy(n => n));
        Assert.All(operacoes, o => Assert.StartsWith("pe_", Texto(o, "Table") ?? Texto(o, "Name")));
        Assert.All(operacoes.Where(o => Tipo(o) == "CreateIndexOperation"), i => Assert.Matches("^(ux|ix)_pe_doc_", Texto(i, "Name")));

        var trocados = operacoes.Where(o => Tipo(o) is "DropCheckConstraintOperation" or "AddCheckConstraintOperation").ToList();
        Assert.Equal(new[] { "ck_pe_arquivo_dono_tipo", "ck_pe_modelo_historico_entidade" },
            trocados.Select(o => Texto(o, "Name")).Distinct().OrderBy(n => n));
        Assert.Equal(new[] { "pe_arquivo", "pe_modelo_historico" }, trocados.Select(o => Texto(o, "Table")).Distinct().OrderBy(n => n));
        Assert.Equal("dono_tipo IS NULL OR dono_tipo IN ('registro','pdtic','doc_modelo')",
            Texto(trocados.Single(o => Tipo(o) == "AddCheckConstraintOperation" && Texto(o, "Table") == "pe_arquivo"), "Sql"));
        Assert.Equal("entidade IN ('nivel','etapa','passo','secao','campo','opcao','orgao_nivel','orgao_ajuste','doc_capitulo','doc_bloco')",
            Texto(trocados.Single(o => Tipo(o) == "AddCheckConstraintOperation" && Texto(o, "Table") == "pe_modelo_historico"), "Sql"));
    }

    [Fact]
    public void Down_DesfazSoAE5()
    {
        var operacoes = Operacoes("DownOperations");

        Assert.Equal(Tabelas, operacoes.Where(o => Tipo(o) == "DropTableOperation").Select(o => Texto(o, "Name")).OrderBy(n => n));
        var outras = operacoes.Where(o => Tipo(o) is not ("DropTableOperation" or "SqlOperation")).ToList();
        Assert.All(outras, o => Assert.Contains(Tipo(o), new[] { "DropCheckConstraintOperation", "AddCheckConstraintOperation" }));
        Assert.Equal("dono_tipo IS NULL OR dono_tipo IN ('registro')",
            Texto(outras.Single(o => Tipo(o) == "AddCheckConstraintOperation" && Texto(o, "Table") == "pe_arquivo"), "Sql"));

        // SQL à mão, só em tabelas pe_: antes de tudo, a versão do conteúdo semeado volta a 2;
        // depois de apagar as tabelas e antes de voltar os CHECKs antigos, sai o que era só da E5
        // nas tabelas que ficam (o histórico do modelo do documento e os arquivos do documento)
        var sqls = operacoes.Where(o => Tipo(o) == "SqlOperation").ToList();
        Assert.Equal(3, sqls.Count);
        Assert.Same(sqls[0], operacoes[0]);
        Assert.StartsWith("UPDATE pe_configuracao SET valor = '2' WHERE chave = 'seed_modelo_versao' ", Texto(sqls[0], "Sql"));
        Assert.Equal("DELETE FROM pe_modelo_historico WHERE entidade IN ('doc_capitulo','doc_bloco');", Texto(sqls[1], "Sql"));
        Assert.Equal("DELETE FROM pe_arquivo WHERE dono_tipo IN ('pdtic','doc_modelo');", Texto(sqls[2], "Sql"));
        var ultimaTabela = operacoes.FindLastIndex(o => Tipo(o) == "DropTableOperation");
        var primeiroCheck = operacoes.FindIndex(o => Tipo(o) == "AddCheckConstraintOperation");
        Assert.All(sqls.Skip(1), s => Assert.InRange(operacoes.IndexOf(s), ultimaTabela + 1, primeiroCheck - 1));
    }

    [Fact]
    public void ForeignKeys_SoParaTabelasPe()
    {
        var fks = Operacoes().Where(o => Tipo(o) == "CreateTableOperation")
            .SelectMany(t => Lista(t, "ForeignKeys"))
            .ToDictionary(fk => Texto(fk, "Name")!, fk => (Principal: Texto(fk, "PrincipalTable")!, OnDelete: Valor(fk, "OnDelete")!.ToString()));

        Assert.All(fks.Keys, n => Assert.StartsWith("fk_pe_doc_", n));
        Assert.All(fks.Values, v => Assert.StartsWith("pe_", v.Principal));
        Assert.Equal(("pe_doc_modelo", "Restrict"), fks["fk_pe_doc_capitulo_modelo"]);
        Assert.Equal(("pe_doc_capitulo", "Restrict"), fks["fk_pe_doc_capitulo_pai"]);
        Assert.Equal(("pe_doc_capitulo", "Restrict"), fks["fk_pe_doc_bloco_capitulo"]);
        Assert.Equal(("pe_pdtic", "Cascade"), fks["fk_pe_doc_orgao_pdtic"]);
        Assert.Equal(("pe_doc_capitulo", "Restrict"), fks["fk_pe_doc_orgao_capitulo"]);
        Assert.Equal(("pe_pdtic", "Cascade"), fks["fk_pe_doc_orgao_bloco_pdtic"]);
        Assert.Equal(("pe_doc_bloco", "Restrict"), fks["fk_pe_doc_orgao_bloco_bloco"]);
        Assert.Equal(("pe_pdtic", "Cascade"), fks["fk_pe_doc_versao_pdtic"]);
        Assert.Equal(("pe_arquivo", "Restrict"), fks["fk_pe_doc_versao_arquivo"]);
    }

    [Fact]
    public void Checks_EIndices()
    {
        string Check(string tabela, string nome) =>
            Texto(Lista(Tabela(tabela), "CheckConstraints").Single(c => Texto(c, "Name") == nome), "Sql")!;

        Assert.Equal("tipo IN ('pdtic')", Check("pe_doc_modelo", "ck_pe_doc_modelo_tipo"));
        Assert.Equal("tipo IN ('texto','tabela_secao','lista_tema','matriz_swot','fluxo','quebra_pagina')", Check("pe_doc_bloco", "ck_pe_doc_bloco_tipo"));
        Assert.Equal("NOT travado OR obrigatorio", Check("pe_doc_capitulo", "ck_pe_doc_capitulo_travado"));
        Assert.Equal("situacao IN ('minuta','enviada','aprovada','publicada')", Check("pe_doc_versao", "ck_pe_doc_versao_situacao"));
        Assert.All(Operacoes().Where(o => Tipo(o) == "CreateTableOperation").SelectMany(t => Lista(t, "CheckConstraints")),
            c => Assert.StartsWith("ck_pe_doc_", Texto(c, "Name")));

        var indices = Operacoes().Where(o => Tipo(o) == "CreateIndexOperation")
            .ToDictionary(o => Texto(o, "Name")!, o => (
                Colunas: string.Join(",", (string[])Valor(o, "Columns")!),
                Unico: (bool)Valor(o, "IsUnique")!,
                Filtro: Texto(o, "Filter")));
        Assert.Equal(("tipo", true, "ativo"), indices["ux_pe_doc_modelo_ativo"]);
        Assert.Equal(("modelo_id,chave", true, (string?)null), indices["ux_pe_doc_capitulo_chave"]);
        Assert.Equal(("pdtic_id,capitulo_id", true, (string?)null), indices["ux_pe_doc_orgao"]);
        Assert.Equal(("pdtic_id,bloco_id", true, (string?)null), indices["ux_pe_doc_orgao_bloco"]);
        Assert.Equal(("pdtic_id,numero", true, (string?)null), indices["ux_pe_doc_versao_numero"]);
    }
}
