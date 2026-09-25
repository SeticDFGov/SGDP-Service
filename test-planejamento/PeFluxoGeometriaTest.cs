using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using api.Planejamento;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Models.Planejamento;
using service;
using service.Planejamento;
using Xunit;

namespace test.planejamento;

/// <summary>
/// A geometria do desenho automático (E9), que o editor visual recebe:
/// <list type="bullet">
/// <item>cada fluxo do guia: sem sobreposição, tudo dentro da raia, ligações ortogonais saindo e
/// chegando na borda das formas, avanço para a direita, retorno para trás e os números da
/// definição;</item>
/// <item>uma fonte só: o SVG sai da geometria (inclusive do JSON que o front recebe) e é o
/// mesmo, byte a byte, que a E8 desenhava para os fluxos do guia;</item>
/// <item>definição incompleta (bloco solto, decisão com uma saída, ligação para o que não
/// existe, sem nome, raia que sumiu, só as raias) desenhada, com os problemas em Erros; 400 só
/// para a ilegível;</item>
/// <item>tempo, permissões (as da leitura dos fluxos), nada gravado, as rotas e o JSON do
/// contrato; e definições ao acaso sempre desenhadas sem sobreposição.</item>
/// </list>
/// </summary>
public class PeFluxoGeometriaTest : PeFluxoTestBase
{
    public static IEnumerable<object[]> ChavesDoGuia => PeFluxoSeed.Ler().Fluxos.Select(f => new object[] { f.Chave });

    /// <summary>
    /// SHA-256 do SVG de cada fluxo do guia com os nomes padrão (GET modelo/fluxos/{chave}/svg),
    /// gerado com o código da E8, antes de o SVG passar a sair da geometria. Mudou o desenho de
    /// propósito? Grave os SVGs novos (variável PE_EXEMPLO_FLUXOS), confira e troque aqui.
    /// </summary>
    private static readonly Dictionary<string, string> SvgDaE8 = new()
    {
        ["macroprocesso"] = "e054a305d4074393c1b822fa64a69bf21fdc05fc18b594164b7885dc78a3d11e",
        ["elaboracao"] = "4b33549006c4011ac348942ae4dfd3458dd20f1f6b0788ccc347a08375d13267",
        ["preparacao"] = "7eb726647d16f05dc02705a198fd3aa9d95a3897394f7d5054754ee2f239f4d6",
        ["diagnostico"] = "8c81c22e13fce875d9d85d5413a79b0f2acb37e42f2e656adf7d3461f7a206ed",
        ["planejamento"] = "08317d03dab32950f728dcd814abcc272c1ab4a110752d97ecd13dccd05d0d47",
        ["acompanhamento"] = "e9b34a4b02e8886ab2f669868f91b5fe04df94681a618b68f626e25427a89b27",
        ["planejamento_acompanhamento"] = "31789943f7e23c4929d34b157976d1dc28627f8e702b6f0cd2f99c7834c2b8e8",
        ["monitoramento"] = "19ffb99ef6304ef5cb1a26f47e93fd704340d14370cd3d4553ea509cc467a30c",
        ["avaliacao_intermediaria"] = "03f88eb4f31606ad5d8b9bd95075c51666e14fb855c3bd5ff6990d237bc3188b",
        ["avaliacao_final"] = "35a935d78e8156cf373aa0d7b180e12a6c65bbdcb66175e5ad846b2eba4f8db4"
    };

    // Como a API escreve o JSON (Program.cs): PascalCase
    private static readonly JsonSerializerOptions ComoAApi = new() { PropertyNamingPolicy = null };

    private static string JsonDe(PeFluxoGeometria g) => JsonSerializer.Serialize(g, ComoAApi);

    private static string SvgDe(PeFluxoGeometria g) => PeFluxoSvg.Escrever(g, PeFluxoFonte.Regular, PeFluxoFonte.Negrito);

    private static PeFluxoDesenhoDTO Previa(PeFluxoDefinicao definicao, long? pdticId = null, string? nome = null) =>
        new() { Definicao = ComoJson(definicao), PdticId = pdticId, Nome = nome };

    private async Task<PeFluxoGeometria> GeometriaAsync(PeFluxoDefinicao definicao, string? nome = "Fluxo de teste") =>
        await Fluxos.GeometriaAsync(Previa(definicao, nome: nome), await Orgao());

    private static PeFluxoElemento Elemento(string id, string tipo, string raia, string nome = "") =>
        new() { Id = id, Tipo = tipo, RaiaId = raia, Nome = nome };

    private static PeFluxoGeometriaElemento Em(PeFluxoGeometria g, string id) => g.Elementos.Single(e => e.Id == id);

    private static PeFluxoDesenho.Retangulo Caixa(PeFluxoGeometriaElemento e) => new(e.X, e.Y, e.Largura, e.Altura);

    // ── Conferências da geometria ─────────────────────────────────────────────

    /// <summary>As formas e os documentos dos artefatos não se sobrepõem.</summary>
    private static void SemSobreposicao(PeFluxoGeometria g, string contexto = "")
    {
        var areas = g.Elementos.Select(e => (Nome: e.Id, Area: Caixa(e)))
            .Concat(g.Artefatos.Select(a => (Nome: $"{a.ElementoId}:doc{a.Indice}", Area: new PeFluxoDesenho.Retangulo(a.X, a.Y, a.Largura, a.Altura))))
            .ToList();
        for (var i = 0; i < areas.Count; i++)
            for (var j = i + 1; j < areas.Count; j++)
                Assert.False(areas[i].Area.Sobrepoe(areas[j].Area), $"{contexto}: {areas[i].Nome} sobre {areas[j].Nome}");
    }

    /// <summary>Cada elemento dentro da faixa da raia dele, depois do cabeçalho; as raias uma embaixo da outra.</summary>
    private static void DentroDasRaias(PeFluxoGeometria g, string contexto = "")
    {
        for (var i = 1; i < g.Raias.Count; i++)
            Assert.Equal(g.Raias[i - 1].Y + g.Raias[i - 1].Altura, g.Raias[i].Y, 6);
        foreach (var e in g.Elementos)
        {
            var raia = g.Raias.Single(r => r.Id == e.RaiaId);
            var dentro = new PeFluxoDesenho.Retangulo(raia.X + raia.LarguraCabecalho, raia.Y, raia.Largura - raia.LarguraCabecalho, raia.Altura);
            Assert.True(dentro.Contem(Caixa(e)), $"{contexto}: {e.Id} fora da raia {raia.Id}");
        }
        Assert.All(g.Raias, r => Assert.True(r.X + r.Largura <= g.Largura && r.Y + r.Altura <= g.Altura, $"{contexto}: raia {r.Id} fora do desenho"));
    }

    private static bool NaBorda(PeFluxoGeometriaPonto p, PeFluxoDesenho.Retangulo r)
    {
        const double folga = 0.01;
        var naVertical = (Math.Abs(p.X - r.X) < folga || Math.Abs(p.X - r.Direita) < folga) && p.Y >= r.Y - folga && p.Y <= r.Base + folga;
        var naHorizontal = (Math.Abs(p.Y - r.Y) < folga || Math.Abs(p.Y - r.Base) < folga) && p.X >= r.X - folga && p.X <= r.Direita + folga;
        return naVertical || naHorizontal;
    }

    /// <summary>Ligações só na horizontal e na vertical, da borda de quem sai à borda de quem recebe.</summary>
    private static void LigacoesNasBordas(PeFluxoGeometria g, string contexto = "")
    {
        var porId = g.Elementos.ToDictionary(e => e.Id);
        foreach (var l in g.Ligacoes)
        {
            Assert.True(l.Pontos.Count >= 2, $"{contexto}: {l.Id} sem pontos");
            Assert.True(NaBorda(l.Pontos[0], Caixa(porId[l.De])), $"{contexto}: {l.Id} não sai da borda de {l.De}");
            Assert.True(NaBorda(l.Pontos[^1], Caixa(porId[l.Para])), $"{contexto}: {l.Id} não chega na borda de {l.Para}");
            for (var i = 1; i < l.Pontos.Count; i++)
            {
                var (p, q) = (l.Pontos[i - 1], l.Pontos[i]);
                Assert.True(Math.Abs(p.X - q.X) < 0.5 || Math.Abs(p.Y - q.Y) < 0.5, $"{contexto}: {l.Id} com trecho torto");
            }
            Assert.Equal(l.Rotulo != null, l.RotuloX != null && l.RotuloY != null && l.RotuloArea != null);
        }
    }

    /// <summary>Nenhum trecho de ligação passa por dentro de uma forma que não é a de saída nem a de chegada.</summary>
    private static void LigacoesSemAtravessar(PeFluxoGeometria g, string contexto = "")
    {
        foreach (var l in g.Ligacoes)
            foreach (var e in g.Elementos.Where(e => e.Id != l.De && e.Id != l.Para))
            {
                var r = Caixa(e).Inflar(-1);
                for (var i = 1; i < l.Pontos.Count; i++)
                {
                    var (p, q) = (l.Pontos[i - 1], l.Pontos[i]);
                    var trecho = new PeFluxoDesenho.Retangulo(Math.Min(p.X, q.X), Math.Min(p.Y, q.Y), Math.Abs(p.X - q.X), Math.Abs(p.Y - q.Y));
                    Assert.False(trecho.Sobrepoe(r), $"{contexto}: a ligação {l.Id} atravessa {e.Id}");
                }
            }
    }

    // ── Os fluxos do guia ─────────────────────────────────────────────────────

    [Theory]
    [MemberData(nameof(ChavesDoGuia))]
    public async Task FluxoDoGuia_GeometriaSemSobreposicao_DentroDasRaias_ComOsNumeros(string chave)
    {
        var g = await Fluxos.GeometriaDoModeloAsync(chave, await ContextoDe(UserPeSgdi));
        var definicao = DefinicaoDoModelo(chave);

        Assert.Empty(g.Erros);
        Assert.Equal(ModeloNoBanco(chave).Nome, g.Titulo);
        Assert.Equal(definicao.Elementos.Select(e => e.Id), g.Elementos.Select(e => e.Id));
        Assert.Equal(definicao.Ligacoes.Select(l => l.Id).OrderBy(x => x), g.Ligacoes.Select(l => l.Id).OrderBy(x => x));
        Assert.Equal(definicao.Raias.Select(r => (r.Id, r.Nome)), g.Raias.Select(r => (r.Id, r.NomeOriginal)));
        Assert.All(g.Elementos, e => Assert.False(e.Solto));
        // Os números são os da definição (a mesma regra do servidor)
        Assert.Equal(definicao.Elementos.Where(e => e.Numero != null).ToDictionary(e => e.Id, e => e.Numero!), g.Numeros);
        Assert.All(g.Elementos, e => Assert.Equal(g.Numeros.GetValueOrDefault(e.Id), e.Numero));

        SemSobreposicao(g, chave);
        DentroDasRaias(g, chave);
        LigacoesNasBordas(g, chave);
        LigacoesSemAtravessar(g, chave);

        // Avanço para uma coluna à direita; retorno para trás (ou na mesma coluna)
        var porId = g.Elementos.ToDictionary(e => e.Id);
        foreach (var l in g.Ligacoes)
            Assert.True(l.Retorno ? porId[l.Para].Camada <= porId[l.De].Camada : porId[l.Para].Camada > porId[l.De].Camada, $"{chave}: {l.Id}");

        // O nome da tarefa quebrado como no SVG, dentro da caixa (a linha de base e a altura da letra)
        foreach (var e in g.Elementos.Where(e => PeDominios.TipoElementoFluxo.EhAtividade(e.Tipo)))
        {
            Assert.Equal("dentro", e.PosicaoNome);
            Assert.Equal(e.Nome, string.Join(" ", e.Linhas));
            Assert.Equal(e.Linhas, g.Textos.Where(t => t.Dono == e.Id && !t.Negrito).Select(t => t.Texto));
            foreach (var t in g.Textos.Where(t => t.Dono == e.Id))
                Assert.True(Caixa(e).Contem(new PeFluxoDesenho.Retangulo(t.X, t.Y - t.Tamanho * 0.78, t.Largura, t.Tamanho), 0.5), $"{chave}: \"{t.Texto}\" sai de {e.Id}");
        }
        // Os artefatos embaixo da tarefa dona, na ordem
        foreach (var grupo in g.Artefatos.GroupBy(a => a.ElementoId))
        {
            var dono = porId[grupo.Key];
            Assert.Equal(definicao.Elementos.Single(e => e.Id == grupo.Key).Artefatos, grupo.OrderBy(a => a.Indice).Select(a => a.Nome));
            Assert.All(grupo, a => Assert.True(a.Y > dono.Y + dono.Altura, $"{chave}: artefato de {dono.Id} fora do lugar"));
        }
    }

    [Theory]
    [MemberData(nameof(ChavesDoGuia))]
    public async Task FluxoDoGuia_SvgIgualAoDaE8_ByteAByte(string chave)
    {
        var svg = await Fluxos.SvgDoModeloAsync(chave, await ContextoDe(UserPeSgdi));
        var pasta = PastaDeExemplo();
        if (pasta != null) File.WriteAllText(Path.Combine(pasta, $"e9-{chave}.svg"), svg, new UTF8Encoding(false));
        Assert.Equal(SvgDaE8[chave], Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(svg))).ToLowerInvariant());
    }

    [Theory]
    [MemberData(nameof(ChavesDoGuia))]
    public async Task Svg_SaiDaGeometria_InclusiveDoJsonQueOFrontRecebe(string chave)
    {
        var ctx = await ContextoDe(UserPeSgdi);
        var svg = await Fluxos.SvgDoModeloAsync(chave, ctx);
        var geometria = await Fluxos.GeometriaDoModeloAsync(chave, ctx);
        Assert.Equal(svg, SvgDe(geometria));

        // Do JSON da resposta sai o mesmo SVG: o front tem tudo o que o PDF usa
        var doJson = JsonSerializer.Deserialize<PeFluxoGeometria>(JsonDe(geometria), ComoAApi)!;
        Assert.Equal(svg, SvgDe(doJson));
    }

    [Fact]
    public async Task DoPdtic_ACopiaDoOrgaoComOsNomes_OMesmoDesenhoDoSvgEDaPrevia()
    {
        var pdtic = await AbrirSesAsync();
        await PreencherNomesAsync(pdtic.Id);
        var definicao = DefinicaoDoModelo("planejamento");
        definicao.Elementos.Single(e => e.Numero == "3.6").Nome = "Listar os fatores críticos de sucesso";
        await Fluxos.SalvarAsync(pdtic.Id, "planejamento", Corpo(definicao, "Planejamento da Saúde"), await Orgao());

        var consulta = await ContextoDe(UserConsultaSes);
        var g = await Fluxos.GeometriaDoPdticAsync(pdtic.Id, "planejamento", consulta);
        Assert.Empty(g.Erros);
        Assert.Equal("Planejamento da Saúde", g.Titulo);
        Assert.NotNull(g.FaixaTitulo);
        Assert.Equal("Planejamento da Saúde", string.Join(" ", g.FaixaTitulo!.Linhas));
        var comite = g.Raias.Single(r => r.NomeOriginal == "{nomes.comite}");
        Assert.Equal("Subcomitê Gestor de TIC da Saúde", comite.Nome);
        Assert.Equal(comite.Nome, string.Join(" ", comite.Linhas));
        Assert.Equal("Secretária de Estado de Saúde", g.Raias.Single(r => r.NomeOriginal == "{nomes.autoridade_cargo}").Nome);
        Assert.Contains(g.Elementos, e => e.Nome == "Listar os fatores críticos de sucesso" && e.Numero == "3.6");
        Assert.StartsWith("Raias: Secretária de Estado de Saúde, Subcomitê Gestor de TIC da Saúde", g.Descricao[0]);

        // O SVG gravado (prévia e PDF) sai desta geometria
        Assert.Equal(await Fluxos.SvgAsync(pdtic.Id, "planejamento", consulta), SvgDe(g));

        // A prévia da mesma definição, com o PDTIC e o nome, dá a mesma geometria (a tela do
        // editor é o PDF); sem o nome, falta a faixa do fluxo
        var salvo = await Fluxos.ObterAsync(pdtic.Id, "planejamento", consulta);
        var previa = await Fluxos.GeometriaAsync(Previa(salvo.Definicao, pdtic.Id, salvo.Nome), await Orgao());
        Assert.Equal(JsonDe(g), JsonDe(previa));
        Assert.Null((await Fluxos.GeometriaAsync(Previa(salvo.Definicao, pdtic.Id), await Orgao())).FaixaTitulo);
        // Sem o PDTIC, os nomes padrão
        var padrao = await Fluxos.GeometriaAsync(Previa(salvo.Definicao, nome: salvo.Nome), await Orgao());
        Assert.Equal("Comitê de Governança Digital", padrao.Raias.Single(r => r.NomeOriginal == "{nomes.comite}").Nome);
    }

    // ── Definição incompleta (durante a edição) ───────────────────────────────

    [Fact]
    public async Task BlocoNovoSemLigacao_NoFimDaRaia_Solto_ComOsErrosDaValidacao()
    {
        var definicao = Simples();
        var antes = await GeometriaAsync(definicao);
        definicao.Elementos.Add(Elemento("novo", "tarefa", "r2", "Nova tarefa"));
        var g = await GeometriaAsync(definicao);

        var novo = Em(g, "novo");
        Assert.True(novo.Solto);
        Assert.Equal("r2", novo.RaiaId);
        Assert.Equal(antes.Elementos.Max(e => e.Camada) + 1, novo.Camada);
        Assert.Equal(0, novo.Faixa);
        Assert.True(novo.X > g.Elementos.Where(e => e.Id != "novo").Max(e => e.X + e.Largura));
        Assert.Equal("9.3", novo.Numero);
        Assert.Equal("9.3", g.Numeros["novo"]);
        Assert.All(g.Elementos.Where(e => e.Id != "novo"), e => Assert.False(e.Solto));
        Assert.Contains("A tarefa \"Nova tarefa\" não tem de onde vir: diga o que vem antes dela.", g.Erros);
        Assert.Contains("A tarefa \"Nova tarefa\" não leva a lugar nenhum: diga o que vem depois.", g.Erros);
        Assert.Equal(PeFluxoDefinicaoLeitor.Ler(ComoJson(definicao)).Erros, g.Erros);

        // O resto do desenho fica onde estava
        foreach (var e in antes.Elementos)
            Assert.Equal((e.Camada, e.Faixa, e.X, e.Y), (Em(g, e.Id).Camada, Em(g, e.Id).Faixa, Em(g, e.Id).X, Em(g, e.Id).Y));
        SemSobreposicao(g);
        DentroDasRaias(g);
    }

    [Fact]
    public async Task VariosSoltos_UmPorColunaEmCadaRaia_NaOrdemDaDefinicao()
    {
        var definicao = Simples();
        definicao.Elementos.Add(Elemento("s1", "tarefa", "r2", "Solta 1"));
        definicao.Elementos.Add(Elemento("s2", "fim", "r1"));
        definicao.Elementos.Add(Elemento("s3", "decisao", "r2", "Solta?"));
        var g = await GeometriaAsync(definicao);

        var ultima = g.Elementos.Where(e => !e.Solto).Max(e => e.Camada);
        Assert.Equal(new[] { "s1", "s2", "s3" }, g.Elementos.Where(e => e.Solto).Select(e => e.Id));
        Assert.Equal(ultima + 1, Em(g, "s1").Camada);
        Assert.Equal(ultima + 1, Em(g, "s2").Camada);
        Assert.Equal(ultima + 2, Em(g, "s3").Camada);
        SemSobreposicao(g);
        DentroDasRaias(g);

        // O fluxo só com blocos soltos começa na primeira coluna
        var soltos = await Fluxos.GeometriaAsync(new PeFluxoDesenhoDTO
        {
            Definicao = ComoJson(new
            {
                Raias = new[] { new { Id = "r1", Nome = "Equipe", Ordem = 1 } },
                Elementos = new[] { new { Id = "i", Tipo = "inicio", RaiaId = "r1", Nome = "" }, new { Id = "t", Tipo = "tarefa", RaiaId = "r1", Nome = "Primeira" } },
                Ligacoes = Array.Empty<object>()
            })
        }, await Orgao());
        Assert.Equal(new[] { 0, 1 }, soltos.Elementos.Select(e => e.Camada));
        Assert.All(soltos.Elementos, e => Assert.True(e.Solto));
    }

    [Fact]
    public async Task DecisaoComUmaSaida_Desenhada_ComOErro()
    {
        var definicao = Simples();
        definicao.Ligacoes.RemoveAll(l => l.Id == "l5");
        var g = await GeometriaAsync(definicao);

        Assert.Contains("A decisão \"Aprovado?\" precisa de pelo menos duas saídas (por exemplo, Sim e Não).", g.Erros);
        Assert.False(Em(g, "d").Solto);
        var saida = g.Ligacoes.Single(l => l.De == "d");
        Assert.Equal(("Sim", false), (saida.Rotulo, saida.Retorno));
        Assert.NotNull(saida.RotuloX);
        Assert.Contains(g.Textos, t => t.Dono == "ligacao:l4" && t.Texto == "Sim" && t.X == saida.RotuloX && t.Y == saida.RotuloY);
        SemSobreposicao(g);
        LigacoesNasBordas(g);
    }

    [Fact]
    public async Task LigacaoParaOQueNaoExiste_FicaDeFora_ComOErro_SemMexerNoResto()
    {
        var definicao = Simples();
        definicao.Ligacoes.Add(new PeFluxoLigacao { Id = "l6", De = "a", Para = "sumiu" });
        definicao.Ligacoes.Add(new PeFluxoLigacao { Id = "l7", De = "a", Para = "a" });
        var g = await GeometriaAsync(definicao);

        Assert.DoesNotContain(g.Ligacoes, l => l.Id is "l6" or "l7");
        Assert.Contains("A ligação 6 vai para um passo que não existe.", g.Erros);
        Assert.Contains("A tarefa \"Primeira tarefa\": uma ligação não pode sair e voltar para o mesmo passo.", g.Erros);
        // Tirando os erros, o desenho é o mesmo de sem essas ligações
        var sem = await GeometriaAsync(Simples());
        g.Erros = new List<string>();
        Assert.Equal(JsonDe(sem), JsonDe(g));
    }

    [Fact]
    public async Task SemNome_ERaiaQueSumiu_Desenhados_ComOsErros()
    {
        var definicao = Simples();
        definicao.Elementos.Single(e => e.Id == "b").Nome = "";
        definicao.Elementos.Single(e => e.Id == "a").RaiaId = "r9";
        var g = await GeometriaAsync(definicao);

        Assert.Contains("Dê um nome ao passo 3 (tarefa).", g.Erros);
        Assert.Contains("A tarefa \"Primeira tarefa\": a raia escolhida não existe.", g.Erros);
        // Sem raia que exista, o elemento vai para a primeira
        Assert.Equal("r1", Em(g, "a").RaiaId);
        var b = Em(g, "b");
        Assert.Empty(b.Linhas);
        Assert.Null(b.PosicaoNome);
        Assert.Equal("9.2", b.Numero);
        SemSobreposicao(g);
        DentroDasRaias(g);
    }

    [Fact]
    public async Task SoAsRaias_OuNenhumaRaia_Desenhados_ComOsErros()
    {
        var vazio = await Fluxos.GeometriaAsync(new PeFluxoDesenhoDTO
        {
            Definicao = ComoJson(new { Raias = new[] { new { Id = "r1", Nome = "Equipe", Ordem = 1 } }, Elementos = Array.Empty<object>(), Ligacoes = Array.Empty<object>() })
        }, await Orgao());
        Assert.Equal("Equipe", Assert.Single(vazio.Raias).Nome);
        Assert.Empty(vazio.Elementos);
        Assert.Contains("O fluxo precisa de um início.", vazio.Erros);
        Assert.Contains("O fluxo precisa de pelo menos um fim.", vazio.Erros);
        Assert.True(vazio.Largura > 0 && vazio.Altura > 0);

        var semRaias = await Fluxos.GeometriaAsync(new PeFluxoDesenhoDTO
        {
            Definicao = ComoJson(new { Raias = Array.Empty<object>(), Elementos = new[] { new { Id = "i", Tipo = "inicio", RaiaId = "r1", Nome = "" } }, Ligacoes = Array.Empty<object>() })
        }, await Orgao());
        Assert.Contains("Inclua pelo menos uma raia (quem faz os passos).", semRaias.Erros);
        Assert.Equal(Assert.Single(semRaias.Raias).Id, Assert.Single(semRaias.Elementos).RaiaId);
    }

    // ── 400 só para a definição ilegível ──────────────────────────────────────

    public static IEnumerable<object[]> Ilegiveis()
    {
        object R(string id = "r1", string nome = "Equipe") => new { Id = id, Nome = nome, Ordem = 1 };
        object T(string id, string nome = "Tarefa", string tipo = "tarefa", object? artefatos = null) =>
            new { Id = id, Tipo = tipo, RaiaId = "r1", Nome = nome, Artefatos = artefatos ?? Array.Empty<string>() };
        object L(string id, string de, string para, string? rotulo = null) => new { Id = id, De = de, Para = para, Rotulo = rotulo };
        object D(object[] elementos, object[]? ligacoes = null, object[]? raias = null) =>
            new { Raias = raias ?? new[] { R() }, Elementos = elementos, Ligacoes = ligacoes ?? Array.Empty<object>() };

        yield return new object[] { "nula", "null", "Envie a definição do fluxo como um objeto com Raias, Elementos e Ligacoes." };
        yield return new object[] { "lista", "[1]", "Envie a definição do fluxo como um objeto com Raias, Elementos e Ligacoes." };
        yield return new object[] { "raias", JsonSerializer.Serialize(new { Raias = "x", Elementos = Array.Empty<object>(), Ligacoes = Array.Empty<object>() }), "Raias precisa ser uma lista." };
        yield return new object[] { "sem elementos", JsonSerializer.Serialize(new { Raias = new[] { R() }, Ligacoes = Array.Empty<object>() }), "Falta a lista Elementos na definição do fluxo." };
        yield return new object[] { "elemento", JsonSerializer.Serialize(D(new object[] { 7 })), "O passo 1 precisa ser um objeto com Id, Tipo, RaiaId e Nome." };
        yield return new object[] { "ligação", JsonSerializer.Serialize(D(new[] { T("a") }, new object[] { "x" })), "A ligação 1 precisa ser um objeto com Id, De e Para." };
        yield return new object[] { "sem id", JsonSerializer.Serialize(D(new[] { T("") })), "A tarefa \"Tarefa\": falta o id." };
        yield return new object[] { "id repetido", JsonSerializer.Serialize(D(new[] { T("r1") })), "O id \"r1\" aparece mais de uma vez. Cada raia, passo e ligação precisa de um id próprio." };
        yield return new object[] { "id que não serve", JsonSerializer.Serialize(D(new[] { T("a b") })), "A tarefa \"Tarefa\": o id \"a b\" não serve. Use até 40 letras, números, hífen ou sublinhado." };
        yield return new object[] { "tipo", JsonSerializer.Serialize(D(new[] { T("a", "Coisa", "evento") })), "O passo \"Coisa\": o tipo \"evento\" não existe." };
        yield return new object[] { "nome longo", JsonSerializer.Serialize(D(new[] { T("a", new string('n', 201)) })), "o nome passa de 200 caracteres." };
        yield return new object[] { "raia longa", JsonSerializer.Serialize(D(new[] { T("a") }, raias: new[] { R("r1", new string('r', 121)) })), "passa de 120 caracteres." };
        yield return new object[] { "rótulo longo", JsonSerializer.Serialize(D(new[] { T("a"), T("b") }, new[] { L("l1", "a", "b", new string('s', 61)) })), "da ligação 1 passa de 60 caracteres." };
        yield return new object[] { "artefatos demais", JsonSerializer.Serialize(D(new[] { T("a", "Muitos", artefatos: new[] { "1", "2", "3", "4", "5", "6", "7" }) })), "A tarefa \"Muitos\": tem artefatos demais (até 6)." };
        yield return new object[] { "artefato longo", JsonSerializer.Serialize(D(new[] { T("a", artefatos: new[] { new string('d', 121) }) })), "passa de 120 caracteres." };
        yield return new object[] { "artefatos sem lista", JsonSerializer.Serialize(D(new[] { T("a", artefatos: "Ata") })), "A tarefa \"Tarefa\": os artefatos vêm numa lista de nomes." };
        yield return new object[] { "raias demais", JsonSerializer.Serialize(D(new[] { T("a") }, raias: Enumerable.Range(1, 13).Select(i => R($"r{i}")).ToArray())), "O fluxo aceita até 12 raias." };
        yield return new object[] { "passos demais", JsonSerializer.Serialize(D(Enumerable.Range(1, 121).Select(i => T($"t{i}")).ToArray())), "O fluxo aceita até 120 passos." };
        yield return new object[] { "ligações demais", JsonSerializer.Serialize(D(new[] { T("a"), T("b") }, Enumerable.Range(1, 241).Select(i => L($"l{i}", "a", "b")).ToArray())), "O fluxo aceita até 240 ligações." };
    }

    [Theory]
    [MemberData(nameof(Ilegiveis))]
    public async Task Ilegivel_400_ComOProblema(string caso, string definicao, string problema)
    {
        using var json = JsonDocument.Parse(definicao);
        var ex = await Assert.ThrowsAsync<PeFluxoInvalidoException>(async () =>
            await Fluxos.GeometriaAsync(new PeFluxoDesenhoDTO { Definicao = json.RootElement.Clone() }, await Orgao()));
        Assert.Equal((int)ErrorCode.PeFluxoInvalido, ex.Error.Code);
        Assert.True(ex.Erros.Any(e => e.Contains(problema)), $"{caso}: {string.Join(" | ", ex.Erros)}");
    }

    [Fact]
    public async Task Ilegivel_OsErrosSoDoQueImpedeDesenhar_ENomeLongo()
    {
        // Bloco solto (tolerado) e nome gigante (ilegível): 400 só com o que impede desenhar
        var definicao = Simples();
        definicao.Elementos.Add(Elemento("novo", "tarefa", "r2", new string('x', 201)));
        var ex = await Assert.ThrowsAsync<PeFluxoInvalidoException>(async () => await GeometriaAsync(definicao));
        Assert.Single(ex.Erros);
        Assert.EndsWith("o nome passa de 200 caracteres.", ex.Erros[0]);

        // O nome do fluxo (o título do desenho) também tem limite, na geometria e na prévia em SVG
        var longo = await Assert.ThrowsAsync<PeFluxoInvalidoException>(async () => await GeometriaAsync(Simples(), new string('t', 201)));
        Assert.Equal(new[] { "O nome do fluxo passa de 200 caracteres." }, longo.Erros);
        var svg = await Assert.ThrowsAsync<PeFluxoInvalidoException>(async () =>
            await Fluxos.DesenhoAsync(Previa(Simples(), nome: new string('t', 201)), await Orgao()));
        Assert.Equal(new[] { "O nome do fluxo passa de 200 caracteres." }, svg.Erros);
        Assert.NotEmpty((await GeometriaAsync(Simples(), new string('t', 200))).Elementos);
    }

    // ── Tempo ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Tempo_DoDiagnostico_OMaior_AbaixoDe150ms()
    {
        var pdtic = await AbrirSesAsync();
        await PreencherNomesAsync(pdtic.Id);
        var ctx = await Orgao();
        var fluxo = await Fluxos.ObterAsync(pdtic.Id, "diagnostico", ctx);
        Assert.Equal(20, fluxo.Definicao.Elementos.Count);
        var dto = Previa(fluxo.Definicao, pdtic.Id, fluxo.Nome);

        // O caminho inteiro da requisição (ler a definição, os nomes do órgão no banco, o layout
        // e o JSON), depois de aquecer (fonte e JIT)
        async Task<double> MedirAsync(Func<Task> acao)
        {
            for (var i = 0; i < 3; i++) await acao();
            var tempos = new List<double>();
            for (var i = 0; i < 15; i++)
            {
                var relogio = Stopwatch.StartNew();
                await acao();
                tempos.Add(relogio.Elapsed.TotalMilliseconds);
            }
            tempos.Sort();
            return tempos[tempos.Count / 2];
        }

        var inteiro = await MedirAsync(async () => JsonDe(await Fluxos.GeometriaAsync(dto, ctx)));
        var soOLayout = await MedirAsync(() =>
        {
            JsonDe(PeFluxoDesenho.Desenhar(fluxo.Definicao, fluxo.Nome, NomesPadrao()).Geometria);
            return Task.CompletedTask;
        });
        Assert.True(inteiro < 150, $"geometria do diagnóstico: mediana de {inteiro.ToString("0.0", CultureInfo.InvariantCulture)} ms");
        Assert.True(soOLayout < 150, $"layout do diagnóstico: mediana de {soOLayout.ToString("0.0", CultureInfo.InvariantCulture)} ms");
    }

    [Fact]
    public void FluxosDoGuia_LongeDoLimiteDoEsforcoDasRotas()
    {
        // O limite só pega desenhos enormes: os fluxos do guia ficam abaixo de 1% dele (então a
        // busca das rotas deles é a completa, a mesma da E8)
        foreach (var chave in ChavesDoGuia.Select(c => (string)c[0]))
        {
            var desenho = PeFluxoDesenho.Desenhar(DefinicaoDoModelo(chave), ModeloNoBanco(chave).Nome, NomesPadrao());
            Assert.True(desenho.EsforcoDasRotas < PeFluxoDesenho.EsforcoMaximo / 100, $"{chave}: {desenho.EsforcoDasRotas}");
        }
    }

    /// <summary>
    /// Um fluxo nos limites da definição: 12 raias, 120 passos (tarefas, decisões e paralelos,
    /// com artefatos) e 240 ligações, com atalhos que atravessam dezenas de colunas e retornos.
    /// </summary>
    private static PeFluxoDefinicao NoLimite()
    {
        var d = new PeFluxoDefinicao { PrefixoNumeracao = "2" };
        for (var r = 0; r < PeFluxoDefinicaoLeitor.MaximoRaias; r++)
            d.Raias.Add(new PeFluxoRaia { Id = $"r{r}", Nome = $"Raia número {r} com um nome de tamanho médio", Ordem = r + 1 });
        d.Elementos.Add(Elemento("i", "inicio", "r0"));
        for (var k = 0; k < 118; k++)
        {
            var tipo = k % 9 == 4 ? "decisao" : k % 13 == 7 ? "paralelo" : "tarefa";
            var e = Elemento($"t{k}", tipo, $"r{k % 12}", $"Passo {k} com um nome que ocupa duas ou três linhas na caixa");
            if (tipo == "tarefa" && k % 3 == 0) e.Artefatos.Add($"Documento {k}");
            d.Elementos.Add(e);
        }
        d.Elementos.Add(Elemento("f", "fim", "r11"));
        void Ligar(string de, string para, string? rotulo = null)
        {
            if (d.Ligacoes.Count < PeFluxoDefinicaoLeitor.MaximoLigacoes)
                d.Ligacoes.Add(new PeFluxoLigacao { Id = $"l{d.Ligacoes.Count}", De = de, Para = para, Rotulo = rotulo });
        }
        Ligar("i", "t0");
        for (var k = 0; k < 117; k++) Ligar($"t{k}", $"t{k + 1}", d.Elementos[k + 1].Tipo == "decisao" ? "Sim" : null);
        Ligar("t117", "f");
        for (var k = 0; d.Ligacoes.Count < PeFluxoDefinicaoLeitor.MaximoLigacoes; k++)
        {
            var de = (k * 7) % 110;
            var para = (de + 3 + (k * 11) % 90) % 118;
            if (para != de) Ligar($"t{de}", $"t{para}", d.Elementos[de + 1].Tipo == "decisao" ? $"R{k}" : null);
        }
        return d;
    }

    [Fact]
    public async Task FluxoNoLimiteDaDefinicao_DesenhadoEmTempoCurto_SemSobreposicao()
    {
        // Sem o limite do esforço, este desenho levava minutos (a busca das rotas crescia com o
        // quadrado das colunas vezes os corredores, a cada ligação)
        var definicao = NoLimite();
        Assert.Equal((120, 240, 12), (definicao.Elementos.Count, definicao.Ligacoes.Count, definicao.Raias.Count));
        var relogio = Stopwatch.StartNew();
        var g = await GeometriaAsync(definicao, "No limite");
        var tempo = relogio.Elapsed.TotalMilliseconds;

        Assert.True(tempo < 5000, $"{tempo.ToString("0", CultureInfo.InvariantCulture)} ms");
        Assert.Equal(120, g.Elementos.Count);
        Assert.Equal(240, g.Ligacoes.Count);
        SemSobreposicao(g, "no limite");
        DentroDasRaias(g, "no limite");
        LigacoesNasBordas(g, "no limite");
        // Passou do limite (a busca ficou econômica) e continua sempre igual para a mesma definição
        var desenho = PeFluxoDesenho.Desenhar(PeFluxoDefinicaoLeitor.Ler(ComoJson(definicao)).Definicao!, "No limite", NomesPadrao());
        Assert.True(desenho.EsforcoDasRotas > PeFluxoDesenho.EsforcoMaximo);
        g.Erros = new List<string>();
        Assert.Equal(JsonDe(desenho.Geometria), JsonDe(g));
    }

    // ── Permissões e nada gravado ─────────────────────────────────────────────

    [Fact]
    public async Task Permissoes_AsDaLeituraDosFluxos_ENadaGravado()
    {
        var pdtic = await AbrirSesAsync();
        var alteradoEm = Context.PePdtics.AsNoTracking().Single(p => p.Id == pdtic.Id).AlteradoEm;

        // Modelo: todo papel do módulo lê
        foreach (var user in new[] { UserPeAdmin, UserPeSgdi, UserPeCgtic, UserOrgaoSes, UserConsultaSes, UserAdminGeral })
            Assert.NotEmpty((await Fluxos.GeometriaDoModeloAsync("preparacao", await ContextoDe(user))).Elementos);
        Assert.Equal(Codigo(ErrorCode.PeSemPermissao), await ErroAsync(async () => await Fluxos.GeometriaDoModeloAsync("preparacao", await ContextoDe(UserSemPapel))));
        Assert.Equal(Codigo(ErrorCode.PeFluxoNaoEncontrado), await ErroAsync(async () => await Fluxos.GeometriaDoModeloAsync("nao_existe", await Admin())));

        // PDTIC: quem vê o órgão (a consulta e os papéis globais inclusive)
        foreach (var user in new[] { UserOrgaoSes, UserConsultaSes, UserPeSgdi, UserPeCgtic, UserPeAdmin, UserAdminGeral })
            Assert.NotEmpty((await Fluxos.GeometriaDoPdticAsync(pdtic.Id, "preparacao", await ContextoDe(user))).Elementos);
        Assert.Equal(Codigo(ErrorCode.PeSemPermissao), await ErroAsync(async () => await Fluxos.GeometriaDoPdticAsync(pdtic.Id, "preparacao", await ContextoDe(UserOrgaoSeec))));
        Assert.Equal(Codigo(ErrorCode.PePdticNaoEncontrado), await ErroAsync(async () => await Fluxos.GeometriaDoPdticAsync(999_999, "preparacao", await Orgao())));
        Assert.Equal(Codigo(ErrorCode.PeFluxoNaoEncontrado), await ErroAsync(async () => await Fluxos.GeometriaDoPdticAsync(pdtic.Id, "nao_existe", await Orgao())));

        // Prévia: todo papel; com o PDTIC, só quem vê o órgão
        Assert.NotEmpty((await Fluxos.GeometriaAsync(Previa(Simples()), await ContextoDe(UserPeCgtic))).Elementos);
        Assert.NotEmpty((await Fluxos.GeometriaAsync(Previa(Simples(), pdtic.Id), await ContextoDe(UserConsultaSes))).Elementos);
        Assert.Equal(Codigo(ErrorCode.PeSemPermissao), await ErroAsync(async () => await Fluxos.GeometriaAsync(Previa(Simples(), pdtic.Id), await ContextoDe(UserOrgaoSeec))));
        Assert.Equal(Codigo(ErrorCode.PeSemPermissao), await ErroAsync(async () => await Fluxos.GeometriaAsync(Previa(Simples()), await ContextoDe(UserSemPapel))));

        // Ler a geometria não grava nada (nem a cópia do órgão, nem o PDTIC)
        Assert.Empty(Context.PeFluxos.AsNoTracking());
        Assert.Equal(alteradoEm, Context.PePdtics.AsNoTracking().Single(p => p.Id == pdtic.Id).AlteradoEm);
    }

    // ── Rotas e JSON do contrato ──────────────────────────────────────────────

    private static List<string> ErrosDoCorpo(object? corpo) =>
        ((IEnumerable<string>)corpo!.GetType().GetProperty("Erros")!.GetValue(corpo)!).ToList();

    [Fact]
    public async Task Rotas_ComoOFrontChama()
    {
        var pdtic = await AbrirSesAsync();
        var orgao = ControladorFluxos(UserOrgaoSes);

        var (status, corpo) = Resultado(await orgao.GeometriaDoModelo("diagnostico"));
        Assert.Equal(StatusCodes.Status200OK, status);
        Assert.Equal(20, Assert.IsType<PeFluxoGeometria>(corpo).Elementos.Count);
        (status, corpo) = Resultado(await ControladorFluxos(UserConsultaSes).GeometriaDoPdtic(pdtic.Id, "diagnostico"));
        Assert.Equal(StatusCodes.Status200OK, status);
        Assert.Empty(Assert.IsType<PeFluxoGeometria>(corpo).Erros);

        // Definição incompleta: 200 com os problemas
        var solto = Simples();
        solto.Elementos.Add(Elemento("novo", "tarefa", "r1", "Nova"));
        (status, corpo) = Resultado(await orgao.Geometria(JsonSerializer.SerializeToElement(new { Definicao = ComoJson(solto), PdticId = pdtic.Id, Nome = "Teste" })));
        Assert.Equal(StatusCodes.Status200OK, status);
        var g = Assert.IsType<PeFluxoGeometria>(corpo);
        Assert.True(Em(g, "novo").Solto);
        Assert.NotEmpty(g.Erros);

        // Ilegível: 400 { Code, Message, Erros }; corpo que não é objeto: 400 com mensagem
        (status, corpo) = Resultado(await orgao.Geometria(JsonSerializer.SerializeToElement(new { Definicao = new { Raias = 1 } })));
        Assert.Equal((StatusCodes.Status400BadRequest, (int)ErrorCode.PeFluxoInvalido), (status, CodigoDe(corpo)));
        Assert.Contains("Raias precisa ser uma lista.", ErrosDoCorpo(corpo));
        (status, corpo) = Resultado(await orgao.Geometria(JsonSerializer.SerializeToElement(new[] { 1 })));
        Assert.Equal((StatusCodes.Status400BadRequest, (int)ErrorCode.PeDadosInvalidos), (status, CodigoDe(corpo)));

        // 403 e 404 com corpo
        (status, corpo) = Resultado(await ControladorFluxos(UserOrgaoSeec).GeometriaDoPdtic(pdtic.Id, "diagnostico"));
        Assert.Equal((StatusCodes.Status403Forbidden, (int)ErrorCode.PeSemPermissao), (status, CodigoDe(corpo)));
        (status, corpo) = Resultado(await ControladorFluxos(UserSemPapel).Geometria(JsonSerializer.SerializeToElement(new { Definicao = ComoJson(Simples()) })));
        Assert.Equal((StatusCodes.Status403Forbidden, (int)ErrorCode.PeSemPermissao), (status, CodigoDe(corpo)));
        (status, corpo) = Resultado(await orgao.GeometriaDoModelo("nao_existe"));
        Assert.Equal((StatusCodes.Status404NotFound, (int)ErrorCode.PeFluxoNaoEncontrado), (status, CodigoDe(corpo)));
        (status, corpo) = Resultado(await orgao.GeometriaDoPdtic(999_999, "diagnostico"));
        Assert.Equal((StatusCodes.Status404NotFound, (int)ErrorCode.PePdticNaoEncontrado), (status, CodigoDe(corpo)));
    }

    [Fact]
    public async Task Json_NoFormatoDoContrato()
    {
        var json = JsonSerializer.SerializeToElement(await GeometriaAsync(Simples()), ComoAApi);

        void Tem(JsonElement objeto, params string[] nomes)
        {
            foreach (var nome in nomes) Assert.True(objeto.TryGetProperty(nome, out _), $"falta {nome}");
        }
        Tem(json, "Largura", "Altura", "Raias", "Elementos", "Ligacoes", "Artefatos", "Erros", "Numeros");
        Tem(json.GetProperty("Raias")[0], "Id", "Nome", "NomeOriginal", "Y", "Altura", "LarguraCabecalho");
        Tem(json.GetProperty("Elementos")[0], "Id", "Tipo", "RaiaId", "Camada", "X", "Y", "Largura", "Altura", "Numero", "Nome", "Linhas", "Solto");
        var ligacoes = json.GetProperty("Ligacoes").EnumerateArray().ToList();
        Assert.All(ligacoes, l => Tem(l, "Id", "De", "Para", "Rotulo", "Retorno", "Pontos", "RotuloX", "RotuloY"));
        Tem(ligacoes[0].GetProperty("Pontos")[0], "X", "Y");
        var comRotulo = ligacoes.Single(l => l.GetProperty("Id").GetString() == "l4");
        Assert.Equal("Sim", comRotulo.GetProperty("Rotulo").GetString());
        Assert.Equal(JsonValueKind.Number, comRotulo.GetProperty("RotuloX").ValueKind);
        var semRotulo = ligacoes.Single(l => l.GetProperty("Id").GetString() == "l1");
        Assert.Equal(JsonValueKind.Null, semRotulo.GetProperty("RotuloX").ValueKind);
        Assert.True(ligacoes.Single(l => l.GetProperty("Id").GetString() == "l5").GetProperty("Retorno").GetBoolean());
        var artefato = Assert.Single(json.GetProperty("Artefatos").EnumerateArray());
        Tem(artefato, "ElementoId", "Indice", "Nome", "X", "Y", "Largura", "Altura");
        Assert.Equal(("b", 0, "Relatório"), (artefato.GetProperty("ElementoId").GetString(), artefato.GetProperty("Indice").GetInt32(), artefato.GetProperty("Nome").GetString()));
        Assert.Equal("9.1", json.GetProperty("Numeros").GetProperty("a").GetString());
        Assert.Equal("9.2", json.GetProperty("Numeros").GetProperty("b").GetString());
        Assert.Equal(JsonValueKind.Array, json.GetProperty("Erros").ValueKind);
        var raw = json.GetRawText();
        Assert.DoesNotContain((char)0x2014, raw);
        Assert.DoesNotContain((char)0x2013, raw);
    }

    // ── Definições ao acaso ───────────────────────────────────────────────────

    // Nomes de passo (até 200 caracteres) e de raia (até 120), inclusive vazios e compridos
    private static readonly string[] NomesAoAcaso =
    {
        "", "Aprovar", "Levantar as necessidades de TIC", "Elaborar o inventário [situação atual] das necessidades",
        "Enviar ao {nomes.comite}", "Palavraenormesemespacoquenaocabenalinhadacaixadatarefa",
        string.Join(" ", Enumerable.Repeat("Consolidar a minuta do PDTIC", 6)), "Ok?", "(parcial)"
    };

    private static readonly string[] RaiasAoAcaso =
    {
        "", "Equipe", "{nomes.equipe}", "{nomes.autoridade_cargo}",
        "Unidade de Tecnologia da Informação e Comunicação do órgão, com o nome comprido para quebrar"
    };

    private static PeFluxoDefinicao AoAcaso(Random sorte)
    {
        var tipos = PeDominios.TipoElementoFluxo.Todos.ToArray();
        var d = new PeFluxoDefinicao { PrefixoNumeracao = sorte.Next(3) == 0 ? null : (1 + sorte.Next(7)).ToString(CultureInfo.InvariantCulture) };
        var raias = 1 + sorte.Next(4);
        for (var r = 0; r < raias; r++)
            d.Raias.Add(new PeFluxoRaia { Id = $"r{r}", Nome = r == 0 ? "{nomes.comite}" : RaiasAoAcaso[sorte.Next(RaiasAoAcaso.Length)], Ordem = r + 1 });
        var n = sorte.Next(0, 26);
        for (var i = 0; i < n; i++)
        {
            var e = Elemento($"e{i}", tipos[sorte.Next(tipos.Length)], sorte.Next(12) == 0 ? "sumiu" : $"r{sorte.Next(raias)}", NomesAoAcaso[sorte.Next(NomesAoAcaso.Length)]);
            if (PeDominios.TipoElementoFluxo.EhAtividade(e.Tipo))
                for (var k = sorte.Next(4); k > 0; k--) e.Artefatos.Add(k == 2 ? "Plano de trabalho do PDTIC (versão preliminar)" : $"Documento {k}");
            d.Elementos.Add(e);
        }
        var m = n == 0 ? 0 : sorte.Next(0, 2 * n + 1);
        for (var i = 0; i < m; i++)
            d.Ligacoes.Add(new PeFluxoLigacao
            {
                Id = $"l{i}",
                De = sorte.Next(12) == 0 ? "sumiu" : $"e{sorte.Next(n)}",
                Para = sorte.Next(12) == 0 ? "sumiu" : $"e{sorte.Next(n)}",
                Rotulo = sorte.Next(3) == 0 ? (sorte.Next(2) == 0 ? "Sim" : "Não") : null
            });
        return d;
    }

    [Fact]
    public void DefinicoesAoAcaso_SempreDesenhadas_SemSobreposicao_EOSvgSaiDaGeometria()
    {
        var sorte = new Random(48900);
        for (var caso = 0; caso < 300; caso++)
        {
            var original = AoAcaso(sorte);
            var lida = PeFluxoDefinicaoLeitor.Ler(ComoJson(original));
            Assert.True(lida.Legivel, $"caso {caso}: {string.Join(" | ", lida.Ilegiveis)}");
            var desenho = PeFluxoDesenho.Desenhar(lida.Definicao!, $"Ao acaso {caso}", NomesPadrao());
            var g = desenho.Geometria;
            var contexto = $"caso {caso}";

            var ids = original.Elementos.Select(e => e.Id).ToHashSet();
            var validas = original.Ligacoes.Where(l => ids.Contains(l.De) && ids.Contains(l.Para) && l.De != l.Para).ToList();
            Assert.Equal(original.Elementos.Select(e => e.Id), g.Elementos.Select(e => e.Id));
            Assert.Equal(validas.Count, g.Ligacoes.Count);
            foreach (var e in g.Elementos)
                Assert.Equal(validas.All(l => l.De != e.Id && l.Para != e.Id), e.Solto);
            if (g.Elementos.Any(e => e.Solto) && g.Elementos.Any(e => !e.Solto))
                Assert.True(g.Elementos.Where(e => e.Solto).Min(e => e.Camada) > g.Elementos.Where(e => !e.Solto).Max(e => e.Camada), contexto);

            SemSobreposicao(g, contexto);
            DentroDasRaias(g, contexto);
            LigacoesNasBordas(g, contexto);
            // O JSON sai (nenhum número infinito ou NaN) e o SVG sai da geometria
            var doJson = JsonSerializer.Deserialize<PeFluxoGeometria>(JsonDe(g), ComoAApi)!;
            Assert.Equal(desenho.Svg, SvgDe(doJson));
            XDocument.Parse(desenho.Svg);
        }
    }
}
