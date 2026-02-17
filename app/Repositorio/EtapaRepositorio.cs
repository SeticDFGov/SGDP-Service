using Microsoft.EntityFrameworkCore;
using Models;
using Repositorio.Interface;

namespace Repositorio;

/// <summary>
/// Repositório para acesso a dados de Etapa (apenas queries simples)
/// Lógica de negócio deve estar em EtapaService
/// </summary>
public class EtapaRepositorio : IEtapaRepositorio
{
    private readonly AppDbContext _context;

    public EtapaRepositorio(AppDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Busca etapas por ID do projeto (data access only)
    /// </summary>
    public async Task<List<Etapa>> GetEtapasByProjetoIdAsync(int projetoId)
    {
        return await _context.Etapas
            .Where(e => e.NM_PROJETO.projetoId == projetoId)
            .ToListAsync();
    }

    /// <summary>
    /// Busca etapa por ID (data access only)
    /// </summary>
    public async Task<Etapa?> GetByIdAsync(int id)
    {
        return await _context.Etapas
            .FirstOrDefaultAsync(e => e.EtapaProjetoId == id);
    }

    /// <summary>
    /// Busca projeto por ID (data access only)
    /// </summary>
    public async Task<Projeto?> GetProjetoByIdAsync(int projetoId)
    {
        return await _context.Projetos
            .FirstOrDefaultAsync(p => p.projetoId == projetoId);
    }

    /// <summary>
    /// Adiciona uma etapa ao contexto
    /// </summary>
    public void Add(Etapa etapa)
    {
        _context.Etapas.Add(etapa);
    }

    /// <summary>
    /// Salva as mudanças no banco de dados
    /// </summary>
    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }
}