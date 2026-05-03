using System.Security.Claims;
using System.Text.Json;
using Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Repositorio;
using DotNetEnv;
using service.Interface;
using service;
using Repositorio.Interface;
using api.Auth;
using demanda_service.service;
using Microsoft.OpenApi.Models;
using QuestPDF.Infrastructure;

var builder = WebApplication.CreateBuilder(args);
QuestPDF.Settings.License = LicenseType.Community;
Env.Load();

var mode = Environment.GetEnvironmentVariable("MODE");
var connectionString = mode == "container" ? "PostgreSqlDocker" : "PostgreSql";

if (string.IsNullOrEmpty(connectionString))
{
    throw new InvalidOperationException("Connection string not found.");
}

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString(connectionString)));

builder.Services.AddCors(options =>
{
    if (builder.Environment.IsDevelopment())
    {
        options.AddPolicy("CorsPolicy",
            corsBuilder => corsBuilder
                .WithOrigins(
                    "http://localhost:4200",
                    "http://localhost:5148",
                    "https://localhost:5148",
                    "http://localhost:3000"
                )
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials());
    }
    else
    {
        options.AddPolicy("CorsPolicy",
            corsBuilder => corsBuilder
                .WithOrigins(
                    "https://subgd.df.gov.br",
                    "https://subgd.setic.df.gov.br",
                    "https://subgd-hom.df.gov.br",
                    "https://subgd-api.df.gov.br"
                )
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials());
    }
});

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = null;
        options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
    });

builder.Services.AddHttpClient("keycloak");

// Repositórios
builder.Services.AddScoped<IDemandanteRepositorio, DemandanteRepositorio>();
builder.Services.AddScoped<IDemandaRepositorio, DemandaRepositorio>();
builder.Services.AddScoped<IEtapaRepositorio, EtapaRepositorio>();
builder.Services.AddScoped<IAreaExecutoraRepositorio, AreaExecutoraRepositorio>();
builder.Services.AddScoped<IAuthRepositorio, AuthRepositorio>();
builder.Services.AddScoped<IEsteiraRepositorio, EsteiraRepositorio>();

// Serviços
builder.Services.AddScoped<IDemandaService, DemandaService>();
builder.Services.AddScoped<IEtapaService, EtapaService>();
builder.Services.AddScoped<IEsteiraService, EsteiraService>();
builder.Services.AddScoped<IPermissionService, PermissionService>();
builder.Services.AddScoped<HttpClient>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "API-SUBGD", Version = "v1" });

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

var authSettingsSection = builder.Configuration.GetSection(AuthSettings.SectionName);
builder.Services.Configure<AuthSettings>(authSettingsSection);
var keycloakAuthority = authSettingsSection["Authority"]!;
var keycloakClientId = authSettingsSection["ClientId"]!;

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = keycloakAuthority;
        options.RequireHttpsMetadata = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = keycloakAuthority,
            ValidateAudience = false,
            ValidateLifetime = true,
        };
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = context =>
            {
                var identity = context.Principal?.Identity as ClaimsIdentity;
                if (identity == null) return Task.CompletedTask;
                var resourceAccessClaim = identity.FindFirst("resource_access")?.Value;
                if (!string.IsNullOrEmpty(resourceAccessClaim))
                {
                    try
                    {
                        var ra = JsonSerializer.Deserialize<JsonElement>(resourceAccessClaim);
                        if (ra.TryGetProperty(keycloakClientId, out var client) &&
                            client.TryGetProperty("roles", out var roles))
                        {
                            foreach (var role in roles.EnumerateArray())
                            {
                                var r = role.GetString();
                                if (!string.IsNullOrEmpty(r))
                                    identity.AddClaim(new Claim(ClaimTypes.Role, r));
                            }
                        }
                    }
                    catch { }
                }
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.UseHttpsRedirection();
app.UseCors("CorsPolicy");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();
