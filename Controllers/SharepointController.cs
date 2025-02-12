using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace YourNamespace.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class SharePointController : ControllerBase
    {
        private readonly GraphService _graphService;

        public SharePointController(GraphService graphService)
        {
            _graphService = graphService;
        }

        // GET api/sharepoint/items
        [HttpGet("items")]
        public async Task<ActionResult<List<IDictionary<string, object>>>> GetAllItems()
        {
            try
            {
                var items = await _graphService.GetSharePointListItemsAsync();
                return Ok(items);  // Retorna os itens como JSON
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Erro ao obter itens: {ex.Message}");
            }
        }

        // GET api/sharepoint/items/{id}
        [HttpGet("items/{id}")]
        public async Task<ActionResult<IDictionary<string, object>>> GetItemById(string id)
        {
            try
            {
                var item = await _graphService.GetSharePointListItemByIdAsync(id);
                if (item == null)
                {
                    return NotFound($"Item com id {id} não encontrado.");
                }
                return Ok(item);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Erro ao obter item {id}: {ex.Message}");
            }
        }

        // POST api/sharepoint/items
       [HttpPost("items")]
        public async Task<ActionResult> CreateItem([FromBody] Dictionary<string, object> fields)
        {
            if (fields == null || fields.Count == 0)
            {
                return BadRequest("Os campos do item não foram fornecidos.");
            }

            try
            {
                var result = await _graphService.CreateSharePointListItemAsync(fields);
                if (result)
                {
                    return CreatedAtAction(nameof(GetAllItems), new { }, fields);
                }
                return StatusCode(500, "Erro ao criar item.");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Erro ao criar item: {ex.Message}");
            }
        }

        // PUT api/sharepoint/items/{id}
        [HttpPut("items/{id}")]
        public async Task<ActionResult> UpdateItem(string id, [FromBody] Dictionary<string, object> fields)
        {
            if (fields == null || fields.Count == 0)
            {
                return BadRequest("Os campos do item não foram fornecidos.");
            }

            try
            {
                var result = await _graphService.UpdateSharePointListItemAsync(id, fields);
                if (result)
                {
                    return NoContent(); // 204 No Content
                }
                return StatusCode(500, "Erro ao atualizar item.");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Erro ao atualizar item {id}: {ex.Message}");
            }
        }

        // DELETE api/sharepoint/items/{id}
        [HttpDelete("items/{id}")]
        public async Task<ActionResult> DeleteItem(string id)
        {
            try
            {
                var result = await _graphService.DeleteSharePointListItemAsync(id);
                if (result)
                {
                    return NoContent(); // 204 No Content
                }
                return StatusCode(500, "Erro ao excluir item.");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Erro ao excluir item {id}: {ex.Message}");
            }
        }
    }
}
