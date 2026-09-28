using System.Text.Json;
using api.Planejamento;
using Microsoft.EntityFrameworkCore;
using Models;
using Models.Planejamento;
using service.Planejamento;
using Xunit;

namespace test.planejamento;

/// <summary>
/// F3 (retorno do dono do produto), parte C1: o modo dos níveis. O carregador versão 8 grava
/// "livre" quando a configuração não existe (e só então; a escolha que já está fica), o GET modelo
/// traz o modo, o PUT modelo/modo-niveis troca e grava no histórico do modelo (entidade
/// configuracao) e, antes da versão 8 (o intervalo do deploy), vale o definido e o PUT responde 409.
/// </summary>
public class PeF3ModoNiveisTest : PeModeloTestBase
{
    [Fact]
    public async Task Carregador_Versao8_GravaLivre_QuandoAChaveNaoExiste_EGuardaAEscolhaQueJaExiste()
    {
        using var vazio = new PeBancoVazio();
        var resultado = await new PeCarregadorModelo(vazio.Context).CarregarAsync();
        Assert.Equal(8, resultado.Versao);
        Assert.Equal(8, PeCarregadorModelo.VersaoDoModoLivre);
        Assert.Equal("\"livre\"", vazio.Context.PeConfiguracoes.AsNoTracking().Single(c => c.Chave == PeConfiguracao.ChaveModoNiveis).Valor);
        Assert.True((await PeModoNiveis.LerAsync(vazio.Context)).Livre);

        // Um banco que já tem o módulo, na versão 7 com o modo escolhido "definido": a carga não troca
        using var outro = new PeBancoVazio();
        var v7 = PeCarregadorModelo.LerSeed();
        v7.Versao = 7;
        v7.Configuracoes.Remove(PeConfiguracao.ChaveModoNiveis);
        await new PeCarregadorModelo(outro.Context).CarregarAsync(v7);
        Assert.False(outro.Context.PeConfiguracoes.Any(c => c.Chave == PeConfiguracao.ChaveModoNiveis));
        Assert.Equal(PeModoNiveis.Anterior, await PeModoNiveis.LerAsync(outro.Context));
        outro.Context.PeConfiguracoes.Add(new PeConfiguracao
        {
            Chave = PeConfiguracao.ChaveModoNiveis,
            Valor = "\"definido\"",
            CriadoEm = DateTime.UtcNow,
            CriadoPor = "teste"
        });
        await outro.Context.SaveChangesAsync();

        var carga = await new PeCarregadorModelo(outro.Context).CarregarAsync();
        Assert.Equal((7, 8, 0), (carga.VersaoAnterior, carga.Versao, carga.Configuracoes));
        Assert.Equal("\"definido\"", outro.Context.PeConfiguracoes.AsNoTracking().Single(c => c.Chave == PeConfiguracao.ChaveModoNiveis).Valor);
        var modo = await PeModoNiveis.LerAsync(outro.Context);
        Assert.True(modo.Ativo);
        Assert.False(modo.Livre);
    }

    [Fact]
    public async Task Carregador_DaVersao7Para8_SoGravaAConfiguracao()
    {
        using var vazio = new PeBancoVazio();
        var v7 = PeCarregadorModelo.LerSeed();
        v7.Versao = 7;
        v7.Configuracoes.Remove(PeConfiguracao.ChaveModoNiveis);
        await new PeCarregadorModelo(vazio.Context).CarregarAsync(v7);

        var resultado = await new PeCarregadorModelo(vazio.Context).CarregarAsync();
        Assert.Equal((7, 8, 1), (resultado.VersaoAnterior, resultado.Versao, resultado.Configuracoes));
        Assert.Equal(0, resultado.Niveis + resultado.Etapas + resultado.Passos + resultado.Secoes + resultado.Campos + resultado.Opcoes
                        + resultado.Registros + resultado.Documentos + resultado.Capitulos + resultado.Blocos + resultado.Fluxos
                        + resultado.SecoesPorCiclo + resultado.Correcoes);
        Assert.False((await new PeCarregadorModelo(vazio.Context).CarregarAsync()).Executou);
    }

    [Fact]
    public async Task Modelo_TrazOModo_EOPutTroca_ComHistorico()
    {
        Assert.Equal(PeDominios.ModoNiveis.Definido, (await Modelo.ObterModeloAsync(false)).ModoNiveis);

        var livre = await Modelo.DefinirModoNiveisAsync(new PeModoNiveisDTO { Modo = " Livre " }, EmailAdmin);
        Assert.Equal(PeDominios.ModoNiveis.Livre, livre.ModoNiveis);
        Assert.Equal(PeDominios.ModoNiveis.Livre, (await Modelo.ObterModeloAsync(false)).ModoNiveis);
        Assert.Equal("\"livre\"", Context.PeConfiguracoes.AsNoTracking().Single(c => c.Chave == PeConfiguracao.ChaveModoNiveis).Valor);

        var historico = Assert.Single(HistoricoDe(PeDominios.EntidadeHistorico.Configuracao, 0));
        Assert.Equal(PeDominios.AcaoHistorico.Alteracao, historico.Acao);
        Assert.Equal(EmailAdmin, historico.AlteradoPor);
        Assert.Equal("definido", JsonDocument.Parse(historico.Antes!).RootElement.GetProperty("Valor").GetString());
        Assert.Equal("livre", JsonDocument.Parse(historico.Depois!).RootElement.GetProperty("Valor").GetString());
        Assert.Equal(PeConfiguracao.ChaveModoNiveis, JsonDocument.Parse(historico.Depois!).RootElement.GetProperty("Chave").GetString());

        // O mesmo modo de novo não grava nada
        await Modelo.DefinirModoNiveisAsync(new PeModoNiveisDTO { Modo = "livre" }, EmailAdmin);
        Assert.Single(HistoricoDe(PeDominios.EntidadeHistorico.Configuracao, 0));

        // Fora do domínio: 400
        Assert.Equal(Codigo(ErrorCode.PeDadosInvalidos), await ErroAsync(() => Modelo.DefinirModoNiveisAsync(new PeModoNiveisDTO { Modo = "misto" }, EmailAdmin)));
        Assert.Equal(Codigo(ErrorCode.PeDadosInvalidos), await ErroAsync(() => Modelo.DefinirModoNiveisAsync(new PeModoNiveisDTO(), EmailAdmin)));

        // O histórico do modelo aceita o filtro da entidade nova
        var pagina = await Modelo.HistoricoAsync(new PeHistoricoConsulta { Entidade = PeDominios.EntidadeHistorico.Configuracao });
        Assert.Single(pagina.Items);
    }

    [Fact]
    public async Task Modo_SemAConfiguracao_EDefinido_EComValorForaDoDominio_Tambem()
    {
        var linha = Context.PeConfiguracoes.Single(c => c.Chave == PeConfiguracao.ChaveModoNiveis);
        Context.PeConfiguracoes.Remove(linha);
        await Context.SaveChangesAsync();
        Assert.Equal((true, PeDominios.ModoNiveis.Definido), await ModoAsync());

        DefinirModoNiveis("qualquer");
        Assert.Equal((true, PeDominios.ModoNiveis.Definido), await ModoAsync());
        Assert.Equal(PeDominios.ModoNiveis.Definido, (await Modelo.ObterModeloAsync(false)).ModoNiveis);

        // O PUT grava a chave que faltava
        DefinirModoNiveis(PeDominios.ModoNiveis.Livre);
        Assert.Equal((true, PeDominios.ModoNiveis.Livre), await ModoAsync());
    }

    [Fact]
    public async Task AntesDaVersao8_ValeODefinido_EOPutResponde409()
    {
        DefinirModoNiveis(PeDominios.ModoNiveis.Livre);
        VersaoGravada(7);

        var modo = await PeModoNiveis.LerAsync(Context);
        Assert.False(modo.Ativo);
        Assert.False(modo.Livre);
        Assert.Equal(PeDominios.ModoNiveis.Definido, (await Modelo.ObterModeloAsync(false)).ModoNiveis);
        var trilha = await TrilhaAsync(OrgaoSes);
        Assert.Equal(PeDominios.ModoNiveis.Definido, trilha.ModoNiveis);
        Assert.All(PassosDa(trilha), p => Assert.Null(p.Detalhe));
        Assert.Equal(23, PassosDa(trilha).Count);

        Assert.Equal(Codigo(ErrorCode.PeModeloIndisponivel),
            await ErroAsync(() => Modelo.DefinirModoNiveisAsync(new PeModoNiveisDTO { Modo = "definido" }, EmailAdmin)));
    }

    [Fact]
    public async Task Controller_SoOAdministradorEOAdminGeralTrocam()
    {
        foreach (var user in new[] { UserPeSgdi, UserPeCgtic, UserOrgaoSes, UserConsultaSes })
        {
            var (status, _) = Resultado(await ControladorModelo(user).DefinirModoNiveis(Corpo(new { Modo = "livre" })));
            Assert.Equal(403, status);
        }
        var (ok, corpo) = Resultado(await ControladorModelo(UserPeAdmin).DefinirModoNiveis(Corpo(new { Modo = "livre" })));
        Assert.Equal(200, ok);
        Assert.Equal("livre", Assert.IsType<PeModoNiveisResponse>(corpo).ModoNiveis);

        RetratoAdmin(UserAdminGeral);
        var (okAdmin, corpoAdmin) = Resultado(await ControladorModelo(UserAdminGeral).DefinirModoNiveis(Corpo(new { Modo = "definido" })));
        Assert.Equal(200, okAdmin);
        Assert.Equal("definido", Assert.IsType<PeModoNiveisResponse>(corpoAdmin).ModoNiveis);

        var (invalido, erro) = Resultado(await ControladorModelo(UserPeAdmin).DefinirModoNiveis(Corpo(new { Modo = 3 })));
        Assert.Equal(400, invalido);
        Assert.Equal((int)ErrorCode.PeDadosInvalidos, JsonSerializer.SerializeToElement(erro).GetProperty("Code").GetInt32());
    }

    private async Task<(bool, string)> ModoAsync()
    {
        var modo = await PeModoNiveis.LerAsync(Context);
        return (modo.Ativo, modo.Vigente);
    }

    private void VersaoGravada(int versao)
    {
        var linha = Context.PeConfiguracoes.Single(c => c.Chave == PeConfiguracao.ChaveVersaoModelo);
        linha.Valor = versao.ToString(System.Globalization.CultureInfo.InvariantCulture);
        Context.SaveChanges();
    }
}
