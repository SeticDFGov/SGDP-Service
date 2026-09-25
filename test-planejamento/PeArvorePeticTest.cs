using api.Planejamento;
using service;
using Xunit;

namespace test.planejamento;

/// <summary>
/// A árvore do PETIC-DF (E8): os objetivos da versão vigente, cada um com os indicadores do
/// próprio PETIC-DF (a meta em texto) e, por órgão, as necessidades e as metas do PDTIC ligadas a
/// ele (só os órgãos com alguma ligação, pela sigla), com os totais; o filtro por órgão; sem
/// vigente, nenhum objetivo; e quem vê.
/// </summary>
public class PeArvorePeticTest : PePaineisTestBase
{
    [Fact]
    public async Task SemPeticVigente_NuloENenhumObjetivo()
    {
        await AbrirSesAsync();

        var arvore = await Paineis.ArvoreAsync(null, await Sgdi());

        Assert.Null(arvore.Petic);
        Assert.Empty(arvore.Objetivos);
    }

    /// <summary>
    /// O PETIC-DF vigente com o OE01 (os indicadores IE01, sem meta, e IE02, 80 % até 31/12/2027) e
    /// o OE02, sem ligação; a SES com a necessidade N01 e a meta M01 ligadas ao OE01 e a N02 sem
    /// ligação; a SEEC com a N01 ligada ao OE01.
    /// </summary>
    private async Task<(PePeticResponse Petic, long Oe01, long Oe02)> CenarioAsync()
    {
        var rascunho = await RascunhoAsync("PETIC-DF 2026-2029");
        var oe01 = await PreencherMinimoAsync(rascunho.Id);
        await IncluirAsync(rascunho.Id, "petic_indicador", new { nome = "Serviços com avaliação do cidadão", meta = 80, unidade = "%", prazo = "2027-12-31" },
            new { objetivo = new[] { oe01.Id } });
        var oe02 = await IncluirAsync(rascunho.Id, "petic_objetivo", new { texto = "Integrar os dados do DF." });
        await AprovarAsync(rascunho.Id);

        var ses = await AbrirSesAsync();
        var n01 = await IncluirNoPdticAsync(ses.Id, "necessidades", new { descricao = "Portal de serviços.", tipo = "servico", prioridade_simples = "alta" },
            new { objetivo_petic = new[] { oe01.Id } });
        await IncluirNoPdticAsync(ses.Id, "necessidades", new { descricao = "Trocar os monitores.", tipo = "infraestrutura", prioridade_simples = "baixa" });
        await IncluirNoPdticAsync(ses.Id, "metas", new { descricao = "Portal no ar.", indicador = "Serviços no portal", valor = "50", prazo = "2027-12-31", situacao = "em_andamento" },
            new { necessidades = new[] { n01.Id }, objetivo_petic = new[] { oe01.Id } });

        var seec = await AbrirSeecAsync();
        await IncluirNoPdticAsync(seec.Id, "necessidades", new { descricao = "Protocolo digital.", tipo = "servico", prioridade_simples = "media" },
            new { objetivo_petic = new[] { oe01.Id } }, user: UserOrgaoSeec);
        return (await Petics.ObterAsync(rascunho.Id, await Admin()), oe01.Id, oe02.Id);
    }

    [Fact]
    public async Task ComVigente_ObjetivosIndicadoresEOsOrgaosLigados()
    {
        var (petic, oe01, oe02) = await CenarioAsync();

        var arvore = await Paineis.ArvoreAsync(null, await Sgdi());

        Assert.Equal((petic.Id, "1.0", "PETIC-DF 2026-2029"), (arvore.Petic!.Id, arvore.Petic.Versao, arvore.Petic.Titulo));
        Assert.Equal(new[] { (oe01, "OE01", "Ampliar os serviços digitais."), (oe02, "OE02", "Integrar os dados do DF.") },
            arvore.Objetivos.Select(o => (o.RegistroId, o.Codigo!, o.Texto)));

        var objetivo = arvore.Objetivos[0];
        Assert.Equal(new[] { ("IE01", "Serviços digitais ofertados", (string?)null), ("IE02", "Serviços com avaliação do cidadão", "80 % até 31/12/2027") },
            objetivo.Indicadores.Select(i => (i.Codigo!, i.Nome, i.Meta)));
        Assert.Equal((2, 1), (objetivo.TotalNecessidades, objetivo.TotalMetas));
        Assert.Equal(new[] { "SEEC", "SES" }, objetivo.Orgaos.Select(o => o.Sigla));

        var ses = objetivo.Orgaos[1];
        Assert.Equal(OrgaoSes.Id, ses.OrgaoId);
        var necessidade = Assert.Single(ses.Necessidades);
        // No Básico o nível não pergunta se a necessidade foi priorizada
        Assert.Equal(("N01", "Portal de serviços.", (bool?)null), (necessidade.Codigo, necessidade.Descricao, necessidade.Priorizada));
        var meta = Assert.Single(ses.Metas);
        Assert.Equal(("M01", "Portal no ar.", "Serviços no portal", "50", new DateOnly(2027, 12, 31), "Em andamento"),
            (meta.Codigo, meta.Descricao, meta.Indicador, meta.Valor, meta.Prazo!.Value, meta.Situacao));
        Assert.Equal("Protocolo digital.", Assert.Single(objetivo.Orgaos[0].Necessidades).Descricao);
        Assert.Empty(objetivo.Orgaos[0].Metas);

        // O OE02 não tem ligação nem indicador
        Assert.Equal((0, 0), (arvore.Objetivos[1].TotalNecessidades, arvore.Objetivos[1].TotalMetas));
        Assert.Empty(arvore.Objetivos[1].Orgaos);
        Assert.Empty(arvore.Objetivos[1].Indicadores);
    }

    [Fact]
    public async Task FiltroPorOrgao_EQuemVe()
    {
        await CenarioAsync();

        var soSes = await Paineis.ArvoreAsync(OrgaoSes.Id, await ContextoDe(UserPeCgtic));
        var objetivo = soSes.Objetivos[0];
        Assert.Equal("SES", Assert.Single(objetivo.Orgaos).Sigla);
        Assert.Equal((1, 1), (objetivo.TotalNecessidades, objetivo.TotalMetas));
        // Os indicadores são do PETIC-DF: não dependem do filtro
        Assert.Equal(2, objetivo.Indicadores.Count);

        Assert.Equal(Codigo(ErrorCode.PeOrgaoNaoEncontrado), await ErroAsync(async () => await Paineis.ArvoreAsync(999999, await Sgdi())));
        foreach (var user in new[] { UserOrgaoSes, UserConsultaSes, UserSemPapel })
            Assert.Equal(Codigo(ErrorCode.PeSemPermissao), await ErroAsync(async () => await Paineis.ArvoreAsync(null, await ContextoDe(user))));
        Assert.Equal(2, (await Paineis.ArvoreAsync(null, await AdminGeral())).Objetivos.Count);
    }

    [Fact]
    public async Task LigacaoComAVersaoAnterior_ContaNoObjetivoDeMesmoCodigo()
    {
        var (_, oe01, _) = await CenarioAsync();

        // A versão 2.0 copia os objetivos (mesmos códigos); as ligações do PDTIC continuam na 1.0
        var rascunho = await RascunhoAsync("PETIC-DF 2027-2030");
        await AprovarAsync(rascunho.Id);

        var arvore = await Paineis.ArvoreAsync(null, await Sgdi());
        Assert.Equal("2.0", arvore.Petic!.Versao);
        var objetivo = arvore.Objetivos.Single(o => o.Codigo == "OE01");
        Assert.NotEqual(oe01, objetivo.RegistroId);
        Assert.Equal((2, 1), (objetivo.TotalNecessidades, objetivo.TotalMetas));
        // Os indicadores copiados apontam para o objetivo da 2.0
        Assert.Equal(2, objetivo.Indicadores.Count);
    }
}
