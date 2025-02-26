using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Categoria.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CategoriaController : ControllerBase
    {
        private readonly CategoriaService _catservice;

        public CategoriaController(CategoriaService catservice)
        {
            _catservice = catservice;
        }

        // GET api/demandante/items
        [HttpGet("items")]
        public async Task<ActionResult<List<IDictionary<string, object>>>> GetAllItems()
        {
            try
            {
                var items = await _catservice.GetCategoriaListItemsAsync();
                return Ok(items);  // Retorna os itens como JSON
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Erro ao obter itens: {ex.Message}");
            }
        }

        // GET api/demandante/items/{id}
        [HttpGet("items/{id}")]
        public async Task<ActionResult<IDictionary<string, object>>> GetItemById(string id)
        {
            try
            {
                var item = await _catservice.GetCategoriaListItemByIdAsync(id);
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
                var result = await _catservice.CreateCategoriaAsync(fields);
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


        // DELETE api/demandante/items/{id}
        [HttpDelete("items/{id}")]
        public async Task<ActionResult> DeleteItem(string id)
        {
            try
            {
                var result = await _catservice.DeleteCategoriaAsync(id);
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
