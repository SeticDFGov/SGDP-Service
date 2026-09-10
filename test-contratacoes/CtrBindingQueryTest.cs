using System.Reflection;
using Controllers.Contratacoes;
using Xunit;

namespace test.contratacoes;

/// <summary>
/// Trava a armadilha de binding que já derrubou a listagem em produção local:
/// quando o parâmetro de um modelo de query tem o MESMO nome de uma propriedade
/// dele (ex.: parâmetro "filtro" e propriedade "Filtro"), o model binder acha o
/// prefixo na query, passa a procurar "filtro.Filtro"/"filtro.Page" e devolve o
/// modelo inteiro no default — a API ignora busca, filtros e paginação em silêncio.
/// </summary>
public class CtrBindingQueryTest
{
    [Fact]
    public void ActionsDeQuery_NaoUsamNomeDeParametroQueColideComPropriedadeDoModelo()
    {
        var controllers = typeof(CtrProcessoController).Assembly.GetTypes()
            .Where(t => t.Namespace == "Controllers.Contratacoes" && t.IsClass && !t.IsAbstract)
            .ToList();

        Assert.NotEmpty(controllers);
        var analisadas = 0;

        foreach (var controller in controllers)
        {
            foreach (var acao in controller.GetMethods(BindingFlags.Public | BindingFlags.Instance
                                                       | BindingFlags.DeclaredOnly))
            {
                foreach (var parametro in acao.GetParameters())
                {
                    var tipo = parametro.ParameterType;
                    // Só os modelos de query do módulo (tipo complexo ligado à query string)
                    if (!tipo.IsClass || tipo.Namespace != "api.Contratacoes") continue;
                    if (!tipo.Name.EndsWith("Filtro")) continue;

                    analisadas++;
                    var colisao = tipo.GetProperties()
                        .FirstOrDefault(p => string.Equals(p.Name, parametro.Name,
                            StringComparison.OrdinalIgnoreCase));

                    Assert.True(colisao == null,
                        $"{controller.Name}.{acao.Name}: o parâmetro \"{parametro.Name}\" colide com a "
                        + $"propriedade \"{colisao?.Name}\" de {tipo.Name} — o binder passaria a exigir "
                        + $"\"{parametro.Name}.{colisao?.Name}\" na query e todos os filtros ficariam no default. "
                        + "Renomeie o parâmetro (ex.: \"consulta\").");
                }
            }
        }

        // Garante que o teste está realmente olhando para as actions de listagem/export
        Assert.Equal(3, analisadas);
    }
}
