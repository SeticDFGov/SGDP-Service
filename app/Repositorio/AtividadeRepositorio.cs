using api.Atividade;
using demanda_service.Repositorio.Interface;
using Microsoft.EntityFrameworkCore;
using Models;

namespace Repositorio;

public class AtividadeRepositorio:IAtividadeRepositorio
{
    public readonly AppDbContext _context;

    public AtividadeRepositorio(AppDbContext context)
    {
        _context = context;
    }

    public async Task IniciarReport(InicioAtividadeDTO inicioatividadeDTO)
    {
        Projeto projeto =  _context.Projetos.FirstOrDefault(c => c.projetoId == inicioatividadeDTO.NM_PROJETO);
        Report novo_report = new Report
        {
            descricao = inicioatividadeDTO.descricao,
            Data_criacao = DateTime.UtcNow,
            Data_fim = inicioatividadeDTO.data_fim,
            NM_PROJETO = projeto,
            fase = inicioatividadeDTO.fase,
        };
        
        _context.Reports.Add(novo_report);
        await _context.SaveChangesAsync();
    }

    public async Task InserirAtividade(AtividadeDTO atividadeDTO, int reportId)
    {
        Report report = await _context.Reports.FirstOrDefaultAsync(c => c.ReportId == reportId);
        var brasilia = TimeZoneInfo.FindSystemTimeZoneById("E. South America Standard Time");
        Atividade nova = new Atividade
        {
            titulo = atividadeDTO.titulo,
            situacao = situacao.proximo,
            categoria = atividadeDTO.categoria,
            descricao = atividadeDTO.descricao,
            data_termino = atividadeDTO.data_fim = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(atividadeDTO.data_fim, DateTimeKind.Unspecified), brasilia),

            Report = report,
        };
        _context.Atividades.Add(nova);
        await _context.SaveChangesAsync();
    }

    public async Task AlterarAtividade(AtividadeDTO atividadeDTO, int atividadeId)
    {
      
      Atividade atividade = await _context.Atividades.FirstOrDefaultAsync(c => c.AtividadeId == atividadeId);
      var brasilia = TimeZoneInfo.FindSystemTimeZoneById("E. South America Standard Time");
      atividade.situacao = atividadeDTO.situacao;
        atividade.categoria = atividadeDTO.categoria;
        atividade.descricao = atividadeDTO.descricao;
        atividade.data_termino = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(atividadeDTO.data_fim, DateTimeKind.Unspecified), brasilia);

        await _context.SaveChangesAsync();
    }

    public async Task RemoverAtividade(int atividadeId)
    {
        Atividade remove = await _context.Atividades.FirstOrDefaultAsync(c => c.AtividadeId == atividadeId);
        _context.Atividades.Remove(remove);
        await _context.SaveChangesAsync();
    }

   
 
    public async Task<List<Atividade>> VisualizarAtividades(int reportId)
    {
        return await _context.Atividades.Where(c => c.Report.ReportId == reportId).ToListAsync();
    }
}
