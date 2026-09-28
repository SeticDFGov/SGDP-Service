using api.Planejamento;
using service.Planejamento;
using Xunit;

namespace test.planejamento;

/// <summary>
/// F2, observação D-O2 da reconferência: dois itens iguais (dois fins soltos na mesma raia, dois
/// inícios, duas decisões com a mesma pergunta, duas tarefas sem nome depois do mesmo passo) não
/// viram uma mensagem só nem uma mensagem que não diz qual: cada um sai com a sua linha, pela
/// posição entre os do mesmo tipo na raia em que o desenho o põe (da esquerda para a direita e, na
/// mesma coluna, de cima para baixo). O vizinho sem nome de um tipo que se repete vai pela raia.
/// </summary>
public class PeF2FluxoMensagensTest : PeFluxoTestBase
{
    private static object R(string id, string nome, int ordem) => new { Id = id, Nome = nome, Ordem = ordem };

    private static object E(string id, string tipo, string raia, string nome = "") =>
        new { Id = id, Tipo = tipo, RaiaId = raia, Nome = nome, Artefatos = Array.Empty<string>() };

    private static object L(string id, string de, string para, string? rotulo = null) => new { Id = id, De = de, Para = para, Rotulo = rotulo };

    private static object Def(object[] raias, object[] elementos, object[] ligacoes, string? prefixo = "2") =>
        new { PrefixoNumeracao = prefixo, Raias = raias, Elementos = elementos, Ligacoes = ligacoes };

    private async Task<PeFluxoGeometria> GeometriaAsync(object definicao) =>
        await Fluxos.GeometriaAsync(new PeFluxoDesenhoDTO { Definicao = ComoJson(definicao) }, await Orgao());

    [Fact]
    public async Task DoisFinsSoltosNaMesmaRaia_UmaLinhaParaCada_PelaPosicao()
    {
        var definicao = Def(
            new[] { R("zqr1", "Equipe", 1) },
            new[] { E("zq1", "inicio", "zqr1"), E("zq2", "tarefa", "zqr1", "Levantar"), E("zq3", "fim", "zqr1"), E("zq4", "fim", "zqr1"), E("zq5", "fim", "zqr1") },
            new[] { L("zql1", "zq1", "zq2"), L("zql2", "zq2", "zq3") });

        // Antes: uma linha só ("O fim, na raia "Equipe", está solto: ..."), sem dizer que eram dois
        var erros = ErrosDe(definicao);
        Assert.Equal(new[]
        {
            "O 2º fim da raia \"Equipe\" está solto: ligue o último passo a ele.",
            "O 3º fim da raia \"Equipe\" está solto: ligue o último passo a ele."
        }, erros);

        // A posição é a do desenho: o 2º fim fica à esquerda do 3º, os dois depois do fim ligado
        var g = await GeometriaAsync(definicao);
        var x = g.Elementos.ToDictionary(e => e.Id, e => e.X);
        Assert.True(x["zq3"] < x["zq4"] && x["zq4"] < x["zq5"]);
        Assert.Equal(erros, g.Erros);
    }

    [Fact]
    public void DoisIniciosNaMesmaRaia_OSemLigacao_PelaPosicao()
    {
        var erros = ErrosDe(Def(
            new[] { R("zqr1", "Equipe", 1) },
            new[] { E("zq1", "inicio", "zqr1"), E("zq2", "tarefa", "zqr1", "Levantar"), E("zq3", "fim", "zqr1"), E("zq4", "inicio", "zqr1") },
            new[] { L("zql1", "zq1", "zq2"), L("zql2", "zq2", "zq3") }));

        // Antes: "Ligue o início, na raia "Equipe", ao primeiro passo do fluxo.", sem dizer qual
        Assert.Equal(new[]
        {
            "O fluxo tem 2 inícios. Deixe só um.",
            "Ligue o 2º início da raia \"Equipe\" ao primeiro passo do fluxo."
        }, erros);

        // Em raias diferentes, a raia basta
        var emOutraRaia = ErrosDe(Def(
            new[] { R("zqr1", "Equipe", 1), R("zqr2", "Comitê", 2) },
            new[] { E("zq1", "inicio", "zqr1"), E("zq2", "tarefa", "zqr1", "Levantar"), E("zq3", "fim", "zqr1"), E("zq4", "inicio", "zqr2") },
            new[] { L("zql1", "zq1", "zq2"), L("zql2", "zq2", "zq3") }));
        Assert.Contains("Ligue o início, na raia \"Comitê\", ao primeiro passo do fluxo.", emOutraRaia);
    }

    [Fact]
    public void CasoDaReconferencia_NaPreparacao_OsSoltosPelaPosicao()
    {
        // A preparação do guia (o início e o fim na raia do comitê) com cinco blocos soltos na
        // mesma raia: um paralelo, uma tarefa com nome, um início e dois fins
        var definicao = DefinicaoDoModelo("preparacao");
        var raia = definicao.Raias.Single(r => r.Nome == "{nomes.comite}").Id;
        definicao.Elementos.AddRange(new[]
        {
            new PeFluxoElemento { Id = "zq1", Tipo = "paralelo", RaiaId = raia },
            new PeFluxoElemento { Id = "zq2", Tipo = "tarefa", RaiaId = raia, Nome = "Tarefa nomeada" },
            new PeFluxoElemento { Id = "zq3", Tipo = "inicio", RaiaId = raia },
            new PeFluxoElemento { Id = "zq4", Tipo = "fim", RaiaId = raia },
            new PeFluxoElemento { Id = "zq5", Tipo = "fim", RaiaId = raia }
        });

        var erros = PeFluxoDefinicaoLeitor.Ler(ComoJson(definicao)).Erros;

        Assert.Equal(new[]
        {
            "O fluxo tem 2 inícios. Deixe só um.",
            "O paralelo sem nome, na raia \"Comitê de Governança Digital\", precisa de entrada e de saída.",
            "A tarefa 1.9 \"Tarefa nomeada\" não tem de onde vir: diga o que vem antes dela.",
            "A tarefa 1.9 \"Tarefa nomeada\" não leva a lugar nenhum: diga o que vem depois.",
            "Ligue o 2º início da raia \"Comitê de Governança Digital\" ao primeiro passo do fluxo.",
            "O 2º fim da raia \"Comitê de Governança Digital\" está solto: ligue o último passo a ele.",
            "O 3º fim da raia \"Comitê de Governança Digital\" está solto: ligue o último passo a ele."
        }, erros);
        Assert.DoesNotContain(erros, m => m.Contains("zq", StringComparison.Ordinal));
    }

    [Fact]
    public void DecisoesComAMesmaPergunta_ONomeEAPosicao()
    {
        var erros = ErrosDe(Def(
            new[] { R("zqr1", "Equipe", 1) },
            new[]
            {
                E("zq1", "inicio", "zqr1"), E("zq2", "tarefa", "zqr1", "Revisar"), E("zq3", "decisao", "zqr1", "Aprovado?"),
                E("zq4", "tarefa", "zqr1", "Ajustar"), E("zq5", "decisao", "zqr1", "Aprovado?"), E("zq6", "fim", "zqr1")
            },
            new[] { L("zql1", "zq1", "zq2"), L("zql2", "zq2", "zq3"), L("zql3", "zq3", "zq4", "Sim"), L("zql4", "zq4", "zq5"), L("zql5", "zq5", "zq6", "Sim") }));

        Assert.Equal(new[]
        {
            "A decisão \"Aprovado?\", a 1ª da raia \"Equipe\", precisa de pelo menos duas saídas (por exemplo, Sim e Não).",
            "A decisão \"Aprovado?\", a 2ª da raia \"Equipe\", precisa de pelo menos duas saídas (por exemplo, Sim e Não)."
        }, erros);
    }

    [Fact]
    public void TarefasSemNomeDepoisDoMesmoPasso_OPedidoDoNomePelaPosicao()
    {
        // Sem prefixo (sem número): as duas tarefas abertas pelo mesmo paralelo, uma sobre a outra
        var erros = ErrosDe(Def(
            new[] { R("zqr1", "Equipe", 1) },
            new[]
            {
                E("zq1", "inicio", "zqr1"), E("zq2", "paralelo", "zqr1"), E("zq3", "tarefa", "zqr1"), E("zq4", "tarefa", "zqr1"),
                E("zq5", "paralelo", "zqr1"), E("zq6", "fim", "zqr1")
            },
            new[] { L("zql1", "zq1", "zq2"), L("zql2", "zq2", "zq3"), L("zql3", "zq2", "zq4"), L("zql4", "zq3", "zq5"), L("zql5", "zq4", "zq5"), L("zql6", "zq5", "zq6") },
            prefixo: null));

        Assert.Equal(new[] { "Dê um nome à 1ª tarefa da raia \"Equipe\".", "Dê um nome à 2ª tarefa da raia \"Equipe\"." }, erros);
    }

    [Fact]
    public void VizinhoSemNomeDeUmTipoQueSeRepete_PelaRaia()
    {
        // Dois fins, um em cada raia: o paralelo que vem antes de um deles diz qual
        var erros = ErrosDe(Def(
            new[] { R("zqr1", "Equipe", 1), R("zqr2", "Comitê", 2) },
            new[]
            {
                E("zq1", "inicio", "zqr1"), E("zq2", "tarefa", "zqr1", "Levantar"), E("zq3", "fim", "zqr1"),
                E("zq4", "paralelo", "zqr2"), E("zq5", "fim", "zqr2")
            },
            new[] { L("zql1", "zq1", "zq2"), L("zql2", "zq2", "zq3"), L("zql3", "zq4", "zq5") }));

        Assert.Equal(new[] { "O paralelo sem nome, antes do fim da raia \"Comitê\", precisa de entrada e de saída." }, erros);
    }
}
