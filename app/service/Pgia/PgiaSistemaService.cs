using System.Text.Json;
using System.Text.Json.Serialization;
using api.Common;
using api.Pgia;
using Microsoft.EntityFrameworkCore;
using Models.Pgia;
using Repositorio.Interface;
using service.Interface;

namespace service.Pgia;

/// <summary>
/// Inventário de sistemas de IA e classificação de risco (etapa 2 do formulário;
/// arts. 9º, I, 14 a 20, 24 e 27). O resultado da classificação é sempre calculado
/// no servidor a partir do checklist dos arts. 15 a 17 — o front só mostra a prévia.
/// </summary>
public class PgiaSistemaService : IPgiaSistemaService
{
    private readonly IPgiaSistemaRepositorio _sistemaRepositorio;
    private readonly IPgiaOrgaoRepositorio _orgaoRepositorio;
    private readonly IPgiaDesignacaoRepositorio _designacaoRepositorio;
    private readonly IPgiaPermissionService _permissionService;

    public PgiaSistemaService(
        IPgiaSistemaRepositorio sistemaRepositorio,
        IPgiaOrgaoRepositorio orgaoRepositorio,
        IPgiaDesignacaoRepositorio designacaoRepositorio,
        IPgiaPermissionService permissionService)
    {
        _sistemaRepositorio = sistemaRepositorio;
        _orgaoRepositorio = orgaoRepositorio;
        _designacaoRepositorio = designacaoRepositorio;
        _permissionService = permissionService;
    }

    // ── Inventário ────────────────────────────────────────────────────────────

    public async Task<PagedResponse<PgiaSistemaResponse>> ListarSistemasAsync(
        PgiaUserContext ctx, long orgaoId, PagedRequest request)
    {
        var query = _permissionService.GetFilteredSistemasQuery(ctx)
            .Where(s => s.OrgaoId == orgaoId)
            .Include(s => s.AvaliadoPorUser)
            .Include(s => s.Responsavel)
                .ThenInclude(r => r!.Agente);

        // PagedRequest é compartilhado com o resto do SGDP e não valida os limites:
        // saneia aqui para Page/PageSize zerados ou negativos não quebrarem o Skip/Take.
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Max(1, request.PageSize);
        var skip = (page - 1) * pageSize;

        var totalItems = await query.CountAsync();
        var sistemas = await query
            .OrderBy(s => s.Denominacao)
            .Skip(skip)
            .Take(pageSize)
            .ToListAsync();

        return new PagedResponse<PgiaSistemaResponse>(
            sistemas.Select(MapSistema).ToList(), totalItems, page, pageSize);
    }

    public async Task<PgiaSistemaResponse> GetSistemaAsync(long id)
    {
        var sistema = await _sistemaRepositorio.GetByIdAsync(id)
            ?? throw new ApiException(ErrorCode.PgiaSistemaNaoEncontrado);
        return MapSistema(sistema);
    }

    public async Task<PgiaSistemaIa?> GetSistemaEntidadeAsync(long id)
    {
        return await _sistemaRepositorio.GetByIdAsync(id);
    }

    public async Task<PgiaSistemaResponse> CriarSistemaAsync(long orgaoId, PgiaSistemaCreateDTO dto, PgiaUserContext ctx)
    {
        var orgao = await _orgaoRepositorio.GetByIdAsync(orgaoId)
            ?? throw new ApiException(ErrorCode.PgiaOrgaoNaoEncontrado);

        // O inventário é responsabilidade do Responsável de IA designado e comunicado (art. 10)
        var responsavel = await _designacaoRepositorio.GetResponsavelVigenteAsync(orgaoId)
            ?? throw new ApiException(ErrorCode.PgiaResponsavelNaoDesignado,
                "Designe e comunique o Responsável de IA antes de cadastrar sistemas (art. 10).");

        ValidarCamposObrigatorios(dto);
        ValidarDominios(dto);

        var denominacao = dto.Denominacao.Trim();
        var jaExiste = await _sistemaRepositorio.GetByDenominacaoAsync(orgaoId, denominacao);
        if (jaExiste != null)
            throw new ApiException(ErrorCode.PgiaSistemaJaExiste,
                $"O órgão já tem um sistema chamado {denominacao} no inventário.");

        var avaliada = AvaliarClassificacao(dto.Classificacao, ctx.Email);
        var (resultado, enquadramento, checklistJson, pontuacao) =
            (avaliada.Resultado, avaliada.Enquadramento, avaliada.ChecklistJson, avaliada.Pontuacao);
        ValidarEAjustarCondicionais(dto, resultado);

        // O cadastro de sistema em uso com Risco Excessivo é aceito de propósito:
        // o inventário registra a realidade para a SGDI agir (arts. 9º, III e 15).
        // O bloqueio vale na edição, ao tentar colocar em uso o que já está classificado.

        var agora = DateTime.UtcNow;
        var sistema = new PgiaSistemaIa
        {
            OrgaoId = orgaoId,
            Orgao = orgao,
            ResponsavelIaId = responsavel.Id,
            Responsavel = responsavel,
            ClassificacaoRiscoAtual = resultado,
            EnquadramentoLegal = enquadramento,
            // Rota de homologação pela classificação: Baixo/Moderado à SGDI, o resto ao CGTIC
            SituacaoHomologacao = PgiaDominios.SituacaoHomologacao.RotaInicial(resultado),
            PublicadoRegistroPublico = false,
            CriadoEm = agora,
            CriadoPor = ctx.Email
        };
        AplicarDadosInventario(sistema, dto, denominacao);

        var classificacao = new PgiaClassificacaoRisco
        {
            Sistema = sistema,
            DataClassificacao = dto.Classificacao.DataClassificacao,
            Motivo = dto.Classificacao.Motivo,
            RespostasChecklist = checklistJson,
            Resultado = resultado,
            Pontuacao = pontuacao,
            EnquadramentoLegal = enquadramento,
            Justificativa = dto.Classificacao.Justificativa.Trim(),
            ClassificadoPor = ctx.UserId,
            // Riscos declarados no grupo "Outros" entram no mesmo SaveChanges
            OutrosRiscos = avaliada.OutrosRiscos,
            CriadoEm = agora,
            CriadoPor = ctx.Email
        };

        // Sistema e classificação inicial gravam juntos: o inventário nunca fica
        // sem a classificação que o art. 14 exige.
        _sistemaRepositorio.AddSistema(sistema);
        _sistemaRepositorio.AddClassificacao(classificacao);
        await _sistemaRepositorio.SaveChangesAsync();

        return MapSistema(sistema);
    }

    public async Task<PgiaSistemaResponse> AtualizarSistemaAsync(long id, PgiaSistemaUpdateDTO dto, PgiaUserContext ctx)
    {
        var sistema = await _sistemaRepositorio.GetByIdAsync(id)
            ?? throw new ApiException(ErrorCode.PgiaSistemaNaoEncontrado);

        ValidarCamposObrigatorios(dto);
        ValidarDominios(dto);

        var denominacao = dto.Denominacao.Trim();
        var jaExiste = await _sistemaRepositorio.GetByDenominacaoAsync(sistema.OrgaoId, denominacao);
        if (jaExiste != null && jaExiste.Id != sistema.Id)
            throw new ApiException(ErrorCode.PgiaSistemaJaExiste,
                $"O órgão já tem um sistema chamado {denominacao} no inventário.");

        if (sistema.ClassificacaoRiscoAtual == PgiaDominios.ResultadoRisco.Excessivo
            && PgiaDominios.StatusCicloVida.EmUso.Contains(dto.StatusCicloVida))
            throw new ApiException(ErrorCode.PgiaRiscoExcessivoBloqueado,
                "Sistema enquadrado no art. 15 não pode ser implantado nem utilizado.");

        // Gate de implantação: só na transição para uso; sistema já em uso continua editável
        var entrandoEmUso = PgiaDominios.StatusCicloVida.EmUso.Contains(dto.StatusCicloVida)
            && !PgiaDominios.StatusCicloVida.EmUso.Contains(sistema.StatusCicloVida);
        if (entrandoEmUso)
            await ValidarGateDeImplantacaoAsync(sistema);

        // As condicionais de Alto/Moderado seguem a classificação vigente;
        // mudar o risco é pelo endpoint de reclassificação.
        ValidarEAjustarCondicionais(dto, sistema.ClassificacaoRiscoAtual);

        AplicarDadosInventario(sistema, dto, denominacao);
        sistema.AlteradoEm = DateTime.UtcNow;
        sistema.AlteradoPor = ctx.Email;

        await _sistemaRepositorio.SaveChangesAsync();
        return MapSistema(sistema);
    }

    // ── Classificação de risco ────────────────────────────────────────────────

    public async Task<PgiaClassificacaoResponse> ReclassificarAsync(long id, PgiaClassificacaoCreateDTO dto, PgiaUserContext ctx)
    {
        var sistema = await _sistemaRepositorio.GetByIdAsync(id)
            ?? throw new ApiException(ErrorCode.PgiaSistemaNaoEncontrado);

        var avaliada = AvaliarClassificacao(dto, ctx.Email);
        var (resultado, enquadramento, checklistJson, pontuacao) =
            (avaliada.Resultado, avaliada.Enquadramento, avaliada.ChecklistJson, avaliada.Pontuacao);

        var agora = DateTime.UtcNow;
        var classificacao = new PgiaClassificacaoRisco
        {
            SistemaIaId = sistema.Id,
            Sistema = sistema,
            DataClassificacao = dto.DataClassificacao,
            Motivo = dto.Motivo,
            RespostasChecklist = checklistJson,
            Resultado = resultado,
            Pontuacao = pontuacao,
            EnquadramentoLegal = enquadramento,
            Justificativa = dto.Justificativa.Trim(),
            ClassificadoPor = ctx.UserId,
            // Riscos declarados no grupo "Outros" entram no mesmo SaveChanges
            OutrosRiscos = avaliada.OutrosRiscos,
            CriadoEm = agora,
            CriadoPor = ctx.Email
        };
        _sistemaRepositorio.AddClassificacao(classificacao);

        // Desnormalizados do inventário acompanham a última classificação (art. 24, II);
        // o histórico anterior fica preservado.
        sistema.ClassificacaoRiscoAtual = resultado;
        sistema.EnquadramentoLegal = enquadramento;
        // Risco novo, homologação nova: a avaliação anterior perde validade e a rota
        // é recalculada (um sistema que virou Alto Risco passa a depender do CGTIC).
        sistema.SituacaoHomologacao = PgiaDominios.SituacaoHomologacao.RotaInicial(resultado);
        sistema.AvaliacaoParecer = null;
        sistema.AvaliadoPor = null;
        sistema.AvaliadoEm = null;
        sistema.DeliberacaoHomologacaoId = null;
        sistema.AlteradoEm = agora;
        sistema.AlteradoPor = ctx.Email;

        await _sistemaRepositorio.SaveChangesAsync();

        var salva = (await _sistemaRepositorio.ListarClassificacoesAsync(sistema.Id))
            .FirstOrDefault(c => c.Id == classificacao.Id);
        // Quem classifica é o órgão: a resposta do POST nunca devolve a pontuação
        return MapClassificacao(salva ?? classificacao, incluirPontuacao: false);
    }

    public async Task<List<PgiaClassificacaoResponse>> ListarClassificacoesAsync(long id, bool incluirPontuacao)
    {
        var lista = await _sistemaRepositorio.ListarClassificacoesAsync(id);
        return lista.Select(c => MapClassificacao(c, incluirPontuacao)).ToList();
    }

    // ── Documentos ────────────────────────────────────────────────────────────

    public async Task<List<PgiaDocumentoResponse>> ListarDocumentosAsync(long sistemaId)
    {
        var documentos = await _sistemaRepositorio.ListarDocumentosAsync(sistemaId);
        return documentos.Select(MapDocumento).ToList();
    }

    public async Task<PgiaDocumentoResponse> CriarDocumentoAsync(long sistemaId, PgiaDocumentoCreateDTO dto, PgiaUserContext ctx)
    {
        var sistema = await _sistemaRepositorio.GetByIdAsync(sistemaId)
            ?? throw new ApiException(ErrorCode.PgiaSistemaNaoEncontrado);

        if (!PgiaDominios.TipoDocumento.Todos.Contains(dto.Tipo))
            throw new ApiException(ErrorCode.PgiaDominioInvalido, $"Tipo de documento inválido: {dto.Tipo}");

        if (string.IsNullOrWhiteSpace(dto.NomeArquivo))
            throw new ApiException(ErrorCode.PgiaDominioInvalido, "Informe o nome do arquivo do documento.");

        var agora = DateTime.UtcNow;
        var documento = new PgiaDocumento
        {
            Tipo = dto.Tipo,
            // O documento pertence ao órgão dono do sistema, não ao órgão de quem envia
            OrgaoId = sistema.OrgaoId,
            SistemaIaId = sistema.Id,
            ProcessoSei = string.IsNullOrWhiteSpace(dto.ProcessoSei) ? null : dto.ProcessoSei.Trim(),
            NomeArquivo = dto.NomeArquivo.Trim(),
            UrlStorage = string.IsNullOrWhiteSpace(dto.UrlStorage) ? null : dto.UrlStorage.Trim(),
            DataEnvio = agora,
            EnviadoPor = ctx.UserId,
            CriadoEm = agora,
            CriadoPor = ctx.Email
        };

        _sistemaRepositorio.AddDocumento(documento);
        await _sistemaRepositorio.SaveChangesAsync();

        var salvo = await _sistemaRepositorio.GetDocumentoAsync(documento.Id);
        return MapDocumento(salvo ?? documento);
    }

    // ── Homologação do inventário (fase 2) ────────────────────────────────────

    public async Task<List<PgiaHomologacaoPendenteResponse>> ListarHomologacoesPendentesAsync(
        PgiaUserContext ctx, string? situacao)
    {
        var aguardando = new[]
        {
            PgiaDominios.SituacaoHomologacao.AguardandoSgdi,
            PgiaDominios.SituacaoHomologacao.AguardandoCgtic
        };

        if (!string.IsNullOrWhiteSpace(situacao))
        {
            if (!aguardando.Contains(situacao))
                throw new ApiException(ErrorCode.PgiaDominioInvalido,
                    $"Situação de homologação inválida para a fila: {situacao}");
            aguardando = new[] { situacao };
        }

        var sistemas = await _permissionService.GetFilteredSistemasQuery(ctx)
            .Include(s => s.Responsavel)
                .ThenInclude(r => r!.Agente)
            .Include(s => s.AvaliadoPorUser)
            .Where(s => aguardando.Contains(s.SituacaoHomologacao))
            // Fila drena por antiguidade: o que espera há mais tempo aparece primeiro
            .OrderBy(s => s.CriadoEm)
            .ThenBy(s => s.Id)
            .ToListAsync();

        // Classificação vigente de toda a fila numa consulta só
        var vigentes = await _sistemaRepositorio.ListarClassificacoesVigentesAsync(sistemas.Select(s => s.Id));

        return sistemas.Select(sistema => new PgiaHomologacaoPendenteResponse
        {
            Sistema = MapSistema(sistema),
            // Quem consulta a fila é instância central: vê a pontuação dos quesitos
            ClassificacaoVigente = vigentes.TryGetValue(sistema.Id, out var vigente)
                ? MapClassificacao(vigente, incluirPontuacao: true)
                : null
        }).ToList();
    }

    public async Task<PgiaSistemaResponse> AvaliarHomologacaoAsync(
        long sistemaId, PgiaAvaliacaoHomologacaoDTO dto, PgiaUserContext ctx)
    {
        var sistema = await _sistemaRepositorio.GetByIdAsync(sistemaId)
            ?? throw new ApiException(ErrorCode.PgiaSistemaNaoEncontrado);

        // Alto Risco e Risco Excessivo são decididos por deliberação do CGTIC,
        // não pela avaliação direta da SGDI.
        if (sistema.SituacaoHomologacao != PgiaDominios.SituacaoHomologacao.AguardandoSgdi)
            throw new ApiException(ErrorCode.PgiaHomologacaoIndevida,
                "Este sistema não está aguardando avaliação da SGDI.");

        if (string.IsNullOrWhiteSpace(dto.Parecer))
            throw new ApiException(ErrorCode.PgiaDominioInvalido, "Informe o parecer da avaliação.");

        var agora = DateTime.UtcNow;
        sistema.SituacaoHomologacao = dto.Aprovado
            ? PgiaDominios.SituacaoHomologacao.Aprovado
            : PgiaDominios.SituacaoHomologacao.Vetado;
        sistema.AvaliacaoParecer = dto.Parecer.Trim();
        sistema.AvaliadoPor = ctx.UserId;
        sistema.AvaliadoEm = agora;
        sistema.AlteradoEm = agora;
        sistema.AlteradoPor = ctx.Email;

        await _sistemaRepositorio.SaveChangesAsync();

        var salvo = await _sistemaRepositorio.GetByIdAsync(sistemaId);
        return MapSistema(salvo ?? sistema);
    }

    public async Task<PgiaSistemaResponse> AtualizarRegistroPublicoAsync(
        long sistemaId, PgiaRegistroPublicoDTO dto, PgiaUserContext ctx)
    {
        var sistema = await _sistemaRepositorio.GetByIdAsync(sistemaId)
            ?? throw new ApiException(ErrorCode.PgiaSistemaNaoEncontrado);

        sistema.PublicadoRegistroPublico = dto.Publicado;
        sistema.DataPublicacaoRegistro = dto.Publicado ? dto.DataPublicacao : null;
        sistema.AlteradoEm = DateTime.UtcNow;
        sistema.AlteradoPor = ctx.Email;

        await _sistemaRepositorio.SaveChangesAsync();
        return MapSistema(sistema);
    }

    /// <summary>
    /// Condições para colocar um sistema em uso: homologação aprovada e, no Alto Risco,
    /// AIA concluída, publicada no Portal e com deliberação favorável (art. 16, § 1º).
    /// </summary>
    private async Task ValidarGateDeImplantacaoAsync(PgiaSistemaIa sistema)
    {
        if (sistema.SituacaoHomologacao == PgiaDominios.SituacaoHomologacao.Vetado)
            throw new ApiException(ErrorCode.PgiaImplantacaoBloqueada,
                "O sistema foi vetado na homologação e não pode ser implantado.");

        if (sistema.SituacaoHomologacao != PgiaDominios.SituacaoHomologacao.Aprovado)
            throw new ApiException(ErrorCode.PgiaImplantacaoBloqueada,
                "A implantação depende da homologação: aguarde a avaliação da SGDI ou a deliberação do CGTIC.");

        if (sistema.ClassificacaoRiscoAtual != PgiaDominios.ResultadoRisco.Alto) return;

        var aias = await _sistemaRepositorio.ListarAiasAsync(sistema.Id);
        // Defesa em profundidade: o tipo da deliberação é reconferido aqui, não só no vínculo
        var apta = aias.Any(a => a.Status == PgiaDominios.StatusAia.Concluida
            && a.PublicadaPortal
            && a.Deliberacao != null
            && a.Deliberacao.Resultado == PgiaDominios.ResultadoDeliberacao.Favoravel
            && PgiaDominios.TipoDeliberacao.DecidemHomologacao.Contains(a.Deliberacao.Tipo));

        if (!apta)
            throw new ApiException(ErrorCode.PgiaImplantacaoBloqueada,
                "Sistema de Alto Risco exige AIA concluída, publicada no Portal e deliberação favorável do CGTIC antes da implantação (art. 16, § 1º).");
    }

    // ── AIA (art. 22) ─────────────────────────────────────────────────────────

    public async Task<List<PgiaAiaResponse>> ListarAiasAsync(long sistemaId)
    {
        var aias = await _sistemaRepositorio.ListarAiasAsync(sistemaId);
        return aias.Select(MapAia).ToList();
    }

    public async Task<PgiaAia?> GetAiaEntidadeAsync(long aiaId)
    {
        return await _sistemaRepositorio.GetAiaByIdAsync(aiaId);
    }

    public async Task<PgiaAiaResponse> CriarAiaAsync(long sistemaId, PgiaAiaCreateDTO dto, PgiaUserContext ctx)
    {
        var sistema = await _sistemaRepositorio.GetByIdAsync(sistemaId)
            ?? throw new ApiException(ErrorCode.PgiaSistemaNaoEncontrado);

        ValidarAia(dto);
        await ValidarDocumentosDaAiaAsync(dto, sistema.OrgaoId);

        var agora = DateTime.UtcNow;
        var aia = new PgiaAia
        {
            SistemaIaId = sistema.Id,
            Sistema = sistema,
            ElaboradaPor = ctx.UserId,
            PublicadaPortal = false,
            CriadoEm = agora,
            CriadoPor = ctx.Email
        };
        AplicarDadosAia(aia, dto);

        _sistemaRepositorio.AddAia(aia);
        await _sistemaRepositorio.SaveChangesAsync();

        var salva = await _sistemaRepositorio.GetAiaByIdAsync(aia.Id);
        return MapAia(salva ?? aia);
    }

    public async Task<PgiaAiaResponse> AtualizarAiaAsync(long aiaId, PgiaAiaUpdateDTO dto, PgiaUserContext ctx)
    {
        var aia = await _sistemaRepositorio.GetAiaByIdAsync(aiaId)
            ?? throw new ApiException(ErrorCode.PgiaAiaNaoEncontrada);

        ValidarAia(dto);
        await ValidarDocumentosDaAiaAsync(dto, aia.Sistema?.OrgaoId ?? 0);

        AplicarDadosAia(aia, dto);
        aia.AlteradoEm = DateTime.UtcNow;
        aia.AlteradoPor = ctx.Email;

        await _sistemaRepositorio.SaveChangesAsync();
        return MapAia(aia);
    }

    public async Task<PgiaAiaResponse> PublicarAiaAsync(long aiaId, PgiaAiaPublicacaoDTO dto, PgiaUserContext ctx)
    {
        var aia = await _sistemaRepositorio.GetAiaByIdAsync(aiaId)
            ?? throw new ApiException(ErrorCode.PgiaAiaNaoEncontrada);

        aia.PublicadaPortal = dto.PublicadaPortal;
        aia.DataPublicacaoPortal = dto.PublicadaPortal ? dto.DataPublicacaoPortal : null;
        aia.UrlPublicacao = dto.PublicadaPortal
            ? (string.IsNullOrWhiteSpace(dto.UrlPublicacao) ? null : dto.UrlPublicacao.Trim())
            : null;
        aia.AlteradoEm = DateTime.UtcNow;
        aia.AlteradoPor = ctx.Email;

        await _sistemaRepositorio.SaveChangesAsync();
        return MapAia(aia);
    }

    public async Task<PgiaAiaResponse> VincularDeliberacaoAiaAsync(long aiaId, long deliberacaoId, PgiaUserContext ctx)
    {
        var aia = await _sistemaRepositorio.GetAiaByIdAsync(aiaId)
            ?? throw new ApiException(ErrorCode.PgiaAiaNaoEncontrada);

        var deliberacao = await _sistemaRepositorio.GetDeliberacaoAsync(deliberacaoId)
            ?? throw new ApiException(ErrorCode.PgiaDeliberacaoNaoEncontrada);

        // Só habilita a implantação a deliberação que decide este sistema:
        // deliberação geral ou de outro assunto (suspensão, guia) não serve de aval.
        if (deliberacao.SistemaIaId != aia.SistemaIaId
            || !PgiaDominios.TipoDeliberacao.DecidemHomologacao.Contains(deliberacao.Tipo))
            throw new ApiException(ErrorCode.PgiaDeliberacaoNaoEncontrada,
                "Vincule uma deliberação do CGTIC que aprove este sistema (art. 16, § 1º).");

        aia.DeliberacaoCgticId = deliberacao.Id;
        aia.Deliberacao = deliberacao;
        aia.AlteradoEm = DateTime.UtcNow;
        aia.AlteradoPor = ctx.Email;

        await _sistemaRepositorio.SaveChangesAsync();
        return MapAia(aia);
    }

    private static void ValidarAia(PgiaAiaCreateDTO dto)
    {
        if (!PgiaDominios.StatusAia.Todos.Contains(dto.Status))
            throw new ApiException(ErrorCode.PgiaDominioInvalido, $"Situação da AIA inválida: {dto.Status}");

        if (string.IsNullOrWhiteSpace(dto.ImpactosDireitosFundamentais)
            || string.IsNullOrWhiteSpace(dto.MedidasPreventivas)
            || string.IsNullOrWhiteSpace(dto.MedidasMitigadoras)
            || string.IsNullOrWhiteSpace(dto.MedidasReversao))
            throw new ApiException(ErrorCode.PgiaDominioInvalido,
                "A AIA exige impactos sobre direitos fundamentais e as medidas preventivas, mitigadoras e de reversão (art. 22).");

        if (dto.DataInicio == default)
            throw new ApiException(ErrorCode.PgiaDominioInvalido, "Informe a data de início da AIA.");

        // A avaliação é prévia e contínua: concluir exige data e próxima revisão (art. 2º, VII)
        if (dto.Status == PgiaDominios.StatusAia.Concluida
            && (dto.DataConclusao == null || dto.ProximaRevisao == null))
            throw new ApiException(ErrorCode.PgiaDominioInvalido,
                "AIA concluída exige a data de conclusão e a data da próxima revisão.");
    }

    private async Task ValidarDocumentosDaAiaAsync(PgiaAiaCreateDTO dto, long orgaoId)
    {
        foreach (var documentoId in new[] { dto.RipdDocumentoId, dto.DocumentoId })
        {
            if (documentoId == null) continue;

            var documento = await _sistemaRepositorio.GetDocumentoAsync(documentoId.Value)
                ?? throw new ApiException(ErrorCode.PgiaDocumentoNaoEncontrado);

            if (documento.OrgaoId != orgaoId)
                throw new ApiException(ErrorCode.PgiaDocumentoNaoEncontrado,
                    "O documento informado pertence a outro órgão.");
        }
    }

    private static void AplicarDadosAia(PgiaAia aia, PgiaAiaCreateDTO dto)
    {
        aia.Status = dto.Status;
        aia.DataInicio = dto.DataInicio;
        aia.DataConclusao = dto.DataConclusao;
        aia.ImpactosDireitosFundamentais = dto.ImpactosDireitosFundamentais.Trim();
        aia.MedidasPreventivas = dto.MedidasPreventivas.Trim();
        aia.MedidasMitigadoras = dto.MedidasMitigadoras.Trim();
        aia.MedidasReversao = dto.MedidasReversao.Trim();
        aia.PreviaLicitacao = dto.PreviaLicitacao;
        aia.ConjuntaRipd = dto.ConjuntaRipd;
        aia.RipdDocumentoId = dto.RipdDocumentoId;
        aia.DocumentoId = dto.DocumentoId;
        aia.ProximaRevisao = dto.ProximaRevisao;
    }

    private static PgiaAiaResponse MapAia(PgiaAia a)
    {
        return new PgiaAiaResponse
        {
            Id = a.Id,
            SistemaIaId = a.SistemaIaId,
            Status = a.Status,
            DataInicio = a.DataInicio,
            DataConclusao = a.DataConclusao,
            ImpactosDireitosFundamentais = a.ImpactosDireitosFundamentais,
            MedidasPreventivas = a.MedidasPreventivas,
            MedidasMitigadoras = a.MedidasMitigadoras,
            MedidasReversao = a.MedidasReversao,
            PreviaLicitacao = a.PreviaLicitacao,
            ConjuntaRipd = a.ConjuntaRipd,
            RipdDocumentoId = a.RipdDocumentoId,
            DocumentoId = a.DocumentoId,
            ElaboradaPorNome = a.ElaboradaPorUser?.Nome ?? string.Empty,
            DeliberacaoCgticId = a.DeliberacaoCgticId,
            DeliberacaoResultado = a.Deliberacao?.Resultado,
            PublicadaPortal = a.PublicadaPortal,
            DataPublicacaoPortal = a.DataPublicacaoPortal,
            UrlPublicacao = a.UrlPublicacao,
            ProximaRevisao = a.ProximaRevisao,
            CriadoEm = a.CriadoEm
        };
    }

    // ── Regra de risco (arts. 15 a 18) ────────────────────────────────────────

    /// <summary>Resultado da avaliação do questionário, pronto para virar entidade.</summary>
    private sealed record ClassificacaoAvaliada(
        string Resultado,
        string? Enquadramento,
        string ChecklistJson,
        int Pontuacao,
        List<PgiaRiscoOutro> OutrosRiscos);

    /// <summary>
    /// Valida o questionário e devolve resultado, enquadramento legal, o jsonb das
    /// respostas e os riscos declarados no grupo "Outros".
    /// Art. 15 vence art. 16, que vence art. 17; sem marcação, Baixo Risco (art. 18).
    /// O "Nenhuma das alternativas acima" só atesta que o grupo foi respondido:
    /// não pontua e não altera o resultado.
    /// </summary>
    private static ClassificacaoAvaliada AvaliarClassificacao(PgiaClassificacaoCreateDTO dto, string? userEmail)
    {
        if (!PgiaDominios.MotivoClassificacao.Todos.Contains(dto.Motivo))
            throw new ApiException(ErrorCode.PgiaDominioInvalido, $"Motivo da classificação inválido: {dto.Motivo}");

        if (string.IsNullOrWhiteSpace(dto.Justificativa))
            throw new ApiException(ErrorCode.PgiaClassificacaoInvalida,
                "A justificativa da classificação de risco é obrigatória.");

        if (dto.DataClassificacao == default)
            throw new ApiException(ErrorCode.PgiaClassificacaoInvalida, "Informe a data da classificação.");

        var checklist = dto.Checklist ?? new PgiaChecklistDTO();
        var q15 = NormalizarIncisos(checklist.Q15, PgiaDominios.ChecklistIncisos.Art15, 15);
        var q16 = NormalizarIncisos(checklist.Q16, PgiaDominios.ChecklistIncisos.Art16, 16);
        var q17 = NormalizarIncisos(checklist.Q17, PgiaDominios.ChecklistIncisos.Art17, 17);

        // Completude: cada grupo respondido com incisos XOR "nenhuma das alternativas"
        ValidarCompletudeDoGrupo(15, q15, checklist.Q15Nenhuma);
        ValidarCompletudeDoGrupo(16, q16, checklist.Q16Nenhuma);
        ValidarCompletudeDoGrupo(17, q17, checklist.Q17Nenhuma);

        string resultado;
        string? enquadramento;
        if (q15.Count > 0)
        {
            resultado = PgiaDominios.ResultadoRisco.Excessivo;
            enquadramento = $"art. 15, {q15[0]}";
        }
        else if (q16.Count > 0)
        {
            resultado = PgiaDominios.ResultadoRisco.Alto;
            enquadramento = $"art. 16, {q16[0]}";
        }
        else if (q17.Count > 0)
        {
            resultado = PgiaDominios.ResultadoRisco.Moderado;
            enquadramento = $"art. 17, {q17[0]}";
        }
        else
        {
            resultado = PgiaDominios.ResultadoRisco.Baixo;
            enquadramento = null;
        }

        // Formato do schema mais os "nenhuma" de cada grupo, no MESMO jsonb
        var json = JsonSerializer.Serialize(new ChecklistPersistido
        {
            Q15 = q15,
            Q16 = q16,
            Q17 = q17,
            Q15Nenhuma = checklist.Q15Nenhuma,
            Q16Nenhuma = checklist.Q16Nenhuma,
            Q17Nenhuma = checklist.Q17Nenhuma
        });

        // Métrica de acompanhamento da SGDI, paralela ao resultado dos arts. 15 a 18.
        // Os "nenhuma" e os riscos do grupo "Outros" não entram nesta soma.
        var pontuacao = PgiaQuesitos.CalcularPontuacao(q15, q16, q17);

        var outros = MontarOutrosRiscos(dto.OutrosRiscos, userEmail);

        return new ClassificacaoAvaliada(resultado, enquadramento, json, pontuacao, outros);
    }

    /// <summary>
    /// Cada grupo do questionário precisa de uma resposta explícita: ou o órgão
    /// marca os incisos que se aplicam, ou marca "Nenhuma das alternativas acima".
    /// Os dois juntos, ou nenhum dos dois, deixam o grupo ambíguo.
    /// </summary>
    private static void ValidarCompletudeDoGrupo(int artigo, List<string> incisos, bool nenhuma)
    {
        if (incisos.Count > 0 && nenhuma)
            throw new ApiException(ErrorCode.PgiaChecklistInvalido,
                $"No grupo do art. {artigo}, marque os incisos aplicáveis ou \"Nenhuma das alternativas acima\", não os dois.");

        if (incisos.Count == 0 && !nenhuma)
            throw new ApiException(ErrorCode.PgiaChecklistInvalido,
                $"Responda o grupo do art. {artigo}: marque os incisos aplicáveis ou \"Nenhuma das alternativas acima\".");
    }

    /// <summary>
    /// Grupo "Outros": riscos que o órgão declara, pela matriz da CGDF. São registro
    /// complementar — não entram no cálculo do resultado nem na pontuação. A escala
    /// aceita aqui é o subconjunto permitido (probabilidade e consequência baixas a
    /// médias); risco alto tem grupo próprio nos arts. 15 a 17.
    /// </summary>
    private static List<PgiaRiscoOutro> MontarOutrosRiscos(List<PgiaRiscoOutroDTO>? itens, string? userEmail)
    {
        var lista = new List<PgiaRiscoOutro>();
        if (itens == null || itens.Count == 0) return lista;

        var agora = DateTime.UtcNow;
        foreach (var item in itens)
        {
            var descricao = (item.DescricaoRisco ?? string.Empty).Trim();
            var mitigacao = (item.AcaoMitigacao ?? string.Empty).Trim();
            var nome = (item.ResponsavelNome ?? string.Empty).Trim();
            var email = (item.ResponsavelEmail ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(descricao) || string.IsNullOrWhiteSpace(mitigacao))
                throw new ApiException(ErrorCode.PgiaDominioInvalido,
                    "Em cada risco declarado, a descrição do risco e a ação de mitigação são obrigatórias.");

            if (string.IsNullOrWhiteSpace(nome))
                throw new ApiException(ErrorCode.PgiaDominioInvalido,
                    "Informe o responsável pela ação de mitigação de cada risco declarado.");

            if (!PgiaValidacoes.EmailValido(email))
                throw new ApiException(ErrorCode.PgiaDominioInvalido,
                    $"E-mail do responsável pelo risco declarado inválido: {email}");

            if (!PgiaDominios.EscalaCgdf.Probabilidade.Permitidos.Contains(item.Probabilidade))
                throw new ApiException(ErrorCode.PgiaDominioInvalido,
                    $"Probabilidade inválida para risco declarado: {item.Probabilidade}. " +
                    $"Use {string.Join(", ", PgiaDominios.EscalaCgdf.Probabilidade.Permitidos)}.");

            if (!PgiaDominios.EscalaCgdf.Consequencia.Permitidos.Contains(item.Consequencia))
                throw new ApiException(ErrorCode.PgiaDominioInvalido,
                    $"Consequência inválida para risco declarado: {item.Consequencia}. " +
                    $"Use {string.Join(", ", PgiaDominios.EscalaCgdf.Consequencia.Permitidos)}.");

            lista.Add(new PgiaRiscoOutro
            {
                DescricaoRisco = descricao,
                AcaoMitigacao = mitigacao,
                ResponsavelNome = nome,
                ResponsavelEmail = email,
                Probabilidade = item.Probabilidade,
                Consequencia = item.Consequencia,
                CriadoEm = agora,
                CriadoPor = userEmail
            });
        }

        return lista;
    }

    /// <summary>
    /// Forma do jsonb de respostas_checklist. Linhas antigas, sem os "nenhuma",
    /// desserializam com os bools em false (compatível para trás).
    /// </summary>
    private sealed class ChecklistPersistido
    {
        [JsonPropertyName("q15")] public List<string> Q15 { get; set; } = new();
        [JsonPropertyName("q16")] public List<string> Q16 { get; set; } = new();
        [JsonPropertyName("q17")] public List<string> Q17 { get; set; } = new();
        [JsonPropertyName("q15_nenhuma")] public bool Q15Nenhuma { get; set; }
        [JsonPropertyName("q16_nenhuma")] public bool Q16Nenhuma { get; set; }
        [JsonPropertyName("q17_nenhuma")] public bool Q17Nenhuma { get; set; }
    }

    /// <summary>
    /// Descarta repetições e devolve os incisos marcados na ordem do artigo
    /// (o enquadramento aponta o primeiro inciso do artigo, não o primeiro digitado).
    /// </summary>
    private static List<string> NormalizarIncisos(List<string>? marcados, string[] validos, int artigo)
    {
        if (marcados == null || marcados.Count == 0) return new List<string>();

        foreach (var inciso in marcados)
        {
            if (!validos.Contains(inciso))
                throw new ApiException(ErrorCode.PgiaChecklistInvalido,
                    $"Inciso inválido no checklist do art. {artigo}: {inciso}");
        }

        return validos.Where(marcados.Contains).ToList();
    }

    // ── Validações do inventário ──────────────────────────────────────────────

    private static void ValidarCamposObrigatorios(PgiaSistemaBaseDTO dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Denominacao) || string.IsNullOrWhiteSpace(dto.Finalidade))
            throw new ApiException(ErrorCode.PgiaDominioInvalido,
                "Denominação e finalidade do sistema são obrigatórias.");
    }

    private static void ValidarDominios(PgiaSistemaBaseDTO dto)
    {
        if (!PgiaDominios.OrigemRegistro.Todos.Contains(dto.OrigemRegistro))
            throw new ApiException(ErrorCode.PgiaDominioInvalido, $"Origem do registro inválida: {dto.OrigemRegistro}");

        if (!PgiaDominios.TipoSistema.Todos.Contains(dto.TipoSistema))
            throw new ApiException(ErrorCode.PgiaDominioInvalido, $"Tipo de sistema inválido: {dto.TipoSistema}");

        if (!PgiaDominios.Tecnologia.Todos.Contains(dto.Tecnologia))
            throw new ApiException(ErrorCode.PgiaDominioInvalido, $"Tecnologia inválida: {dto.Tecnologia}");

        if (!PgiaDominios.StatusCicloVida.Todos.Contains(dto.StatusCicloVida))
            throw new ApiException(ErrorCode.PgiaDominioInvalido, $"Fase do ciclo de vida inválida: {dto.StatusCicloVida}");

        if (!PgiaDominios.EscopoDados.Todos.Contains(dto.EscopoDados))
            throw new ApiException(ErrorCode.PgiaDominioInvalido, $"Natureza dos dados tratados inválida: {dto.EscopoDados}");
    }

    /// <summary>
    /// Condicionais do formulário (seções 2.1 a 2.3): exige o que o decreto exige e
    /// anula o que ficou fora do caso condicionado, para não guardar dado órfão.
    /// </summary>
    private static void ValidarEAjustarCondicionais(PgiaSistemaBaseDTO dto, string? resultadoRisco)
    {
        // Origem "Outros" só se sustenta com o descritivo livre
        if (dto.OrigemRegistro == PgiaDominios.OrigemRegistro.Outros)
        {
            if (string.IsNullOrWhiteSpace(dto.OrigemRegistroDescricao))
                throw new ApiException(ErrorCode.PgiaDominioInvalido, "Descreva a origem do registro.");
        }
        else
        {
            dto.OrigemRegistroDescricao = null;
        }

        // Sistema em uso precisa da data de implantação (art. 35, IV)
        if (PgiaDominios.StatusCicloVida.EmUso.Contains(dto.StatusCicloVida) && dto.DataImplantacao == null)
            throw new ApiException(ErrorCode.PgiaDominioInvalido,
                "Informe a data de implantação para sistema em uso (Implantado ou Monitoramento).");

        // Afeta cidadão: natureza das decisões e efeitos vão ao Registro Público (art. 24, III)
        if (dto.AfetaCidadao)
        {
            if (string.IsNullOrWhiteSpace(dto.NaturezaDecisoes) || string.IsNullOrWhiteSpace(dto.EfeitosCidadao))
                throw new ApiException(ErrorCode.PgiaDominioInvalido,
                    "Sistema que afeta diretamente cidadãos exige a natureza das decisões e os efeitos sobre o cidadão (art. 24, III).");
        }
        else
        {
            dto.NaturezaDecisoes = null;
            dto.EfeitosCidadao = null;
        }

        // Bloco LGPD, aberto pela natureza dos dados (art. 19)
        if (PgiaDominios.EscopoDados.ComDadosPessoais.Contains(dto.EscopoDados))
        {
            if (string.IsNullOrWhiteSpace(dto.BaseLegalLgpd) || !PgiaDominios.BaseLegalLgpd.Todos.Contains(dto.BaseLegalLgpd))
                throw new ApiException(ErrorCode.PgiaDominioInvalido,
                    $"Base legal do tratamento inválida ou não informada: {dto.BaseLegalLgpd}");

            if (string.IsNullOrWhiteSpace(dto.CategoriasDadosPessoais)
                || string.IsNullOrWhiteSpace(dto.FinalidadeTratamentoDados)
                || string.IsNullOrWhiteSpace(dto.MedidasSeguranca))
                throw new ApiException(ErrorCode.PgiaDominioInvalido,
                    "Tratamento de dados pessoais exige categorias de dados, finalidade do tratamento e medidas de segurança (art. 19).");
        }
        else
        {
            dto.BaseLegalLgpd = null;
            dto.CategoriasDadosPessoais = null;
            dto.FinalidadeTratamentoDados = null;
            dto.MedidasSeguranca = null;
        }

        // Solução contratada não pode duplicar solução corporativa (art. 27)
        if (dto.TipoSistema == PgiaDominios.TipoSistema.Contratado)
        {
            if (string.IsNullOrWhiteSpace(dto.JustificativaNaoRedundancia))
                throw new ApiException(ErrorCode.PgiaDominioInvalido,
                    "Sistema contratado exige a justificativa de não redundância (art. 27).");
        }
        else
        {
            dto.JustificativaNaoRedundancia = null;
        }

        // Supervisão humana, aviso de interação e identificador de autenticidade são
        // coletados de todo sistema (o formulário os exibe de forma neutra, sem revelar
        // o grupo de risco); abaixo só a obrigatoriedade de quem não pode ficar sem eles.

        // Alto Risco: supervisão humana descrita (arts. 5º, II, 13, V e 23, II)
        if (resultadoRisco == PgiaDominios.ResultadoRisco.Alto
            && string.IsNullOrWhiteSpace(dto.SupervisaoHumanaDescricao))
            throw new ApiException(ErrorCode.PgiaClassificacaoInvalida,
                "Sistema de Alto Risco exige a descrição da supervisão humana (arts. 5º, II e 23, II).");

        // Risco Moderado: aviso de que o cidadão interage com IA (art. 17, § 1º)
        if (resultadoRisco == PgiaDominios.ResultadoRisco.Moderado && dto.AvisoInteracaoIa == null)
            throw new ApiException(ErrorCode.PgiaClassificacaoInvalida,
                "Sistema de Risco Moderado exige informar se há aviso de interação com IA (art. 17, § 1º).");
    }

    private static void AplicarDadosInventario(PgiaSistemaIa sistema, PgiaSistemaBaseDTO dto, string denominacao)
    {
        sistema.Denominacao = denominacao;
        sistema.Finalidade = dto.Finalidade.Trim();
        sistema.OrigemRegistro = dto.OrigemRegistro;
        sistema.OrigemRegistroDescricao = dto.OrigemRegistroDescricao?.Trim();
        sistema.TipoSistema = dto.TipoSistema;
        sistema.Tecnologia = dto.Tecnologia;
        sistema.StatusCicloVida = dto.StatusCicloVida;
        sistema.DataImplantacao = dto.DataImplantacao;
        sistema.EscopoDados = dto.EscopoDados;
        sistema.AfetaCidadao = dto.AfetaCidadao;
        sistema.NaturezaDecisoes = dto.NaturezaDecisoes?.Trim();
        sistema.EfeitosCidadao = dto.EfeitosCidadao?.Trim();
        sistema.SupervisaoHumanaDescricao = dto.SupervisaoHumanaDescricao?.Trim();
        sistema.AvisoInteracaoIa = dto.AvisoInteracaoIa;
        sistema.IdentificadorAutenticidade = dto.IdentificadorAutenticidade;
        sistema.BaseLegalLgpd = dto.BaseLegalLgpd;
        sistema.CategoriasDadosPessoais = dto.CategoriasDadosPessoais?.Trim();
        sistema.FinalidadeTratamentoDados = dto.FinalidadeTratamentoDados?.Trim();
        sistema.MedidasSeguranca = dto.MedidasSeguranca?.Trim();
        sistema.InteroperavelPadroesSgdi = dto.InteroperavelPadroesSgdi;
        sistema.JustificativaNaoRedundancia = dto.JustificativaNaoRedundancia?.Trim();
        sistema.DataAnaliseSgtic = dto.DataAnaliseSgtic;
        sistema.ParecerSgtic = dto.ParecerSgtic?.Trim();
        sistema.ProcessoSei = string.IsNullOrWhiteSpace(dto.ProcessoSei) ? null : dto.ProcessoSei.Trim();
        sistema.ComunicadoSgdiEm = dto.ComunicadoSgdiEm;
    }

    // ── Mapeamentos ───────────────────────────────────────────────────────────

    private static PgiaSistemaResponse MapSistema(PgiaSistemaIa s)
    {
        return new PgiaSistemaResponse
        {
            Id = s.Id,
            OrgaoId = s.OrgaoId,
            OrgaoSigla = s.Orgao?.Sigla ?? string.Empty,
            Denominacao = s.Denominacao,
            Finalidade = s.Finalidade,
            OrigemRegistro = s.OrigemRegistro,
            OrigemRegistroDescricao = s.OrigemRegistroDescricao,
            TipoSistema = s.TipoSistema,
            Tecnologia = s.Tecnologia,
            StatusCicloVida = s.StatusCicloVida,
            DataImplantacao = s.DataImplantacao,
            DataDescontinuacao = s.DataDescontinuacao,
            EscopoDados = s.EscopoDados,
            AfetaCidadao = s.AfetaCidadao,
            NaturezaDecisoes = s.NaturezaDecisoes,
            EfeitosCidadao = s.EfeitosCidadao,
            ClassificacaoRiscoAtual = s.ClassificacaoRiscoAtual,
            EnquadramentoLegal = s.EnquadramentoLegal,
            SupervisaoHumanaDescricao = s.SupervisaoHumanaDescricao,
            AvisoInteracaoIa = s.AvisoInteracaoIa,
            IdentificadorAutenticidade = s.IdentificadorAutenticidade,
            BaseLegalLgpd = s.BaseLegalLgpd,
            CategoriasDadosPessoais = s.CategoriasDadosPessoais,
            FinalidadeTratamentoDados = s.FinalidadeTratamentoDados,
            MedidasSeguranca = s.MedidasSeguranca,
            InteroperavelPadroesSgdi = s.InteroperavelPadroesSgdi,
            JustificativaNaoRedundancia = s.JustificativaNaoRedundancia,
            DataAnaliseSgtic = s.DataAnaliseSgtic,
            ParecerSgtic = s.ParecerSgtic,
            ResponsavelIaId = s.ResponsavelIaId,
            ResponsavelNome = s.Responsavel?.Agente?.Nome ?? string.Empty,
            ProcessoSei = s.ProcessoSei,
            ComunicadoSgdiEm = s.ComunicadoSgdiEm,
            SituacaoHomologacao = s.SituacaoHomologacao,
            AvaliacaoParecer = s.AvaliacaoParecer,
            AvaliadoPorNome = s.AvaliadoPorUser?.Nome,
            AvaliadoEm = s.AvaliadoEm,
            PublicadoRegistroPublico = s.PublicadoRegistroPublico,
            DataPublicacaoRegistro = s.DataPublicacaoRegistro,
            CriadoEm = s.CriadoEm
        };
    }

    private static PgiaClassificacaoResponse MapClassificacao(PgiaClassificacaoRisco c, bool incluirPontuacao)
    {
        return new PgiaClassificacaoResponse
        {
            Id = c.Id,
            SistemaIaId = c.SistemaIaId,
            DataClassificacao = c.DataClassificacao,
            Motivo = c.Motivo,
            Checklist = DesserializarChecklist(c.RespostasChecklist),
            OutrosRiscos = (c.OutrosRiscos ?? new List<PgiaRiscoOutro>())
                .OrderBy(r => r.Id).Select(MapRiscoOutro).ToList(),
            Resultado = c.Resultado,
            // O órgão preenche o checklist mas não enxerga a pontuação
            Pontuacao = incluirPontuacao ? c.Pontuacao : null,
            EnquadramentoLegal = c.EnquadramentoLegal,
            Justificativa = c.Justificativa,
            ClassificadoPorNome = c.ClassificadoPorUser?.Nome ?? string.Empty,
            CriadoEm = c.CriadoEm
        };
    }

    private static PgiaChecklistDTO DesserializarChecklist(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new PgiaChecklistDTO();

        var persistido = JsonSerializer.Deserialize<ChecklistPersistido>(json);
        if (persistido == null) return new PgiaChecklistDTO();

        return new PgiaChecklistDTO
        {
            Q15 = persistido.Q15,
            Q16 = persistido.Q16,
            Q17 = persistido.Q17,
            Q15Nenhuma = persistido.Q15Nenhuma,
            Q16Nenhuma = persistido.Q16Nenhuma,
            Q17Nenhuma = persistido.Q17Nenhuma
        };
    }

    private static PgiaRiscoOutroResponse MapRiscoOutro(PgiaRiscoOutro r) => new()
    {
        Id = r.Id,
        DescricaoRisco = r.DescricaoRisco,
        AcaoMitigacao = r.AcaoMitigacao,
        ResponsavelNome = r.ResponsavelNome,
        ResponsavelEmail = r.ResponsavelEmail,
        Probabilidade = r.Probabilidade,
        Consequencia = r.Consequencia
    };

    private static PgiaDocumentoResponse MapDocumento(PgiaDocumento d)
    {
        return new PgiaDocumentoResponse
        {
            Id = d.Id,
            Tipo = d.Tipo,
            OrgaoId = d.OrgaoId,
            SistemaIaId = d.SistemaIaId,
            ProcessoSei = d.ProcessoSei,
            NomeArquivo = d.NomeArquivo,
            UrlStorage = d.UrlStorage,
            DataEnvio = d.DataEnvio,
            EnviadoPorNome = d.EnviadoPorUser?.Nome ?? string.Empty
        };
    }
}
