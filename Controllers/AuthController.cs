
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Auth.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        // POST api/auth/login
        [HttpPost("login")]
        public ActionResult Login([FromBody] LoginModel login)
        {
            // Checando as credenciais
            if (login.Username == "admin" && login.Password == "@admin")
            {
                return Ok(new { message = "Login bem-sucedido!" });
            }
            return Unauthorized(new { message = "Credenciais inválidas" });
        }
    }

    public class LoginModel
    {
        public string Username { get; set; }
        public string Password { get; set; }
    }
}
