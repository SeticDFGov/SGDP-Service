using System.Linq.Expressions;
using api.Common;
using api.Contratacoes;
using Microsoft.EntityFrameworkCore;
using Models.Contratacoes;
using Repositorio.Interface;
using service.Interface;

namespace service.Contratacoes;

/// <summary>
/// Manifestações da SGDI ao TCDF: validação dos blocos condicionais do despacho
/// (incisos I e II), estágio derivado e geração do despacho em PDF.
/// </summary>
public class CtrManifestacaoService : ICtrManifestacaoService
{
    private const int PageSizeMaximo = 100;

    /// <summary>Teto do prazo de regularização do inciso II (art. 40 da IN).</summary>
    public const int PrazoRegularizacaoMaximo = 365;

    private readonly ICtrProcessoRepositorio _repositorio;

    public CtrManifestacaoService(ICtrProcessoRepositorio repositorio)
    {
        _repositorio = repositorio;
    }

    // ── Estágio derivado ──────────────────────────────────────────────────────

    /// <summary>
    /// Estágio da manifestação — FONTE ÚNICA, nunca gravado: inciso II vira
    /// "Notificação para regularizar"; no inciso I sai do resultado da análise e,
    /// nos riscos significativos, do próprio desfecho.
    /// </summary>
    public static string CalcularEstagio(CtrManifestacaoTcdf m)
    {
        if (m.SituacaoPortfolio == CtrDominios.SituacaoPortfolio.NaoComunicadaPreviamente)
            return CtrDominios.Estagio.NotificacaoRegularizar;

        return m.ResultadoAnalise switch
        {
            CtrDominios.ResultadoAnalise.Alinhada => CtrDominios.Estagio.Alinhada,
            CtrDominios.ResultadoAnalise.InformacoesComplementares => CtrDominios.Estagio.InformacoesSolicitadas,
            CtrDominios.ResultadoAnalise.RiscosSignificativos => m.DesfechoRisco ?? string.Empty,
            _ => string.Empty
        };
    }

    /// <summary>
    /// Traduz o estágio derivado em predicado sobre as colunas (é função delas),
    /// para o filtro rodar no banco. Null quando o valor não é do domínio.
    /// </summary>
    public static Expression<Func<CtrManifestacaoTcdf, bool>>? PredicadoEstagio(string? estagio) => estagio switch
    {
        CtrDominios.Estagio.NotificacaoRegularizar => m =>
            m.SituacaoPortfolio == CtrDominios.SituacaoPortfolio.NaoComunicadaPreviamente,
        CtrDominios.Estagio.Alinhada => m =>
            m.SituacaoPortfolio == CtrDominios.SituacaoPortfolio.ComunicadaPreviamente
            && m.ResultadoAnalise == CtrDominios.ResultadoAnalise.Alinhada,
        CtrDominios.Estagio.InformacoesSolicitadas => m =>
            m.SituacaoPortfolio == CtrDominios.SituacaoPortfolio.ComunicadaPreviamente
            && m.ResultadoAnalise == CtrDominios.ResultadoAnalise.InformacoesComplementares,
        CtrDominios.Estagio.AguardandoResposta => m =>
            m.ResultadoAnalise == CtrDominios.ResultadoAnalise.RiscosSignificativos
            && m.DesfechoRisco == CtrDominios.DesfechoRisco.AguardandoResposta,
        CtrDominios.Estagio.RiscoResolvido => m =>
            m.ResultadoAnalise == CtrDominios.ResultadoAnalise.RiscosSignificativos
            && m.DesfechoRisco == CtrDominios.DesfechoRisco.RiscoResolvido,
        CtrDominios.Estagio.NaoPodeProsseguir => m =>
            m.ResultadoAnalise == CtrDominios.ResultadoAnalise.RiscosSignificativos
            && m.DesfechoRisco == CtrDominios.DesfechoRisco.NaoPodeProsseguir,
        _ => null
    };

    // ── Validação dos blocos condicionais do despacho ─────────────────────────

    /// <summary>
    /// Normaliza e valida a manifestação. Campo de bloco INATIVO preenchido é
    /// RECUSADO (nomeando o campo) — não anulado em silêncio.
    /// </summary>
    public static void ValidarManifestacao(CtrManifestacaoTcdf m)
    {
        m.OficioTcdf = (m.OficioTcdf ?? string.Empty).Trim();
        m.Observacao = string.IsNullOrWhiteSpace(m.Observacao) ? null : m.Observacao.Trim();
        m.SituacaoPortfolio = (m.SituacaoPortfolio ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(m.OficioTcdf))
            throw new ApiException(ErrorCode.CtrManifestacaoInvalida,
                "Informe o ofício/comunicação do TCDF.");

        if (m.DataOficio == default)
            throw new ApiException(ErrorCode.CtrManifestacaoInvalida, "Informe a data do ofício do TCDF.");

        if (m.DataOficio > CtrProcessoService.HojeBrasilia())
            throw new ApiException(ErrorCode.CtrManifestacaoInvalida,
                $"A data do ofício ({m.DataOficio:dd/MM/yyyy}) não pode ser futura.");

        if (!CtrDominios.SituacaoPortfolio.Todos.Contains(m.SituacaoPortfolio))
            throw new ApiException(ErrorCode.CtrDominioInvalido,
                $"Situação no portfólio inválida: {m.SituacaoPortfolio}");

        if (m.SituacaoPortfolio == CtrDominios.SituacaoPortfolio.ComunicadaPreviamente)
            ValidarIncisoI(m);
        else
            ValidarIncisoII(m);
    }

    private static void ValidarIncisoI(CtrManifestacaoTcdf m)
    {
        if (m.ComunicadaDesde == null)
            throw new ApiException(ErrorCode.CtrManifestacaoInvalida,
                "Contratação comunicada previamente exige a data desde quando está no monitoramento contínuo.");

        if (m.ComunicadaDesde.Value > CtrProcessoService.HojeBrasilia())
            throw new ApiException(ErrorCode.CtrManifestacaoInvalida,
                $"A data em que a contratação passou ao monitoramento contínuo "
                + $"({m.ComunicadaDesde.Value:dd/MM/yyyy}) não pode ser futura.");

        // A contratação entra no monitoramento ANTES de o TCDF comunicar (é o que o
        // inciso I afirma); data posterior ao ofício contradiz o próprio despacho
        if (m.ComunicadaDesde.Value > m.DataOficio)
            throw new ApiException(ErrorCode.CtrManifestacaoInvalida,
                $"A data do monitoramento contínuo ({m.ComunicadaDesde.Value:dd/MM/yyyy}) não pode ser "
                + $"posterior à data do ofício do TCDF ({m.DataOficio:dd/MM/yyyy}).");

        if (string.IsNullOrWhiteSpace(m.Criticidade))
            throw new ApiException(ErrorCode.CtrManifestacaoInvalida,
                "Contratação comunicada previamente exige a criticidade (art. 11 da IN).");

        if (!CtrDominios.Criticidade.Todos.Contains(m.Criticidade))
            throw new ApiException(ErrorCode.CtrDominioInvalido, $"Criticidade inválida: {m.Criticidade}");

        if (string.IsNullOrWhiteSpace(m.ResultadoAnalise))
            throw new ApiException(ErrorCode.CtrManifestacaoInvalida,
                "Contratação comunicada previamente exige o resultado da análise.");

        if (!CtrDominios.ResultadoAnalise.Todos.Contains(m.ResultadoAnalise))
            throw new ApiException(ErrorCode.CtrDominioInvalido,
                $"Resultado da análise inválido: {m.ResultadoAnalise}");

        if (m.PrazoRegularizacaoDias != null)
            throw new ApiException(ErrorCode.CtrManifestacaoInvalida,
                "\"Prazo de regularização\" só vale para contratação NÃO comunicada previamente (inciso II).");

        if (m.ResultadoAnalise == CtrDominios.ResultadoAnalise.RiscosSignificativos)
        {
            if (string.IsNullOrWhiteSpace(m.DesfechoRisco))
                throw new ApiException(ErrorCode.CtrManifestacaoInvalida,
                    "Riscos significativos exigem o desfecho (aguardando resposta, risco resolvido ou não pode prosseguir).");

            if (!CtrDominios.DesfechoRisco.Todos.Contains(m.DesfechoRisco))
                throw new ApiException(ErrorCode.CtrDominioInvalido,
                    $"Desfecho do risco inválido: {m.DesfechoRisco}");
        }
        else
        {
            if (m.DesfechoRisco != null)
                throw new ApiException(ErrorCode.CtrManifestacaoInvalida,
                    "\"Desfecho do risco\" só vale com o resultado \"Riscos significativos\".");

            if (m.RecomendouSuspensao)
                throw new ApiException(ErrorCode.CtrManifestacaoInvalida,
                    "\"Recomendou suspensão temporária\" só vale com o resultado \"Riscos significativos\".");

            if (m.ComunicouControleInterno)
                throw new ApiException(ErrorCode.CtrManifestacaoInvalida,
                    "\"Comunicou ao controle interno\" só vale com o resultado \"Riscos significativos\".");
        }
    }

    private static void ValidarIncisoII(CtrManifestacaoTcdf m)
    {
        if (m.PrazoRegularizacaoDias == null)
            throw new ApiException(ErrorCode.CtrManifestacaoInvalida,
                "Contratação não comunicada previamente exige o prazo de regularização, em dias (art. 40 da IN).");

        if (m.PrazoRegularizacaoDias < 1 || m.PrazoRegularizacaoDias > PrazoRegularizacaoMaximo)
            throw new ApiException(ErrorCode.CtrManifestacaoInvalida,
                $"O prazo de regularização deve estar entre 1 e {PrazoRegularizacaoMaximo} dias.");

        if (m.ComunicadaDesde != null)
            throw new ApiException(ErrorCode.CtrManifestacaoInvalida,
                "\"Comunicada desde\" só vale para contratação comunicada previamente (inciso I).");

        if (m.Criticidade != null)
            throw new ApiException(ErrorCode.CtrManifestacaoInvalida,
                "\"Criticidade\" só vale para contratação comunicada previamente (inciso I).");

        if (m.ResultadoAnalise != null)
            throw new ApiException(ErrorCode.CtrManifestacaoInvalida,
                "\"Resultado da análise\" só vale para contratação comunicada previamente (inciso I).");

        if (m.DesfechoRisco != null)
            throw new ApiException(ErrorCode.CtrManifestacaoInvalida,
                "\"Desfecho do risco\" só vale para contratação comunicada previamente (inciso I).");

        if (m.RecomendouSuspensao)
            throw new ApiException(ErrorCode.CtrManifestacaoInvalida,
                "\"Recomendou suspensão temporária\" só vale para contratação comunicada previamente (inciso I).");

        if (m.ComunicouControleInterno)
            throw new ApiException(ErrorCode.CtrManifestacaoInvalida,
                "\"Comunicou ao controle interno\" só vale para contratação comunicada previamente (inciso I).");
    }

    // ── Consultas ─────────────────────────────────────────────────────────────

    public async Task<PagedResponse<CtrManifestacaoResponse>> ListarAsync(CtrManifestacaoFiltro filtro)
    {
        // Só manifestações de processos ATIVOS (o soft delete some com elas também)
        var query = _repositorio.QueryManifestacoesDeProcessosAtivos();

        if (!string.IsNullOrWhiteSpace(filtro.Filtro))
        {
            var termo = filtro.Filtro.Trim().ToLower();
            query = query.Where(m =>
                m.Processo!.NumeroProcesso.ToLower().Contains(termo) ||
                m.Processo!.OrgaoSigla.ToLower().Contains(termo) ||
                m.OficioTcdf.ToLower().Contains(termo));
        }

        if (!string.IsNullOrWhiteSpace(filtro.Estagio))
        {
            // Estágio fora do domínio devolve VAZIO (nunca "todos" em silêncio)
            var predicado = PredicadoEstagio(filtro.Estagio);
            query = predicado != null ? query.Where(predicado) : query.Where(m => false);
        }

        var pageSize = Math.Clamp(filtro.PageSize, 1, PageSizeMaximo);
        var page = Math.Clamp(filtro.Page, 1, int.MaxValue / pageSize);

        var totalItems = await query.CountAsync();
        var manifestacoes = await query
            .OrderByDescending(m => m.DataOficio)
            .ThenByDescending(m => m.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new PagedResponse<CtrManifestacaoResponse>(
            manifestacoes.Select(Map).ToList(), totalItems, page, pageSize);
    }

    public async Task<List<CtrManifestacaoResponse>> ListarDoProcessoAsync(long processoId)
    {
        var manifestacoes = await _repositorio.ListarManifestacoesDoProcessoAsync(processoId);
        return manifestacoes.Select(Map).ToList();
    }

    public async Task<CtrManifestacaoTcdf?> GetEntidadeAsync(long id)
    {
        var manifestacao = await _repositorio.GetManifestacaoByIdAsync(id);
        // Manifestação de processo excluído não aparece em lista nenhuma nem no GET
        return manifestacao?.Processo is { Ativo: true } ? manifestacao : null;
    }

    public async Task<CtrManifestacaoResponse> GetAsync(long id)
    {
        var manifestacao = await GetEntidadeAsync(id)
            ?? throw new ApiException(ErrorCode.CtrManifestacaoNaoEncontrada);

        return Map(manifestacao);
    }

    // ── Escrita ───────────────────────────────────────────────────────────────

    public async Task<CtrManifestacaoResponse> CriarAsync(long processoId, CtrManifestacaoCreateDTO dto,
        CtrUserContext ctx)
    {
        var processo = await _repositorio.GetByIdAsync(processoId);
        if (processo is not { Ativo: true })
            throw new ApiException(ErrorCode.CtrProcessoNaoEncontrado);

        var manifestacao = new CtrManifestacaoTcdf
        {
            ProcessoId = processo.Id,
            Processo = processo,
            CriadoEm = DateTime.UtcNow,
            CriadoPor = ctx.Email
        };
        AplicarDto(manifestacao, dto);
        ValidarManifestacao(manifestacao);

        _repositorio.AddManifestacao(manifestacao);
        await _repositorio.SaveChangesAsync();

        return Map(manifestacao);
    }

    public async Task<CtrManifestacaoResponse> AtualizarAsync(long id, CtrManifestacaoUpdateDTO dto,
        CtrUserContext ctx)
    {
        var manifestacao = await GetEntidadeAsync(id)
            ?? throw new ApiException(ErrorCode.CtrManifestacaoNaoEncontrada);

        // Valida num candidato solto e só depois copia: PUT recusado não pode deixar
        // a entidade rastreada com o estado inválido (mesma disciplina do processo)
        var candidato = Clonar(manifestacao);
        AplicarDto(candidato, dto);
        ValidarManifestacao(candidato);

        AplicarDto(manifestacao, DtoDe(candidato));
        manifestacao.AlteradoEm = DateTime.UtcNow;
        manifestacao.AlteradoPor = ctx.Email;

        await _repositorio.SaveChangesAsync();
        return Map(manifestacao);
    }

    public async Task<byte[]> GerarDespachoPdfAsync(long id, string local, string nome, string cargo)
    {
        var manifestacao = await GetEntidadeAsync(id)
            ?? throw new ApiException(ErrorCode.CtrManifestacaoNaoEncontrada);

        var processo = CtrProcessoService.MapProcesso(manifestacao.Processo!, CtrProcessoService.HojeBrasilia());

        return CtrDespachoPdf.Gerar(processo, Map(manifestacao), local.Trim(), nome.Trim(), cargo.Trim(),
            CtrProcessoService.HojeBrasilia());
    }

    /// <summary>DTO a partir da entidade (valores já normalizados pela validação).</summary>
    private static CtrManifestacaoCreateDTO DtoDe(CtrManifestacaoTcdf m) => new()
    {
        OficioTcdf = m.OficioTcdf,
        DataOficio = m.DataOficio,
        SituacaoPortfolio = m.SituacaoPortfolio,
        ComunicadaDesde = m.ComunicadaDesde,
        Criticidade = m.Criticidade,
        ResultadoAnalise = m.ResultadoAnalise,
        RecomendouSuspensao = m.RecomendouSuspensao,
        ComunicouControleInterno = m.ComunicouControleInterno,
        DesfechoRisco = m.DesfechoRisco,
        PrazoRegularizacaoDias = m.PrazoRegularizacaoDias,
        Observacao = m.Observacao
    };

    /// <summary>Cópia solta da manifestação, para validar sem sujar o que o contexto rastreia.</summary>
    private static CtrManifestacaoTcdf Clonar(CtrManifestacaoTcdf m)
    {
        var copia = new CtrManifestacaoTcdf
        {
            Id = m.Id,
            ProcessoId = m.ProcessoId,
            CriadoEm = m.CriadoEm,
            CriadoPor = m.CriadoPor,
            AlteradoEm = m.AlteradoEm,
            AlteradoPor = m.AlteradoPor
        };
        AplicarDto(copia, DtoDe(m));
        return copia;
    }

    private static void AplicarDto(CtrManifestacaoTcdf m, CtrManifestacaoCreateDTO dto)
    {
        m.OficioTcdf = dto.OficioTcdf;
        m.DataOficio = dto.DataOficio;
        m.SituacaoPortfolio = dto.SituacaoPortfolio;
        m.ComunicadaDesde = dto.ComunicadaDesde;
        m.Criticidade = string.IsNullOrWhiteSpace(dto.Criticidade) ? null : dto.Criticidade.Trim();
        m.ResultadoAnalise = string.IsNullOrWhiteSpace(dto.ResultadoAnalise) ? null : dto.ResultadoAnalise.Trim();
        m.RecomendouSuspensao = dto.RecomendouSuspensao;
        m.ComunicouControleInterno = dto.ComunicouControleInterno;
        m.DesfechoRisco = string.IsNullOrWhiteSpace(dto.DesfechoRisco) ? null : dto.DesfechoRisco.Trim();
        m.PrazoRegularizacaoDias = dto.PrazoRegularizacaoDias;
        m.Observacao = dto.Observacao;
    }

    public static CtrManifestacaoResponse Map(CtrManifestacaoTcdf m) => new()
    {
        Id = m.Id,
        ProcessoId = m.ProcessoId,
        NumeroProcesso = m.Processo?.NumeroProcesso ?? string.Empty,
        OrgaoSigla = m.Processo?.OrgaoSigla ?? string.Empty,
        OrgaoNome = m.Processo?.OrgaoNome ?? string.Empty,
        Objeto = m.Processo?.Objeto ?? string.Empty,
        OficioTcdf = m.OficioTcdf,
        DataOficio = m.DataOficio,
        SituacaoPortfolio = m.SituacaoPortfolio,
        ComunicadaDesde = m.ComunicadaDesde,
        Criticidade = m.Criticidade,
        ResultadoAnalise = m.ResultadoAnalise,
        RecomendouSuspensao = m.RecomendouSuspensao,
        ComunicouControleInterno = m.ComunicouControleInterno,
        DesfechoRisco = m.DesfechoRisco,
        PrazoRegularizacaoDias = m.PrazoRegularizacaoDias,
        Observacao = m.Observacao,
        Estagio = CalcularEstagio(m),
        CriadoEm = m.CriadoEm,
        CriadoPor = m.CriadoPor,
        AlteradoEm = m.AlteradoEm,
        AlteradoPor = m.AlteradoPor
    };
}
