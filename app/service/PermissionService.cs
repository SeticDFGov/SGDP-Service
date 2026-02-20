using app.Auth;
using app.Models;
using Microsoft.EntityFrameworkCore;
using Models;
using service.Interface;

namespace service;

public class PermissionService : IPermissionService
{
    private readonly AppDbContext _context;

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
            Perfis.CentralIT => resource is "entregavel",
            Perfis.Parceiro => false,
            _ => false
        };
    }

    public bool CanEdit(string perfil, string resource)
    {
        return perfil switch
        {
            Perfis.Admin => true,
            Perfis.Gestor => true,
            Perfis.CentralIT => resource is "entregavel" or "percentual",
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
            Perfis.CentralIT => false,
            Perfis.Parceiro => false,
            _ => false
        };
    }

    public IQueryable<Demanda> GetFilteredDemandasQuery(string perfil, string? unidadeNome)
    {
        var query = _context.Demandas
            .Include(d => d.AREA_DEMANDANTE)
            .Include(d => d.Esteira)
            .Include(d => d.Entregaveis!)
                .ThenInclude(e => e.Responsavel)
            .AsSplitQuery()
            .AsQueryable();

        return perfil switch
        {
            Perfis.Admin => query,
            Perfis.Gestor => query,
            Perfis.CentralIT => query,
            Perfis.Parceiro => query.Where(d => d.AREA_DEMANDANTE != null && d.AREA_DEMANDANTE.NM_DEMANDANTE == unidadeNome),
            _ => query.Where(d => d.AREA_DEMANDANTE != null && d.AREA_DEMANDANTE.NM_DEMANDANTE == unidadeNome)
        };
    }
}
