using System.Text.Json;
using api.Planejamento;
using Microsoft.EntityFrameworkCore;
using Models.Planejamento;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using service;
using service.Planejamento;
using Xunit;

namespace test.planejamento;

/// <summary>
/// F3, parte B: a validação da equipe. Aceitam os passos da elaboração dos tipos dados,
/// conferência dos temas, documento e fluxo (não os externos do PDTIC registrado fora do sistema);
/// marca a equipe do PDTIC do órgão (e o admin geral) com o passo feito e editável; a marca guarda
/// quem e quando; a mudança depois (incluir, alterar, apagar ou reordenar registro, o texto e os
/// capítulos do documento, a cópia de um fluxo) grava a data da primeira mudança; o PDF e a forma
/// do passo não contam; o "não se aplica" tira a marca; a revisão nasce sem marca; e antes da versão
/// 8 do modelo inicial não há validação (409 nas rotas).
/// </summary>
public class PeF3ValidacaoTest : PePaineisTestBase
{
    private async Task<PePassoSituacaoResponse> ValidarAsync(long pdticId, string passo, app.Models.User? user = null) =>
        await Pdtics.ValidarPassoAsync(pdticId, Passo(passo).Id, await ContextoDe(user ?? UserOrgaoSes));

    private async Task<PePassoSituacaoResponse> DesfazerAsync(long pdticId, string passo, app.Models.User? user = null) =>
        await Pdtics.DesfazerValidacaoAsync(pdticId, Passo(passo).Id, await ContextoDe(user ?? UserOrgaoSes));

    private async Task<PeValidacaoResponse?> ValidacaoAsync(long pdticId, string passo) => (await PassoAsync(pdticId, passo)).Validacao;

    [Fact]
    public async Task AceitaValidacao_PeloTipoEPelaEtapa()
    {
        var pdtic = await AbrirSesAsync();
        var situacao = await SituacaoAsync(pdtic.Id);
        bool Aceita(string chave) => situacao.Passos.Single(p => p.Chave == chave).AceitaValidacao;

        Assert.True(Aceita("preparacao.abrangencia"));
        Assert.True(Aceita("preparacao.metodologia"));
        Assert.True(Aceita("planejamento.acoes-tematicas"));
        Assert.True(Aceita("planejamento.documento"));
        Assert.False(Aceita("planejamento.aprovacao-sgtic"));
        Assert.False(Aceita("planejamento.deliberacao-cgtic"));
        Assert.False(Aceita("planejamento.publicacao"));
        Assert.False(Aceita("plano-acompanhamento.quem-acompanha"));
        Assert.False(Aceita("monitoramento.ciclo-monitoramento"));
        Assert.All(situacao.Passos, p => Assert.Null(p.Validacao));

        // O passo de aprovação da elaboração também não (no modo livre, ele aparece)
        DefinirModoNiveis(PeDominios.ModoNiveis.Livre);
        situacao = await SituacaoAsync(pdtic.Id);
        Assert.False(Aceita("preparacao.aprovacao-plano-trabalho"));
        Assert.True(Aceita("diagnostico.swot"));
    }

    [Fact]
    public async Task AceitaValidacao_NoRegistradoForaDoSistema_SoOsPassosQueAEquipePreenche()
    {
        var arquivo = await EnviarArquivoAsync(UserOrgaoSes, "pdtic.pdf", Pdf());
        var pdtic = await Aprovacao.RegistrarExternoAsync(new PeRegistroExternoDTO
        {
            Versao = "1.0",
            VigenciaInicio = "2026-01-01",
            VigenciaFim = "2029-12-31",
            ArquivoId = arquivo.Id,
            AprovacaoInstancia = "cgtic",
            AprovacaoData = "2026-02-10",
            AprovacaoAtoTipo = "Resolução",
            AprovacaoAtoNumero = "3/2026",
            PublicacaoData = "2026-02-20",
            PublicacaoEndereco = Endereco
        }, await Orgao());

        var situacao = await SituacaoAsync(pdtic.Id);
        Assert.False(situacao.Passos.Single(p => p.Chave == "preparacao.abrangencia").AceitaValidacao);
        Assert.True(situacao.Passos.Single(p => p.Chave == PeDominios.ChavePdtic.PassoMetasAcoes).AceitaValidacao);
        // O passo externo não recebe a validação: 409 1145
        var ex = await Assert.ThrowsAnyAsync<ApiException>(() => ValidarAsync(pdtic.Id, "preparacao.abrangencia"));
        Assert.Equal(((int)ErrorCode.PeValidacaoRecusada, PeValidacaoDaEquipe.TipoNaoAceita), (ex.Error.Code, ex.Error.Message));
    }

    [Fact]
    public async Task Validar_SoOPassoFeito_ComQuemEQuando_EMarcarDeNovoRegrava()
    {
        var pdtic = await AbrirSesAsync();
        var naoFeito = await Assert.ThrowsAnyAsync<ApiException>(() => ValidarAsync(pdtic.Id, "preparacao.abrangencia"));
        Assert.Equal(((int)ErrorCode.PeValidacaoRecusada, PeValidacaoDaEquipe.SoPassoFeito), (naoFeito.Error.Code, naoFeito.Error.Message));
        var tipo = await Assert.ThrowsAnyAsync<ApiException>(() => ValidarAsync(pdtic.Id, "planejamento.aprovacao-sgtic"));
        Assert.Equal(((int)ErrorCode.PeValidacaoRecusada, PeValidacaoDaEquipe.TipoNaoAceita), (tipo.Error.Code, tipo.Error.Message));
        // Passo que não aparece para o órgão (a SWOT no Básico): 404
        var fora = await Assert.ThrowsAnyAsync<ApiException>(() => ValidarAsync(pdtic.Id, "diagnostico.swot"));
        Assert.Equal(((int)ErrorCode.PePassoIndisponivel, "Este passo não aparece para o órgão. Atualize a tela."), (fora.Error.Code, fora.Error.Message));

        await PreencherAbrangenciaAsync(pdtic.Id);
        var antes = DateTime.UtcNow;
        var validado = await ValidarAsync(pdtic.Id, "preparacao.abrangencia");
        Assert.Equal(PeDominios.SituacaoPasso.Feito, validado.Situacao);
        Assert.NotNull(validado.Validacao);
        Assert.Equal((UserOrgaoSes.Email, "Otávio da Saúde", (DateTime?)null),
            (validado.Validacao!.ValidadoPor, validado.Validacao.ValidadoPorNome, validado.Validacao.AlteradoDepoisEm));
        Assert.True(validado.Validacao.ValidadoEm >= antes);
        // A marca criou a linha do passo, com o "não se aplica" desmarcado
        var linha = Context.PePdticPassos.AsNoTracking().Single(p => p.PdticId == pdtic.Id && p.PassoId == Passo("preparacao.abrangencia").Id);
        Assert.False(linha.NaoSeAplica);

        // Marcar de novo regrava quem e quando e limpa a mudança
        await Registros.AtualizarAsync(PeDono.DoPdtic(pdtic.Id), "abrangencia", AbrangenciaId(pdtic.Id),
            Salvar(new { tipo_abrangencia = "todo_com_vinculadas", vigencia_inicio = "2026-01-01", vigencia_fim = "2029-12-31", periodicidade_revisao = "semestral" }),
            await Orgao());
        Assert.NotNull((await ValidacaoAsync(pdtic.Id, "preparacao.abrangencia"))!.AlteradoDepoisEm);
        RetratoAdmin(UserAdminGeral);
        var deNovo = await ValidarAsync(pdtic.Id, "preparacao.abrangencia", UserAdminGeral);
        Assert.Equal((UserAdminGeral.Email, (DateTime?)null), (deNovo.Validacao!.ValidadoPor, deNovo.Validacao.AlteradoDepoisEm));
        Assert.True(deNovo.Validacao.ValidadoEm >= validado.Validacao.ValidadoEm);
    }

    [Fact]
    public async Task Validar_SoAEquipeDoPdticEOAdminGeral()
    {
        var pdtic = await AbrirSesAsync();
        await PreencherAbrangenciaAsync(pdtic.Id);
        foreach (var user in new[] { UserConsultaSes, UserPeSgdi, UserPeCgtic, UserPeAdmin, UserOrgaoSeec })
        {
            Assert.Equal(Codigo(ErrorCode.PeSemPermissao), await ErroAsync(() => ValidarAsync(pdtic.Id, "preparacao.abrangencia", user)));
            Assert.Equal(Codigo(ErrorCode.PeSemPermissao), await ErroAsync(() => DesfazerAsync(pdtic.Id, "preparacao.abrangencia", user)));
        }
        var ex = await Assert.ThrowsAnyAsync<ApiException>(() => ValidarAsync(pdtic.Id, "preparacao.abrangencia", UserConsultaSes));
        Assert.Equal("Quem valida os passos é a equipe do PDTIC.", ex.Error.Message);

        var admin = await ValidarAsync(pdtic.Id, "preparacao.abrangencia", UserAdminGeral);
        Assert.Equal(UserAdminGeral.Email, admin.Validacao!.ValidadoPor);
        // Quem só lê vê a marca
        var consulta = (await SituacaoAsync(pdtic.Id, UserConsultaSes)).Passos.Single(p => p.Chave == "preparacao.abrangencia");
        Assert.Equal((true, false), (consulta.Validacao != null, consulta.PodeEditar));
    }

    [Fact]
    public async Task Validar_ComOPassoFechado_409DaSituacao()
    {
        var pdtic = await ProntoParaEnviarAsync();
        await ValidarAsync(pdtic.Id, "preparacao.abrangencia");
        await EnviarAsync(pdtic.Id);

        var ex = await Assert.ThrowsAnyAsync<ApiException>(() => ValidarAsync(pdtic.Id, "preparacao.nomes"));
        Assert.Equal(((int)ErrorCode.PePdticFechado, "Este PDTIC foi enviado ao CGTIC e não muda até a decisão."), (ex.Error.Code, ex.Error.Message));
        Assert.Equal(Codigo(ErrorCode.PePdticFechado), await ErroAsync(() => DesfazerAsync(pdtic.Id, "preparacao.abrangencia")));
        // A marca fica depois do envio
        Assert.NotNull(await ValidacaoAsync(pdtic.Id, "preparacao.abrangencia"));
    }

    [Fact]
    public async Task Desfazer_TiraAMarca_ESemMarcaNaoFazNada()
    {
        var pdtic = await AbrirSesAsync();
        await PreencherAbrangenciaAsync(pdtic.Id);
        await ValidarAsync(pdtic.Id, "preparacao.abrangencia");

        var desfeito = await DesfazerAsync(pdtic.Id, "preparacao.abrangencia");
        Assert.Null(desfeito.Validacao);
        Assert.Equal(PeDominios.SituacaoPasso.Feito, desfeito.Situacao);
        Assert.Null(await ValidacaoAsync(pdtic.Id, "preparacao.abrangencia"));
        // A linha do passo fica (é a do "não se aplica")
        Assert.True(Context.PePdticPassos.Any(p => p.PdticId == pdtic.Id && p.PassoId == Passo("preparacao.abrangencia").Id));

        var deNovo = await DesfazerAsync(pdtic.Id, "preparacao.abrangencia");
        Assert.Null(deNovo.Validacao);
        // Sem marca, até num passo que não recebe a validação
        Assert.Null((await DesfazerAsync(pdtic.Id, "preparacao.nomes")).Validacao);
    }

    [Fact]
    public async Task MudancaDepois_IncluirAlterarApagarEReordenar_ADataDaPrimeiraMudanca()
    {
        var pdtic = await AbrirSesAsync();
        var dono = PeDono.DoPdtic(pdtic.Id);
        await IncluirNoPdticAsync(pdtic.Id, "equipe_elaboracao", new { nome = "Ana Souza" });
        var bruno = await IncluirNoPdticAsync(pdtic.Id, "equipe_elaboracao", new { nome = "Bruno Lima" });
        await ValidarAsync(pdtic.Id, "preparacao.equipe");
        Assert.Null((await ValidacaoAsync(pdtic.Id, "preparacao.equipe"))!.AlteradoDepoisEm);

        // Alterar com os mesmos dados não é mudança
        await Registros.AtualizarAsync(dono, "equipe_elaboracao", bruno.Id, Salvar(new { nome = "Bruno Lima" }), await Orgao());
        Assert.Null((await ValidacaoAsync(pdtic.Id, "preparacao.equipe"))!.AlteradoDepoisEm);

        // Incluir: grava a data; a mudança seguinte não a move
        await IncluirNoPdticAsync(pdtic.Id, "equipe_elaboracao", new { nome = "Carla Dias" });
        var primeira = (await ValidacaoAsync(pdtic.Id, "preparacao.equipe"))!.AlteradoDepoisEm;
        Assert.NotNull(primeira);
        await Registros.AtualizarAsync(dono, "equipe_elaboracao", bruno.Id, Salvar(new { nome = "Bruno de Lima" }), await Orgao());
        Assert.Equal(primeira, (await ValidacaoAsync(pdtic.Id, "preparacao.equipe"))!.AlteradoDepoisEm);
        // A marca fica, com o aviso
        Assert.Equal(UserOrgaoSes.Email, (await ValidacaoAsync(pdtic.Id, "preparacao.equipe"))!.ValidadoPor);

        // Alterar
        await ValidarAsync(pdtic.Id, "preparacao.equipe");
        await Registros.AtualizarAsync(dono, "equipe_elaboracao", bruno.Id, Salvar(new { nome = "Bruno Lima" }), await Orgao());
        Assert.NotNull((await ValidacaoAsync(pdtic.Id, "preparacao.equipe"))!.AlteradoDepoisEm);

        // Reordenar (a mesma ordem não conta)
        await ValidarAsync(pdtic.Id, "preparacao.equipe");
        var ids = (await Registros.ListarAsync(dono, "equipe_elaboracao", await Orgao())).Registros.Select(r => r.Id).ToList();
        await Registros.OrdenarAsync(dono, "equipe_elaboracao", new PeOrdemDTO { Ids = ids }, await Orgao());
        Assert.Null((await ValidacaoAsync(pdtic.Id, "preparacao.equipe"))!.AlteradoDepoisEm);
        ids.Reverse();
        await Registros.OrdenarAsync(dono, "equipe_elaboracao", new PeOrdemDTO { Ids = ids }, await Orgao());
        Assert.NotNull((await ValidacaoAsync(pdtic.Id, "preparacao.equipe"))!.AlteradoDepoisEm);

        // Apagar
        await ValidarAsync(pdtic.Id, "preparacao.equipe");
        await Registros.ExcluirAsync(dono, "equipe_elaboracao", bruno.Id, await Orgao());
        Assert.NotNull((await ValidacaoAsync(pdtic.Id, "preparacao.equipe"))!.AlteradoDepoisEm);

        // A mudança num passo não mexe na marca de outro
        await PreencherAbrangenciaAsync(pdtic.Id);
        await ValidarAsync(pdtic.Id, "preparacao.abrangencia");
        await IncluirNoPdticAsync(pdtic.Id, "equipe_elaboracao", new { nome = "Davi" });
        Assert.Null((await ValidacaoAsync(pdtic.Id, "preparacao.abrangencia"))!.AlteradoDepoisEm);
    }

    [Fact]
    public async Task MudancaDepois_ODocumentoEOsFluxos_NaoOPdfNemAForma()
    {
        DefinirModoNiveis(PeDominios.ModoNiveis.Livre);
        var pdtic = await AbrirSesAsync();
        await Documentos.GerarPdfAsync(pdtic.Id, await Orgao());
        await ValidarAsync(pdtic.Id, "planejamento.documento");

        // Gerar outro PDF não conta
        await Documentos.GerarPdfAsync(pdtic.Id, await Orgao());
        Assert.Null((await ValidacaoAsync(pdtic.Id, "planejamento.documento"))!.AlteradoDepoisEm);

        // Gravar o texto de um bloco conta; voltar ao modelo também
        var bloco = BlocoDoModelo("introducao").Id;
        await Documentos.SalvarTextoAsync(pdtic.Id, bloco, Json(Rico("A introdução da SES.")), await Orgao());
        Assert.NotNull((await ValidacaoAsync(pdtic.Id, "planejamento.documento"))!.AlteradoDepoisEm);
        await ValidarAsync(pdtic.Id, "planejamento.documento");
        await Documentos.RestaurarTextoAsync(pdtic.Id, bloco, await Orgao());
        Assert.NotNull((await ValidacaoAsync(pdtic.Id, "planejamento.documento"))!.AlteradoDepoisEm);

        // Mudar um capítulo (título próprio) conta
        await ValidarAsync(pdtic.Id, "planejamento.documento");
        await Documentos.AtualizarCapituloAsync(pdtic.Id, CapituloDoModelo("introducao").Id,
            PeCorpoParcial.Ler<PeDocCapituloOrgaoDTO>(Json(new { TituloProprio = "Para começar" })), await Orgao());
        Assert.NotNull((await ValidacaoAsync(pdtic.Id, "planejamento.documento"))!.AlteradoDepoisEm);

        // A cópia de um fluxo muda a metodologia (o passo com os fluxos)
        await IncluirNoPdticAsync(pdtic.Id, "metodologia", new { metodologia_adotada = "guia_sisp" });
        await ValidarAsync(pdtic.Id, "preparacao.metodologia");
        var fluxos = new PeFluxoService(Context, Registros, Permissoes);
        var modelo = Context.PeFluxosModelo.AsNoTracking().Single(m => m.Chave == "preparacao");
        var definicao = JsonDocument.Parse(PeFluxoDefinicaoLeitor.ParaJson(PeFluxoDefinicaoLeitor.DoBanco(modelo.Definicao))).RootElement.Clone();
        await fluxos.SalvarAsync(pdtic.Id, "preparacao", new PeFluxoSalvarDTO { Nome = "Preparação da Saúde", Definicao = definicao }, await Orgao());
        Assert.NotNull((await ValidacaoAsync(pdtic.Id, "preparacao.metodologia"))!.AlteradoDepoisEm);
        await ValidarAsync(pdtic.Id, "preparacao.metodologia");
        await fluxos.RestaurarAsync(pdtic.Id, "preparacao", await Orgao());
        Assert.NotNull((await ValidacaoAsync(pdtic.Id, "preparacao.metodologia"))!.AlteradoDepoisEm);

        // Mudar a forma do passo (parte C) não conta
        await ValidarAsync(pdtic.Id, "preparacao.metodologia");
        await Orgaos.DefinirDetalheAsync(OrgaoSes.Id, Passo("preparacao.metodologia").Id,
            new PePassoDetalheDTO { NivelId = NivelId("intermediario") }, UserOrgaoSes.Email);
        var metodologia = await PassoAsync(pdtic.Id, "preparacao.metodologia");
        Assert.NotNull(metodologia.Validacao);
        Assert.Null(metodologia.Validacao!.AlteradoDepoisEm);
    }

    [Fact]
    public async Task NaoSeAplica_TiraAValidacao()
    {
        DefinirModoNiveis(PeDominios.ModoNiveis.Livre);
        var pdtic = await AbrirSesAsync();
        await IncluirNoPdticAsync(pdtic.Id, "documentos_referencia", new { identificacao = "Plano Plurianual", tipo = "ppa" });
        await ValidarAsync(pdtic.Id, "preparacao.documentos-referencia");

        var marcado = await Pdtics.MarcarNaoSeAplicaAsync(pdtic.Id, Passo("preparacao.documentos-referencia").Id,
            new PeNaoSeAplicaDTO { Justificativa = "O órgão não usa documentos de referência." }, await Orgao());
        Assert.Equal(PeDominios.SituacaoPasso.NaoSeAplica, marcado.Situacao);
        Assert.Null(marcado.Validacao);

        var desfeito = await Pdtics.DesmarcarNaoSeAplicaAsync(pdtic.Id, Passo("preparacao.documentos-referencia").Id, await Orgao());
        Assert.Null(desfeito.Validacao);
    }

    [Fact]
    public async Task Revisao_NasceSemNenhumaMarca()
    {
        var pdtic = await ProntoParaEnviarAsync();
        await ValidarAsync(pdtic.Id, "preparacao.abrangencia");
        await ValidarAsync(pdtic.Id, "diagnostico.ativos");
        await EnviarAsync(pdtic.Id);
        await AprovarNoCgticAsync(pdtic.Id);
        await PublicarAsync(pdtic.Id);

        var revisao = await Aprovacao.RevisarAsync(pdtic.Id, new PeRevisaoDTO { Justificativa = "Mudou a estrutura." }, await Orgao());
        Assert.All((await SituacaoAsync(revisao.Id)).Passos, p => Assert.Null(p.Validacao));
        Assert.False(Context.PePdticPassosValidacao.Any(v => v.PdticId == revisao.Id));
        // A versão revista continua com as marcas
        Assert.NotNull(await ValidacaoAsync(pdtic.Id, "diagnostico.ativos"));
    }

    [Fact]
    public async Task PaginaDoOrgao_OsValidadosPorEtapa()
    {
        var pdtic = await ProntoParaEnviarAsync();
        await ValidarAsync(pdtic.Id, "preparacao.abrangencia");
        await ValidarAsync(pdtic.Id, "preparacao.nomes");
        await ValidarAsync(pdtic.Id, "diagnostico.ativos");

        var resumo = await Paineis.ResumoAsync(OrgaoSes.Id, await Sgdi());
        Assert.Equal(new[] { 2, 1, 0 }, resumo.Andamento.Take(3).Select(a => a.Validados));
    }

    [Fact]
    public async Task AntesDaVersao8_SemValidacao_EAsRotasRespondem409()
    {
        var pdtic = await AbrirSesAsync();
        await PreencherAbrangenciaAsync(pdtic.Id);
        await ValidarAsync(pdtic.Id, "preparacao.abrangencia");
        VersaoDoModelo(7);

        var passo = await PassoAsync(pdtic.Id, "preparacao.abrangencia");
        Assert.False(passo.AceitaValidacao);
        Assert.Null(passo.Validacao);
        Assert.Equal(Codigo(ErrorCode.PeModeloIndisponivel), await ErroAsync(() => ValidarAsync(pdtic.Id, "preparacao.abrangencia")));
        Assert.Equal(Codigo(ErrorCode.PeModeloIndisponivel), await ErroAsync(() => DesfazerAsync(pdtic.Id, "preparacao.abrangencia")));

        // A gravação no passo não mexe na validação (as colunas nem existem no intervalo do deploy)
        await Registros.AtualizarAsync(PeDono.DoPdtic(pdtic.Id), "abrangencia", AbrangenciaId(pdtic.Id),
            Salvar(new { tipo_abrangencia = "todo_com_vinculadas", vigencia_inicio = "2026-01-01", vigencia_fim = "2029-12-31", periodicidade_revisao = "semestral" }),
            await Orgao());
        Assert.Null(Context.PePdticPassosValidacao.AsNoTracking().Single(v => v.PdticId == pdtic.Id).AlteradaEm);
    }

    [Fact]
    public async Task Controller_AsRotasDoContrato()
    {
        var pdtic = await AbrirSesAsync();
        var passo = Passo("preparacao.abrangencia").Id;

        var (recusa, corpo) = Resultado(await ControladorPdtic(UserOrgaoSes).Validar(pdtic.Id, passo));
        Assert.Equal(409, recusa);
        var erro = JsonSerializer.SerializeToElement(corpo);
        Assert.Equal((1145, PeValidacaoDaEquipe.SoPassoFeito), (erro.GetProperty("Code").GetInt32(), erro.GetProperty("Message").GetString()));

        await PreencherAbrangenciaAsync(pdtic.Id);
        var (ok, validado) = Resultado(await ControladorPdtic(UserOrgaoSes).Validar(pdtic.Id, passo));
        Assert.Equal(200, ok);
        Assert.NotNull(Assert.IsType<PePassoSituacaoResponse>(validado).Validacao);

        var (proibido, _) = Resultado(await ControladorPdtic(UserConsultaSes).DesfazerValidacao(pdtic.Id, passo));
        Assert.Equal(403, proibido);
        var (desfeito, semMarca) = Resultado(await ControladorPdtic(UserOrgaoSes).DesfazerValidacao(pdtic.Id, passo));
        Assert.Equal(200, desfeito);
        Assert.Null(Assert.IsType<PePassoSituacaoResponse>(semMarca).Validacao);
    }

    private long AbrangenciaId(long pdticId) =>
        Context.PeRegistros.AsNoTracking().Single(r => r.PdticId == pdticId && r.SecaoId == Secao("abrangencia").Id).Id;

    private static byte[] Pdf()
    {
        QuestPDF.Settings.License = LicenseType.Community;
        return Document.Create(d => d.Page(p => p.Content().Text("PDTIC aprovado fora do sistema."))).GeneratePdf();
    }
}
