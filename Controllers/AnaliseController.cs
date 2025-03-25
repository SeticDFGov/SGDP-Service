using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Projeto.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AnaliseController : ControllerBase
    {
        private readonly AnaliseService _analiseService;

        public AnaliseController(AnaliseService analiseService)
        {
            _analiseService = analiseService;
        }

        [HttpPut("items/{id}")]
        public async Task<ActionResult> UpdateItem(string id, [FromBody] Dictionary<string, object> fields)
        {
            if (fields == null || fields.Count == 0)
            {
                return BadRequest("Os campos da análise não foram fornecidos.");
            }

            try
            {
                var result = await _analiseService.UpdateAnaliseAsync(id, fields);
                if (result)
                {
                    return NoContent();
                }
                return StatusCode(500, "Erro ao atualizar análise.");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Erro ao atualizar análise {id}: {ex.Message}");
            }
        }

        [HttpGet("{nome_etapa}")]
        public async Task<ActionResult<List<IDictionary<string, object>>>> GetAllItems(string nome_etapa)
        {
            try
            {
                var items = await _analiseService.GetAnaliseListItemsAsync(nome_etapa);
                return Ok(items);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Erro ao obter análises: {ex.Message}");
            }
        }

        [HttpGet("items/{id}")]
        public async Task<ActionResult<IDictionary<string, object>>> GetItemById(string id)
        {
            try
            {
                var item = await _analiseService.GetAnaliseListItemByIdAsync(id);
                if (item == null)
                {
                    return NotFound("Análise não encontrada.");
                }
                return Ok(item);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Erro ao obter análise {id}: {ex.Message}");
            }
        }

        [HttpPost("items")]
        public async Task<ActionResult> CreateItem([FromBody] Dictionary<string, object> fields)
        {
            if (fields == null || fields.Count == 0)
            {
                return BadRequest("Os campos da análise não foram fornecidos.");
            }

            try
            {
                var result = await _analiseService.CreateAnaliseAsync(fields);
                if (result)
                {
                    return Ok(fields);
                }
                return StatusCode(500, "Erro ao criar análise.");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Erro ao criar análise: {ex.Message}");
            }
        }

        [HttpDelete("items/{id}")]
        public async Task<ActionResult> DeleteItem(string id)
        {
            try
            {
                var result = await _analiseService.DeleteAnaliseAsync(id);
                if (result)
                {
                    return NoContent();
                }
                return StatusCode(500, "Erro ao excluir análise.");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Erro ao excluir análise {id}: {ex.Message}");
            }
        }
    }
}
