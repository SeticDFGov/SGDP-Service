using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Models.Planejamento;
using service.Planejamento;
using Xunit;

namespace test.planejamento;

/// <summary>
/// Carregador, versão 2 (E3): as seções do catálogo do DF (princípios e diretrizes do
/// ciclo) e do PETIC-DF, fora do PDTIC, com a situação geral; e os 11 princípios do art. 4º
/// do Decreto nº 48.900/2026 como registros do sistema, com o texto literal do inciso.
/// Idempotente, sem sobrescrever, e subindo por cima de uma base que já tinha a versão 1.
/// </summary>
public class PeReferenciaisCargaTest : PeReferenciaisTestBase
{
    /// <summary>Texto literal de cada inciso do art. 4º (sem o rótulo), como publicado.</summary>
    public static readonly string[] Art4 =
    {
        "Eficiência e economicidade: uso racional dos recursos de TIC, evitando redundâncias e priorizando soluções corporativas compartilhadas;",
        "Alinhamento estratégico: subordinação das iniciativas de TIC às prioridades do GDF definidas no Plano Estratégico de TIC e na Estratégia de Governança Digital;",
        "Centralização orientada ao valor: preferência por soluções e infraestruturas corporativas centralizadas quando gerarem ganhos de escala, segurança ou padronização;",
        "Transparência e prestação de contas: publicidade dos instrumentos de planejamento, das decisões e dos resultados de governança de TIC;",
        "Segurança e resiliência: proteção dos ativos de informação e garantia de continuidade dos serviços essenciais;",
        "Interoperabilidade: integração e comunicação entre sistemas, dados e plataformas, eliminando silos tecnológicos;",
        "Transformação digital orientada ao cidadão: uso da tecnologia para ampliar a qualidade, a acessibilidade e a eficiência dos serviços públicos; e",
        "Integridade pública: alinhamento das condutas dos agentes públicos a valores éticos, priorizando o interesse coletivo sobre interesses privados. É a base da boa governança, prevenindo desvios e garantindo serviços eficientes e transparentes.",
        "Segregação de funções nas contratações: separação das atribuições entre diferentes agentes públicos, de modo a evitar conflitos de interesse, erros, fraudes e ocultação de falhas nas contratações;",
        "uso ético e responsável da Inteligência Artificial: as iniciativas de TIC que envolvam sistemas de IA observarão os princípios e as diretrizes estabelecidos na PGIA/DF; e",
        "Governança de Dados orientada ao valor público: os dados gerados e tratados no âmbito das iniciativas de TIC serão geridos conforme as diretrizes da PGD/DF, assegurando qualidade, integridade e uso estratégico."
    };

    private static readonly string[] Romanos = { "I", "II", "III", "IV", "V", "VI", "VII", "VIII", "IX", "X", "XI" };

    [Fact]
    public void Secoes_DoDfEDoPetic_ForaDoPdtic_ComSituacaoGeral_SemNiveis()
    {
        var df = Context.PeSecoes.Where(s => s.Escopo == PeDominios.Escopo.Df).OrderBy(s => s.Ordem).ToList();
        var petic = Context.PeSecoes.Where(s => s.Escopo == PeDominios.Escopo.Petic).OrderBy(s => s.Ordem).ToList();

        Assert.Equal(new[] { "principio", "diretriz_ciclo" }, df.Select(s => s.Chave));
        Assert.Equal(new[] { "PR", "DC" }, df.Select(s => s.PrefixoCodigo));
        Assert.Equal(new[] { "petic_identidade", "petic_diretriz", "petic_objetivo_programa", "petic_objetivo",
            "petic_prioridade", "petic_indicador", "petic_iniciativa", "petic_eixo" }, petic.Select(s => s.Chave));
        Assert.Equal(new string?[] { null, "D", "OP", "OE", "P", "IE", "IN", "EG" }, petic.Select(s => s.PrefixoCodigo));
        // O que o decreto pede (art. 11, § 1º) é obrigatório; identidade, objetivos do programa e eixos, opcionais
        Assert.Equal(new[] { "opcional", "obrigatorio", "opcional", "obrigatorio", "obrigatorio", "obrigatorio", "obrigatorio", "opcional" },
            petic.Select(s => s.SituacaoGeral));

        var todas = df.Concat(petic).ToList();
        Assert.All(todas, s =>
        {
            Assert.Null(s.PassoId);
            Assert.True(s.Sistema);
            Assert.False(s.Travada);
            Assert.Single(Context.PeCampos.Where(c => c.SecaoId == s.Id && c.Principal));
        });
        var ids = todas.Select(s => s.Id).ToList();
        Assert.Empty(Context.PeSecoesNivel.Where(n => ids.Contains(n.SecaoId)));
        Assert.All(Context.PeCampos.Where(c => ids.Contains(c.SecaoId)).ToList(), c =>
        {
            Assert.NotNull(c.SituacaoGeral);
            Assert.True(c.Sistema);
            Assert.Empty(Context.PeCamposNivel.Where(n => n.CampoId == c.Id));
        });
    }

    [Fact]
    public void CamposDoPetic_LigacoesDentroDaVersao_EListaDaDiretrizDoCiclo()
    {
        Assert.Equal("petic_objetivo_programa", PeConfigCampo.SecaoDaLigacao(Campo("petic_objetivo", "objetivo_programa").Config));
        Assert.Equal("petic_objetivo", PeConfigCampo.SecaoDaLigacao(Campo("petic_indicador", "objetivo").Config));
        Assert.Equal("petic_objetivo", PeConfigCampo.SecaoDaLigacao(Campo("petic_iniciativa", "objetivo").Config));
        Assert.Equal("obrigatorio", Campo("petic_indicador", "objetivo").SituacaoGeral);
        Assert.Equal("opcional", Campo("petic_objetivo", "objetivo_programa").SituacaoGeral);
        Assert.Equal(new[] { "nome", "objetivo", "formula", "unidade", "linha_base", "meta", "prazo", "fonte" },
            Context.PeCampos.Where(c => c.SecaoId == Secao("petic_indicador").Id).OrderBy(c => c.Ordem).Select(c => c.Chave));
        // Na seção opcional, o principal é obrigatório quando alguém inclui um item
        Assert.Equal("obrigatorio", Campo("petic_identidade", "missao").SituacaoGeral);
        Assert.Equal("obrigatorio", Campo("petic_eixo", "nome").SituacaoGeral);

        var situacao = Campo("diretriz_ciclo", "situacao_deliberacao");
        Assert.Equal(new[] { "proposta", "em_deliberacao", "aprovada", "devolvida" },
            Context.PeOpcoes.Where(o => o.CampoId == situacao.Id).OrderBy(o => o.Ordem).Select(o => o.Valor));
        using var ano = JsonDocument.Parse(Campo("diretriz_ciclo", "ano").Config);
        Assert.Equal(0, ano.RootElement.GetProperty("casas").GetInt32());
    }

    [Fact]
    public void Principios_OsOnzeDoArt4_RegistrosDoSistema_ComTextoLiteral()
    {
        var secao = Secao("principio");
        var registros = Context.PeRegistros.AsNoTracking().Where(r => r.SecaoId == secao.Id).OrderBy(r => r.Ordem).ToList();

        Assert.Equal(11, registros.Count);
        Assert.Equal(Enumerable.Range(1, 11).Select(n => $"PR{n:00}"), registros.Select(r => r.Codigo));
        Assert.Equal(Enumerable.Range(1, 11), registros.Select(r => r.Ordem));
        Assert.All(registros, r =>
        {
            Assert.True(r.Sistema);
            Assert.Null(r.PeticId);
        });
        for (var i = 0; i < 11; i++)
        {
            var dados = PeRegistroDados.Ler(registros[i].Dados);
            Assert.Equal(Art4[i], PeRegistroDados.Texto(dados["texto"]));
            Assert.Equal($"art. 4º, {Romanos[i]}, do Decreto nº 48.900/2026", PeRegistroDados.Texto(dados["fundamento"]));
            Assert.True(dados["criterio_priorizacao"]!.GetValue<bool>());
        }

        // O próximo princípio acrescentado continua a sequência
        var sequencia = Context.PeRegistroSequencias.AsNoTracking().Single(s => s.SecaoId == secao.Id);
        Assert.Equal("df", sequencia.Dono);
        Assert.Equal(11, sequencia.Ultimo);
    }

    [Fact]
    public async Task SegundaCarga_NaoDuplicaOsPrincipios()
    {
        var seed = PeCarregadorModelo.LerSeed();
        seed.Versao++;

        var resultado = await new PeCarregadorModelo(Context).CarregarAsync(seed);

        Assert.True(resultado.Executou);
        Assert.Equal(0, resultado.Registros + resultado.Secoes + resultado.Campos);
        Assert.Equal(11, Context.PeRegistros.Count(r => r.SecaoId == Secao("principio").Id));
    }

    [Fact]
    public async Task VersaoNova_NaoMexeNoQueOAdministradorFez_NemNaSequencia()
    {
        // O administrador acrescenta um princípio (PR12) e renomeia a seção
        var criado = await Registros.CriarAsync(PeDono.Df, "principio",
            Salvar(new { texto = "Preferência por software público.", fundamento = "Resolução CGTIC nº 1/2026" }), await Admin());
        Assert.Equal("PR12", criado.Codigo);
        await Modelo.AtualizarSecaoAsync(Secao("principio").Id,
            new api.Planejamento.PeSecaoAtualizarDTO { Titulo = "Princípios", Informados = new HashSet<string> { "Titulo" } }, EmailAdmin);

        var seed = PeCarregadorModelo.LerSeed();
        seed.Versao++;
        await new PeCarregadorModelo(Context).CarregarAsync(seed);

        Assert.Equal("Princípios", Secao("principio").Titulo);
        Assert.Equal(12, Context.PeRegistros.Count(r => r.SecaoId == Secao("principio").Id));
        Assert.Equal(12, Context.PeRegistroSequencias.AsNoTracking().Single(s => s.SecaoId == Secao("principio").Id).Ultimo);
    }

    [Fact]
    public async Task BaseComAVersao1_RecebeSoOsReferenciais()
    {
        using var vazio = new PeBancoVazio();
        var v1 = PeCarregadorModelo.LerSeed();
        v1.Versao = 1;
        v1.SecoesForaDoPdtic.Clear();
        await new PeCarregadorModelo(vazio.Context).CarregarAsync(v1);
        Assert.Empty(vazio.Context.PeSecoes.Where(s => s.Escopo != PeDominios.Escopo.Pdtic));
        var passos = vazio.Context.PePassos.Count();

        var resultado = await new PeCarregadorModelo(vazio.Context).CarregarAsync();

        Assert.True(resultado.Executou);
        Assert.Equal(1, resultado.VersaoAnterior);
        Assert.Equal(10, resultado.Secoes);
        Assert.Equal(11, resultado.Registros);
        Assert.Equal(0, resultado.Passos + resultado.Niveis + resultado.Etapas);
        Assert.Equal(passos, vazio.Context.PePassos.Count());
        Assert.Equal(11, vazio.Context.PeRegistros.Count(r => r.Sistema));
    }

    // ── Validação do JSON ─────────────────────────────────────────────────────

    [Fact]
    public void Json_RegistroComCampoQueNaoExiste_Recusa()
    {
        var seed = PeCarregadorModelo.LerSeed();
        seed.SecoesForaDoPdtic.Single(s => s.Chave == "principio").Registros[0].Dados["inventado"] =
            JsonSerializer.SerializeToElement("x");

        var ex = Assert.Throws<InvalidOperationException>(() => PeCarregadorModelo.ValidarSeed(seed));
        Assert.Contains("inventado", ex.Message);
    }

    [Fact]
    public void Json_RegistroForaDoCatalogoDoDf_ECodigoForaDoPrefixo_Recusam()
    {
        var seed = PeCarregadorModelo.LerSeed();
        seed.SecoesForaDoPdtic.Single(s => s.Chave == "petic_diretriz").Registros.Add(new PeSeedRegistro
        {
            Codigo = "D01",
            Dados = new Dictionary<string, JsonElement> { ["texto"] = JsonSerializer.SerializeToElement("Diretriz") }
        });
        Assert.Throws<InvalidOperationException>(() => PeCarregadorModelo.ValidarSeed(seed));

        var outro = PeCarregadorModelo.LerSeed();
        outro.SecoesForaDoPdtic.Single(s => s.Chave == "principio").Registros[0].Codigo = "XX01";
        Assert.Throws<InvalidOperationException>(() => PeCarregadorModelo.ValidarSeed(outro));
    }

    [Fact]
    public void Json_SecaoForaDoPdticComNiveisOuTrava_Recusa()
    {
        var seed = PeCarregadorModelo.LerSeed();
        seed.SecoesForaDoPdtic[0].Travada = true;
        Assert.Throws<InvalidOperationException>(() => PeCarregadorModelo.ValidarSeed(seed));

        var outro = PeCarregadorModelo.LerSeed();
        outro.SecoesForaDoPdtic[0].Chave = "abrangencia";
        Assert.Throws<InvalidOperationException>(() => PeCarregadorModelo.ValidarSeed(outro));
    }

    [Fact]
    public async Task Json_RegistroSemCampoObrigatorio_RecusaSemGravarNada()
    {
        using var vazio = new PeBancoVazio();
        var seed = PeCarregadorModelo.LerSeed();
        seed.SecoesForaDoPdtic.Single(s => s.Chave == "principio").Registros[3].Dados.Remove("fundamento");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => new PeCarregadorModelo(vazio.Context).CarregarAsync(seed));
        Assert.Contains("PR04", ex.Message);
        Assert.Empty(vazio.Context.PeSecoes);
    }
}
