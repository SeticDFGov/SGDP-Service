using Models.Contratacoes;
using service.Contratacoes;
using Xunit;

namespace test.contratacoes;

/// <summary>
/// Painel: contagens (as 7 situações sempre presentes), médias com amostra
/// conhecida e lista de gargalos.
/// </summary>
public class CtrPainelTest : CtrTestBase
{
    private readonly CtrProcessoService _service;

    public CtrPainelTest()
    {
        _service = NovoProcessoService();

        // Concluído em 10 dias: SGDI -> SUBGD (2), SUBGD -> UGTIC (3),
        // UGTIC -> Gab (4), Gab -> órgão (1), chegada -> conclusão (10)
        SemearProcesso("04044-00000001/2026-11", p =>
        {
            p.OrgaoSigla = "SEEC";
            p.CategoriaObjeto = CtrDominios.CategoriaObjeto.InfraestruturaRede;
            p.ChegadaSgdi = DiasAtras(30);
            p.ChegadaSubgd = DiasAtras(28);
            p.ChegadaUgtic = DiasAtras(25);
            p.RetornoGabSgdi = DiasAtras(21);
            p.RetornoOrgao = DiasAtras(20);
        });

        // Concluído em 20 dias: SGDI -> SUBGD (4), SUBGD -> UGTIC (5),
        // UGTIC -> Gab (8), Gab -> órgão (3)
        SemearProcesso("04044-00000002/2026-12", p =>
        {
            p.OrgaoSigla = "SEEC";
            p.CategoriaObjeto = CtrDominios.CategoriaObjeto.InfraestruturaRede;
            p.ChegadaSgdi = DiasAtras(60);
            p.ChegadaSubgd = DiasAtras(56);
            p.ChegadaUgtic = DiasAtras(51);
            p.RetornoGabSgdi = DiasAtras(43);
            p.RetornoOrgao = DiasAtras(40);
        });

        // Parado na SUBGD há 45 dias (gargalo)
        SemearProcesso("00080-00000003/2026-13", p =>
        {
            p.OrgaoSigla = "SEEDF";
            p.CategoriaObjeto = CtrDominios.CategoriaObjeto.SistemasGestao;
            p.ChegadaSgdi = DiasAtras(50);
            p.ChegadaSubgd = DiasAtras(45);
        });

        // Parado na SGDI há 3 dias (não é gargalo com o limite padrão)
        SemearProcesso("00080-00000004/2026-14", p =>
        {
            p.OrgaoSigla = "SEEDF";
            p.CategoriaObjeto = CtrDominios.CategoriaObjeto.SistemasGestao;
            p.ChegadaSgdi = DiasAtras(3);
        });

        // Restituído há 60 dias: fora dos gargalos, ainda que parado
        SemearProcesso("00480-00000005/2026-15", p =>
        {
            p.OrgaoSigla = "CGDF";
            p.CategoriaObjeto = CtrDominios.CategoriaObjeto.Outros;
            p.ChegadaSgdi = DiasAtras(70);
            p.Restituido = true;
            p.RestituidoEm = DiasAtras(60);
            p.RestituidoMotivo = "Processo restituído ao órgão";
        });

        // Excluído: não entra em nada
        SemearProcesso("00480-00000006/2026-16", p =>
        {
            p.OrgaoSigla = "SLU";
            p.Ativo = false;
            p.ChegadaSgdi = DiasAtras(90);
        });
    }

    [Fact]
    public async Task Painel_ContaSoOsAtivosECobreAsSeteSituacoes()
    {
        var painel = await _service.MontarPainelAsync(15);

        Assert.Equal(5, painel.TotalAtivos);
        Assert.Equal(15, painel.LimiteDias);
        Assert.Equal(7, painel.PorSituacao.Count);
        Assert.Equal(CtrDominios.Situacao.Todos.OrderBy(s => s),
            painel.PorSituacao.Select(c => c.Chave).OrderBy(s => s));

        Assert.Equal(2, painel.PorSituacao.Single(c => c.Chave == CtrDominios.Situacao.Concluido).Quantidade);
        Assert.Equal(1, painel.PorSituacao.Single(c => c.Chave == CtrDominios.Situacao.EmAnaliseSubgd).Quantidade);
        Assert.Equal(1, painel.PorSituacao.Single(c => c.Chave == CtrDominios.Situacao.EmAnaliseSgdi).Quantidade);
        Assert.Equal(1, painel.PorSituacao.Single(c => c.Chave == CtrDominios.Situacao.Restituido).Quantidade);
        // As situações sem nenhum processo aparecem com zero
        Assert.Equal(0, painel.PorSituacao.Single(c => c.Chave == CtrDominios.Situacao.EmAnaliseUgtic).Quantidade);
    }

    [Fact]
    public async Task Painel_AgrupaPorCategoriaEOrgao()
    {
        var painel = await _service.MontarPainelAsync(15);

        Assert.Equal(CtrDominios.CategoriaObjeto.InfraestruturaRede, painel.PorCategoria[0].Chave);
        Assert.Equal(2, painel.PorCategoria[0].Quantidade);

        Assert.Equal(2, painel.PorOrgao.Single(c => c.Chave == "SEEC").Quantidade);
        Assert.Equal(2, painel.PorOrgao.Single(c => c.Chave == "SEEDF").Quantidade);
        Assert.Equal(1, painel.PorOrgao.Single(c => c.Chave == "CGDF").Quantidade);
    }

    [Fact]
    public async Task Painel_MediasSoContamQuemTemOsDoisExtremos()
    {
        var painel = await _service.MontarPainelAsync(15);
        var medias = painel.TemposMedios;

        // SGDI -> SUBGD: 2, 4 e 5 dias (o restituído e o parado na SGDI não entram)
        Assert.Equal(3.7d, medias.SgdiParaSubgd);
        // SUBGD -> UGTIC: 3 e 5
        Assert.Equal(4d, medias.SubgdParaUgtic);
        // UGTIC -> Gab: 4 e 8
        Assert.Equal(6d, medias.UgticParaRetornoGab);
        // Gab -> órgão: 1 e 3
        Assert.Equal(2d, medias.RetornoGabParaOrgao);
        // Chegada -> conclusão: 10 e 20
        Assert.Equal(15d, medias.ChegadaParaConclusao);
    }

    [Fact]
    public async Task Painel_SemAmostra_DevolveMediaNula()
    {
        Context.CtrProcessos.RemoveRange(Context.CtrProcessos);
        await Context.SaveChangesAsync();
        SemearProcesso("04044-00000009/2026-19");

        var painel = await _service.MontarPainelAsync(15);

        Assert.Equal(1, painel.TotalAtivos);
        Assert.Null(painel.TemposMedios.SgdiParaSubgd);
        Assert.Null(painel.TemposMedios.ChegadaParaConclusao);
    }

    [Fact]
    public async Task Painel_GargalosIgnoramConcluidosERestituidos()
    {
        var painel = await _service.MontarPainelAsync(15);

        Assert.Single(painel.Gargalos);
        Assert.Equal("00080-00000003/2026-13", painel.Gargalos[0].NumeroProcesso);
        Assert.Equal(45, painel.Gargalos[0].DiasSemMovimento);
    }

    [Fact]
    public async Task Painel_LimiteDeDiasMuda_AListaDeGargalos()
    {
        var todos = await _service.MontarPainelAsync(0);

        // Com limite zero entram os dois em análise; concluídos e restituído continuam fora
        Assert.Equal(2, todos.Gargalos.Count);
        Assert.Equal(45, todos.Gargalos[0].DiasSemMovimento); // do mais parado para o menos
        Assert.Equal(3, todos.Gargalos[1].DiasSemMovimento);

        // Limite negativo é saneado para zero
        var negativo = await _service.MontarPainelAsync(-10);
        Assert.Equal(0, negativo.LimiteDias);
        Assert.Equal(2, negativo.Gargalos.Count);
    }
}
