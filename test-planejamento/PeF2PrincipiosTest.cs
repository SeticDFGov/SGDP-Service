using api.Planejamento;
using Microsoft.EntityFrameworkCore;
using Models.Planejamento;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using service.Planejamento;
using Xunit;

namespace test.planejamento;

/// <summary>
/// F2 (segunda leva), passo 1.8: ao abrir o PDTIC (POST pdtic), os princípios do art. 4º do
/// Decreto nº 48.900/2026 entram como sugestão na seção dos princípios e diretrizes, uma linha
/// por princípio do catálogo do DF, com a origem "art. 4º", o texto (sem a pontuação da
/// enumeração do decreto), o documento de origem e "serve de critério". São registros comuns,
/// que o órgão muda e apaga; o registro fora do sistema não os recebe, e a revisão os leva pela
/// cópia de sempre.
/// </summary>
public class PeF2PrincipiosTest : PePaineisTestBase
{
    private const string SecaoPrincipios = "principios_diretrizes";

    private async Task<List<PeRegistroResponse>> PrincipiosAsync(long pdticId, app.Models.User? user = null) =>
        (await Registros.ListarAsync(PeDono.DoPdtic(pdticId), SecaoPrincipios, await ContextoDe(user ?? UserOrgaoSes))).Registros;

    private List<PeRegistro> PrincipiosNoBanco(long pdticId)
    {
        var secaoId = Context.PeSecoes.AsNoTracking().Single(s => s.Chave == SecaoPrincipios).Id;
        return Context.PeRegistros.AsNoTracking().Where(r => r.PdticId == pdticId && r.SecaoId == secaoId).OrderBy(r => r.Ordem).ToList();
    }

    private static byte[] PdfDeUmaPagina()
    {
        QuestPDF.Settings.License = LicenseType.Community;
        return Document.Create(c => c.Page(p => p.Content().Text("PDTIC aprovado fora do sistema."))).GeneratePdf();
    }

    [Fact]
    public async Task Abrir_TrazOsOnzePrincipiosDoArt4_ComAOrigem_EOPassoJaFeito()
    {
        var pdtic = await AbrirSesAsync();

        var linhas = await PrincipiosAsync(pdtic.Id);
        Assert.Equal(Enumerable.Range(1, 11).Select(n => $"PD{n:00}"), linhas.Select(l => l.Codigo));
        Assert.All(linhas, l => Assert.False(l.Sistema));
        Assert.All(linhas, l => Assert.Equal("art4", Valor(l, "origem")));
        Assert.All(linhas, l => Assert.Equal("Art. 4º do Decreto nº 48.900/2026", l.Rotulos["origem"]));
        Assert.All(linhas, l => Assert.Equal(UserOrgaoSes.Email, l.CriadoPor));

        // O texto de cada inciso, na ordem do decreto, sem o ";" e o "; e" que o ligam ao seguinte
        Assert.Equal("Eficiência e economicidade: uso racional dos recursos de TIC, evitando redundâncias e priorizando soluções "
                     + "corporativas compartilhadas.", Valor(linhas[0], "principio"));
        Assert.StartsWith("Transformação digital orientada ao cidadão:", Valor(linhas[6], "principio"));
        Assert.EndsWith("dos serviços públicos.", Valor(linhas[6], "principio"));
        Assert.EndsWith("garantindo serviços eficientes e transparentes.", Valor(linhas[7], "principio"));
        // O inciso X começa em minúscula no decreto: a linha começa em maiúscula
        Assert.StartsWith("Uso ético e responsável da Inteligência Artificial:", Valor(linhas[9], "principio"));
        Assert.EndsWith("na PGIA/DF.", Valor(linhas[9], "principio"));
        Assert.StartsWith("Governança de Dados orientada ao valor público:", Valor(linhas[10], "principio"));
        Assert.All(linhas, l => Assert.DoesNotMatch(";( e)?$", Valor(l, "principio")));

        // A seção obrigatória já tem as linhas: o passo dos princípios nasce feito, e o PDTIC não conta como alterado
        Assert.Equal("feito", (await PassoAsync(pdtic.Id, "preparacao.principios")).Situacao);
        Assert.Null(PdticNoBanco(pdtic.Id).AlteradoEm);
    }

    [Fact]
    public async Task OsCamposQueONivelEsconde_GuardamOValor_EAparecemNoNivelQueOsLiga()
    {
        var pdtic = await AbrirSesAsync();

        // No Básico, o documento de origem e o "serve de critério" estão desligados: não aparecem...
        var noBasico = (await PrincipiosAsync(pdtic.Id))[0];
        Assert.False(noBasico.Dados.ContainsKey("fonte"));
        Assert.False(noBasico.Dados.ContainsKey("criterio_priorizacao"));
        // ...mas guardam o valor (decisão 13): o fundamento do catálogo e o "pode ser critério"
        var guardado = PeRegistroDados.Ler(PrincipiosNoBanco(pdtic.Id)[0].Dados);
        Assert.Equal("art. 4º, I, do Decreto nº 48.900/2026", PeRegistroDados.Texto(guardado["fonte"]));
        Assert.True(guardado["criterio_priorizacao"]!.GetValue<bool>());

        await DefinirNivelDoOrgaoAsync(OrgaoSes, "avancado");
        var noAvancado = await PrincipiosAsync(pdtic.Id);
        Assert.Equal("art. 4º, XI, do Decreto nº 48.900/2026", Valor(noAvancado[10], "fonte"));
        Assert.Equal("Sim", noAvancado[10].Rotulos["criterio_priorizacao"]);
    }

    [Fact]
    public async Task SaoSugestoes_OOrgaoMudaEApaga_EONovoGanhaOProximoCodigo()
    {
        var pdtic = await AbrirSesAsync();
        var dono = PeDono.DoPdtic(pdtic.Id);
        var linhas = await PrincipiosAsync(pdtic.Id);

        await Registros.ExcluirAsync(dono, SecaoPrincipios, linhas[2].Id, await Orgao());
        var mudado = await Registros.AtualizarAsync(dono, SecaoPrincipios, linhas[0].Id,
            Salvar(new { principio = "Eficiência e economicidade no uso da TIC da Saúde.", origem = "orgao" }), await Orgao());
        var novo = await IncluirNoPdticAsync(pdtic.Id, SecaoPrincipios, new { principio = "O paciente no centro das decisões de TIC.", origem = "orgao" });

        Assert.Equal(("orgao", "Eficiência e economicidade no uso da TIC da Saúde."), (Valor(mudado, "origem"), Valor(mudado, "principio")));
        Assert.Equal("PD12", novo.Codigo);
        var depois = await PrincipiosAsync(pdtic.Id);
        Assert.Equal(11, depois.Count);
        Assert.DoesNotContain(depois, l => l.Codigo == "PD03");

        // Apagadas todas, o passo volta a pedir pelo menos um item
        foreach (var linha in depois) await Registros.ExcluirAsync(dono, SecaoPrincipios, linha.Id, await Orgao());
        Assert.Equal("pendente", (await PassoAsync(pdtic.Id, "preparacao.principios")).Situacao);
    }

    [Fact]
    public async Task RegistroForaDoSistema_NaoRecebeOsPrincipios()
    {
        var arquivo = await EnviarArquivoAsync(UserOrgaoSes, "PDTIC SES.pdf", PdfDeUmaPagina());
        var pdtic = await Aprovacao.RegistrarExternoAsync(new PeRegistroExternoDTO
        {
            Versao = "1.0",
            VigenciaInicio = "2025-01-01",
            VigenciaFim = "2028-12-31",
            ArquivoId = arquivo.Id,
            AprovacaoInstancia = "cgtic",
            AprovacaoData = "2025-03-10",
            AprovacaoAtoTipo = "Resolução",
            AprovacaoAtoNumero = "4/2025",
            PublicacaoData = "2025-03-20",
            PublicacaoEndereco = Endereco
        }, await Orgao());

        Assert.Empty(PrincipiosNoBanco(pdtic.Id));
    }

    [Fact]
    public async Task Revisao_LevaOsPrincipiosPelaCopia_ComAsMudancasDoOrgao()
    {
        var pdtic = await AbrirSesAsync();
        var linhas = await PrincipiosAsync(pdtic.Id);
        await Registros.ExcluirAsync(PeDono.DoPdtic(pdtic.Id), SecaoPrincipios, linhas[4].Id, await Orgao());
        // O preenchimento do Básico inclui mais um princípio do órgão (PD12)
        await PreencherElaboracaoAsync(pdtic.Id);
        await EnviarAsync(pdtic.Id);
        await AprovarNoCgticAsync(pdtic.Id);
        await PublicarAsync(pdtic.Id);
        var vigente = (await PrincipiosAsync(pdtic.Id)).Select(l => (l.Codigo, Valor(l, "principio"))).ToList();

        var revisao = await Aprovacao.RevisarAsync(pdtic.Id, new PeRevisaoDTO { Justificativa = "O órgão mudou de estrutura." }, await Orgao());

        // A cópia de sempre: os mesmos códigos e textos, sem os princípios de novo (a revisão não é uma abertura)
        Assert.Equal(vigente, (await PrincipiosAsync(revisao.Id)).Select(l => (l.Codigo, Valor(l, "principio"))).ToList());
        Assert.Equal(11, vigente.Count);
        Assert.DoesNotContain(vigente, l => l.Codigo == "PD05");
    }

    [Fact]
    public async Task SemASecaoParaOOrgao_OuSemOsPrincipiosNoCatalogo_OPdticAbreSemLinhas()
    {
        // O passo 1.8 desligado por um ajuste do órgão: a seção não aparece e nada entra
        await AjustarPassosAsync(OrgaoSes, ("preparacao.principios", "desligado"));
        var ses = await AbrirSesAsync();
        Assert.Empty(PrincipiosNoBanco(ses.Id));

        // O catálogo do DF sem os princípios do sistema (antes de o carregador trazer os referenciais)
        var catalogo = Context.PeSecoes.Single(s => s.Chave == "principio");
        Context.PeRegistros.RemoveRange(Context.PeRegistros.Where(r => r.SecaoId == catalogo.Id && r.Sistema));
        Context.SaveChanges();
        Context.ChangeTracker.Clear();
        var seec = await AbrirSeecAsync();
        Assert.Empty(PrincipiosNoBanco(seec.Id));
        Assert.Equal("em_elaboracao", seec.Situacao);
    }

    [Theory]
    [InlineData("Eficiência e economicidade: uso racional;", "Eficiência e economicidade: uso racional.")]
    [InlineData("Transformação digital orientada ao cidadão: serviços públicos; e", "Transformação digital orientada ao cidadão: serviços públicos.")]
    [InlineData("uso ético e responsável da IA; e", "Uso ético e responsável da IA.")]
    [InlineData("Integridade pública: a base da boa governança. Previne desvios.", "Integridade pública: a base da boa governança. Previne desvios.")]
    [InlineData("  Governança de dados  ", "Governança de dados.")]
    [InlineData("  ", "")]
    public void TextoDoPrincipio_SemAPontuacaoDaEnumeracao(string texto, string esperado) =>
        Assert.Equal(esperado, PeRegistroService.TextoDoPrincipio(texto));
}
