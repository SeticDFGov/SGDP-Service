using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Projeto.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class EtapaController : ControllerBase
    {
        private readonly EtapaService _etapaService;

        public EtapaController(EtapaService etapaService)
        {
            _etapaService = etapaService;
        }

        [HttpPut("items/{id}")]
        public async Task<ActionResult> UpdateItem(string id, [FromBody] Dictionary<string, object> fields)
        {
            if (fields == null || fields.Count == 0)
            {
                return BadRequest("Os campos da etapa não foram fornecidos.");
            }

            try
            {
                var result = await _etapaService.UpdateEtapaAsync(id, fields);
                if (result)
                {
                    return NoContent();
                }
                return StatusCode(500, "Erro ao atualizar etapa.");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Erro ao atualizar etapa {id}: {ex.Message}");
            }
        }

        [HttpGet("{nome_projeto}")]
        public async Task<ActionResult<List<IDictionary<string, object>>>> GetAllItems(string nome_projeto)
        {
            try
            {
                var items = await _etapaService.GetEtapaListItemsAsync(nome_projeto);
                return Ok(items);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Erro ao obter etapas: {ex.Message}");
            }
        }

        [HttpGet("items/{id}")]
        public async Task<ActionResult<IDictionary<string, object>>> GetItemById(string id)
        {
            try
            {
                var item = await _etapaService.GetEtapaListItemByIdAsync(id);
                if (item == null)
                {
                    return NotFound("Etapa não encontrada.");
                }
                return Ok(item);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Erro ao obter etapa {id}: {ex.Message}");
            }
        }

        [HttpPost("items")]
        public async Task<ActionResult> CreateItem([FromBody] Dictionary<string, object> fields)
        {
            if (fields == null || fields.Count == 0)
            {
                return BadRequest("Os campos da etapa não foram fornecidos.");
            }

            try
            {
                var result = await _etapaService.CreateEtapaAsync(fields);
                if (result)
                {
                    return CreatedAtAction(nameof(GetAllItems), new { }, fields);
                }
                return StatusCode(500, "Erro ao criar etapa.");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Erro ao criar etapa: {ex.Message}");
            }
        }

        [HttpDelete("items/{id}")]
        public async Task<ActionResult> DeleteItem(string id)
        {
            try
            {
                var result = await _etapaService.DeleteEtapaAsync(id);
                if (result)
                {
                    return NoContent();
                }
                return StatusCode(500, "Erro ao excluir etapa.");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Erro ao excluir etapa {id}: {ex.Message}");
            }
        }
    }
}
