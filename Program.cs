using System.Text;
using Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.Configuration;
using Repositorio;
using DotNetEnv;
using service;
using Microsoft.Identity.Client;

var builder = WebApplication.CreateBuilder(args);

Env.Load();


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

var allowedOrigins =  "*";
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll",
        builder => builder
            .AllowAnyOrigin()
            .AllowAnyHeader()
            .AllowAnyMethod());
});


builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = null;
    });


var clientId = Env.GetString("ClientId");
var clientSecret = Env.GetString("ClientSecret");
var tenantId = Env.GetString("TenantId");


builder.Services.AddScoped<CategoriaRepositorio>();
builder.Services.AddScoped<DemandanteRepositorio>();
builder.Services.AddScoped<DemandaRepositorio>();
builder.Services.AddScoped<ProjetoRepositorio>();
builder.Services.AddScoped<EtapaRepositorio>();
builder.Services.AddScoped<EtapaService>();
builder.Services.AddScoped<ProjetoService>();
builder.Services.AddScoped<DetalhamentoRepositorio>();
builder.Services.AddSingleton<IConfidentialClientApplication>(sp =>
{
    return ConfidentialClientApplicationBuilder.Create(clientId)
        .WithClientSecret(clientSecret)
        .WithAuthority(new Uri($"https://login.microsoftonline.com/{tenantId}"))
        .WithRedirectUri($"{Env.GetString("UrlBack")}/api/auth/callback")
        .Build();
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();


var key = Encoding.UTF8.GetBytes(Env.GetString("JWT_KEY") ?? throw new InvalidOperationException("Chave JWT não configurada."));

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
            IssuerSigningKey = new SymmetricSecurityKey(key)
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.UseHttpsRedirection();
app.UseCors("AllowAll");
app.UseAuthentication(); 
app.UseAuthorization();  
app.MapControllers();
app.Run();
