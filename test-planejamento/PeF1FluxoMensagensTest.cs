using System.Text.RegularExpressions;
using api.Planejamento;
using service.Planejamento;
using Xunit;

namespace test.planejamento;

/// <summary>
/// F1, achado D11 da revisão complementar: as mensagens da validação dos fluxos (Erros do POST
/// fluxos/validacao, da geometria, da prévia e da gravação) citam cada item como o desenho o
/// mostra: a tarefa e o subprocesso pelo número e pelo nome, a decisão pela pergunta, o que não
/// tem nome pelo vizinho ("o paralelo sem nome, depois de 2.2"), a raia pelo nome (com o
/// dicionário) ou pela posição de cima para baixo; nunca pelo id interno nem pela posição na
/// lista; e a mesma linha não se repete. Os ids destes testes começam com "zq": nenhum aparece
/// nas mensagens.
/// </summary>
public class PeF1FluxoMensagensTest : PeFluxoTestBase
{
    private static object R(string id, string nome, int ordem) => new { Id = id, Nome = nome, Ordem = ordem };

    private static object E(string id, string tipo, string raia, string nome = "", string[]? artefatos = null) =>
        new { Id = id, Tipo = tipo, RaiaId = raia, Nome = nome, Artefatos = artefatos ?? Array.Empty<string>() };

    private static object L(string id, string de, string para, string? rotulo = null) => new { Id = id, De = de, Para = para, Rotulo = rotulo };

    private static object Def(object[] raias, object[] elementos, object[] ligacoes, string? prefixo = "2") =>
        new { PrefixoNumeracao = prefixo, Raias = raias, Elementos = elementos, Ligacoes = ligacoes };

    private async Task<PeFluxoGeometria> GeometriaAsync(object definicao, long? pdticId = null) =>
        await Fluxos.GeometriaAsync(new PeFluxoDesenhoDTO { Definicao = ComoJson(definicao), PdticId = pdticId }, await Orgao());

    /// <summary>Nenhuma mensagem cita um id ("zq...") nem a posição de um passo, de uma ligação ou de uma raia na lista.</summary>
    private static void SemIdNemPosicao(IEnumerable<string> erros)
    {
        foreach (var m in erros)
        {
            Assert.DoesNotContain("zq", m);
            Assert.DoesNotMatch(new Regex(@"\b(passo|ligação|raia) \d"), m);
        }
    }

    [Fact]
    public async Task CasoDaRevisao_ParalelosSemNome_PeloVizinho_EOInicioEOFimRepetidos_PelaRaia()
    {
        var definicao = Def(
            new[] { R("zqr1", "Equipe", 1), R("zqr2", "{nomes.comite}", 2) },
            new[]
            {
                E("zq1", "inicio", "zqr1"), E("zq2", "tarefa", "zqr1", "Levantar os dados"), E("zq3", "tarefa", "zqr1", "Descrever o ambiente"),
                E("zq4", "paralelo", "zqr1"), E("zq5", "tarefa", "zqr1", "Consolidar"), E("zq6", "fim", "zqr1"),
                E("zq7", "paralelo", "zqr2"), E("zq8", "inicio", "zqr2"), E("zq9", "fim", "zqr2")
            },
            new[] { L("zql1", "zq1", "zq2"), L("zql2", "zq2", "zq3"), L("zql3", "zq3", "zq4"), L("zql4", "zq4", "zq5"), L("zql5", "zq5", "zq6") });

        var erros = ErrosDe(definicao);

        // Antes: "O paralelo e4 precisa...", "O paralelo e7 precisa de entrada e de saída." e as
        // linhas do segundo início e do segundo fim iguais às de um só
        Assert.Equal(new[]
        {
            "O fluxo tem 2 inícios. Deixe só um.",
            "O paralelo sem nome, depois de 2.2, precisa abrir caminhos (duas saídas ou mais) ou juntar caminhos (duas entradas ou mais).",
            "O paralelo sem nome, na raia \"Comitê de Governança Digital\", precisa de entrada e de saída.",
            "Ligue o início, na raia \"Comitê de Governança Digital\", ao primeiro passo do fluxo.",
            "O fim, na raia \"Comitê de Governança Digital\", está solto: ligue o último passo a ele."
        }, erros);
        SemIdNemPosicao(erros);

        // O número da mensagem é o do desenho, e a geometria traz as mesmas linhas
        var g = await GeometriaAsync(definicao);
        Assert.Equal("2.2", g.Numeros["zq3"]);
        Assert.Equal(erros, g.Erros);
    }

    [Fact]
    public async Task SemNome_PeloNumeroDoDesenho_OuPeloVizinho_EARaiaPelaPosicao()
    {
        // Com prefixo: o subprocesso sem nome pelo número que o desenho mostra
        var comNumero = Def(
            new[] { R("zqr1", "Equipe", 1), R("zqr2", "", 2) },
            new[] { E("zq1", "inicio", "zqr1"), E("zq2", "tarefa", "zqr1", "Levantar"), E("zq3", "subprocesso", "zqr1"), E("zq4", "fim", "zqr1") },
            new[] { L("zql1", "zq1", "zq2"), L("zql2", "zq2", "zq3"), L("zql3", "zq3", "zq4") });
        var erros = ErrosDe(comNumero);
        var numeros = (await GeometriaAsync(comNumero)).Numeros;
        Assert.Equal("2.2", numeros["zq3"]);
        Assert.Equal(new[] { "Dê um nome à 2ª raia (de cima para baixo).", "Dê um nome ao subprocesso 2.2." }, erros);

        // Sem prefixo (sem número): pelo vizinho; sem ligação, pela raia
        var semNumero = Def(
            new[] { R("zqr1", "Equipe", 1) },
            new[] { E("zq1", "inicio", "zqr1"), E("zq2", "tarefa", "zqr1"), E("zq3", "fim", "zqr1"), E("zq4", "ligacao", "zqr1") },
            new[] { L("zql1", "zq1", "zq2"), L("zql2", "zq2", "zq3") },
            prefixo: null);
        Assert.Equal(new[]
        {
            "Dê um nome à tarefa que vem depois do início.",
            "Dê um nome à ligação com outro fluxo na raia \"Equipe\", dizendo de onde o fluxo vem ou para onde segue."
        }, ErrosDe(semNumero));
    }

    [Fact]
    public void Decisao_PelaPergunta_OuSemPergunta_PeloVizinho()
    {
        var erros = ErrosDe(Def(
            new[] { R("zqr1", "Equipe", 1) },
            new[] { E("zq1", "inicio", "zqr1"), E("zq2", "tarefa", "zqr1", "Avaliar"), E("zq3", "decisao", "zqr1"), E("zq4", "tarefa", "zqr1", "Ajustar"), E("zq5", "fim", "zqr1") },
            new[] { L("zql1", "zq1", "zq2"), L("zql2", "zq2", "zq3"), L("zql3", "zq3", "zq5", "Sim"), L("zql4", "zq3", "zq4"), L("zql5", "zq4", "zq5") }));
        Assert.Equal(new[] { "A decisão sem pergunta, depois de 2.1: dê um rótulo a cada saída (por exemplo, Sim e Não)." }, erros);

        var comPergunta = ErrosDe(Def(
            new[] { R("zqr1", "Equipe", 1) },
            new[] { E("zq1", "inicio", "zqr1"), E("zq3", "decisao", "zqr1", "Aprovado?"), E("zq5", "fim", "zqr1") },
            new[] { L("zql1", "zq1", "zq3"), L("zql3", "zq3", "zq5", "Sim") }));
        Assert.Equal(new[] { "A decisão \"Aprovado?\" precisa de pelo menos duas saídas (por exemplo, Sim e Não)." }, comPergunta);
    }

    [Fact]
    public void AMesmaLinha_UmaVezSo()
    {
        // Dois paralelos sem nome depois da mesma tarefa, cada um com uma saída: o mesmo problema
        // descrito do mesmo jeito sai uma vez só
        var erros = ErrosDe(Def(
            new[] { R("zqr1", "Equipe", 1) },
            new[]
            {
                E("zq1", "inicio", "zqr1"), E("zq2", "tarefa", "zqr1", "Levantar"), E("zq3", "paralelo", "zqr1"), E("zq4", "paralelo", "zqr1"),
                E("zq5", "tarefa", "zqr1", "Consolidar"), E("zq6", "fim", "zqr1")
            },
            new[] { L("zql1", "zq1", "zq2"), L("zql2", "zq2", "zq3"), L("zql3", "zq2", "zq4"), L("zql4", "zq3", "zq5"), L("zql5", "zq4", "zq5"), L("zql6", "zq5", "zq6") }));
        Assert.Equal(new[] { "O paralelo sem nome, depois de 2.1, precisa abrir caminhos (duas saídas ou mais) ou juntar caminhos (duas entradas ou mais)." }, erros);

        // Duas ligações da mesma tarefa para passos apagados: uma linha
        var apagados = ErrosDe(Def(
            new[] { R("zqr1", "Equipe", 1) },
            new[] { E("zq1", "inicio", "zqr1"), E("zq2", "tarefa", "zqr1", "Levantar"), E("zq3", "fim", "zqr1") },
            new[] { L("zql1", "zq1", "zq2"), L("zql2", "zq2", "zq3"), L("zql3", "zq2", "zqx1"), L("zql4", "zq2", "zqx2") }));
        Assert.Equal(new[] { "A ligação que sai de 2.1 vai para um passo que não existe: apague essa ligação." }, apagados);
    }

    [Fact]
    public async Task EstruturaQuebrada_TudoPeloDesenho_SemIdNemPosicao()
    {
        var definicao = Def(
            new[] { R("zqr1", "Equipe", 1), R("zqr2", "", 2) },
            new[]
            {
                E("zq1", "inicio", "zqr1"), E("zq2", "tarefa", "zqr1"), E("zq3", "subprocesso", "zqr9", "Sub"), E("zq4", "ligacao", "zqr1"),
                E("zq5", "decisao", "zqr1", "Ok?", new[] { "Ata" }), E("zq6", "tarefa", "", "Sem raia", new[] { "  " }), E("zq7", "fim", "zqr1")
            },
            new object[]
            {
                L("zql1", "zq1", "zq2"), L("zql2", "zq2", "zq2"), L("zql3", "zq2", "zqnada"), L("zql4", "zqnada", "zq2"),
                new { Id = "zql5", De = "", Para = "zq2", Rotulo = (string?)null }, L("zql6", "zq2", "zq7"), L("zql7", "zq2", "zq7")
            });
        var erros = ErrosDe(definicao);
        SemIdNemPosicao(erros);

        // Os números são os que o desenho da mesma definição mostra
        var n = (await GeometriaAsync(definicao)).Numeros;
        var tarefa = n["zq2"];
        Assert.Contains("Dê um nome à 2ª raia (de cima para baixo).", erros);
        Assert.Contains($"Dê um nome à tarefa {tarefa}.", erros);
        Assert.Contains($"O subprocesso {n["zq3"]} \"Sub\": a raia escolhida não existe.", erros);
        Assert.Contains("Dê um nome à ligação com outro fluxo na raia \"Equipe\", dizendo de onde o fluxo vem ou para onde segue.", erros);
        Assert.Contains("A decisão \"Ok?\": só tarefas e subprocessos têm artefatos.", erros);
        Assert.Contains($"A tarefa {n["zq6"]} \"Sem raia\": escolha a raia.", erros);
        Assert.Contains($"A tarefa {n["zq6"]} \"Sem raia\": dê um nome a cada artefato.", erros);
        Assert.Contains($"A tarefa {tarefa}, ainda sem nome: uma ligação não pode sair e voltar para o mesmo passo.", erros);
        Assert.Contains($"A ligação que sai de {tarefa} vai para um passo que não existe: apague essa ligação.", erros);
        Assert.Contains($"A ligação que chega a {tarefa} vem de um passo que não existe: apague essa ligação.", erros);
        Assert.Contains($"A ligação que chega a {tarefa} não diz de onde sai.", erros);
        Assert.Contains($"Há duas ligações da tarefa {tarefa}, ainda sem nome, para o fim. Deixe só uma.", erros);
        Assert.Equal(erros.Distinct().Count(), erros.Count);
    }

    [Fact]
    public void ForaDoContrato_OsIdsQueFaltamOuSeRepetem_SemMostrarOId()
    {
        var resultado = PeFluxoDefinicaoLeitor.Ler(ComoJson(Def(
            new[] { R("zqr1", "Equipe", 1) },
            new object[]
            {
                E("zq1", "inicio", "zqr1"), E("zqr1", "tarefa", "zqr1", "Repete a raia"), E("zq a", "tarefa", "zqr1", "Id com espaço"),
                E("zq1", "paralelo", "zqr1"), E("", "fim", "zqr1")
            },
            new object[] { new { Id = "", De = "zq1", Para = "zq9", Rotulo = (string?)null } })));

        Assert.False(resultado.Legivel);
        Assert.Equal(new[]
        {
            "A raia \"Equipe\" e a tarefa \"Repete a raia\" têm o mesmo identificador interno (Id). Cada raia, passo e ligação precisa de um identificador próprio.",
            "A tarefa \"Id com espaço\": o identificador interno (Id) não serve. Use até 40 letras, números, hífen ou sublinhado.",
            "O início e o paralelo sem nome, na raia \"Equipe\", têm o mesmo identificador interno (Id). Cada raia, passo e ligação precisa de um identificador próprio.",
            "O fim: falta o identificador interno (Id).",
            "A ligação que sai do início: falta o identificador interno (Id)."
        }, resultado.Ilegiveis);
        SemIdNemPosicao(resultado.Erros);
    }

    [Fact]
    public async Task ComOsNomesDoOrgao_AMensagemMostraONomeDoDesenho()
    {
        var pdtic = await AbrirSesAsync();
        await PreencherNomesAsync(pdtic.Id);
        var definicao = Def(
            new[] { R("zqr1", "{nomes.comite}", 1) },
            new[] { E("zq1", "inicio", "zqr1"), E("zq2", "paralelo", "zqr1"), E("zq3", "fim", "zqr1") },
            Array.Empty<object>());
        const string Problema = "O paralelo sem nome, na raia \"{0}\", precisa de entrada e de saída.";

        // Na geometria do PDTIC, o nome que o desenho mostra (o do órgão)
        var g = await GeometriaAsync(definicao, pdtic.Id);
        Assert.Equal("Subcomitê Gestor de TIC da Saúde", g.Raias.Single().Nome);
        Assert.Contains(string.Format(Problema, "Subcomitê Gestor de TIC da Saúde"), g.Erros);

        // Na gravação da cópia do órgão também
        var paraGravar = PeFluxoDefinicaoLeitor.Ler(ComoJson(definicao)).Definicao!;
        var ex = await Assert.ThrowsAsync<PeFluxoInvalidoException>(async () =>
            await Fluxos.SalvarAsync(pdtic.Id, "preparacao", Corpo(paraGravar), await Orgao()));
        Assert.Equal((int)ErrorCode.PeFluxoInvalido, ex.Error.Code);
        Assert.Contains(string.Format(Problema, "Subcomitê Gestor de TIC da Saúde"), ex.Erros);

        // Sem o PDTIC (e na validação), os nomes padrão
        Assert.Contains(string.Format(Problema, "Comitê de Governança Digital"), ErrosDe(definicao));
        Assert.Contains(string.Format(Problema, "Comitê de Governança Digital"), (await GeometriaAsync(definicao)).Erros);
    }
}
