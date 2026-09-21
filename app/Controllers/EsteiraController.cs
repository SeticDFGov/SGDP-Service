using app.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Models;
using service.Interface;
using System;
using System.Threading.Tasks;

namespace Controllers
{
    [ApiController]
    [Authorize(Policy = ModulosSgdp.PoliticaDemandas)]
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
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> Add([FromBody] Esteira esteira)
        {
            await _service.AddEsteiraAsync(esteira);
            return Ok();
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> Delete(Guid id)
        {
            await _service.DeleteEsteiraAsync(id);
            return Ok();
        }
    }
} 