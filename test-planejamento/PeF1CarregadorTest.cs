using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Models;
using Models.Planejamento;
using service.Planejamento;
using Xunit;

namespace test.planejamento;

/// <summary>
/// F1: a versão 7 do carregador num banco que já tinha a versão 6. Os textos que o próprio
/// carregador gravou e que ninguém mudou passam aos de hoje (os "antes" do JSON: a ajuda de
/// "Quem decidiu", o passo 5.1, a sigla do órgão e a capa do RA); as regras novas do config (a
/// data que não pode ser futura e o formato do processo SEI e do endereço) entram nos campos que
/// já existiam; o que o administrador mudou fica.
/// </summary>
public class PeF1CarregadorTest
{
    private const string PassoDoCiclo = "monitoramento.ciclo-monitoramento";

    [Fact]
    public void Conteudo_DaVersao7_OsAntesEAsRegrasNovas()
    {
        var seed = PeCarregadorModelo.LerSeed();
        var campos = CamposDoSeed(seed).ToList();

        Assert.Equal(PeCarregadorModelo.VersaoDaRevisaoFinal, seed.Versao);
        Assert.Single(seed.Etapas.SelectMany(e => e.Passos), p => p.Antes != null);
        // A sigla do órgão e a ajuda de "Quem decidiu" nas cinco aprovações
        Assert.Equal(6, campos.Count(c => c.Antes != null));
        // 11 datas que não podem ser futuras, 10 processos SEI e 2 endereços
        Assert.Equal(11, campos.Count(c => Tem(c.Config, "naoFutura")));
        Assert.Equal(12, campos.Count(c => Tem(c.Config, "formato")));
        Assert.Equal(23, campos.Count(c => TemChaveNova(c.Config)));
        Assert.Single(BlocosDoSeed(PeDocSeed.Ler()), b => b.Antes != null);
    }

    [Fact]
    public async Task Da6Para7_TrocaOsTextosDoCarregador_EAcrescentaAsRegrasNovas()
    {
        using var banco = new PeBancoVazio();
        var (v6, documentos6) = Versao6();
        await new PeCarregadorModelo(banco.Context).CarregarAsync(v6, documentos6);

        // Na versão 6: os textos antigos e nenhuma regra nova
        await using (var antes = new AppDbContext(banco.Opcoes))
        {
            Assert.Equal("Atualize a situação das ações e registre as medições e os riscos que ocorreram", Passo(antes, PassoDoCiclo).Titulo);
            Assert.Equal("Em geral, o SGTIC. Mude quando outra instância decidiu.", Campo(antes, "aprovacao_inventario", "instancia").Ajuda);
            Assert.Null(Campo(antes, "nomes", "sigla_orgao").Largura);
            Assert.False(Config(Campo(antes, "publicacao", "data")).ContainsKey("naoFutura"));
            Assert.Equal("{\"max\":30}", Config(Campo(antes, "sgtic", "sei")).ToJsonString());
            Assert.Contains("{ciclo.rotulo}", CapaDoRa(antes).Config);
        }

        PeCarregamentoResultado resultado;
        await using (var carga = new AppDbContext(banco.Opcoes))
            resultado = await new PeCarregadorModelo(carga).CarregarAsync();

        var seed = PeCarregadorModelo.LerSeed();
        Assert.True(resultado.Executou);
        Assert.Equal((PeCarregadorModelo.VersaoDoAcompanhamento, PeCarregadorModelo.VersaoDaRevisaoFinal),
            (resultado.VersaoAnterior, resultado.Versao));
        Assert.Equal(0, resultado.Niveis + resultado.Etapas + resultado.Passos + resultado.Secoes + resultado.Campos + resultado.Opcoes
                        + resultado.Documentos + resultado.Capitulos + resultado.Blocos + resultado.Fluxos + resultado.Registros);
        // 1 passo, 6 campos com "antes", 23 campos com regra nova e a capa do RA
        Assert.Equal(31, resultado.Correcoes);

        await using var depois = new AppDbContext(banco.Opcoes);
        var passo = Passo(depois, PassoDoCiclo);
        var doSeed = seed.Etapas.SelectMany(e => e.Passos).Single(p => p.Chave == PassoDoCiclo);
        Assert.Equal((doSeed.Titulo, doSeed.OQueFazer), (passo.Titulo, passo.OQueFazer));
        Assert.Equal(PeCarregadorModelo.Autor, passo.AlteradoPor);

        foreach (var (secao, sc) in CamposComSecao(seed).Where(x => x.Campo.Antes != null))
        {
            var campo = Campo(depois, secao, sc.Chave);
            Assert.Equal(sc.Ajuda, campo.Ajuda);
            Assert.Equal(sc.Largura, campo.Largura);
            Assert.Equal(PeCarregadorModelo.Autor, campo.AlteradoPor);
        }
        Assert.Equal("Em geral, quem aprova o relatório de resultados final é a autoridade máxima do órgão. Mude quando outra instância decidiu.",
            Campo(depois, "aprovacao_resultados_autoridade", "instancia").Ajuda);
        var sigla = Campo(depois, "nomes", "sigla_orgao");
        Assert.Equal(("estreita", "{\"max\":20}"), (sigla.Largura, Config(sigla).ToJsonString()));

        // As regras novas em todos os campos que as trazem, sem perder as outras chaves
        foreach (var (secao, sc) in CamposComSecao(seed).Where(x => TemChaveNova(x.Campo.Config)))
        {
            var config = Config(Campo(depois, secao, sc.Chave));
            foreach (var propriedade in sc.Config!.Value.EnumerateObject())
                Assert.Equal(JsonNode.Parse(propriedade.Value.GetRawText())!.ToJsonString(), config[propriedade.Name]?.ToJsonString());
        }
        Assert.True(Config(Campo(depois, "publicacao", "data"))["naoFutura"]!.GetValue<bool>());
        Assert.Equal("url", Config(Campo(depois, "publicacao", "endereco"))["formato"]!.GetValue<string>());
        Assert.Equal((30, "sei"), (Config(Campo(depois, "sgtic", "sei"))["max"]!.GetValue<int>(),
            Config(Campo(depois, "sgtic", "sei"))["formato"]!.GetValue<string>()));

        // A capa do RA: só a vigência e a versão (o ciclo já está no título da capa)
        var capa = CapaDoRa(depois).Config;
        Assert.DoesNotContain("{ciclo.rotulo}", capa);
        Assert.DoesNotContain("{ciclo.inicio}", capa);
        Assert.Contains("{vigencia.inicio}", capa);
        Assert.Contains("{pdtic.versao}", capa);

        // A versão 7 já carregada: nada mais
        await using var outra = new AppDbContext(banco.Opcoes);
        Assert.False((await new PeCarregadorModelo(outra).CarregarAsync()).Executou);
    }

    [Fact]
    public async Task Da6Para7_OQueOAdministradorMudou_Fica()
    {
        using var banco = new PeBancoVazio();
        var (v6, documentos6) = Versao6();
        await new PeCarregadorModelo(banco.Context).CarregarAsync(v6, documentos6);

        await using (var admin = new AppDbContext(banco.Opcoes))
        {
            Passo(admin, PassoDoCiclo, rastrear: true).Titulo = "Atualize as ações do ciclo";
            Campo(admin, "aprovacao_inventario", "instancia", rastrear: true).Ajuda = "Quem decide aqui é o comitê do órgão.";
            Campo(admin, "nomes", "sigla_orgao", rastrear: true).Config = "{\"max\":40}";
            Campo(admin, "sgtic", "sei", rastrear: true).Config = "{\"max\":25}";
            var capa = CapaDoRa(admin, rastrear: true);
            var original = capa.Config;
            capa.Config = original.Replace("Ciclo {ciclo.rotulo}", "Ciclo de referência {ciclo.rotulo}");
            Assert.NotEqual(original, capa.Config);
            await admin.SaveChangesAsync();
        }

        PeCarregamentoResultado resultado;
        await using (var carga = new AppDbContext(banco.Opcoes))
            resultado = await new PeCarregadorModelo(carga).CarregarAsync();

        // A ajuda de "Quem decidiu" do inventário e a capa do RA ficaram de fora
        Assert.Equal(29, resultado.Correcoes);

        await using var depois = new AppDbContext(banco.Opcoes);
        var seed = PeCarregadorModelo.LerSeed();
        var doSeed = seed.Etapas.SelectMany(e => e.Passos).Single(p => p.Chave == PassoDoCiclo);
        // Cada texto é conferido sozinho: o título mudado fica, o "o que fazer" que ninguém mudou passa ao de hoje
        var passo = Passo(depois, PassoDoCiclo);
        Assert.Equal(("Atualize as ações do ciclo", doSeed.OQueFazer), (passo.Titulo, passo.OQueFazer));

        Assert.Equal("Quem decide aqui é o comitê do órgão.", Campo(depois, "aprovacao_inventario", "instancia").Ajuda);
        Assert.Equal("Em geral, quem aprova o plano de trabalho é o comitê interno de TIC (SGTIC). Mude quando outra instância decidiu.",
            Campo(depois, "aprovacao_plano_trabalho", "instancia").Ajuda);

        // O limite que o administrador escolheu fica; a largura, que ninguém mudou, passa à de hoje
        var sigla = Campo(depois, "nomes", "sigla_orgao");
        Assert.Equal(("estreita", "{\"max\":40}"), (sigla.Largura, Config(sigla).ToJsonString()));

        // A regra nova entra ao lado do que o administrador mudou no config
        var sei = Config(Campo(depois, "sgtic", "sei"));
        Assert.Equal((25, "sei"), (sei["max"]!.GetValue<int>(), sei["formato"]!.GetValue<string>()));

        Assert.Contains("Ciclo de referência {ciclo.rotulo}", CapaDoRa(depois).Config);
    }

    [Fact]
    public async Task DepoisDa7_ARegraQueOAdministradorTirou_NaoVolta()
    {
        using var banco = new PeBancoVazio();
        await new PeCarregadorModelo(banco.Context).CarregarAsync();

        await using (var admin = new AppDbContext(banco.Opcoes))
        {
            Campo(admin, "publicacao", "data", rastrear: true).Config = "{}";
            Campo(admin, "sgtic", "sei", rastrear: true).Config = "{\"max\":30}";
            await admin.SaveChangesAsync();
        }

        var seed = PeCarregadorModelo.LerSeed();
        seed.Versao++;
        PeCarregamentoResultado resultado;
        await using (var carga = new AppDbContext(banco.Opcoes))
            resultado = await new PeCarregadorModelo(carga).CarregarAsync(seed);

        Assert.True(resultado.Executou);
        Assert.Equal(0, resultado.Correcoes);
        await using var depois = new AppDbContext(banco.Opcoes);
        Assert.Empty(Config(Campo(depois, "publicacao", "data")));
        Assert.Equal("{\"max\":30}", Config(Campo(depois, "sgtic", "sei")).ToJsonString());
    }

    // ── Apoio ───────────────────────────────────────────────────────────────

    /// <summary>
    /// O JSON como era na versão 6: os textos dos "antes" no lugar dos de hoje, sem as chaves novas
    /// do config e com a capa do RA de quatro linhas.
    /// </summary>
    private static (PeSeedModelo Seed, PeSeedDocumentos Documentos) Versao6()
    {
        var seed = PeCarregadorModelo.LerSeed();
        seed.Versao = PeCarregadorModelo.VersaoDoAcompanhamento;
        foreach (var passo in seed.Etapas.SelectMany(e => e.Passos).Where(p => p.Antes != null))
        {
            passo.Titulo = passo.Antes!.Titulo?.FirstOrDefault() ?? passo.Titulo;
            passo.OQueFazer = passo.Antes.OQueFazer?.FirstOrDefault() ?? passo.OQueFazer;
            passo.Antes = null;
        }
        foreach (var campo in CamposDoSeed(seed))
        {
            if (campo.Antes != null)
            {
                if (campo.Antes.Ajuda is { Count: > 0 } ajuda) campo.Ajuda = ajuda[0];
                if (campo.Antes.Config is { Count: > 0 } config) campo.Config = config[0];
                if (campo.Antes.Largura is { Count: > 0 } largura) campo.Largura = largura[0];
                campo.Antes = null;
            }
            campo.Config = SemAsChavesNovas(campo.Config);
        }

        var documentos = PeDocSeed.Ler();
        foreach (var bloco in BlocosDoSeed(documentos).Where(b => b.Antes != null))
        {
            bloco.Texto = bloco.Antes![0];
            bloco.Antes = null;
        }
        return (seed, documentos);
    }

    private static IEnumerable<PeSeedCampo> CamposDoSeed(PeSeedModelo seed) => CamposComSecao(seed).Select(x => x.Campo);

    private static IEnumerable<(string Secao, PeSeedCampo Campo)> CamposComSecao(PeSeedModelo seed) =>
        seed.Etapas.SelectMany(e => e.Passos).SelectMany(p => p.Secoes).Concat(seed.SecoesForaDoPdtic)
            .SelectMany(s => s.Campos.Select(c => (s.Chave, c)));

    private static IEnumerable<PeSeedDocBloco> BlocosDoSeed(PeSeedDocumentos documentos)
    {
        static IEnumerable<PeSeedDocCapitulo> Todos(IEnumerable<PeSeedDocCapitulo> capitulos) =>
            capitulos.SelectMany(c => new[] { c }.Concat(Todos(c.Subcapitulos)));
        return documentos.Modelos.SelectMany(m => Todos(m.Capitulos)).SelectMany(c => c.Blocos);
    }

    private static JsonElement? SemAsChavesNovas(JsonElement? config)
    {
        if (config is not { ValueKind: JsonValueKind.Object } objeto) return config;
        var sem = JsonNode.Parse(objeto.GetRawText())!.AsObject();
        foreach (var chave in PeCarregadorModelo.ChavesNovasDoConfig) sem.Remove(chave);
        return sem.Count == 0 ? null : JsonSerializer.SerializeToElement(sem);
    }

    private static bool Tem(JsonElement? config, string chave) =>
        config is { ValueKind: JsonValueKind.Object } objeto && objeto.TryGetProperty(chave, out _);

    private static bool TemChaveNova(JsonElement? config) => PeCarregadorModelo.ChavesNovasDoConfig.Any(chave => Tem(config, chave));

    private static PePasso Passo(AppDbContext contexto, string chave, bool rastrear = false) =>
        (rastrear ? contexto.PePassos : contexto.PePassos.AsNoTracking()).Single(p => p.Chave == chave);

    private static PeCampo Campo(AppDbContext contexto, string secao, string chave, bool rastrear = false) =>
        (rastrear ? contexto.PeCampos : contexto.PeCampos.AsNoTracking()).Single(c => c.Chave == chave && c.Secao!.Chave == secao);

    private static PeDocBloco CapaDoRa(AppDbContext contexto, bool rastrear = false) =>
        (rastrear ? contexto.PeDocBlocos : contexto.PeDocBlocos.AsNoTracking())
            .Single(b => b.Tipo == PeDominios.TipoBloco.Texto && b.Capitulo!.Chave == "capa"
                         && b.Capitulo.Modelo!.Tipo == PeDominios.TipoDocumento.Ra);

    private static JsonObject Config(PeCampo campo) =>
        JsonNode.Parse(string.IsNullOrWhiteSpace(campo.Config) ? "{}" : campo.Config)!.AsObject();
}
