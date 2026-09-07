using System.Text.RegularExpressions;
using api.Common;
using api.Pgia;
using app.Models;
using Microsoft.EntityFrameworkCore;
using Models.Pgia;
using Repositorio.Pgia;
using service;
using service.Pgia;
using Xunit;

namespace test.pgia;

/// <summary>
/// Transparência pública (fase 5): Registro Público do art. 24 e solicitações do
/// cidadão (arts. 11, IV e 23), incluindo a minimização de dados pessoais.
/// </summary>
public class PgiaPublicoTest : PgiaTestBase
{
    private readonly PgiaPublicoService _service;
    private readonly PgiaSistemaService _sistemaService;
    private readonly PgiaRelatorioService _relatorioService;
    private readonly PgiaPermissionService _permissionService;

    public PgiaPublicoTest()
    {
        _permissionService = new PgiaPermissionService(Context);
        _service = new PgiaPublicoService(new PgiaPublicoRepositorio(Context));
        _relatorioService = new PgiaRelatorioService(new PgiaRelatorioRepositorio(Context));
        _sistemaService = new PgiaSistemaService(
            new PgiaSistemaRepositorio(Context),
            new PgiaOrgaoRepositorio(Context),
            new PgiaDesignacaoRepositorio(Context),
            _permissionService);

        DesignarResponsavel(OrgaoSes, UserOrgaoSes);
        DesignarResponsavel(OrgaoSeec, UserOrgaoSeec);
    }

    // ── Apoio ─────────────────────────────────────────────────────────────────

    private void DesignarResponsavel(PgiaOrgao orgao, User agente)
    {
        Context.PgiaResponsaveisIa.Add(new PgiaResponsavelIa
        {
            OrgaoId = orgao.Id,
            AgenteId = agente.Id,
            AtoTipo = "Portaria",
            AtoNumero = "214/2026",
            AtoData = new DateOnly(2026, 7, 20),
            ProcessoSeiComunicacao = "00060-00012345/2026-11",
            DataComunicacaoSgdi = new DateOnly(2026, 7, 28),
            InicioVigencia = new DateOnly(2026, 7, 20),
            Ativo = true,
            CriadoEm = DateTime.UtcNow
        });
        Context.SaveChanges();
    }

    private async Task<PgiaUserContext> CtxAsync(string email) =>
        (await _permissionService.GetContextAsync(email, PerfilDe(email)))!;

    private async Task<PgiaSistemaResponse> NovoSistemaAsync(
        long orgaoId, string email, string denominacao, bool afetaCidadao = true)
    {
        var ctx = await CtxAsync(email);
        return await _sistemaService.CriarSistemaAsync(orgaoId, new PgiaSistemaCreateDTO
        {
            Denominacao = denominacao,
            Finalidade = "Apoiar o atendimento ao cidadão",
            OrigemRegistro = "Nova iniciativa",
            TipoSistema = "Desenvolvido internamente",
            Tecnologia = "IA generativa",
            StatusCicloVida = "Planejamento",
            // Dados pessoais sensíveis: o bloco LGPD existe, mas nada dele é público
            EscopoDados = PgiaDominios.EscopoDados.DadosPessoaisSensiveis,
            BaseLegalLgpd = "Execução de políticas públicas",
            CategoriasDadosPessoais = "Nome, CPF e dados de saúde",
            FinalidadeTratamentoDados = "Triagem de atendimento",
            MedidasSeguranca = "Criptografia em repouso e controle de acesso",
            AfetaCidadao = afetaCidadao,
            NaturezaDecisoes = afetaCidadao ? "Prioriza atendimentos" : null,
            EfeitosCidadao = afetaCidadao ? "Ordem de chamada no atendimento" : null,
            InteroperavelPadroesSgdi = true,
            Classificacao = new PgiaClassificacaoCreateDTO
            {
                Checklist = ChecklistRespondido(),
                OutrosRiscos = OutrosRiscosSeNecessario(),
                Motivo = "Classificação inicial",
                DataClassificacao = new DateOnly(2026, 9, 1),
                Justificativa = "Sem enquadramento nos arts. 15 a 17."
            }
        }, ctx);
    }

    private async Task PublicarAsync(long sistemaId)
    {
        var sgdiCtx = await CtxAsync(UserSgdi.Email);
        await _sistemaService.AtualizarRegistroPublicoAsync(sistemaId, new PgiaRegistroPublicoDTO
        {
            Publicado = true,
            DataPublicacao = new DateOnly(2026, 12, 1)
        }, sgdiCtx);
    }

    private static PgiaSolicitacaoCreateDTO NovaSolicitacaoDto(long sistemaId, string? tipo = null) => new()
    {
        SistemaIaId = sistemaId,
        Tipo = tipo ?? "Explicação da decisão",
        SolicitanteNome = "Joana da Silva",
        SolicitanteContato = "joana@exemplo.com",
        ReferenciaDecisao = "Atendimento 4521/2026",
        Descricao = "Quero entender por que fui colocada no fim da fila."
    };

    // ── Registro Público (art. 24) ────────────────────────────────────────────

    [Fact]
    public async Task Registro_TrazSomenteOsSistemasPublicados()
    {
        var publicado = await NovoSistemaAsync(OrgaoSes.Id, UserOrgaoSes.Email, "Assistente 156");
        await NovoSistemaAsync(OrgaoSes.Id, UserOrgaoSes.Email, "Sistema interno");
        await PublicarAsync(publicado.Id);

        var registro = await _service.ListarRegistroPublicoAsync(new PagedRequest());

        Assert.Equal(1, registro.TotalItems);
        Assert.Equal("Assistente 156", registro.Items[0].Denominacao);
        Assert.Equal("SES", registro.Items[0].OrgaoSigla);
        Assert.Equal("Secretaria de Estado de Saúde do Distrito Federal", registro.Items[0].OrgaoNome);
        Assert.Equal(new DateOnly(2026, 12, 1), registro.Items[0].DataPublicacaoRegistro);
        // III — natureza e efeitos, porque o sistema afeta cidadãos
        Assert.Equal("Prioriza atendimentos", registro.Items[0].NaturezaDecisoes);
        Assert.Equal("Ordem de chamada no atendimento", registro.Items[0].EfeitosCidadao);
    }

    [Fact]
    public async Task Registro_DespublicarRemoveDoRegistro()
    {
        var sistema = await NovoSistemaAsync(OrgaoSes.Id, UserOrgaoSes.Email, "Assistente 156");
        await PublicarAsync(sistema.Id);
        Assert.Equal(1, (await _service.ListarRegistroPublicoAsync(new PagedRequest())).TotalItems);

        var sgdiCtx = await CtxAsync(UserSgdi.Email);
        await _sistemaService.AtualizarRegistroPublicoAsync(sistema.Id, new PgiaRegistroPublicoDTO
        {
            Publicado = false
        }, sgdiCtx);

        Assert.Equal(0, (await _service.ListarRegistroPublicoAsync(new PagedRequest())).TotalItems);
    }

    [Fact]
    public async Task Registro_ContaIndicadoresPublicadosETrazAUrlDaAia()
    {
        var orgaoCtx = await CtxAsync(UserOrgaoSes.Email);
        var sgdiCtx = await CtxAsync(UserSgdi.Email);
        var sistema = await NovoSistemaAsync(OrgaoSes.Id, UserOrgaoSes.Email, "Assistente 156");
        await PublicarAsync(sistema.Id);

        // Dois indicadores publicados e um não publicado
        foreach (var (nome, publicado) in new[] { ("Acurácia", true), ("Equidade", true), ("Interno", false) })
        {
            await _relatorioService.CriarIndicadorAsync(sistema.Id, new PgiaIndicadorCreateDTO
            {
                Nome = nome,
                Categoria = "Acurácia",
                Valor = 0.9m,
                PeriodoInicio = new DateOnly(2026, 7, 1),
                PeriodoFim = new DateOnly(2026, 12, 31),
                PublicadoRegistroPublico = publicado
            }, orgaoCtx);
        }

        // Duas AIA publicadas: vale a mais recente
        foreach (var url in new[] { "https://transparencia.df.gov.br/aia/antiga", "https://transparencia.df.gov.br/aia/atual" })
        {
            var aia = await _sistemaService.CriarAiaAsync(sistema.Id, new PgiaAiaCreateDTO
            {
                Status = "Em elaboração",
                DataInicio = new DateOnly(2026, 9, 10),
                ImpactosDireitosFundamentais = "Risco de viés.",
                MedidasPreventivas = "Curadoria da base.",
                MedidasMitigadoras = "Revisão humana.",
                MedidasReversao = "Desligamento do modelo."
            }, orgaoCtx);
            await _sistemaService.PublicarAiaAsync(aia.Id, new PgiaAiaPublicacaoDTO
            {
                PublicadaPortal = true,
                DataPublicacaoPortal = new DateOnly(2026, 10, 20),
                UrlPublicacao = url
            }, sgdiCtx);
        }

        var registro = await _service.ListarRegistroPublicoAsync(new PagedRequest());

        Assert.Equal(2, registro.Items[0].IndicadoresPublicados);
        Assert.Equal("https://transparencia.df.gov.br/aia/atual", registro.Items[0].AiaResultadoUrl);
    }

    [Fact]
    public async Task Registro_SemAiaOuIndicadoresVemZeradoENulo()
    {
        var sistema = await NovoSistemaAsync(OrgaoSes.Id, UserOrgaoSes.Email, "Assistente 156");
        await PublicarAsync(sistema.Id);

        var registro = await _service.ListarRegistroPublicoAsync(new PagedRequest());

        Assert.Null(registro.Items[0].AiaResultadoUrl);
        Assert.Equal(0, registro.Items[0].IndicadoresPublicados);
    }

    [Fact]
    public async Task Registro_PaginaEOrdenaPorOrgaoEDenominacao()
    {
        var b = await NovoSistemaAsync(OrgaoSes.Id, UserOrgaoSes.Email, "B da SES");
        var a = await NovoSistemaAsync(OrgaoSes.Id, UserOrgaoSes.Email, "A da SES");
        var seec = await NovoSistemaAsync(OrgaoSeec.Id, UserOrgaoSeec.Email, "Z da SEEC");
        foreach (var id in new[] { a.Id, b.Id, seec.Id }) await PublicarAsync(id);

        var primeira = await _service.ListarRegistroPublicoAsync(new PagedRequest { Page = 1, PageSize = 2 });

        Assert.Equal(3, primeira.TotalItems);
        Assert.Equal(2, primeira.TotalPages);
        // SEEC antes de SES; dentro do órgão, por denominação
        Assert.Equal("Z da SEEC", primeira.Items[0].Denominacao);
        Assert.Equal("A da SES", primeira.Items[1].Denominacao);

        // Página zerada cai na primeira, como nos demais módulos
        var saneada = await _service.ListarRegistroPublicoAsync(new PagedRequest { Page = 0, PageSize = 2 });
        Assert.Equal(1, saneada.CurrentPage);
        Assert.Equal("Z da SEEC", saneada.Items[0].Denominacao);
    }

    // ── Abertura de solicitação (art. 23) ─────────────────────────────────────

    [Fact]
    public async Task Solicitacao_SistemaNaoPublicadoEhRejeitado()
    {
        var naoPublicado = await NovoSistemaAsync(OrgaoSes.Id, UserOrgaoSes.Email, "Sistema interno");

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _service.CriarSolicitacaoAsync(NovaSolicitacaoDto(naoPublicado.Id)));

        Assert.Equal((int)ErrorCode.PgiaSistemaNaoPublicado, ex.Error.Code);

        var inexistente = await Assert.ThrowsAsync<ApiException>(() =>
            _service.CriarSolicitacaoAsync(NovaSolicitacaoDto(9999)));
        Assert.Equal((int)ErrorCode.PgiaSistemaNaoPublicado, inexistente.Error.Code);
    }

    [Fact]
    public async Task Solicitacao_TipoForaDoDominioEhRejeitado()
    {
        var sistema = await NovoSistemaAsync(OrgaoSes.Id, UserOrgaoSes.Email, "Assistente 156");
        await PublicarAsync(sistema.Id);

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _service.CriarSolicitacaoAsync(NovaSolicitacaoDto(sistema.Id, "Reclamação genérica")));

        Assert.Equal((int)ErrorCode.PgiaSolicitacaoInvalida, ex.Error.Code);
    }

    [Fact]
    public async Task Solicitacao_CamposObrigatoriosSaoExigidosAposTrim()
    {
        var sistema = await NovoSistemaAsync(OrgaoSes.Id, UserOrgaoSes.Email, "Assistente 156");
        await PublicarAsync(sistema.Id);

        var semNome = NovaSolicitacaoDto(sistema.Id);
        semNome.SolicitanteNome = "   ";
        var exNome = await Assert.ThrowsAsync<ApiException>(() => _service.CriarSolicitacaoAsync(semNome));
        Assert.Equal((int)ErrorCode.PgiaSolicitacaoInvalida, exNome.Error.Code);

        var semContato = NovaSolicitacaoDto(sistema.Id);
        semContato.SolicitanteContato = "  ";
        var exContato = await Assert.ThrowsAsync<ApiException>(() => _service.CriarSolicitacaoAsync(semContato));
        Assert.Equal((int)ErrorCode.PgiaSolicitacaoInvalida, exContato.Error.Code);

        var semDescricao = NovaSolicitacaoDto(sistema.Id);
        semDescricao.Descricao = " ";
        var exDescricao = await Assert.ThrowsAsync<ApiException>(() => _service.CriarSolicitacaoAsync(semDescricao));
        Assert.Equal((int)ErrorCode.PgiaSolicitacaoInvalida, exDescricao.Error.Code);
    }

    [Fact]
    public async Task Solicitacao_ProtocoloEhGeradoNoServidorEUnico()
    {
        var sistema = await NovoSistemaAsync(OrgaoSes.Id, UserOrgaoSes.Email, "Assistente 156");
        await PublicarAsync(sistema.Id);

        var protocolos = new List<string>();
        for (var i = 0; i < 5; i++)
        {
            var resposta = await _service.CriarSolicitacaoAsync(NovaSolicitacaoDto(sistema.Id));
            protocolos.Add(resposta.Protocolo);
        }

        var ano = demanda_service.Helpers.DateTimeHelper.TodayBrasilia().Year;
        Assert.All(protocolos, p => Assert.Matches(new Regex($@"^PGIA-{ano}-\d{{6}}-[A-HJ-NP-Z]{{4}}$"), p));
        Assert.Equal(protocolos.Count, protocolos.Distinct().Count());
        Assert.Equal(5, await Context.PgiaSolicitacoesCidadao.CountAsync());
    }

    [Fact]
    public async Task Solicitacao_ReclamacaoDeDadosNasceEncaminhadaAoDpo()
    {
        var sistema = await NovoSistemaAsync(OrgaoSes.Id, UserOrgaoSes.Email, "Assistente 156");
        await PublicarAsync(sistema.Id);

        var reclamacao = await _service.CriarSolicitacaoAsync(
            NovaSolicitacaoDto(sistema.Id, PgiaDominios.TipoSolicitacao.ReclamacaoDados));

        var salva = await Context.PgiaSolicitacoesCidadao
            .FirstAsync(s => s.Protocolo == reclamacao.Protocolo);
        Assert.True(salva.EncaminhadaDpo);
        Assert.Equal(PgiaDominios.StatusSolicitacao.EncaminhadaDpo, salva.Status);
        Assert.Equal("publico", salva.CriadoPor);

        // Os demais tipos nascem apenas recebidos
        var explicacao = await _service.CriarSolicitacaoAsync(NovaSolicitacaoDto(sistema.Id));
        var outra = await Context.PgiaSolicitacoesCidadao.FirstAsync(s => s.Protocolo == explicacao.Protocolo);
        Assert.False(outra.EncaminhadaDpo);
        Assert.Equal(PgiaDominios.StatusSolicitacao.Recebida, outra.Status);
    }

    // ── Acompanhamento público (minimização) ──────────────────────────────────

    [Fact]
    public async Task Consulta_PorProtocoloNaoExpoeDadosDoSolicitante()
    {
        var sistema = await NovoSistemaAsync(OrgaoSes.Id, UserOrgaoSes.Email, "Assistente 156");
        await PublicarAsync(sistema.Id);
        var aberta = await _service.CriarSolicitacaoAsync(NovaSolicitacaoDto(sistema.Id));

        var publica = await _service.ConsultarPorProtocoloAsync(aberta.Protocolo);

        Assert.NotNull(publica);
        Assert.Equal(aberta.Protocolo, publica!.Protocolo);
        Assert.Equal("Assistente 156", publica.SistemaDenominacao);
        Assert.Equal("SES", publica.OrgaoSigla);
        Assert.Equal(PgiaDominios.StatusSolicitacao.Recebida, publica.Status);

        // O tipo devolvido não carrega nome, contato, descrição nem referência
        var propriedades = publica.GetType().GetProperties().Select(p => p.Name).ToList();
        Assert.DoesNotContain("SolicitanteNome", propriedades);
        Assert.DoesNotContain("SolicitanteContato", propriedades);
        Assert.DoesNotContain("Descricao", propriedades);
        Assert.DoesNotContain("ReferenciaDecisao", propriedades);
    }

    [Fact]
    public async Task Consulta_ProtocoloDesconhecidoNaoDevolveNada()
    {
        var sistema = await NovoSistemaAsync(OrgaoSes.Id, UserOrgaoSes.Email, "Assistente 156");
        await PublicarAsync(sistema.Id);
        await _service.CriarSolicitacaoAsync(NovaSolicitacaoDto(sistema.Id));

        Assert.Null(await _service.ConsultarPorProtocoloAsync("PGIA-2026-000000"));
        Assert.Null(await _service.ConsultarPorProtocoloAsync("   "));
        Assert.Null(await _service.ConsultarPorProtocoloAsync("qualquer-coisa"));
    }

    [Fact]
    public async Task Registro_ItemPublicoNaoCarregaCamposInternosNemLgpd()
    {
        var sistema = await NovoSistemaAsync(OrgaoSes.Id, UserOrgaoSes.Email, "Assistente 156");
        await PublicarAsync(sistema.Id);

        var registro = await _service.ListarRegistroPublicoAsync(new PagedRequest());
        var propriedades = registro.Items[0].GetType().GetProperties().Select(p => p.Name).ToList();

        // O bloco LGPD e os campos internos do inventário não existem no contrato público
        foreach (var interno in new[]
        {
            "BaseLegalLgpd", "CategoriasDadosPessoais", "FinalidadeTratamentoDados", "MedidasSeguranca",
            "ProcessoSei", "SituacaoHomologacao", "AvaliacaoParecer", "ParecerSgtic", "ResponsavelNome"
        })
        {
            Assert.DoesNotContain(interno, propriedades);
        }
    }

    // ── Tratamento pelo órgão ─────────────────────────────────────────────────

    [Fact]
    public async Task Orgao_ListaAsSolicitacoesDosSeusSistemas()
    {
        var daSes = await NovoSistemaAsync(OrgaoSes.Id, UserOrgaoSes.Email, "Assistente 156");
        var daSeec = await NovoSistemaAsync(OrgaoSeec.Id, UserOrgaoSeec.Email, "Classificador");
        await PublicarAsync(daSes.Id);
        await PublicarAsync(daSeec.Id);

        await _service.CriarSolicitacaoAsync(NovaSolicitacaoDto(daSes.Id));
        await _service.CriarSolicitacaoAsync(NovaSolicitacaoDto(daSeec.Id));

        var listaSes = await _service.ListarSolicitacoesPorOrgaoAsync(OrgaoSes.Id);
        Assert.Single(listaSes);
        Assert.Equal("Assistente 156", listaSes[0].SistemaDenominacao);
        // Na área autenticada o órgão vê os dados para poder responder
        Assert.Equal("Joana da Silva", listaSes[0].SolicitanteNome);
        Assert.Equal("joana@exemplo.com", listaSes[0].SolicitanteContato);

        Assert.Single(await _service.ListarSolicitacoesPorOrgaoAsync(OrgaoSeec.Id));
    }

    [Fact]
    public async Task Orgao_NaoAlcancaSolicitacaoDeSistemaDeOutroOrgao()
    {
        var daSeec = await NovoSistemaAsync(OrgaoSeec.Id, UserOrgaoSeec.Email, "Classificador");
        await PublicarAsync(daSeec.Id);
        var aberta = await _service.CriarSolicitacaoAsync(NovaSolicitacaoDto(daSeec.Id));

        var solicitacao = await _service.GetSolicitacaoEntidadeAsync(
            (await Context.PgiaSolicitacoesCidadao.FirstAsync(s => s.Protocolo == aberta.Protocolo)).Id);

        // É esta checagem que o controller faz antes de deixar responder
        var ctxSes = await CtxAsync(UserOrgaoSes.Email);
        Assert.NotNull(solicitacao!.Sistema);
        Assert.False(_permissionService.CanAccessOrgao(ctxSes, solicitacao.Sistema!.OrgaoId));

        var ctxSeec = await CtxAsync(UserOrgaoSeec.Email);
        Assert.True(_permissionService.CanAccessOrgao(ctxSeec, solicitacao.Sistema.OrgaoId));
    }

    [Fact]
    public async Task Orgao_RespostaGravaAutorDataEStatus()
    {
        var sistema = await NovoSistemaAsync(OrgaoSes.Id, UserOrgaoSes.Email, "Assistente 156");
        await PublicarAsync(sistema.Id);
        var aberta = await _service.CriarSolicitacaoAsync(NovaSolicitacaoDto(sistema.Id));
        var id = (await Context.PgiaSolicitacoesCidadao.FirstAsync(s => s.Protocolo == aberta.Protocolo)).Id;

        var ctx = await CtxAsync(UserOrgaoSes.Email);
        var respondida = await _service.ResponderAsync(id, new PgiaSolicitacaoRespostaDTO
        {
            Resposta = "A ordem de atendimento considerou a data de solicitação."
        }, ctx);

        Assert.Equal(PgiaDominios.StatusSolicitacao.Respondida, respondida.Status);
        Assert.Equal("Maria Andrade", respondida.RespondidoPorNome);
        Assert.NotNull(respondida.DataResposta);

        var salva = await Context.PgiaSolicitacoesCidadao.FirstAsync(s => s.Id == id);
        Assert.Equal(ctx.UserId, salva.RespondidoPor);
        Assert.Equal(UserOrgaoSes.Email, salva.AlteradoPor);

        // O cidadão passa a ver a resposta pelo protocolo
        var publica = await _service.ConsultarPorProtocoloAsync(aberta.Protocolo);
        Assert.Equal("A ordem de atendimento considerou a data de solicitação.", publica!.Resposta);
    }

    [Fact]
    public async Task Orgao_RespostaVaziaEhRejeitada()
    {
        var sistema = await NovoSistemaAsync(OrgaoSes.Id, UserOrgaoSes.Email, "Assistente 156");
        await PublicarAsync(sistema.Id);
        var aberta = await _service.CriarSolicitacaoAsync(NovaSolicitacaoDto(sistema.Id));
        var id = (await Context.PgiaSolicitacoesCidadao.FirstAsync(s => s.Protocolo == aberta.Protocolo)).Id;
        var ctx = await CtxAsync(UserOrgaoSes.Email);

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _service.ResponderAsync(id, new PgiaSolicitacaoRespostaDTO { Resposta = "   " }, ctx));

        Assert.Equal((int)ErrorCode.PgiaSolicitacaoInvalida, ex.Error.Code);
    }

    [Fact]
    public async Task Orgao_EncaminhaAoDpoEMarcaEmAnalise()
    {
        var sistema = await NovoSistemaAsync(OrgaoSes.Id, UserOrgaoSes.Email, "Assistente 156");
        await PublicarAsync(sistema.Id);
        var aberta = await _service.CriarSolicitacaoAsync(NovaSolicitacaoDto(sistema.Id));
        var id = (await Context.PgiaSolicitacoesCidadao.FirstAsync(s => s.Protocolo == aberta.Protocolo)).Id;
        var ctx = await CtxAsync(UserOrgaoSes.Email);

        var emAnalise = await _service.MarcarEmAnaliseAsync(id, ctx);
        Assert.Equal("Em análise", emAnalise.Status);

        var noDpo = await _service.EncaminharAoDpoAsync(id, ctx);
        Assert.True(noDpo.EncaminhadaDpo);
        Assert.Equal(PgiaDominios.StatusSolicitacao.EncaminhadaDpo, noDpo.Status);
    }

    [Fact]
    public async Task Orgao_SolicitacaoInexistenteEhRejeitada()
    {
        var ctx = await CtxAsync(UserOrgaoSes.Email);

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _service.ResponderAsync(9999, new PgiaSolicitacaoRespostaDTO { Resposta = "Qualquer" }, ctx));

        Assert.Equal((int)ErrorCode.PgiaSolicitacaoNaoEncontrada, ex.Error.Code);
    }
}
