using Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace service.Interface
{
    public interface IEsteiraService
    {
        Task<List<Esteira>> GetAllEsteirasAsync();
        Task<Esteira?> GetEsteiraByIdAsync(Guid id);
        Task AddEsteiraAsync(Esteira esteira);
        Task DeleteEsteiraAsync(Guid id);
    }
} 