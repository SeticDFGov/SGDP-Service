using System.Reflection;
using api.Contratacoes;
using Controllers.Contratacoes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Models.Contratacoes;
using service.Contratacoes;
using Xunit;

namespace test.contratacoes;

/// <summary>
/// Painel público (GET api/contratacoes/publico/painel): só contratos assinados da supervisão
/// contínua, só os campos abertos, a ordem, a data da última alteração e o controller anônimo
/// com o limitador próprio.
/// </summary>
public class CtrPainelPublicoTest : CtrTestBase
{
    private readonly CtrProcessoService _service;

    public CtrPainelPublicoTest()
    {
        _service = NovoProcessoService();
    }

    private CtrProcesso Assinado(string numero, string sigla, int diasDesdeAssinatura, Action<CtrProcesso>? ajustar = null) =>
        SemearProcesso(numero, p =>
        {
            p.OrgaoSigla = sigla;
            p.OrgaoNome = "Órgão " + sigla;
            p.ChegadaSgdi = DiasAtras(diasDesdeAssinatura + 90);
            p.DataAssinaturaContrato = DiasAtras(diasDesdeAssinatura);
            p.ValorEstimado = 1000m * diasDesdeAssinatura;
            ajustar?.Invoke(p);
        });

    [Fact]
    public async Task Painel_SoContratosAssinados_DosProcessosAtivosDaSupervisaoContinua()
    {
        Assinado("04044-00000001/2026-11", "SES", 10);
        SemearProcesso("04044-00000002/2026-12", p => p.ChegadaSgdi = DiasAtras(20)); // ainda em análise
        Assinado("04044-00000003/2026-13", "PCDF", 15, p => p.Origem = CtrDominios.Origem.Tcdf); // comunicação do TCDF
        Assinado("04044-00000004/2026-14", "SEEC", 20, p => p.Ativo = false); // excluído
        Assinado("04044-00000005/2026-15", "SEEDF", 30, p => p.Restituido = true); // restituído e depois assinado

        var painel = await _service.MontarPainelPublicoAsync();

        Assert.Equal(new[] { "SES", "SEEDF" }, painel.Contratacoes.Select(c => c.OrgaoSigla));
    }

    [Fact]
    public async Task Painel_DevolveOsCamposAbertos_DoContratoMaisRecenteParaOMaisAntigo()
    {
        Assinado("04044-00000001/2026-11", "SEEC", 40);
        Assinado("04044-00000002/2026-12", "SES", 5, p =>
        {
            p.OrgaoNome = "Secretaria de Estado de Saúde";
            p.Objeto = "Fábrica de software para os sistemas da regulação";
            p.CategoriaObjeto = CtrDominios.CategoriaObjeto.DesenvolvimentoSoftware;
            p.ChegadaSgdi = new DateOnly(2026, 2, 10);
            p.ValorEstimado = 14_850_000.50m;
        });
        // Mesmo dia de assinatura: desempata pela sigla
        Assinado("04044-00000003/2026-13", "CAESB", 40);

        var painel = await _service.MontarPainelPublicoAsync();

        Assert.Equal(new[] { "SES", "CAESB", "SEEC" }, painel.Contratacoes.Select(c => c.OrgaoSigla));
        var ses = painel.Contratacoes[0];
        Assert.Equal("Secretaria de Estado de Saúde", ses.OrgaoNome);
        Assert.Equal("Fábrica de software para os sistemas da regulação", ses.Objeto);
        Assert.Equal(CtrDominios.CategoriaObjeto.DesenvolvimentoSoftware, ses.CategoriaObjeto);
        Assert.Equal(new DateOnly(2026, 2, 10), ses.ChegadaSgdi);
        Assert.Equal(DiasAtras(5), ses.DataAssinaturaContrato);
        Assert.Equal(14_850_000.50m, ses.ValorEstimado);
    }

    [Fact]
    public async Task Painel_ValorEChegadaNaoInformados_VemNulos()
    {
        Assinado("04044-00000001/2026-11", "SES", 10, p =>
        {
            p.ValorEstimado = null;
            p.ChegadaSgdi = null;
        });

        var contratacao = Assert.Single((await _service.MontarPainelPublicoAsync()).Contratacoes);

        Assert.Null(contratacao.ValorEstimado);
        Assert.Null(contratacao.ChegadaSgdi);
    }

    [Fact]
    public async Task Painel_AtualizadoEm_EAUltimaAlteracaoEntreOsPublicados()
    {
        var criado = new DateTime(2026, 9, 1, 12, 0, 0, DateTimeKind.Utc);
        var alterado = new DateTime(2026, 9, 20, 15, 30, 0, DateTimeKind.Utc);
        Assinado("04044-00000001/2026-11", "SES", 10, p => p.CriadoEm = criado);
        Assinado("04044-00000002/2026-12", "SEEC", 12, p => { p.CriadoEm = criado; p.AlteradoEm = alterado; });
        // Alteração mais nova, mas de um processo que não aparece no painel
        SemearProcesso("04044-00000003/2026-13", p => { p.CriadoEm = criado; p.AlteradoEm = alterado.AddDays(5); });

        var painel = await _service.MontarPainelPublicoAsync();

        Assert.Equal(alterado, painel.AtualizadoEm);
    }

    [Fact]
    public async Task Painel_SemContratoAssinado_ListaVaziaESemData()
    {
        SemearProcesso("04044-00000001/2026-11");

        var painel = await _service.MontarPainelPublicoAsync();

        Assert.Empty(painel.Contratacoes);
        Assert.Null(painel.AtualizadoEm);
    }

    [Fact]
    public void Contrato_SoOsCamposAbertos()
    {
        string[] Propriedades(Type tipo) => tipo.GetProperties().Select(p => p.Name).OrderBy(n => n).ToArray();

        Assert.Equal(
            new[] { "CategoriaObjeto", "ChegadaSgdi", "DataAssinaturaContrato", "Objeto", "OrgaoNome", "OrgaoSigla", "ValorEstimado" },
            Propriedades(typeof(CtrContratacaoPublicaResponse)));
        Assert.Equal(new[] { "AtualizadoEm", "Contratacoes" }, Propriedades(typeof(CtrPainelPublicoResponse)));
    }

    [Fact]
    public void Controller_AnonimoComLimitadorProprio()
    {
        var tipo = typeof(CtrPublicoController);

        Assert.Empty(tipo.GetCustomAttributes<AuthorizeAttribute>(false));
        Assert.Equal("api/contratacoes/publico", tipo.GetCustomAttribute<RouteAttribute>()!.Template);
        Assert.Equal("contratacoes-publico", tipo.GetCustomAttribute<EnableRateLimitingAttribute>()!.PolicyName);

        var acoes = tipo.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
        var painel = Assert.Single(acoes);
        Assert.NotNull(painel.GetCustomAttribute<AllowAnonymousAttribute>());
        Assert.Equal("painel", painel.GetCustomAttribute<HttpGetAttribute>()!.Template);
    }

    [Fact]
    public async Task Controller_DevolveOPainel()
    {
        Assinado("04044-00000001/2026-11", "SES", 10);

        var resultado = await new CtrPublicoController(_service).Painel();

        var painel = Assert.IsType<CtrPainelPublicoResponse>(Assert.IsType<OkObjectResult>(resultado).Value);
        Assert.Equal("SES", Assert.Single(painel.Contratacoes).OrgaoSigla);
    }
}
