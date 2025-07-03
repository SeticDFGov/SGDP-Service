using Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace service.Interface
{
    public interface IDespachoService
    {
        Task CriarDespachoAsync(Despacho despacho);
        Task<List<Despacho>> ListarDespachosPorProjetoAsync(int projetoId);
        Task<Despacho?> ObterUltimoDespachoPorProjetoAsync(int projetoId);
    }
} 