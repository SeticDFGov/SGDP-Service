using api.Planejamento;
using Microsoft.EntityFrameworkCore;
using Models.Planejamento;
using service;
using service.Planejamento;
using Xunit;

namespace test.planejamento;

/// <summary>
/// Publicação, acompanhamento e encerramento do PDTIC (E7, rodada A): publicar só o aprovado,
/// com a data e o endereço da seção da publicação (400 com Campos sem eles), a versão aprovada
/// do documento vira publicada; a aprovação do plano de acompanhamento (4.6) põe o PDTIC em
/// acompanhamento; encerrar pela equipe (com a aprovação da autoridade máxima) ou pelo
/// administrador (com o motivo, depois do fim da vigência); quem pode cada coisa.
/// </summary>
public class PePublicacaoEncerramentoTest : PeAprovacaoTestBase
{
    private async Task<PePdticResponse> AprovadoAsync()
    {
        var pdtic = await ProntoParaEnviarAsync();
        await EnviarAsync(pdtic.Id);
        await AprovarNoCgticAsync(pdtic.Id);
        return pdtic;
    }

    [Fact]
    public async Task Publicar_SoDepoisDaAprovacao_ComADataEOEndereco()
    {
        var pdtic = await ProntoParaEnviarAsync();
        Assert.Equal(Codigo(ErrorCode.PePdticSituacaoInvalida), await ErroAsync(async () => await Aprovacao.PublicarAsync(pdtic.Id, await Orgao())));
        // A seção da publicação só se grava depois da aprovação
        var ex = await Assert.ThrowsAsync<ApiException>(() => IncluirNoPdticAsync(pdtic.Id, "publicacao", new { data = "2026-09-15", endereco = Endereco }));
        Assert.Equal((int)ErrorCode.PePdticFechado, ex.Error.Code);
        Assert.Equal("A publicação é registrada depois da aprovação do CGTIC.", ex.Error.Message);

        await EnviarAsync(pdtic.Id);
        await AprovarNoCgticAsync(pdtic.Id);

        // Sem a seção: 400 com os dois campos (a data e o endereço)
        var (codigo, campos) = await ValidacaoAsync(async () => await Aprovacao.PublicarAsync(pdtic.Id, await Orgao()));
        Assert.Equal(Codigo(ErrorCode.PePublicacaoIncompleta), codigo);
        Assert.Equal(new[] { "data", "endereco" }, campos.Keys.OrderBy(k => k));
        Assert.Equal("Informe a data da publicação.", campos["data"]);
        Assert.Equal("Informe o endereço da íntegra do PDTIC na internet.", campos["endereco"]);
        Assert.Equal(PeDominios.SituacaoPdtic.Aprovado, PdticNoBanco(pdtic.Id).Situacao);
        var registro = await IncluirNoPdticAsync(pdtic.Id, "publicacao", new { data = "2026-09-15", endereco = Endereco });

        var publicado = await Aprovacao.PublicarAsync(pdtic.Id, await Orgao());
        Assert.Equal(PeDominios.SituacaoPdtic.Publicado, publicado.Situacao);
        Assert.NotNull(publicado.PublicadoEm);
        Assert.False(publicado.PodeEditar);
        // A versão que o CGTIC aprovou (a enviada) é a publicada; o PDF não muda
        Assert.Equal(new[] { (2, "publicada"), (1, "minuta") },
            (await Documentos.VersoesAsync(pdtic.Id, await Orgao())).Select(v => (v.Numero, v.Situacao)));

        // Publicado: a publicação ainda se corrige; publicar de novo, não
        await Registros.AtualizarAsync(PeDono.DoPdtic(pdtic.Id), "publicacao", registro.Id,
            Salvar(new { data = "2026-09-16", endereco = Endereco + "/2026" }), await Orgao());
        Assert.Equal(Codigo(ErrorCode.PePdticSituacaoInvalida), await ErroAsync(async () => await Aprovacao.PublicarAsync(pdtic.Id, await Orgao())));
    }

    [Fact]
    public async Task Publicar_SoAEquipeDoOrgao()
    {
        var pdtic = await AprovadoAsync();
        await IncluirNoPdticAsync(pdtic.Id, "publicacao", new { data = "2026-09-15", endereco = Endereco });
        foreach (var user in new[] { UserConsultaSes, UserPeSgdi, UserPeCgtic, UserPeAdmin, UserOrgaoSeec })
            Assert.Equal(Codigo(ErrorCode.PeSemPermissao), await ErroAsync(async () => await Aprovacao.PublicarAsync(pdtic.Id, await ContextoDe(user))));
        Assert.Equal(PeDominios.SituacaoPdtic.Publicado, (await Aprovacao.PublicarAsync(pdtic.Id, await ContextoDe(UserAdminGeral))).Situacao);
    }

    [Fact]
    public async Task AprovacaoDoPlanoDeAcompanhamento_ComAprovado_PoeOPdticEmAcompanhamento()
    {
        // O passo 4.6 não está no Básico: ligado para a SES por ajuste
        await AjustarPassosAsync(OrgaoSes, ("plano-acompanhamento.aprovacao-acompanhamento", "opcional"));
        var pdtic = await PublicadoAsync();
        var dono = PeDono.DoPdtic(pdtic.Id);

        var registro = await IncluirNoPdticAsync(pdtic.Id, "aprovacao_plano_acompanhamento", new { decisao = "devolvido", data = "2026-10-01" });
        Assert.Equal(PeDominios.SituacaoPdtic.Publicado, PdticNoBanco(pdtic.Id).Situacao);
        Assert.Contains(PePdticService.AvisoDevolvido, (await PassoDaSituacaoAsync(pdtic.Id, "plano-acompanhamento.aprovacao-acompanhamento")).Avisos);

        await Registros.AtualizarAsync(dono, "aprovacao_plano_acompanhamento", registro.Id, Salvar(new { decisao = "aprovado", data = "2026-10-05" }), await Orgao());
        Assert.Equal(PeDominios.SituacaoPdtic.EmAcompanhamento, PdticNoBanco(pdtic.Id).Situacao);
        Assert.Equal(PeDominios.SituacaoPasso.Feito, (await PassoDaSituacaoAsync(pdtic.Id, "plano-acompanhamento.aprovacao-acompanhamento")).Situacao);

        // Em acompanhamento: a publicação não muda mais; as etapas 4 a 7 continuam editáveis
        var publicacao = (await Registros.ListarAsync(dono, "publicacao", await Orgao()));
        Assert.False(publicacao.PodeEditar);
        Assert.True((await Registros.ListarAsync(dono, "responsavel_acompanhamento", await Orgao())).PodeEditar);
    }

    [Fact]
    public async Task Encerrar_PelaEquipe_ComAAprovacaoDaAutoridadeMaxima()
    {
        var pdtic = await PublicadoAsync();
        var numero = (await PassoDaSituacaoAsync(pdtic.Id, "fechamento.aprovacao-autoridade")).Numero;

        var ex = await Assert.ThrowsAsync<ApiException>(async () => await Aprovacao.EncerrarAsync(pdtic.Id, new PeEncerrarDTO(), await Orgao()));
        Assert.Equal((int)ErrorCode.PeEncerramentoRecusado, ex.Error.Code);
        Assert.Equal($"Para encerrar o PDTIC, registre a aprovação da autoridade máxima (passo {numero}) com a decisão \"Aprovado\".", ex.Error.Message);

        await IncluirNoPdticAsync(pdtic.Id, "aprovacao_resultados_autoridade", new { decisao = "devolvido", data = "2029-12-10" });
        Assert.Equal(Codigo(ErrorCode.PeEncerramentoRecusado), await ErroAsync(async () => await Aprovacao.EncerrarAsync(pdtic.Id, new PeEncerrarDTO(), await Orgao())));
        var registro = (await Registros.ListarAsync(PeDono.DoPdtic(pdtic.Id), "aprovacao_resultados_autoridade", await Orgao())).Registros.Single();
        await Registros.AtualizarAsync(PeDono.DoPdtic(pdtic.Id), "aprovacao_resultados_autoridade", registro.Id,
            Salvar(new { decisao = "aprovado", data = "2029-12-15" }), await Orgao());

        var encerrado = await Aprovacao.EncerrarAsync(pdtic.Id, new PeEncerrarDTO(), await Orgao());
        Assert.Equal(PeDominios.SituacaoPdtic.Encerrado, encerrado.Situacao);
        Assert.NotNull(encerrado.EncerradoEm);
        Assert.Null(encerrado.EncerramentoMotivo);

        // Encerrado: nada muda, nada é recomendado, e o órgão pode começar o ciclo seguinte (2.0)
        Assert.Null((await SituacaoAsync(pdtic.Id)).ProximoPasso);
        Assert.All((await SituacaoAsync(pdtic.Id)).Passos, p => Assert.False(p.PodeEditar));
        Assert.Equal(Codigo(ErrorCode.PePdticSituacaoInvalida), await ErroAsync(async () => await Aprovacao.EncerrarAsync(pdtic.Id, new PeEncerrarDTO(), await Orgao())));
        Assert.Null(await Pdtics.AtualAsync(null, await Orgao()));
        Assert.Equal("2.0", (await AbrirSesAsync()).Versao);
    }

    [Fact]
    public async Task Encerrar_PeloAdministrador_ComOMotivo_DepoisDoFimDaVigencia()
    {
        var pdtic = await PublicadoAsync();
        var admin = await Admin();

        Assert.Equal(Codigo(ErrorCode.PeJustificativaObrigatoria), await ErroAsync(() => Aprovacao.EncerrarAsync(pdtic.Id, new PeEncerrarDTO(), admin)));
        var ex = await Assert.ThrowsAsync<ApiException>(() => Aprovacao.EncerrarAsync(pdtic.Id, new PeEncerrarDTO { Motivo = "Vigência vencida." }, admin));
        Assert.Equal((int)ErrorCode.PeEncerramentoRecusado, ex.Error.Code);
        Assert.Equal("A vigência do PDTIC vai até 31/12/2029. O administrador encerra o PDTIC só depois do fim da vigência.", ex.Error.Message);

        Situacao(pdtic.Id, PeDominios.SituacaoPdtic.Publicado, new DateOnly(2025, 12, 31));
        var encerrado = await Aprovacao.EncerrarAsync(pdtic.Id, new PeEncerrarDTO { Motivo = "  O órgão não fechou o ciclo.  " }, admin);
        Assert.Equal(PeDominios.SituacaoPdtic.Encerrado, encerrado.Situacao);
        Assert.Equal("O órgão não fechou o ciclo.", encerrado.EncerramentoMotivo);
        Assert.Equal(UserPeAdmin.Email, PdticNoBanco(pdtic.Id).AlteradoPor);
    }

    [Fact]
    public async Task Encerrar_OAdministradorEncerraAElaboracaoVencida_MasNaoOQueEstaComOCgtic()
    {
        var pdtic = await ProntoParaEnviarAsync();
        Situacao(pdtic.Id, PeDominios.SituacaoPdtic.EmAprovacao, new DateOnly(2025, 12, 31));
        Assert.Equal(Codigo(ErrorCode.PePdticSituacaoInvalida), await ErroAsync(async () =>
            await Aprovacao.EncerrarAsync(pdtic.Id, new PeEncerrarDTO { Motivo = "Abandonado." }, await ContextoDe(UserAdminGeral))));

        Situacao(pdtic.Id, PeDominios.SituacaoPdtic.EmElaboracao);
        var encerrado = await Aprovacao.EncerrarAsync(pdtic.Id, new PeEncerrarDTO { Motivo = "Abandonado." }, await ContextoDe(UserAdminGeral));
        Assert.Equal(PeDominios.SituacaoPdtic.Encerrado, encerrado.Situacao);
    }

    [Fact]
    public async Task Encerrar_QuemNaoEncerra_403()
    {
        var pdtic = await PublicadoAsync();
        Situacao(pdtic.Id, PeDominios.SituacaoPdtic.Publicado, new DateOnly(2025, 12, 31));
        foreach (var user in new[] { UserConsultaSes, UserPeSgdi, UserPeCgtic, UserOrgaoSeec })
            Assert.Equal(Codigo(ErrorCode.PeSemPermissao), await ErroAsync(async () =>
                await Aprovacao.EncerrarAsync(pdtic.Id, new PeEncerrarDTO { Motivo = "x" }, await ContextoDe(user))));
        // Motivo longo demais: 400
        Assert.Equal(Codigo(ErrorCode.PeDadosInvalidos), await ErroAsync(async () =>
            await Aprovacao.EncerrarAsync(pdtic.Id, new PeEncerrarDTO { Motivo = new string('a', 1001) }, await Admin())));
    }
}
