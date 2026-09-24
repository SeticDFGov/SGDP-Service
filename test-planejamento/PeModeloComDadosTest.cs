using System.Text.Json;
using api.Planejamento;
using Models.Planejamento;
using service.Planejamento;
using Xunit;

namespace test.planejamento;

/// <summary>
/// Regras do editor do modelo (E2) que passam a olhar os registros (E3): campo com valor
/// gravado não muda de chave nem de tipo (nem de alvo, se é ligação); seção com registros
/// não muda de chave nem de tipo; opção guardada num registro só é desativada.
/// </summary>
public class PeModeloComDadosTest : PeReferenciaisTestBase
{
    private static HashSet<string> Inf(params string[] nomes) => new(nomes);

    [Fact]
    public async Task CampoComValorGravado_NaoMudaDeChaveNemDeTipo_SemValorMuda()
    {
        var secao = await SecaoDfAsync("Regras");
        await CampoAsync(secao.Id, "nome", "texto_curto", null, "obrigatorio");
        var nota = await CampoAsync(secao.Id, "nota", "texto_curto");
        var ctx = await Admin();

        // Registro sem valor no campo: a chave ainda muda
        await Registros.CriarAsync(PeDono.Df, secao.Chave, Salvar(new { nome = "Um" }), ctx);
        var renomeado = await Modelo.AtualizarCampoAsync(nota.Id, new PeCampoAtualizarDTO { Chave = "observacao", Informados = Inf("Chave") }, EmailAdmin);
        Assert.Equal("observacao", renomeado.Chave);

        await Registros.CriarAsync(PeDono.Df, secao.Chave, Salvar(new { nome = "Dois", observacao = "Texto" }), ctx);
        var ex = await Assert.ThrowsAsync<service.ApiException>(() => Modelo.AtualizarCampoAsync(nota.Id,
            new PeCampoAtualizarDTO { Chave = "outra", Informados = Inf("Chave") }, EmailAdmin));
        Assert.Equal((int)ErrorCode.PeItemEmUso, ex.Error.Code);
        Assert.Contains("1 registro", ex.Error.Message);
        Assert.Equal(Codigo(ErrorCode.PeItemEmUso), await ErroAsync(() => Modelo.AtualizarCampoAsync(nota.Id,
            new PeCampoAtualizarDTO { Tipo = "texto_longo", Informados = Inf("Tipo") }, EmailAdmin)));

        // O resto do campo continua editável
        var rotulo = await Modelo.AtualizarCampoAsync(nota.Id, new PeCampoAtualizarDTO { Rotulo = "Observação", Informados = Inf("Rotulo") }, EmailAdmin);
        Assert.Equal("Observação", rotulo.Rotulo);
        Assert.Equal("observacao", Campo(secao.Chave, "observacao").Chave);
    }

    [Fact]
    public async Task SecaoComRegistros_NaoMudaDeChaveNemDeTipo()
    {
        var secao = await SecaoDfAsync("Anotações");
        await CampoAsync(secao.Id, "nome", "texto_curto", null, "obrigatorio");

        var semRegistro = await Modelo.AtualizarSecaoAsync(secao.Id, new PeSecaoAtualizarDTO { Chave = "anotacoes_df", Informados = Inf("Chave") }, EmailAdmin);
        Assert.Equal("anotacoes_df", semRegistro.Chave);

        await Registros.CriarAsync(PeDono.Df, "anotacoes_df", Salvar(new { nome = "Um" }), await Admin());
        var ex = await Assert.ThrowsAsync<service.ApiException>(() => Modelo.AtualizarSecaoAsync(secao.Id,
            new PeSecaoAtualizarDTO { Tipo = "formulario", Informados = Inf("Tipo") }, EmailAdmin));
        Assert.Equal((int)ErrorCode.PeItemEmUso, ex.Error.Code);
        Assert.Contains("1 registro gravado", ex.Error.Message);
        Assert.Equal(Codigo(ErrorCode.PeItemEmUso), await ErroAsync(() => Modelo.AtualizarSecaoAsync(secao.Id,
            new PeSecaoAtualizarDTO { Chave = "outra", Informados = Inf("Chave") }, EmailAdmin)));
        Assert.Equal("Anotações do DF", (await Modelo.AtualizarSecaoAsync(secao.Id,
            new PeSecaoAtualizarDTO { Titulo = "Anotações do DF", Informados = Inf("Titulo") }, EmailAdmin)).Titulo);
    }

    [Fact]
    public async Task LigacaoComLigacoesGravadas_NaoTrocaOAlvo_MasMudaOResto()
    {
        var secao = await SecaoDfAsync("Referências");
        await CampoAsync(secao.Id, "nome", "texto_curto", null, "obrigatorio");
        var ligacao = await CampoAsync(secao.Id, "principios", "ligacao_catalogo", new { catalogo = "principio", multipla = true });
        var principio = (await Registros.CatalogoAsync("principio", await Admin()))[0];
        await Registros.CriarAsync(PeDono.Df, secao.Chave, Salvar(new { nome = "Um" }, new { principios = new[] { principio.Id } }), await Admin());

        Assert.Equal(Codigo(ErrorCode.PeItemEmUso), await ErroAsync(() => Modelo.AtualizarCampoAsync(ligacao.Id,
            new PeCampoAtualizarDTO { Config = JsonSerializer.SerializeToElement(new { catalogo = "petic_eixo", multipla = true }), Informados = Inf("Config") },
            EmailAdmin)));
        Assert.Equal(Codigo(ErrorCode.PeItemEmUso), await ErroAsync(() => Modelo.AtualizarCampoAsync(ligacao.Id,
            new PeCampoAtualizarDTO { Tipo = "texto_curto", Config = JsonSerializer.SerializeToElement(new { }), Informados = Inf("Tipo", "Config") },
            EmailAdmin)));

        var umaSo = await Modelo.AtualizarCampoAsync(ligacao.Id, new PeCampoAtualizarDTO
        {
            Config = JsonSerializer.SerializeToElement(new { catalogo = "principio", multipla = false }),
            Informados = Inf("Config")
        }, EmailAdmin);
        Assert.False(umaSo.Config.GetProperty("multipla").GetBoolean());
    }

    [Fact]
    public async Task OpcaoGuardadaNumaListaMultipla_SoEDesativada()
    {
        var secao = await SecaoDfAsync("Temas");
        await CampoAsync(secao.Id, "nome", "texto_curto", null, "obrigatorio");
        var temas = await CampoAsync(secao.Id, "temas", "lista_multipla");
        await OpcoesAsync(temas.Id, "dados", "seguranca", "nuvem");
        await Registros.CriarAsync(PeDono.Df, secao.Chave, Salvar(new { nome = "Um", temas = new[] { "seguranca" } }), await Admin());

        var desativada = await Modelo.ExcluirOpcaoAsync(Opcao(secao.Chave, "temas", "seguranca").Id, EmailAdmin);
        Assert.False(desativada!.Ativa);
        Assert.Null(await Modelo.ExcluirOpcaoAsync(Opcao(secao.Chave, "temas", "nuvem").Id, EmailAdmin));
        Assert.Equal(new[] { "dados", "seguranca" }, Context.PeOpcoes.Where(o => o.CampoId == temas.Id).OrderBy(o => o.Ordem).Select(o => o.Valor));
    }
}
