using Models;
using Repositorio.Interface;
using service.Interface;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace service
{
    public class EsteiraService : IEsteiraService
    {
        private readonly IEsteiraRepositorio _repo;
        public EsteiraService(IEsteiraRepositorio repo)
        {
            _repo = repo;
        }

        public Task<List<Esteira>> GetAllEsteirasAsync() => _repo.GetAllEsteirasAsync();
        public Task<Esteira?> GetEsteiraByIdAsync(Guid id) => _repo.GetEsteiraByIdAsync(id);
        public Task AddEsteiraAsync(Esteira esteira) => _repo.AddEsteiraAsync(esteira);
        public Task DeleteEsteiraAsync(Guid id) => _repo.DeleteEsteiraAsync(id);
    }
} 