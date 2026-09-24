using System.Collections;
using System.Reflection;
using app.Auth;
using Models.Planejamento;
using Xunit;

namespace test.planejamento;

/// <summary>
/// Migration PeAcessoPapeis por INSPEÇÃO das operações, lidas por reflexão (mesmo
/// padrão do CtrMigrationSupervisaoTest: o provider InMemory não executa migration).
/// O que se prova: além das duas tabelas pe_ (e do índice), o script só recria os dois
/// CHECKs de módulo de acesso_modulo e pedido_acesso, agora com "planejamento"; nenhuma
/// coluna nova em tabela existente (Users e pedido_acesso ficam como estão).
/// </summary>
public class PeMigrationTest
{
    private const string NomeMigration = "demanda_service.Migrations.PeAcessoPapeis";

    private static readonly string[] ChecksDeModulo = { "ck_acesso_modulo_modulo", "ck_pedido_acesso_modulo" };

    private static List<object> Operacoes(string lado = "UpOperations")
    {
        var tipo = typeof(PePapelUsuario).Assembly.GetType(NomeMigration);
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
    public void Migration_SoCriaObjetosPe_EAlemDelesSoTrocaOsDoisChecksDeModulo()
    {
        var operacoes = Operacoes();
        Assert.NotEmpty(operacoes);

        foreach (var operacao in operacoes)
        {
            // Nenhum SQL cru, nenhuma coluna ou tabela existente alterada
            Assert.NotEqual("SqlOperation", Tipo(operacao));
            Assert.NotEqual("AddColumnOperation", Tipo(operacao));
            Assert.NotEqual("AlterColumnOperation", Tipo(operacao));
            Assert.NotEqual("DropColumnOperation", Tipo(operacao));

            var tabela = Tipo(operacao) == "CreateTableOperation" ? Texto(operacao, "Name") : Texto(operacao, "Table");
            Assert.NotNull(tabela);

            if (tabela!.StartsWith("pe_")) continue;

            // Fora das tabelas pe_, só a troca dos dois CHECKs de módulo
            Assert.Contains(Tipo(operacao), new[] { "DropCheckConstraintOperation", "AddCheckConstraintOperation" });
            Assert.Contains(Texto(operacao, "Name"), ChecksDeModulo);
        }

        Assert.Equal(new[] { "pe_papel_usuario", "pe_papel_usuario_historico" },
            operacoes.Where(o => Tipo(o) == "CreateTableOperation").Select(o => Texto(o, "Name")).OrderBy(n => n));
        Assert.Equal(2, operacoes.Count(o => Tipo(o) == "DropCheckConstraintOperation"));
        Assert.Equal(2, operacoes.Count(o => Tipo(o) == "AddCheckConstraintOperation"));

        var indice = Assert.Single(operacoes, o => Tipo(o) == "CreateIndexOperation");
        Assert.Equal("ix_pe_papel_usuario_historico_user", Texto(indice, "Name"));
        Assert.Equal(new[] { "user_id", "alterado_em" }, (string[])Valor(indice, "Columns")!);
    }

    [Fact]
    public void Migration_ChecksNovosTrazemOModulo_EODownVoltaOsAntigos()
    {
        var novos = Operacoes().Where(o => Tipo(o) == "AddCheckConstraintOperation")
            .ToDictionary(o => Texto(o, "Name")!, o => Texto(o, "Sql")!);

        Assert.Equal("modulo IN ('demandas','pgia','contratacoes','planejamento','administracao')",
            novos["ck_acesso_modulo_modulo"]);
        Assert.Equal("modulo IN ('demandas','pgia','contratacoes','planejamento')",
            novos["ck_pedido_acesso_modulo"]);

        var antigos = Operacoes("DownOperations").Where(o => Tipo(o) == "AddCheckConstraintOperation")
            .ToDictionary(o => Texto(o, "Name")!, o => Texto(o, "Sql")!);

        Assert.DoesNotContain(ModulosSgdp.Planejamento, antigos["ck_acesso_modulo_modulo"]);
        Assert.DoesNotContain(ModulosSgdp.Planejamento, antigos["ck_pedido_acesso_modulo"]);
    }

    [Fact]
    public void TabelaDoPapel_ChaveNoUsuario_CheckDosCincoPapeis_FkEmCascata()
    {
        var tabela = Operacoes().Single(o => Tipo(o) == "CreateTableOperation" && Texto(o, "Name") == "pe_papel_usuario");

        Assert.Equal(new[] { "user_id", "papel", "concedido_em", "concedido_por", "alterado_em", "alterado_por" },
            Lista(tabela, "Columns").Select(c => Texto(c, "Name")));

        var chave = Valor(tabela, "PrimaryKey")!;
        Assert.Equal("pk_pe_papel_usuario", Texto(chave, "Name"));
        Assert.Equal(new[] { "user_id" }, (string[])Valor(chave, "Columns")!);

        var check = Assert.Single(Lista(tabela, "CheckConstraints"));
        Assert.Equal("ck_pe_papel_usuario_papel", Texto(check, "Name"));
        Assert.Equal("papel IN ('pe_admin','pe_sgdi','pe_cgtic','pe_orgao','pe_orgao_consulta')", Texto(check, "Sql"));

        var fk = Assert.Single(Lista(tabela, "ForeignKeys"));
        Assert.Equal("fk_pe_papel_usuario_user", Texto(fk, "Name"));
        Assert.Equal("Users", Texto(fk, "PrincipalTable"));
        Assert.Equal("Cascade", Valor(fk, "OnDelete")!.ToString());
    }

    [Fact]
    public void TabelaDoHistorico_CheckDaOrigem_PedidoSemFk_FkEmCascata()
    {
        var tabela = Operacoes().Single(o => Tipo(o) == "CreateTableOperation" && Texto(o, "Name") == "pe_papel_usuario_historico");

        Assert.Equal(
            new[] { "id", "user_id", "papel_anterior", "papel_novo", "origem", "pedido_acesso_id", "alterado_em", "alterado_por" },
            Lista(tabela, "Columns").Select(c => Texto(c, "Name")));

        var colunas = Lista(tabela, "Columns").ToDictionary(c => Texto(c, "Name")!);
        Assert.True((bool)Valor(colunas["papel_anterior"], "IsNullable")!);
        Assert.True((bool)Valor(colunas["papel_novo"], "IsNullable")!);
        Assert.True((bool)Valor(colunas["pedido_acesso_id"], "IsNullable")!);

        var check = Assert.Single(Lista(tabela, "CheckConstraints"));
        Assert.Equal("ck_pe_papel_usuario_historico_origem", Texto(check, "Name"));
        Assert.Equal("origem IN ('pessoas','pedido','gestao_acessos','modo_local')", Texto(check, "Sql"));

        // Só a FK para Users: o pedido de acesso não é FK (o histórico não depende dele)
        var fk = Assert.Single(Lista(tabela, "ForeignKeys"));
        Assert.Equal("fk_pe_papel_usuario_historico_user", Texto(fk, "Name"));
        Assert.Equal("Cascade", Valor(fk, "OnDelete")!.ToString());
    }
}
