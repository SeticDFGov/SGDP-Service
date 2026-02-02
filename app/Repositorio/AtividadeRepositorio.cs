using api.Atividade;
using demanda_service.Repositorio.Interface;
using Microsoft.EntityFrameworkCore;
using Models;

namespace Repositorio;

/// <summary>
/// Repositório para acesso a dados de Atividade
/// Após refatoração: Atividades estão vinculadas a Etapas, não a Projetos
/// </summary>
public class AtividadeRepositorio : IAtividadeRepositorio
{
    private readonly AppDbContext _context;

    public AtividadeRepositorio(AppDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Busca atividades por ID da etapa (data access only)
    /// </summary>
    public async Task<List<Atividade>> GetAtividadesByEtapaIdAsync(int etapaId)
    {
        return await _context.Atividades
            .Where(a => a.EtapaProjetoId == etapaId)
            .OrderBy(a => a.Order)
            .ToListAsync();
    }

    /// <summary>
    /// Busca atividades por ID do projeto (através das etapas)
    /// </summary>
    public async Task<List<Atividade>> GetAtividadesByProjetoIdAsync(int projetoId)
    {
        return await _context.Atividades
            .Include(a => a.Etapa)
            .Where(a => a.Etapa.NM_PROJETO.projetoId == projetoId)
            .OrderBy(a => a.Etapa.Order)
            .ThenBy(a => a.Order)
            .ToListAsync();
    }

    /// <summary>
    /// Busca atividade por ID (data access only)
    /// </summary>
    public async Task<Atividade?> GetByIdAsync(int id)
    {
        return await _context.Atividades
            .Include(a => a.Etapa)
            .FirstOrDefaultAsync(a => a.AtividadeId == id);
    }

    /// <summary>
    /// Busca etapa por ID (data access only)
    /// </summary>
    public async Task<Etapa?> GetEtapaByIdAsync(int etapaId)
    {
        return await _context.Etapas
            .FirstOrDefaultAsync(e => e.EtapaProjetoId == etapaId);
    }

    /// <summary>
    /// Adiciona uma atividade ao contexto
    /// </summary>
    public void Add(Atividade atividade)
    {
        _context.Atividades.Add(atividade);
    }

    /// <summary>
    /// Remove uma atividade do contexto
    /// </summary>
    public void Remove(Atividade atividade)
    {
        _context.Atividades.Remove(atividade);
    }

    /// <summary>
    /// Salva as mudanças no banco de dados
    /// </summary>
    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }

    // ==== MÉTODOS LEGADOS (Reports - manter temporariamente para compatibilidade) ====

    /// <summary>
    /// LEGADO: Iniciar report (manter para compatibilidade com Reports antigos)
    /// </summary>
    public async Task IniciarReport(InicioAtividadeDTO inicioatividadeDTO)
    {
        var projeto = await _context.Projetos
            .FirstOrDefaultAsync(c => c.projetoId == inicioatividadeDTO.NM_PROJETO);

        if (projeto == null)
            throw new Exception("Projeto não encontrado");

        // Busca atividades através das etapas
        var atividades = await GetAtividadesByProjetoIdAsync(inicioatividadeDTO.NM_PROJETO);

        var novoReport = new Report
        {
            descricao = inicioatividadeDTO.descricao,
            Data_criacao = DateTime.UtcNow,
            Data_fim = inicioatividadeDTO.data_fim,
            NM_PROJETO = projeto,
            Atividades = atividades,
            fase = inicioatividadeDTO.fase,
        };

        _context.Reports.Add(novoReport);
        await _context.SaveChangesAsync();
    }

    /// <summary>
    /// LEGADO: Remover atividade (manter para compatibilidade)
    /// </summary>
    public async Task RemoverAtividade(int atividadeId)
    {
        var atividade = await GetByIdAsync(atividadeId);
        if (atividade != null)
        {
            Remove(atividade);
            await SaveChangesAsync();
        }
    }

    /// <summary>
    /// LEGADO: Visualizar atividades (agora busca por projeto através das etapas)
    /// </summary>
    public async Task<List<Atividade>> VisualizarAtividades(int projetoId)
    {
        return await GetAtividadesByProjetoIdAsync(projetoId);
    }

    // Método GerarReportPDF movido para AtividadeReports.cs (controller de reports)
    // Método GerarStatusReportExport pode ser implementado em service dedicado
}
