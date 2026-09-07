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
            _ => false
        };
    }

    public bool CanEdit(string perfil, string resource)
    {
        return perfil switch
        {
            Perfis.Admin => true,
            Perfis.Gestor => true,
            _ => false
        };
    }

    public bool CanDelete(string perfil, string resource)
    {
        return perfil switch
        {
            Perfis.Admin => true,
            Perfis.Gestor => true,
            _ => false
        };
    }

    public IQueryable<Demanda> GetFilteredDemandasQuery(string perfil, string? unidadeNome)
    {
        return _context.Demandas
            .Include(d => d.AREA_DEMANDANTE)
            .Include(d => d.Esteira)
            .Include(d => d.Entregaveis!)
                .ThenInclude(e => e.Responsavel)
            .AsSplitQuery()
            .AsQueryable();
    }
}
