
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Models;
using Repositorio;
using Repositorio.Interface;
using Xunit.Microsoft.DependencyInjection;
using Xunit.Microsoft.DependencyInjection.Abstracts;

namespace test.fixtures;

public class Base : TestBedFixture
{
    protected override void AddServices(IServiceCollection services, IConfiguration? configuration)
    {
        var dataName = "DbInMemory" + Random.Shared.Next().ToString();

        services.AddDbContext<AppDbContext>(options =>
            options.UseInMemoryDatabase(dataName));


        services.AddScoped<ICategoriaRepositorio, CategoriaRepositorio>();
        services.AddScoped<IDemandaRepositorio, DemandaRepositorio>();
        services.AddScoped<IProjetoRepositorio, ProjetoRepositorio>();
        services.AddScoped<DetalhamentoRepositorio>();
        services.AddScoped<IDemandanteRepositorio, DemandanteRepositorio>();

    }

    protected override ValueTask DisposeAsyncCore() => new();

    protected override IEnumerable<TestAppSettings> GetTestAppSettings()
    {
        yield return new(); 
    }
}