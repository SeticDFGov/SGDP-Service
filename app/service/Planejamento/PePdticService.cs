using System.Globalization;
using api.Common;
using api.Planejamento;
using app.Auth;
using demanda_service.Helpers;
using Microsoft.EntityFrameworkCore;
using Models;
using Models.Pgia;
using Models.Planejamento;
using service.Interface;

namespace service.Planejamento;

/// <summary>
/// A situação dos passos com o que o envio ao CGTIC usa a mais (E7): o que falta em cada passo
/// pendente ou em atenção, em linguagem simples, e a trilha e a análise usadas no cálculo.
/// </summary>
public sealed class PeSituacaoDetalhada
{
    public required PePdticSituacaoResponse Resposta { get; init; }

    // Passo (id) → o que falta nele (passos pendentes e em atenção)
    public required Dictionary<long, string> OQueFalta { get; init; }

    public required PeTrilhaOrgao Trilha { get; init; }

    public required PeAnaliseDono Analise { get; init; }

    // Os ciclos que valem para o PDTIC (os gravados e o plano da periodicidade, sem gravar); vazio
    // antes da publicação e sem o acompanhamento ligado (a conformidade da E8 confere os atrasados)
    public List<PeCiclo> Ciclos { get; init; } = new();
}

/// <summary>
/// O PDTIC de cada órgão (E4 e E7): abrir (um ciclo por vez), ler, listar os atuais (papéis
/// globais), as versões do órgão, a situação de cada passo da trilha com os avisos do guia, o
/// "não se aplica", a conferência dos temas das ações (incisos V, VI e IX) e os sistemas de IA
/// do PGIA (inciso VIII, só leitura). Os registros das seções ficam no motor
/// (PeRegistroService, dono PDTIC); os comentários, no PeComentarioService; o caminho da
/// aprovação (enviar, publicar, encerrar, revisar e registrar fora do sistema), no
/// PePdticAprovacaoService.
/// <list type="bullet">
/// <item>Situação do passo, nesta ordem: "atencao" quando há comentário aberto; "nao_se_aplica"
/// quando o órgão marcou (e a marca ainda vale); "externo" nas etapas 1 a 3 do PDTIC
/// registrado fora do sistema (menos 3.3 e 3.9); "aguardando" nas etapas 4 a 7 antes da
/// publicação; senão, pelo tipo: dados e conferência de temas (feito ou pendente pelos
/// obrigatórios), documento (feito com um PDF gerado), aprovação (feito com a decisão
/// tomada; devolvido pende com aviso), envio (feito depois do envio), deliberação (aguardando
/// o CGTIC, atenção na devolução, feito depois da aprovação), publicação (aguardando a
/// aprovação, pendente em aprovado, feito depois da publicação) e monitoramento (contínuo até
/// a rodada B).</item>
/// <item>Próximo passo: o primeiro atrasado, senão o primeiro em atenção, senão o primeiro
/// pendente, na ordem da trilha; na revisão que ninguém mexeu ainda, o primeiro passo da etapa
/// 2; encerrado e substituído não recomendam nada.</item>
/// <item>Avisos (não bloqueiam): fraqueza da SWOT sem necessidade ligada e ameaça sem risco
/// ligado (no passo da SWOT), equipe de elaboração só de TIC (no passo da equipe), decisão
/// "devolvido" nos passos de aprovação e a devolução do CGTIC (no passo da deliberação).</item>
/// </list>
/// </summary>
public class PePdticService : IPePdticService
{
    private const int TamanhoPaginaPadrao = 20;
    private const int TamanhoPaginaMaximo = 100;
    private const int MaximoJustificativa = 1000;

    public const string MotivoExterno = "Feito fora do sistema";
    public const string MotivoAntesDaPublicacao = "Disponível depois da publicação do PDTIC";
    public const string MotivoAntesDaAprovacao = "Disponível depois da aprovação do CGTIC";
    public const string AvisoDevolvido = "Devolvido: ajuste e registre a nova decisão.";
    public const string MotivoSemAvaliacao = "Abra uma avaliação intermediária quando o comitê pedir";

    /// <summary>O motivo do monitoramento sem ciclo porque a vigência terminou antes do começo do acompanhamento (C37).</summary>
    public static string MotivoVigenciaTerminada(DateOnly fim) =>
        $"A vigência do PDTIC terminou em {PeCiclos.Data(fim)}, antes do começo do acompanhamento no sistema: não há ciclo de monitoramento a registrar";

    private readonly AppDbContext _context;
    private readonly IPeRegistroService _registros;
    private readonly IPePermissionService _permissoes;

    public PePdticService(AppDbContext context, IPeRegistroService registros, IPePermissionService permissoes)
    {
        _context = context;
        _registros = registros;
        _permissoes = permissoes;
    }

    // ── PDTIC ───────────────────────────────────────────────────────────────

    public async Task<PePdticResponse?> AtualAsync(long? orgaoId, PeUserContext ctx)
    {
        var alvo = OrgaoAlvo(orgaoId, ctx);
        var atuais = await _context.PePdtics.AsNoTracking()
            .Where(p => p.OrgaoId == alvo && !PeDominios.SituacaoPdtic.Encerradas.Contains(p.Situacao))
            .OrderByDescending(p => p.Id)
            .ToListAsync();
        // A versão em elaboração (a revisão, quando há uma), senão a vigente
        var pdtic = atuais.FirstOrDefault(p => PeDominios.SituacaoPdtic.DaElaboracao.Contains(p.Situacao)) ?? atuais.FirstOrDefault();
        return pdtic == null ? null : await RespostaAsync(pdtic, ctx);
    }

    public async Task<List<PePdticResponse>> VersoesAsync(long? orgaoId, PeUserContext ctx)
    {
        var alvo = OrgaoAlvo(orgaoId, ctx);
        var pdtics = await _context.PePdtics.AsNoTracking()
            .Where(p => p.OrgaoId == alvo)
            .OrderByDescending(p => p.Id)
            .ToListAsync();
        if (pdtics.Count == 0) return new List<PePdticResponse>();
        var orgao = await _context.PgiaOrgaos.AsNoTracking().FirstAsync(o => o.Id == alvo);
        return await RespostasAsync(pdtics.Select(p => (Pdtic: p, Orgao: orgao)).ToList(), ctx);
    }

    public async Task<PePdticResponse> ObterAsync(long id, PeUserContext ctx) =>
        await RespostaAsync(await LerAsync(_context, _permissoes, id, ctx), ctx);

    public async Task<PePdticResponse> ResponderAsync(long id, PeUserContext ctx) =>
        await RespostaAsync(await _context.PePdtics.AsNoTracking().FirstAsync(p => p.Id == id), ctx);

    public async Task<PePdticResponse> AbrirAsync(PePdticCriarDTO dto, PeUserContext ctx)
    {
        var orgaoId = OrgaoParaCriar(dto.OrgaoId, ctx, "Só a equipe do órgão abre o PDTIC.", "Você só abre o PDTIC do seu próprio órgão.");

        var orgao = await _context.PgiaOrgaos.AsNoTracking().FirstOrDefaultAsync(o => o.Id == orgaoId && o.Ativo)
            ?? throw new ApiException(ErrorCode.PeOrgaoNaoEncontrado, "Órgão não encontrado ou desativado.");

        var anteriores = await _context.PePdtics.AsNoTracking()
            .Where(p => p.OrgaoId == orgaoId)
            .OrderByDescending(p => p.Id)
            .ToListAsync();
        if (anteriores.FirstOrDefault(p => !PeDominios.SituacaoPdtic.Encerradas.Contains(p.Situacao)) is { } atual)
            throw JaTemAtual(atual);

        var agora = DateTime.UtcNow;
        var pdtic = new PePdtic
        {
            OrgaoId = orgao.Id,
            Versao = anteriores.Count == 0 ? "1.0" : PePeticService.ProximaVersao(anteriores.Select(p => p.Versao)),
            Situacao = PeDominios.SituacaoPdtic.EmElaboracao,
            AnteriorId = anteriores.FirstOrDefault()?.Id,
            CriadoEm = agora,
            CriadoPor = ctx.Email
        };

        // O PDTIC e os princípios do art. 4º (as sugestões do passo 1.8, F2) numa transação: a
        // sequência dos códigos precisa do id do PDTIC, então são duas gravações
        await using var transacao = _context.Database.IsRelational() ? await _context.Database.BeginTransactionAsync() : null;
        _context.PePdtics.Add(pdtic);
        await _context.SaveChangesAsync();
        await _registros.SugerirPrincipiosDoArt4Async(pdtic, ctx);
        if (transacao != null) await transacao.CommitAsync();
        return await RespostaAsync(pdtic, ctx);
    }

    /// <summary>
    /// O órgão de um PDTIC novo (abrir ou registrar fora do sistema): a equipe do órgão, o
    /// próprio; o admin geral, o informado (ou o dele). Os outros papéis recebem 403.
    /// </summary>
    internal static long OrgaoParaCriar(long? informado, PeUserContext ctx, string soAEquipe, string soOProprio)
    {
        if (ctx.EhAdminGeral)
            return informado ?? ctx.OrgaoId ?? throw new ApiException(ErrorCode.PeOrgaoObrigatorio, "Escolha o órgão do PDTIC.");
        if (ctx.Papel != PapeisPlanejamento.Orgao) throw new ApiException(ErrorCode.PeSemPermissao, soAEquipe);
        var proprio = ctx.OrgaoId ?? throw SemOrgao();
        if (informado != null && informado != proprio) throw new ApiException(ErrorCode.PeSemPermissao, soOProprio);
        return proprio;
    }

    /// <summary>409 de quem tenta abrir (ou registrar) um PDTIC com outro em andamento no órgão.</summary>
    internal static ApiException JaTemAtual(PePdtic atual) => new(ErrorCode.PePdticJaExiste,
        PeDominios.SituacaoPdtic.Vigentes.Contains(atual.Situacao)
            ? $"O órgão já tem o PDTIC {atual.Versao} {PeDominios.SituacaoPdtic.RotuloMinusculo(atual.Situacao)}. "
              + "Para mudar o plano, abra uma revisão; um PDTIC novo só depois de encerrar este."
            : $"O órgão já tem um PDTIC em andamento (versão {atual.Versao}, {PeDominios.SituacaoPdtic.RotuloMinusculo(atual.Situacao)}). Continue por ele.");

    public async Task<PagedResponse<PePdticResponse>> ListarAsync(PePdticConsulta consulta, PeUserContext ctx)
    {
        if (!_permissoes.PodeVerConsolidado(ctx))
            throw new ApiException(ErrorCode.PeSemPermissao, "A lista dos PDTICs de todos os órgãos é da SGDI, da Secretaria do CGTIC e do administrador do módulo.");

        var pageSize = consulta.PageSize < 1 ? TamanhoPaginaPadrao : Math.Min(consulta.PageSize, TamanhoPaginaMaximo);
        var page = Math.Clamp(consulta.Page, 1, int.MaxValue / pageSize);

        var query = from p in _context.PePdtics.AsNoTracking()
                    join o in _context.PgiaOrgaos.AsNoTracking() on p.OrgaoId equals o.Id
                    select new { Pdtic = p, Orgao = o };
        if (string.IsNullOrWhiteSpace(consulta.Situacao))
        {
            // Sem filtro, os atuais (os encerrados e os substituídos só com o filtro deles)
            query = query.Where(x => !PeDominios.SituacaoPdtic.Encerradas.Contains(x.Pdtic.Situacao));
        }
        else
        {
            // Fora do domínio: lista vazia, nunca "todos" em silêncio. Encerrado e substituído
            // também valem (F2: antes a lista cortava os dois antes do filtro e voltava vazia)
            var situacao = consulta.Situacao.Trim();
            query = PeDominios.SituacaoPdtic.Todas.Contains(situacao)
                ? query.Where(x => x.Pdtic.Situacao == situacao)
                : query.Where(x => false);
        }
        if (!string.IsNullOrWhiteSpace(consulta.Filtro))
        {
            // ToLower().Contains() vale no Npgsql e no InMemory
            var filtro = consulta.Filtro.Trim().ToLower();
            query = query.Where(x => x.Orgao.Sigla.ToLower().Contains(filtro) || x.Orgao.Nome.ToLower().Contains(filtro));
        }

        var total = await query.CountAsync();
        var linhas = await query
            .OrderBy(x => x.Orgao.Sigla).ThenBy(x => x.Orgao.Nome).ThenBy(x => x.Pdtic.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new PagedResponse<PePdticResponse>(
            await RespostasAsync(linhas.Select(l => (l.Pdtic, l.Orgao)).ToList(), ctx), total, page, pageSize);
    }

    // ── Situação dos passos ─────────────────────────────────────────────────

    public async Task<PePdticSituacaoResponse> SituacaoAsync(long id, PeUserContext ctx)
    {
        var pdtic = await LerAsync(_context, _permissoes, id, ctx);
        var trilha = await PeTrilhaOrgao.DoPdticAsync(_context, pdtic);
        return (await DetalharSituacaoAsync(pdtic, trilha, ctx)).Resposta;
    }

    /// <summary>A situação de cada passo visível da trilha do órgão (sem conferir quem lê; com ctx, o PodeEditar de cada passo).</summary>
    public async Task<PePdticSituacaoResponse> CalcularSituacaoAsync(PePdtic pdtic, PeTrilhaOrgao trilha, PeUserContext? ctx = null) =>
        (await DetalharSituacaoAsync(pdtic, trilha, ctx)).Resposta;

    public async Task<PeSituacaoDetalhada> DetalharSituacaoAsync(PePdtic pdtic, PeTrilhaOrgao trilha, PeUserContext? ctx = null)
    {
        // As regras próprias do PDTIC (o registrado fora do sistema dispensa ligações) valem na trilha
        if (trilha.AjustadaParaPdtic != pdtic.Id)
        {
            if (trilha.AjustadaParaPdtic != null || pdtic.RegistradoExternamente) trilha = await PeTrilhaOrgao.DoPdticAsync(_context, pdtic);
            else trilha.AjustarAoPdtic(pdtic);
        }

        // O mesmo caminho da situação de vários PDTICs de uma vez (E8), com uma lista de um
        var leitura = await PeLeituraDaSituacao.CarregarAsync(_context, new[] { pdtic.Id }, trilha.Dados.Acompanhamento.Ativo);
        return Detalhar(pdtic, trilha, leitura, ctx != null && _permissoes.PodeEditarPdtic(ctx, pdtic.OrgaoId));
    }

    /// <summary>
    /// A situação de cada passo com tudo já lido (<see cref="PeLeituraDaSituacao"/>): o PDTIC, a
    /// trilha do órgão já ajustada a ele (<see cref="PeTrilhaOrgao.AjustarAoPdtic"/>) e se quem chama
    /// tem o papel de editar (o PodeEditar de cada passo). Não lê o banco: o painel e a conformidade
    /// da SGDI (E8) calculam aqui a situação de todos os órgãos, depois de ler tudo de uma vez.
    /// </summary>
    internal static PeSituacaoDetalhada Detalhar(PePdtic pdtic, PeTrilhaOrgao trilha, PeLeituraDaSituacao leitura, bool papelEdita)
    {
        var marcas = leitura.Marcas(pdtic.Id);
        var abertos = leitura.Abertos(pdtic.Id);
        var analise = leitura.Analisar(pdtic.Id, trilha.SecoesMontadas());
        var porSecao = analise.Secoes.ToDictionary(s => s.Secao.Secao.Id);

        // Acompanhamento (E7, rodada B; com a versão 6 do modelo inicial carregada): os ciclos (os
        // de monitoramento pelo plano da periodicidade, sem gravar), a avaliação intermediária mais
        // recente (as seções da avaliação são analisadas nela) e a espera da etapa 7
        var acompanhamento = trilha.Dados.Acompanhamento;
        var ciclos = acompanhamento.Ativo
            ? leitura.CiclosEfetivos(pdtic, trilha, analise)
            : new List<PeCiclo>();
        var avaliacao = ciclos.Where(c => c.Tipo == PeDominios.TipoCiclo.Avaliacao).OrderByDescending(c => c.Numero).FirstOrDefault();
        if (avaliacao != null)
        {
            var daAvaliacao = trilha.SecoesMontadas().Where(s => s.PorCiclo == PeDominios.TipoCiclo.Avaliacao).ToList();
            foreach (var secao in leitura.Analisar(pdtic.Id, daAvaliacao, avaliacao.Id).Secoes)
                porSecao[secao.Secao.Secao.Id] = secao;
        }
        var esperaDoFechamento = acompanhamento.Ativo ? EsperaDoFechamento(pdtic, trilha, porSecao, acompanhamento.DiasAvaliacaoFinal) : null;
        // Só as versões do PDF do PDTIC contam (as do RA e do RR, não)
        var temDocumento = trilha.Passos.Any(p => p.Tipo == PeDominios.TipoPasso.Documento) && leitura.TemDocumento(pdtic.Id);
        var deliberacao = pdtic.Situacao == PeDominios.SituacaoPdtic.Devolvido ? leitura.Deliberacoes(pdtic.Id).FirstOrDefault() : null;
        var etapas = PeEdicaoPdtic.EtapasDosPassos(trilha);

        // Marca que ainda vale (o passo continua opcional, aceita e não é travado)
        bool NaoSeAplica(PeTrilhaPasso passo) => marcas.ContainsKey(passo.Id) && RecusaDoNaoSeAplica(passo) == null;

        var avisos = Avisos(trilha, analise, NaoSeAplica);
        var temas = Temas(trilha, analise);
        var oQueFalta = new Dictionary<long, string>();
        // Os passos que a situação do PDTIC deixa editar agora (o próximo passo pendente sai deles)
        var editaveis = new HashSet<long>();

        var passos = trilha.Passos.Select(passo =>
        {
            var grupo = PeEdicaoPdtic.GrupoDe(etapas.GetValueOrDefault(passo.Id), passo.Tipo);
            if (PeEdicaoPdtic.Editavel(pdtic, grupo, passo.Chave)) editaveis.Add(passo.Id);
            var comentarios = abertos.GetValueOrDefault(passo.Id);
            var marcado = NaoSeAplica(passo);
            var doPasso = avisos.GetValueOrDefault(passo.Id) ?? new List<string>();
            string? motivo = null;
            string? falta = null;
            string situacao;

            if (comentarios > 0)
            {
                situacao = PeDominios.SituacaoPasso.Atencao;
                falta = "Há comentário aberto neste passo: responda e marque como resolvido.";
            }
            else if (marcado)
            {
                situacao = PeDominios.SituacaoPasso.NaoSeAplica;
            }
            else if (PeEdicaoPdtic.Externo(pdtic, grupo, passo.Chave))
            {
                situacao = PeDominios.SituacaoPasso.Externo;
                motivo = MotivoExterno;
            }
            else if (grupo == PeEdicaoPdtic.Grupo.Acompanhamento && PeDominios.SituacaoPdtic.DaElaboracao.Contains(pdtic.Situacao))
            {
                situacao = PeDominios.SituacaoPasso.Aguardando;
                motivo = MotivoAntesDaPublicacao;
            }
            else if (acompanhamento.Ativo && grupo == PeEdicaoPdtic.Grupo.Acompanhamento && passo.Tipo == PeDominios.TipoPasso.Monitoramento)
            {
                (situacao, motivo, falta) = PeloCiclo(pdtic, passo, ciclos, PeCiclos.Hoje());
            }
            else if (acompanhamento.Ativo && grupo == PeEdicaoPdtic.Grupo.Acompanhamento
                     && passo.Secoes.Any(s => s.PorCiclo == PeDominios.TipoCiclo.Avaliacao) && avaliacao == null)
            {
                // Encerrado ou substituído sem avaliação: não há mais o que esperar (F1, junto do C37)
                if (PeDominios.SituacaoPdtic.Encerradas.Contains(pdtic.Situacao))
                {
                    situacao = PeDominios.SituacaoPasso.NaoSeAplica;
                    motivo = pdtic.Situacao == PeDominios.SituacaoPdtic.Encerrado
                        ? "O PDTIC foi encerrado sem avaliação intermediária"
                        : "Esta versão do PDTIC foi substituída sem avaliação intermediária";
                }
                else
                {
                    situacao = PeDominios.SituacaoPasso.Aguardando;
                    motivo = MotivoSemAvaliacao;
                }
            }
            else if (esperaDoFechamento != null && etapas.GetValueOrDefault(passo.Id) == PeDominios.EtapaPdtic.Fechamento)
            {
                situacao = PeDominios.SituacaoPasso.Aguardando;
                motivo = esperaDoFechamento;
            }
            else
            {
                (situacao, motivo, falta) = PeloTipo(pdtic, trilha, passo, porSecao, temas, temDocumento, deliberacao, doPasso);
            }

            // O aviso da decisão "devolvido" aparece também quando o passo está em atenção
            if (situacao == PeDominios.SituacaoPasso.Atencao && passo.Tipo is PeDominios.TipoPasso.Aprovacao or PeDominios.TipoPasso.Envio
                && DecisaoDevolvida(passo, porSecao) && !doPasso.Contains(AvisoDevolvido))
                doPasso.Add(AvisoDevolvido);

            if (falta != null && situacao is PeDominios.SituacaoPasso.Pendente or PeDominios.SituacaoPasso.Atencao)
                oQueFalta[passo.Id] = falta;
            var marca = marcado ? marcas[passo.Id] : null;
            return new PePassoSituacaoResponse
            {
                PassoId = passo.Id,
                Chave = passo.Chave,
                Numero = passo.Numero,
                Situacao = situacao,
                Motivo = motivo,
                PodeEditar = papelEdita && editaveis.Contains(passo.Id),
                NaoSeAplica = marca == null
                    ? null
                    : new PeNaoSeAplicaResponse
                    {
                        Justificativa = marca.Justificativa ?? string.Empty,
                        MarcadoEm = marca.MarcadoEm,
                        MarcadoPor = marca.MarcadoPor,
                        MarcadoPorNome = leitura.Nomes.DeObrigatorio(marca.MarcadoPor)
                    },
                ComentariosAbertos = comentarios,
                Avisos = doPasso
            };
        }).ToList();

        return new PeSituacaoDetalhada
        {
            Resposta = ComProximoPasso(new PePdticSituacaoResponse { Passos = passos }, ProximoPasso(pdtic, trilha, passos, editaveis)),
            OQueFalta = oQueFalta,
            Trilha = trilha,
            Analise = analise,
            Ciclos = ciclos
        };
    }

    /// <summary>
    /// A situação pelo tipo do passo (fora da atenção, do "não se aplica", do externo e da
    /// espera da publicação), o motivo (aguardando) e o que falta (pendente), em texto.
    /// </summary>
    private static (string Situacao, string? Motivo, string? Falta) PeloTipo(PePdtic pdtic, PeTrilhaOrgao trilha, PeTrilhaPasso passo,
        IReadOnlyDictionary<long, PeSecaoAnalisada> porSecao, IReadOnlyList<PeTemaResponse> temas, bool temDocumento,
        PeDeliberacao? deliberacao, List<string> avisos)
    {
        const string feito = PeDominios.SituacaoPasso.Feito;
        const string pendente = PeDominios.SituacaoPasso.Pendente;
        var situacaoPdtic = pdtic.Situacao;

        switch (passo.Tipo)
        {
            case PeDominios.TipoPasso.Dados:
            {
                var falta = FaltaNasSecoes(passo, porSecao);
                return falta == null ? (feito, null, null) : (pendente, null, falta);
            }
            case PeDominios.TipoPasso.ConferenciaTemas:
            {
                var semAcao = temas.Where(t => t.Acoes.Count == 0 && string.IsNullOrWhiteSpace(t.Justificativa)).Select(t => t.Rotulo).ToList();
                var falta = FaltaNasSecoes(passo, porSecao);
                if (semAcao.Count > 0)
                    falta = $"Inclua uma ação com o tema ou justifique o tema sem ação: {Lista(semAcao)}." + (falta == null ? string.Empty : " " + falta);
                return falta == null ? (feito, null, null) : (pendente, null, falta);
            }
            case PeDominios.TipoPasso.Documento:
                return temDocumento ? (feito, null, null) : (pendente, null, "Gere o PDF do documento e confira a prévia.");
            case PeDominios.TipoPasso.Aprovacao:
            {
                var falta = FaltaNaAprovacao(passo, porSecao, avisos);
                return falta == null ? (feito, null, null) : (pendente, null, falta);
            }
            case PeDominios.TipoPasso.Envio:
            {
                if (DecisaoDevolvida(passo, porSecao)) avisos.Add(AvisoDevolvido);
                var enviado = situacaoPdtic is PeDominios.SituacaoPdtic.EmAprovacao or PeDominios.SituacaoPdtic.Aprovado
                              || PeDominios.SituacaoPdtic.Vigentes.Contains(situacaoPdtic)
                              || (PeDominios.SituacaoPdtic.Encerradas.Contains(situacaoPdtic) && pdtic.EnviadoEm != null);
                return enviado
                    ? (feito, null, null)
                    : (pendente, null, situacaoPdtic == PeDominios.SituacaoPdtic.Devolvido
                        ? "O CGTIC devolveu o PDTIC: ajuste o que foi pedido e envie de novo."
                        : "Registre a aprovação do SGTIC e envie o PDTIC ao CGTIC.");
            }
            case PeDominios.TipoPasso.Deliberacao:
            {
                if (pdtic.AprovadoEm != null) return (feito, null, null);
                if (situacaoPdtic == PeDominios.SituacaoPdtic.EmAprovacao)
                    return (PeDominios.SituacaoPasso.Aguardando, "Aguardando a deliberação do CGTIC desde " + DataBrasilia(pdtic.EnviadoEm), null);
                if (situacaoPdtic == PeDominios.SituacaoPdtic.Devolvido)
                {
                    var envio = trilha.Passos.FirstOrDefault(p => p.Tipo == PeDominios.TipoPasso.Envio);
                    var quando = deliberacao?.DecididoEm is DateTime decidido ? $" em {DataBrasilia(decidido)}" : string.Empty;
                    var observacao = string.IsNullOrWhiteSpace(deliberacao?.Observacao) ? string.Empty : $": {deliberacao!.Observacao!.Trim()}";
                    var reenvio = envio == null ? "envie de novo" : $"envie de novo no passo {envio.Numero}";
                    avisos.Add($"O CGTIC devolveu o PDTIC{quando}{observacao}{(observacao.EndsWith('.') ? string.Empty : ".")} Ajuste o que foi pedido e {reenvio}.");
                    return (PeDominios.SituacaoPasso.Atencao, null, "O CGTIC devolveu o PDTIC: veja a observação da Secretaria.");
                }
                return (pendente, null, "Envie o PDTIC ao CGTIC.");
            }
            case PeDominios.TipoPasso.Publicacao:
            {
                if (pdtic.PublicadoEm != null) return (feito, null, null);
                if (situacaoPdtic == PeDominios.SituacaoPdtic.Aprovado)
                    return (pendente, null, "Registre a data e o endereço da publicação e confirme.");
                return (PeDominios.SituacaoPasso.Aguardando, MotivoAntesDaAprovacao, null);
            }
            default:
                // Monitoramento (rodada B: por ciclo) e fluxo: contínuos
                return (PeDominios.SituacaoPasso.Continuo, null, null);
        }
    }

    /// <summary>
    /// Os passos do monitoramento (5.1 e 5.2) pelos ciclos (E7, rodada B): atrasado quando algum
    /// ciclo começado passou do prazo sem fechar; pendente quando há ciclo aberto no prazo; feito
    /// quando todos os ciclos começados estão fechados; aguardando enquanto nenhum começou. Sem
    /// ciclo nenhum (F1, achado C37): "não se aplica" quando nunca haverá ciclo (a vigência terminou
    /// antes do começo do acompanhamento, como no PDTIC registrado fora do sistema depois do fim
    /// dela, ou o PDTIC foi encerrado ou substituído sem ciclo), com o motivo; aguardando só
    /// enquanto falta a vigência.
    /// </summary>
    private static (string Situacao, string? Motivo, string? Falta) PeloCiclo(PePdtic pdtic, PeTrilhaPasso passo, IReadOnlyList<PeCiclo> ciclos,
        DateOnly hoje)
    {
        var monitoramento = ciclos.Where(c => c.Tipo == PeDominios.TipoCiclo.Monitoramento).OrderBy(c => c.Inicio).ToList();
        if (monitoramento.Count == 0)
        {
            if (pdtic.VigenciaFim is DateOnly fim && PeCiclos.InicioDoAcompanhamento(pdtic) > fim)
                return (PeDominios.SituacaoPasso.NaoSeAplica, MotivoVigenciaTerminada(fim), null);
            if (PeDominios.SituacaoPdtic.Encerradas.Contains(pdtic.Situacao))
                return (PeDominios.SituacaoPasso.NaoSeAplica, pdtic.Situacao == PeDominios.SituacaoPdtic.Encerrado
                    ? "O PDTIC foi encerrado sem ciclo de monitoramento"
                    : "Esta versão do PDTIC foi substituída sem ciclo de monitoramento", null);
            return (PeDominios.SituacaoPasso.Aguardando, "Os ciclos de monitoramento saem da vigência do PDTIC (passo da abrangência)", null);
        }
        var comecados = monitoramento.Where(c => c.Inicio <= hoje).ToList();
        if (comecados.Count == 0)
            return (PeDominios.SituacaoPasso.Aguardando, $"O primeiro ciclo de monitoramento começa em {PeCiclos.Data(monitoramento[0].Inicio)}", null);

        var fechar = passo.Chave == PeDominios.ChaveAcompanhamento.PassoRelatorioAcompanhamento;
        var atrasados = comecados
            .Where(c => PeCiclos.Exibida(c, hoje) == PeDominios.SituacaoCicloExibida.Atrasado)
            .ToList();
        if (atrasados.Count > 0)
        {
            var motivo = atrasados.Count == 1
                ? $"O ciclo {atrasados[0].Rotulo} passou do prazo de fechamento ({PeCiclos.Data(atrasados[0].Prazo!.Value)}) e ainda está aberto"
                : $"Os ciclos {Lista(atrasados.Select(c => c.Rotulo).ToList())} passaram do prazo de fechamento e ainda estão abertos";
            return (PeDominios.SituacaoPasso.Atrasado, motivo, $"{motivo}. Registre os dados e feche o ciclo.");
        }
        var aberto = comecados.FirstOrDefault(c => c.Situacao == PeDominios.SituacaoCiclo.Aberto);
        if (aberto != null)
            return (PeDominios.SituacaoPasso.Pendente, null, fechar
                ? $"Feche o ciclo {aberto.Rotulo} até {PeCiclos.Data(aberto.Prazo ?? aberto.Inicio)}."
                : $"Registre a situação das ações do ciclo {aberto.Rotulo} até {PeCiclos.Data(aberto.Prazo ?? aberto.Inicio)}.");
        return (PeDominios.SituacaoPasso.Feito, null, null);
    }

    /// <summary>
    /// A etapa 7 (feche o ciclo) espera até faltarem os dias da configuração dias_avaliacao_final
    /// para o fim da vigência, ou até a equipe gravar algum dado nela. Devolve o motivo da espera
    /// (com a data), ou nulo quando a etapa já está disponível (ou o PDTIC não está vigente).
    /// </summary>
    private static string? EsperaDoFechamento(PePdtic pdtic, PeTrilhaOrgao trilha, IReadOnlyDictionary<long, PeSecaoAnalisada> porSecao, int dias)
    {
        if (!PeDominios.SituacaoPdtic.Vigentes.Contains(pdtic.Situacao) || pdtic.VigenciaFim is not DateOnly fim) return null;
        var etapa = trilha.Etapas.FirstOrDefault(e => e.Chave == PeDominios.EtapaPdtic.Fechamento);
        if (etapa == null) return null;
        var comDado = etapa.Passos.SelectMany(p => p.Secoes).Any(s => porSecao.TryGetValue(s.Id, out var analisada) && analisada.Registros.Count > 0);
        var abre = fim.AddDays(-dias);
        if (comDado || abre <= PeCiclos.Hoje()) return null;
        return $"Disponível a partir de {PeCiclos.Data(abre)}, {dias.ToString(CultureInfo.InvariantCulture)} dias antes do fim da vigência ({PeCiclos.Data(fim)})";
    }

    /// <summary>O que falta nas seções visíveis do passo (a seção obrigatória sem registro e os obrigatórios vazios), ou nulo.</summary>
    internal static string? FaltaNasSecoes(PeTrilhaPasso passo, IReadOnlyDictionary<long, PeSecaoAnalisada> porSecao)
    {
        var faltas = new List<string>();
        foreach (var secao in passo.Secoes)
        {
            if (!porSecao.TryGetValue(secao.Id, out var analisada) || analisada.Completa) continue;
            if (analisada.FaltaRegistro)
            {
                faltas.Add(analisada.Secao.EhFormulario
                    ? $"Preencha \"{analisada.Secao.Secao.Titulo}\"."
                    : $"Inclua pelo menos um item em \"{analisada.Secao.Secao.Titulo}\".");
                continue;
            }
            foreach (var (registro, campos) in analisada.Incompletos)
                faltas.Add($"{registro.Codigo ?? analisada.Secao.Secao.Titulo}: preencha {Lista(campos.Select(c => $"\"{c.Rotulo}\"").ToList())}.");
        }
        if (faltas.Count == 0) return null;
        return faltas.Count <= 2
            ? string.Join(" ", faltas)
            : string.Join(" ", faltas.Take(2)) + $" E mais {faltas.Count - 2} pendências neste passo.";
    }

    /// <summary>
    /// O que falta num passo de aprovação: as seções completas e a decisão tomada (aprovado; na
    /// avaliação do comitê, seguir ou revisar). A decisão "devolvido" pende, com o aviso.
    /// </summary>
    internal static string? FaltaNaAprovacao(PeTrilhaPasso passo, IReadOnlyDictionary<long, PeSecaoAnalisada> porSecao, List<string> avisos)
    {
        var devolvido = DecisaoDevolvida(passo, porSecao);
        if (devolvido) avisos.Add(AvisoDevolvido);

        string? semDecisao = null;
        foreach (var secao in passo.Secoes.Where(s => s.Campos.Any(c => c.Chave == PeDominios.ChavePdtic.CampoDecisao)))
        {
            var decisao = Decisao(porSecao.GetValueOrDefault(secao.Id));
            if (decisao == null || !PeDominios.Decisao.Tomadas.Contains(decisao))
                semDecisao = devolvido
                    ? "A decisão registrada foi \"Devolvido\": ajuste o que foi pedido e registre a nova decisão."
                    : "Registre a decisão e a data.";
        }
        return semDecisao ?? FaltaNasSecoes(passo, porSecao);
    }

    /// <summary>A decisão de uma seção de aprovação (o campo decisao do registro do formulário), ou nulo.</summary>
    internal static string? Decisao(PeSecaoAnalisada? secao) =>
        secao?.Registros.FirstOrDefault() is { } registro
            ? PeRegistroDados.Texto(PeRegistroDados.Ler(registro.Dados)[PeDominios.ChavePdtic.CampoDecisao])
            : null;

    /// <summary>Alguma seção de decisão do passo está com "devolvido".</summary>
    private static bool DecisaoDevolvida(PeTrilhaPasso passo, IReadOnlyDictionary<long, PeSecaoAnalisada> porSecao) =>
        passo.Secoes.Any(s => s.Campos.Any(c => c.Chave == PeDominios.ChavePdtic.CampoDecisao)
                              && Decisao(porSecao.GetValueOrDefault(s.Id)) == PeDominios.Decisao.Devolvido);

    /// <summary>O motivo do próximo passo na revisão recém-aberta (F1, achado B12).</summary>
    public const string MotivoInicioDaRevisao =
        "Revise o PDTIC a partir do diagnóstico (etapa 2): os dados da versão anterior já estão nos passos. Confira cada um e ajuste o que mudou.";

    private static PePdticSituacaoResponse ComProximoPasso(PePdticSituacaoResponse resposta, (string? Numero, string? Motivo) proximo)
    {
        resposta.ProximoPasso = proximo.Numero;
        resposta.ProximoPassoMotivo = proximo.Motivo;
        return resposta;
    }

    /// <summary>
    /// O passo recomendado: o primeiro atrasado, senão o primeiro em atenção (responder ao
    /// comentário ou à devolução sempre se faz), senão (na revisão que ninguém mexeu ainda) o
    /// primeiro passo da etapa 2, com o motivo próprio (<see cref="MotivoInicioDaRevisao"/>),
    /// senão o primeiro pendente que a situação do PDTIC deixa fazer agora (o pendente de uma
    /// etapa fechada não é recomendado: por exemplo, um passo opcional da elaboração depois do envio).
    /// </summary>
    private static (string? Numero, string? Motivo) ProximoPasso(PePdtic pdtic, PeTrilhaOrgao trilha, IReadOnlyList<PePassoSituacaoResponse> passos,
        IReadOnlySet<long> editaveis)
    {
        if (PeDominios.SituacaoPdtic.Encerradas.Contains(pdtic.Situacao)) return (null, null);
        var atrasado = passos.FirstOrDefault(p => p.Situacao == PeDominios.SituacaoPasso.Atrasado);
        if (atrasado != null) return (atrasado.Numero, null);
        var atencao = passos.FirstOrDefault(p => p.Situacao == PeDominios.SituacaoPasso.Atencao);
        if (atencao != null) return (atencao.Numero, null);
        if (PeEdicaoPdtic.EhRevisao(pdtic) && pdtic.AlteradoEm == null && pdtic.Situacao == PeDominios.SituacaoPdtic.EmElaboracao
            && trilha.Etapas.FirstOrDefault(e => e.Chave == PeDominios.EtapaPdtic.Diagnostico)?.Passos.FirstOrDefault() is { } inicio)
            return (inicio.Numero, MotivoInicioDaRevisao);
        return (passos.FirstOrDefault(p => p.Situacao == PeDominios.SituacaoPasso.Pendente && editaveis.Contains(p.PassoId))?.Numero, null);
    }

    /// <summary>A data em Brasília, dd/mm/aaaa (vazio sem data).</summary>
    internal static string DataBrasilia(DateTime? utc) =>
        utc is DateTime data ? DateTimeHelper.ToBrasilia(data).ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) : string.Empty;

    /// <summary>
    /// Avisos do guia, por passo: fraqueza da SWOT sem necessidade ligada e ameaça sem risco
    /// ligado (no passo da SWOT, quando o campo de ligação aparece para o órgão) e equipe de
    /// elaboração só de TIC (no passo da equipe). Passo marcado "não se aplica" não avisa.
    /// </summary>
    private static Dictionary<long, List<string>> Avisos(PeTrilhaOrgao trilha, PeAnaliseDono analise, Func<PeTrilhaPasso, bool> naoSeAplica)
    {
        var avisos = new Dictionary<long, List<string>>();
        void Avisar(long passoId, string texto)
        {
            if (!avisos.TryGetValue(passoId, out var lista)) avisos[passoId] = lista = new List<string>();
            lista.Add(texto);
        }

        // Fraqueza sem necessidade e ameaça sem risco: a origem da SWOT é ligada pelo campo da outra seção
        void SemLigacao(string secaoSwot, string secaoQueLiga, string campoQueLiga, string uma, string varias, string complemento)
        {
            var swot = trilha.Secao(secaoSwot);
            var liga = trilha.Secao(secaoQueLiga);
            var campo = trilha.Campo(secaoQueLiga, campoQueLiga);
            if (swot == null || liga == null || campo == null || naoSeAplica(swot.Value.Passo) || naoSeAplica(liga.Value.Passo)) return;

            var ligadas = analise.Vinculos.Where(v => v.CampoId == campo.Id).Select(v => v.RegistroDestinoId).ToHashSet();
            var soltas = (analise.Secao(secaoSwot)?.Registros ?? new List<PeRegistro>())
                .Where(r => !ligadas.Contains(r.Id))
                .Select(r => r.Codigo ?? $"#{r.Ordem}")
                .ToList();
            if (soltas.Count == 0) return;
            var texto = soltas.Count == 1 ? string.Format(uma, soltas[0]) : string.Format(varias, Lista(soltas));
            Avisar(swot.Value.Passo.Id, $"{texto} {string.Format(complemento, liga.Value.Passo.Numero)}");
        }

        SemLigacao(PeDominios.ChavePdtic.SecaoFraquezas, PeDominios.ChavePdtic.SecaoNecessidades, PeDominios.ChavePdtic.CampoFraqueza,
            "A fraqueza {0} ainda não tem necessidade de TIC ligada.",
            "As fraquezas {0} ainda não têm necessidade de TIC ligada.",
            "O guia recomenda que cada fraqueza vire pelo menos uma necessidade: faça a ligação no passo {0}.");
        SemLigacao(PeDominios.ChavePdtic.SecaoAmeacas, PeDominios.ChavePdtic.SecaoRiscos, PeDominios.ChavePdtic.CampoAmeaca,
            "A ameaça {0} ainda não tem risco ligado.",
            "As ameaças {0} ainda não têm risco ligado.",
            "O guia recomenda tratar cada ameaça no plano de riscos: faça a ligação no passo {0}.");

        // Equipe de elaboração só de TIC (o guia pede também as áreas finalísticas)
        var equipe = trilha.Secao(PeDominios.ChavePdtic.SecaoEquipe);
        if (equipe != null && trilha.Campo(PeDominios.ChavePdtic.SecaoEquipe, PeDominios.ChavePdtic.CampoTipoArea) != null
            && !naoSeAplica(equipe.Value.Passo))
        {
            var areas = (analise.Secao(PeDominios.ChavePdtic.SecaoEquipe)?.Registros ?? new List<PeRegistro>())
                .Select(r => PeRegistroDados.Texto(PeRegistroDados.Ler(r.Dados)[PeDominios.ChavePdtic.CampoTipoArea]))
                .Where(a => a != null)
                .ToList();
            if (areas.Count > 0 && areas.All(a => a == PeDominios.ChavePdtic.AreaTic))
                Avisar(equipe.Value.Passo.Id,
                    "A equipe de elaboração tem só pessoas da área de TIC. O guia recomenda incluir também pessoas das áreas finalísticas do órgão.");
        }
        return avisos;
    }

    /// <summary>"D01", "D01 e D02", "D01, D02 e D03", até cinco e depois "e mais N".</summary>
    internal static string Lista(IReadOnlyList<string> itens)
    {
        if (itens.Count > 5) return string.Join(", ", itens.Take(5)) + $" e mais {itens.Count - 5}";
        return itens.Count == 1 ? itens[0] : string.Join(", ", itens.Take(itens.Count - 1)) + " e " + itens[^1];
    }

    // ── Não se aplica ───────────────────────────────────────────────────────

    public async Task<PePassoSituacaoResponse> MarcarNaoSeAplicaAsync(long id, long passoId, PeNaoSeAplicaDTO dto, PeUserContext ctx)
    {
        var (pdtic, trilha, passo) = await PassoParaMarcarAsync(id, passoId, ctx);
        if (RecusaDoNaoSeAplica(passo) is string recusa) throw new ApiException(ErrorCode.PeNaoSeAplicaRecusado, recusa);

        var justificativa = dto.Justificativa?.Trim();
        if (string.IsNullOrEmpty(justificativa))
            throw new ApiException(ErrorCode.PeJustificativaObrigatoria, "Explique por que este passo não se aplica ao órgão.");
        if (justificativa.Length > MaximoJustificativa)
            throw new ApiException(ErrorCode.PeDadosInvalidos, $"A justificativa tem no máximo {MaximoJustificativa} caracteres.");

        var agora = DateTime.UtcNow;
        var linha = await _context.PePdticPassos.FirstOrDefaultAsync(p => p.PdticId == id && p.PassoId == passoId);
        if (linha == null)
        {
            linha = new PePdticPasso { PdticId = id, PassoId = passoId };
            _context.PePdticPassos.Add(linha);
        }
        linha.NaoSeAplica = true;
        linha.Justificativa = justificativa;
        linha.MarcadoEm = agora;
        linha.MarcadoPor = ctx.Email;
        pdtic.AlteradoEm = agora;
        pdtic.AlteradoPor = ctx.Email;
        await _context.SaveChangesAsync();

        return (await CalcularSituacaoAsync(pdtic, trilha, ctx)).Passos.Single(p => p.PassoId == passoId);
    }

    public async Task<PePassoSituacaoResponse> DesmarcarNaoSeAplicaAsync(long id, long passoId, PeUserContext ctx)
    {
        var (pdtic, trilha, _) = await PassoParaMarcarAsync(id, passoId, ctx);

        var linha = await _context.PePdticPassos.FirstOrDefaultAsync(p => p.PdticId == id && p.PassoId == passoId);
        if (linha is { NaoSeAplica: true })
        {
            var agora = DateTime.UtcNow;
            linha.NaoSeAplica = false;
            linha.Justificativa = null;
            linha.MarcadoEm = agora;
            linha.MarcadoPor = ctx.Email;
            pdtic.AlteradoEm = agora;
            pdtic.AlteradoPor = ctx.Email;
            await _context.SaveChangesAsync();
        }
        return (await CalcularSituacaoAsync(pdtic, trilha, ctx)).Passos.Single(p => p.PassoId == passoId);
    }

    /// <summary>
    /// "Não se aplica" só em passo opcional no nível do órgão (e nos ajustes), que aceite e não
    /// seja travado. Devolve o motivo da recusa, ou nulo quando pode.
    /// </summary>
    public static string? RecusaDoNaoSeAplica(PeTrilhaPasso passo)
    {
        if (passo.Travado)
            return "Este passo é um dos nove conteúdos mínimos do PDTIC (art. 12, § 2º, do Decreto nº 48.900/2026) e não pode ficar como \"não se aplica\".";
        if (!passo.AceitaNaoSeAplica) return "Este passo não aceita \"não se aplica\".";
        if (passo.Situacao != PeDominios.Situacao.Opcional)
            return "Este passo é obrigatório no nível do órgão e não pode ficar como \"não se aplica\".";
        return null;
    }

    /// <summary>O passo para marcar ou desfazer o "não se aplica": a equipe do órgão, com o passo editável pela situação e pela etapa.</summary>
    private async Task<(PePdtic Pdtic, PeTrilhaOrgao Trilha, PeTrilhaPasso Passo)> PassoParaMarcarAsync(long id, long passoId, PeUserContext ctx)
    {
        var pdtic = await LerAsync(_context, _permissoes, id, ctx, rastrear: true);
        if (!_permissoes.PodeEditarPdtic(ctx, pdtic.OrgaoId))
            throw new ApiException(ErrorCode.PeSemPermissao, "Só a equipe do órgão marca um passo como \"não se aplica\".");

        var trilha = await PeTrilhaOrgao.DoPdticAsync(_context, pdtic);
        var passo = trilha.Passo(passoId)
            ?? throw new ApiException(ErrorCode.PePassoIndisponivel, "Este passo não está na trilha do órgão. Atualize a tela.");
        if (PeEdicaoPdtic.Recusa(pdtic, PeEdicaoPdtic.GrupoDoPasso(trilha, passo), passo.Chave) is string fechado)
            throw PeEdicaoPdtic.Fechado(fechado);
        return (pdtic, trilha, passo);
    }

    // ── Temas das ações (incisos V, VI e IX) ────────────────────────────────

    public async Task<PeTemasResponse> TemasAsync(long id, PeUserContext ctx)
    {
        var pdtic = await LerAsync(_context, _permissoes, id, ctx);
        var trilha = await PeTrilhaOrgao.CarregarAsync(_context, pdtic.OrgaoId, soAtivo: false);
        var secoes = new[] { PeDominios.TemaDecreto.SecaoAcoes, PeDominios.TemaDecreto.SecaoJustificativas }
            .Select(chave => trilha.Secao(chave))
            .Where(s => s != null)
            .Select(s => trilha.Montar(s!.Value.Secao))
            .ToList();
        var analise = await _registros.AnalisarAsync(PeDono.DoPdtic(id), secoes);
        return new PeTemasResponse { Temas = Temas(trilha, analise) };
    }

    /// <summary>
    /// Os três temas do decreto com as ações do PDTIC marcadas em cada um (campo acoes.tema) e a
    /// justificativa de tema sem ação (seção temas_sem_acao, quando o campo aparece para o órgão).
    /// </summary>
    private static List<PeTemaResponse> Temas(PeTrilhaOrgao trilha, PeAnaliseDono analise)
    {
        var acoes = analise.Secao(PeDominios.TemaDecreto.SecaoAcoes);
        var campoTema = trilha.Dados.SecaoPorChave(PeDominios.TemaDecreto.SecaoAcoes) is PeSecao secaoAcoes
            ? trilha.Dados.CamposDaSecao(secaoAcoes.Id, incluirExcluidos: true).FirstOrDefault(c => c.Chave == PeDominios.TemaDecreto.CampoTema)
            : null;
        var opcoesTema = campoTema == null ? new List<PeOpcao>() : trilha.Dados.OpcoesDoCampo(campoTema.Id).ToList();
        var campoSituacao = acoes?.Secao.Visiveis.FirstOrDefault(v => v.Campo.Chave == PeDominios.TemaDecreto.CampoSituacao)?.Campo;

        var justificativas = analise.Secao(PeDominios.TemaDecreto.SecaoJustificativas);
        var dadosJustificativa = PeRegistroDados.Ler(justificativas?.Registros.FirstOrDefault()?.Dados);

        return PeDominios.TemaDecreto.Todos.Select(tema =>
        {
            var doTema = (acoes?.Registros ?? new List<PeRegistro>())
                .Select(r => (Registro: r, Dados: PeRegistroDados.Ler(r.Dados)))
                .Where(x => PeRegistroDados.Textos(x.Dados[PeDominios.TemaDecreto.CampoTema]).Contains(tema.Valor))
                .Select(x => new PeTemaAcaoResponse
                {
                    RegistroId = x.Registro.Id,
                    Codigo = x.Registro.Codigo,
                    Descricao = PeRegistroDados.Texto(x.Dados[PeDominios.TemaDecreto.CampoDescricao]) ?? string.Empty,
                    Situacao = campoSituacao == null
                        ? null
                        : PeValores.Rotulo(campoSituacao, acoes!.Secao.OpcoesDe(campoSituacao), x.Dados[campoSituacao.Chave])
                })
                .ToList();
            var justificativaVisivel = justificativas?.Secao.Visiveis.Any(v => v.Campo.Chave == tema.CampoJustificativa) == true;
            var justificativa = justificativaVisivel ? PeRegistroDados.Texto(dadosJustificativa[tema.CampoJustificativa]) : null;
            return new PeTemaResponse
            {
                Valor = tema.Valor,
                Rotulo = opcoesTema.FirstOrDefault(o => o.Valor == tema.Valor)?.Rotulo ?? tema.Valor,
                Inciso = tema.Inciso,
                Acoes = doTema,
                Justificativa = string.IsNullOrWhiteSpace(justificativa) ? null : justificativa
            };
        }).ToList();
    }

    // ── Sistemas de IA do PGIA (inciso VIII) ────────────────────────────────

    public async Task<List<PeSistemaIaPgiaResponse>> SistemasIaAsync(long id, PeUserContext ctx)
    {
        var pdtic = await LerAsync(_context, _permissoes, id, ctx);
        return await _context.PgiaSistemasIa.AsNoTracking()
            .Where(s => s.OrgaoId == pdtic.OrgaoId)
            .OrderBy(s => s.Denominacao).ThenBy(s => s.Id)
            .Select(s => new PeSistemaIaPgiaResponse
            {
                Id = s.Id,
                Nome = s.Denominacao,
                Finalidade = s.Finalidade,
                Classificacao = s.ClassificacaoRiscoAtual,
                Base = s.EnquadramentoLegal,
                Situacao = s.StatusCicloVida
            })
            .ToListAsync();
    }

    // ── Apoio ───────────────────────────────────────────────────────────────

    /// <summary>
    /// O PDTIC para quem pode ver o órgão dele (papéis globais, admin geral e os dois papéis
    /// do próprio órgão): 404 se não existe, 403 se é de outro órgão.
    /// </summary>
    internal static async Task<PePdtic> LerAsync(AppDbContext context, IPePermissionService permissoes, long id, PeUserContext ctx,
        bool rastrear = false)
    {
        if (!permissoes.PodeLerReferenciais(ctx))
            throw new ApiException(ErrorCode.PeSemPermissao, "Você ainda não tem papel na Governança Estratégica. Fale com o administrador do módulo.");
        var consulta = rastrear ? context.PePdtics : context.PePdtics.AsNoTracking();
        var pdtic = await consulta.FirstOrDefaultAsync(p => p.Id == id) ?? throw NaoEncontrado();
        if (!permissoes.PodeVerOrgao(ctx, pdtic.OrgaoId))
            throw new ApiException(ErrorCode.PeSemPermissao, "Você só vê o PDTIC do seu próprio órgão.");
        return pdtic;
    }

    internal static ApiException NaoEncontrado() => new(ErrorCode.PePdticNaoEncontrado, "PDTIC não encontrado. Atualize a tela.");

    /// <summary>A elaboração fechada (fora de em_elaboracao e devolvido): 409 com a mensagem da situação.</summary>
    internal static ApiException Fechado(PePdtic pdtic) => PeEdicaoPdtic.ElaboracaoFechada(pdtic);

    private static ApiException SemOrgao() => new(ErrorCode.PeOrgaoNaoEncontrado,
        "Seu usuário ainda não está ligado a um órgão. Fale com o administrador do módulo.");

    /// <summary>
    /// O órgão pedido: com orgaoId, quem vê o órgão; sem ele, o da própria pessoa (papel de
    /// órgão, ou papel global com órgão; papel global sem órgão precisa dizer qual).
    /// </summary>
    private long OrgaoAlvo(long? orgaoId, PeUserContext ctx)
    {
        if (orgaoId != null)
        {
            if (!_permissoes.PodeVerOrgao(ctx, orgaoId.Value))
                throw new ApiException(ErrorCode.PeSemPermissao, "Você só vê o PDTIC do seu próprio órgão.");
            return orgaoId.Value;
        }
        if (PapeisPlanejamento.EhDeOrgao(ctx.Papel)) return ctx.OrgaoId ?? throw SemOrgao();
        if (_permissoes.VeTodosOsOrgaos(ctx))
            return ctx.OrgaoId ?? throw new ApiException(ErrorCode.PeOrgaoObrigatorio, "Escolha o órgão para ver o PDTIC dele.");
        throw new ApiException(ErrorCode.PeSemPermissao, "Você ainda não tem papel na Governança Estratégica. Fale com o administrador do módulo.");
    }

    private async Task<PePdticResponse> RespostaAsync(PePdtic pdtic, PeUserContext ctx)
    {
        var orgao = await _context.PgiaOrgaos.AsNoTracking().FirstAsync(o => o.Id == pdtic.OrgaoId);
        return (await RespostasAsync(new List<(PePdtic, PgiaOrgao)> { (pdtic, orgao) }, ctx))[0];
    }

    /// <summary>As respostas de uma lista de PDTICs, com o nível, a deliberação mais recente e a versão revista (poucas consultas para a lista inteira).</summary>
    private async Task<List<PePdticResponse>> RespostasAsync(IReadOnlyList<(PePdtic Pdtic, PgiaOrgao Orgao)> itens, PeUserContext ctx)
    {
        var niveis = await PeTrilhaOrgao.NiveisDosOrgaosAsync(_context, itens.Select(i => i.Orgao.Id));
        var deliberacoes = await PeDeliberacaoService.UltimasDosPdticsAsync(_context, itens.Select(i => i.Pdtic.Id).ToList());
        var idsAnteriores = itens.Where(i => PeEdicaoPdtic.EhRevisao(i.Pdtic)).Select(i => i.Pdtic.AnteriorId!.Value).Distinct().ToList();
        var nomes = await PeNomes.CarregarAsync(_context, itens.Select(i => i.Pdtic.CriadoPor));
        var anteriores = idsAnteriores.Count == 0
            ? new Dictionary<long, string>()
            : await _context.PePdtics.AsNoTracking().Where(p => idsAnteriores.Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => p.Versao);

        return itens.Select(i =>
        {
            var pdtic = i.Pdtic;
            var nivel = niveis.GetValueOrDefault(i.Orgao.Id);
            return new PePdticResponse
            {
                Id = pdtic.Id,
                OrgaoId = i.Orgao.Id,
                OrgaoSigla = i.Orgao.Sigla,
                OrgaoNome = i.Orgao.Nome,
                Versao = pdtic.Versao,
                Situacao = pdtic.Situacao,
                VigenciaInicio = pdtic.VigenciaInicio,
                VigenciaFim = pdtic.VigenciaFim,
                RegistradoExternamente = pdtic.RegistradoExternamente,
                AnteriorId = pdtic.AnteriorId,
                NivelId = nivel?.Id,
                NivelNome = nivel?.Nome,
                PodeEditar = _permissoes.PodeEditarPdtic(ctx, i.Orgao.Id) && PeEdicaoPdtic.ElaboracaoAberta(pdtic),
                CriadoEm = pdtic.CriadoEm,
                CriadoPor = pdtic.CriadoPor,
                CriadoPorNome = nomes.DeObrigatorio(pdtic.CriadoPor),
                EnviadoEm = pdtic.EnviadoEm,
                AprovadoEm = pdtic.AprovadoEm,
                PublicadoEm = pdtic.PublicadoEm,
                EncerradoEm = pdtic.EncerradoEm,
                EncerramentoMotivo = pdtic.EncerramentoMotivo,
                Deliberacao = deliberacoes.GetValueOrDefault(pdtic.Id),
                Revisao = PeEdicaoPdtic.EhRevisao(pdtic) && anteriores.TryGetValue(pdtic.AnteriorId!.Value, out var versaoAnterior)
                    ? new PePdticRevisaoResponse
                    {
                        AnteriorId = pdtic.AnteriorId!.Value,
                        AnteriorVersao = versaoAnterior,
                        Justificativa = pdtic.RevisaoJustificativa
                    }
                    : null
            };
        }).ToList();
    }
}

/// <summary>
/// Comentários dos passos do PDTIC (E4), pelo quadro de papéis do plano (seção 4.1): o
/// administrador do módulo, a SGDI e o admin geral comentam (a Secretaria do CGTIC não); a
/// equipe do órgão responde e marca como resolvido; quem comentou também resolve. Um nível
/// de resposta. Quem vê o órgão lê.
/// </summary>
public class PeComentarioService : IPeComentarioService
{
    public const int MaximoTexto = 2000;

    private readonly AppDbContext _context;
    private readonly IPePermissionService _permissoes;

    public PeComentarioService(AppDbContext context, IPePermissionService permissoes)
    {
        _context = context;
        _permissoes = permissoes;
    }

    public async Task<List<PeComentarioResponse>> ListarAsync(long pdticId, long? passoId, PeUserContext ctx)
    {
        await PePdticService.LerAsync(_context, _permissoes, pdticId, ctx);
        var query = _context.PeComentarios.AsNoTracking().Where(c => c.PdticId == pdticId);
        if (passoId != null) query = query.Where(c => c.PassoId == passoId);
        var todos = await query.OrderBy(c => c.CriadoEm).ThenBy(c => c.Id).ToListAsync();
        var respostas = todos.Where(c => c.PaiId != null).ToLookup(c => c.PaiId!.Value);
        var nomes = await PeNomes.CarregarAsync(_context, todos.Select(c => c.ResolvidoPor));
        return todos.Where(c => c.PaiId == null).Select(c => Resposta(c, respostas[c.Id], nomes)).ToList();
    }

    public async Task<PeComentarioResponse> CriarAsync(long pdticId, PeComentarioCriarDTO dto, PeUserContext ctx)
    {
        var pdtic = await PePdticService.LerAsync(_context, _permissoes, pdticId, ctx);
        var texto = dto.Texto?.Trim();
        if (string.IsNullOrEmpty(texto)) throw Invalido("Escreva o comentário.");
        if (texto.Length > MaximoTexto) throw Invalido($"O comentário tem no máximo {MaximoTexto} caracteres (o texto tem {texto.Length}).");

        long passoId;
        long? paiId = null;
        if (dto.PaiId != null)
        {
            var pai = await _context.PeComentarios.AsNoTracking().FirstOrDefaultAsync(c => c.Id == dto.PaiId && c.PdticId == pdticId)
                ?? throw new ApiException(ErrorCode.PeComentarioNaoEncontrado, "O comentário respondido não foi encontrado. Atualize a tela.");
            if (pai.PaiId != null) throw Invalido("Responda ao comentário principal, não a uma resposta.");
            if (dto.PassoId != null && dto.PassoId != pai.PassoId) throw Invalido("A resposta precisa ser do mesmo passo do comentário.");
            // Responde a equipe do órgão (e o admin geral, que tem tudo)
            if (!_permissoes.PodeEditarPdtic(ctx, pdtic.OrgaoId))
                throw new ApiException(ErrorCode.PeSemPermissao, "Quem responde aos comentários é a equipe do órgão.");
            if (pai.ResolvidoEm != null)
                throw new ApiException(ErrorCode.PeComentarioResolvido,
                    "Este comentário já foi resolvido. Para continuar a conversa, faça um comentário novo.");
            passoId = pai.PassoId;
            paiId = pai.Id;
        }
        else
        {
            if (!_permissoes.PodeComentarPdtic(ctx))
                throw new ApiException(ErrorCode.PeSemPermissao,
                    "Só a SGDI e o administrador do módulo comentam os passos. A equipe do órgão responde aos comentários.");
            if (dto.PassoId == null) throw Invalido("Diga em que passo é o comentário.");
            var trilha = await PeTrilhaOrgao.CarregarAsync(_context, pdtic.OrgaoId, soAtivo: false);
            if (trilha.Passo(dto.PassoId.Value) == null)
                throw new ApiException(ErrorCode.PePassoIndisponivel, "Este passo não está na trilha do órgão. Atualize a tela.");
            passoId = dto.PassoId.Value;
        }

        var nome = await _context.Users.AsNoTracking().Where(u => u.Id == ctx.UserId).Select(u => u.Nome).FirstOrDefaultAsync();
        var comentario = new PeComentario
        {
            PdticId = pdticId,
            PassoId = passoId,
            PaiId = paiId,
            Texto = texto,
            AutorEmail = ctx.Email,
            AutorNome = string.IsNullOrWhiteSpace(nome) ? ctx.Email : nome.Trim(),
            CriadoEm = DateTime.UtcNow
        };
        _context.PeComentarios.Add(comentario);
        await _context.SaveChangesAsync();
        return await ConversaAsync(paiId ?? comentario.Id);
    }

    public async Task<PeComentarioResponse> ResolverAsync(long id, PeUserContext ctx)
    {
        var comentario = await _context.PeComentarios.FirstOrDefaultAsync(c => c.Id == id)
            ?? throw new ApiException(ErrorCode.PeComentarioNaoEncontrado, "Comentário não encontrado. Atualize a tela.");
        var pdtic = await PePdticService.LerAsync(_context, _permissoes, comentario.PdticId, ctx);
        if (comentario.PaiId != null) throw Invalido("Só o comentário principal é resolvido: a resposta acompanha o dele.");

        var autor = string.Equals(comentario.AutorEmail.Trim(), ctx.Email.Trim(), StringComparison.OrdinalIgnoreCase);
        if (!autor && !_permissoes.PodeEditarPdtic(ctx, pdtic.OrgaoId))
            throw new ApiException(ErrorCode.PeSemPermissao, "Quem resolve o comentário é a equipe do órgão ou quem comentou.");

        // Já resolvido: fica como estava (dois cliques não mudam quem resolveu)
        if (comentario.ResolvidoEm == null)
        {
            comentario.ResolvidoEm = DateTime.UtcNow;
            comentario.ResolvidoPor = ctx.Email;
            await _context.SaveChangesAsync();
        }
        return await ConversaAsync(comentario.Id);
    }

    /// <summary>O comentário principal com as respostas, em ordem.</summary>
    private async Task<PeComentarioResponse> ConversaAsync(long id)
    {
        var linhas = await _context.PeComentarios.AsNoTracking()
            .Where(c => c.Id == id || c.PaiId == id)
            .OrderBy(c => c.CriadoEm).ThenBy(c => c.Id)
            .ToListAsync();
        var nomes = await PeNomes.CarregarAsync(_context, linhas.Select(c => c.ResolvidoPor));
        return Resposta(linhas.Single(c => c.Id == id), linhas.Where(c => c.PaiId == id), nomes);
    }

    private static PeComentarioResponse Resposta(PeComentario c, IEnumerable<PeComentario> respostas, PeNomes nomes) => new()
    {
        Id = c.Id,
        PassoId = c.PassoId,
        Texto = c.Texto,
        AutorNome = c.AutorNome,
        AutorEmail = c.AutorEmail,
        CriadoEm = c.CriadoEm,
        ResolvidoEm = c.ResolvidoEm,
        ResolvidoPor = c.ResolvidoPor,
        ResolvidoPorNome = nomes.De(c.ResolvidoPor),
        Respostas = respostas.Select(r => new PeComentarioRespostaResponse
        {
            Id = r.Id,
            Texto = r.Texto,
            AutorNome = r.AutorNome,
            AutorEmail = r.AutorEmail,
            CriadoEm = r.CriadoEm
        }).ToList()
    };

    private static ApiException Invalido(string mensagem) => new(ErrorCode.PeComentarioInvalido, mensagem);
}
