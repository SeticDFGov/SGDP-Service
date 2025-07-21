using System.Text;
using Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.Configuration;
using Repositorio;
using DotNetEnv;
using service.Interface;
using service;
using Microsoft.Identity.Client;
using Repositorio.Interface;
using api.Auth;
using Microsoft.OpenApi.Models;
using Interface.Repositorio;

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


builder.Services.AddScoped<ICategoriaRepositorio,CategoriaRepositorio>();
builder.Services.AddScoped<IDemandanteRepositorio,DemandanteRepositorio>();
builder.Services.AddScoped<IDemandaRepositorio, DemandaRepositorio>();
builder.Services.AddScoped<ITemplateRepositorio, TemplateRepositorio>(); 
builder.Services.AddScoped<IProjetoRepositorio, ProjetoRepositorio>();
builder.Services.AddScoped<IEtapaRepositorio, EtapaRepositorio>();
builder.Services.AddScoped<IAuthRepositorio, AuthRepositorio>();
builder.Services.AddScoped<EtapaService>();
builder.Services.AddScoped<ProjetoService>();
builder.Services.AddScoped<HttpClient>();
builder.Services.AddScoped<DetalhamentoRepositorio>();
builder.Services.AddScoped<ConfigAuth>();
builder.Services.AddScoped<IEsteiraRepositorio, EsteiraRepositorio>();
builder.Services.AddScoped<IEsteiraService, EsteiraService>();
builder.Services.AddScoped<IDespachoRepositorio, DespachoRepositorio>();
builder.Services.AddScoped<IDespachoService, DespachoService>();
builder.Services.AddSingleton<IConfidentialClientApplication>(sp =>
{
    return ConfidentialClientApplicationBuilder.Create(clientId)
        .WithClientSecret(clientSecret)
        .WithRedirectUri($"{Env.GetString("UrlBack")}/api/auth/callback")
        .WithAuthority(new Uri($"https://login.microsoftonline.com/{tenantId}"))
        .Build();
});


builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Minha API", Version = "v1" });

    
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "Insira o token JWT no formato: Bearer {seu token}",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                },
                Scheme = "oauth2",
                Name = "Bearer",
                In = ParameterLocation.Header,
            },
            new List<string>()
        }
    });
});



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
