using Models;
using Microsoft.EntityFrameworkCore;
using Repositorio.Interface;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Repositorio
{
    public class EsteiraRepositorio : IEsteiraRepositorio
    {
        private readonly AppDbContext _context;
        public EsteiraRepositorio(AppDbContext context)
        {
            _context = context;
        }

        public async Task<List<Esteira>> GetAllEsteirasAsync()
        {
            return await _context.Esteiras.ToListAsync();
        }

        public async Task<Esteira?> GetEsteiraByIdAsync(Guid id)
        {
            return await _context.Esteiras.FindAsync(id);
        }

        public async Task AddEsteiraAsync(Esteira esteira)
        {
            _context.Esteiras.Add(esteira);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteEsteiraAsync(Guid id)
        {
            var esteira = await _context.Esteiras.FindAsync(id);
            if (esteira != null)
            {
                _context.Esteiras.Remove(esteira);
                await _context.SaveChangesAsync();
            }
        }
    }
} 