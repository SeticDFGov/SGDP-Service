using api.Planejamento;
using service.Planejamento;
using Xunit;

namespace test.planejamento;

/// <summary>
/// F2 (segunda leva), marcadores do documento: cada marcador diz onde o valor se preenche
/// (SecaoChave, PassoChave e, no documento de um PDTIC, o PassoNumero na trilha do órgão), para
/// o link "preencha no passo N.M" da prévia. As aprovações vêm do passo do envio (SGTIC) e do
/// passo da deliberação (CGTIC), e a publicação do passo dela: 3.11, 3.12 e 3.13 no Avançado.
/// </summary>
public class PeF2MarcadoresTest : PeAcompanhamentoTestBase
{
    private static PeDocMarcadorResponse M(IEnumerable<PeDocMarcadorResponse> lista, string chave) => lista.Single(m => m.Chave == chave);

    private static (string? Secao, string? Passo, string? Numero) Onde(IEnumerable<PeDocMarcadorResponse> lista, string chave)
    {
        var m = M(lista, chave);
        return (m.SecaoChave, m.PassoChave, m.PassoNumero);
    }

    [Fact]
    public async Task ModeloDoDocumento_CadaMarcadorDizOPassoOndeSePreenche_SemONumero()
    {
        var lista = (await ModeloDoc.ObterAsync("pdtic")).Marcadores;

        Assert.Equal(("aprovacao_sgtic", "planejamento.aprovacao-sgtic", (string?)null), Onde(lista, "aprovacao.sgtic.data"));
        Assert.Equal(("aprovacao_sgtic", "planejamento.aprovacao-sgtic", (string?)null), Onde(lista, "aprovacao.sgtic.ato"));
        // A aprovação do CGTIC não tem seção: quem registra a decisão é a Secretaria, no passo da deliberação
        Assert.Equal(((string?)null, "planejamento.deliberacao-cgtic", (string?)null), Onde(lista, "aprovacao.cgtic.data"));
        Assert.Equal(((string?)null, "planejamento.deliberacao-cgtic", (string?)null), Onde(lista, "aprovacao.cgtic.ato"));
        Assert.Equal(("publicacao", "planejamento.publicacao", (string?)null), Onde(lista, "publicacao.data"));
        Assert.Equal(("publicacao", "planejamento.publicacao", (string?)null), Onde(lista, "publicacao.endereco"));
        Assert.Equal(("nomes", "preparacao.nomes", (string?)null), Onde(lista, "nomes.comite"));
        Assert.Equal(("nomes", "preparacao.nomes", (string?)null), Onde(lista, "orgao.sigla"));
        Assert.Equal(("abrangencia", "preparacao.abrangencia", (string?)null), Onde(lista, "vigencia.inicio"));
        // O que vem do sistema não tem passo
        foreach (var chave in new[] { "orgao.nome", "pdtic.versao", "hoje" })
            Assert.Equal(((string?)null, (string?)null, (string?)null), Onde(lista, chave));
        // O modelo do RA tem os do ciclo, também sem passo
        Assert.Equal(((string?)null, (string?)null, (string?)null), Onde((await ModeloDoc.ObterAsync("ra")).Marcadores, "ciclo.rotulo"));
    }

    [Fact]
    public async Task DocumentoNoAvancado_AsAprovacoesEAPublicacao_NosPassos311_312E313()
    {
        await DefinirNivelDoOrgaoAsync(OrgaoSes, "avancado");
        var pdtic = await AbrirSesAsync();

        var documento = await DocumentoAsync(pdtic.Id);
        var lista = documento.Marcadores;
        Assert.Equal(("aprovacao_sgtic", "planejamento.aprovacao-sgtic", "3.11"), Onde(lista, "aprovacao.sgtic.data"));
        Assert.Equal(((string?)null, "planejamento.deliberacao-cgtic", "3.12"), Onde(lista, "aprovacao.cgtic.ato"));
        Assert.Equal(("publicacao", "planejamento.publicacao", "3.13"), Onde(lista, "publicacao.endereco"));
        Assert.Equal(("nomes", "preparacao.nomes", "1.2"), Onde(lista, "nomes.comite"));
        Assert.Equal(("abrangencia", "preparacao.abrangencia", "1.1"), Onde(lista, "vigencia.fim"));
        Assert.DoesNotContain(lista, m => m.Chave.StartsWith("ciclo.", StringComparison.Ordinal));

        // Cada marcador sem valor dos blocos (a folha de rosto traz as aprovações e a publicação) está na lista, com o passo
        var semValor = documento.Capitulos.SelectMany(c => c.Blocos).SelectMany(b => b.MarcadoresSemValor).Distinct().ToList();
        Assert.Contains("aprovacao.cgtic.data", semValor);
        Assert.Contains("publicacao.data", semValor);
        Assert.All(semValor, chave => Assert.NotNull(M(lista, chave).PassoNumero));
    }

    [Fact]
    public async Task DocumentoNoBasico_ONumeroDaTrilhaDoOrgao_EOPassoEscondidoFicaSemNumero()
    {
        var pdtic = await AbrirSesAsync();
        var trilha = await TrilhaAsync(OrgaoSes);
        var lista = (await DocumentoAsync(pdtic.Id)).Marcadores;
        foreach (var (chave, passo) in new[]
                 {
                     ("aprovacao.sgtic.data", "planejamento.aprovacao-sgtic"), ("aprovacao.cgtic.data", "planejamento.deliberacao-cgtic"),
                     ("publicacao.data", "planejamento.publicacao"), ("nomes.autoridade", "preparacao.nomes")
                 })
            Assert.Equal((passo, NaTrilha(trilha, passo)!.Numero), (M(lista, chave).PassoChave, M(lista, chave).PassoNumero));

        // O passo do dicionário de nomes desligado para o órgão: a chave fica, sem o número (a prévia não mostra o link)
        await AjustarPassosAsync(OrgaoSes, ("preparacao.nomes", "desligado"));
        Assert.Equal(("nomes", "preparacao.nomes", (string?)null), Onde((await DocumentoAsync(pdtic.Id)).Marcadores, "nomes.comite"));
    }

    [Fact]
    public async Task RelatorioDeAcompanhamento_TambemTrazOsMarcadores_ComOsDoCiclo()
    {
        var id = await AcompanhadoAsync();
        var ciclo = (await CiclosAsync(id, "monitoramento")).First(c => c.Situacao != "futuro");

        var ra = await Documentos.ObterAsync(PeDocAlvo.Ra(id, ciclo.Id), await Orgao());
        Assert.Equal(((string?)null, (string?)null, (string?)null), Onde(ra.Marcadores, "ciclo.rotulo"));
        Assert.Equal("planejamento.deliberacao-cgtic", M(ra.Marcadores, "aprovacao.cgtic.data").PassoChave);
        var rr = await Documentos.ObterAsync(PeDocAlvo.Rr(id), await Orgao());
        Assert.DoesNotContain(rr.Marcadores, m => m.Chave.StartsWith("ciclo.", StringComparison.Ordinal));
        Assert.Equal("planejamento.publicacao", M(rr.Marcadores, "publicacao.endereco").PassoChave);
    }
}
