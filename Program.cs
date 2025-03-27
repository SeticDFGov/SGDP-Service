using System.Text;
using Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.Configuration;
using Repositorio;
using DotNetEnv;

var builder = WebApplication.CreateBuilder(args);

// Carregar variáveis do .env (se existir)
Env.Load();

// Construir a configuração usando `appsettings.json` + variáveis de ambiente
var config = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables() // Permite sobrescrever valores com variáveis do sistema
    .Build();

// Carregar string de conexão
var connectionString = $"Host={Env.GetString("DB_HOST")};" +
                       $"Port={Env.GetInt("DB_PORT")};" +
                       $"Database={Env.GetString("DB_NAME")};" +
                       $"Username={Env.GetString("DB_USER")};" +
                       $"Password={Env.GetString("DB_PASS")}";

if (string.IsNullOrEmpty(connectionString))
{
    throw new InvalidOperationException("Connection string not found.");
}

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));

// Configurar CORS
var allowedOrigins = config["CorsPolicy:AllowedOrigins"] ?? "*";
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAllOrigins",
        policy =>
        {
            policy.WithOrigins(allowedOrigins)
                  .AllowAnyMethod()
                  .AllowAnyHeader();
        });
});

// Adicionar Serviços
builder.Services.AddControllers();
builder.Services.AddSingleton<GraphService>();
builder.Services.AddSingleton<DemandanteService>();
builder.Services.AddScoped<CategoriaRepositorio>();
builder.Services.AddScoped<DemandanteRepositorio>();
builder.Services.AddScoped<DemandaRepositorio>();
builder.Services.AddSingleton<DetalhamentoService>();
builder.Services.AddSingleton<EtapaService>();
builder.Services.AddSingleton<AnaliseService>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Adicionar Autenticação e Autorização (JWT)
var jwtKey = Env.GetString("JWT_KEY");
if (string.IsNullOrEmpty(jwtKey))
{
    throw new InvalidOperationException("JWT Key is not configured");
}

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = Env.GetString("JWT_ISSUER"),
            ValidAudience = Env.GetString("JWT_AUDIENCE"),
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };
    });

// Adicionar o serviço de Autorização
builder.Services.AddAuthorization();

var app = builder.Build();

// Configurar Middlewares
app.UseSwagger();
app.UseSwaggerUI();

app.UseHttpsRedirection();
app.UseCors("AllowAllOrigins");
app.UseAuthentication(); // Adicionado para autenticação funcionar corretamente
app.UseAuthorization();  // Adicionado para autorização funcionar corretamente
app.MapControllers();
app.Run();
