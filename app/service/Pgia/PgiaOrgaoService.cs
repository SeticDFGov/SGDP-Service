using api.Pgia;
using demanda_service.Helpers;
using Microsoft.EntityFrameworkCore;
using Models;
using Models.Pgia;
using Repositorio.Interface;
using service.Interface;

namespace service.Pgia;

public class PgiaOrgaoService : IPgiaOrgaoService
{
    private readonly IPgiaOrgaoRepositorio _orgaoRepositorio;
    private readonly IPgiaDesignacaoRepositorio _designacaoRepositorio;
    private readonly IPgiaPrazoRepositorio _prazoRepositorio;
    private readonly IPgiaPermissionService _permissionService;
    private readonly AppDbContext _context;

    public PgiaOrgaoService(
        IPgiaOrgaoRepositorio orgaoRepositorio,
        IPgiaDesignacaoRepositorio designacaoRepositorio,
        IPgiaPrazoRepositorio prazoRepositorio,
        IPgiaPermissionService permissionService,
        AppDbContext context)
    {
        _orgaoRepositorio = orgaoRepositorio;
        _designacaoRepositorio = designacaoRepositorio;
        _prazoRepositorio = prazoRepositorio;
        _permissionService = permissionService;
        _context = context;
    }

    public async Task<PgiaMeuOrgaoResponse> GetMeuOrgaoAsync(PgiaUserContext ctx)
    {
        if (ctx.OrgaoId == null)
            return new PgiaMeuOrgaoResponse();

        var orgao = await _orgaoRepositorio.GetByIdAsync(ctx.OrgaoId.Value);
        if (orgao == null)
            return new PgiaMeuOrgaoResponse();

        var responsaveis = await _designacaoRepositorio.ListarVigentesAsync<PgiaResponsavelIa>(orgao.Id);
        var encarregados = await _designacaoRepositorio.ListarVigentesAsync<PgiaEncarregadoDados>(orgao.Id);
        var curadores = await _designacaoRepositorio.ListarVigentesAsync<PgiaCuradorDados>(orgao.Id);

        const string titular = PgiaDominios.PosicaoDesignacao.Titular;
        const string suplente = PgiaDominios.PosicaoDesignacao.Suplente;
        static T? Na<T>(List<T> lista, string posicao) where T : class, IPgiaDesignacao =>
            lista.FirstOrDefault(d => d.Posicao == posicao);

        return new PgiaMeuOrgaoResponse
        {
            Orgao = PgiaAdminService.MapOrgao(orgao),
            ResponsavelIaVigente = Na(responsaveis, titular) is { } rt ? MapResponsavel(rt) : null,
            ResponsavelIaSuplenteVigente = Na(responsaveis, suplente) is { } rs ? MapResponsavel(rs) : null,
            EncarregadoDadosVigente = Na(encarregados, titular) is { } et ? MapDesignacao(et) : null,
            EncarregadoDadosSuplenteVigente = Na(encarregados, suplente) is { } es ? MapDesignacao(es) : null,
            CuradorDadosVigente = Na(curadores, titular) is { } ct ? MapDesignacao(ct) : null,
            CuradorDadosSuplenteVigente = Na(curadores, suplente) is { } cs ? MapDesignacao(cs) : null
        };
    }

    public async Task<List<PgiaOrgaoResponse>> ListarOrgaosAsync(PgiaUserContext ctx)
    {
        var orgaos = await _permissionService.GetFilteredOrgaosQuery(ctx)
            .OrderBy(o => o.Sigla)
            .ToListAsync();
        return orgaos.Select(PgiaAdminService.MapOrgao).ToList();
    }

    public async Task<PgiaOrgaoResponse> AtualizarDadosOrgaoAsync(long orgaoId, PgiaOrgaoDadosDTO dto, string userEmail)
    {
        var orgao = await _orgaoRepositorio.GetByIdAsync(orgaoId)
            ?? throw new ApiException(ErrorCode.PgiaOrgaoNaoEncontrado);

        if (string.IsNullOrWhiteSpace(dto.Nome))
            throw new ApiException(ErrorCode.PgiaDominioInvalido, "Nome do órgão é obrigatório.");

        if (!PgiaDominios.NaturezaJuridica.Todos.Contains(dto.NaturezaJuridica))
            throw new ApiException(ErrorCode.PgiaDominioInvalido, $"Natureza jurídica inválida: {dto.NaturezaJuridica}");

        // Unidade: nula NÃO desvincula (mantém a atual) — o front antigo não envia o
        // campo; com valor, liga/troca, validando existência e unicidade (1 unidade
        // ↔ 1 órgão, ux_pgia_orgao_unidade). É o conserto de órgão criado sem unidade.
        // A sigla é sempre o código do grupo Keycloak da unidade vigente — nunca
        // digitada; sem unidade (caso legado), a sigla existente é preservada.
        if (dto.UnidadeId != null && dto.UnidadeId != orgao.UnidadeId)
        {
            var novaUnidade = await _context.Unidades.FirstOrDefaultAsync(u => u.id == dto.UnidadeId)
                ?? throw new ApiException(ErrorCode.PgiaUnidadeNaoEncontrada);

            var mesmaUnidade = await _orgaoRepositorio.GetByUnidadeIdAsync(dto.UnidadeId.Value);
            if (mesmaUnidade != null && mesmaUnidade.Id != orgaoId)
                throw new ApiException(ErrorCode.PgiaOrgaoJaExiste,
                    $"A unidade já está vinculada ao órgão {mesmaUnidade.Sigla}.");

            orgao.UnidadeId = dto.UnidadeId;
            orgao.Sigla = novaUnidade.CodigoExterno ?? novaUnidade.Nome;
        }
        else if (orgao.UnidadeId != null)
        {
            var unidadeAtual = await _context.Unidades.FirstAsync(u => u.id == orgao.UnidadeId);
            orgao.Sigla = unidadeAtual.CodigoExterno ?? unidadeAtual.Nome;
        }

        var prestaAplicavel = dto.NaturezaJuridica is PgiaDominios.NaturezaJuridica.EmpresaPublica
            or PgiaDominios.NaturezaJuridica.SociedadeEconomiaMista;

        orgao.Nome = dto.Nome.Trim();
        orgao.NaturezaJuridica = dto.NaturezaJuridica;
        orgao.PrestaServicoCidadao = prestaAplicavel ? dto.PrestaServicoCidadao : null;
        orgao.AlteradoEm = DateTime.UtcNow;
        orgao.AlteradoPor = userEmail;

        await _orgaoRepositorio.SaveChangesAsync();
        return PgiaAdminService.MapOrgao(orgao);
    }

    public async Task<List<PgiaPessoaResponse>> ListarPessoasAsync(long orgaoId)
    {
        var orgao = await _orgaoRepositorio.GetByIdAsync(orgaoId)
            ?? throw new ApiException(ErrorCode.PgiaOrgaoNaoEncontrado);

        if (orgao.UnidadeId == null)
            return new List<PgiaPessoaResponse>();

        var users = await _designacaoRepositorio.ListarUsersDaUnidadeAsync(orgao.UnidadeId.Value);
        var infos = await _designacaoRepositorio.ListarAgenteInfosAsync(users.Select(u => u.Id));
        var infoPorUser = infos.ToDictionary(i => i.UserId);

        return users.Select(u =>
        {
            infoPorUser.TryGetValue(u.Id, out var info);
            return new PgiaPessoaResponse
            {
                UserId = u.Id,
                Nome = u.Nome,
                Email = u.Email,
                PapelPgia = u.PapelPgia,
                Matricula = info?.Matricula,
                CargoFuncao = info?.CargoFuncao,
                Vinculo = info?.Vinculo
            };
        }).ToList();
    }

    public async Task SalvarAgenteInfoAsync(long orgaoId, Guid userId, PgiaAgenteInfoDTO dto, string userEmail)
    {
        var orgao = await _orgaoRepositorio.GetByIdAsync(orgaoId)
            ?? throw new ApiException(ErrorCode.PgiaOrgaoNaoEncontrado);

        if (!PgiaDominios.Vinculo.Todos.Contains(dto.Vinculo))
            throw new ApiException(ErrorCode.PgiaDominioInvalido, $"Vínculo inválido: {dto.Vinculo}");

        var user = await _designacaoRepositorio.GetUserByIdAsync(userId)
            ?? throw new ApiException(ErrorCode.PgiaUsuarioNaoEncontrado);

        if (orgao.UnidadeId == null || user.Unidade?.id != orgao.UnidadeId)
            throw new ApiException(ErrorCode.PgiaAgenteDeOutroOrgao,
                "A pessoa não pertence à unidade vinculada ao órgão.");

        var info = await _designacaoRepositorio.GetAgenteInfoAsync(userId);
        if (info == null)
        {
            info = new PgiaAgenteInfo
            {
                UserId = userId,
                Matricula = dto.Matricula?.Trim(),
                CargoFuncao = dto.CargoFuncao?.Trim(),
                Vinculo = dto.Vinculo,
                CriadoEm = DateTime.UtcNow,
                CriadoPor = userEmail
            };
            _designacaoRepositorio.AddAgenteInfo(info);
        }
        else
        {
            info.Matricula = dto.Matricula?.Trim();
            info.CargoFuncao = dto.CargoFuncao?.Trim();
            info.Vinculo = dto.Vinculo;
            info.AlteradoEm = DateTime.UtcNow;
            info.AlteradoPor = userEmail;
        }

        await _designacaoRepositorio.SaveChangesAsync();
    }

    // ── Designações: Responsável de IA, Encarregado de Dados e Curador de Dados ──
    // As três seguem o mesmo fluxo (IPgiaDesignacao): um titular e um suplente
    // vigentes por órgão; designar numa posição encerra só a vigente daquela posição.

    public async Task<PgiaDesignacaoResponse> DesignarResponsavelIaAsync(long orgaoId, PgiaResponsavelIaCreateDTO dto, string userEmail)
    {
        var designacao = await DesignarAsync(orgaoId, dto, userEmail, new PgiaResponsavelIa
        {
            AcumulaFuncaoTic = dto.AcumulaFuncaoTic,
            // Condicional: capacitação só se aplica quando acumula a função de TIC (art. 10, § 2º)
            CapacitacaoAdequada = dto.AcumulaFuncaoTic ? dto.CapacitacaoAdequada : null
        });
        return MapResponsavel(designacao);
    }

    public async Task<List<PgiaDesignacaoResponse>> ListarResponsaveisIaAsync(long orgaoId)
    {
        var lista = await _designacaoRepositorio.ListarAsync<PgiaResponsavelIa>(orgaoId);
        return lista.Select(MapResponsavel).ToList();
    }

    public async Task<PgiaDesignacaoResponse> AtualizarResponsavelIaAsync(long orgaoId, long designacaoId, PgiaResponsavelIaAtualizarDTO dto, string userEmail)
    {
        var designacao = await AtualizarAtoAsync<PgiaResponsavelIa>(orgaoId, designacaoId, dto, userEmail, r =>
        {
            r.AcumulaFuncaoTic = dto.AcumulaFuncaoTic;
            r.CapacitacaoAdequada = dto.AcumulaFuncaoTic ? dto.CapacitacaoAdequada : null;
        });
        return MapResponsavel(designacao);
    }

    public async Task<PgiaDesignacaoResponse> DesignarEncarregadoDadosAsync(long orgaoId, PgiaDesignacaoCreateDTO dto, string userEmail)
    {
        return MapDesignacao(await DesignarAsync(orgaoId, dto, userEmail, new PgiaEncarregadoDados()));
    }

    public async Task<List<PgiaDesignacaoResponse>> ListarEncarregadosDadosAsync(long orgaoId)
    {
        var lista = await _designacaoRepositorio.ListarAsync<PgiaEncarregadoDados>(orgaoId);
        return lista.Select(MapDesignacao).ToList();
    }

    public async Task<PgiaDesignacaoResponse> AtualizarEncarregadoDadosAsync(long orgaoId, long designacaoId, PgiaDesignacaoAtualizarDTO dto, string userEmail)
    {
        return MapDesignacao(await AtualizarAtoAsync<PgiaEncarregadoDados>(orgaoId, designacaoId, dto, userEmail, null));
    }

    public async Task<PgiaDesignacaoResponse> DesignarCuradorDadosAsync(long orgaoId, PgiaDesignacaoCreateDTO dto, string userEmail)
    {
        return MapDesignacao(await DesignarAsync(orgaoId, dto, userEmail, new PgiaCuradorDados()));
    }

    public async Task<List<PgiaDesignacaoResponse>> ListarCuradoresDadosAsync(long orgaoId)
    {
        var lista = await _designacaoRepositorio.ListarAsync<PgiaCuradorDados>(orgaoId);
        return lista.Select(MapDesignacao).ToList();
    }

    public async Task<PgiaDesignacaoResponse> AtualizarCuradorDadosAsync(long orgaoId, long designacaoId, PgiaDesignacaoAtualizarDTO dto, string userEmail)
    {
        return MapDesignacao(await AtualizarAtoAsync<PgiaCuradorDados>(orgaoId, designacaoId, dto, userEmail, null));
    }

    /// <summary>
    /// Designa a pessoa na posição pedida (ausente = titular). A vigente da MESMA
    /// posição é encerrada e fica no histórico (art. 35, I); a da outra posição não
    /// muda. A mesma pessoa não pode ser titular e suplente da mesma designação.
    /// </summary>
    private async Task<T> DesignarAsync<T>(long orgaoId, PgiaDesignacaoCreateDTO dto, string userEmail, T nova)
        where T : class, IPgiaDesignacao
    {
        var posicao = PgiaDominios.PosicaoDesignacao.Normalizar(dto.Posicao)
            ?? throw new ApiException(ErrorCode.PgiaDominioInvalido,
                $"Posição inválida: {dto.Posicao}. Use Titular ou Suplente.");

        var orgao = await ValidarDesignacaoAsync(orgaoId, dto);

        var outraPosicao = posicao == PgiaDominios.PosicaoDesignacao.Titular
            ? PgiaDominios.PosicaoDesignacao.Suplente
            : PgiaDominios.PosicaoDesignacao.Titular;
        var naOutraPosicao = await _designacaoRepositorio.GetVigenteAsync<T>(orgao.Id, outraPosicao);
        if (naOutraPosicao != null && naOutraPosicao.AgenteId == dto.AgenteId)
            throw new ApiException(ErrorCode.PgiaDominioInvalido,
                $"Esta pessoa já é a {outraPosicao.ToLowerInvariant()} vigente desta designação; " +
                "a mesma pessoa não pode ser titular e suplente ao mesmo tempo.");

        var vigente = await _designacaoRepositorio.GetVigenteAsync<T>(orgao.Id, posicao);
        if (vigente != null)
        {
            // O histórico de designações fica preservado (art. 35, I)
            vigente.Ativo = false;
            vigente.FimVigencia ??= dto.InicioVigencia;
            vigente.AlteradoEm = DateTime.UtcNow;
            vigente.AlteradoPor = userEmail;
            // A desativação é salva antes de inserir a nova: o índice único parcial
            // (orgao_id, posicao) WHERE ativo é validado por linha e o EF não garante
            // UPDATE antes de INSERT dentro do mesmo SaveChanges.
            await _designacaoRepositorio.SaveChangesAsync();
        }

        nova.OrgaoId = orgao.Id;
        nova.AgenteId = dto.AgenteId;
        nova.Posicao = posicao;
        nova.AtoPendente = false;
        AplicarAto(nova, dto);
        nova.Ativo = true;
        nova.CriadoEm = DateTime.UtcNow;
        nova.CriadoPor = userEmail;

        _designacaoRepositorio.Add(nova);
        await _designacaoRepositorio.SaveChangesAsync();

        return await _designacaoRepositorio.GetByIdAsync<T>(orgao.Id, nova.Id) ?? nova;
    }

    /// <summary>
    /// Completa (ato pendente da carga da planilha) ou corrige o ato de uma designação
    /// do órgão, vigente ou do histórico. Exige todos os campos, como a criação; salvo,
    /// a marca de ato pendente fica desligada. Pessoa e posição não mudam.
    /// </summary>
    private async Task<T> AtualizarAtoAsync<T>(long orgaoId, long designacaoId, PgiaDesignacaoAtualizarDTO dto,
        string userEmail, Action<T>? extra) where T : class, IPgiaDesignacao
    {
        var orgao = await _orgaoRepositorio.GetByIdAsync(orgaoId)
            ?? throw new ApiException(ErrorCode.PgiaOrgaoNaoEncontrado);

        var designacao = await _designacaoRepositorio.GetByIdAsync<T>(orgaoId, designacaoId)
            ?? throw new ApiException(ErrorCode.PgiaDesignacaoNaoEncontrada);

        ValidarAto(dto);
        await ValidarDocumentoAsync(orgao, dto.DocumentoId);

        if (designacao.FimVigencia != null && dto.InicioVigencia > designacao.FimVigencia)
            throw new ApiException(ErrorCode.PgiaDominioInvalido,
                $"O início da vigência não pode ser depois do fim da vigência ({designacao.FimVigencia:dd/MM/yyyy}).");

        AplicarAto(designacao, dto);
        extra?.Invoke(designacao);
        designacao.AtoPendente = false;
        designacao.AlteradoEm = DateTime.UtcNow;
        designacao.AlteradoPor = userEmail;

        await _designacaoRepositorio.SaveChangesAsync();
        return designacao;
    }

    private static void AplicarAto(IPgiaDesignacao designacao, PgiaDesignacaoAtoDTO dto)
    {
        designacao.AtoTipo = dto.AtoTipo.Trim();
        designacao.AtoNumero = dto.AtoNumero.Trim();
        designacao.AtoData = dto.AtoData;
        designacao.ProcessoSeiComunicacao = dto.ProcessoSeiComunicacao.Trim();
        designacao.DataComunicacaoSgdi = dto.DataComunicacaoSgdi;
        designacao.DocumentoId = dto.DocumentoId;
        designacao.InicioVigencia = dto.InicioVigencia;
    }

    public async Task<List<PgiaPrazoResponse>> ListarPrazosOrgaoAsync(long orgaoId)
    {
        var prazos = await _prazoRepositorio.ListarPorOrgaoAsync(orgaoId);
        return prazos.Select(MapPrazo).ToList();
    }

    public async Task<List<PgiaPrazoResponse>> ListarPrazosCentraisAsync()
    {
        var prazos = await _prazoRepositorio.ListarCentraisAsync();
        return prazos.Select(MapPrazo).ToList();
    }

    public async Task<PgiaPrazoResponse> MarcarCumprimentoAsync(long prazoId, PgiaMarcarCumprimentoDTO dto, string userEmail)
    {
        var prazo = await _prazoRepositorio.GetByIdAsync(prazoId)
            ?? throw new ApiException(ErrorCode.PgiaPrazoNaoEncontrado);

        prazo.CumpridoEm = dto.CumpridoEm;
        prazo.AlteradoEm = DateTime.UtcNow;
        prazo.AlteradoPor = userEmail;

        await _prazoRepositorio.SaveChangesAsync();
        return MapPrazo(prazo);
    }

    public async Task<PgiaPrazoConformidade?> GetPrazoAsync(long prazoId)
    {
        return await _prazoRepositorio.GetByIdAsync(prazoId);
    }

    private async Task<PgiaOrgao> ValidarDesignacaoAsync(long orgaoId, PgiaDesignacaoCreateDTO dto)
    {
        var orgao = await _orgaoRepositorio.GetByIdAsync(orgaoId)
            ?? throw new ApiException(ErrorCode.PgiaOrgaoNaoEncontrado);

        ValidarAto(dto);

        if (orgao.UnidadeId == null)
            throw new ApiException(ErrorCode.PgiaOrgaoSemUnidadeVinculada,
                "O órgão ainda não está vinculado a uma unidade do SGDP.");

        var agente = await _designacaoRepositorio.GetUserByIdAsync(dto.AgenteId)
            ?? throw new ApiException(ErrorCode.PgiaUsuarioNaoEncontrado);

        if (agente.Unidade?.id != orgao.UnidadeId)
            throw new ApiException(ErrorCode.PgiaAgenteDeOutroOrgao,
                "A pessoa designada não pertence à unidade vinculada ao órgão.");

        await ValidarDocumentoAsync(orgao, dto.DocumentoId);
        return orgao;
    }

    /// <summary>
    /// A API exige o ato inteiro, na criação e na atualização: só a carga SQL da
    /// planilha da SGDI grava designação com o ato pendente.
    /// </summary>
    private static void ValidarAto(PgiaDesignacaoAtoDTO dto)
    {
        if (string.IsNullOrWhiteSpace(dto.AtoTipo) || string.IsNullOrWhiteSpace(dto.AtoNumero)
            || string.IsNullOrWhiteSpace(dto.ProcessoSeiComunicacao))
            throw new ApiException(ErrorCode.PgiaDominioInvalido,
                "Tipo do ato, número do ato e processo SEI da comunicação são obrigatórios.");

        if (dto.AtoData == default || dto.DataComunicacaoSgdi == default || dto.InicioVigencia == default)
            throw new ApiException(ErrorCode.PgiaDominioInvalido,
                "Data do ato, data da comunicação à SGDI e início da vigência são obrigatórios.");
    }

    /// <summary>Cópia do ato: documento tem de existir e ser DO MESMO órgão da designação.</summary>
    private async Task ValidarDocumentoAsync(PgiaOrgao orgao, long? documentoId)
    {
        if (documentoId == null) return;

        var documento = await _designacaoRepositorio.GetDocumentoByIdAsync(documentoId.Value)
            ?? throw new ApiException(ErrorCode.PgiaDocumentoNaoEncontrado);

        if (documento.OrgaoId != orgao.Id)
            throw new ApiException(ErrorCode.PgiaDominioInvalido,
                "A cópia do ato precisa ser um documento do próprio órgão.");
    }

    private static PgiaDesignacaoResponse MapResponsavel(PgiaResponsavelIa r)
    {
        var resposta = MapDesignacao(r);
        resposta.AcumulaFuncaoTic = r.AcumulaFuncaoTic;
        resposta.CapacitacaoAdequada = r.CapacitacaoAdequada;
        return resposta;
    }

    /// <summary>Resposta comum às três designações (o Responsável completa a acumulação de TIC).</summary>
    public static PgiaDesignacaoResponse MapDesignacao(IPgiaDesignacao d)
    {
        return new PgiaDesignacaoResponse
        {
            Id = d.Id,
            OrgaoId = d.OrgaoId,
            AgenteId = d.AgenteId,
            AgenteNome = d.Agente?.Nome ?? string.Empty,
            AgenteEmail = d.Agente?.Email ?? string.Empty,
            Posicao = d.Posicao,
            AtoPendente = d.AtoPendente,
            AtoTipo = d.AtoTipo,
            AtoNumero = d.AtoNumero,
            AtoData = d.AtoData,
            ProcessoSeiComunicacao = d.ProcessoSeiComunicacao,
            DataComunicacaoSgdi = d.DataComunicacaoSgdi,
            DocumentoId = d.DocumentoId,
            InicioVigencia = d.InicioVigencia,
            FimVigencia = d.FimVigencia,
            Ativo = d.Ativo
        };
    }

    private static PgiaPrazoResponse MapPrazo(PgiaPrazoConformidade p)
    {
        // Dia civil de Brasília, como na view v_prazos_situacao (CURRENT_DATE) do schema
        var hoje = DateOnly.FromDateTime(DateTimeHelper.TodayBrasilia());
        string situacao;
        if (p.CumpridoEm != null && p.CumpridoEm <= p.DataLimite)
            situacao = PgiaDominios.SituacaoPrazo.CumpridaNoPrazo;
        else if (p.CumpridoEm != null)
            situacao = PgiaDominios.SituacaoPrazo.CumpridaEmAtraso;
        else if (hoje > p.DataLimite)
            situacao = PgiaDominios.SituacaoPrazo.Vencida;
        else
            situacao = PgiaDominios.SituacaoPrazo.NoPrazo;

        return new PgiaPrazoResponse
        {
            Id = p.Id,
            Obrigacao = p.Obrigacao,
            BaseLegal = p.BaseLegal,
            OrgaoId = p.OrgaoId,
            DataLimite = p.DataLimite,
            CumpridoEm = p.CumpridoEm,
            Situacao = situacao
        };
    }
}
