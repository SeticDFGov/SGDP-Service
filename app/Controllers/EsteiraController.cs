using Microsoft.AspNetCore.Mvc;
using Models;
using service.Interface;
using System;
using System.Threading.Tasks;

namespace Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class EsteiraController : ControllerBase
    {
        private readonly IEsteiraService _service;
        public EsteiraController(IEsteiraService service)
        {
            _service = service;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var esteiras = await _service.GetAllEsteirasAsync();
            return Ok(esteiras);
        }

        [HttpPost]
        public async Task<IActionResult> Add([FromBody] Esteira esteira)
        {
            await _service.AddEsteiraAsync(esteira);
            return Ok();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            await _service.DeleteEsteiraAsync(id);
            return Ok();
        }
    }
} 