using api.Planejamento;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace test.planejamento;

/// <summary>
/// Nível e ajustes de cada órgão (PeOrgaoService): lista com o nível ou o padrão, troca
/// de nível com justificativa obrigatória e histórico, ajustes gravados como a lista
/// inteira (nulo ou ausente sai), travado nunca desliga e só itens do PDTIC.
/// </summary>
public class PeOrgaosTest : PeModeloTestBase
{
    // ── Lista ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Lista_OrgaosAtivos_ComONivelOuOPadrao_EFiltro()
    {
        await DefinirNivelDoOrgaoAsync(OrgaoSeec, "avancado");

        var lista = await Orgaos.ListarAsync(new PeOrgaosConsulta());

        Assert.Equal(new[] { "SEEC", "SES" }, lista.Select(o => o.Sigla));
        var seec = lista[0];
        Assert.Equal("Avançado", seec.NivelNome);
        Assert.False(seec.NivelPadrao);
        var ses = lista[1];
        Assert.Equal("Básico", ses.NivelNome);
        Assert.True(ses.NivelPadrao);
        Assert.Equal(0, ses.Ajustes);

        Assert.Equal(new[] { "SES" }, (await Orgaos.ListarAsync(new PeOrgaosConsulta { Filtro = "saúde" })).Select(o => o.Sigla));
        Assert.Equal(new[] { "SEEC" }, (await Orgaos.ListarAsync(new PeOrgaosConsulta { Filtro = "seec" })).Select(o => o.Sigla));
    }

    // ── Nível ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task TrocarNivel_ExigeJustificativa_ENivelAtivoQueExista()
    {
        Assert.Equal(Codigo(ErrorCode.PeJustificativaObrigatoria), await ErroAsync(() => Orgaos.DefinirNivelAsync(OrgaoSes.Id,
            new PeOrgaoNivelDTO { NivelId = NivelId("avancado"), Justificativa = "  " }, EmailAdmin)));
        Assert.Equal(Codigo(ErrorCode.PeDadosInvalidos), await ErroAsync(() => Orgaos.DefinirNivelAsync(OrgaoSes.Id,
            new PeOrgaoNivelDTO { NivelId = 999_999, Justificativa = "Teste" }, EmailAdmin)));
        Assert.Equal(Codigo(ErrorCode.PeDadosInvalidos), await ErroAsync(() => Orgaos.DefinirNivelAsync(OrgaoSes.Id,
            new PeOrgaoNivelDTO { Justificativa = "Teste" }, EmailAdmin)));
        Assert.Equal(Codigo(ErrorCode.PeOrgaoNaoEncontrado), await ErroAsync(() => Orgaos.DefinirNivelAsync(999_999,
            new PeOrgaoNivelDTO { NivelId = NivelId("avancado"), Justificativa = "Teste" }, EmailAdmin)));

        await Modelo.AtualizarNivelAsync(NivelId("intermediario"),
            new PeNivelAtualizarDTO { Ativo = false, Informados = new HashSet<string> { "Ativo" } }, EmailAdmin);
        Assert.Equal(Codigo(ErrorCode.PeNivelInativo), await ErroAsync(() => Orgaos.DefinirNivelAsync(OrgaoSes.Id,
            new PeOrgaoNivelDTO { NivelId = NivelId("intermediario"), Justificativa = "Teste" }, EmailAdmin)));
        Assert.Empty(Context.PeOrgaosConfig);
    }

    [Fact]
    public async Task TrocarNivel_GravaEDevolveOItem_ComHistorico()
    {
        var item = await Orgaos.DefinirNivelAsync(OrgaoSes.Id,
            new PeOrgaoNivelDTO { NivelId = NivelId("intermediario"), Justificativa = "Já tem PDTIC anterior" }, EmailAdmin);

        Assert.Equal("Intermediário", item.NivelNome);
        Assert.False(item.NivelPadrao);
        var config = Context.PeOrgaosConfig.AsNoTracking().Single(c => c.OrgaoId == OrgaoSes.Id);
        Assert.Equal("Já tem PDTIC anterior", config.Justificativa);
        Assert.Equal(EmailAdmin, config.DefinidoPor);

        await Orgaos.DefinirNivelAsync(OrgaoSes.Id,
            new PeOrgaoNivelDTO { NivelId = NivelId("avancado"), Justificativa = "Maturidade subiu" }, EmailAdmin);
        // Mesmo nível e mesma justificativa: nada muda, nada entra no histórico
        await Orgaos.DefinirNivelAsync(OrgaoSes.Id,
            new PeOrgaoNivelDTO { NivelId = NivelId("avancado"), Justificativa = "Maturidade subiu" }, EmailAdmin);

        var historico = await Orgaos.HistoricoNivelAsync(OrgaoSes.Id);

        Assert.Equal(2, historico.Count);
        Assert.Equal("Intermediário", historico[0].NivelAnterior);
        Assert.False(historico[0].NivelAnteriorPadrao);
        Assert.Equal("Avançado", historico[0].NivelNovo);
        Assert.Equal("Maturidade subiu", historico[0].Justificativa);
        Assert.Equal("Básico", historico[1].NivelAnterior);
        Assert.True(historico[1].NivelAnteriorPadrao);
        Assert.Equal(NivelId("intermediario"), historico[1].NivelNovoId);
        Assert.Equal(EmailAdmin, historico[1].DefinidoPor);
    }

    // ── Ajustes ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task Ajustes_ListaInteira_NuloOuAusenteSai_ComHistorico()
    {
        var nomes = Passo("preparacao.nomes").Id;
        var equipe = Secao("equipe_designacao").Id;
        var cargo = Campo("equipe_elaboracao", "cargo").Id;

        var gravados = await Orgaos.DefinirAjustesAsync(OrgaoSes.Id, new List<PeOrgaoAjusteDTO>
        {
            new() { AlvoTipo = "passo", AlvoId = nomes, Situacao = "opcional", Justificativa = "Usa os nomes do cadastro" },
            new() { AlvoTipo = "secao", AlvoId = equipe, Situacao = "obrigatorio" },
            new() { AlvoTipo = "campo", AlvoId = cargo, Situacao = "obrigatorio" }
        }, EmailAdmin);

        Assert.Equal(3, gravados.Count);
        Assert.Equal("Defina os nomes do órgão: comitê, equipes, autoridade máxima, unidade de TIC",
            gravados.Single(a => a.AlvoTipo == "passo").AlvoTitulo);
        Assert.Equal(3, (await Orgaos.ListarAsync(new PeOrgaosConsulta { Filtro = "SES" })).Single().Ajustes);

        // Lista nova: o campo sai por ausência, a seção por situação nula, o passo muda
        var depois = await Orgaos.DefinirAjustesAsync(OrgaoSes.Id, new List<PeOrgaoAjusteDTO>
        {
            new() { AlvoTipo = "passo", AlvoId = nomes, Situacao = "desligado", Justificativa = "Não se aplica" },
            new() { AlvoTipo = "secao", AlvoId = equipe, Situacao = null }
        }, EmailAdmin);

        var unico = Assert.Single(depois);
        Assert.Equal("desligado", unico.Situacao);
        Assert.Equal(new[] { "criacao", "criacao", "criacao", "remocao", "remocao", "alteracao" },
            HistoricoDe("orgao_ajuste", OrgaoSes.Id).Select(h => h.Acao));

        // Lista vazia tira tudo
        Assert.Empty(await Orgaos.DefinirAjustesAsync(OrgaoSes.Id, new List<PeOrgaoAjusteDTO>(), EmailAdmin));
    }

    [Fact]
    public async Task Ajuste_TravadoNaoDesliga()
    {
        Assert.Equal(Codigo(ErrorCode.PeItemTravado), await ErroAsync(() => Orgaos.DefinirAjustesAsync(OrgaoSes.Id,
            new List<PeOrgaoAjusteDTO> { new() { AlvoTipo = "passo", AlvoId = Passo("planejamento.contratacoes").Id, Situacao = "desligado" } },
            EmailAdmin)));
        Assert.Equal(Codigo(ErrorCode.PeItemTravado), await ErroAsync(() => Orgaos.DefinirAjustesAsync(OrgaoSes.Id,
            new List<PeOrgaoAjusteDTO> { new() { AlvoTipo = "secao", AlvoId = Secao("necessidades").Id, Situacao = "desligado" } },
            EmailAdmin)));
        Assert.Equal(Codigo(ErrorCode.PeItemTravado), await ErroAsync(() => Orgaos.DefinirAjustesAsync(OrgaoSes.Id,
            new List<PeOrgaoAjusteDTO> { new() { AlvoTipo = "campo", AlvoId = Campo("necessidades", "descricao").Id, Situacao = "desligado" } },
            EmailAdmin)));
        Assert.Equal(Codigo(ErrorCode.PeItemTravado), await ErroAsync(() => Orgaos.DefinirAjustesAsync(OrgaoSes.Id,
            new List<PeOrgaoAjusteDTO> { new() { AlvoTipo = "campo", AlvoId = Campo("acoes", "tema").Id, Situacao = "desligado" } },
            EmailAdmin)));
        Assert.Empty(Context.PeOrgaosAjuste);

        // Travado pode ficar opcional para o órgão
        var opcional = await Orgaos.DefinirAjustesAsync(OrgaoSes.Id,
            new List<PeOrgaoAjusteDTO> { new() { AlvoTipo = "passo", AlvoId = Passo("planejamento.contratacoes").Id, Situacao = "opcional" } },
            EmailAdmin);
        Assert.Single(opcional);
    }

    [Fact]
    public async Task Ajuste_ItemQueNaoExisteOuForaDoPdtic_OuRepetido_400()
    {
        Assert.Equal(Codigo(ErrorCode.PeDadosInvalidos), await ErroAsync(() => Orgaos.DefinirAjustesAsync(OrgaoSes.Id,
            new List<PeOrgaoAjusteDTO> { new() { AlvoTipo = "passo", AlvoId = 999_999, Situacao = "opcional" } }, EmailAdmin)));
        Assert.Equal(Codigo(ErrorCode.PeDadosInvalidos), await ErroAsync(() => Orgaos.DefinirAjustesAsync(OrgaoSes.Id,
            new List<PeOrgaoAjusteDTO> { new() { AlvoTipo = "etapa", AlvoId = 1, Situacao = "opcional" } }, EmailAdmin)));
        Assert.Equal(Codigo(ErrorCode.PeDadosInvalidos), await ErroAsync(() => Orgaos.DefinirAjustesAsync(OrgaoSes.Id,
            new List<PeOrgaoAjusteDTO> { new() { AlvoTipo = "passo", AlvoId = Passo("preparacao.nomes").Id, Situacao = "talvez" } },
            EmailAdmin)));

        var nomes = Passo("preparacao.nomes").Id;
        Assert.Equal(Codigo(ErrorCode.PeDadosInvalidos), await ErroAsync(() => Orgaos.DefinirAjustesAsync(OrgaoSes.Id,
            new List<PeOrgaoAjusteDTO>
            {
                new() { AlvoTipo = "passo", AlvoId = nomes, Situacao = "opcional" },
                new() { AlvoTipo = "passo", AlvoId = nomes, Situacao = "desligado" }
            }, EmailAdmin)));

        var petic = await Modelo.CriarSecaoAsync(new PeSecaoCriarDTO { Escopo = "petic", Titulo = "Objetivos", Tipo = "tabela" }, EmailAdmin);
        Assert.Equal(Codigo(ErrorCode.PeDadosInvalidos), await ErroAsync(() => Orgaos.DefinirAjustesAsync(OrgaoSes.Id,
            new List<PeOrgaoAjusteDTO> { new() { AlvoTipo = "secao", AlvoId = petic.Id, Situacao = "opcional" } }, EmailAdmin)));
    }

    [Fact]
    public async Task Ajuste_DeItemApagadoDepois_FicaGuardadoEMarcado()
    {
        var passo = await Modelo.CriarPassoAsync(new PePassoCriarDTO
        {
            EtapaId = Passo("preparacao.nomes").EtapaId, Titulo = "Registre os parceiros", OQueFazer = "Registre."
        }, EmailAdmin);
        await Orgaos.DefinirAjustesAsync(OrgaoSes.Id,
            new List<PeOrgaoAjusteDTO> { new() { AlvoTipo = "passo", AlvoId = passo.Id, Situacao = "obrigatorio" } }, EmailAdmin);

        await Modelo.ExcluirPassoAsync(passo.Id, EmailAdmin);

        var ajuste = Assert.Single(await Orgaos.AjustesAsync(OrgaoSes.Id));
        Assert.True(ajuste.AlvoExcluido);
        Assert.Null(NaTrilha(await TrilhaAsync(OrgaoSes), passo.Chave));
    }
}
