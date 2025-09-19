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
using demanda_service.Repositorio.Interface;
using Microsoft.OpenApi.Models;
using Interface.Repositorio;

var builder = WebApplication.CreateBuilder(args);

Env.Load();


var mode = Environment.GetEnvironmentVariable("MODE");
var connectionString = mode == "container" ? "PostgreSqlDocker" : "PostgreSql";

if (string.IsNullOrEmpty(connectionString))
{
    throw new InvalidOperationException("Connection string not found.");
}

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString(connectionString)));
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
builder.Services.AddScoped<IEsteiraRepositorio, EsteiraRepositorio>();
builder.Services.AddScoped<IEsteiraService, EsteiraService>();
builder.Services.AddScoped<IDespachoRepositorio, DespachoRepositorio>();
builder.Services.AddScoped<IDespachoService, DespachoService>();
builder.Services.AddScoped<IAtividadeRepositorio, AtividadeRepositorio>();

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
var authSettingsSection = builder.Configuration.GetSection(AuthSettings.SectionName);
builder.Services.Configure<AuthSettings>(authSettingsSection);

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidIssuer = authSettingsSection["Issuer"],
            ValidAudience = authSettingsSection["Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(authSettingsSection["Key"]!)),
            ValidateIssuer = bool.Parse(authSettingsSection["ValidateIssuer"] ?? "false"),
            ValidateAudience = bool.Parse(authSettingsSection["ValidateAudience"] ?? "false"),
            ValidateLifetime = true,
            ValidateIssuerSigningKey = bool.Parse(authSettingsSection["ValidateIssuerSigningKey"] ?? "false")
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
