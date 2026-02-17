using api.Common;
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
    public readonly IEtapaService _etapaService;
    public ProjetoService(IProjetoRepositorio projetoRepositorio,
        IEtapaService etapaService,
        AppDbContext context)
    {
        _projetoRepositorio = projetoRepositorio;
        _context = context;
        _etapaService = etapaService;

    }
    public async Task<string> CalculoSituacaoProjeto(int id)
    {
        var etapas = await _etapaService.GetEtapaListItemsAsync(id);
        if (etapas == null || !etapas.Any())
        {
            return "Não Iniciado";
        }

        bool temAtraso = etapas.Any(e => e.SITUACAO == "Atrasado");
        if (temAtraso) return "Atrasado";

        bool emAndamento = etapas.Any(e => e.SITUACAO == "Em Andamento");

        if (emAndamento) return "Em Andamento";
        bool tudoConcluido = etapas.All(e => e.SITUACAO == "Concluído");

        if (tudoConcluido) return "Concluído";

        return "Não Iniciado";
    }

    public async Task<List<Projeto>> GetProjetoListItemsAsync(string unidade)
    {
        // 1. Busca os dados com eager loading (Etapas já incluídas via Include)
        var projetos = await _projetoRepositorio.GetProjetoListItemsAsync(unidade);

        // 2. Validação de existência
        if (projetos == null || projetos.Count == 0)
        {
            throw new ApiException(ErrorCode.ProjetoNaoEncontrado);
        }

        // 3. Calcula situação em memória usando etapas já carregadas (SEM query adicional)
        foreach (var projeto in projetos)
        {
            // Usa as etapas já carregadas via Include (evita N+1 query)
            var etapas = projeto.Etapas ?? new List<Etapa>();

            if (!etapas.Any())
            {
                projeto.SITUACAO = "Não Iniciado";
                continue;
            }

            // Calcula situação do projeto baseado nas etapas
            projeto.SITUACAO = CalcularSituacaoProjeto(etapas);
        }

        return projetos;
    }

    /// <summary>
    /// Calcula situação do projeto baseado nas situações das etapas (em memória)
    /// </summary>
    private string CalcularSituacaoProjeto(ICollection<Etapa> etapas)
    {
        if (!etapas.Any())
        {
            return "Não Iniciado";
        }

        // Prioridade 1: Atrasado (se qualquer etapa estiver atrasada)
        if (etapas.Any(e => e.SITUACAO == "Atrasado"))
        {
            return "Atrasado";
        }

        // Prioridade 2: Em Andamento
        if (etapas.Any(e => e.SITUACAO == "Em Andamento"))
        {
            return "Em Andamento";
        }

        // Prioridade 3: Concluído (todas etapas concluídas)
        if (etapas.All(e => e.SITUACAO == "Concluído"))
        {
            return "Concluído";
        }

        // Padrão: Não Iniciado
        return "Não Iniciado";
    }

    public async Task<Projeto> GetProjetoById(int id)
    {
        return await _projetoRepositorio.GetProjetoById(id) ?? throw new ApiException(ErrorCode.ProjetoNaoEncontrado);
    }

    /// <summary>
    /// Obtém projetos paginados de uma unidade
    /// </summary>
    public async Task<PagedResponse<Projeto>> GetProjetosPaginatedAsync(string unidade, PagedRequest request)
    {
        // Query base (com Atividades para cálculo de situação)
        var query = _context.Projetos
            .Where(p => p.Unidade.Nome == unidade)
            .Include(p => p.AREA_DEMANDANTE)
            .Include(p => p.Unidade)
            .Include(p => p.Esteira)
            .Include(p => p.Etapas)
                .ThenInclude(e => e.Atividades)
            .AsSplitQuery();

        // Conta total (antes da paginação)
        var totalItems = await query.CountAsync();

        // Aplica paginação
        var projetos = await query
            .Skip(request.Skip)
            .Take(request.PageSize)
            .ToListAsync();

        // Calcula situação para cada projeto (em memória)
        foreach (var projeto in projetos)
        {
            var etapas = projeto.Etapas ?? new List<Etapa>();
            projeto.SITUACAO = etapas.Any() ? CalcularSituacaoProjeto(etapas) : "Não Iniciado";
        }

        // Retorna resposta paginada
        return PagedResponse<Projeto>.Create(projetos, totalItems, request);
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
                    // Cria apenas a estrutura de etapas
                    // As datas e percentuais agora são definidos pelas Atividades
                    var etapa = new Etapa
                    {
                        NM_ETAPA = template.NM_ETAPA,
                        NM_PROJETO = projeto,
                        Order = template.ORDER
                    };

                    _context.Etapas.Add(etapa);

                    // TODO: Criar Atividades padrão para cada etapa baseado no template
                    // (será implementado quando AtividadeService estiver pronto)
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
