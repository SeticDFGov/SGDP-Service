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
    /// Estágio da manifestação — FONTE ÚNICA, nunca gravado. O status no TCDF tem
    /// PRECEDÊNCIA (suspensão e revogação são fato do Tribunal e valem em qualquer
    /// inciso); sem ele, inciso II vira "Notificação para regularizar" e o inciso I
    /// sai do resultado da análise e, nos riscos significativos, do próprio desfecho.
    /// </summary>
    public static string CalcularEstagio(CtrManifestacaoTcdf m)
    {
        // Mesma comparação do PredicadoEstagio (== null): o valor já é normalizado
        // para nulo no AplicarDto, então cálculo e filtro não podem divergir
        if (m.StatusTcdf != null) return m.StatusTcdf;

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
    /// Os seis estágios de análise exigem status_tcdf NULO — quem tem status foi
    /// para um dos dois estágios novos (mesma precedência do CalcularEstagio).
    /// </summary>
    public static Expression<Func<CtrManifestacaoTcdf, bool>>? PredicadoEstagio(string? estagio) => estagio switch
    {
        CtrDominios.Estagio.NotificacaoRegularizar => m =>
            m.StatusTcdf == null
            && m.SituacaoPortfolio == CtrDominios.SituacaoPortfolio.NaoComunicadaPreviamente,
        CtrDominios.Estagio.Alinhada => m =>
            m.StatusTcdf == null
            && m.SituacaoPortfolio == CtrDominios.SituacaoPortfolio.ComunicadaPreviamente
            && m.ResultadoAnalise == CtrDominios.ResultadoAnalise.Alinhada,
        CtrDominios.Estagio.InformacoesSolicitadas => m =>
            m.StatusTcdf == null
            && m.SituacaoPortfolio == CtrDominios.SituacaoPortfolio.ComunicadaPreviamente
            && m.ResultadoAnalise == CtrDominios.ResultadoAnalise.InformacoesComplementares,
        CtrDominios.Estagio.AguardandoResposta => m =>
            m.StatusTcdf == null
            && m.ResultadoAnalise == CtrDominios.ResultadoAnalise.RiscosSignificativos
            && m.DesfechoRisco == CtrDominios.DesfechoRisco.AguardandoResposta,
        CtrDominios.Estagio.RiscoResolvido => m =>
            m.StatusTcdf == null
            && m.ResultadoAnalise == CtrDominios.ResultadoAnalise.RiscosSignificativos
            && m.DesfechoRisco == CtrDominios.DesfechoRisco.RiscoResolvido,
        CtrDominios.Estagio.NaoPodeProsseguir => m =>
            m.StatusTcdf == null
            && m.ResultadoAnalise == CtrDominios.ResultadoAnalise.RiscosSignificativos
            && m.DesfechoRisco == CtrDominios.DesfechoRisco.NaoPodeProsseguir,
        CtrDominios.Estagio.SuspensoIrregularidades => m =>
            m.StatusTcdf == CtrDominios.StatusTcdf.SuspensoIrregularidades,
        CtrDominios.Estagio.EditalRevogado => m =>
            m.StatusTcdf == CtrDominios.StatusTcdf.EditalRevogado,
        _ => null
    };

    // ── Validação dos blocos condicionais do despacho ─────────────────────────

    /// <summary>
    /// Normaliza e valida a manifestação. Campo de bloco INATIVO preenchido é
    /// RECUSADO (nomeando o campo) — não anulado em silêncio.
    /// </summary>
    /// <param name="criticidadeProcesso">
    /// Criticidade gravada no PROCESSO (art. 11 da IN). O inciso I a exige: o dado
    /// nasce no cadastro do processo, a manifestação só o reporta.
    /// </param>
    /// <param name="exigirComunicacao">
    /// Os quatro dados da comunicação do TCDF são obrigatórios? Sim na criação e na
    /// edição de manifestação que já os tem; na edição de uma registrada antes deles,
    /// vêm os quatro ou nenhum (ver <see cref="TemComunicacao"/>).
    /// </param>
    public static void ValidarManifestacao(CtrManifestacaoTcdf m, string? criticidadeProcesso, bool exigirComunicacao)
    {
        m.OficioTcdf = (m.OficioTcdf ?? string.Empty).Trim();
        m.Observacao = string.IsNullOrWhiteSpace(m.Observacao) ? null : m.Observacao.Trim();
        m.SituacaoPortfolio = (m.SituacaoPortfolio ?? string.Empty).Trim();

        // Status no TCDF vale em QUALQUER inciso (é fato do Tribunal), então é
        // validado aqui e não dentro dos blocos condicionais
        if (m.StatusTcdf != null && !CtrDominios.StatusTcdf.Todos.Contains(m.StatusTcdf))
            throw new ApiException(ErrorCode.CtrDominioInvalido, $"Status no TCDF inválido: {m.StatusTcdf}");

        if (string.IsNullOrWhiteSpace(m.OficioTcdf))
            throw new ApiException(ErrorCode.CtrManifestacaoInvalida,
                "Informe o ofício/comunicação do TCDF.");

        if (m.DataOficio == default)
            throw new ApiException(ErrorCode.CtrManifestacaoInvalida, "Informe a data do ofício do TCDF.");

        if (m.DataOficio > CtrProcessoService.HojeBrasilia())
            throw new ApiException(ErrorCode.CtrManifestacaoInvalida,
                $"A data do ofício ({m.DataOficio:dd/MM/yyyy}) não pode ser futura.");

        // Tudo ou nada: preencher um dos quatro numa manifestação antiga pede os demais
        if (exigirComunicacao || TemComunicacao(m))
            ValidarComunicacao(m);

        if (!CtrDominios.SituacaoPortfolio.Todos.Contains(m.SituacaoPortfolio))
            throw new ApiException(ErrorCode.CtrDominioInvalido,
                $"Situação no portfólio inválida: {m.SituacaoPortfolio}");

        if (m.SituacaoPortfolio == CtrDominios.SituacaoPortfolio.ComunicadaPreviamente)
            ValidarIncisoI(m, criticidadeProcesso);
        else
            ValidarIncisoII(m);
    }

    /// <summary>
    /// A manifestação traz algum dos dados da comunicação do TCDF? As registradas antes
    /// deles (2026-09-23) têm os quatro nulos.
    /// </summary>
    public static bool TemComunicacao(CtrManifestacaoTcdf m) =>
        m.ProcessoComunicacaoTcdf != null || m.DataRecebimento != null
        || m.AtoTcdf != null || m.NumeroAtoTcdf != null;

    /// <summary>Os quatro dados da comunicação do TCDF, todos obrigatórios.</summary>
    private static void ValidarComunicacao(CtrManifestacaoTcdf m)
    {
        if (m.ProcessoComunicacaoTcdf == null)
            throw new ApiException(ErrorCode.CtrManifestacaoInvalida,
                "Informe o número do processo de comunicação do TCDF.");

        if (!CtrProcessoService.FormatoSei.IsMatch(m.ProcessoComunicacaoTcdf))
            throw new ApiException(ErrorCode.CtrManifestacaoInvalida,
                "Número do processo de comunicação do TCDF fora do formato SEI (00000-00000000/AAAA-DD): "
                + m.ProcessoComunicacaoTcdf);

        if (m.DataRecebimento == null)
            throw new ApiException(ErrorCode.CtrManifestacaoInvalida,
                "Informe a data de recebimento do processo na SGDI.");

        if (m.DataRecebimento.Value > CtrProcessoService.HojeBrasilia())
            throw new ApiException(ErrorCode.CtrManifestacaoInvalida,
                $"A data de recebimento ({m.DataRecebimento.Value:dd/MM/yyyy}) não pode ser futura.");

        // O processo chega à SGDI depois de o TCDF expedir o ofício
        if (m.DataRecebimento.Value < m.DataOficio)
            throw new ApiException(ErrorCode.CtrManifestacaoInvalida,
                $"A data de recebimento ({m.DataRecebimento.Value:dd/MM/yyyy}) não pode ser anterior "
                + $"à data do ofício do TCDF ({m.DataOficio:dd/MM/yyyy}).");

        if (m.AtoTcdf == null)
            throw new ApiException(ErrorCode.CtrManifestacaoInvalida,
                "Informe se o ato do TCDF é Despacho Singular ou Decisão.");

        if (!CtrDominios.AtoTcdf.Todos.Contains(m.AtoTcdf))
            throw new ApiException(ErrorCode.CtrDominioInvalido, $"Ato do TCDF inválido: {m.AtoTcdf}");

        if (m.NumeroAtoTcdf == null)
            throw new ApiException(ErrorCode.CtrManifestacaoInvalida,
                m.AtoTcdf == CtrDominios.AtoTcdf.Decisao
                    ? "Informe o número da Decisão do TCDF."
                    : "Informe o número do Despacho Singular do TCDF.");
    }

    private static void ValidarIncisoI(CtrManifestacaoTcdf m, string? criticidadeProcesso)
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

        // A criticidade é do PROCESSO (art. 11 da IN): o despacho do inciso I a
        // reporta, então ela precisa já estar definida no cadastro da contratação
        if (string.IsNullOrWhiteSpace(criticidadeProcesso))
            throw new ApiException(ErrorCode.CtrManifestacaoInvalida,
                "Defina a criticidade no cadastro do processo antes de registrar a manifestação do inciso I.");

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

        // A criticidade não é mais campo da manifestação (vive no processo), então
        // não há o que recusar aqui

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
            // O teste de nulo antes do ToLower vale para o InMemory, que avalia em C#
            query = query.Where(m =>
                m.Processo!.NumeroProcesso.ToLower().Contains(termo) ||
                (m.ProcessoComunicacaoTcdf != null && m.ProcessoComunicacaoTcdf.ToLower().Contains(termo)) ||
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
        ValidarManifestacao(manifestacao, processo.Criticidade, exigirComunicacao: true);

        _repositorio.AddManifestacao(manifestacao);
        await _repositorio.SaveChangesAsync();

        return Map(manifestacao);
    }

    public async Task<bool> ProcessoAtivoExisteAsync(long processoId) =>
        await _repositorio.GetByIdAsync(processoId) is { Ativo: true };

    public async Task<CtrManifestacaoResponse> CriarComunicacaoAsync(CtrComunicacaoTcdfCreateDTO dto,
        CtrUserContext ctx)
    {
        if ((dto.ProcessoId == null) == (dto.Contratacao == null))
            throw new ApiException(ErrorCode.CtrManifestacaoInvalida,
                "Informe o processo já cadastrado da contratação ou os dados de uma contratação nova, "
                + "um dos dois.");

        if (dto.ProcessoId is { } processoId)
            return await CriarAsync(processoId, dto, ctx);

        var agora = DateTime.UtcNow;
        var manifestacao = new CtrManifestacaoTcdf { CriadoEm = agora, CriadoPor = ctx.Email };
        AplicarDto(manifestacao, dto);

        // O inciso I reporta a criticidade e a data de entrada no monitoramento, que são
        // do processo da contratação já acompanhada: ela está no módulo e é a ele que a
        // comunicação se liga (a mensagem do inciso I sozinha mandaria definir a
        // criticidade num processo que nem existe)
        if (manifestacao.SituacaoPortfolio?.Trim() == CtrDominios.SituacaoPortfolio.ComunicadaPreviamente)
            throw new ApiException(ErrorCode.CtrManifestacaoInvalida,
                "Contratação comunicada previamente (inciso I) já é acompanhada pela SGDI: registre a "
                + "comunicação no processo dela, que tem a criticidade.");

        ValidarManifestacao(manifestacao, criticidadeProcesso: null, exigirComunicacao: true);

        // A contratação que o módulo ainda não tem vira processo de origem TCDF. O único
        // processo SEI que a SGDI tem dela é o da comunicação, e é o número dele que o
        // processo recebe (sem trâmite, riscos nem critérios de criticidade)
        var contratacao = dto.Contratacao!;
        var processo = new CtrProcesso
        {
            NumeroProcesso = manifestacao.ProcessoComunicacaoTcdf!,
            OrgaoNome = contratacao.OrgaoNome,
            OrgaoSigla = contratacao.OrgaoSigla,
            Objeto = contratacao.Objeto,
            CategoriaObjeto = contratacao.CategoriaObjeto,
            ValorEstimado = contratacao.ValorEstimado,
            Origem = CtrDominios.Origem.Tcdf,
            Ativo = true,
            CriadoEm = agora,
            CriadoPor = ctx.Email
        };

        // Ofício novo sobre processo que já está no módulo: é nele que a comunicação entra
        if (await _repositorio.NumeroDuplicadoAsync(processo.NumeroProcesso, null))
            throw new ApiException(ErrorCode.CtrProcessoDuplicado,
                $"Já existe processo cadastrado com o número {processo.NumeroProcesso}: registre a "
                + "comunicação nele em vez de cadastrar a contratação de novo.");

        CtrProcessoService.ValidarProcesso(processo, numeroDuplicado: false);

        manifestacao.Processo = processo;
        _repositorio.Add(processo);
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
        // Registrada antes dos dados da comunicação do TCDF, pode seguir sem eles
        ValidarManifestacao(candidato, manifestacao.Processo?.Criticidade,
            exigirComunicacao: TemComunicacao(manifestacao));

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

        // Cinto de segurança: sem a criticidade do processo o despacho do inciso I
        // sairia com o "[Alta/Média/Baixa]" do formulário em branco
        if (manifestacao.SituacaoPortfolio == CtrDominios.SituacaoPortfolio.ComunicadaPreviamente
            && string.IsNullOrWhiteSpace(processo.Criticidade))
            throw new ApiException(ErrorCode.CtrManifestacaoInvalida,
                "Defina a criticidade no cadastro do processo antes de gerar o despacho do inciso I.");

        return CtrDespachoPdf.Gerar(processo, Map(manifestacao), local.Trim(), nome.Trim(), cargo.Trim(),
            CtrProcessoService.HojeBrasilia());
    }

    /// <summary>DTO a partir da entidade (valores já normalizados pela validação).</summary>
    private static CtrManifestacaoCreateDTO DtoDe(CtrManifestacaoTcdf m) => new()
    {
        OficioTcdf = m.OficioTcdf,
        DataOficio = m.DataOficio,
        ProcessoComunicacaoTcdf = m.ProcessoComunicacaoTcdf,
        DataRecebimento = m.DataRecebimento,
        AtoTcdf = m.AtoTcdf,
        NumeroAtoTcdf = m.NumeroAtoTcdf,
        SituacaoPortfolio = m.SituacaoPortfolio,
        EsclarecimentosAdicionais = m.EsclarecimentosAdicionais,
        StatusTcdf = m.StatusTcdf,
        ComunicadaDesde = m.ComunicadaDesde,
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
        m.ProcessoComunicacaoTcdf = string.IsNullOrWhiteSpace(dto.ProcessoComunicacaoTcdf)
            ? null
            : dto.ProcessoComunicacaoTcdf.Trim();
        m.DataRecebimento = dto.DataRecebimento;
        m.AtoTcdf = string.IsNullOrWhiteSpace(dto.AtoTcdf) ? null : dto.AtoTcdf.Trim();
        m.NumeroAtoTcdf = string.IsNullOrWhiteSpace(dto.NumeroAtoTcdf) ? null : dto.NumeroAtoTcdf.Trim();
        m.SituacaoPortfolio = dto.SituacaoPortfolio;
        m.EsclarecimentosAdicionais = string.IsNullOrWhiteSpace(dto.EsclarecimentosAdicionais)
            ? null
            : dto.EsclarecimentosAdicionais.Trim();
        m.StatusTcdf = string.IsNullOrWhiteSpace(dto.StatusTcdf) ? null : dto.StatusTcdf.Trim();
        m.ComunicadaDesde = dto.ComunicadaDesde;
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
        CategoriaObjeto = m.Processo?.CategoriaObjeto ?? string.Empty,
        ValorEstimado = m.Processo?.ValorEstimado,
        Origem = m.Processo?.Origem ?? string.Empty,
        OficioTcdf = m.OficioTcdf,
        DataOficio = m.DataOficio,
        ProcessoComunicacaoTcdf = m.ProcessoComunicacaoTcdf,
        DataRecebimento = m.DataRecebimento,
        AtoTcdf = m.AtoTcdf,
        NumeroAtoTcdf = m.NumeroAtoTcdf,
        SituacaoPortfolio = m.SituacaoPortfolio,
        EsclarecimentosAdicionais = m.EsclarecimentosAdicionais,
        StatusTcdf = m.StatusTcdf,
        ComunicadaDesde = m.ComunicadaDesde,
        // Espelho somente leitura: a criticidade é do processo
        Criticidade = m.Processo?.Criticidade,
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
