using app.Models;
using Microsoft.EntityFrameworkCore;
using Models;
using Models.Pgia;
using Repositorio.Interface;

namespace Repositorio.Pgia;

public class PgiaGovernancaRepositorio : IPgiaGovernancaRepositorio
{
    private readonly AppDbContext _context;

    public PgiaGovernancaRepositorio(AppDbContext context)
    {
        _context = context;
    }

    // ── Deliberações ──────────────────────────────────────────────────────────

    public async Task<PgiaDeliberacaoCgtic?> GetDeliberacaoByIdAsync(long id)
    {
        return await _context.PgiaDeliberacoesCgtic
            .Include(d => d.Sistema)
                .ThenInclude(s => s!.Orgao)
            .FirstOrDefaultAsync(d => d.Id == id);
    }

    public async Task<List<PgiaDeliberacaoCgtic>> ListarDeliberacoesAsync()
    {
        return await _context.PgiaDeliberacoesCgtic
            .Include(d => d.Sistema)
                .ThenInclude(s => s!.Orgao)
            .OrderByDescending(d => d.DataDeliberacao)
            .ThenByDescending(d => d.Id)
            .ToListAsync();
    }

    public async Task<List<PgiaDeliberacaoCgtic>> ListarDeliberacoesPorSistemaAsync(long sistemaId)
    {
        return await _context.PgiaDeliberacoesCgtic
            .Include(d => d.Sistema)
                .ThenInclude(s => s!.Orgao)
            .Where(d => d.SistemaIaId == sistemaId)
            .OrderByDescending(d => d.DataDeliberacao)
            .ThenByDescending(d => d.Id)
            .ToListAsync();
    }

    public void AddDeliberacao(PgiaDeliberacaoCgtic deliberacao)
    {
        _context.PgiaDeliberacoesCgtic.Add(deliberacao);
    }

    // ── Plataformas ───────────────────────────────────────────────────────────

    public async Task<PgiaPlataformaIaGenerativa?> GetPlataformaByIdAsync(long id)
    {
        return await _context.PgiaPlataformasIaGenerativa.FirstOrDefaultAsync(p => p.Id == id);
    }

    public async Task<PgiaPlataformaIaGenerativa?> GetPlataformaByNomeAsync(string nome)
    {
        return await _context.PgiaPlataformasIaGenerativa.FirstOrDefaultAsync(p => p.Nome == nome);
    }

    public async Task<List<PgiaPlataformaIaGenerativa>> ListarPlataformasAsync()
    {
        return await _context.PgiaPlataformasIaGenerativa
            .OrderBy(p => p.Nome)
            .ToListAsync();
    }

    public void AddPlataforma(PgiaPlataformaIaGenerativa plataforma)
    {
        _context.PgiaPlataformasIaGenerativa.Add(plataforma);
    }

    // ── Normas ────────────────────────────────────────────────────────────────

    public async Task<PgiaNormaComplementar?> GetNormaByIdAsync(long id)
    {
        return await _context.PgiaNormasComplementares.FirstOrDefaultAsync(n => n.Id == id);
    }

    public async Task<List<PgiaNormaComplementar>> ListarNormasAsync()
    {
        return await _context.PgiaNormasComplementares
            .OrderByDescending(n => n.DataPublicacao)
            .ThenByDescending(n => n.Id)
            .ToListAsync();
    }

    public void AddNorma(PgiaNormaComplementar norma)
    {
        _context.PgiaNormasComplementares.Add(norma);
    }

    // ── Autorizações ──────────────────────────────────────────────────────────

    public async Task<PgiaAutorizacaoExcepcional?> GetAutorizacaoByIdAsync(long id)
    {
        return await _context.PgiaAutorizacoesExcepcionais
            .Include(a => a.Orgao)
            .Include(a => a.Plataforma)
            .FirstOrDefaultAsync(a => a.Id == id);
    }

    public async Task<List<PgiaAutorizacaoExcepcional>> ListarAutorizacoesAsync(long? orgaoId)
    {
        var query = _context.PgiaAutorizacoesExcepcionais
            .Include(a => a.Orgao)
            .Include(a => a.Plataforma)
            .AsQueryable();

        if (orgaoId != null)
            query = query.Where(a => a.OrgaoId == orgaoId);

        return await query
            .OrderByDescending(a => a.DataAutorizacao)
            .ThenByDescending(a => a.Id)
            .ToListAsync();
    }

    public void AddAutorizacao(PgiaAutorizacaoExcepcional autorizacao)
    {
        _context.PgiaAutorizacoesExcepcionais.Add(autorizacao);
    }

    // ── Gestão de pessoas (papel PGIA + unidade) ──────────────────────────────

    public async Task<List<User>> ListarUsuariosAsync(string? filtro, Guid? unidadeId, int? limite)
    {
        // Todos os usuários do SGDP, inclusive sem unidade e sem papel PGIA: é
        // assim que a SGDI encontra quem acabou de chegar para vincular ao órgão.
        var query = _context.Users
            .Include(u => u.Unidade)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(filtro))
        {
            // ToLower().Contains() em vez de ILike: o mesmo LINQ vale no Npgsql
            // (vira lower(...) like ...) e no provedor InMemory dos testes
            var termo = filtro.Trim().ToLower();
            query = query.Where(u => u.Nome.ToLower().Contains(termo)
                || u.Email.ToLower().Contains(termo));
        }

        if (unidadeId != null)
            query = query.Where(u => u.Unidade != null && u.Unidade.id == unidadeId);

        query = query.OrderBy(u => u.Nome);

        if (limite != null)
            query = query.Take(limite.Value);

        return await query.ToListAsync();
    }

    public async Task<User?> GetUserByIdAsync(Guid userId)
    {
        return await _context.Users
            .Include(u => u.Unidade)
            .FirstOrDefaultAsync(u => u.Id == userId);
    }

    public async Task<User?> GetUserByEmailAsync(string email)
    {
        // Sem diferenciar maiúsculas: o pré-cadastro não pode gerar uma segunda
        // pessoa só porque o e-mail foi digitado com outra caixa
        var alvo = email.Trim().ToLower();
        return await _context.Users
            .Include(u => u.Unidade)
            .FirstOrDefaultAsync(u => u.Email.ToLower() == alvo);
    }

    public void AddUser(User user)
    {
        _context.Users.Add(user);
    }

    public async Task<Unidade?> GetUnidadeByIdAsync(Guid unidadeId)
    {
        return await _context.Unidades.FirstOrDefaultAsync(u => u.id == unidadeId);
    }

    public async Task<List<PgiaOrgao>> ListarOrgaosAtivosComUnidadeAsync()
    {
        return await _context.PgiaOrgaos
            .Where(o => o.Ativo && o.UnidadeId != null)
            .ToListAsync();
    }

    public async Task<Dictionary<Guid, PgiaAgenteInfo>> ListarAgenteInfosAsync(IEnumerable<Guid> userIds)
    {
        var ids = userIds.Distinct().ToList();
        if (ids.Count == 0) return new Dictionary<Guid, PgiaAgenteInfo>();

        var infos = await _context.PgiaAgenteInfos
            .Where(a => ids.Contains(a.UserId))
            .ToListAsync();

        return infos.ToDictionary(a => a.UserId);
    }

    public async Task<PgiaAgenteInfo?> GetAgenteInfoAsync(Guid userId)
    {
        return await _context.PgiaAgenteInfos.FirstOrDefaultAsync(a => a.UserId == userId);
    }

    public async Task<PgiaResponsavelIa?> GetResponsavelVigenteDoAgenteAsync(Guid agenteId)
    {
        return await _context.PgiaResponsaveisIa
            .Include(r => r.Orgao)
            .FirstOrDefaultAsync(r => r.AgenteId == agenteId && r.Ativo);
    }

    public async Task<PgiaEncarregadoDados?> GetEncarregadoVigenteDoAgenteAsync(Guid agenteId)
    {
        return await _context.PgiaEncarregadosDados
            .Include(e => e.Orgao)
            .FirstOrDefaultAsync(e => e.AgenteId == agenteId && e.Ativo);
    }

    // ── Apoio ─────────────────────────────────────────────────────────────────

    public async Task<List<PgiaSistemaIa>> ListarSistemasAsync()
    {
        return await _context.PgiaSistemasIa
            .Include(s => s.Orgao)
            .OrderBy(s => s.Orgao!.Sigla)
            .ThenBy(s => s.Denominacao)
            .ToListAsync();
    }

    public async Task<List<PgiaSistemaIa>> ListarCasosCgticAsync()
    {
        // Rota do comitê: a classificação vigente desnormalizada é Alto/Excessivo
        // (PgiaDominios.SituacaoHomologacao.RotaInicial) e a homologação está com o CGTIC
        var riscos = new[]
        {
            PgiaDominios.ResultadoRisco.Alto,
            PgiaDominios.ResultadoRisco.Excessivo
        };
        var situacoes = new[]
        {
            PgiaDominios.SituacaoHomologacao.AguardandoCgtic,
            PgiaDominios.SituacaoHomologacao.Aprovado,
            PgiaDominios.SituacaoHomologacao.Vetado
        };

        return await _context.PgiaSistemasIa
            .Include(s => s.Orgao)
            .Where(s => s.ClassificacaoRiscoAtual != null
                && riscos.Contains(s.ClassificacaoRiscoAtual)
                && situacoes.Contains(s.SituacaoHomologacao))
            .ToListAsync();
    }

    public async Task<Dictionary<long, DateOnly>> ListarDatasClassificacaoVigenteAsync(IEnumerable<long> sistemaIds)
    {
        var ids = sistemaIds.Distinct().ToList();
        if (ids.Count == 0) return new Dictionary<long, DateOnly>();

        // Uma consulta para todos os casos; a vigente é a de maior Id (mesma
        // regra do PgiaSistemaRepositorio.ListarClassificacoesVigentesAsync)
        var classificacoes = await _context.PgiaClassificacoesRisco
            .Where(c => ids.Contains(c.SistemaIaId))
            .Select(c => new { c.SistemaIaId, c.Id, c.DataClassificacao })
            .ToListAsync();

        return classificacoes
            .GroupBy(c => c.SistemaIaId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(c => c.Id).First().DataClassificacao);
    }

    public async Task<PgiaSistemaIa?> GetSistemaByIdAsync(long id)
    {
        return await _context.PgiaSistemasIa
            .Include(s => s.Orgao)
            .FirstOrDefaultAsync(s => s.Id == id);
    }

    public async Task<PgiaOrgao?> GetOrgaoByIdAsync(long id)
    {
        return await _context.PgiaOrgaos.FirstOrDefaultAsync(o => o.Id == id);
    }

    public async Task<PgiaDocumento?> GetDocumentoByIdAsync(long id)
    {
        return await _context.PgiaDocumentos.FirstOrDefaultAsync(d => d.Id == id);
    }

    public async Task<PgiaContratoIa?> GetContratoByIdAsync(long id)
    {
        return await _context.PgiaContratosIa.FirstOrDefaultAsync(c => c.Id == id);
    }

    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }
}
