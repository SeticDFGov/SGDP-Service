using Models;
using Microsoft.EntityFrameworkCore;
using Repositorio.Interface;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Repositorio
{
    public class DespachoRepositorio : IDespachoRepositorio
    {
        private readonly AppDbContext _context;
        public DespachoRepositorio(AppDbContext context)
        {
            _context = context;
        }

        public async Task CriarDespachoAsync(Despacho despacho)
        {
            _context.Despachos.Add(despacho);
            await _context.SaveChangesAsync();
        }

        public async Task<List<Despacho>> ListarDespachosPorProjetoAsync(int projetoId)
        {
            return await _context.Despachos
                .Where(d => d.Projeto.projetoId == projetoId)
                .OrderBy(d => d.DataEntrada)
                .ToListAsync();
        }

        public async Task<Despacho?> ObterUltimoDespachoPorProjetoAsync(int projetoId)
        {
            return await _context.Despachos
                .Where(d => d.Projeto.projetoId == projetoId)
                .OrderByDescending(d => d.DataEntrada)
                .FirstOrDefaultAsync();
        }
    }
} 