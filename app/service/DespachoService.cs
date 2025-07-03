using Models;
using Repositorio.Interface;
using service.Interface;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace service
{
    public class DespachoService : IDespachoService
    {
        private readonly IDespachoRepositorio _repo;
        public DespachoService(IDespachoRepositorio repo)
        {
            _repo = repo;
        }

        public Task CriarDespachoAsync(Despacho despacho) => _repo.CriarDespachoAsync(despacho);
        public Task<List<Despacho>> ListarDespachosPorProjetoAsync(int projetoId) => _repo.ListarDespachosPorProjetoAsync(projetoId);
        public Task<Despacho?> ObterUltimoDespachoPorProjetoAsync(int projetoId) => _repo.ObterUltimoDespachoPorProjetoAsync(projetoId);
    }
} 