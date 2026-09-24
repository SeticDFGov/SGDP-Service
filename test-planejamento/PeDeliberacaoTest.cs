using api.Planejamento;
using Models.Planejamento;
using service;
using Xunit;

namespace test.planejamento;

/// <summary>
/// Deliberações do CGTIC (E3): a fila (aguardando primeiro, da mais antiga; depois as
/// decididas, da mais nova), os filtros, a paginação e as regras da decisão.
/// </summary>
public class PeDeliberacaoTest : PeReferenciaisTestBase
{
    /// <summary>Uma deliberação de PDTIC aguardando, sem o PDTIC (o envio de verdade está no PeEnvioTest).</summary>
    private PeDeliberacao DeliberacaoDePdtic(long objetoId, DateTime enviadoEm)
    {
        var deliberacao = new PeDeliberacao
        {
            ObjetoTipo = PeDominios.ObjetoDeliberacao.Pdtic,
            ObjetoId = objetoId,
            VersaoObjeto = "1.0",
            EnviadoEm = enviadoEm,
            EnviadoPor = "orgao@saude.df.gov.br",
            Situacao = PeDominios.SituacaoDeliberacao.Aguardando,
            CriadoEm = enviadoEm,
            CriadoPor = "orgao@saude.df.gov.br"
        };
        Context.PeDeliberacoes.Add(deliberacao);
        Context.SaveChanges();
        return deliberacao;
    }

    [Fact]
    public async Task Fila_AguardandoPrimeiroDaMaisAntiga_DepoisAsDecididasDaMaisNova()
    {
        var ctx = await Admin();
        var rascunho = await RascunhoAsync();
        await PreencherMinimoAsync(rascunho.Id);
        var devolvida = (await Petics.EnviarAsync(rascunho.Id, ctx)).Deliberacao!;
        await Deliberacoes.DecidirAsync(devolvida.Id, new PeDecidirDTO { Decisao = "devolvido", Observacao = "Ajustar." }, await Cgtic());
        var aprovada = (await Petics.EnviarAsync(rascunho.Id, ctx)).Deliberacao!;
        await Deliberacoes.DecidirAsync(aprovada.Id, new PeDecidirDTO { Decisao = "aprovado", AtoNumero = "1", AtoData = "2026-09-01" }, await Cgtic());
        var novo = await RascunhoAsync("PETIC-DF 2", copiar: true);
        var aguardandoPetic = (await Petics.EnviarAsync(novo.Id, ctx)).Deliberacao!;
        var aguardandoPdtic = DeliberacaoDePdtic(7, DateTime.UtcNow.AddDays(1));

        var fila = await Deliberacoes.ListarAsync(new PeDeliberacoesConsulta());

        Assert.Equal(new[] { aguardandoPetic.Id, aguardandoPdtic.Id, aprovada.Id, devolvida.Id }, fila.Items.Select(d => d.Id));
        Assert.Equal(4, fila.TotalItems);
        Assert.Equal("PETIC-DF 2.0", fila.Items[0].Titulo);
        Assert.Equal("aprovado", fila.Items[2].Situacao);
        Assert.Equal("Ajustar.", fila.Items[3].Observacao);

        Assert.Equal(new[] { aguardandoPetic.Id, aguardandoPdtic.Id },
            (await Deliberacoes.ListarAsync(new PeDeliberacoesConsulta { Situacao = "aguardando" })).Items.Select(d => d.Id));
        Assert.Equal(new[] { aguardandoPdtic.Id },
            (await Deliberacoes.ListarAsync(new PeDeliberacoesConsulta { ObjetoTipo = "pdtic" })).Items.Select(d => d.Id));
        Assert.Empty((await Deliberacoes.ListarAsync(new PeDeliberacoesConsulta { Situacao = "qualquer" })).Items);
        Assert.Empty((await Deliberacoes.ListarAsync(new PeDeliberacoesConsulta { ObjetoTipo = "outro" })).Items);

        var pagina = await Deliberacoes.ListarAsync(new PeDeliberacoesConsulta { Page = 2, PageSize = 3 });
        Assert.Equal(new[] { devolvida.Id }, pagina.Items.Select(d => d.Id));
        Assert.Equal(2, pagina.TotalPages);
        var saneada = await Deliberacoes.ListarAsync(new PeDeliberacoesConsulta { Page = -5, PageSize = 0 });
        Assert.Equal(1, saneada.CurrentPage);
        Assert.Equal(20, saneada.PageSize);
    }

    [Fact]
    public async Task Decidir_Validacoes_DaDecisaoDoAtoEDoSei()
    {
        var ctx = await Admin();
        var cgtic = await Cgtic();
        var rascunho = await RascunhoAsync();
        await PreencherMinimoAsync(rascunho.Id);
        var id = (await Petics.EnviarAsync(rascunho.Id, ctx)).Deliberacao!.Id;

        async Task<int> Decidir(PeDecidirDTO dto) => await ErroAsync(() => Deliberacoes.DecidirAsync(id, dto, cgtic));
        Assert.Equal(Codigo(ErrorCode.PeDecisaoInvalida), await Decidir(new PeDecidirDTO { Decisao = "talvez" }));
        Assert.Equal(Codigo(ErrorCode.PeDecisaoInvalida), await Decidir(new PeDecidirDTO { Decisao = "aprovado", AtoData = "2026-09-01" }));
        Assert.Equal(Codigo(ErrorCode.PeDecisaoInvalida), await Decidir(new PeDecidirDTO { Decisao = "aprovado", AtoNumero = "1" }));
        Assert.Equal(Codigo(ErrorCode.PeDecisaoInvalida), await Decidir(new PeDecidirDTO
        {
            Decisao = "aprovado", AtoNumero = "1", AtoData = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(10)).ToString("yyyy-MM-dd")
        }));
        Assert.Equal(Codigo(ErrorCode.PeDecisaoInvalida), await Decidir(new PeDecidirDTO
        {
            Decisao = "aprovado", AtoNumero = "1", AtoData = "2026-09-01", Sei = "123"
        }));
        Assert.Equal(Codigo(ErrorCode.PeDecisaoInvalida), await Decidir(new PeDecidirDTO
        {
            Decisao = "devolvido", Observacao = new string('x', 2001)
        }));
        Assert.Equal("aguardando", (await Petics.ObterAsync(rascunho.Id, ctx)).Deliberacao!.Situacao);

        Assert.Equal(Codigo(ErrorCode.PeDeliberacaoNaoEncontrada), await ErroAsync(() =>
            Deliberacoes.DecidirAsync(999, new PeDecidirDTO { Decisao = "devolvido", Observacao = "x" }, cgtic)));

        // A deliberação de PDTIC (E7) sem o PDTIC (o objeto 3 não existe): 404
        var pdtic = DeliberacaoDePdtic(3, DateTime.UtcNow);
        Assert.Equal(Codigo(ErrorCode.PePdticNaoEncontrado), await ErroAsync(() =>
            Deliberacoes.DecidirAsync(pdtic.Id, new PeDecidirDTO { Decisao = "devolvido", Observacao = "x" }, cgtic)));
    }
}
