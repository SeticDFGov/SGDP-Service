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
/// Base dos testes da E3 (referenciais e registros): a base do modelo (E2) com o modelo
/// inicial carregado (versão 2: seções do DF e do PETIC-DF e os 11 princípios), mais os
/// serviços da E3 e atalhos para montar registros, versões do PETIC-DF e seções de teste.
/// </summary>
public abstract class PeReferenciaisTestBase : PeModeloTestBase
{
    protected readonly PeRegistroService Registros;
    protected readonly PePeticService Petics;
    protected readonly PeDeliberacaoService Deliberacoes;
    protected readonly PeArquivoService Arquivos;
    protected readonly PePlanilhaService Planilhas;

    protected PeReferenciaisTestBase()
    {
        Registros = new PeRegistroService(Context, Permissoes);
        Petics = new PePeticService(Context, Registros, Permissoes);
        Deliberacoes = new PeDeliberacaoService(Context);
        Arquivos = new PeArquivoService(Context, Permissoes);
        Planilhas = new PePlanilhaService(Context, Registros);
    }

    protected Task<PeUserContext> Admin() => ContextoDe(UserPeAdmin);

    protected Task<PeUserContext> Cgtic() => ContextoDe(UserPeCgtic);

    /// <summary>Corpo do POST/PUT de registro a partir de objetos anônimos ({ texto = "..." }).</summary>
    protected static PeRegistroSalvarDTO Salvar(object? dados = null, object? vinculos = null)
    {
        var corpo = new Dictionary<string, object?>();
        if (dados != null) corpo["Dados"] = dados;
        if (vinculos != null) corpo["Vinculos"] = vinculos;
        return PeRegistroSalvarDTO.Ler(JsonSerializer.SerializeToElement(corpo));
    }

    /// <summary>As mensagens por campo de uma validação que falhou.</summary>
    protected static async Task<IReadOnlyDictionary<string, string>> CamposComErroAsync(Func<Task> acao)
    {
        var ex = await Assert.ThrowsAsync<PeValidacaoException>(acao);
        Assert.Equal((int)ErrorCode.PeRegistroInvalido, ex.Error.Code);
        return ex.Campos;
    }

    protected PeRegistro RegistroNoBanco(long id) => Context.PeRegistros.AsNoTracking().Single(r => r.Id == id);

    protected static string Valor(PeRegistroResponse registro, string chave) => registro.Dados[chave].ToString();

    // ── PETIC-DF ─────────────────────────────────────────────────────────────

    protected async Task<PePeticResponse> RascunhoAsync(string titulo = "PETIC-DF 2027-2030", bool copiar = true) =>
        await Petics.CriarAsync(new PePeticCriarDTO
        {
            Titulo = titulo,
            VigenciaInicio = "2027-01-01",
            VigenciaFim = "2030-12-31",
            CopiarDaVigente = copiar
        }, await Admin());

    protected async Task<PeRegistroResponse> IncluirAsync(long peticId, string secao, object dados, object? vinculos = null) =>
        await Registros.CriarAsync(PeDono.DoPetic(peticId), secao, Salvar(dados, vinculos), await Admin());

    /// <summary>
    /// Preenche o mínimo para a versão ir ao CGTIC: uma diretriz, um objetivo, uma prioridade,
    /// um indicador e uma iniciativa (as seções obrigatórias). Devolve o objetivo.
    /// </summary>
    protected async Task<PeRegistroResponse> PreencherMinimoAsync(long peticId)
    {
        await IncluirAsync(peticId, "petic_diretriz", new { texto = "Priorizar soluções corporativas." });
        var objetivo = await IncluirAsync(peticId, "petic_objetivo", new { texto = "Ampliar os serviços digitais." });
        await IncluirAsync(peticId, "petic_prioridade", new { texto = "Serviços digitais ao cidadão." });
        await IncluirAsync(peticId, "petic_indicador", new { nome = "Serviços digitais ofertados" },
            new { objetivo = new[] { objetivo.Id } });
        await IncluirAsync(peticId, "petic_iniciativa", new { descricao = "Portal único de serviços." },
            new { objetivo = new[] { objetivo.Id } });
        return objetivo;
    }

    protected async Task<PeDeliberacaoResponse> AprovarAsync(long peticId)
    {
        var enviado = await Petics.EnviarAsync(peticId, await Admin());
        return await Deliberacoes.DecidirAsync(enviado.Deliberacao!.Id, new PeDecidirDTO
        {
            Decisao = "aprovado",
            AtoTipo = "Resolução",
            AtoNumero = "1/2026",
            AtoData = "2026-09-01",
            Sei = "00040-00012345/2026-11"
        }, await Cgtic());
    }

    /// <summary>Uma versão aprovada (vigente), com o mínimo preenchido; devolve a versão e o objetivo.</summary>
    protected async Task<(PePeticResponse Versao, PeRegistroResponse Objetivo)> VigenteAsync()
    {
        var rascunho = await RascunhoAsync("PETIC-DF 2026-2029");
        var objetivo = await PreencherMinimoAsync(rascunho.Id);
        await AprovarAsync(rascunho.Id);
        return (await Petics.ObterAsync(rascunho.Id, await Admin()), objetivo);
    }

    // ── Seções de teste (criadas pelo administrador, pelo editor do modelo) ──

    /// <summary>Seção do catálogo do DF criada pelo administrador, já ligada.</summary>
    protected async Task<PeSecaoResponse> SecaoDfAsync(string titulo, string tipo = "tabela", string? prefixo = "T", string situacao = "obrigatorio")
    {
        var secao = await Modelo.CriarSecaoAsync(new PeSecaoCriarDTO
        {
            Escopo = "df",
            Titulo = titulo,
            Tipo = tipo,
            PrefixoCodigo = prefixo
        }, EmailAdmin);
        await Modelo.DefinirSituacaoSecaoAsync(secao.Id, new PeSituacoesDTO { SituacaoGeral = situacao }, EmailAdmin);
        return secao;
    }

    /// <summary>Campo criado pelo administrador numa seção fora do PDTIC, já com a situação geral.</summary>
    protected async Task<PeCampoResponse> CampoAsync(long secaoId, string chave, string tipo, object? config = null,
        string situacao = "opcional", string? rotulo = null)
    {
        var campo = await Modelo.CriarCampoAsync(new PeCampoCriarDTO
        {
            SecaoId = secaoId,
            Chave = chave,
            Rotulo = rotulo ?? chave,
            Tipo = tipo,
            Config = config == null ? null : JsonSerializer.SerializeToElement(config)
        }, EmailAdmin);
        return await Modelo.DefinirSituacaoCampoAsync(campo.Id, new PeSituacoesDTO { SituacaoGeral = situacao }, EmailAdmin);
    }

    protected async Task OpcoesAsync(long campoId, params string[] valores)
    {
        foreach (var valor in valores)
            await Modelo.CriarOpcaoAsync(campoId, new PeOpcaoCriarDTO { Valor = valor, Rotulo = "Opção " + valor }, EmailAdmin);
    }

    // ── Controllers ──────────────────────────────────────────────────────────

    protected PeRegistrosController ControladorRegistros(User user) => new(Registros, Planilhas, Permissoes)
    {
        ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = Principal(user) } }
    };

    protected PePeticController ControladorPetic(User user) => new(Petics, Planilhas, Permissoes)
    {
        ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = Principal(user) } }
    };

    protected PeDeliberacoesController ControladorDeliberacoes(User user) => new(Deliberacoes, Permissoes)
    {
        ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = Principal(user) } }
    };

    protected PeArquivosController ControladorArquivos(User user) => new(Arquivos, Permissoes)
    {
        ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = Principal(user) } }
    };

    // ── Arquivos ─────────────────────────────────────────────────────────────

    protected static byte[] Pdf(int tamanho = 1000)
    {
        var bytes = new byte[tamanho];
        "%PDF-1.7\n"u8.ToArray().CopyTo(bytes, 0);
        return bytes;
    }

    protected static byte[] Png(int tamanho = 1000)
    {
        var bytes = new byte[tamanho];
        new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }.CopyTo(bytes, 0);
        return bytes;
    }

    protected async Task<PeArquivoResponse> EnviarArquivoAsync(User user, string nome, byte[] conteudo) =>
        await Arquivos.EnviarAsync(new PeArquivoUpload { NomeOriginal = nome, Tamanho = conteudo.Length, Conteudo = new MemoryStream(conteudo) },
            await ContextoDe(user));
}
