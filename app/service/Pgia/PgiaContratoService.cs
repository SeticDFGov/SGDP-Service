using api.Pgia;
using demanda_service.Helpers;
using Models.Pgia;
using Repositorio.Interface;
using service.Interface;

namespace service.Pgia;

/// <summary>
/// Contratações de IA: contratos e seus requisitos obrigatórios (arts. 21, 25 a 27)
/// e triagem dos instrumentos anteriores ao decreto (art. 37).
/// </summary>
public class PgiaContratoService : IPgiaContratoService
{
    private readonly IPgiaContratoRepositorio _repositorio;

    public PgiaContratoService(IPgiaContratoRepositorio repositorio)
    {
        _repositorio = repositorio;
    }

    // ── Contratos ─────────────────────────────────────────────────────────────

    public async Task<PgiaContratoIa?> GetContratoEntidadeAsync(long id)
    {
        return await _repositorio.GetContratoByIdAsync(id);
    }

    public async Task<PgiaContratoResponse> GetContratoAsync(long id)
    {
        var contrato = await _repositorio.GetContratoByIdAsync(id)
            ?? throw new ApiException(ErrorCode.PgiaContratoNaoEncontrado);
        return MapContrato(contrato);
    }

    public async Task<List<PgiaContratoResponse>> ListarContratosPorOrgaoAsync(long orgaoId)
    {
        var lista = await _repositorio.ListarContratosPorOrgaoAsync(orgaoId);
        return lista.Select(MapContrato).ToList();
    }

    public async Task<PgiaContratoResponse> CriarContratoAsync(long orgaoId, PgiaContratoCreateDTO dto, PgiaUserContext ctx)
    {
        var sistema = await ValidarContratoAsync(dto, orgaoId);

        var contrato = new PgiaContratoIa
        {
            OrgaoId = orgaoId,
            CriadoEm = DateTime.UtcNow,
            CriadoPor = ctx.Email
        };
        AplicarDadosContrato(contrato, dto, sistema);

        _repositorio.AddContrato(contrato);
        await _repositorio.SaveChangesAsync();

        var salvo = await _repositorio.GetContratoByIdAsync(contrato.Id);
        return MapContrato(salvo ?? contrato);
    }

    public async Task<PgiaContratoResponse> AtualizarContratoAsync(long id, PgiaContratoUpdateDTO dto, PgiaUserContext ctx)
    {
        var contrato = await _repositorio.GetContratoByIdAsync(id)
            ?? throw new ApiException(ErrorCode.PgiaContratoNaoEncontrado);

        var sistema = await ValidarContratoAsync(dto, contrato.OrgaoId);

        AplicarDadosContrato(contrato, dto, sistema);
        contrato.AlteradoEm = DateTime.UtcNow;
        contrato.AlteradoPor = ctx.Email;

        await _repositorio.SaveChangesAsync();
        return MapContrato(contrato);
    }

    /// <summary>
    /// Valida o contrato e devolve o sistema vinculado (null quando não há).
    /// Os condicionais do art. 25 dependem da classificação do sistema: sem vínculo,
    /// não há risco a exigir PDTIC, homologação técnica, auditoria ou ANS.
    /// </summary>
    private async Task<PgiaSistemaIa?> ValidarContratoAsync(PgiaContratoCreateDTO dto, long orgaoId)
    {
        if (string.IsNullOrWhiteSpace(dto.NumeroContrato) || string.IsNullOrWhiteSpace(dto.ProcessoSei)
            || string.IsNullOrWhiteSpace(dto.Objeto) || string.IsNullOrWhiteSpace(dto.FornecedorNome))
            throw new ApiException(ErrorCode.PgiaContratoInvalido,
                "Número do contrato, processo SEI, objeto e fornecedor são obrigatórios.");

        if (!PgiaDominios.StatusContrato.Todos.Contains(dto.Status))
            throw new ApiException(ErrorCode.PgiaDominioInvalido, $"Situação do contrato inválida: {dto.Status}");

        PgiaSistemaIa? sistema = null;
        if (dto.SistemaIaId != null)
        {
            sistema = await _repositorio.GetSistemaByIdAsync(dto.SistemaIaId.Value)
                ?? throw new ApiException(ErrorCode.PgiaSistemaNaoEncontrado);

            if (sistema.OrgaoId != orgaoId)
                throw new ApiException(ErrorCode.PgiaSistemaNaoEncontrado,
                    "O sistema vinculado pertence a outro órgão.");
        }

        await ValidarDocumentoDoOrgaoAsync(dto.HomologacaoSgdiDocId, orgaoId);

        // Todo contrato de IA veda o treinamento com dados do GDF; sem a cláusula,
        // só com autorização excepcional aprovada pelo CGTIC (arts. 21 e 25, § 1º, e)
        if (!dto.ClausulaVedacaoTreinamento && dto.AutorizacaoTreinamentoId == null)
            throw new ApiException(ErrorCode.PgiaContratoInvalido,
                "Contrato sem cláusula de vedação de treinamento exige a autorização excepcional do art. 21.");

        // A autorização vinculada é validada em qualquer dos ramos: informada com a
        // cláusula presente, ela continua sendo o que dispensa a vedação amanhã.
        await ValidarAutorizacaoDeTreinamentoAsync(dto.AutorizacaoTreinamentoId, orgaoId);

        ValidarCondicionaisPorRisco(dto, sistema?.ClassificacaoRiscoAtual);
        return sistema;
    }

    /// <summary>
    /// A exceção do art. 21 só protege este contrato se for do mesmo órgão, estiver
    /// ativa e dentro da vigência — autorização alheia, revogada ou vencida não vale.
    /// </summary>
    private async Task ValidarAutorizacaoDeTreinamentoAsync(long? autorizacaoId, long orgaoId)
    {
        if (autorizacaoId == null) return;

        var autorizacao = await _repositorio.GetAutorizacaoByIdAsync(autorizacaoId.Value)
            ?? throw new ApiException(ErrorCode.PgiaAutorizacaoNaoEncontrada);

        if (autorizacao.Tipo != PgiaDominios.TipoAutorizacao.TreinamentoFornecedor)
            throw new ApiException(ErrorCode.PgiaContratoInvalido,
                "A autorização vinculada não é de uso de dados do GDF para treinamento (art. 21).");

        if (autorizacao.OrgaoId != orgaoId)
            throw new ApiException(ErrorCode.PgiaContratoInvalido,
                "A autorização de treinamento é de outro órgão e não ampara este contrato (art. 21).");

        if (!autorizacao.Ativo)
            throw new ApiException(ErrorCode.PgiaContratoInvalido,
                "A autorização de treinamento vinculada está revogada (art. 21).");

        var hoje = DateOnly.FromDateTime(DateTimeHelper.TodayBrasilia());
        if (autorizacao.VigenciaFim != null && autorizacao.VigenciaFim < hoje)
            throw new ApiException(ErrorCode.PgiaContratoInvalido,
                "A autorização de treinamento vinculada está com a vigência vencida (art. 21).");
    }

    /// <summary>Metadados de documento só valem dentro do próprio órgão.</summary>
    private async Task ValidarDocumentoDoOrgaoAsync(long? documentoId, long orgaoId)
    {
        if (documentoId == null) return;

        var documento = await _repositorio.GetDocumentoByIdAsync(documentoId.Value)
            ?? throw new ApiException(ErrorCode.PgiaDocumentoNaoEncontrado);

        if (documento.OrgaoId != orgaoId)
            throw new ApiException(ErrorCode.PgiaDocumentoNaoEncontrado,
                "O documento informado pertence a outro órgão.");
    }

    private static void ValidarCondicionaisPorRisco(PgiaContratoCreateDTO dto, string? risco)
    {
        var alto = risco == PgiaDominios.ResultadoRisco.Alto;
        var moderado = risco == PgiaDominios.ResultadoRisco.Moderado;

        // Alto Risco e Risco Moderado: previsão no PDTIC (art. 25, I, a e II, a)
        if ((alto || moderado) && dto.PrevistoPdtic == null)
            throw new ApiException(ErrorCode.PgiaContratoInvalido,
                "Informe se a contratação está prevista no PDTIC (art. 25, I, a e II, a).");

        // Risco Moderado: homologação técnica prévia da SGDI (art. 25, II, b e c)
        if (moderado && dto.HomologacaoSgdiDocId == null)
            throw new ApiException(ErrorCode.PgiaContratoInvalido,
                "Contratação de Risco Moderado exige o documento da homologação técnica prévia da SGDI (art. 25, II, b e c).");

        if (!alto) return;

        // Alto Risco: auditoria independente e ANS com penalidades (art. 25, § 2º)
        if (dto.ClausulaAuditoriaIndependente == null)
            throw new ApiException(ErrorCode.PgiaContratoInvalido,
                "Contratação de Alto Risco exige informar a cláusula de auditoria independente (art. 25, § 2º).");

        if (dto.SlaDesempenho == null || dto.SlaAcuracia == null
            || dto.SlaEquidade == null || dto.SlaDisponibilidade == null)
            throw new ApiException(ErrorCode.PgiaContratoInvalido,
                "Contratação de Alto Risco exige os níveis de serviço de desempenho, acurácia, equidade e disponibilidade (art. 25, § 2º).");

        if (string.IsNullOrWhiteSpace(dto.SlaPenalidades))
            throw new ApiException(ErrorCode.PgiaContratoInvalido,
                "Contratação de Alto Risco exige as penalidades da ANS (art. 25, § 2º).");
    }

    private static void AplicarDadosContrato(PgiaContratoIa contrato, PgiaContratoCreateDTO dto, PgiaSistemaIa? sistema)
    {
        contrato.SistemaIaId = dto.SistemaIaId;
        contrato.Sistema = sistema;
        contrato.NumeroContrato = dto.NumeroContrato.Trim();
        contrato.ProcessoSei = dto.ProcessoSei.Trim();
        contrato.Objeto = dto.Objeto.Trim();
        contrato.FornecedorNome = dto.FornecedorNome.Trim();
        contrato.Status = dto.Status;
        contrato.PrevistoPdtic = dto.PrevistoPdtic;
        contrato.HomologacaoSgdiDocId = dto.HomologacaoSgdiDocId;
        contrato.ClausulaVedacaoTreinamento = dto.ClausulaVedacaoTreinamento;
        contrato.AutorizacaoTreinamentoId = dto.AutorizacaoTreinamentoId;
        contrato.ReqExplicabilidade = dto.ReqExplicabilidade;
        contrato.ReqAuditabilidade = dto.ReqAuditabilidade;
        contrato.ReqPortabilidade = dto.ReqPortabilidade;
        contrato.ReqSemAprisionamento = dto.ReqSemAprisionamento;
        contrato.ReqAcessibilidade = dto.ReqAcessibilidade;
        contrato.ClausulaAuditoriaIndependente = dto.ClausulaAuditoriaIndependente;
        contrato.SlaDesempenho = dto.SlaDesempenho;
        contrato.SlaAcuracia = dto.SlaAcuracia;
        contrato.SlaEquidade = dto.SlaEquidade;
        contrato.SlaDisponibilidade = dto.SlaDisponibilidade;
        contrato.SlaPenalidades = string.IsNullOrWhiteSpace(dto.SlaPenalidades) ? null : dto.SlaPenalidades.Trim();
        contrato.ConformeGuiaContratacoes = dto.ConformeGuiaContratacoes;
    }

    // ── Instrumentos legados ──────────────────────────────────────────────────

    public async Task<PgiaInstrumentoLegado?> GetLegadoEntidadeAsync(long id)
    {
        return await _repositorio.GetLegadoByIdAsync(id);
    }

    public async Task<List<PgiaLegadoResponse>> ListarLegadosPorOrgaoAsync(long orgaoId)
    {
        var lista = await _repositorio.ListarLegadosPorOrgaoAsync(orgaoId);
        return lista.Select(MapLegado).ToList();
    }

    public async Task<PgiaLegadoResponse> CriarLegadoAsync(long orgaoId, PgiaLegadoCreateDTO dto, PgiaUserContext ctx)
    {
        var sistema = await ValidarLegadoAsync(dto, orgaoId);

        var legado = new PgiaInstrumentoLegado
        {
            OrgaoId = orgaoId,
            CriadoEm = DateTime.UtcNow,
            CriadoPor = ctx.Email
        };
        AplicarDadosLegado(legado, dto, sistema, ctx);

        _repositorio.AddLegado(legado);
        await _repositorio.SaveChangesAsync();

        var salvo = await _repositorio.GetLegadoByIdAsync(legado.Id);
        return MapLegado(salvo ?? legado);
    }

    public async Task<PgiaLegadoResponse> AtualizarLegadoAsync(long id, PgiaLegadoUpdateDTO dto, PgiaUserContext ctx)
    {
        var legado = await _repositorio.GetLegadoByIdAsync(id)
            ?? throw new ApiException(ErrorCode.PgiaLegadoNaoEncontrado);

        var sistema = await ValidarLegadoAsync(dto, legado.OrgaoId);

        AplicarDadosLegado(legado, dto, sistema, ctx);
        legado.AlteradoEm = DateTime.UtcNow;
        legado.AlteradoPor = ctx.Email;

        await _repositorio.SaveChangesAsync();

        var salvo = await _repositorio.GetLegadoByIdAsync(legado.Id);
        return MapLegado(salvo ?? legado);
    }

    private async Task<PgiaSistemaIa?> ValidarLegadoAsync(PgiaLegadoCreateDTO dto, long orgaoId)
    {
        if (!PgiaDominios.TipoInstrumentoLegado.Todos.Contains(dto.TipoInstrumento))
            throw new ApiException(ErrorCode.PgiaDominioInvalido,
                $"Tipo de instrumento inválido: {dto.TipoInstrumento}");

        if (!PgiaDominios.EnvolveIa.Todos.Contains(dto.EnvolveIa))
            throw new ApiException(ErrorCode.PgiaDominioInvalido,
                $"Resposta inválida para o envolvimento de IA: {dto.EnvolveIa}");

        if (string.IsNullOrWhiteSpace(dto.Descricao))
            throw new ApiException(ErrorCode.PgiaDominioInvalido, "Descreva o instrumento.");

        PgiaSistemaIa? sistema = null;
        if (dto.SistemaIaId != null)
        {
            sistema = await _repositorio.GetSistemaByIdAsync(dto.SistemaIaId.Value)
                ?? throw new ApiException(ErrorCode.PgiaSistemaNaoEncontrado);

            if (sistema.OrgaoId != orgaoId)
                throw new ApiException(ErrorCode.PgiaSistemaNaoEncontrado,
                    "O sistema vinculado pertence a outro órgão.");
        }

        await ValidarDocumentoDoOrgaoAsync(dto.ComprovacaoDocId, orgaoId);

        // Condicionais do art. 37: o instrumento revisado que envolve (ou pode envolver)
        // IA precisa dizer se recebeu o aditivo com a cláusula de vedação do art. 21.
        if (dto.Revisado && dto.EnvolveIa != "Não" && dto.AditivoClausulaTreinamento == null)
            throw new ApiException(ErrorCode.PgiaDominioInvalido,
                "Instrumento revisado que envolve IA exige informar o termo aditivo com a cláusula de vedação de treinamento (arts. 21 e 37).");

        if (dto.AditivoClausulaTreinamento == true && dto.DataAditivo == null)
            throw new ApiException(ErrorCode.PgiaDominioInvalido,
                "Informe a data do termo aditivo (arts. 21 e 37).");

        return sistema;
    }

    private static void AplicarDadosLegado(
        PgiaInstrumentoLegado legado, PgiaLegadoCreateDTO dto, PgiaSistemaIa? sistema, PgiaUserContext ctx)
    {
        legado.TipoInstrumento = dto.TipoInstrumento;
        legado.Descricao = dto.Descricao.Trim();
        legado.Numero = string.IsNullOrWhiteSpace(dto.Numero) ? null : dto.Numero.Trim();
        legado.EnvolveIa = dto.EnvolveIa;

        // Quem registra a triagem responde por ela; uma edição que não mexe na data
        // não transfere a autoria para quem passou por ali depois.
        if (dto.DataTriagem == null)
            legado.TriadoPor = null;
        else if (dto.DataTriagem != legado.DataTriagem || legado.TriadoPor == null)
            legado.TriadoPor = ctx.UserId;
        legado.DataTriagem = dto.DataTriagem;
        legado.Revisado = dto.Revisado;
        legado.DataRevisao = dto.DataRevisao;
        legado.AditivoClausulaTreinamento = dto.AditivoClausulaTreinamento;
        legado.DataAditivo = dto.DataAditivo;
        legado.ProcessoSei = string.IsNullOrWhiteSpace(dto.ProcessoSei) ? null : dto.ProcessoSei.Trim();
        legado.ComprovacaoDocId = dto.ComprovacaoDocId;
        legado.SistemaIaId = dto.SistemaIaId;
        legado.Sistema = sistema;
    }

    // ── Mapeamentos ───────────────────────────────────────────────────────────

    private static PgiaContratoResponse MapContrato(PgiaContratoIa c)
    {
        return new PgiaContratoResponse
        {
            Id = c.Id,
            OrgaoId = c.OrgaoId,
            OrgaoSigla = c.Orgao?.Sigla ?? string.Empty,
            SistemaIaId = c.SistemaIaId,
            SistemaDenominacao = c.Sistema?.Denominacao,
            SistemaClassificacao = c.Sistema?.ClassificacaoRiscoAtual,
            NumeroContrato = c.NumeroContrato,
            ProcessoSei = c.ProcessoSei,
            Objeto = c.Objeto,
            FornecedorNome = c.FornecedorNome,
            Status = c.Status,
            PrevistoPdtic = c.PrevistoPdtic,
            HomologacaoSgdiDocId = c.HomologacaoSgdiDocId,
            ClausulaVedacaoTreinamento = c.ClausulaVedacaoTreinamento,
            AutorizacaoTreinamentoId = c.AutorizacaoTreinamentoId,
            ReqExplicabilidade = c.ReqExplicabilidade,
            ReqAuditabilidade = c.ReqAuditabilidade,
            ReqPortabilidade = c.ReqPortabilidade,
            ReqSemAprisionamento = c.ReqSemAprisionamento,
            ReqAcessibilidade = c.ReqAcessibilidade,
            ClausulaAuditoriaIndependente = c.ClausulaAuditoriaIndependente,
            SlaDesempenho = c.SlaDesempenho,
            SlaAcuracia = c.SlaAcuracia,
            SlaEquidade = c.SlaEquidade,
            SlaDisponibilidade = c.SlaDisponibilidade,
            SlaPenalidades = c.SlaPenalidades,
            ConformeGuiaContratacoes = c.ConformeGuiaContratacoes,
            CriadoEm = c.CriadoEm
        };
    }

    private static PgiaLegadoResponse MapLegado(PgiaInstrumentoLegado l)
    {
        return new PgiaLegadoResponse
        {
            Id = l.Id,
            OrgaoId = l.OrgaoId,
            OrgaoSigla = l.Orgao?.Sigla ?? string.Empty,
            TipoInstrumento = l.TipoInstrumento,
            Descricao = l.Descricao,
            Numero = l.Numero,
            EnvolveIa = l.EnvolveIa,
            DataTriagem = l.DataTriagem,
            TriadoPorNome = l.TriadoPorUser?.Nome,
            Revisado = l.Revisado,
            DataRevisao = l.DataRevisao,
            AditivoClausulaTreinamento = l.AditivoClausulaTreinamento,
            DataAditivo = l.DataAditivo,
            ProcessoSei = l.ProcessoSei,
            ComprovacaoDocId = l.ComprovacaoDocId,
            SistemaIaId = l.SistemaIaId,
            SistemaDenominacao = l.Sistema?.Denominacao,
            CriadoEm = l.CriadoEm
        };
    }
}
