using Microsoft.AspNetCore.Mvc;
using Microsoft.Identity.Client;
using Microsoft.Graph;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Microsoft.Kiota.Abstractions.Authentication;
using Microsoft.Graph.Models;
using service.consts;

namespace Auth.Controllers
{
    [ApiController]
    [Route("api/auth")]
    public class AuthController : ControllerBase
    {
        private readonly IConfidentialClientApplication _clientApp;
        private readonly IConfiguration _configuration;
        private readonly string[] _scopes = new[] { "https://graph.microsoft.com/.default" }; // Escopo adequado para Graph API

        public AuthController(IConfidentialClientApplication clientApp, IConfiguration configuration)
        {
            _clientApp = clientApp;
            _configuration = configuration;
        }

        [HttpGet("login")]
        public async Task<IActionResult> Login()
        {
            try
            {
                var authUrl = await _clientApp.GetAuthorizationRequestUrl(_scopes)
                    .WithRedirectUri($"{Environment.GetEnvironmentVariable("UrlBack")}/api/auth/callback")
                    .ExecuteAsync();

                return Redirect(authUrl.ToString());
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = "Erro ao gerar a URL de login.", details = ex.Message });
            }
        }

        [HttpGet("callback")]
        public async Task<IActionResult> Callback([FromQuery] string code)
        {
            if (string.IsNullOrEmpty(code))
            {
                return BadRequest(new { error = "Código de autorização inválido." });
            }

            try
            {
                // Troca o código pelo token de acesso
                var result = await _clientApp.AcquireTokenByAuthorizationCode(_scopes, code)
                .ExecuteAsync(); // Não precisa de WithRedirectUri aqui
                string accessToken = result.AccessToken;

                // Configuração do GraphServiceClient com BaseBearerTokenAuthenticationProvider
                var authProvider = new BaseBearerTokenAuthenticationProvider(new TokenProvider(accessToken));
                var graphClient = new GraphServiceClient(authProvider);

                // Obtém as informações do usuário logado
                var user = await graphClient.Me.GetAsync();
                var admins = new AcessSGD{};
                bool check = false;

                foreach (var admin in admins.UsersSGD){
                    if(admin.Contains(user.DisplayName)){
                        check = true;
                    }
                       
                }

                if(!check)
                    return Unauthorized("Usuário não possui elevação suficiente");


                

                // Gera um JWT personalizado
                var tokenHandler = new JwtSecurityTokenHandler();
                var key = Encoding.UTF8.GetBytes(Environment.GetEnvironmentVariable("Key") ?? throw new InvalidOperationException("Chave JWT não configurada."));
                var tokenDescriptor = new SecurityTokenDescriptor
                {
                    Subject = new ClaimsIdentity(new[]
                    {
                        new Claim(ClaimTypes.Name, user.DisplayName ?? "Unknown"),
                        new Claim(ClaimTypes.Email, user.Mail ?? "Unknown"),
                        new Claim("id", user.Id ?? "Unknown")
                    }),
                    Expires = DateTime.UtcNow.AddHours(1),
                    
                    // Issuer = _configuration["Jwt:Issuer"],
                    Issuer  = Environment.GetEnvironmentVariable("Issuer"),
                    // Audience = _configuration["Jwt:Audience"],
                    Audience  = Environment.GetEnvironmentVariable("Audience"),
                    SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
                };

                var token = tokenHandler.CreateToken(tokenDescriptor);
                var jwt = tokenHandler.WriteToken(token);
                return Redirect($"{Environment.GetEnvironmentVariable("UrlFront")}/auth/callback?token={jwt}&name={Uri.EscapeDataString(user.DisplayName)}");
                // Retorna o JWT para o frontend
              
            }
            catch (MsalException msalEx)
            {
                return BadRequest(new { error = "Erro na autenticação com a Microsoft.", details = msalEx.Message });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = "Erro ao processar a autenticação.", details = ex.Message });
            }
        }
    }

    // Classe para fornecer o token ao GraphServiceClient
    public class TokenProvider : IAccessTokenProvider
    {
        private readonly string _accessToken;

        public TokenProvider(string accessToken)
        {
            _accessToken = accessToken;
        }

        public Task<string> GetAuthorizationTokenAsync(Uri uri, Dictionary<string, object> additionalAuthenticationContext = default, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_accessToken);
        }

        public AllowedHostsValidator AllowedHostsValidator => new AllowedHostsValidator();
    }
}
