using api.Common;
using api.Pgia;
using app.Auth;
using demanda_service.Helpers;
using Microsoft.EntityFrameworkCore;
using Models.Pgia;
using Repositorio.Interface;
using service.Interface;

namespace service.Pgia;

/// <summary>
/// Operação contínua do PGIA: incidentes graves (arts. 13, II e 30), não conformidades
/// (arts. 8º, X e 9º, II), trilhas ProCapIA/DF (arts. 28 e 29) e registro de uso de IA
/// (art. 13, IV e V).
/// </summary>
public class PgiaOperacaoService : IPgiaOperacaoService
{
    private readonly IPgiaOperacaoRepositorio _repositorio;

    public PgiaOperacaoService(IPgiaOperacaoRepositorio repositorio)
    {
        _repositorio = repositorio;
    }

    // ── Incidentes graves ─────────────────────────────────────────────────────

    public async Task<PgiaIncidente?> GetIncidenteEntidadeAsync(long id)
    {
        return await _repositorio.GetIncidenteByIdAsync(id);
    }

    public async Task<long?> GetSistemaOrgaoAsync(long sistemaId)
    {
        var sistema = await _repositorio.GetSistemaByIdAsync(sistemaId);
        return sistema?.OrgaoId;
    }

    public async Task<PgiaIncidenteResponse> CriarAvisoAsync(PgiaIncidenteAvisoDTO dto, PgiaUserContext ctx)
    {
        // O aviso do art. 13, II é do agente para o Responsável de IA do seu próprio órgão
        var sistema = await ResolverSistemaDoUsuarioAsync(dto.SistemaIaId, ctx);

        if (string.IsNullOrWhiteSpace(dto.Descricao))
            throw new ApiException(ErrorCode.PgiaDominioInvalido, "Descreva o que aconteceu.");

        if (!string.IsNullOrWhiteSpace(dto.Hipotese)
            && !PgiaDominios.HipoteseIncidente.Todos.Contains(dto.Hipotese))
            throw new ApiException(ErrorCode.PgiaDominioInvalido, $"Hipótese do art. 30 inválida: {dto.Hipotese}");

        var agora = DateTime.UtcNow;
        var incidente = new PgiaIncidente
        {
            SistemaIaId = sistema.Id,
            Sistema = sistema,
            OrgaoId = sistema.OrgaoId,
            Hipotese = string.IsNullOrWhiteSpace(dto.Hipotese) ? null : dto.Hipotese,
            Descricao = dto.Descricao.Trim(),
            DataOcorrencia = dto.DataOcorrencia ?? agora,
            DataDeteccao = dto.DataDeteccao ?? agora,
            // O aviso é a própria notificação ao Responsável de IA
            NotificadoResponsavelEm = agora,
            ComunicadoPor = ctx.UserId,
            // A comunicação formal à SGDI e o SEI vêm depois, pelo Responsável
            DataComunicacaoSgdi = null,
            ProcessoSei = null,
            StatusApuracao = PgiaDominios.StatusApuracao.Recebida,
            SistemaSuspenso = false,
            CriadoEm = agora,
            CriadoPor = ctx.Email
        };

        _repositorio.AddIncidente(incidente);
        await _repositorio.SaveChangesAsync();

        var salvo = await _repositorio.GetIncidenteByIdAsync(incidente.Id);
        return MapIncidente(salvo ?? incidente);
    }

    public async Task<PgiaIncidenteResponse> CriarIncidenteFormalAsync(PgiaIncidenteCreateDTO dto, PgiaUserContext ctx)
    {
        var sistema = await _repositorio.GetSistemaByIdAsync(dto.SistemaIaId)
            ?? throw new ApiException(ErrorCode.PgiaSistemaNaoEncontrado);

        ValidarComunicacao(dto.Hipotese, dto.ProcessoSei, dto.DataComunicacaoSgdi);

        if (string.IsNullOrWhiteSpace(dto.Descricao))
            throw new ApiException(ErrorCode.PgiaDominioInvalido, "Descreva o que aconteceu.");

        if (dto.DataOcorrencia == default || dto.DataDeteccao == default || dto.NotificadoResponsavelEm == default)
            throw new ApiException(ErrorCode.PgiaDominioInvalido,
                "Informe as datas de ocorrência, detecção e notificação ao Responsável de IA.");

        var agora = DateTime.UtcNow;
        var incidente = new PgiaIncidente
        {
            SistemaIaId = sistema.Id,
            Sistema = sistema,
            OrgaoId = sistema.OrgaoId,
            Hipotese = dto.Hipotese,
            Descricao = dto.Descricao.Trim(),
            DataOcorrencia = dto.DataOcorrencia,
            DataDeteccao = dto.DataDeteccao,
            NotificadoResponsavelEm = dto.NotificadoResponsavelEm,
            ComunicadoPor = ctx.UserId,
            DataComunicacaoSgdi = dto.DataComunicacaoSgdi,
            ProcessoSei = dto.ProcessoSei.Trim(),
            StatusApuracao = PgiaDominios.StatusApuracao.Recebida,
            SistemaSuspenso = false,
            MedidasAdotadas = string.IsNullOrWhiteSpace(dto.MedidasAdotadas) ? null : dto.MedidasAdotadas.Trim(),
            CriadoEm = agora,
            CriadoPor = ctx.Email
        };

        _repositorio.AddIncidente(incidente);
        await _repositorio.SaveChangesAsync();

        var salvo = await _repositorio.GetIncidenteByIdAsync(incidente.Id);
        return MapIncidente(salvo ?? incidente);
    }

    public async Task<PgiaIncidenteResponse> ComunicarAsync(long id, PgiaIncidenteComunicarDTO dto, PgiaUserContext ctx)
    {
        var incidente = await _repositorio.GetIncidenteByIdAsync(id)
            ?? throw new ApiException(ErrorCode.PgiaIncidenteNaoEncontrado);

        // A comunicação à SGDI é ato único: reabri-la apagaria a data que conta o prazo do art. 30
        if (incidente.DataComunicacaoSgdi != null)
            throw new ApiException(ErrorCode.PgiaDominioInvalido, "Este incidente já foi comunicado à SGDI.");

        ValidarComunicacao(dto.Hipotese, dto.ProcessoSei, dto.DataComunicacaoSgdi);

        incidente.Hipotese = dto.Hipotese;
        incidente.ProcessoSei = dto.ProcessoSei.Trim();
        incidente.DataComunicacaoSgdi = dto.DataComunicacaoSgdi;
        // O aviso do agente traz datas aproximadas; a apuração interna do órgão as corrige
        if (dto.DataOcorrencia != null && dto.DataOcorrencia != default(DateTime))
            incidente.DataOcorrencia = dto.DataOcorrencia.Value;
        if (dto.DataDeteccao != null && dto.DataDeteccao != default(DateTime))
            incidente.DataDeteccao = dto.DataDeteccao.Value;
        if (!string.IsNullOrWhiteSpace(dto.MedidasAdotadas))
            incidente.MedidasAdotadas = dto.MedidasAdotadas.Trim();
        incidente.AlteradoEm = DateTime.UtcNow;
        incidente.AlteradoPor = ctx.Email;

        await _repositorio.SaveChangesAsync();
        return MapIncidente(incidente);
    }

    public async Task<PgiaIncidenteResponse> AtualizarMedidasAsync(long id, PgiaIncidenteMedidasDTO dto, PgiaUserContext ctx)
    {
        var incidente = await _repositorio.GetIncidenteByIdAsync(id)
            ?? throw new ApiException(ErrorCode.PgiaIncidenteNaoEncontrado);

        incidente.MedidasAdotadas = string.IsNullOrWhiteSpace(dto.MedidasAdotadas) ? null : dto.MedidasAdotadas.Trim();
        incidente.AlteradoEm = DateTime.UtcNow;
        incidente.AlteradoPor = ctx.Email;

        await _repositorio.SaveChangesAsync();
        return MapIncidente(incidente);
    }

    public async Task<PgiaIncidenteResponse> ApurarAsync(long id, PgiaIncidenteApuracaoDTO dto, PgiaUserContext ctx)
    {
        var incidente = await _repositorio.GetIncidenteByIdAsync(id)
            ?? throw new ApiException(ErrorCode.PgiaIncidenteNaoEncontrado);

        // A apuração da SGDI pressupõe a comunicação formal do órgão (art. 30, § único)
        if (incidente.DataComunicacaoSgdi == null)
            throw new ApiException(ErrorCode.PgiaIncidenteNaoComunicado,
                "O incidente ainda não foi comunicado à SGDI pelo órgão.");

        if (!PgiaDominios.StatusApuracao.Todos.Contains(dto.StatusApuracao))
            throw new ApiException(ErrorCode.PgiaDominioInvalido,
                $"Situação da apuração inválida: {dto.StatusApuracao}");

        if (dto.SistemaSuspenso && dto.DataSuspensao == null)
            throw new ApiException(ErrorCode.PgiaDominioInvalido,
                "Informe a data da suspensão do sistema.");

        incidente.StatusApuracao = dto.StatusApuracao;
        incidente.RecomendacoesSgdi = string.IsNullOrWhiteSpace(dto.RecomendacoesSgdi) ? null : dto.RecomendacoesSgdi.Trim();
        incidente.PropostaCgtic = dto.PropostaCgtic;
        incidente.EncaminhadoOrgaoCompetenteEm = dto.EncaminhadoOrgaoCompetenteEm;
        incidente.SistemaSuspenso = dto.SistemaSuspenso;
        incidente.DataSuspensao = dto.SistemaSuspenso ? dto.DataSuspensao : null;
        incidente.AlteradoEm = DateTime.UtcNow;
        incidente.AlteradoPor = ctx.Email;

        await _repositorio.SaveChangesAsync();
        return MapIncidente(incidente);
    }

    public async Task<List<PgiaIncidenteResponse>> ListarIncidentesPorOrgaoAsync(long orgaoId)
    {
        var lista = await _repositorio.ListarIncidentesPorOrgaoAsync(orgaoId);
        return lista.Select(MapIncidente).ToList();
    }

    public async Task<List<PgiaIncidenteResponse>> ListarComunicadosAsync(string? status)
    {
        if (!string.IsNullOrWhiteSpace(status) && !PgiaDominios.StatusApuracao.Todos.Contains(status))
            throw new ApiException(ErrorCode.PgiaDominioInvalido, $"Situação da apuração inválida: {status}");

        var lista = await _repositorio.ListarIncidentesComunicadosAsync(status);
        return lista.Select(MapIncidente).ToList();
    }

    /// <summary>Hipótese, processo SEI e data são o mínimo da comunicação formal (art. 30).</summary>
    private static void ValidarComunicacao(string hipotese, string processoSei, DateTime dataComunicacao)
    {
        if (!PgiaDominios.HipoteseIncidente.Todos.Contains(hipotese))
            throw new ApiException(ErrorCode.PgiaDominioInvalido,
                $"Informe a hipótese do art. 30 (I a V). Valor recebido: {hipotese}");

        if (string.IsNullOrWhiteSpace(processoSei))
            throw new ApiException(ErrorCode.PgiaDominioInvalido,
                "Informe o processo SEI da comunicação à SGDI.");

        if (dataComunicacao == default)
            throw new ApiException(ErrorCode.PgiaDominioInvalido, "Informe a data da comunicação à SGDI.");
    }

    /// <summary>
    /// O agente sem papel PGIA só alcança sistemas do próprio órgão (art. 13).
    /// </summary>
    private async Task<PgiaSistemaIa> ResolverSistemaDoUsuarioAsync(long sistemaId, PgiaUserContext ctx)
    {
        if (ctx.OrgaoId == null)
            throw new ApiException(ErrorCode.PgiaSemOrgaoResolvido,
                "A sua unidade ainda não está vinculada a um órgão do PGIA.");

        var sistema = await _repositorio.GetSistemaByIdAsync(sistemaId)
            ?? throw new ApiException(ErrorCode.PgiaSistemaNaoEncontrado);

        if (sistema.OrgaoId != ctx.OrgaoId)
            throw new ApiException(ErrorCode.PgiaSistemaNaoEncontrado,
                "O sistema informado não pertence ao seu órgão.");

        return sistema;
    }

    // ── Não conformidades ─────────────────────────────────────────────────────

    public async Task<PgiaNaoConformidade?> GetNaoConformidadeEntidadeAsync(long id)
    {
        return await _repositorio.GetNaoConformidadeByIdAsync(id);
    }

    public async Task<PgiaNaoConformidadeResponse> CriarNaoConformidadeAsync(
        PgiaNaoConformidadeCreateDTO dto, PgiaUserContext ctx)
    {
        if (string.IsNullOrWhiteSpace(dto.Descricao))
            throw new ApiException(ErrorCode.PgiaDominioInvalido, "Descreva a não conformidade.");

        long orgaoId;
        string origem;

        if (ctx.PapelEfetivo == PapeisPgia.Orgao)
        {
            // O SGTIC registra os próprios achados: origem e órgão não são escolha do corpo
            if (ctx.OrgaoId == null)
                throw new ApiException(ErrorCode.PgiaSemOrgaoResolvido,
                    "A sua unidade ainda não está vinculada a um órgão do PGIA.");
            orgaoId = ctx.OrgaoId.Value;
            origem = PgiaDominios.OrigemNaoConformidade.Sgtic;
        }
        else
        {
            // SGDI e admin registram descumprimento de qualquer órgão, e o órgão é obrigatório
            if (dto.OrgaoId == null)
                throw new ApiException(ErrorCode.PgiaDominioInvalido,
                    "Informe o órgão da não conformidade.");

            _ = await _repositorio.GetOrgaoByIdAsync(dto.OrgaoId.Value)
                ?? throw new ApiException(ErrorCode.PgiaOrgaoNaoEncontrado);

            orgaoId = dto.OrgaoId.Value;

            if (ctx.PapelEfetivo == PapeisPgia.Sgdi)
            {
                // O achado da SGDI é da supervisão por definição: a origem não é escolha do corpo
                origem = PgiaDominios.OrigemNaoConformidade.Sgdi;
            }
            else
            {
                // Admin registra em nome de qualquer instância, então a origem vem do corpo
                origem = string.IsNullOrWhiteSpace(dto.Origem)
                    ? PgiaDominios.OrigemNaoConformidade.Sgdi
                    : dto.Origem;

                if (!PgiaDominios.OrigemNaoConformidade.Todos.Contains(origem))
                    throw new ApiException(ErrorCode.PgiaDominioInvalido, $"Origem da não conformidade inválida: {origem}");
            }
        }

        var naoConformidade = new PgiaNaoConformidade
        {
            OrgaoId = orgaoId,
            Descricao = dto.Descricao.Trim(),
            Origem = origem,
            // Dia civil de Brasília, como o DEFAULT CURRENT_DATE do schema
            DataRegistro = DateOnly.FromDateTime(DateTimeHelper.TodayBrasilia()),
            ReportadaSgdiEm = dto.ReportadaSgdiEm,
            Situacao = PgiaDominios.SituacaoNaoConformidade.Registrada,
            CriadoEm = DateTime.UtcNow,
            CriadoPor = ctx.Email
        };

        _repositorio.AddNaoConformidade(naoConformidade);
        await _repositorio.SaveChangesAsync();

        var salva = await _repositorio.GetNaoConformidadeByIdAsync(naoConformidade.Id);
        return MapNaoConformidade(salva ?? naoConformidade);
    }

    public async Task<PgiaNaoConformidadeResponse> AtualizarNaoConformidadeAsync(
        long id, PgiaNaoConformidadeUpdateDTO dto, PgiaUserContext ctx)
    {
        var naoConformidade = await _repositorio.GetNaoConformidadeByIdAsync(id)
            ?? throw new ApiException(ErrorCode.PgiaNaoConformidadeNaoEncontrada);

        // O achado da supervisão não é editável pelo supervisionado: quem o registrou
        // é quem o encerra. O órgão segue tratando as de origem SGTIC.
        if (naoConformidade.Origem == PgiaDominios.OrigemNaoConformidade.Sgdi
            && ctx.PapelEfetivo == PapeisPgia.Orgao)
            throw new ApiException(ErrorCode.PgiaDominioInvalido,
                "Não conformidades da supervisão da SGDI são tratadas pelo órgão central.");

        if (string.IsNullOrWhiteSpace(dto.Descricao))
            throw new ApiException(ErrorCode.PgiaDominioInvalido, "Descreva a não conformidade.");

        if (!PgiaDominios.SituacaoNaoConformidade.Todos.Contains(dto.Situacao))
            throw new ApiException(ErrorCode.PgiaDominioInvalido, $"Situação inválida: {dto.Situacao}");

        naoConformidade.Descricao = dto.Descricao.Trim();
        naoConformidade.Situacao = dto.Situacao;
        naoConformidade.ReportadaSgdiEm = dto.ReportadaSgdiEm;
        naoConformidade.ComunicadaControleInternoEm = dto.ComunicadaControleInternoEm;
        naoConformidade.AlteradoEm = DateTime.UtcNow;
        naoConformidade.AlteradoPor = ctx.Email;

        await _repositorio.SaveChangesAsync();
        return MapNaoConformidade(naoConformidade);
    }

    /// <summary>
    /// Sem paginação por decisão registrada: descumprimentos são pontuais e o
    /// painel precisa de todos à vista.
    /// </summary>
    public async Task<List<PgiaNaoConformidadeResponse>> ListarNaoConformidadesPorOrgaoAsync(long orgaoId)
    {
        var lista = await _repositorio.ListarNaoConformidadesPorOrgaoAsync(orgaoId);
        return lista.Select(MapNaoConformidade).ToList();
    }

    public async Task<List<PgiaNaoConformidadeResponse>> ListarNaoConformidadesAsync()
    {
        var lista = await _repositorio.ListarNaoConformidadesAsync();
        return lista.Select(MapNaoConformidade).ToList();
    }

    // ── Capacitação ───────────────────────────────────────────────────────────

    public async Task<PgiaCapacitacao?> GetCapacitacaoEntidadeAsync(long id)
    {
        return await _repositorio.GetCapacitacaoByIdAsync(id);
    }

    public async Task<PgiaCapacitacaoResponse> CriarCapacitacaoAsync(
        long orgaoId, PgiaCapacitacaoCreateDTO dto, PgiaUserContext ctx)
    {
        var orgao = await _repositorio.GetOrgaoByIdAsync(orgaoId)
            ?? throw new ApiException(ErrorCode.PgiaOrgaoNaoEncontrado);

        if (!PgiaDominios.TrilhaCapacitacao.Todos.Contains(dto.Trilha))
            throw new ApiException(ErrorCode.PgiaDominioInvalido, $"Trilha inválida: {dto.Trilha}");

        ValidarStatusCapacitacao(dto.Status, dto.DataConclusao);

        if (orgao.UnidadeId == null)
            throw new ApiException(ErrorCode.PgiaOrgaoSemUnidadeVinculada,
                "O órgão ainda não está vinculado a uma unidade do SGDP.");

        var agente = await _repositorio.GetUserByIdAsync(dto.AgenteId)
            ?? throw new ApiException(ErrorCode.PgiaUsuarioNaoEncontrado);

        if (agente.Unidade?.id != orgao.UnidadeId)
            throw new ApiException(ErrorCode.PgiaAgenteDeOutroOrgao,
                "A pessoa não pertence à unidade vinculada ao órgão.");

        // O único do schema é (agente, trilha), sem o órgão: a trilha acompanha a pessoa
        // mesmo que ela mude de órgão, então a colisão vale entre órgãos.
        var existente = await _repositorio.GetCapacitacaoPorAgenteTrilhaAsync(dto.AgenteId, dto.Trilha);
        if (existente != null)
            throw new ApiException(ErrorCode.PgiaCapacitacaoJaExiste,
                "Esta pessoa já tem registro desta trilha de capacitação.");

        await ValidarCertificadoAsync(dto.CertificadoDocId, orgao.Id);

        var capacitacao = new PgiaCapacitacao
        {
            AgenteId = dto.AgenteId,
            Agente = agente,
            OrgaoId = orgao.Id,
            Trilha = dto.Trilha,
            Status = dto.Status,
            // Data de conclusão só faz sentido na trilha concluída (paridade com a edição)
            DataConclusao = dto.Status == PgiaDominios.StatusCapacitacao.Concluida ? dto.DataConclusao : null,
            PrevistaPlanoCapacitacao = dto.PrevistaPlanoCapacitacao,
            CertificadoDocId = dto.CertificadoDocId,
            CriadoEm = DateTime.UtcNow,
            CriadoPor = ctx.Email
        };

        _repositorio.AddCapacitacao(capacitacao);
        await _repositorio.SaveChangesAsync();
        return MapCapacitacao(capacitacao);
    }

    public async Task<PgiaCapacitacaoResponse> AtualizarCapacitacaoAsync(
        long id, PgiaCapacitacaoUpdateDTO dto, PgiaUserContext ctx)
    {
        var capacitacao = await _repositorio.GetCapacitacaoByIdAsync(id)
            ?? throw new ApiException(ErrorCode.PgiaCapacitacaoNaoEncontrada);

        ValidarStatusCapacitacao(dto.Status, dto.DataConclusao);
        await ValidarCertificadoAsync(dto.CertificadoDocId, capacitacao.OrgaoId);

        // Agente e trilha são a identidade do registro (índice único): trocar um deles
        // é criar outro registro, não editar este.
        capacitacao.Status = dto.Status;
        capacitacao.DataConclusao = dto.Status == PgiaDominios.StatusCapacitacao.Concluida ? dto.DataConclusao : null;
        capacitacao.PrevistaPlanoCapacitacao = dto.PrevistaPlanoCapacitacao;
        capacitacao.CertificadoDocId = dto.CertificadoDocId;
        capacitacao.AlteradoEm = DateTime.UtcNow;
        capacitacao.AlteradoPor = ctx.Email;

        await _repositorio.SaveChangesAsync();
        return MapCapacitacao(capacitacao);
    }

    /// <summary>
    /// Sem paginação por decisão registrada: o volume é limitado por natureza
    /// (pessoas do órgão × 4 trilhas do art. 29).
    /// </summary>
    public async Task<List<PgiaCapacitacaoResponse>> ListarCapacitacoesPorOrgaoAsync(long orgaoId)
    {
        var lista = await _repositorio.ListarCapacitacoesPorOrgaoAsync(orgaoId);
        return lista.Select(MapCapacitacao).ToList();
    }

    private static void ValidarStatusCapacitacao(string status, DateOnly? dataConclusao)
    {
        if (!PgiaDominios.StatusCapacitacao.Todos.Contains(status))
            throw new ApiException(ErrorCode.PgiaDominioInvalido, $"Situação da capacitação inválida: {status}");

        if (status == PgiaDominios.StatusCapacitacao.Concluida && dataConclusao == null)
            throw new ApiException(ErrorCode.PgiaDominioInvalido,
                "Trilha concluída exige a data de conclusão.");
    }

    /// <summary>
    /// Certificado de capacitação: além de existir, tem de ser documento DO ÓRGÃO
    /// da capacitação — mesmo escopo dos demais validadores de documento do módulo.
    /// </summary>
    private async Task ValidarCertificadoAsync(long? certificadoDocId, long orgaoId)
    {
        if (certificadoDocId == null) return;

        var documento = await _repositorio.GetDocumentoByIdAsync(certificadoDocId.Value)
            ?? throw new ApiException(ErrorCode.PgiaDocumentoNaoEncontrado);

        if (documento.OrgaoId != orgaoId)
            throw new ApiException(ErrorCode.PgiaDominioInvalido,
                "O certificado precisa ser um documento do próprio órgão.");
    }

    // ── Registro de uso de IA ─────────────────────────────────────────────────

    public async Task<PgiaRegistroUsoResponse> CriarUsoAsync(PgiaRegistroUsoCreateDTO dto, PgiaUserContext ctx)
    {
        if (ctx.OrgaoId == null)
            throw new ApiException(ErrorCode.PgiaSemOrgaoResolvido,
                "A sua unidade ainda não está vinculada a um órgão do PGIA.");

        // Espelha o CHECK ck_pgia_uso_fonte, que o provider InMemory dos testes não reproduz
        var fontes = (dto.SistemaIaId != null ? 1 : 0) + (dto.PlataformaId != null ? 1 : 0);
        if (fontes != 1)
            throw new ApiException(ErrorCode.PgiaRegistroUsoInvalido,
                "Escolha o sistema do órgão OU a plataforma homologada.");

        if (string.IsNullOrWhiteSpace(dto.ProdutoRef))
            throw new ApiException(ErrorCode.PgiaRegistroUsoInvalido,
                "Informe o produto, documento ou atividade em que a IA foi usada (art. 13, IV).");

        if (dto.DataUso == default)
            throw new ApiException(ErrorCode.PgiaRegistroUsoInvalido, "Informe a data do uso.");

        // Declaração do art. 13, V: sem ela não há registro
        if (!dto.RevisaoHumanaConfirmada)
            throw new ApiException(ErrorCode.PgiaRevisaoHumanaObrigatoria,
                "Para registrar, confirme a declaração de revisão do conteúdo (art. 13, V).");

        PgiaSistemaIa? sistema = null;
        PgiaPlataformaIaGenerativa? plataforma = null;

        if (dto.SistemaIaId != null)
        {
            sistema = await ResolverSistemaDoUsuarioAsync(dto.SistemaIaId.Value, ctx);
        }
        else
        {
            plataforma = await _repositorio.GetPlataformaByIdAsync(dto.PlataformaId!.Value)
                ?? throw new ApiException(ErrorCode.PgiaPlataformaNaoEncontrada);

            // Art. 20: só plataforma homologada pela SGDI ("Homologada" e "Homologada apta a...")
            if (!plataforma.StatusHomologacao.StartsWith("Homologada", StringComparison.Ordinal))
                throw new ApiException(ErrorCode.PgiaPlataformaNaoHomologada,
                    "Só é possível registrar uso de plataformas homologadas pela SGDI (art. 20).");
        }

        // Um lookup do agente basta para a resposta: nada de reler o histórico do usuário
        var agente = await _repositorio.GetUserByIdAsync(ctx.UserId);

        var uso = new PgiaRegistroUsoIa
        {
            AgenteId = ctx.UserId,
            Agente = agente,
            OrgaoId = ctx.OrgaoId.Value,
            SistemaIaId = sistema?.Id,
            Sistema = sistema,
            PlataformaId = plataforma?.Id,
            Plataforma = plataforma,
            ProdutoRef = dto.ProdutoRef.Trim(),
            ProcessoSei = string.IsNullOrWhiteSpace(dto.ProcessoSei) ? null : dto.ProcessoSei.Trim(),
            DataUso = dto.DataUso,
            RevisaoHumanaConfirmada = true,
            CriadoEm = DateTime.UtcNow,
            CriadoPor = ctx.Email
        };

        _repositorio.AddUso(uso);
        await _repositorio.SaveChangesAsync();

        return MapUso(uso);
    }

    public async Task<PagedResponse<PgiaRegistroUsoResponse>> ListarMeusUsosAsync(PgiaUserContext ctx, PagedRequest request)
    {
        return await PaginarUsosAsync(_repositorio.QueryUsosPorAgente(ctx.UserId), request);
    }

    public async Task<PagedResponse<PgiaRegistroUsoResponse>> ListarUsosPorOrgaoAsync(long orgaoId, PagedRequest request)
    {
        return await PaginarUsosAsync(_repositorio.QueryUsosPorOrgao(orgaoId), request);
    }

    /// <summary>
    /// O registro de uso cresce sem teto (um por produto entregue), por isso pagina.
    /// PagedRequest é compartilhado e não valida limites: saneia aqui, como na fase 1.
    /// </summary>
    private static async Task<PagedResponse<PgiaRegistroUsoResponse>> PaginarUsosAsync(
        IQueryable<PgiaRegistroUsoIa> query, PagedRequest request)
    {
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Max(1, request.PageSize);
        var skip = (page - 1) * pageSize;

        var totalItems = await query.CountAsync();
        var usos = await query
            .OrderByDescending(u => u.DataUso)
            .ThenByDescending(u => u.Id)
            .Skip(skip)
            .Take(pageSize)
            .ToListAsync();

        return new PagedResponse<PgiaRegistroUsoResponse>(
            usos.Select(MapUso).ToList(), totalItems, page, pageSize);
    }

    public async Task<PgiaUsoFontesResponse> ListarFontesDeUsoAsync(PgiaUserContext ctx)
    {
        var resposta = new PgiaUsoFontesResponse
        {
            OrgaoId = ctx.OrgaoId,
            // Plataformas homologadas valem para todo o GDF, mesmo sem órgão resolvido
            Plataformas = (await _repositorio.ListarPlataformasHomologadasAsync())
                .Select(p => new PgiaUsoFontePlataforma { Id = p.Id, Nome = p.Nome })
                .ToList()
        };

        if (ctx.OrgaoId == null) return resposta;

        var orgao = await _repositorio.GetOrgaoByIdAsync(ctx.OrgaoId.Value);
        resposta.OrgaoSigla = orgao?.Sigla;
        resposta.Sistemas = (await _repositorio.ListarSistemasDoOrgaoAsync(ctx.OrgaoId.Value))
            .Select(s => new PgiaUsoFonteSistema { Id = s.Id, Denominacao = s.Denominacao })
            .ToList();

        return resposta;
    }

    // ── Mapeamentos ───────────────────────────────────────────────────────────

    private static PgiaIncidenteResponse MapIncidente(PgiaIncidente i)
    {
        return new PgiaIncidenteResponse
        {
            Id = i.Id,
            SistemaIaId = i.SistemaIaId,
            SistemaDenominacao = i.Sistema?.Denominacao ?? string.Empty,
            OrgaoId = i.OrgaoId,
            OrgaoSigla = i.Orgao?.Sigla ?? i.Sistema?.Orgao?.Sigla ?? string.Empty,
            Hipotese = i.Hipotese,
            Descricao = i.Descricao,
            DataOcorrencia = i.DataOcorrencia,
            DataDeteccao = i.DataDeteccao,
            NotificadoResponsavelEm = i.NotificadoResponsavelEm,
            ComunicadoPorNome = i.ComunicadoPorUser?.Nome ?? string.Empty,
            DataComunicacaoSgdi = i.DataComunicacaoSgdi,
            ProcessoSei = i.ProcessoSei,
            StatusApuracao = i.StatusApuracao,
            RecomendacoesSgdi = i.RecomendacoesSgdi,
            PropostaCgtic = i.PropostaCgtic,
            EncaminhadoOrgaoCompetenteEm = i.EncaminhadoOrgaoCompetenteEm,
            SistemaSuspenso = i.SistemaSuspenso,
            DataSuspensao = i.DataSuspensao,
            MedidasAdotadas = i.MedidasAdotadas,
            CriadoEm = i.CriadoEm
        };
    }

    private static PgiaNaoConformidadeResponse MapNaoConformidade(PgiaNaoConformidade n)
    {
        return new PgiaNaoConformidadeResponse
        {
            Id = n.Id,
            OrgaoId = n.OrgaoId,
            OrgaoSigla = n.Orgao?.Sigla ?? string.Empty,
            Descricao = n.Descricao,
            Origem = n.Origem,
            DataRegistro = n.DataRegistro,
            ReportadaSgdiEm = n.ReportadaSgdiEm,
            Situacao = n.Situacao,
            ComunicadaControleInternoEm = n.ComunicadaControleInternoEm,
            CriadoEm = n.CriadoEm
        };
    }

    private static PgiaCapacitacaoResponse MapCapacitacao(PgiaCapacitacao c)
    {
        return new PgiaCapacitacaoResponse
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
        };
    }

    private static PgiaRegistroUsoResponse MapUso(PgiaRegistroUsoIa u)
    {
        return new PgiaRegistroUsoResponse
        {
            Id = u.Id,
            AgenteNome = u.Agente?.Nome ?? string.Empty,
            OrgaoId = u.OrgaoId,
            SistemaIaId = u.SistemaIaId,
            SistemaDenominacao = u.Sistema?.Denominacao,
            PlataformaId = u.PlataformaId,
            PlataformaNome = u.Plataforma?.Nome,
            ProdutoRef = u.ProdutoRef,
            ProcessoSei = u.ProcessoSei,
            DataUso = u.DataUso,
            RevisaoHumanaConfirmada = u.RevisaoHumanaConfirmada,
            CriadoEm = u.CriadoEm
        };
    }
}
