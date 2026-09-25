using System.Text.Json;
using api.Planejamento;
using Microsoft.EntityFrameworkCore;
using Models.Planejamento;
using service;
using service.Planejamento;
using Xunit;

namespace test.planejamento;

/// <summary>
/// F1, a rodada de correções da revisão final, no modelo e no conteúdo semeado: a trilha diz
/// quais seções saem na planilha (I06); o config ganhou naoFutura na data e o formato do texto
/// curto (C05, C33, B22), com o erro no campo da janela (A14); o órgão volta ao nível padrão e o
/// histórico diz quando o nível é o padrão (A21); a ajuda de "Quem decidiu" em cada aprovação
/// (C34) e os textos do 5.1 (C18); o endereço de internet com servidor de verdade (B02) e os
/// nomes das abas da planilha cortados numa palavra (B20).
/// </summary>
public class PeF1ModeloTest : PeReferenciaisTestBase
{
    private static HashSet<string> Inf(params string[] nomes) => nomes.ToHashSet();

    private static JsonElement ConfigDe(PeCampo campo) => JsonDocument.Parse(campo.Config).RootElement;

    // ── I06: a seção na planilha, na trilha ─────────────────────────────────

    [Fact]
    public async Task Trilha_TrazNaPlanilhaEmCadaSecao()
    {
        var antes = await TrilhaAsync(OrgaoSes);
        var secoes = antes.Etapas.SelectMany(e => e.Passos).SelectMany(p => p.Secoes).ToList();
        Assert.True(secoes.Single(s => s.Chave == "ativos").NaPlanilha);

        await Modelo.AtualizarSecaoAsync(Secao("ativos").Id, new PeSecaoAtualizarDTO { NaPlanilha = false, Informados = Inf("NaPlanilha") }, EmailAdmin);
        var depois = await TrilhaAsync(OrgaoSes);
        Assert.False(depois.Etapas.SelectMany(e => e.Passos).SelectMany(p => p.Secoes).Single(s => s.Chave == "ativos").NaPlanilha);
    }

    // ── C05, C33 e B22: as regras novas do config ──────────────────────────

    [Fact]
    public async Task Config_NaoFuturaNaData_EFormatoNoTextoCurto()
    {
        var secao = Secao("nomes").Id;
        var data = await Modelo.CriarCampoAsync(new PeCampoCriarDTO
        {
            SecaoId = secao, Rotulo = "Data da portaria", Tipo = "data", Config = Corpo(new { naoFutura = true })
        }, EmailAdmin);
        Assert.True(data.Config.GetProperty("naoFutura").GetBoolean());

        // Falso não é guardado (o padrão), e nulo conta como ausente
        var semRegra = await Modelo.CriarCampoAsync(new PeCampoCriarDTO
        {
            SecaoId = secao, Rotulo = "Outra data", Tipo = "data", Config = Corpo(new { naoFutura = false })
        }, EmailAdmin);
        Assert.False(semRegra.Config.TryGetProperty("naoFutura", out _));

        var sei = await Modelo.CriarCampoAsync(new PeCampoCriarDTO
        {
            SecaoId = secao, Rotulo = "Processo", Tipo = "texto_curto", Config = Corpo(new { max = 30, formato = "sei" })
        }, EmailAdmin);
        Assert.Equal("sei", sei.Config.GetProperty("formato").GetString());
        var url = await Modelo.CriarCampoAsync(new PeCampoCriarDTO
        {
            SecaoId = secao, Rotulo = "Página", Tipo = "texto_curto", Config = Corpo(new { formato = "url" })
        }, EmailAdmin);
        Assert.Equal("url", url.Config.GetProperty("formato").GetString());

        // Formato fora da lista e chave que o tipo não aceita: 400 com o campo da janela (A14)
        var ex = await Assert.ThrowsAsync<PeValidacaoException>(() => Modelo.CriarCampoAsync(new PeCampoCriarDTO
        {
            SecaoId = secao, Rotulo = "Telefone", Tipo = "texto_curto", Config = Corpo(new { formato = "telefone" })
        }, EmailAdmin));
        Assert.Equal((int)ErrorCode.PeConfigInvalida, ex.Error.Code);
        Assert.True(ex.Campos.ContainsKey("Config.formato"));
        ex = await Assert.ThrowsAsync<PeValidacaoException>(() => Modelo.CriarCampoAsync(new PeCampoCriarDTO
        {
            SecaoId = secao, Rotulo = "Data", Tipo = "data", Config = Corpo(new { formato = "sei" })
        }, EmailAdmin));
        Assert.True(ex.Campos.ContainsKey("Config"));
    }

    [Fact]
    public void Valores_DataNaoFutura_Sei_EEndereco()
    {
        PeCampo Campo(string tipo, string config) => new() { Chave = "c", Rotulo = "C", Tipo = tipo, Config = config };
        var hoje = PeValores.Hoje();
        string Iso(DateOnly d) => d.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);

        var data = Campo("data", "{\"naoFutura\":true}");
        Assert.Null(PeValores.Normalizar(data, new List<PeOpcao>(), JsonSerializer.SerializeToElement(Iso(hoje)), null).Erro);
        Assert.Equal("A data não pode ser depois de hoje.",
            PeValores.Normalizar(data, new List<PeOpcao>(), JsonSerializer.SerializeToElement(Iso(hoje.AddDays(1))), null).Erro);
        // Sem a regra, a data futura vale (o prazo de uma ação, por exemplo)
        Assert.Null(PeValores.Normalizar(Campo("data", "{}"), new List<PeOpcao>(), JsonSerializer.SerializeToElement(Iso(hoje.AddYears(2))), null).Erro);

        var sei = Campo("texto_curto", "{\"max\":30,\"formato\":\"sei\"}");
        Assert.Null(PeValores.Normalizar(sei, new List<PeOpcao>(), JsonSerializer.SerializeToElement(" 00040-00012345/2026-11 "), null).Erro);
        Assert.Equal(PeValores.MensagemSei, PeValores.Normalizar(sei, new List<PeOpcao>(), JsonSerializer.SerializeToElement("123"), null).Erro);

        var url = Campo("texto_curto", "{\"formato\":\"url\"}");
        Assert.Null(PeValores.Normalizar(url, new List<PeOpcao>(), JsonSerializer.SerializeToElement("https://www.saude.df.gov.br/pdtic"), null).Erro);
        foreach (var errado in new[] { "site do órgão", "www.df.gov.br", "https://https://www.df.gov.br", "ftp://www.df.gov.br", "http://intranet" })
            Assert.Equal(PeValores.MensagemEndereco, PeValores.Normalizar(url, new List<PeOpcao>(), JsonSerializer.SerializeToElement(errado), null).Erro);
    }

    [Fact]
    public void LinkDoTextoRico_ServidorComPonto_SemOPrefixoRepetido()
    {
        Assert.True(PeTextoRico.LinkValido("https://www.df.gov.br"));
        Assert.True(PeTextoRico.LinkValido("http://10.0.0.1/pdtic"));
        Assert.False(PeTextoRico.LinkValido("https://https://www.df.gov.br"));
        Assert.False(PeTextoRico.LinkValido("https://intranet/pdtic"));
        Assert.False(PeTextoRico.LinkValido("javascript:alert(1)"));
    }

    // ── A14: o erro da janela do campo, no campo ──────────────────────────

    [Fact]
    public async Task JanelaDoCampo_ErrosComOCampo()
    {
        var secao = Secao("nomes").Id;

        var ex = await Assert.ThrowsAsync<PeValidacaoException>(() => Modelo.CriarCampoAsync(new PeCampoCriarDTO
        {
            SecaoId = secao, Rotulo = "Texto de teste", Tipo = "texto_curto", Config = Corpo(new { max = 5000 })
        }, EmailAdmin));
        Assert.Equal((int)ErrorCode.PeConfigInvalida, ex.Error.Code);
        Assert.Equal("O tamanho máximo do texto curto vai de 1 a 1000 caracteres.", ex.Campos["Config.max"]);
        Assert.Equal(ex.Error.Message, ex.Campos["Config.max"]);

        ex = await Assert.ThrowsAsync<PeValidacaoException>(() => Modelo.CriarCampoAsync(new PeCampoCriarDTO
        {
            SecaoId = secao, Rotulo = "  ", Tipo = "texto_curto"
        }, EmailAdmin));
        Assert.True(ex.Campos.ContainsKey("Rotulo"));

        ex = await Assert.ThrowsAsync<PeValidacaoException>(() => Modelo.CriarCampoAsync(new PeCampoCriarDTO
        {
            SecaoId = secao, Chave = "comite", Rotulo = "Outro comitê", Tipo = "texto_curto"
        }, EmailAdmin));
        Assert.Equal((int)ErrorCode.PeChaveDuplicada, ex.Error.Code);
        Assert.True(ex.Campos.ContainsKey("Chave"));

        // Na alteração também, e no 400 do controller vão Code, Message e Campos
        var campo = await Modelo.CriarCampoAsync(new PeCampoCriarDTO { SecaoId = secao, Rotulo = "Apelido", Tipo = "texto_curto" }, EmailAdmin);
        ex = await Assert.ThrowsAsync<PeValidacaoException>(() => Modelo.AtualizarCampoAsync(campo.Id,
            new PeCampoAtualizarDTO { Config = Corpo(new { max = 0 }), Informados = Inf("Config") }, EmailAdmin));
        Assert.True(ex.Campos.ContainsKey("Config.max"));
        var (status, corpo) = Resultado(await ControladorModelo(UserPeAdmin).CriarCampo(new PeCampoCriarDTO
        {
            SecaoId = secao, Rotulo = "Outro texto", Tipo = "texto_curto", Config = Corpo(new { max = 5000 })
        }));
        Assert.Equal(400, status);
        var json = JsonSerializer.Serialize(corpo);
        Assert.Contains("\"Campos\"", json);
        Assert.Contains("\"Config.max\"", json);
    }

    // ── A21: a volta ao nível padrão ──────────────────────────────────────

    [Fact]
    public async Task Orgao_VoltaAoNivelPadrao_ComJustificativa_EOHistoricoDiz()
    {
        await Orgaos.DefinirNivelAsync(OrgaoSes.Id, new PeOrgaoNivelDTO { NivelId = NivelId("avancado"), Justificativa = "Tem PDTIC anterior." }, EmailAdmin);

        var item = await Orgaos.DefinirNivelAsync(OrgaoSes.Id, new PeOrgaoNivelDTO { NivelId = null, Justificativa = "Voltou para começar do básico." }, EmailAdmin);
        Assert.True(item.NivelPadrao);
        Assert.Equal("Básico", item.NivelNome);
        Assert.Empty(Context.PeOrgaosConfig.AsNoTracking().Where(c => c.OrgaoId == OrgaoSes.Id));

        var historico = await Orgaos.HistoricoNivelAsync(OrgaoSes.Id);
        var volta = historico[0];
        Assert.True(volta.NivelNovoPadrao);
        Assert.Equal(("Avançado", false, "Básico"), (volta.NivelAnterior, volta.NivelAnteriorPadrao, volta.NivelNovo));
        Assert.Equal("Voltou para começar do básico.", volta.Justificativa);
        Assert.Equal("Paula Administradora", volta.DefinidoPorNome);
        // A primeira troca saiu do padrão
        var primeira = historico[1];
        Assert.True(primeira.NivelAnteriorPadrao);
        Assert.False(primeira.NivelNovoPadrao);

        // Já no padrão: nada muda nem entra no histórico
        await Orgaos.DefinirNivelAsync(OrgaoSes.Id, new PeOrgaoNivelDTO { NivelId = null, Justificativa = "De novo." }, EmailAdmin);
        Assert.Equal(2, (await Orgaos.HistoricoNivelAsync(OrgaoSes.Id)).Count);
        Assert.Equal((int)ErrorCode.PeJustificativaObrigatoria, await ErroAsync(() =>
            Orgaos.DefinirNivelAsync(OrgaoSes.Id, new PeOrgaoNivelDTO { NivelId = null }, EmailAdmin)));
    }

    // ── C05, C33, C34, C18 e B22: o conteúdo semeado ───────────────────────

    [Fact]
    public void Seed_DatasDeDecisaoNaoFuturas_SeiEEnderecosComFormato()
    {
        foreach (var secao in new[]
                 {
                     "aprovacao_plano_trabalho", "aprovacao_inventario", "aprovacao_sgtic", "aprovacao_plano_acompanhamento",
                     "avaliacao_comite", "aprovacao_resultados_comite", "aprovacao_resultados_autoridade"
                 })
        {
            Assert.True(ConfigDe(Campo(secao, "data")).GetProperty("naoFutura").GetBoolean(), secao);
            Assert.Equal("sei", ConfigDe(Campo(secao, "sei")).GetProperty("formato").GetString());
        }
        foreach (var (secao, campo) in new[] { ("sgtic", "ato_data"), ("equipe_designacao", "ato_data"), ("responsavel_acompanhamento", "ato_data"), ("publicacao", "data") })
            Assert.True(ConfigDe(Campo(secao, campo)).GetProperty("naoFutura").GetBoolean(), $"{secao}.{campo}");
        Assert.Equal("url", ConfigDe(Campo("publicacao", "endereco")).GetProperty("formato").GetString());
        Assert.Equal("url", ConfigDe(Campo("documentos_referencia", "link")).GetProperty("formato").GetString());

        var sigla = Campo("nomes", "sigla_orgao");
        Assert.Equal(20, ConfigDe(sigla).GetProperty("max").GetInt32());
        Assert.Equal("estreita", sigla.Largura);
    }

    [Fact]
    public void Seed_AjudaDeQuemDecidiu_CertaParaCadaAprovacao()
    {
        const string doSgtic = "Em geral, o SGTIC. Mude quando outra instância decidiu.";
        Assert.Equal(doSgtic, Campo("aprovacao_sgtic", "instancia").Ajuda);
        foreach (var secao in new[] { "aprovacao_plano_trabalho", "aprovacao_inventario", "aprovacao_plano_acompanhamento", "aprovacao_resultados_comite" })
        {
            var ajuda = Campo(secao, "instancia").Ajuda!;
            Assert.NotEqual(doSgtic, ajuda);
            Assert.Contains("comitê interno de TIC (SGTIC)", ajuda);
        }
        Assert.Equal("Em geral, quem aprova o relatório de resultados final é a autoridade máxima do órgão. Mude quando outra instância decidiu.",
            Campo("aprovacao_resultados_autoridade", "instancia").Ajuda);
    }

    [Fact]
    public void Seed_OPasso51ServeATodosOsNiveis()
    {
        var passo = Passo("monitoramento.ciclo-monitoramento");
        Assert.Equal("Atualize a situação das ações no ciclo", passo.Titulo);
        // F3: na linguagem das formas, que vale nos dois modos dos níveis
        Assert.StartsWith("A cada ciclo, atualize a situação de cada ação. Na forma do nível Básico, o mínimo do decreto, basta a situação;",
            passo.OQueFazer);
        // A observação da grade continua opcional no Básico
        Assert.Equal("p p p", SituacoesDoCampo("monitoramento_acoes", "observacao"));
    }

    [Fact]
    public async Task Seed_SiglaComMaximo_EEnderecoDeReferenciaValidadosNoServidor()
    {
        var pdtic = await Pdtics().AbrirAsync(new PePdticCriarDTO(), await ContextoDe(UserOrgaoSes));
        var ctx = await ContextoDe(UserOrgaoSes);

        var campos = await CamposComErroAsync(() => Registros.CriarAsync(PeDono.DoPdtic(pdtic.Id), "nomes",
            Salvar(new { sigla_orgao = new string('S', 21) }), ctx));
        Assert.Equal("Use no máximo 20 caracteres (o texto tem 21).", campos["sigla_orgao"]);

        await DefinirNivelDoOrgaoAsync(OrgaoSes, "avancado");
        campos = await CamposComErroAsync(() => Registros.CriarAsync(PeDono.DoPdtic(pdtic.Id), "documentos_referencia",
            Salvar(new { identificacao = "Plano Plurianual", tipo = "ppa", link = "isto não é um endereço" }), ctx));
        Assert.Equal(PeValores.MensagemEndereco, campos["link"]);
    }

    private PePdticService Pdtics() => new(Context, Registros, Permissoes);

    // ── B20: os nomes das abas ─────────────────────────────────────────────

    [Fact]
    public void Abas_CortadasNumaPalavra_SemALigacaoSolta_ESemRepetir()
    {
        var nomes = PeXlsx.NomesUnicos(new[]
        {
            "Instrumentos que o órgão não tem e o que usa no lugar",
            "Plano de levantamento das necessidades",
            "Aprovação do relatório de resultados (comitê)",
            "Aprovação do relatório de resultados (autoridade)",
            "Ativos",
            "Umapalavrasoquepassadostrintaeumcaracteres"
        });
        Assert.Equal(new[]
        {
            "Instrumentos que o órgão não", "Plano de levantamento", "Aprovação do relatório",
            "Aprovação do relatório (2)", "Ativos", "Umapalavrasoquepassadostrintaeu"
        }, nomes);
        Assert.All(nomes, n => Assert.True(n.Length <= 31, n));
    }
}
