using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Detalhamento.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class DetalhamentoController : ControllerBase
    {
        
        private readonly DetalhamentoService _detalheservice;

        public DetalhamentoController(DetalhamentoService detalheservice)
        {
            _detalheservice = detalheservice;
        }

        // GET api/demandante/items
        [HttpPost("detalhes")]
        public async Task<ActionResult<List<IDictionary<string, object>>>> GetAllDetalhamentos([FromBody] DemandaRequest nameDemanda)
        {
            try
            {
                var items = await _detalheservice.GetDetalhesListItemsAsync(nameDemanda.NameDemanda);
                return Ok(items);  // Retorna os itens como JSON
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Erro ao obter itens: {ex.Message}");
            }
        }
        public class DemandaRequest
        {
            public string NameDemanda { get; set; }
        }
        // GET api/demandante/items/{id}
        [HttpGet("items/{id}")]
        public async Task<ActionResult<IDictionary<string, object>>> GetItemById(string id)
        {
            try
            {
                var item = await _detalheservice.GetDetalhesListItemByIdAsync(id);
                if (item == null)
                {
                    return NotFound($"Demandante não encontrado.");
                }
                return Ok(item);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Erro ao obter item {id}: {ex.Message}");
            }
        }

        // POST api/demandante/items
       [HttpPost("items")]
        public async Task<ActionResult> CreateItem([FromBody] Dictionary<string, object> fields)
        {
            if (fields == null || fields.Count == 0)
            {
                return BadRequest("Os campos do item não foram fornecidos.");
            }

            try
            {
                var result = await _detalheservice.CreateDetalhamentoAsync(fields);
                if (result)
                {
                    return CreatedAtAction(nameof(GetAllDetalhamentos), new { }, fields);
                }
                return StatusCode(500, "Erro ao criar item.");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Erro ao criar item: {ex.Message}");
            }
        }


        
         
    }
}
