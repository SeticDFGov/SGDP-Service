using api.Pgia;
using app.Auth;
using demanda_service.Helpers;
using Models.Pgia;
using Repositorio.Interface;
using service.Interface;

namespace service.Pgia;

/// <summary>
/// Indicadores de desempenho (arts. 24, V, 31 e 32, III), relatório semestral do
/// órgão (art. 32), Relatório Anual de Governança (art. 33) e auditorias técnicas
/// (arts. 25, § 2º e 34).
/// </summary>
public class PgiaRelatorioService : IPgiaRelatorioService
{
    private readonly IPgiaRelatorioRepositorio _repositorio;

    public PgiaRelatorioService(IPgiaRelatorioRepositorio repositorio)
    {
        _repositorio = repositorio;
    }

    // ── Indicadores ───────────────────────────────────────────────────────────

    public async Task<PgiaIndicadorDesempenho?> GetIndicadorEntidadeAsync(long id)
    {
        return await _repositorio.GetIndicadorByIdAsync(id);
    }

    public async Task<List<PgiaIndicadorResponse>> ListarIndicadoresPorSistemaAsync(long sistemaId)
    {
        var lista = await _repositorio.ListarIndicadoresPorSistemaAsync(sistemaId);
        return lista.Select(MapIndicador).ToList();
    }

    public async Task<PgiaIndicadorResponse> CriarIndicadorAsync(
        long sistemaId, PgiaIndicadorCreateDTO dto, PgiaUserContext ctx)
    {
        var sistema = await _repositorio.GetSistemaByIdAsync(sistemaId)
            ?? throw new ApiException(ErrorCode.PgiaSistemaNaoEncontrado);

        ValidarIndicador(dto);

        var indicador = new PgiaIndicadorDesempenho
        {
            SistemaIaId = sistema.Id,
            Sistema = sistema,
            CriadoEm = DateTime.UtcNow,
            CriadoPor = ctx.Email
        };
        AplicarDadosIndicador(indicador, dto);

        _repositorio.AddIndicador(indicador);
        await _repositorio.SaveChangesAsync();
        return MapIndicador(indicador);
    }

    public async Task<PgiaIndicadorResponse> AtualizarIndicadorAsync(
        long id, PgiaIndicadorCreateDTO dto, PgiaUserContext ctx)
    {
        var indicador = await _repositorio.GetIndicadorByIdAsync(id)
            ?? throw new ApiException(ErrorCode.PgiaIndicadorNaoEncontrado);

        ValidarIndicador(dto);
        AplicarDadosIndicador(indicador, dto);
        indicador.AlteradoEm = DateTime.UtcNow;
        indicador.AlteradoPor = ctx.Email;

        await _repositorio.SaveChangesAsync();
        return MapIndicador(indicador);
    }

    private static void ValidarIndicador(PgiaIndicadorCreateDTO dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Nome))
            throw new ApiException(ErrorCode.PgiaDominioInvalido, "Informe o nome do indicador.");

        if (!PgiaDominios.CategoriaIndicador.Todos.Contains(dto.Categoria))
            throw new ApiException(ErrorCode.PgiaDominioInvalido, $"Categoria do indicador inválida: {dto.Categoria}");

        if (dto.PeriodoInicio == default || dto.PeriodoFim == default)
            throw new ApiException(ErrorCode.PgiaPeriodoInvalido, "Informe o período de referência do indicador.");

        if (dto.PeriodoFim < dto.PeriodoInicio)
            throw new ApiException(ErrorCode.PgiaPeriodoInvalido,
                "O fim do período de referência não pode ser anterior ao início.");
    }

    private static void AplicarDadosIndicador(PgiaIndicadorDesempenho indicador, PgiaIndicadorCreateDTO dto)
    {
        indicador.Nome = dto.Nome.Trim();
        indicador.Categoria = dto.Categoria;
        indicador.Valor = dto.Valor;
        indicador.Unidade = string.IsNullOrWhiteSpace(dto.Unidade) ? null : dto.Unidade.Trim();
        indicador.PeriodoInicio = dto.PeriodoInicio;
        indicador.PeriodoFim = dto.PeriodoFim;
        indicador.Meta = dto.Meta;
        indicador.PublicadoRegistroPublico = dto.PublicadoRegistroPublico;
    }

    // ── Relatório semestral ───────────────────────────────────────────────────

    public async Task<PgiaRelatorioSemestral?> GetRelatorioSemestralEntidadeAsync(long id)
    {
        return await _repositorio.GetRelatorioSemestralByIdAsync(id);
    }

    public async Task<PgiaRelatorioSemestralResponse> CriarRelatorioSemestralAsync(
        long orgaoId, PgiaRelatorioSemestralCreateDTO dto, PgiaUserContext ctx)
    {
        var orgao = await _repositorio.GetOrgaoByIdAsync(orgaoId)
            ?? throw new ApiException(ErrorCode.PgiaOrgaoNaoEncontrado);

        if (dto.Semestre is not (1 or 2))
            throw new ApiException(ErrorCode.PgiaDominioInvalido, "O semestre deve ser 1 ou 2.");

        // Não se abre relatório de exercício que ainda não começou (nem de antes do decreto)
        var anoLimite = DateTimeHelper.TodayBrasilia().Year + 1;
        if (dto.Ano < 2026 || dto.Ano > anoLimite)
            throw new ApiException(ErrorCode.PgiaDominioInvalido,
                $"Ano fora do intervalo aceito (2026 a {anoLimite}): {dto.Ano}");

        var existente = await _repositorio.GetRelatorioSemestralAsync(orgaoId, dto.Ano, dto.Semestre);
        if (existente != null)
            throw new ApiException(ErrorCode.PgiaRelatorioJaExiste,
                $"O órgão já tem relatório do {dto.Semestre}º semestre de {dto.Ano}.");

        var relatorio = new PgiaRelatorioSemestral
        {
            OrgaoId = orgao.Id,
            Orgao = orgao,
            Ano = dto.Ano,
            Semestre = dto.Semestre,
            Status = PgiaDominios.StatusRelatorioSemestral.Pendente,
            PrazoEnvio = CalcularPrazoEnvio(dto.Ano, dto.Semestre),
            CriadoEm = DateTime.UtcNow,
            CriadoPor = ctx.Email
        };

        _repositorio.AddRelatorioSemestral(relatorio);
        await _repositorio.SaveChangesAsync();
        return MapRelatorioSemestral(relatorio, null);
    }

    /// <summary>
    /// Adaptação registrada: o decreto remete o prazo a norma complementar. Adotamos
    /// o último dia do mês seguinte ao fim do semestre — 31/07 do próprio ano no
    /// primeiro semestre e 31/01 do ano seguinte no segundo.
    /// </summary>
    private static DateOnly CalcularPrazoEnvio(short ano, short semestre) =>
        semestre == 1 ? new DateOnly(ano, 7, 31) : new DateOnly(ano + 1, 1, 31);

    /// <summary>Janela do semestre: 01/01–30/06 ou 01/07–31/12.</summary>
    private static (DateOnly Inicio, DateOnly Fim) JanelaDoSemestre(short ano, short semestre) =>
        semestre == 1
            ? (new DateOnly(ano, 1, 1), new DateOnly(ano, 6, 30))
            : (new DateOnly(ano, 7, 1), new DateOnly(ano, 12, 31));

    public async Task<List<PgiaRelatorioSemestralResponse>> ListarRelatoriosSemestraisAsync(long orgaoId)
    {
        var lista = await _repositorio.ListarRelatoriosSemestraisPorOrgaoAsync(orgaoId);
        // Lista de acompanhamento: sem o conteúdo agregado, que é caro e só interessa no detalhe
        return lista.Select(r => MapRelatorioSemestral(r, null)).ToList();
    }

    public async Task<PgiaRelatorioSemestralResponse> GetRelatorioSemestralAsync(long id)
    {
        var relatorio = await _repositorio.GetRelatorioSemestralByIdAsync(id)
            ?? throw new ApiException(ErrorCode.PgiaRelatorioNaoEncontrado);

        var conteudo = await MontarConteudoAsync(relatorio);
        return MapRelatorioSemestral(relatorio, conteudo);
    }

    /// <summary>
    /// Incisos I a V do art. 32, agregados das demais tabelas no momento da consulta.
    /// </summary>
    private async Task<PgiaRelatorioConteudoResponse> MontarConteudoAsync(PgiaRelatorioSemestral relatorio)
    {
        var (inicio, fim) = JanelaDoSemestre(relatorio.Ano, relatorio.Semestre);
        // O semestre é dia civil de Brasília, mas os timestamps do banco são UTC de
        // verdade: converter (e não carimbar Kind) evita perder o que aconteceu nas
        // três últimas horas do último dia do semestre.
        var inicioUtc = DateTimeHelper.ToUtc(inicio.ToDateTime(TimeOnly.MinValue));
        var fimUtc = DateTimeHelper.ToUtc(fim.ToDateTime(TimeOnly.MaxValue));

        var sistemas = await _repositorio.ListarSistemasDoOrgaoAsync(relatorio.OrgaoId);
        var incidentes = await _repositorio.ListarIncidentesComunicadosNoPeriodoAsync(relatorio.OrgaoId, inicioUtc, fimUtc);
        var capacitacoes = await _repositorio.ListarCapacitacoesPorOrgaoAsync(relatorio.OrgaoId);

        // III — indicadores dos sistemas de Alto Risco. Critério: interseção do período
        // de referência do indicador com o semestre (um indicador trimestral ou anual
        // que cobre parte do semestre continua sendo informação daquele semestre).
        var altoRisco = sistemas
            .Where(s => s.ClassificacaoRiscoAtual == PgiaDominios.ResultadoRisco.Alto)
            .Select(s => s.Id)
            .ToList();
        var indicadores = await _repositorio.ListarIndicadoresDoPeriodoAsync(altoRisco, inicio, fim);

        // V — inventário movimentado no período: sistema criado ou reclassificado
        var reclassificados = await _repositorio.ListarSistemasReclassificadosNoPeriodoAsync(relatorio.OrgaoId, inicioUtc, fimUtc);
        var atualizados = sistemas
            .Where(s => (s.CriadoEm >= inicioUtc && s.CriadoEm <= fimUtc) || reclassificados.Contains(s.Id))
            .ToList();

        return new PgiaRelatorioConteudoResponse
        {
            Sistemas = sistemas.Select(MapSistemaItem).ToList(),
            Incidentes = incidentes.Select(i => new PgiaRelatorioIncidenteItem
            {
                Id = i.Id,
                SistemaDenominacao = i.Sistema?.Denominacao ?? string.Empty,
                Hipotese = i.Hipotese,
                DataComunicacaoSgdi = i.DataComunicacaoSgdi,
                StatusApuracao = i.StatusApuracao,
                MedidasAdotadas = i.MedidasAdotadas
            }).ToList(),
            IndicadoresAltoRisco = indicadores.Select(MapIndicador).ToList(),
            Capacitacoes = capacitacoes.Select(c => new PgiaCapacitacaoResponse
            {
                Id = c.Id,
                AgenteId = c.AgenteId,
                AgenteNome = c.Agente?.Nome ?? string.Empty,
                OrgaoId = c.OrgaoId,
                Trilha = c.Trilha,
                Status = c.Status,
                DataConclusao = c.DataConclusao,
                PrevistaPlanoCapacitacao = c.PrevistaPlanoCapacitacao,
                CertificadoDocId = c.CertificadoDocId,
                CriadoEm = c.CriadoEm
            }).ToList(),
            AtualizacoesInventario = atualizados.Select(MapSistemaItem).ToList()
        };
    }

    public async Task<PgiaRelatorioSemestralResponse> RegistrarEnvioAsync(
        long id, PgiaRelatorioEnvioDTO dto, PgiaUserContext ctx)
    {
        var relatorio = await _repositorio.GetRelatorioSemestralByIdAsync(id)
            ?? throw new ApiException(ErrorCode.PgiaRelatorioNaoEncontrado);

        if (dto.DataEnvio == default)
            throw new ApiException(ErrorCode.PgiaDominioInvalido, "Informe a data do envio à SGDI.");

        if (string.IsNullOrWhiteSpace(dto.ProcessoSei))
            throw new ApiException(ErrorCode.PgiaDominioInvalido,
                "Informe o processo SEI do envio (art. 32, caput).");

        // Não há como prestar contas de um semestre que ainda está correndo
        var (_, fimDoSemestre) = JanelaDoSemestre(relatorio.Ano, relatorio.Semestre);
        if (dto.DataEnvio <= fimDoSemestre)
            throw new ApiException(ErrorCode.PgiaPeriodoInvalido,
                "O relatório só pode ser enviado após o fim do semestre.");

        if (dto.DocumentoId != null)
        {
            var documento = await _repositorio.GetDocumentoByIdAsync(dto.DocumentoId.Value)
                ?? throw new ApiException(ErrorCode.PgiaDocumentoNaoEncontrado);

            if (documento.OrgaoId != relatorio.OrgaoId)
                throw new ApiException(ErrorCode.PgiaDocumentoNaoEncontrado,
                    "O documento informado pertence a outro órgão.");
        }

        relatorio.DataEnvio = dto.DataEnvio;
        relatorio.ProcessoSei = dto.ProcessoSei.Trim();
        relatorio.DocumentoId = dto.DocumentoId;
        // O status do envio é calculado, não declarado pelo órgão
        relatorio.Status = dto.DataEnvio <= relatorio.PrazoEnvio
            ? PgiaDominios.StatusRelatorioSemestral.EnviadoNoPrazo
            : PgiaDominios.StatusRelatorioSemestral.EnviadoEmAtraso;
        relatorio.AlteradoEm = DateTime.UtcNow;
        relatorio.AlteradoPor = ctx.Email;

        await _repositorio.SaveChangesAsync();
        return MapRelatorioSemestral(relatorio, null);
    }

    public async Task<PgiaRelatorioSemestralResponse> AtualizarSituacaoAsync(
        long id, PgiaRelatorioSituacaoDTO dto, PgiaUserContext ctx)
    {
        var relatorio = await _repositorio.GetRelatorioSemestralByIdAsync(id)
            ?? throw new ApiException(ErrorCode.PgiaRelatorioNaoEncontrado);

        if (!PgiaDominios.StatusRelatorioSemestral.Todos.Contains(dto.Status))
            throw new ApiException(ErrorCode.PgiaDominioInvalido, $"Situação do relatório inválida: {dto.Status}");

        // A situação declarada pela SGDI não pode contradizer o que está registrado:
        // o envio é o fato, o status é a leitura dele.
        var enviado = relatorio.DataEnvio != null;
        var statusDeEnvio = dto.Status is PgiaDominios.StatusRelatorioSemestral.EnviadoNoPrazo
            or PgiaDominios.StatusRelatorioSemestral.EnviadoEmAtraso;

        if (statusDeEnvio && !enviado)
            throw new ApiException(ErrorCode.PgiaDominioInvalido,
                "Não é possível marcar como enviado um relatório sem envio registrado.");

        if (dto.Status == PgiaDominios.StatusRelatorioSemestral.Inadimplente
            && enviado && relatorio.DataEnvio <= relatorio.PrazoEnvio)
            throw new ApiException(ErrorCode.PgiaDominioInvalido,
                "O relatório foi enviado dentro do prazo e não pode ser marcado como inadimplente.");

        if (dto.Status == PgiaDominios.StatusRelatorioSemestral.Pendente && enviado)
            throw new ApiException(ErrorCode.PgiaDominioInvalido,
                "O relatório já tem envio registrado e não pode voltar a pendente.");

        relatorio.Status = dto.Status;
        relatorio.RegistradoPainelEm = dto.RegistradoPainelEm;
        relatorio.ComunicadoControleInternoEm = dto.ComunicadoControleInternoEm;
        relatorio.AlteradoEm = DateTime.UtcNow;
        relatorio.AlteradoPor = ctx.Email;

        await _repositorio.SaveChangesAsync();
        return MapRelatorioSemestral(relatorio, null);
    }

    public async Task<byte[]> GerarPdfRelatorioSemestralAsync(long id)
    {
        var relatorio = await _repositorio.GetRelatorioSemestralByIdAsync(id)
            ?? throw new ApiException(ErrorCode.PgiaRelatorioNaoEncontrado);

        var conteudo = await MontarConteudoAsync(relatorio);
        return PgiaRelatorioPdf.Gerar(MapRelatorioSemestral(relatorio, conteudo));
    }

    // ── Relatório anual ───────────────────────────────────────────────────────

    public async Task<List<PgiaRelatorioAnualResponse>> ListarRelatoriosAnuaisAsync()
    {
        var lista = await _repositorio.ListarRelatoriosAnuaisAsync();
        return lista.Select(MapRelatorioAnual).ToList();
    }

    public async Task<PgiaRelatorioAnualResponse> CriarRelatorioAnualAsync(
        PgiaRelatorioAnualCreateDTO dto, PgiaUserContext ctx)
    {
        if (dto.Ano is < 2026 or > 2100)
            throw new ApiException(ErrorCode.PgiaDominioInvalido, $"Ano fora do intervalo aceito: {dto.Ano}");

        var existente = await _repositorio.GetRelatorioAnualPorAnoAsync(dto.Ano);
        if (existente != null)
            throw new ApiException(ErrorCode.PgiaRelatorioJaExiste,
                $"O Relatório Anual de {dto.Ano} já foi cadastrado.");

        await ValidarDocumentoAsync(dto.DocumentoId);

        var relatorio = new PgiaRelatorioAnual
        {
            Ano = dto.Ano,
            CriadoEm = DateTime.UtcNow,
            CriadoPor = ctx.Email
        };
        AplicarDadosRelatorioAnual(relatorio, dto);

        _repositorio.AddRelatorioAnual(relatorio);
        await _repositorio.SaveChangesAsync();
        return MapRelatorioAnual(relatorio);
    }

    public async Task<PgiaRelatorioAnualResponse> AtualizarRelatorioAnualAsync(
        long id, PgiaRelatorioAnualUpdateDTO dto, PgiaUserContext ctx)
    {
        var relatorio = await _repositorio.GetRelatorioAnualByIdAsync(id)
            ?? throw new ApiException(ErrorCode.PgiaRelatorioNaoEncontrado);

        if (dto.Ano is < 2026 or > 2100)
            throw new ApiException(ErrorCode.PgiaDominioInvalido, $"Ano fora do intervalo aceito: {dto.Ano}");

        var mesmoAno = await _repositorio.GetRelatorioAnualPorAnoAsync(dto.Ano);
        if (mesmoAno != null && mesmoAno.Id != id)
            throw new ApiException(ErrorCode.PgiaRelatorioJaExiste,
                $"O Relatório Anual de {dto.Ano} já foi cadastrado.");

        await ValidarDocumentoAsync(dto.DocumentoId);

        relatorio.Ano = dto.Ano;
        AplicarDadosRelatorioAnual(relatorio, dto);
        relatorio.AlteradoEm = DateTime.UtcNow;
        relatorio.AlteradoPor = ctx.Email;

        await _repositorio.SaveChangesAsync();
        return MapRelatorioAnual(relatorio);
    }

    private static void AplicarDadosRelatorioAnual(PgiaRelatorioAnual relatorio, PgiaRelatorioAnualCreateDTO dto)
    {
        relatorio.DataPublicacao = dto.DataPublicacao;
        relatorio.UrlPublicacao = string.IsNullOrWhiteSpace(dto.UrlPublicacao) ? null : dto.UrlPublicacao.Trim();
        relatorio.DocumentoId = dto.DocumentoId;
        relatorio.ApreciadoCgticEm = dto.ApreciadoCgticEm;
        relatorio.Recomendacoes = string.IsNullOrWhiteSpace(dto.Recomendacoes) ? null : dto.Recomendacoes.Trim();
        relatorio.AgendaInovacao = string.IsNullOrWhiteSpace(dto.AgendaInovacao) ? null : dto.AgendaInovacao.Trim();
    }

    private async Task ValidarDocumentoAsync(long? documentoId)
    {
        if (documentoId == null) return;

        _ = await _repositorio.GetDocumentoByIdAsync(documentoId.Value)
            ?? throw new ApiException(ErrorCode.PgiaDocumentoNaoEncontrado);
    }

    // ── Auditorias técnicas ───────────────────────────────────────────────────

    public async Task<PgiaAuditoriaTecnica?> GetAuditoriaEntidadeAsync(long id)
    {
        return await _repositorio.GetAuditoriaByIdAsync(id);
    }

    public async Task<List<PgiaAuditoriaResponse>> ListarAuditoriasAsync()
    {
        var lista = await _repositorio.ListarAuditoriasAsync();
        return lista.Select(MapAuditoria).ToList();
    }

    public async Task<List<PgiaAuditoriaResponse>> ListarMinhasAuditoriasAsync(PgiaUserContext ctx)
    {
        var lista = await _repositorio.ListarAuditoriasPorAuditorAsync(ctx.UserId);
        return lista.Select(MapAuditoria).ToList();
    }

    public async Task<List<PgiaAuditoriaResponse>> ListarAuditoriasPorSistemaAsync(long sistemaId)
    {
        var lista = await _repositorio.ListarAuditoriasPorSistemaAsync(sistemaId);
        return lista.Select(MapAuditoria).ToList();
    }

    public async Task<PgiaAuditoriaResponse> CriarAuditoriaAsync(PgiaAuditoriaCreateDTO dto, PgiaUserContext ctx)
    {
        var sistema = await _repositorio.GetSistemaByIdAsync(dto.SistemaIaId)
            ?? throw new ApiException(ErrorCode.PgiaSistemaNaoEncontrado);

        await ValidarAuditoriaAsync(dto);

        var auditoria = new PgiaAuditoriaTecnica
        {
            SistemaIaId = sistema.Id,
            Sistema = sistema,
            PublicadoPortal = false,
            CriadoEm = DateTime.UtcNow,
            CriadoPor = ctx.Email
        };
        AplicarDadosAuditoria(auditoria, dto);

        _repositorio.AddAuditoria(auditoria);
        await _repositorio.SaveChangesAsync();

        var salva = await _repositorio.GetAuditoriaByIdAsync(auditoria.Id);
        return MapAuditoria(salva ?? auditoria);
    }

    public async Task<PgiaAuditoriaResponse> AtualizarAuditoriaAsync(
        long id, PgiaAuditoriaCreateDTO dto, PgiaUserContext ctx)
    {
        var auditoria = await _repositorio.GetAuditoriaByIdAsync(id)
            ?? throw new ApiException(ErrorCode.PgiaAuditoriaNaoEncontrada);

        var sistema = await _repositorio.GetSistemaByIdAsync(dto.SistemaIaId)
            ?? throw new ApiException(ErrorCode.PgiaSistemaNaoEncontrado);

        // Revalida a designação (papel de auditoria externa) também na edição
        await ValidarAuditoriaAsync(dto);

        // A nova data de início não pode passar a conclusão já registrada
        if (auditoria.DataFim != null && dto.DataInicio > auditoria.DataFim)
            throw new ApiException(ErrorCode.PgiaPeriodoInvalido,
                "A data de início não pode ser posterior à conclusão já registrada da auditoria.");

        auditoria.SistemaIaId = sistema.Id;
        auditoria.Sistema = sistema;
        AplicarDadosAuditoria(auditoria, dto);
        auditoria.AlteradoEm = DateTime.UtcNow;
        auditoria.AlteradoPor = ctx.Email;

        await _repositorio.SaveChangesAsync();

        var salva = await _repositorio.GetAuditoriaByIdAsync(auditoria.Id);
        return MapAuditoria(salva ?? auditoria);
    }

    private async Task ValidarAuditoriaAsync(PgiaAuditoriaCreateDTO dto)
    {
        if (!PgiaDominios.TipoAuditoria.Todos.Contains(dto.Tipo))
            throw new ApiException(ErrorCode.PgiaDominioInvalido, $"Tipo de auditoria inválido: {dto.Tipo}");

        if (string.IsNullOrWhiteSpace(dto.EntidadeAuditora))
            throw new ApiException(ErrorCode.PgiaDominioInvalido, "Informe a entidade auditora.");

        if (dto.DataInicio == default)
            throw new ApiException(ErrorCode.PgiaDominioInvalido, "Informe a data de início da auditoria.");

        if (dto.AuditorUserId == null) return;

        // A designação só vale para quem tem o papel de auditoria externa: é o que
        // sustenta o escopo "só as auditorias designadas a ela"
        var auditor = await _repositorio.GetUserByIdAsync(dto.AuditorUserId.Value)
            ?? throw new ApiException(ErrorCode.PgiaUsuarioNaoEncontrado);

        if (auditor.PapelPgia != PapeisPgia.Auditoria)
            throw new ApiException(ErrorCode.PgiaDominioInvalido,
                "Designe um usuário com papel de auditoria externa.");
    }

    private static void AplicarDadosAuditoria(PgiaAuditoriaTecnica auditoria, PgiaAuditoriaCreateDTO dto)
    {
        auditoria.Tipo = dto.Tipo;
        auditoria.EntidadeAuditora = dto.EntidadeAuditora.Trim();
        auditoria.AuditorUserId = dto.AuditorUserId;
        auditoria.ExternaFornecedor = dto.ExternaFornecedor;
        auditoria.ApoioFapdf = dto.ApoioFapdf;
        auditoria.DataInicio = dto.DataInicio;
    }

    public async Task<PgiaAuditoriaResponse> RegistrarParecerAsync(
        long id, PgiaAuditoriaParecerDTO dto, PgiaUserContext ctx)
    {
        var auditoria = await _repositorio.GetAuditoriaByIdAsync(id)
            ?? throw new ApiException(ErrorCode.PgiaAuditoriaNaoEncontrada);

        // A auditora externa só trabalha no que lhe foi designado; SGDI e admin
        // registram por ela quando o parecer chega por fora do sistema.
        if (ctx.PapelEfetivo == PapeisPgia.Auditoria && auditoria.AuditorUserId != ctx.UserId)
            throw new ApiException(ErrorCode.PgiaAuditoriaNaoDesignada,
                "Esta auditoria não está designada a você.");

        if (dto.DataInicio == default)
            throw new ApiException(ErrorCode.PgiaDominioInvalido, "Informe a data de início dos trabalhos.");

        if (dto.DataFim != null && dto.DataFim < dto.DataInicio)
            throw new ApiException(ErrorCode.PgiaPeriodoInvalido,
                "A data de conclusão não pode ser anterior ao início dos trabalhos.");

        // A entidade designada mexe só nas datas e no parecer
        auditoria.DataInicio = dto.DataInicio;
        auditoria.DataFim = dto.DataFim;
        auditoria.Parecer = string.IsNullOrWhiteSpace(dto.Parecer) ? null : dto.Parecer.Trim();
        auditoria.AlteradoEm = DateTime.UtcNow;
        auditoria.AlteradoPor = ctx.Email;

        await _repositorio.SaveChangesAsync();
        return MapAuditoria(auditoria);
    }

    public async Task<PgiaAuditoriaResponse> PublicarAuditoriaAsync(
        long id, PgiaAuditoriaPublicacaoDTO dto, PgiaUserContext ctx)
    {
        var auditoria = await _repositorio.GetAuditoriaByIdAsync(id)
            ?? throw new ApiException(ErrorCode.PgiaAuditoriaNaoEncontrada);

        auditoria.PublicadoPortal = dto.PublicadoPortal;
        auditoria.DataPublicacao = dto.PublicadoPortal ? dto.DataPublicacao : null;
        auditoria.Url = dto.PublicadoPortal
            ? (string.IsNullOrWhiteSpace(dto.Url) ? null : dto.Url.Trim())
            : null;
        auditoria.AlteradoEm = DateTime.UtcNow;
        auditoria.AlteradoPor = ctx.Email;

        await _repositorio.SaveChangesAsync();
        return MapAuditoria(auditoria);
    }

    public async Task<List<PgiaAuditorResponse>> ListarAuditoresAsync()
    {
        var auditores = await _repositorio.ListarAuditoresAsync();
        return auditores.Select(u => new PgiaAuditorResponse
        {
            UserId = u.Id,
            Nome = u.Nome,
            Email = u.Email
        }).ToList();
    }

    // ── Mapeamentos ───────────────────────────────────────────────────────────

    private static PgiaRelatorioSistemaItem MapSistemaItem(PgiaSistemaIa s) => new()
    {
        Id = s.Id,
        Denominacao = s.Denominacao,
        StatusCicloVida = s.StatusCicloVida,
        ClassificacaoRiscoAtual = s.ClassificacaoRiscoAtual,
        SituacaoHomologacao = s.SituacaoHomologacao
    };

    private static PgiaIndicadorResponse MapIndicador(PgiaIndicadorDesempenho i) => new()
    {
        Id = i.Id,
        SistemaIaId = i.SistemaIaId,
        SistemaDenominacao = i.Sistema?.Denominacao ?? string.Empty,
        Nome = i.Nome,
        Categoria = i.Categoria,
        Valor = i.Valor,
        Unidade = i.Unidade,
        PeriodoInicio = i.PeriodoInicio,
        PeriodoFim = i.PeriodoFim,
        Meta = i.Meta,
        PublicadoRegistroPublico = i.PublicadoRegistroPublico,
        CriadoEm = i.CriadoEm
    };

    private static PgiaRelatorioSemestralResponse MapRelatorioSemestral(
        PgiaRelatorioSemestral r, PgiaRelatorioConteudoResponse? conteudo) => new()
    {
        Id = r.Id,
        OrgaoId = r.OrgaoId,
        OrgaoSigla = r.Orgao?.Sigla ?? string.Empty,
        Ano = r.Ano,
        Semestre = r.Semestre,
        DataEnvio = r.DataEnvio,
        ProcessoSei = r.ProcessoSei,
        DocumentoId = r.DocumentoId,
        Status = r.Status,
        RegistradoPainelEm = r.RegistradoPainelEm,
        ComunicadoControleInternoEm = r.ComunicadoControleInternoEm,
        PrazoEnvio = r.PrazoEnvio,
        Conteudo = conteudo,
        CriadoEm = r.CriadoEm
    };

    private static PgiaRelatorioAnualResponse MapRelatorioAnual(PgiaRelatorioAnual r) => new()
    {
        Id = r.Id,
        Ano = r.Ano,
        DataPublicacao = r.DataPublicacao,
        UrlPublicacao = r.UrlPublicacao,
        DocumentoId = r.DocumentoId,
        ApreciadoCgticEm = r.ApreciadoCgticEm,
        Recomendacoes = r.Recomendacoes,
        AgendaInovacao = r.AgendaInovacao,
        CriadoEm = r.CriadoEm
    };

    private static PgiaAuditoriaResponse MapAuditoria(PgiaAuditoriaTecnica a) => new()
    {
        Id = a.Id,
        SistemaIaId = a.SistemaIaId,
        SistemaDenominacao = a.Sistema?.Denominacao ?? string.Empty,
        OrgaoSigla = a.Sistema?.Orgao?.Sigla ?? string.Empty,
        Tipo = a.Tipo,
        EntidadeAuditora = a.EntidadeAuditora,
        AuditorUserId = a.AuditorUserId,
        AuditorNome = a.AuditorUser?.Nome,
        ExternaFornecedor = a.ExternaFornecedor,
        ApoioFapdf = a.ApoioFapdf,
        DataInicio = a.DataInicio,
        DataFim = a.DataFim,
        Parecer = a.Parecer,
        PublicadoPortal = a.PublicadoPortal,
        DataPublicacao = a.DataPublicacao,
        Url = a.Url,
        CriadoEm = a.CriadoEm
    };
}
