using api.Projeto;
using Models;
using Repositorio;
using Repositorio.Interface;

namespace service;

public class ProjetoService
{
    public readonly IProjetoRepositorio _projetoRepositorio;
    public readonly AppDbContext _context;
    public ProjetoService(IProjetoRepositorio projetoRepositorio, AppDbContext context)
    {
        _projetoRepositorio = projetoRepositorio;
        _context = context;
    }
    
    public async Task<QuantidadeProjetoDTO> GetQuantidadeProjetos()
    {
        var quantidadeSUBTDCR = _context.Projetos.Where(p => p.UNIDADE == "SUBTDCR" || p.UNIDADE == "").Count();
        var quantidadeSUBSIS = _context.Projetos.Where(p => p.UNIDADE == "SUBSIS").Count();

        var quantidadeSUBINFRA =  _context.Projetos.Where(p => p.UNIDADE == "SUBINFRA").Count();

        return new QuantidadeProjetoDTO { SUBTDCR = quantidadeSUBTDCR, SUBSIS = quantidadeSUBSIS, SUBINFRA = quantidadeSUBINFRA };
    }
    
}