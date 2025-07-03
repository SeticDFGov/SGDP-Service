using Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Repositorio.Interface
{
    public interface IDespachoRepositorio
    {
        Task CriarDespachoAsync(Despacho despacho);
        Task<List<Despacho>> ListarDespachosPorProjetoAsync(int projetoId);
        Task<Despacho?> ObterUltimoDespachoPorProjetoAsync(int projetoId);
    }
} 