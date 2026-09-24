using System.Text.Json;
using System.Text.Json.Nodes;
using Models.Planejamento;
using service;
using service.Planejamento;
using Xunit;

namespace test.planejamento;

/// <summary>
/// As peças puras do documento: o config dos blocos (PeDocConfig), os marcadores e a forma
/// canônica dos textos (PeDocMarcadores), sem banco.
/// </summary>
public class PeDocConfigMarcadoresTest
{
    private static readonly PeDocConfig.Contexto Contexto = new()
    {
        CamposDaSecao = chave => chave switch
        {
            "ativos" => new List<(string, string)> { ("nome", "texto_curto"), ("situacao", "lista"), ("usa_gdfnet", "sim_nao"), ("descricao", "texto_rico") },
            "necessidades" => new List<(string, string)> { ("descricao", "texto_longo"), ("priorizada", "sim_nao"), ("fraqueza", "ligacao_secao") },
            _ => null
        },
        Temas = () => new[] { "seguranca", "sistemas" }
    };

    private static JsonElement J(object valor) => JsonSerializer.SerializeToElement(valor);

    private static int Erro(Action acao) => Assert.Throws<ApiException>(acao).Error.Code;

    // ── Config ────────────────────────────────────────────────────────────────

    [Fact]
    public void Tabela_NormalizaAsChaves_ColunasEFiltro()
    {
        var r = PeDocConfig.Normalizar("tabela_secao", J(new
        {
            secao = " ativos ", colunas = new[] { "nome", "situacao" }, paginadeitada = true,
            filtro = new { campo = "usa_gdfnet", valor = true }
        }), Contexto);

        var config = PeDocConfig.Ler(r.Json);
        Assert.Equal("ativos", PeDocConfig.Secao(config));
        Assert.Equal(new[] { "nome", "situacao" }, PeDocConfig.Colunas(config));
        Assert.True(PeDocConfig.PaginaDeitada(config));
        var filtro = PeDocConfig.Filtro(config)!;
        Assert.Equal("usa_gdfnet", filtro.Campo);
        Assert.False(filtro.Excluir);
        Assert.True(filtro.Valor!.GetValue<bool>());
        Assert.Empty(r.Imagens);

        // Sem colunas: todas as visíveis marcadas "no documento" (nulo no config)
        Assert.Null(PeDocConfig.Colunas(PeDocConfig.Ler(PeDocConfig.Normalizar("tabela_secao", J(new { Secao = "ativos" }), Contexto).Json)));
        // O inventário do PGIA é uma seção só de leitura, com as colunas próprias
        Assert.Equal(PeDocConfig.SecaoPgia, PeDocConfig.Secao(PeDocConfig.Ler(PeDocConfig.Normalizar("tabela_secao",
            J(new { Secao = PeDocConfig.SecaoPgia, Colunas = new[] { "nome", "classificacao" } }), Contexto).Json)));
    }

    [Fact]
    public void Tabela_Recusa_SecaoColunaFiltroEChaveErrados()
    {
        var codigo = (int)ErrorCode.PeDocConfigInvalida;
        Assert.Equal(codigo, Erro(() => PeDocConfig.Normalizar("tabela_secao", J(new { }), Contexto)));
        Assert.Equal(codigo, Erro(() => PeDocConfig.Normalizar("tabela_secao", J(new { Secao = "nao_existe" }), Contexto)));
        Assert.Equal(codigo, Erro(() => PeDocConfig.Normalizar("tabela_secao", J(new { Secao = "ativos", Colunas = new[] { "nome", "nome" } }), Contexto)));
        Assert.Equal(codigo, Erro(() => PeDocConfig.Normalizar("tabela_secao", J(new { Secao = "ativos", Colunas = Array.Empty<string>() }), Contexto)));
        Assert.Equal(codigo, Erro(() => PeDocConfig.Normalizar("tabela_secao", J(new { Secao = "ativos", Tema = "seguranca" }), Contexto)));
        Assert.Equal(codigo, Erro(() => PeDocConfig.Normalizar("tabela_secao",
            J(new { Secao = "necessidades", Filtro = new { Campo = "fraqueza", Valor = 1 } }), Contexto)));
        Assert.Equal(codigo, Erro(() => PeDocConfig.Normalizar("tabela_secao",
            J(new { Secao = "ativos", Filtro = new { Campo = "descricao", Valor = "x" } }), Contexto)));
        Assert.Equal(codigo, Erro(() => PeDocConfig.Normalizar("tabela_secao",
            J(new { Secao = "ativos", Filtro = new { Campo = "nome", Valor = new[] { 1 } } }), Contexto)));
        Assert.Equal(codigo, Erro(() => PeDocConfig.Normalizar("tabela_secao",
            J(new { Secao = PeDocConfig.SecaoPgia, Filtro = new { Campo = "nome", Valor = "x" } }), Contexto)));
        Assert.Equal(codigo, Erro(() => PeDocConfig.Normalizar("tabela_secao", J(new[] { 1 }), Contexto)));
    }

    [Fact]
    public void Texto_Tema_Fluxo_Quebra_ETipo()
    {
        var texto = PeDocConfig.Normalizar("texto", J(new
        {
            Texto = new { type = "doc", content = new object[] { new { type = "paragraph", attrs = new { textAlign = "center" }, content = new object[] { new { type = "text", text = "Oi {orgao.nome}" } } } } }
        }), Contexto);
        // O atributo que o módulo não usa sai sem erro
        Assert.DoesNotContain("textAlign", texto.Json);
        Assert.Contains("Oi {orgao.nome}", texto.Json);
        // Texto sem letra: o bloco fica sem texto
        Assert.Null(PeDocConfig.TextoDoBloco(PeDocConfig.Ler(PeDocConfig.Normalizar("texto", J(new { Texto = new { type = "doc", content = new object[] { new { type = "paragraph" } } } }), Contexto).Json)));
        Assert.Equal((int)ErrorCode.PeDocConfigInvalida, Erro(() => PeDocConfig.Normalizar("texto",
            J(new { Texto = new { type = "doc", content = new object[] { new { type = "blockquote" } } } }), Contexto)));

        Assert.Equal("seguranca", PeDocConfig.Tema(PeDocConfig.Ler(PeDocConfig.Normalizar("lista_tema", J(new { Tema = "seguranca" }), Contexto).Json)));
        Assert.Equal((int)ErrorCode.PeDocConfigInvalida, Erro(() => PeDocConfig.Normalizar("lista_tema", J(new { Tema = "inventado" }), Contexto)));
        Assert.Equal("elaboracao", PeDocConfig.Fluxo(PeDocConfig.Ler(PeDocConfig.Normalizar("fluxo", J(new { Fluxo = "elaboracao" }), Contexto).Json)));
        Assert.Equal((int)ErrorCode.PeDocConfigInvalida, Erro(() => PeDocConfig.Normalizar("fluxo", J(new { Fluxo = "Com Espaço" }), Contexto)));

        // A quebra de página aceita PaginaDeitada (o editor manda em todo bloco), mas não guarda
        Assert.Equal("{}", PeDocConfig.Normalizar("quebra_pagina", J(new { PaginaDeitada = true }), Contexto).Json);
        Assert.Equal("{}", PeDocConfig.Normalizar("matriz_swot", null, Contexto).Json);
        Assert.Equal((int)ErrorCode.PeDocConfigInvalida, Erro(() => PeDocConfig.Normalizar("quebra_pagina", J(new { Secao = "ativos" }), Contexto)));
        Assert.Equal((int)ErrorCode.PeDocConfigInvalida, Erro(() => PeDocConfig.Normalizar("grafico", null, Contexto)));
    }

    [Theory]
    [InlineData("true", "true", true)]
    [InlineData("false", "true", false)]
    [InlineData("\"alta\"", "\"alta\"", true)]
    [InlineData("[\"seguranca\",\"sistemas\"]", "\"sistemas\"", true)]
    [InlineData("[\"seguranca\"]", "\"sistemas\"", false)]
    [InlineData("3", "3.0", true)]
    [InlineData("\"3\"", "3", true)]
    [InlineData("null", "true", false)]
    public void Filtro_Bate(string guardado, string filtro, bool bate) =>
        Assert.Equal(bate, PeDocConfig.Bate(JsonNode.Parse(guardado), JsonNode.Parse(filtro)));

    // ── Marcadores ────────────────────────────────────────────────────────────

    private static readonly Dictionary<string, string?> Valores = new()
    {
        ["orgao.nome"] = "Secretaria de Estado de Saúde",
        ["nomes.comite"] = null,
        ["vigencia.inicio"] = "01/01/2026"
    };

    private static JsonNode Documento(params string[] textos) => JsonNode.Parse(JsonSerializer.Serialize(new
    {
        type = "doc",
        content = textos.Select(t => new { type = "paragraph", content = new object[] { new { type = "text", text = t, marks = new object[] { new { type = "bold" } } } } })
    }))!;

    [Fact]
    public void Marcadores_ResolveOsConhecidos_ESemValorFicaOuSaiEmBranco()
    {
        var doc = Documento("{orgao.nome} desde {vigencia.inicio}", "Comitê: {nomes.comite}", "{nomes.comite}", "{outra.coisa} e {orgao}");

        Assert.Equal(new[] { "orgao.nome", "vigencia.inicio", "nomes.comite" }, PeDocMarcadores.Encontrados(doc, Valores));
        Assert.Equal(new[] { "nomes.comite" }, PeDocMarcadores.SemValor(doc, Valores));

        var previa = PeDocMarcadores.Resolver(doc, Valores, emBranco: false)!;
        Assert.Equal("Secretaria de Estado de Saúde desde 01/01/2026 Comitê: {nomes.comite} {nomes.comite} {outra.coisa} e {orgao}",
            PeValores.TextoDoRico(previa));
        var pdf = PeDocMarcadores.Resolver(doc, Valores, emBranco: true)!;
        Assert.Equal("Secretaria de Estado de Saúde desde 01/01/2026 Comitê: {outra.coisa} e {orgao}", PeValores.TextoDoRico(pdf));
        // O texto que fica vazio sai (o TipTap não aceita texto vazio); a marca de negrito continua
        Assert.Null(pdf["content"]![2]!["content"]);
        Assert.Equal("bold", pdf["content"]![0]!["content"]![0]!["marks"]![0]!["type"]!.GetValue<string>());
        // O original não muda
        Assert.Equal("{nomes.comite}", doc["content"]![2]!["content"]![0]!["text"]!.GetValue<string>());
    }

    [Fact]
    public void Lista_FixosEOsNomesCriadosPeloAdministrador()
    {
        var campos = new[]
        {
            new PeCampo { Id = 1, Chave = "comite", Rotulo = "Comitê", Tipo = "texto_curto", Ordem = 1 },
            new PeCampo { Id = 2, Chave = "equipe_elaboracao", Rotulo = "Equipe", Tipo = "texto_curto", Ordem = 2 },
            new PeCampo { Id = 3, Chave = "logotipo", Rotulo = "Logotipo", Tipo = "arquivo", Ordem = 3 },
            new PeCampo { Id = 4, Chave = "ouvidoria", Rotulo = "Ouvidoria", Tipo = "texto_curto", Ordem = 4 },
            new PeCampo { Id = 5, Chave = "apagado", Rotulo = "Apagado", Tipo = "texto_curto", Ordem = 5, ExcluidoEm = DateTime.UtcNow }
        };

        var lista = PeDocMarcadores.Lista(campos);

        Assert.Equal(12, PeDocMarcadores.Fixos.Count);
        Assert.Equal(PeDocMarcadores.Fixos.Select(m => m.Chave).Append("nomes.ouvidoria"), lista.Select(m => m.Chave));
    }

    [Fact]
    public void Canonico_IgnoraAOrdemDasChaves_EOHashAcompanha()
    {
        var a = JsonNode.Parse("{\"type\":\"doc\",\"content\":[{\"type\":\"text\",\"text\":\"ç\"}]}");
        var b = JsonNode.Parse("{\"content\":[{\"text\":\"ç\",\"type\":\"text\"}],\"type\":\"doc\"}");
        var c = JsonNode.Parse("{\"content\":[{\"text\":\"c\",\"type\":\"text\"}],\"type\":\"doc\"}");

        Assert.Equal(PeDocMarcadores.Canonico(a), PeDocMarcadores.Canonico(b));
        Assert.Equal(PeDocMarcadores.Hash(a), PeDocMarcadores.Hash(b));
        Assert.NotEqual(PeDocMarcadores.Hash(a), PeDocMarcadores.Hash(c));
        Assert.Equal(64, PeDocMarcadores.Hash(null).Length);
    }

    [Fact]
    public void Numero_AnoSemPontoDeMilhar_EOResto_ComPonto()
    {
        var ano = new PeCampo { Chave = "ano", Tipo = "numero", Config = "{\"min\":2020,\"max\":2100,\"casas\":0}" };
        var horas = new PeCampo { Chave = "horas", Tipo = "numero", Config = "{\"min\":0,\"casas\":0}" };

        Assert.Equal("2026", PeValores.Rotulo(ano, Array.Empty<PeOpcao>(), JsonValue.Create(2026)));
        Assert.Equal("12.000", PeValores.Rotulo(horas, Array.Empty<PeOpcao>(), JsonValue.Create(12000)));
    }

    [Fact]
    public void NomeDoArquivo_SiglaSegura()
    {
        Assert.Equal("PDTIC_SES_v1.0_3.pdf", PeDocumentoService.NomeDoArquivo("SES", "1.0", 3));
        Assert.Equal("PDTIC_SES_DF_v2.0_12.pdf", PeDocumentoService.NomeDoArquivo("SES/DF", "2.0", 12));
        Assert.Equal("PDTIC_orgao_v1.0_1.pdf", PeDocumentoService.NomeDoArquivo(" ", "1.0", 1));
    }
}
