using api.Planejamento;
using app.Auth;
using Microsoft.EntityFrameworkCore;
using Models.Planejamento;
using service;
using service.Planejamento;
using Xunit;

namespace test.planejamento;

/// <summary>
/// PDTIC dos órgãos (E4): abertura (um atual por órgão, versão 1.0 em elaboração; um ciclo
/// novo depois de encerrado vira 2.0), quem abre (equipe do órgão no próprio; admin geral em
/// qualquer um), leitura (órgão só o próprio; consulta e globais leem), lista dos atuais para
/// os papéis globais e a edição só em elaboração ou devolvido.
/// </summary>
public class PePdticTest : PePdticTestBase
{
    [Fact]
    public async Task Abrir_PelaEquipeDoOrgao_Versao10EmElaboracao_ComONivelDoOrgao()
    {
        var pdtic = await AbrirSesAsync();

        Assert.Equal("1.0", pdtic.Versao);
        Assert.Equal(PeDominios.SituacaoPdtic.EmElaboracao, pdtic.Situacao);
        Assert.Equal(OrgaoSes.Id, pdtic.OrgaoId);
        Assert.Equal("SES", pdtic.OrgaoSigla);
        Assert.Equal("Secretaria de Estado de Saúde", pdtic.OrgaoNome);
        Assert.Equal(NivelId("basico"), pdtic.NivelId);
        Assert.Equal("Básico", pdtic.NivelNome);
        Assert.True(pdtic.PodeEditar);
        Assert.False(pdtic.RegistradoExternamente);
        Assert.Null(pdtic.AnteriorId);
        Assert.Null(pdtic.VigenciaInicio);
        Assert.Equal(UserOrgaoSes.Email, pdtic.CriadoPor);

        // Um atual por órgão
        Assert.Equal(Codigo(ErrorCode.PePdticJaExiste), await ErroAsync(AbrirSesAsync));
        Assert.Single(Context.PePdtics.Where(p => p.OrgaoId == OrgaoSes.Id));

        // O nível de hoje do órgão
        await DefinirNivelDoOrgaoAsync(OrgaoSes, "avancado");
        var lido = await Pdtics.ObterAsync(pdtic.Id, await Orgao());
        Assert.Equal("Avançado", lido.NivelNome);
    }

    [Fact]
    public async Task Abrir_SoAEquipeDoOrgaoNoProprio_OuOAdminGeralEmQualquerUm()
    {
        foreach (var user in new[] { UserConsultaSes, UserPeSgdi, UserPeCgtic, UserPeAdmin })
            Assert.Equal(Codigo(ErrorCode.PeSemPermissao),
                await ErroAsync(async () => await Pdtics.AbrirAsync(new PePdticCriarDTO(), await ContextoDe(user))));

        // A equipe da SES não abre o da SEEC
        Assert.Equal(Codigo(ErrorCode.PeSemPermissao),
            await ErroAsync(async () => await Pdtics.AbrirAsync(new PePdticCriarDTO { OrgaoId = OrgaoSeec.Id }, await Orgao())));

        // Admin geral: precisa dizer o órgão
        var admin = await ContextoDe(UserAdminGeral);
        Assert.Equal(Codigo(ErrorCode.PeOrgaoObrigatorio), await ErroAsync(() => Pdtics.AbrirAsync(new PePdticCriarDTO(), admin)));
        var daSeec = await Pdtics.AbrirAsync(new PePdticCriarDTO { OrgaoId = OrgaoSeec.Id }, admin);
        Assert.Equal("SEEC", daSeec.OrgaoSigla);
        Assert.True(daSeec.PodeEditar);
        Assert.Equal(Codigo(ErrorCode.PeOrgaoNaoEncontrado),
            await ErroAsync(() => Pdtics.AbrirAsync(new PePdticCriarDTO { OrgaoId = 999 }, admin)));

        // Equipe de órgão sem órgão
        var semOrgao = NovoUser("sem.orgao@df.gov.br", "Sem Órgão", Perfis.Basico, UnidadeCentral);
        DarPapel(semOrgao, PapeisPlanejamento.Orgao);
        Assert.Equal(Codigo(ErrorCode.PeOrgaoNaoEncontrado),
            await ErroAsync(async () => await Pdtics.AbrirAsync(new PePdticCriarDTO(), await ContextoDe(semOrgao))));
    }

    [Fact]
    public async Task Atual_DoProprioOrgao_OuDoOrgaoPedido()
    {
        Assert.Null(await Pdtics.AtualAsync(null, await Orgao()));
        var pdtic = await AbrirSesAsync();

        Assert.Equal(pdtic.Id, (await Pdtics.AtualAsync(null, await Orgao()))!.Id);
        var consulta = (await Pdtics.AtualAsync(null, await ContextoDe(UserConsultaSes)))!;
        Assert.False(consulta.PodeEditar);
        Assert.Equal(pdtic.Id, (await Pdtics.AtualAsync(OrgaoSes.Id, await ContextoDe(UserPeSgdi)))!.Id);
        Assert.Null(await Pdtics.AtualAsync(OrgaoSeec.Id, await ContextoDe(UserPeSgdi)));

        // Papel global sem órgão precisa dizer qual; papel de órgão não pede outro órgão
        Assert.Equal(Codigo(ErrorCode.PeOrgaoObrigatorio), await ErroAsync(async () => await Pdtics.AtualAsync(null, await ContextoDe(UserPeSgdi))));
        Assert.Equal(Codigo(ErrorCode.PeSemPermissao), await ErroAsync(async () => await Pdtics.AtualAsync(OrgaoSeec.Id, await Orgao())));
    }

    [Fact]
    public async Task Ler_OrgaoSoOProprio_ConsultaEGlobaisLeem_SoAEquipeEOAdminEditam()
    {
        var pdtic = await AbrirSesAsync();

        foreach (var user in new[] { UserConsultaSes, UserPeSgdi, UserPeCgtic, UserPeAdmin })
            Assert.False((await Pdtics.ObterAsync(pdtic.Id, await ContextoDe(user))).PodeEditar);
        Assert.True((await Pdtics.ObterAsync(pdtic.Id, await ContextoDe(UserAdminGeral))).PodeEditar);

        Assert.Equal(Codigo(ErrorCode.PeSemPermissao), await ErroAsync(async () => await Pdtics.ObterAsync(pdtic.Id, await ContextoDe(UserOrgaoSeec))));
        Assert.Equal(Codigo(ErrorCode.PeSemPermissao), await ErroAsync(async () => await Pdtics.ObterAsync(pdtic.Id, await ContextoDe(UserSemPapel))));
        Assert.Equal(Codigo(ErrorCode.PePdticNaoEncontrado), await ErroAsync(async () => await Pdtics.ObterAsync(999, await Orgao())));
    }

    [Fact]
    public async Task Listar_OsAtuaisDeTodosOsOrgaos_ComFiltros_SoParaOsGlobais()
    {
        var ses = await AbrirSesAsync();
        await AbrirSeecAsync();
        var sgdi = await ContextoDe(UserPeSgdi);

        var todos = await Pdtics.ListarAsync(new PePdticConsulta(), sgdi);
        Assert.Equal(2, todos.TotalItems);
        Assert.Equal(new[] { "SEEC", "SES" }, todos.Items.Select(i => i.OrgaoSigla));
        Assert.All(todos.Items, i => Assert.False(i.PodeEditar));

        Assert.Equal("SES", Assert.Single((await Pdtics.ListarAsync(new PePdticConsulta { Filtro = "saúde" }, sgdi)).Items).OrgaoSigla);
        Assert.Equal(2, (await Pdtics.ListarAsync(new PePdticConsulta { Situacao = "em_elaboracao" }, sgdi)).TotalItems);
        Assert.Equal(0, (await Pdtics.ListarAsync(new PePdticConsulta { Situacao = "devolvido" }, sgdi)).TotalItems);
        Assert.Equal(0, (await Pdtics.ListarAsync(new PePdticConsulta { Situacao = "qualquer" }, sgdi)).TotalItems);
        Assert.Equal("SES", Assert.Single((await Pdtics.ListarAsync(new PePdticConsulta { PageSize = 1, Page = 2 }, sgdi)).Items).OrgaoSigla);

        foreach (var user in new[] { UserOrgaoSes, UserConsultaSes })
            Assert.Equal(Codigo(ErrorCode.PeSemPermissao),
                await ErroAsync(async () => await Pdtics.ListarAsync(new PePdticConsulta(), await ContextoDe(user))));
        Assert.Equal(2, (await Pdtics.ListarAsync(new PePdticConsulta(), await ContextoDe(UserAdminGeral))).TotalItems);

        // Encerrado sai da lista, e o órgão abre um novo ciclo (versão 2.0, com o anterior)
        MudarSituacao(ses.Id, PeDominios.SituacaoPdtic.Encerrado);
        Assert.Equal(new[] { "SEEC" }, (await Pdtics.ListarAsync(new PePdticConsulta(), sgdi)).Items.Select(i => i.OrgaoSigla));
        Assert.Null(await Pdtics.AtualAsync(null, await Orgao()));
        var novo = await AbrirSesAsync();
        Assert.Equal("2.0", novo.Versao);
        Assert.Equal(ses.Id, novo.AnteriorId);
    }

    [Fact]
    public async Task Editar_SoEmElaboracaoOuDevolvido_ESoAEquipeDoOrgao()
    {
        var pdtic = await AbrirSesAsync();
        var dono = PeDono.DoPdtic(pdtic.Id);
        var nomes = new { comite = "Comitê Interno de TIC", equipe_elaboracao = "Equipe do PDTIC", autoridade_cargo = "Secretário",
            autoridade_nome = "Fulano", unidade_tic = "Subsecretaria de TIC" };

        // Consulta, globais e a equipe de outro órgão não gravam
        foreach (var user in new[] { UserConsultaSes, UserPeSgdi, UserPeAdmin, UserPeCgtic, UserOrgaoSeec })
            Assert.Equal(Codigo(ErrorCode.PeSemPermissao),
                await ErroAsync(async () => await Registros.CriarAsync(dono, "nomes", Salvar(nomes), await ContextoDe(user))));

        var registro = await IncluirNoPdticAsync(pdtic.Id, "nomes", nomes);
        Assert.True((await Registros.ListarAsync(dono, "nomes", await Orgao())).PodeEditar);
        Assert.False((await Registros.ListarAsync(dono, "nomes", await ContextoDe(UserConsultaSes))).PodeEditar);
        Assert.False((await Registros.ListarAsync(dono, "nomes", await ContextoDe(UserPeSgdi))).PodeEditar);

        // Gravar toca o PDTIC
        var tocado = Context.PePdtics.AsNoTracking().Single(p => p.Id == pdtic.Id);
        Assert.Equal(UserOrgaoSes.Email, tocado.AlteradoPor);

        // Em aprovação: fechado para todos (409); devolvido: aberto de novo
        MudarSituacao(pdtic.Id, PeDominios.SituacaoPdtic.EmAprovacao);
        Assert.Equal(Codigo(ErrorCode.PePdticFechado),
            await ErroAsync(async () => await Registros.AtualizarAsync(dono, "nomes", registro.Id, Salvar(nomes), await Orgao())));
        Assert.Equal(Codigo(ErrorCode.PePdticFechado),
            await ErroAsync(async () => await Registros.ExcluirAsync(dono, "nomes", registro.Id, await ContextoDe(UserAdminGeral))));
        Assert.False((await Registros.ListarAsync(dono, "nomes", await Orgao())).PodeEditar);
        Assert.False((await Pdtics.ObterAsync(pdtic.Id, await Orgao())).PodeEditar);

        MudarSituacao(pdtic.Id, PeDominios.SituacaoPdtic.Devolvido);
        var atualizado = await Registros.AtualizarAsync(dono, "nomes", registro.Id, Salvar(new { comite = "SGTIC", equipe_elaboracao = "Equipe",
            autoridade_cargo = "Secretário", autoridade_nome = "Fulano", unidade_tic = "SUTIC" }), await ContextoDe(UserAdminGeral));
        Assert.Equal("SGTIC", Valor(atualizado, "comite"));
    }

    [Fact]
    public async Task Registros_DeOutroOrgao_NemLe()
    {
        var ses = await AbrirSesAsync();
        await IncluirNoPdticAsync(ses.Id, "ativos", new { nome = "Sistema de regulação", tipo = "sistema", situacao = "em_operacao" });

        Assert.Equal(Codigo(ErrorCode.PeSemPermissao),
            await ErroAsync(async () => await Registros.ListarAsync(PeDono.DoPdtic(ses.Id), "ativos", await ContextoDe(UserOrgaoSeec))));
        Assert.Single((await Registros.ListarAsync(PeDono.DoPdtic(ses.Id), "ativos", await ContextoDe(UserConsultaSes))).Registros);
        Assert.Equal(Codigo(ErrorCode.PePdticNaoEncontrado),
            await ErroAsync(async () => await Registros.ListarAsync(PeDono.DoPdtic(999), "ativos", await Orgao())));
    }

    protected void MudarSituacao(long pdticId, string situacao)
    {
        var pdtic = Context.PePdtics.Single(p => p.Id == pdticId);
        pdtic.Situacao = situacao;
        Context.SaveChanges();
        Context.ChangeTracker.Clear();
    }
}
