using Microsoft.AspNetCore.Mvc;
using Models;
using service.Interface;
using api.Despacho;
using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
namespace Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class DespachoController : ControllerBase
    {
        private readonly IDespachoService _service;
        private readonly AppDbContext _context;
        public DespachoController(IDespachoService service, AppDbContext context)
        {
            _service = service;
            _context = context;
        }

        [HttpPost]
        public async Task<IActionResult> Criar([FromBody] DespachoCreateDTO dto)
        {
            var projeto = await _context.Projetos.FindAsync(dto.ProjetoId);
            if (projeto == null) return BadRequest("Projeto não encontrado");

            var brasilia = TimeZoneInfo.FindSystemTimeZoneById("E. South America Standard Time");
            var dataEntradaUtc = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(dto.DataEntrada, DateTimeKind.Unspecified), brasilia);
            DateTime? dataSaidaUtc = null;
            if (dto.DataSaida != null)
            {
                dataSaidaUtc = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(dto.DataSaida, DateTimeKind.Unspecified), brasilia);
            }

            var despacho = new Despacho
            {
                Projeto = projeto,
                NomeOrgao = dto.NomeOrgao,
                DataEntrada = dataEntradaUtc,
                DataSaida = dataSaidaUtc
            };
            await _service.CriarDespachoAsync(despacho);
            return Ok();
        }

        [HttpGet("projeto/{projetoId}")]
        public async Task<IActionResult> ListarPorProjeto(int projetoId)
        {
            var despachos = await _service.ListarDespachosPorProjetoAsync(projetoId);
            return Ok(despachos);
        }

        [HttpGet("projeto/{projetoId}/ultimo")]
        public async Task<IActionResult> ObterUltimoPorProjeto(int projetoId)
        {
            var despacho = await _service.ObterUltimoDespachoPorProjetoAsync(projetoId);
            if (despacho == null) return NotFound();
            return Ok(despacho);
        }
    }
} 