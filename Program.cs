using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Identity.Client;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);




var azureAdConfig = builder.Configuration.GetSection("AzureAd");

var confidentialClientApp = ConfidentialClientApplicationBuilder
    .Create(Environment.GetEnvironmentVariable("ClientId"))
    .WithClientSecret(Environment.GetEnvironmentVariable("ClientSecret"))
    .WithAuthority($"https://login.microsoftonline.com/{Environment.GetEnvironmentVariable("TenantId")}")
    .WithRedirectUri($"{Environment.GetEnvironmentVariable("UrlBack")}/api/auth/callback")
    .Build();

builder.Services.AddSingleton<IConfidentialClientApplication>(confidentialClientApp);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAllOrigins",
        policy =>
        {
            policy.AllowAnyOrigin()
                  .AllowAnyMethod()
                  .AllowAnyHeader();
        });
});



builder.Services.AddControllers();
builder.Services.AddSingleton<GraphService>();
builder.Services.AddSingleton<DemandanteService>();
builder.Services.AddSingleton<CategoriaService>();
builder.Services.AddSingleton<DetalhamentoService>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = Environment.GetEnvironmentVariable("Issuer"),
            ValidAudience = Environment.GetEnvironmentVariable("Audience"),
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Environment.GetEnvironmentVariable("Key")))
        };
    });

var app = builder.Build();
app.UseSwagger();
app.UseSwaggerUI();

app.UseCors("AllowAllOrigins"); 

app.UseAuthorization();



// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();
app.MapControllers();


app.Run();

