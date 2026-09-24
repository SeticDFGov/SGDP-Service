using System.Text.Json;
using api.Planejamento;
using app.Models;
using Controllers.Planejamento;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Models.Planejamento;
using service.Planejamento;
using Xunit;

namespace test.planejamento;

/// <summary>
/// Base dos testes da E6 (fluxos): a base do documento (modelo inicial na versão 4, com os
/// fluxos do guia carregados), o serviço dos fluxos e atalhos para montar definições, ler os
/// modelos semeados, desenhar e conferir os erros de validação.
/// </summary>
public abstract class PeFluxoTestBase : PeDocumentoTestBase
{
    protected readonly PeFluxoService Fluxos;

    protected PeFluxoTestBase()
    {
        Fluxos = new PeFluxoService(Context, Registros, Permissoes);
    }

    /// <summary>Os nomes padrão (o modelo): "{nomes.comite}" vira "Comitê de Governança Digital".</summary>
    protected static Dictionary<string, string?> NomesPadrao() => PeFluxoNomes.Mapa(null, null);

    protected PeFluxoModelo ModeloNoBanco(string chave) => Context.PeFluxosModelo.AsNoTracking().Single(m => m.Chave == chave);

    protected PeFluxoDefinicao DefinicaoDoModelo(string chave) => PeFluxoDefinicaoLeitor.DoBanco(ModeloNoBanco(chave).Definicao);

    protected static JsonElement ComoJson(object valor) => JsonSerializer.SerializeToElement(valor);

    protected static JsonElement ComoJson(PeFluxoDefinicao definicao) =>
        JsonDocument.Parse(PeFluxoDefinicaoLeitor.ParaJson(definicao)).RootElement.Clone();

    protected static PeFluxoSalvarDTO Corpo(PeFluxoDefinicao definicao, string? nome = null) =>
        new() { Nome = nome, Definicao = ComoJson(definicao) };

    /// <summary>Os erros de uma definição (a validação da API).</summary>
    protected static List<string> ErrosDe(object definicao) => PeFluxoDefinicaoLeitor.Ler(ComoJson(definicao)).Erros;

    /// <summary>Uma definição válida e pequena: início, duas tarefas numa raia, uma decisão com retorno e o fim.</summary>
    protected static PeFluxoDefinicao Simples(string prefixo = "9") => new()
    {
        PrefixoNumeracao = prefixo,
        Raias = { new PeFluxoRaia { Id = "r1", Nome = "{nomes.comite}", Ordem = 1 }, new PeFluxoRaia { Id = "r2", Nome = "Equipe", Ordem = 2 } },
        Elementos =
        {
            new PeFluxoElemento { Id = "i", Tipo = "inicio", RaiaId = "r1" },
            new PeFluxoElemento { Id = "a", Tipo = "tarefa", RaiaId = "r1", Nome = "Primeira tarefa" },
            new PeFluxoElemento { Id = "b", Tipo = "tarefa", RaiaId = "r2", Nome = "Segunda tarefa", Artefatos = { "Relatório" } },
            new PeFluxoElemento { Id = "d", Tipo = "decisao", RaiaId = "r1", Nome = "Aprovado?" },
            new PeFluxoElemento { Id = "f", Tipo = "fim", RaiaId = "r1" }
        },
        Ligacoes =
        {
            new PeFluxoLigacao { Id = "l1", De = "i", Para = "a" },
            new PeFluxoLigacao { Id = "l2", De = "a", Para = "b" },
            new PeFluxoLigacao { Id = "l3", De = "b", Para = "d" },
            new PeFluxoLigacao { Id = "l4", De = "d", Para = "f", Rotulo = "Sim" },
            new PeFluxoLigacao { Id = "l5", De = "d", Para = "b", Rotulo = "Não" }
        }
    };

    protected static PeFluxoDesenho Desenhar(PeFluxoDefinicao definicao, string? titulo = "Fluxo de teste") =>
        PeFluxoDesenho.Desenhar(definicao, titulo, NomesPadrao());

    protected PeFluxosController ControladorFluxos(User user) => new(Fluxos, Permissoes)
    {
        ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = Principal(user) } }
    };

    /// <summary>A pasta onde os SVGs e o PDF de exemplo são gravados (variável PE_EXEMPLO_FLUXOS), ou nula.</summary>
    protected static string? PastaDeExemplo()
    {
        var pasta = Environment.GetEnvironmentVariable("PE_EXEMPLO_FLUXOS");
        if (string.IsNullOrWhiteSpace(pasta)) return null;
        Directory.CreateDirectory(pasta);
        return pasta;
    }
}
