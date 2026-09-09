using api.Auth;
using app.Auth;
using app.Models;
using Microsoft.EntityFrameworkCore;
using Models;
using Models.Pgia;

namespace Repositorio;

public class AuthRepositorio : IAuthRepositorio
{
    private readonly AppDbContext _context;

    public AuthRepositorio(AppDbContext context)
    {
        _context = context;
    }

    public async Task<User> GetOrCreateUserAsync(string keycloakId, string nome, string email, string? unidadeCodigo = null)
    {
        var user = await _context.Users.Include(u => u.Unidade)
            .FirstOrDefaultAsync(u => u.KeycloakId == keycloakId);

        if (user == null)
            user = await _context.Users.Include(u => u.Unidade)
                .FirstOrDefaultAsync(u => u.Email == email);

        if (user == null)
        {
            user = new User { KeycloakId = keycloakId, Nome = nome, Email = email };
            _context.Users.Add(user);
        }
        else
        {
            user.KeycloakId = keycloakId;
            user.Nome = nome;
            _context.Users.Update(user);
        }

        if (!string.IsNullOrEmpty(unidadeCodigo) && !await TemDesignacaoVigenteAsync(user.Id))
            user.Unidade = await GetOrCreateUnidadeByCodigoAsync(unidadeCodigo);

        await _context.SaveChangesAsync();
        return user;
    }

    private async Task<Unidade> GetOrCreateUnidadeByCodigoAsync(string codigo)
    {
        var unidade = await _context.Unidades.FirstOrDefaultAsync(u => u.CodigoExterno == codigo);
        if (unidade != null)
        {
            await GarantirOrgaoDaUnidadeAsync(unidade);
            return unidade;
        }

        unidade = new Unidade { Nome = codigo, CodigoExterno = codigo };
        _context.Unidades.Add(unidade);
        await GarantirOrgaoDaUnidadeAsync(unidade);
        return unidade;
    }

    // O grupo Keycloak é a própria Unidade (já sincronizada acima) e representa
    // 1:1 o órgão do PGIA: todo grupo novo provisiona um pgia_orgao automático,
    // com dados-placeholder que a SGDI completa depois em "Dados dos órgãos"
    // (PUT governanca/orgao/{id}/dados) — ninguém fica bloqueado esperando
    // cadastro manual prévio da SGDI.
    private async Task GarantirOrgaoDaUnidadeAsync(Unidade unidade)
    {
        var jaTemOrgao = await _context.PgiaOrgaos.AnyAsync(o => o.UnidadeId == unidade.id);
        if (jaTemOrgao) return;

        var sigla = await SiglaDisponivelAsync(unidade.CodigoExterno ?? unidade.Nome);
        var orgao = new PgiaOrgao
        {
            Sigla = sigla,
            Nome = unidade.Nome,
            NaturezaJuridica = PgiaDominios.NaturezaJuridica.AdministracaoDireta,
            UnidadeId = unidade.id,
            Ativo = true,
            CriadoEm = DateTime.UtcNow,
            CriadoPor = "sistema (auto-provisionado no login)"
        };
        _context.PgiaOrgaos.Add(orgao);

        // Mesma adesão automática de PgiaAdminService.CriarOrgaoAsync: instancia
        // as obrigações-modelo com prazo (art. 35 e correlatos), exceto as
        // exclusivas da SGDI.
        var modelos = await _context.PgiaPrazosConformidade
            .Where(p => p.OrgaoId == null)
            .ToListAsync();
        foreach (var modelo in modelos.Where(m => !m.Obrigacao.StartsWith("SGDI:")))
        {
            _context.PgiaPrazosConformidade.Add(new PgiaPrazoConformidade
            {
                Obrigacao = modelo.Obrigacao,
                BaseLegal = modelo.BaseLegal,
                Orgao = orgao,
                DataLimite = modelo.DataLimite,
                CriadoEm = DateTime.UtcNow,
                CriadoPor = "sistema (auto-provisionado no login)"
            });
        }
    }

    private async Task<string> SiglaDisponivelAsync(string base_)
    {
        var sigla = base_.Length > 20 ? base_[..20] : base_;
        if (!await _context.PgiaOrgaos.AnyAsync(o => o.Sigla == sigla)) return sigla;

        for (var sufixo = 2; ; sufixo++)
        {
            var candidata = $"{sigla}-{sufixo}";
            if (!await _context.PgiaOrgaos.AnyAsync(o => o.Sigla == candidata)) return candidata;
        }
    }

    // Responsável de IA e Encarregado de Dados vigentes têm a unidade travada à
    // designação (PgiaGovernancaService.GarantirQueNaoOrfanizaDesignacaoAsync); o
    // login não deve sobrescrever silenciosamente algo que a troca manual recusa.
    private async Task<bool> TemDesignacaoVigenteAsync(Guid userId)
    {
        var ehResponsavel = await _context.PgiaResponsaveisIa
            .AnyAsync(r => r.AgenteId == userId && r.Ativo);
        if (ehResponsavel) return true;

        return await _context.PgiaEncarregadosDados
            .AnyAsync(e => e.AgenteId == userId && e.Ativo);
    }

    public async Task CriarUnidade(UnidadeDTO unidade)
    {
        _context.Unidades.Add(new Unidade { Nome = unidade.nome });
        await _context.SaveChangesAsync();
    }

    public async Task<List<Unidade>> GetUnidadesAsync()
    {
        return await _context.Unidades.ToListAsync();
    }

    public async Task InformarUnidadeUsuario(string email, string unidadeId)
    {
        var usuario = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
        var unidade = await _context.Unidades.FirstOrDefaultAsync(u => u.id == Guid.Parse(unidadeId));
        if (usuario == null || unidade == null) return;
        usuario.Unidade = unidade;
        _context.Users.Update(usuario);
        await _context.SaveChangesAsync();
    }

    public async Task<List<User>> ListarUsuariosAsync()
    {
        return await _context.Users.Include(u => u.Unidade).ToListAsync();
    }

    public async Task<bool> ModificarUnidadeUsuario(string email, string unidadeId)
    {
        var usuario = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
        if (usuario == null) return false;

        var unidade = await _context.Unidades.FirstOrDefaultAsync(u => u.id == Guid.Parse(unidadeId));
        if (unidade == null) return false;

        usuario.Unidade = unidade;
        _context.Users.Update(usuario);
        await _context.SaveChangesAsync();
        return true;
    }
}
