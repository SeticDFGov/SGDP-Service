using app.Auth;
using app.Models;
using Microsoft.EntityFrameworkCore;
using Models;
using service.Interface;

namespace service;

public class PermissionService : IPermissionService
{
    private readonly AppDbContext _context;

    // Nome da esteira para CentralIT
    private const string EsteiraCentralIT = "Desenvolvimento de Soluções";

    public PermissionService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<string> GetUserPerfilAsync(string email)
    {
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Email == email);

        return user?.Perfil ?? Perfis.Basico;
    }

    public async Task<Unidade?> GetUserUnidadeAsync(string email)
    {
        var user = await _context.Users
            .Include(u => u.Unidade)
            .FirstOrDefaultAsync(u => u.Email == email);

        return user?.Unidade;
    }

    public bool CanCreate(string perfil, string resource)
    {
        return perfil switch
        {
            Perfis.Admin => true,
            Perfis.Gestor => true,
            Perfis.CentralIT => resource is "etapa" or "atividade",
            Perfis.Parceiro => false,
            _ => false // basico ou outros
        };
    }

    public bool CanEdit(string perfil, string resource)
    {
        return perfil switch
        {
            Perfis.Admin => true,
            Perfis.Gestor => true,
            Perfis.CentralIT => resource is "etapa" or "atividade" or "descricao",
            Perfis.Parceiro => false,
            _ => false
        };
    }

    public bool CanDelete(string perfil, string resource)
    {
        return perfil switch
        {
            Perfis.Admin => true,
            Perfis.Gestor => true,
            Perfis.CentralIT => false, // CentralIT NUNCA pode excluir
            Perfis.Parceiro => false,
            _ => false
        };
    }

    public IQueryable<Projeto> GetFilteredProjetosQuery(string perfil, string? unidadeNome)
    {
        var query = _context.Projetos
            .Include(p => p.AREA_DEMANDANTE)
            .Include(p => p.Unidade)
            .Include(p => p.Esteira)
            .Include(p => p.Etapas!)
                .ThenInclude(e => e.Atividades)
            .AsSplitQuery()
            .AsQueryable();

        return perfil switch
        {
            // Admin vê tudo
            Perfis.Admin => query,

            // Gestor vê apenas projetos da sua unidade
            Perfis.Gestor => query.Where(p => p.Unidade != null && p.Unidade.Nome == unidadeNome),

            // CentralIT vê apenas projetos da esteira "Desenvolvimento de Soluções"
            Perfis.CentralIT => query.Where(p => p.Esteira != null && p.Esteira.Nome == EsteiraCentralIT),

            // Parceiro vê apenas projetos onde AreaDemandante.NM_DEMANDANTE == User.Unidade.Nome
            Perfis.Parceiro => query.Where(p => p.AREA_DEMANDANTE != null && p.AREA_DEMANDANTE.NM_DEMANDANTE == unidadeNome),

            // Basico e outros - vê apenas projetos da sua unidade (comportamento padrão)
            _ => query.Where(p => p.Unidade != null && p.Unidade.Nome == unidadeNome)
        };
    }
}
