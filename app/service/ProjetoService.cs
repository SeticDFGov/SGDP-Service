using api.Projeto;
using Microsoft.EntityFrameworkCore;
using Models;
using Repositorio;
using Repositorio.Interface;
using service;
using service.Interface;

namespace demanda_service.service;

public class ProjetoService : IProjetoService
{
    public readonly IProjetoRepositorio _projetoRepositorio;
    public readonly AppDbContext _context;

    public ProjetoService(IProjetoRepositorio projetoRepositorio,
        AppDbContext context)
    {
        _projetoRepositorio = projetoRepositorio;
        _context = context;
    }

    public async Task<List<Projeto>> GetProjetoListItemsAsync(string unidade)
    {
        var result = await _projetoRepositorio.GetProjetoListItemsAsync(unidade);
        return result.Count==0 ? throw new ApiException(ErrorCode.ProjetoNaoEncontrado): result;;
    }

    public async Task<Projeto> GetProjetoById(int id)
    {
        return await _projetoRepositorio.GetProjetoById(id) ?? throw new ApiException(ErrorCode.ProjetoNaoEncontrado);
    }

    public async Task CreateProjetoByTemplate(Projeto projeto)
    {
        using (var transaction = await _context.Database.BeginTransactionAsync())
        {
            try
            {

                _context.Projetos.Add(projeto);
                await _context.SaveChangesAsync();

                List<Template> templates = await _context.Templates
                    .Where(c => c.NM_TEMPLATE == projeto.TEMPLATE)
                    .ToListAsync();

                if (templates.Count == 0)
                {
                    await transaction.CommitAsync();
                    return;
                }

                _context.Attach(projeto);

                foreach (Template template in templates)
                {
                    var etapa = new Etapa
                    {
                        NM_ETAPA = template.NM_ETAPA,
                        NM_PROJETO = projeto,
                        PERCENT_TOTAL_ETAPA = template.PERCENT_TOTAL,
                        DIAS_PREVISTOS = template.DIAS_PREVISTOS,
                        Order = template.ORDER
                    };

                    _context.Etapas.Add(etapa);
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                throw new Exception("Erro ao salvar o projeto e as etapas: " + ex.Message, ex);
            }

        }
    }

    public async Task<QuantidadeProjetoDTO> GetQuantidadeProjetos()
        {
            var quantidadeSUBTDCR =
                _context.Projetos.Where(p => p.Unidade.Nome == "SUBTDCR" || p.Unidade.Nome == "").Count();
            var quantidadeSUBSIS = _context.Projetos.Where(p => p.Unidade.Nome == "SUBSIS").Count();

            var quantidadeSUBINFRA = _context.Projetos.Where(p => p.Unidade.Nome == "SUBINFRA").Count();

            return new QuantidadeProjetoDTO
                { SUBTDCR = quantidadeSUBTDCR, SUBSIS = quantidadeSUBSIS, SUBINFRA = quantidadeSUBINFRA };
        }

    
}