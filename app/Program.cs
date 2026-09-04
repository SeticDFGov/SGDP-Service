using System.Security.Claims;
using System.Text.Json;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.JsonWebTokens;
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
using app.Auth;
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
                // O front lê o nome do anexo no download (aditivo: só expõe
                // um header de RESPOSTA ao JS; nada muda para chamadas atuais)
                .WithExposedHeaders("Content-Disposition")
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
                .WithExposedHeaders("Content-Disposition")
                .AllowCredentials());
    }
});

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = null;
        options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
        // Adiciona conversor de datas para sempre retornar/receber datas no horário de Brasília
        options.JsonSerializerOptions.Converters.Add(new demanda_service.Helpers.BrasiliaDateTimeConverter());
        options.JsonSerializerOptions.Converters.Add(new demanda_service.Helpers.BrasiliaDateTimeNullableConverter());
    });

builder.Services.AddHttpClient("keycloak");

// Repositórios
builder.Services.AddScoped<IDemandanteRepositorio, DemandanteRepositorio>();
builder.Services.AddScoped<IDemandaRepositorio, DemandaRepositorio>();
builder.Services.AddScoped<IEtapaRepositorio, EtapaRepositorio>();
builder.Services.AddScoped<IAreaExecutoraRepositorio, AreaExecutoraRepositorio>();
builder.Services.AddScoped<IAuthRepositorio, AuthRepositorio>();
builder.Services.AddScoped<IEsteiraRepositorio, EsteiraRepositorio>();

// Repositórios do módulo PGIA
builder.Services.AddScoped<IPgiaOrgaoRepositorio, Repositorio.Pgia.PgiaOrgaoRepositorio>();
builder.Services.AddScoped<IPgiaDesignacaoRepositorio, Repositorio.Pgia.PgiaDesignacaoRepositorio>();
builder.Services.AddScoped<IPgiaPrazoRepositorio, Repositorio.Pgia.PgiaPrazoRepositorio>();
builder.Services.AddScoped<IPgiaSistemaRepositorio, Repositorio.Pgia.PgiaSistemaRepositorio>();
builder.Services.AddScoped<IPgiaGovernancaRepositorio, Repositorio.Pgia.PgiaGovernancaRepositorio>();
builder.Services.AddScoped<IPgiaOperacaoRepositorio, Repositorio.Pgia.PgiaOperacaoRepositorio>();
builder.Services.AddScoped<IPgiaContratoRepositorio, Repositorio.Pgia.PgiaContratoRepositorio>();
builder.Services.AddScoped<IPgiaRelatorioRepositorio, Repositorio.Pgia.PgiaRelatorioRepositorio>();
builder.Services.AddScoped<IPgiaPublicoRepositorio, Repositorio.Pgia.PgiaPublicoRepositorio>();
builder.Services.AddScoped<IPgiaDocumentoRepositorio, Repositorio.Pgia.PgiaDocumentoRepositorio>();

// Serviços
builder.Services.AddScoped<IDemandaService, DemandaService>();
builder.Services.AddScoped<IEtapaService, EtapaService>();
builder.Services.AddScoped<IEsteiraService, EsteiraService>();
builder.Services.AddScoped<IPermissionService, PermissionService>();

// Serviços do módulo PGIA
builder.Services.AddScoped<IPgiaPermissionService, service.Pgia.PgiaPermissionService>();
builder.Services.AddScoped<IPgiaAdminService, service.Pgia.PgiaAdminService>();
builder.Services.AddScoped<IPgiaOrgaoService, service.Pgia.PgiaOrgaoService>();
builder.Services.AddScoped<IPgiaSistemaService, service.Pgia.PgiaSistemaService>();
builder.Services.AddScoped<IPgiaGovernancaService, service.Pgia.PgiaGovernancaService>();
builder.Services.AddScoped<IPgiaOperacaoService, service.Pgia.PgiaOperacaoService>();
builder.Services.AddScoped<IPgiaContratoService, service.Pgia.PgiaContratoService>();
builder.Services.AddScoped<IPgiaRelatorioService, service.Pgia.PgiaRelatorioService>();
builder.Services.AddScoped<IPgiaPublicoService, service.Pgia.PgiaPublicoService>();
builder.Services.AddScoped<IPgiaDocumentoService, service.Pgia.PgiaDocumentoService>();
// Anexos gravados no próprio banco (bytea em pgia_documento_arquivo), por decisão
// do responsável pelo sistema. Trocar pelo futuro servidor de arquivos da Infra é
// só registrar outra implementação de IPgiaArquivoStorage aqui.
builder.Services.AddScoped<IPgiaArquivoStorage, service.Pgia.PgiaArquivoStorage>();

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

// MODO LOCAL (execução independente para testes, sem Keycloak): opt-in explícito
// via Auth:ModoLocal=true e proibido fora de Development — a aplicação nem sobe.
var modoLocal = builder.Configuration.GetValue<bool>("Auth:ModoLocal");
if (modoLocal && !builder.Environment.IsDevelopment())
{
    throw new InvalidOperationException(
        "Auth:ModoLocal=true só é permitido em ASPNETCORE_ENVIRONMENT=Development.");
}

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        if (modoLocal)
        {
            // Valida os tokens emitidos pelo AuthLocalController (chave por processo);
            // todo o restante do pipeline (roles, /me, perfis) permanece idêntico.
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = ModoLocalTokens.Issuer,
                IssuerSigningKey = ModoLocalTokens.Key,
                ValidateAudience = false,
                ValidateLifetime = true,
            };
        }
        else
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
        }
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = context =>
            {
                var identity = context.Principal?.Identity as ClaimsIdentity;
                if (identity == null) return Task.CompletedTask;

                try
                {
                    JsonElement resourceAccess = default;

                    // .NET 8 usa JsonWebToken — lê diretamente do payload
                    if (context.SecurityToken is JsonWebToken jwt)
                        jwt.TryGetPayloadValue(keycloakClientId == null ? "" : "resource_access", out resourceAccess);

                    // Fallback: tenta via claim serializado
                    if (resourceAccess.ValueKind == JsonValueKind.Undefined)
                    {
                        var raw = identity.FindFirst("resource_access")?.Value;
                        if (!string.IsNullOrEmpty(raw))
                            resourceAccess = JsonSerializer.Deserialize<JsonElement>(raw);
                    }

                    if (resourceAccess.ValueKind != JsonValueKind.Undefined &&
                        resourceAccess.TryGetProperty(keycloakClientId, out var client) &&
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

                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

// Política usada SÓ pela superfície anônima do PGIA ([EnableRateLimiting] no
// PgiaPublicoController); nenhum endpoint existente passa pelo limitador.
// Atrás do proxy o IP real vem no X-Forwarded-For (espoofável — é atrito contra
// abuso, não fronteira de segurança; a credencial real é a entropia do protocolo).
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("pgia-publico", httpContext =>
    {
        var ip = httpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault()?.Split(',')[0].Trim();
        if (string.IsNullOrEmpty(ip))
            ip = httpContext.Connection.RemoteIpAddress?.ToString() ?? "anon";

        return RateLimitPartition.GetFixedWindowLimiter(ip, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 30,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0
        });
    });
});

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.UseHttpsRedirection();
app.UseCors("CorsPolicy");
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.MapControllers();
app.Run();
