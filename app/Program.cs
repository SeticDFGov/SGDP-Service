using System.Security.Claims;
using System.Linq;
using System.Text.Json;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Authentication;
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

// Repositório do módulo Análises de Contratações
builder.Services.AddScoped<ICtrProcessoRepositorio, Repositorio.Contratacoes.CtrProcessoRepositorio>();

// Serviços
builder.Services.AddScoped<IDemandaService, DemandaService>();
builder.Services.AddScoped<IEtapaService, EtapaService>();
builder.Services.AddScoped<IEsteiraService, EsteiraService>();
builder.Services.AddScoped<IPermissionService, PermissionService>();

// Isolamento entre módulos: regra única do acesso (Keycloak + concessões do sistema)
// e a transformação que põe os módulos liberados nas claims de cada requisição
builder.Services.AddScoped<IAcessoModuloService, service.Acesso.AcessoModuloService>();
builder.Services.AddScoped<IPedidoAcessoService, service.Acesso.PedidoAcessoService>();
builder.Services.AddScoped<IClaimsTransformation, ModuloAcessoClaimsTransformation>();

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

// Serviços do módulo Análises de Contratações
builder.Services.AddScoped<ICtrPermissionService, service.Contratacoes.CtrPermissionService>();
builder.Services.AddScoped<ICtrAdminService, service.Contratacoes.CtrAdminService>();
builder.Services.AddScoped<ICtrProcessoService, service.Contratacoes.CtrProcessoService>();
builder.Services.AddScoped<ICtrManifestacaoService, service.Contratacoes.CtrManifestacaoService>();
builder.Services.AddScoped<ICtrImportacaoService, service.Contratacoes.CtrImportacaoService>();

// Serviços do módulo Governança Estratégica (planejamento). O cálculo do acesso de
// cada requisição não passa por eles: nenhuma tabela pe_ é lida fora das actions do
// módulo, das telas de acesso e da fila de pedidos (regra do deploy)
builder.Services.AddScoped<IPePermissionService, service.Planejamento.PePermissionService>();
builder.Services.AddScoped<IPePessoaService, service.Planejamento.PePessoaService>();
builder.Services.AddScoped<IPeModeloService, service.Planejamento.PeModeloService>();
builder.Services.AddScoped<IPeOrgaoService, service.Planejamento.PeOrgaoService>();
// Referenciais e registros (E3): motor de registros, PETIC-DF, deliberações do CGTIC,
// anexos e planilhas
builder.Services.AddScoped<IPeRegistroService, service.Planejamento.PeRegistroService>();
builder.Services.AddScoped<IPePeticService, service.Planejamento.PePeticService>();
builder.Services.AddScoped<IPeDeliberacaoService, service.Planejamento.PeDeliberacaoService>();
builder.Services.AddScoped<IPeArquivoService, service.Planejamento.PeArquivoService>();
builder.Services.AddScoped<IPePlanilhaService, service.Planejamento.PePlanilhaService>();
// PDTIC dos órgãos (E4): o PDTIC, a situação dos passos, o "não se aplica", os temas, o PGIA
// (só leitura) e os comentários
builder.Services.AddScoped<IPePdticService, service.Planejamento.PePdticService>();
builder.Services.AddScoped<IPeComentarioService, service.Planejamento.PeComentarioService>();
// Documento do PDTIC (E5): a prévia resolvida, a cópia do órgão, o PDF com as versões e o
// modelo do documento do administrador
builder.Services.AddScoped<IPeDocumentoService, service.Planejamento.PeDocumentoService>();
builder.Services.AddScoped<IPeDocModeloService, service.Planejamento.PeDocModeloService>();
// Fluxos (E6): os fluxos do guia como modelo, a cópia do órgão, o desenho em SVG e o
// cronograma sugerido do plano de trabalho
builder.Services.AddScoped<IPeFluxoService, service.Planejamento.PeFluxoService>();
// Aprovação (E7): o envio ao CGTIC, a publicação, o encerramento, a revisão e o PDTIC
// aprovado fora do sistema (a decisão do CGTIC fica no serviço das deliberações)
builder.Services.AddScoped<IPePdticAprovacaoService, service.Planejamento.PePdticAprovacaoService>();
// Acompanhamento (E7, rodada B): os ciclos de monitoramento e de avaliação, as grades das ações
// e das medições e o painel do PDTIC (os relatórios RA e RR ficam no serviço do documento)
builder.Services.AddScoped<IPeAcompanhamentoService, service.Planejamento.PeAcompanhamentoService>();
// Modelo inicial (trilha e campos do PDTIC, e os referenciais do DF e do PETIC-DF, com os
// princípios do art. 4º): carregado ao subir, fora das requisições.
// Sem as tabelas (intervalo entre o PR e a migration do merge) só registra no log e
// tenta de novo mais tarde; o boot e os outros módulos seguem normais
builder.Services.AddScoped<service.Planejamento.PeCarregadorModelo>();
builder.Services.AddHostedService<service.Planejamento.PeCarregadorModeloHostedService>();

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
                        var rolesDoToken = roles.EnumerateArray()
                            .Select(role => role.GetString())
                            .Where(r => !string.IsNullOrEmpty(r))
                            .Cast<string>()
                            .ToList();

                        // O perfil do SGDP entra PRIMEIRO (admin > gestor > basico), depois as
                        // demais roles do token (ex. "pgia", que abre o módulo PGIA) — ver
                        // ModulosSgdp.RolesComPerfilPrimeiro. O acesso a cada módulo não
                        // depende do perfil: é a política "modulo:*" que decide.
                        foreach (var r in ModulosSgdp.RolesComPerfilPrimeiro(rolesDoToken))
                            identity.AddClaim(new Claim(ClaimTypes.Role, r));
                    }

                    JsonElement groups = default;
                    if (context.SecurityToken is JsonWebToken jwtGroups)
                        jwtGroups.TryGetPayloadValue("groups", out groups);

                    if (groups.ValueKind == JsonValueKind.Undefined)
                    {
                        var rawGroups = identity.FindFirst("groups")?.Value;
                        if (!string.IsNullOrEmpty(rawGroups))
                            groups = JsonSerializer.Deserialize<JsonElement>(rawGroups);
                    }

                    if (groups.ValueKind == JsonValueKind.Array)
                    {
                        // Claim "groups" traz o nome da unidade diretamente (sem path).
                        var codigo = groups.EnumerateArray()
                            .Select(g => g.GetString())
                            .FirstOrDefault(g => !string.IsNullOrEmpty(g));

                        if (!string.IsNullOrEmpty(codigo))
                            identity.AddClaim(new Claim("unidade_codigo", codigo));
                    }
                }
                catch { }

                return Task.CompletedTask;
            }
        };
    });

// Uma política por módulo: exige a claim sgdp_modulo que a claims transformation
// acrescenta a partir das roles do token e das concessões gravadas no sistema
builder.Services.AddAuthorization(ModulosSgdp.AdicionarPoliticas);

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
