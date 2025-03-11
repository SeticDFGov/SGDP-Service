using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Projeto.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ProjetoController : ControllerBase
    {
        private readonly ProjetoService _projetoService;

        public ProjetoController(ProjetoService projetoService)
        {
            _projetoService = projetoService;
        }
        [HttpPut("items/{id}")]
        public async Task<ActionResult> UpdateItem(string id, [FromBody] Dictionary<string, object> fields)
        {
            if (fields == null || fields.Count == 0)
            {
                return BadRequest("Os campos do projeto não foram fornecidos.");
            }

            try
            {
                var result = await _projetoService.UpdateProjetoAsync(id, fields);
                if (result)
                {
                    return NoContent();
                }
                return StatusCode(500, "Erro ao atualizar projeto.");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Erro ao atualizar projeto {id}: {ex.Message}");
            }
        }
        // GET api/projeto/items
        [HttpGet("items")]
        public async Task<ActionResult<List<IDictionary<string, object>>>> GetAllItems()
        {
            try
            {
                var items = await _projetoService.GetProjetoListItemsAsync();
                return Ok(items);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Erro ao obter projetos: {ex.Message}");
            }
        }

        // GET api/projeto/items/{id}
        [HttpGet("items/{id}")]
        public async Task<ActionResult<IDictionary<string, object>>> GetItemById(string id)
        {
            try
            {
                var item = await _projetoService.GetProjetoListItemByIdAsync(id);
                if (item == null)
                {
                    return NotFound("Projeto não encontrado.");
                }
                return Ok(item);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Erro ao obter projeto {id}: {ex.Message}");
            }
        }

        // POST api/projeto/items
        [HttpPost("items")]
        public async Task<ActionResult> CreateItem([FromBody] Dictionary<string, object> fields)
        {
            if (fields == null || fields.Count == 0)
            {
                return BadRequest("Os campos do projeto não foram fornecidos.");
            }

            try
            {
                var result = await _projetoService.CreateProjetoAsync(fields);
                if (result)
                {
                    return CreatedAtAction(nameof(GetAllItems), new { }, fields);
                }
                return StatusCode(500, "Erro ao criar projeto.");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Erro ao criar projeto: {ex.Message}");
            }
        }

        // DELETE api/projeto/items/{id}
        [HttpDelete("items/{id}")]
        public async Task<ActionResult> DeleteItem(string id)
        {
            try
            {
                var result = await _projetoService.DeleteProjetoAsync(id);
                if (result)
                {
                    return NoContent();
                }
                return StatusCode(500, "Erro ao excluir projeto.");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Erro ao excluir projeto {id}: {ex.Message}");
            }
        }
    }
}
