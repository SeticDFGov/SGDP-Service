using System.Text.Json;
using api.Planejamento;
using Microsoft.EntityFrameworkCore;
using Models.Planejamento;
using service;
using service.Planejamento;
using Xunit;

namespace test.planejamento;

/// <summary>
/// F2 (segunda leva), os tamanhos máximos que a tela e o servidor tinham diferentes, agora um
/// valor só por campo: o nome do documento que o passo do fluxo gera (o artefato) passa a 200,
/// o da tela, porque fica no jsonb da definição (sem coluna); a justificativa do "não se aplica",
/// o motivo do encerramento e a justificativa da revisão ficam em 1000 e a justificativa da
/// inadimplência em 2000, os tamanhos das colunas (subir pediria migration).
/// </summary>
public class PeF2LimitesTest : PePaineisTestBase
{
    private PeFluxoService Fluxos() => new(Context, Registros, Permissoes);

    private static JsonElement Json(PeFluxoDefinicao definicao) =>
        JsonDocument.Parse(PeFluxoDefinicaoLeitor.ParaJson(definicao)).RootElement.Clone();

    private static PeFluxoDefinicao ComDocumento(string nome) => new()
    {
        PrefixoNumeracao = "1",
        Raias = { new PeFluxoRaia { Id = "r1", Nome = "Equipe", Ordem = 1 } },
        Elementos =
        {
            new PeFluxoElemento { Id = "i", Tipo = "inicio", RaiaId = "r1" },
            new PeFluxoElemento { Id = "a", Tipo = "tarefa", RaiaId = "r1", Nome = "Elaborar o relatório", Artefatos = { nome } },
            new PeFluxoElemento { Id = "f", Tipo = "fim", RaiaId = "r1" }
        },
        Ligacoes = { new PeFluxoLigacao { Id = "l1", De = "i", Para = "a" }, new PeFluxoLigacao { Id = "l2", De = "a", Para = "f" } }
    };

    // 200 caracteres com espaços ("Ata da reunião " repetido) e 200 numa palavra só
    private static readonly string ComEspacos = string.Concat(Enumerable.Repeat("Ata da reunião ", 14))[..200];
    private static readonly string NumaPalavra = "Documento " + new string('x', 190);

    [Fact]
    public async Task NomeDoDocumentoNoFluxo_Ate200Caracteres_EODesenhoCortaEmQuatroLinhas()
    {
        Assert.Equal((200, 200), (ComEspacos.Length, NumaPalavra.Length));
        foreach (var nome in new[] { ComEspacos, NumaPalavra })
        {
            var lido = PeFluxoDefinicaoLeitor.Ler(Json(ComDocumento(nome)));
            Assert.True(lido.Valida, string.Join(" | ", lido.Erros));

            var g = await Fluxos().GeometriaAsync(new PeFluxoDesenhoDTO { Definicao = Json(ComDocumento(nome)), Nome = "Fluxo de teste" }, await Orgao());
            Assert.Empty(g.Erros);
            var documento = Assert.Single(g.Artefatos);
            Assert.Equal(nome, documento.Nome);
            // O rótulo embaixo do documento tem no máximo 4 linhas, com reticências no fim, e cabe no desenho
            Assert.Equal(4, documento.Linhas.Count);
            Assert.EndsWith("…", documento.Linhas[^1]);
            Assert.True(documento.Rotulo.X >= 0 && documento.Rotulo.X + documento.Rotulo.Largura <= g.Largura);
            Assert.True(documento.Rotulo.Y + documento.Rotulo.Altura <= g.Altura);
        }

        // 201: fora do limite (400, o problema que impede desenhar)
        var longo = PeFluxoDefinicaoLeitor.Ler(Json(ComDocumento(ComEspacos + "a")));
        Assert.Contains(longo.Ilegiveis, e => e.EndsWith("passa de 200 caracteres.", StringComparison.Ordinal));
        await Assert.ThrowsAsync<PeFluxoInvalidoException>(async () =>
            await Fluxos().GeometriaAsync(new PeFluxoDesenhoDTO { Definicao = Json(ComDocumento(ComEspacos + "a")) }, await Orgao()));
    }

    [Fact]
    public async Task NomeDoDocumentoComDuzentosCaracteres_GravaNaCopiaDoOrgao()
    {
        var pdtic = await AbrirSesAsync();
        var definicao = PeFluxoDefinicaoLeitor.DoBanco(Context.PeFluxosModelo.AsNoTracking().Single(m => m.Chave == "preparacao").Definicao);
        definicao.Elementos.First(e => e.Tipo == "tarefa").Artefatos = new List<string> { ComEspacos };

        var salvo = await Fluxos().SalvarAsync(pdtic.Id, "preparacao", new PeFluxoSalvarDTO { Definicao = Json(definicao) }, await Orgao());

        Assert.True(salvo.Personalizado);
        Assert.Contains(ComEspacos, PeFluxoDefinicaoLeitor.DoBanco(Context.PeFluxos.AsNoTracking().Single(f => f.PdticId == pdtic.Id).Definicao)
            .Elementos.SelectMany(e => e.Artefatos));
    }

    [Fact]
    public async Task NaoSeAplica_EncerramentoERevisao_Ate1000_EAInadimplencia_Ate2000()
    {
        // "Não se aplica" (o passo 1.8 opcional para o órgão): 1000
        await AjustarPassosAsync(OrgaoSes, ("preparacao.principios", "opcional"));
        var aberto = await AbrirSesAsync();
        var passo = Passo("preparacao.principios").Id;
        var orgao = await Orgao();
        var admin = await Admin();
        var excesso = await Assert.ThrowsAnyAsync<ApiException>(() =>
            Pdtics.MarcarNaoSeAplicaAsync(aberto.Id, passo, new PeNaoSeAplicaDTO { Justificativa = new string('a', 1001) }, orgao));
        Assert.Equal((Codigo(ErrorCode.PeDadosInvalidos), "A justificativa tem no máximo 1000 caracteres."), (excesso.Error.Code, excesso.Error.Message));
        Assert.Equal("nao_se_aplica", (await Pdtics.MarcarNaoSeAplicaAsync(aberto.Id, passo,
            new PeNaoSeAplicaDTO { Justificativa = new string('a', 1000) }, orgao)).Situacao);

        // Revisão e encerramento do PDTIC vigente: 1000
        Situacao(aberto.Id, PeDominios.SituacaoPdtic.Publicado);
        var revisao = await Assert.ThrowsAnyAsync<ApiException>(() =>
            Aprovacao.RevisarAsync(aberto.Id, new PeRevisaoDTO { Justificativa = new string('r', 1001) }, orgao));
        Assert.Equal("A justificativa tem no máximo 1000 caracteres.", revisao.Error.Message);
        Situacao(aberto.Id, PeDominios.SituacaoPdtic.Publicado, fimDaVigencia: HojeData().AddDays(-1));
        var encerramento = await Assert.ThrowsAnyAsync<ApiException>(() =>
            Aprovacao.EncerrarAsync(aberto.Id, new PeEncerrarDTO { Motivo = new string('m', 1001) }, admin));
        Assert.Equal("O motivo tem no máximo 1000 caracteres.", encerramento.Error.Message);
        Assert.Equal("encerrado", (await Aprovacao.EncerrarAsync(aberto.Id, new PeEncerrarDTO { Motivo = new string('m', 1000) }, admin)).Situacao);

        // A justificativa aceita da inadimplência: 2000
        var notificada = InadimplenciaNoBanco(OrgaoSeec, "notificado", HojeData());
        var (codigo, campos) = await ValidacaoAsync(async () =>
            await Inadimplencias.JustificarAsync(notificada.Id, new PeInadimplenciaJustificarDTO { Justificativa = new string('j', 2001) }, await Sgdi()));
        Assert.Equal(Codigo(ErrorCode.PeInadimplenciaInvalida), codigo);
        Assert.StartsWith("A justificativa tem no máximo 2000 caracteres", campos["Justificativa"]);
        Assert.Equal("justificado", (await Inadimplencias.JustificarAsync(notificada.Id,
            new PeInadimplenciaJustificarDTO { Justificativa = new string('j', 2000) }, await Sgdi())).Situacao);
    }
}
