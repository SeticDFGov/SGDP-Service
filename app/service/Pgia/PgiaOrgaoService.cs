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

        var responsavel = await _designacaoRepositorio.GetResponsavelVigenteAsync(orgao.Id);
        var encarregado = await _designacaoRepositorio.GetEncarregadoVigenteAsync(orgao.Id);

        return new PgiaMeuOrgaoResponse
        {
            Orgao = PgiaAdminService.MapOrgao(orgao),
            ResponsavelIaVigente = responsavel == null ? null : MapResponsavel(responsavel),
            EncarregadoDadosVigente = encarregado == null ? null : MapEncarregado(encarregado)
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

        if (string.IsNullOrWhiteSpace(dto.Sigla) || string.IsNullOrWhiteSpace(dto.Nome))
            throw new ApiException(ErrorCode.PgiaDominioInvalido, "Sigla e nome do órgão são obrigatórios.");

        if (!PgiaDominios.NaturezaJuridica.Todos.Contains(dto.NaturezaJuridica))
            throw new ApiException(ErrorCode.PgiaDominioInvalido, $"Natureza jurídica inválida: {dto.NaturezaJuridica}");

        var mesmaSigla = await _orgaoRepositorio.GetBySiglaAsync(dto.Sigla.Trim());
        if (mesmaSigla != null && mesmaSigla.Id != orgaoId)
            throw new ApiException(ErrorCode.PgiaOrgaoJaExiste, $"Já existe órgão com a sigla {dto.Sigla.Trim()}.");

        // Unidade: nula NÃO desvincula (mantém a atual) — o front antigo não envia o
        // campo; com valor, liga/troca, validando existência e unicidade (1 unidade
        // ↔ 1 órgão, ux_pgia_orgao_unidade). É o conserto de órgão criado sem unidade.
        if (dto.UnidadeId != null && dto.UnidadeId != orgao.UnidadeId)
        {
            _ = await _context.Unidades.FirstOrDefaultAsync(u => u.id == dto.UnidadeId)
                ?? throw new ApiException(ErrorCode.PgiaUnidadeNaoEncontrada);

            var mesmaUnidade = await _orgaoRepositorio.GetByUnidadeIdAsync(dto.UnidadeId.Value);
            if (mesmaUnidade != null && mesmaUnidade.Id != orgaoId)
                throw new ApiException(ErrorCode.PgiaOrgaoJaExiste,
                    $"A unidade já está vinculada ao órgão {mesmaUnidade.Sigla}.");

            orgao.UnidadeId = dto.UnidadeId;
        }

        var prestaAplicavel = dto.NaturezaJuridica is PgiaDominios.NaturezaJuridica.EmpresaPublica
            or PgiaDominios.NaturezaJuridica.SociedadeEconomiaMista;

        orgao.Sigla = dto.Sigla.Trim();
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

    public async Task<PgiaDesignacaoResponse> DesignarResponsavelIaAsync(long orgaoId, PgiaResponsavelIaCreateDTO dto, string userEmail)
    {
        var orgao = await ValidarDesignacaoAsync(orgaoId, dto);

        var vigente = await _designacaoRepositorio.GetResponsavelVigenteAsync(orgaoId);
        if (vigente != null)
        {
            // O histórico de designações fica preservado (art. 35, I)
            vigente.Ativo = false;
            vigente.FimVigencia ??= dto.InicioVigencia;
            vigente.AlteradoEm = DateTime.UtcNow;
            vigente.AlteradoPor = userEmail;
            // A desativação é salva antes de inserir a nova: o índice único parcial
            // ux_pgia_responsavel_ia_vigente (orgao_id WHERE ativo) é validado por linha
            // e o EF não garante UPDATE antes de INSERT dentro do mesmo SaveChanges.
            await _designacaoRepositorio.SaveChangesAsync();
        }

        var designacao = new PgiaResponsavelIa
        {
            OrgaoId = orgao.Id,
            AgenteId = dto.AgenteId,
            AtoTipo = dto.AtoTipo.Trim(),
            AtoNumero = dto.AtoNumero.Trim(),
            AtoData = dto.AtoData,
            ProcessoSeiComunicacao = dto.ProcessoSeiComunicacao.Trim(),
            DataComunicacaoSgdi = dto.DataComunicacaoSgdi,
            AcumulaFuncaoTic = dto.AcumulaFuncaoTic,
            // Condicional: capacitação só se aplica quando acumula a função de TIC (art. 10, § 2º)
            CapacitacaoAdequada = dto.AcumulaFuncaoTic ? dto.CapacitacaoAdequada : null,
            InicioVigencia = dto.InicioVigencia,
            Ativo = true,
            CriadoEm = DateTime.UtcNow,
            CriadoPor = userEmail
        };

        _designacaoRepositorio.AddResponsavel(designacao);
        await _designacaoRepositorio.SaveChangesAsync();

        var salvo = await _designacaoRepositorio.GetResponsavelVigenteAsync(orgaoId);
        return MapResponsavel(salvo ?? designacao);
    }

    public async Task<List<PgiaDesignacaoResponse>> ListarResponsaveisIaAsync(long orgaoId)
    {
        var lista = await _designacaoRepositorio.ListarResponsaveisAsync(orgaoId);
        return lista.Select(MapResponsavel).ToList();
    }

    public async Task<PgiaDesignacaoResponse> DesignarEncarregadoDadosAsync(long orgaoId, PgiaDesignacaoCreateDTO dto, string userEmail)
    {
        var orgao = await ValidarDesignacaoAsync(orgaoId, dto);

        var vigente = await _designacaoRepositorio.GetEncarregadoVigenteAsync(orgaoId);
        if (vigente != null)
        {
            vigente.Ativo = false;
            vigente.FimVigencia ??= dto.InicioVigencia;
            vigente.AlteradoEm = DateTime.UtcNow;
            vigente.AlteradoPor = userEmail;
            // Mesma razão do Responsável de IA: o índice único parcial exige a
            // desativação persistida antes do INSERT da nova designação.
            await _designacaoRepositorio.SaveChangesAsync();
        }

        var designacao = new PgiaEncarregadoDados
        {
            OrgaoId = orgao.Id,
            AgenteId = dto.AgenteId,
            AtoTipo = dto.AtoTipo.Trim(),
            AtoNumero = dto.AtoNumero.Trim(),
            AtoData = dto.AtoData,
            ProcessoSeiComunicacao = dto.ProcessoSeiComunicacao.Trim(),
            DataComunicacaoSgdi = dto.DataComunicacaoSgdi,
            InicioVigencia = dto.InicioVigencia,
            Ativo = true,
            CriadoEm = DateTime.UtcNow,
            CriadoPor = userEmail
        };

        _designacaoRepositorio.AddEncarregado(designacao);
        await _designacaoRepositorio.SaveChangesAsync();

        var salvo = await _designacaoRepositorio.GetEncarregadoVigenteAsync(orgaoId);
        return MapEncarregado(salvo ?? designacao);
    }

    public async Task<List<PgiaDesignacaoResponse>> ListarEncarregadosDadosAsync(long orgaoId)
    {
        var lista = await _designacaoRepositorio.ListarEncarregadosAsync(orgaoId);
        return lista.Select(MapEncarregado).ToList();
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

        if (string.IsNullOrWhiteSpace(dto.AtoTipo) || string.IsNullOrWhiteSpace(dto.AtoNumero)
            || string.IsNullOrWhiteSpace(dto.ProcessoSeiComunicacao))
            throw new ApiException(ErrorCode.PgiaDominioInvalido,
                "Tipo do ato, número do ato e processo SEI da comunicação são obrigatórios.");

        if (orgao.UnidadeId == null)
            throw new ApiException(ErrorCode.PgiaOrgaoSemUnidadeVinculada,
                "O órgão ainda não está vinculado a uma unidade do SGDP.");

        var agente = await _designacaoRepositorio.GetUserByIdAsync(dto.AgenteId)
            ?? throw new ApiException(ErrorCode.PgiaUsuarioNaoEncontrado);

        if (agente.Unidade?.id != orgao.UnidadeId)
            throw new ApiException(ErrorCode.PgiaAgenteDeOutroOrgao,
                "A pessoa designada não pertence à unidade vinculada ao órgão.");

        return orgao;
    }

    private static PgiaDesignacaoResponse MapResponsavel(PgiaResponsavelIa r)
    {
        return new PgiaDesignacaoResponse
        {
            Id = r.Id,
            OrgaoId = r.OrgaoId,
            AgenteId = r.AgenteId,
            AgenteNome = r.Agente?.Nome ?? string.Empty,
            AgenteEmail = r.Agente?.Email ?? string.Empty,
            AtoTipo = r.AtoTipo,
            AtoNumero = r.AtoNumero,
            AtoData = r.AtoData,
            ProcessoSeiComunicacao = r.ProcessoSeiComunicacao,
            DataComunicacaoSgdi = r.DataComunicacaoSgdi,
            AcumulaFuncaoTic = r.AcumulaFuncaoTic,
            CapacitacaoAdequada = r.CapacitacaoAdequada,
            InicioVigencia = r.InicioVigencia,
            FimVigencia = r.FimVigencia,
            Ativo = r.Ativo
        };
    }

    private static PgiaDesignacaoResponse MapEncarregado(PgiaEncarregadoDados e)
    {
        return new PgiaDesignacaoResponse
        {
            Id = e.Id,
            OrgaoId = e.OrgaoId,
            AgenteId = e.AgenteId,
            AgenteNome = e.Agente?.Nome ?? string.Empty,
            AgenteEmail = e.Agente?.Email ?? string.Empty,
            AtoTipo = e.AtoTipo,
            AtoNumero = e.AtoNumero,
            AtoData = e.AtoData,
            ProcessoSeiComunicacao = e.ProcessoSeiComunicacao,
            DataComunicacaoSgdi = e.DataComunicacaoSgdi,
            InicioVigencia = e.InicioVigencia,
            FimVigencia = e.FimVigencia,
            Ativo = e.Ativo
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
