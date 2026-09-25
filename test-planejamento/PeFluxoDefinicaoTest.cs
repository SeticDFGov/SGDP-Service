using System.Text.Json;
using api.Planejamento;
using service.Planejamento;
using Xunit;

namespace test.planejamento;

/// <summary>
/// Validação, normalização e numeração da definição de um fluxo (E6): cada regra do contrato
/// (um início, pelo menos um fim, ligações entre elementos que existem, decisão com duas saídas
/// rotuladas, paralelo que abre ou fecha, ligação com outro fluxo só de entrada ou só de saída,
/// nada solto), as mensagens em linguagem simples e o número calculado no servidor.
/// </summary>
public class PeFluxoDefinicaoTest
{
    private static JsonElement J(object valor) => JsonSerializer.SerializeToElement(valor);

    private static PeFluxoDefinicaoLeitor.Resultado Ler(object definicao) => PeFluxoDefinicaoLeitor.Ler(J(definicao));

    private static List<string> Erros(object definicao) => Ler(definicao).Erros;

    private static object Raia(string id = "r1", string nome = "Equipe", int ordem = 1) => new { Id = id, Nome = nome, Ordem = ordem };

    private static object E(string id, string tipo, string nome = "", string raia = "r1", string[]? artefatos = null) =>
        new { Id = id, Tipo = tipo, RaiaId = raia, Nome = nome, Artefatos = artefatos ?? Array.Empty<string>() };

    private static object L(string id, string de, string para, string? rotulo = null) => new { Id = id, De = de, Para = para, Rotulo = rotulo };

    private static object Def(object[] elementos, object[] ligacoes, object[]? raias = null, string? prefixo = "1") =>
        new { PrefixoNumeracao = prefixo, Raias = raias ?? new[] { Raia() }, Elementos = elementos, Ligacoes = ligacoes };

    /// <summary>Início, duas tarefas e o fim, em linha.</summary>
    private static object Linear(string prefixo = "1") => Def(
        new[] { E("i", "inicio"), E("a", "tarefa", "Tarefa A"), E("b", "tarefa", "Tarefa B"), E("f", "fim") },
        new[] { L("l1", "i", "a"), L("l2", "a", "b"), L("l3", "b", "f") },
        prefixo: prefixo);

    [Fact]
    public void Valida_Normaliza_ENumera()
    {
        var resultado = Ler(Def(
            new object[]
            {
                new { Id = "i", Tipo = "inicio", RaiaId = "r2", Nome = (string?)null },
                new { Id = "a", Tipo = "TAREFA", RaiaId = "r2", Nome = "  Levantar   as\nnecessidades ", Numero = "99", Artefatos = new[] { " Lista " } },
                new { Id = "d", Tipo = "decisao", RaiaId = "r1", Nome = "Aprovado?", Numero = "7" },
                E("f", "fim", raia: "r1")
            },
            new[] { L("l1", "i", "a"), L("l2", "a", "d", "  "), L("l3", "d", "f", " Sim "), L("l4", "d", "a", "Não") },
            new[] { Raia("r1", "{nomes.comite}", 5), Raia("r2", "Equipe", 2) },
            prefixo: "3"));

        Assert.True(resultado.Valida, string.Join(" | ", resultado.Erros));
        var d = resultado.Definicao!;
        // Raias na ordem pedida, renumeradas 1, 2
        Assert.Equal(new[] { ("r2", 1), ("r1", 2) }, d.Raias.Select(r => (r.Id, r.Ordem)));
        var a = d.Elementos.Single(e => e.Id == "a");
        Assert.Equal("tarefa", a.Tipo);
        Assert.Equal("Levantar as necessidades", a.Nome);
        Assert.Equal(new[] { "Lista" }, a.Artefatos);
        // O número vem do servidor (o que o front mandou é ignorado); só a tarefa e o subprocesso têm
        Assert.Equal("3.1", a.Numero);
        Assert.Null(d.Elementos.Single(e => e.Id == "d").Numero);
        Assert.Equal(string.Empty, d.Elementos.Single(e => e.Id == "i").Nome);
        // Rótulo em branco vira nulo; o resto sem espaço sobrando
        Assert.Null(d.Ligacoes.Single(l => l.Id == "l2").Rotulo);
        Assert.Equal("Sim", d.Ligacoes.Single(l => l.Id == "l3").Rotulo);
    }

    [Fact]
    public void SemPrefixo_NadaNumerado()
    {
        var resultado = Ler(Linear(prefixo: ""));
        Assert.True(resultado.Valida);
        Assert.Null(resultado.Definicao!.PrefixoNumeracao);
        Assert.All(resultado.Definicao.Elementos, e => Assert.Null(e.Numero));
    }

    [Fact]
    public void Numeracao_PelaOrdemDoDesenho_ParalelosDeCimaParaBaixo()
    {
        // As tarefas vêm fora de ordem na lista: o número segue as colunas e as linhas
        var resultado = Ler(Def(
            new[]
            {
                E("i", "inicio"), E("z", "tarefa", "Depois de juntar"), E("p1", "paralelo"), E("c1", "tarefa", "Caminho 1"),
                E("c2", "tarefa", "Caminho 2"), E("c3", "tarefa", "Caminho 3"), E("p2", "paralelo"), E("a", "tarefa", "Primeira"), E("f", "fim")
            },
            new[]
            {
                L("l1", "i", "a"), L("l2", "a", "p1"), L("l3", "p1", "c1"), L("l4", "p1", "c2"), L("l5", "p1", "c3"),
                L("l6", "c1", "p2"), L("l7", "c2", "p2"), L("l8", "c3", "p2"), L("l9", "p2", "z"), L("l10", "z", "f")
            },
            prefixo: "2"));

        Assert.True(resultado.Valida, string.Join(" | ", resultado.Erros));
        var numeros = resultado.Definicao!.Elementos.Where(e => e.Numero != null).ToDictionary(e => e.Id, e => e.Numero);
        Assert.Equal("2.1", numeros["a"]);
        Assert.Equal("2.2", numeros["c1"]);
        Assert.Equal("2.3", numeros["c2"]);
        Assert.Equal("2.4", numeros["c3"]);
        Assert.Equal("2.5", numeros["z"]);
    }

    // ── Estrutura ─────────────────────────────────────────────────────────────

    [Fact]
    public void Estrutura_ObjetoELista()
    {
        Assert.Contains("Envie a definição do fluxo como um objeto", Assert.Single(PeFluxoDefinicaoLeitor.Ler(null).Erros));
        Assert.Contains("Envie a definição do fluxo como um objeto", Assert.Single(PeFluxoDefinicaoLeitor.Ler(J(new[] { 1 })).Erros));
        var erros = Erros(new { Raias = "x", Elementos = (object?)null });
        Assert.Contains("Raias precisa ser uma lista.", erros);
        Assert.Contains("Falta a lista Elementos na definição do fluxo.", erros);
        Assert.Contains("Falta a lista Ligacoes na definição do fluxo.", erros);
        Assert.Contains("Inclua pelo menos uma raia (quem faz os passos).", Erros(Def(Array.Empty<object>(), Array.Empty<object>(), Array.Empty<object>())));
    }

    [Fact]
    public void Ids_CurtosValidosEUnicosNoFluxoInteiro()
    {
        // O mesmo id numa raia e num passo não vale ("únicos no fluxo")
        var erros = Erros(Def(
            new[] { E("r1", "inicio"), E("a b", "tarefa", "X"), E("f", "fim") },
            new[] { L("l1", "r1", "f"), new { Id = "", De = "r1", Para = "f", Rotulo = (string?)null } }));
        // Desde a F1 (D11), sem o id: os itens como o desenho os mostra
        Assert.Contains("A raia \"Equipe\" e o início têm o mesmo identificador interno (Id). Cada raia, passo e ligação precisa de um identificador próprio.", erros);
        Assert.Contains("A tarefa \"X\": o identificador interno (Id) não serve. Use até 40 letras, números, hífen ou sublinhado.", erros);
        Assert.Contains("A ligação que chega ao fim: falta o identificador interno (Id).", erros);
        // As duas ligações do início repetido (que perdeu o id) dão a mesma mensagem: uma vez só
        Assert.Single(erros, e => e == "A ligação que chega ao fim vem de um passo que não existe: apague essa ligação.");
        Assert.DoesNotContain(erros, e => e.Contains("\"r1\"") || e.Contains("\"a b\""));
    }

    [Fact]
    public void Raias_ComNome()
    {
        var erros = Erros(Def(new[] { E("i", "inicio") }, Array.Empty<object>(), new[] { Raia("r1", "  ") }));
        Assert.Contains("Dê um nome à 1ª raia (de cima para baixo).", erros);
        var longa = Erros(Def(new[] { E("i", "inicio") }, Array.Empty<object>(), new[] { Raia("r1", new string('a', 121)) }));
        Assert.Contains(longa, e => e.Contains("passa de 120 caracteres"));
    }

    [Fact]
    public void Elementos_TipoRaiaNomeEArtefatos()
    {
        var erros = Erros(Def(
            new object[]
            {
                E("i", "inicio"), E("x", "evento", "Coisa"), E("a", "tarefa", "", "r1"), E("s", "subprocesso", "Sub", "r9"),
                E("k", "ligacao", ""), E("d", "decisao", "Ok?", artefatos: new[] { "Ata" }),
                E("t", "tarefa", "Muitos", artefatos: new[] { "1", "2", "3", "4", "5", "6", "7" }),
                new { Id = "u", Tipo = "tarefa", RaiaId = "", Nome = "Sem raia", Artefatos = new[] { "  " } },
                E("f", "fim")
            },
            Array.Empty<object>()));

        // Os passos soltos ficam no fim da raia, na ordem da lista: as tarefas e o subprocesso
        // recebem 1.1 a 1.4, como no desenho (F1, D11: o número, não a posição na lista)
        Assert.Contains(erros, e => e.StartsWith("O passo \"Coisa\": o tipo \"evento\" não existe."));
        Assert.Contains("Dê um nome à tarefa 1.1.", erros);
        Assert.Contains("O subprocesso 1.2 \"Sub\": a raia escolhida não existe.", erros);
        Assert.Contains("Dê um nome à ligação com outro fluxo na raia \"Equipe\", dizendo de onde o fluxo vem ou para onde segue.", erros);
        Assert.Contains("A decisão \"Ok?\": só tarefas e subprocessos têm artefatos.", erros);
        Assert.Contains("A tarefa 1.3 \"Muitos\": tem artefatos demais (até 6).", erros);
        Assert.Contains("A tarefa 1.4 \"Sem raia\": escolha a raia.", erros);
        Assert.Contains("A tarefa 1.4 \"Sem raia\": dê um nome a cada artefato.", erros);
    }

    [Fact]
    public void Ligacoes_EntreElementosQueExistem_SemLacoProprioNemRepeticao()
    {
        var erros = Erros(Def(
            new[] { E("i", "inicio"), E("a", "tarefa", "A"), E("f", "fim") },
            new[]
            {
                L("l1", "i", "a"), L("l2", "a", "f"), L("l3", "a", "f"), L("l4", "a", "a"), L("l5", "a", "nada"),
                L("l6", "nada", "a"), new { Id = "l7", De = "", Para = "a", Rotulo = (string?)null }, L("l8", "i", "f", new string('r', 61))
            }));

        Assert.Contains("Há duas ligações da tarefa 1.1 \"A\" para o fim. Deixe só uma.", erros);
        Assert.Contains("A tarefa 1.1 \"A\": uma ligação não pode sair e voltar para o mesmo passo.", erros);
        // A ligação pelas pontas que existem (não pela posição na lista)
        Assert.Contains("A ligação que sai de 1.1 vai para um passo que não existe: apague essa ligação.", erros);
        Assert.Contains("A ligação que chega a 1.1 vem de um passo que não existe: apague essa ligação.", erros);
        Assert.Contains("A ligação que chega a 1.1 não diz de onde sai.", erros);
        Assert.Contains(erros, e => e.StartsWith("O rótulo \"rrr") && e.EndsWith("\" da ligação do início para o fim passa de 60 caracteres."));
    }

    [Fact]
    public void Prefixo_SoNumeros()
    {
        Assert.Contains("O prefixo da numeração usa só números, como 1 ou 4 (ou fica vazio, sem numeração).", Erros(Linear("A")));
        Assert.True(Ler(Linear("4")).Valida);
        Assert.True(Ler(Linear("2.1")).Valida);
        Assert.Equal("2.1.1", Ler(Linear("2.1")).Definicao!.Elementos.Single(e => e.Id == "a").Numero);
    }

    // ── Regras do grafo ───────────────────────────────────────────────────────

    [Fact]
    public void InicioEFim()
    {
        Assert.Contains("O fluxo precisa de um início.", Erros(Def(new[] { E("a", "tarefa", "A"), E("f", "fim") }, new[] { L("l1", "a", "f") })));
        Assert.Contains("O fluxo tem 2 inícios. Deixe só um.", Erros(Def(
            new[] { E("i", "inicio"), E("j", "inicio"), E("f", "fim") }, new[] { L("l1", "i", "f"), L("l2", "j", "f") })));
        Assert.Contains("O fluxo precisa de pelo menos um fim.", Erros(Def(new[] { E("i", "inicio"), E("a", "tarefa", "A") }, new[] { L("l1", "i", "a") })));

        var erros = Erros(Def(
            new[] { E("i", "inicio"), E("a", "tarefa", "A"), E("f", "fim") },
            new[] { L("l1", "i", "a"), L("l2", "a", "i"), L("l3", "a", "f"), L("l4", "f", "a") }));
        Assert.Contains("O início não recebe ligação: nada vem antes dele.", erros);
        Assert.Contains("O fim não tem saída: é onde o fluxo termina.", erros);

        Assert.Contains("Ligue o início ao primeiro passo do fluxo.", Erros(Def(new[] { E("i", "inicio"), E("f", "fim") }, Array.Empty<object>())));
    }

    [Fact]
    public void Tarefa_ComEntradaESaida()
    {
        var erros = Erros(Def(
            new[] { E("i", "inicio"), E("a", "tarefa", "A"), E("s", "subprocesso", "Solto"), E("b", "tarefa", "Sem saída"), E("f", "fim") },
            new[] { L("l1", "i", "a"), L("l2", "a", "f"), L("l3", "a", "b"), L("l4", "s", "f") }));
        Assert.Contains("O subprocesso 1.2 \"Solto\" não tem de onde vir: diga o que vem antes dele.", erros);
        Assert.Contains("A tarefa 1.3 \"Sem saída\" não leva a lugar nenhum: diga o que vem depois.", erros);
    }

    [Fact]
    public void Decisao_DuasSaidasRotuladas_SemRepetir()
    {
        var umaSaida = Erros(Def(
            new[] { E("i", "inicio"), E("d", "decisao", "Aprovado?"), E("f", "fim") },
            new[] { L("l1", "i", "d"), L("l2", "d", "f", "Sim") }));
        Assert.Contains("A decisão \"Aprovado?\" precisa de pelo menos duas saídas (por exemplo, Sim e Não).", umaSaida);

        var semRotulo = Erros(Def(
            new[] { E("i", "inicio"), E("d", "decisao"), E("a", "tarefa", "A"), E("f", "fim") },
            new[] { L("l1", "i", "d"), L("l2", "d", "f", "Sim"), L("l3", "d", "a"), L("l4", "a", "f") }));
        // A decisão sem pergunta, pelo vizinho no desenho (não pelo id)
        Assert.Contains("A decisão sem pergunta, depois do início: dê um rótulo a cada saída (por exemplo, Sim e Não).", semRotulo);

        var repetido = Erros(Def(
            new[] { E("i", "inicio"), E("d", "decisao", "Ok?"), E("a", "tarefa", "A"), E("f", "fim") },
            new[] { L("l1", "i", "d"), L("l2", "d", "f", "Sim"), L("l3", "d", "a", "sim"), L("l4", "a", "f") }));
        Assert.Contains("A decisão \"Ok?\" tem duas saídas com o rótulo \"Sim\".", repetido);
    }

    [Fact]
    public void Paralelo_AbreOuFecha()
    {
        var erros = Erros(Def(
            new[] { E("i", "inicio"), E("p", "paralelo"), E("f", "fim") },
            new[] { L("l1", "i", "p"), L("l2", "p", "f") }));
        Assert.Contains("O paralelo sem nome, depois do início, precisa abrir caminhos (duas saídas ou mais) ou juntar caminhos (duas entradas ou mais).", erros);
    }

    [Fact]
    public void LigacaoComOutroFluxo_SoEntradaOuSoSaida()
    {
        var dosDois = Erros(Def(
            new[] { E("i", "inicio"), E("k", "ligacao", "Diagnóstico"), E("f", "fim") },
            new[] { L("l1", "i", "k"), L("l2", "k", "f") }));
        Assert.Contains("A ligação com outro fluxo \"Diagnóstico\" só recebe (o fluxo segue em outro) ou só sai (o fluxo vem de outro), não os dois.", dosDois);

        // Entrada vinda de outro fluxo (só sai) e saída para outro fluxo (só recebe) valem
        var resultado = Ler(Def(
            new[] { E("i", "inicio"), E("k1", "ligacao", "Vem da revisão"), E("a", "tarefa", "A"), E("d", "decisao", "Revisar?"),
                    E("k2", "ligacao", "Segue no diagnóstico"), E("f", "fim") },
            new[] { L("l1", "i", "a"), L("l2", "k1", "a"), L("l3", "a", "d"), L("l4", "d", "f", "Não"), L("l5", "d", "k2", "Sim") }));
        Assert.True(resultado.Valida, string.Join(" | ", resultado.Erros));
    }

    [Fact]
    public void NadaSolto_AlcancadoDoInicio_EChegandoAoFim()
    {
        // Um laço de duas tarefas que nada alcança
        var solto = Erros(Def(
            new[] { E("i", "inicio"), E("a", "tarefa", "A"), E("f", "fim"), E("x", "tarefa", "Ilha 1"), E("y", "tarefa", "Ilha 2") },
            new[] { L("l1", "i", "a"), L("l2", "a", "f"), L("l3", "x", "y"), L("l4", "y", "x") }));
        // A ilha começa na primeira coluna do desenho, embaixo do início: ganha o 1.1
        Assert.Contains("A tarefa 1.1 \"Ilha 1\" não é alcançada a partir do início: ligue-a ao caminho do fluxo.", solto);

        // Um laço de onde não se sai
        var semSaida = Erros(Def(
            new[] { E("i", "inicio"), E("a", "tarefa", "A"), E("b", "tarefa", "B"), E("c", "tarefa", "C"), E("d", "decisao", "Segue?"), E("f", "fim") },
            new[] { L("l1", "i", "a"), L("l2", "a", "d"), L("l3", "d", "f", "Sim"), L("l4", "d", "b", "Não"), L("l5", "b", "c"), L("l6", "c", "b") }));
        Assert.Contains("A tarefa 1.2 \"B\" não leva a nenhum fim: todo caminho precisa terminar num fim.", semSaida);
    }

    [Fact]
    public void ErrosDemais_ALista_ParaNoMaximo()
    {
        var elementos = Enumerable.Range(1, 40).Select(i => E($"t{i}", "tarefa", $"T{i}")).Prepend(E("i", "inicio")).Append(E("f", "fim")).ToArray();
        var erros = Erros(Def(elementos, new[] { L("l1", "i", "f") }));
        Assert.Equal(PeFluxoDefinicaoLeitor.MaximoErros + 1, erros.Count);
        Assert.StartsWith("E mais ", erros[^1]);
    }

    [Fact]
    public void Ilegiveis_SoForaDoContratoOuAlemDosLimites()
    {
        // Estado de edição (bloco sem nome e sem ligação, raia que sumiu, ligação para o que não
        // existe): erros, mas legível (a geometria da E9 desenha)
        var edicao = Ler(Def(
            new[] { E("i", "inicio"), E("a", "tarefa", "A"), E("b", "tarefa", ""), E("c", "tarefa", "C", "r9"), E("f", "fim") },
            new[] { L("l1", "i", "a"), L("l2", "a", "f"), L("l3", "a", "sumiu"), L("l4", "a", "a") }));
        Assert.NotEmpty(edicao.Erros);
        Assert.Empty(edicao.Ilegiveis);
        Assert.True(edicao.Legivel);
        Assert.False(edicao.Valida);

        // Fora do contrato ou além dos limites: ilegível, com os mesmos textos da validação
        var fora = Ler(Def(
            new object[] { E("i", "inicio"), E("i", "tarefa", "Repetido"), E("x", "evento", "Coisa"), E("n", "tarefa", new string('n', 201)), 7 },
            new object[] { L("l1", "i", "x", new string('r', 61)), "ligação" }));
        Assert.False(fora.Legivel);
        Assert.Contains("O início e a tarefa \"Repetido\" têm o mesmo identificador interno (Id). Cada raia, passo e ligação precisa de um identificador próprio.", fora.Ilegiveis);
        Assert.Contains(fora.Ilegiveis, e => e.StartsWith("O passo \"Coisa\": o tipo \"evento\" não existe."));
        Assert.Contains(fora.Ilegiveis, e => e.EndsWith("o nome passa de 200 caracteres."));
        // O item que não é objeto só tem a posição na lista (é erro de quem monta o JSON, não da tela)
        Assert.Contains("O item 5 da lista de passos precisa ser um objeto com Id, Tipo, RaiaId e Nome.", fora.Ilegiveis);
        Assert.Contains(fora.Ilegiveis, e => e.EndsWith("\" da ligação do início para o passo \"Coisa\" passa de 60 caracteres."));
        Assert.Contains("O item 2 da lista de ligações precisa ser um objeto com Id, De e Para.", fora.Ilegiveis);
        Assert.All(fora.Ilegiveis, e => Assert.Contains(e, fora.Erros));
        Assert.Contains("Envie a definição do fluxo como um objeto com Raias, Elementos e Ligacoes.", PeFluxoDefinicaoLeitor.Ler(null).Ilegiveis);
        Assert.Contains("Raias precisa ser uma lista.", PeFluxoDefinicaoLeitor.Ler(J(new { Raias = 1, Elementos = Array.Empty<object>(), Ligacoes = Array.Empty<object>() })).Ilegiveis);
    }

    [Fact]
    public void LerValida_LancaComOsErros()
    {
        var ex = Assert.Throws<PeFluxoInvalidoException>(() => PeFluxoDefinicaoLeitor.LerValida(J(Def(new[] { E("i", "inicio") }, Array.Empty<object>()))));
        Assert.Equal((int)ErrorCode.PeFluxoInvalido, ex.Error.Code);
        Assert.Contains("O fluxo precisa de pelo menos um fim.", ex.Erros);
    }

    [Fact]
    public void DoBanco_RenumeraETolera()
    {
        var guardada = PeFluxoDefinicaoLeitor.ParaJson(Ler(Linear("5")).Definicao!).Replace("\"5.1\"", "\"9.9\"");
        var lida = PeFluxoDefinicaoLeitor.DoBanco(guardada);
        Assert.Equal(new[] { "5.1", "5.2" }, lida.Elementos.Where(e => e.Numero != null).Select(e => e.Numero));
        // JSON que não é definição vira uma definição vazia (o desenho não quebra)
        var vazia = PeFluxoDefinicaoLeitor.DoBanco("[1,2]");
        Assert.Empty(vazia.Elementos);
        Assert.Empty(PeFluxoDefinicaoLeitor.DoBanco(null).Raias);
    }
}
