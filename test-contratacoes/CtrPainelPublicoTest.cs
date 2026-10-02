using System.Reflection;
using System.Text.Json;
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
/// Painel público (GET api/contratacoes/publico/painel): todos os processos ativos de supervisão
/// contínua (em análise e assinados; as comunicações do TCDF ficam de fora), só os campos abertos
/// (o valor estimado não sai, pedido de 2026-10-02), a ordem pela chegada à SGDI, a data da última
/// alteração e o controller anônimo com o limitador próprio.
/// </summary>
public class CtrPainelPublicoTest : CtrTestBase
{
    private readonly CtrProcessoService _service;

    public CtrPainelPublicoTest()
    {
        _service = NovoProcessoService();
    }

    private CtrProcesso Chegou(string numero, string sigla, int diasDesdeAChegada, Action<CtrProcesso>? ajustar = null) =>
        SemearProcesso(numero, p =>
        {
            p.OrgaoSigla = sigla;
            p.OrgaoNome = "Órgão " + sigla;
            p.ChegadaSgdi = DiasAtras(diasDesdeAChegada);
            p.ValorEstimado = 1000m * diasDesdeAChegada;
            ajustar?.Invoke(p);
        });

    [Fact]
    public async Task Painel_TodosOsProcessosAtivosDaSupervisaoContinua_EmAnaliseEAssinados()
    {
        Chegou("04044-00000001/2026-11", "SES", 10);                                                  // em análise
        Chegou("04044-00000002/2026-12", "SEEDF", 20, p => p.DataAssinaturaContrato = DiasAtras(5));  // assinado
        Chegou("04044-00000003/2026-13", "PCDF", 15, p => p.Origem = CtrDominios.Origem.Tcdf);       // comunicação do TCDF
        Chegou("04044-00000004/2026-14", "SEEC", 25, p => p.Ativo = false);                           // excluído
        Chegou("04044-00000005/2026-15", "DETRAN-DF", 30, p => p.Restituido = true);                  // restituído

        var painel = await _service.MontarPainelPublicoAsync();

        Assert.Equal(new[] { "SES", "SEEDF", "DETRAN-DF" }, painel.Contratacoes.Select(c => c.OrgaoSigla));
        Assert.Null(painel.Contratacoes[0].DataAssinaturaContrato);
        Assert.Equal(DiasAtras(5), painel.Contratacoes[1].DataAssinaturaContrato);
    }

    [Fact]
    public async Task Painel_DevolveOsCamposAbertos_DaChegadaMaisRecenteParaAMaisAntiga()
    {
        Chegou("04044-00000001/2026-11", "SEEC", 40);
        Chegou("04044-00000002/2026-12", "SES", 5, p =>
        {
            p.OrgaoNome = "Secretaria de Estado de Saúde";
            p.Objeto = "Fábrica de software para os sistemas da regulação";
            p.CategoriaObjeto = CtrDominios.CategoriaObjeto.DesenvolvimentoSoftware;
            p.DataAssinaturaContrato = DiasAtras(1);
        });
        // Mesma chegada: desempata pela sigla; sem a data da chegada, vai para o fim
        Chegou("04044-00000003/2026-13", "CAESB", 40);
        Chegou("04044-00000004/2026-14", "ADASA", 0, p => p.ChegadaSgdi = null);

        var painel = await _service.MontarPainelPublicoAsync();

        Assert.Equal(new[] { "SES", "CAESB", "SEEC", "ADASA" }, painel.Contratacoes.Select(c => c.OrgaoSigla));
        var ses = painel.Contratacoes[0];
        Assert.Equal("Secretaria de Estado de Saúde", ses.OrgaoNome);
        Assert.Equal("Fábrica de software para os sistemas da regulação", ses.Objeto);
        Assert.Equal(CtrDominios.CategoriaObjeto.DesenvolvimentoSoftware, ses.CategoriaObjeto);
        Assert.Equal(DiasAtras(5), ses.ChegadaSgdi);
        Assert.Equal(DiasAtras(1), ses.DataAssinaturaContrato);
        Assert.Null(painel.Contratacoes[3].ChegadaSgdi);
    }

    [Fact]
    public async Task Painel_NaoPublicaOValorEstimado_MesmoGravado()
    {
        Chegou("04044-00000001/2026-11", "SES", 10, p => p.ValorEstimado = 14_850_000.50m);

        var json = JsonSerializer.Serialize(await _service.MontarPainelPublicoAsync());

        Assert.DoesNotContain("Valor", json);
        Assert.DoesNotContain("14850000", json);
    }

    [Fact]
    public async Task Painel_AtualizadoEm_EAUltimaAlteracaoEntreOsPublicados()
    {
        var criado = new DateTime(2026, 9, 1, 12, 0, 0, DateTimeKind.Utc);
        var alterado = new DateTime(2026, 9, 20, 15, 30, 0, DateTimeKind.Utc);
        Chegou("04044-00000001/2026-11", "SES", 10, p => p.CriadoEm = criado);
        Chegou("04044-00000002/2026-12", "SEEC", 12, p => { p.CriadoEm = criado; p.AlteradoEm = alterado; });
        // Alteração mais nova, mas de uma comunicação do TCDF, que não aparece no painel
        Chegou("04044-00000003/2026-13", "PCDF", 3, p =>
        {
            p.Origem = CtrDominios.Origem.Tcdf;
            p.CriadoEm = criado;
            p.AlteradoEm = alterado.AddDays(5);
        });

        var painel = await _service.MontarPainelPublicoAsync();

        Assert.Equal(alterado, painel.AtualizadoEm);
    }

    [Fact]
    public async Task Painel_SemProcessoDeSupervisaoContinua_ListaVaziaESemData()
    {
        Chegou("04044-00000001/2026-11", "PCDF", 10, p => p.Origem = CtrDominios.Origem.Tcdf);
        Chegou("04044-00000002/2026-12", "SES", 10, p => p.Ativo = false);

        var painel = await _service.MontarPainelPublicoAsync();

        Assert.Empty(painel.Contratacoes);
        Assert.Null(painel.AtualizadoEm);
    }

    [Fact]
    public void Contrato_SoOsCamposAbertos()
    {
        string[] Propriedades(Type tipo) => tipo.GetProperties().Select(p => p.Name).OrderBy(n => n).ToArray();

        Assert.Equal(
            new[] { "CategoriaObjeto", "ChegadaSgdi", "DataAssinaturaContrato", "Objeto", "OrgaoNome", "OrgaoSigla" },
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
        Chegou("04044-00000001/2026-11", "SES", 10);

        var resultado = await new CtrPublicoController(_service).Painel();

        var painel = Assert.IsType<CtrPainelPublicoResponse>(Assert.IsType<OkObjectResult>(resultado).Value);
        Assert.Equal("SES", Assert.Single(painel.Contratacoes).OrgaoSigla);
    }
}
