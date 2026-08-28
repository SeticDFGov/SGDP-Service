using api.Pgia;
using app.Auth;
using Microsoft.EntityFrameworkCore;
using Models;
using Models.Pgia;
using Repositorio.Interface;
using service.Interface;

namespace service.Pgia;

public class PgiaAdminService : IPgiaAdminService
{
    private readonly IPgiaOrgaoRepositorio _orgaoRepositorio;
    private readonly IPgiaPrazoRepositorio _prazoRepositorio;
    private readonly AppDbContext _context;

    public PgiaAdminService(IPgiaOrgaoRepositorio orgaoRepositorio, IPgiaPrazoRepositorio prazoRepositorio, AppDbContext context)
    {
        _orgaoRepositorio = orgaoRepositorio;
        _prazoRepositorio = prazoRepositorio;
        _context = context;
    }

    public async Task AtribuirPapelAsync(AtribuirPapelPgiaDTO dto, string adminEmail)
    {
        if (!PapeisPgia.EhValido(dto.PapelPgia))
            throw new ApiException(ErrorCode.PgiaPapelInvalido,
                $"Papel PGIA inválido: {dto.PapelPgia}");

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == dto.Email)
            ?? throw new ApiException(ErrorCode.PgiaUsuarioNaoEncontrado);

        // Só o papel PGIA muda; o Perfil do SGDP nunca é tocado aqui.
        user.PapelPgia = dto.PapelPgia;
        await _context.SaveChangesAsync();
    }

    public async Task<List<PgiaOrgaoResponse>> ListarOrgaosAsync()
    {
        var orgaos = await _orgaoRepositorio.ListarAsync();
        return orgaos.Select(MapOrgao).ToList();
    }

    public async Task<PgiaOrgaoResponse> CriarOrgaoAsync(PgiaOrgaoCreateDTO dto, string adminEmail)
    {
        await ValidarOrgaoAsync(dto.Sigla, dto.Nome, dto.NaturezaJuridica, dto.UnidadeId, orgaoId: null);

        var orgao = new PgiaOrgao
        {
            Sigla = dto.Sigla.Trim(),
            Nome = dto.Nome.Trim(),
            NaturezaJuridica = dto.NaturezaJuridica,
            PrestaServicoCidadao = PrestaServicoAplicavel(dto.NaturezaJuridica) ? dto.PrestaServicoCidadao : null,
            UnidadeId = dto.UnidadeId,
            Ativo = true,
            CriadoEm = DateTime.UtcNow,
            CriadoPor = adminEmail
        };

        _orgaoRepositorio.Add(orgao);

        // Adesão do órgão: instancia as obrigações-modelo com prazo (art. 35 e correlatos).
        // As obrigações exclusivas da SGDI ("SGDI: ...") permanecem só nas linhas centrais.
        var modelos = await _prazoRepositorio.ListarCentraisAsync();
        var copias = modelos
            .Where(m => !m.Obrigacao.StartsWith("SGDI:"))
            .Select(m => new PgiaPrazoConformidade
            {
                Obrigacao = m.Obrigacao,
                BaseLegal = m.BaseLegal,
                Orgao = orgao,
                DataLimite = m.DataLimite,
                CriadoEm = DateTime.UtcNow,
                CriadoPor = adminEmail
            });
        _prazoRepositorio.AddRange(copias);

        await _orgaoRepositorio.SaveChangesAsync();
        return MapOrgao(orgao);
    }

    public async Task<PgiaOrgaoResponse> EditarOrgaoAsync(long id, PgiaOrgaoUpdateDTO dto, string adminEmail)
    {
        var orgao = await _orgaoRepositorio.GetByIdAsync(id)
            ?? throw new ApiException(ErrorCode.PgiaOrgaoNaoEncontrado);

        await ValidarOrgaoAsync(dto.Sigla, dto.Nome, dto.NaturezaJuridica, dto.UnidadeId, orgaoId: id);

        orgao.Sigla = dto.Sigla.Trim();
        orgao.Nome = dto.Nome.Trim();
        orgao.NaturezaJuridica = dto.NaturezaJuridica;
        orgao.PrestaServicoCidadao = PrestaServicoAplicavel(dto.NaturezaJuridica) ? dto.PrestaServicoCidadao : null;
        orgao.UnidadeId = dto.UnidadeId;
        orgao.Ativo = dto.Ativo;
        orgao.AlteradoEm = DateTime.UtcNow;
        orgao.AlteradoPor = adminEmail;

        await _orgaoRepositorio.SaveChangesAsync();
        return MapOrgao(orgao);
    }

    private async Task ValidarOrgaoAsync(string sigla, string nome, string natureza, Guid? unidadeId, long? orgaoId)
    {
        if (string.IsNullOrWhiteSpace(sigla) || string.IsNullOrWhiteSpace(nome))
            throw new ApiException(ErrorCode.PgiaDominioInvalido, "Sigla e nome do órgão são obrigatórios.");

        if (!PgiaDominios.NaturezaJuridica.Todos.Contains(natureza))
            throw new ApiException(ErrorCode.PgiaDominioInvalido, $"Natureza jurídica inválida: {natureza}");

        var mesmaSigla = await _orgaoRepositorio.GetBySiglaAsync(sigla.Trim());
        if (mesmaSigla != null && mesmaSigla.Id != orgaoId)
            throw new ApiException(ErrorCode.PgiaOrgaoJaExiste, $"Já existe órgão com a sigla {sigla.Trim()}.");

        if (unidadeId != null)
        {
            var unidade = await _context.Unidades.FirstOrDefaultAsync(u => u.id == unidadeId)
                ?? throw new ApiException(ErrorCode.PgiaUnidadeNaoEncontrada);

            var mesmaUnidade = await _orgaoRepositorio.GetByUnidadeIdAsync(unidade.id);
            if (mesmaUnidade != null && mesmaUnidade.Id != orgaoId)
                throw new ApiException(ErrorCode.PgiaOrgaoJaExiste,
                    $"A unidade {unidade.Nome} já está vinculada ao órgão {mesmaUnidade.Sigla}.");
        }
    }

    private static bool PrestaServicoAplicavel(string natureza) =>
        natureza is PgiaDominios.NaturezaJuridica.EmpresaPublica
            or PgiaDominios.NaturezaJuridica.SociedadeEconomiaMista;

    internal static PgiaOrgaoResponse MapOrgao(PgiaOrgao orgao)
    {
        return new PgiaOrgaoResponse
        {
            Id = orgao.Id,
            Sigla = orgao.Sigla,
            Nome = orgao.Nome,
            NaturezaJuridica = orgao.NaturezaJuridica,
            PrestaServicoCidadao = orgao.PrestaServicoCidadao,
            Ativo = orgao.Ativo,
            UnidadeId = orgao.UnidadeId,
            UnidadeNome = orgao.Unidade?.Nome
        };
    }
}
