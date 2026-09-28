using System.Xml.Linq;
using api.Planejamento;
using app.Models;
using Microsoft.EntityFrameworkCore;
using Models.Planejamento;
using service.Planejamento;
using Xunit;

namespace test.planejamento;

/// <summary>
/// O serviço dos fluxos (E6): os modelos (todo papel lê; só o administrador do módulo e o admin
/// geral mudam, com histórico), a cópia do órgão (só existe quando ele muda; igual ao modelo
/// volta a seguir o modelo; "restaurar"; o aviso de que o modelo mudou; só a equipe do órgão
/// grava, com o PDTIC aberto), os desenhos com os nomes do dicionário (padrão no modelo, do
/// órgão no PDTIC), a prévia do editor e o cronograma sugerido do plano de trabalho.
/// </summary>
public class PeFluxoServiceTest : PeFluxoTestBase
{
    private static string Desc(string svg)
    {
        XNamespace ns = "http://www.w3.org/2000/svg";
        return XDocument.Parse(svg).Root!.Element(ns + "desc")!.Value;
    }

    private async Task<long> PdticSesAsync() => (await AbrirSesAsync()).Id;

    /// <summary>A preparação com uma tarefa a mais no fim, feita pela equipe (adaptação do órgão).</summary>
    private PeFluxoDefinicao PreparacaoComTarefaExtra()
    {
        var definicao = DefinicaoDoModelo("preparacao");
        var d1 = definicao.Elementos.Single(e => e.Id == "d1");
        definicao.Elementos.Add(new PeFluxoElemento { Id = "extra", Tipo = "tarefa", RaiaId = "r1", Nome = "Publicar o plano de trabalho no portal" });
        var sim = definicao.Ligacoes.Single(l => l.De == d1.Id && l.Rotulo == "Sim");
        sim.Para = "extra";
        definicao.Ligacoes.Add(new PeFluxoLigacao { Id = "lextra", De = "extra", Para = "fim" });
        return definicao;
    }

    // ── Modelos ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task Modelos_TodoPapelLe_NaOrdem()
    {
        foreach (var user in new[] { UserPeAdmin, UserPeSgdi, UserPeCgtic, UserOrgaoSes, UserConsultaSes, UserAdminGeral })
        {
            var modelos = await Fluxos.ModelosAsync(await ContextoDe(user));
            Assert.Equal(10, modelos.Count);
            Assert.Equal("macroprocesso", modelos[0].Chave);
            Assert.Equal("Figura 6", modelos.Single(m => m.Chave == "preparacao").FiguraGuia);
            Assert.Equal("1.1", modelos.Single(m => m.Chave == "preparacao").Definicao.Elementos.Single(e => e.Id == "t1").Numero);
        }
        Assert.Equal((int)ErrorCode.PeSemPermissao, await ErroAsync(async () => await Fluxos.ModelosAsync(await ContextoDe(UserSemPapel))));
    }

    [Fact]
    public async Task SalvarModelo_SoOAdministrador_ComHistorico()
    {
        var definicao = DefinicaoDoModelo("preparacao");
        definicao.Elementos.Single(e => e.Id == "t3").Nome = "Descrever a metodologia adotada";

        foreach (var user in new[] { UserPeSgdi, UserPeCgtic, UserOrgaoSes })
            Assert.Equal((int)ErrorCode.PeSemPermissao, await ErroAsync(async () => await Fluxos.SalvarModeloAsync("preparacao", Corpo(definicao), await ContextoDe(user))));

        var salvo = await Fluxos.SalvarModeloAsync("preparacao", Corpo(definicao, "Preparação da elaboração"), await Admin());
        Assert.Equal("Preparação da elaboração", salvo.Nome);
        Assert.Equal("Descrever a metodologia adotada", salvo.Definicao.Elementos.Single(e => e.Id == "t3").Nome);
        Assert.Equal(UserPeAdmin.Email, salvo.AlteradoPor);
        var historico = Context.PeModeloHistorico.AsNoTracking().Single(h => h.Entidade == PeDominios.EntidadeHistorico.FluxoModelo);
        Assert.Equal(ModeloNoBanco("preparacao").Id, historico.EntidadeId);
        Assert.Equal(PeDominios.AcaoHistorico.Alteracao, historico.Acao);
        Assert.Contains("Descrever a metodologia de elaboração", historico.Antes);
        Assert.Contains("Descrever a metodologia adotada", historico.Depois);

        // O mesmo de novo não grava outra linha; o admin geral também muda
        await Fluxos.SalvarModeloAsync("preparacao", Corpo(definicao, "Preparação da elaboração"), await Admin());
        Assert.Equal(1, Context.PeModeloHistorico.Count(h => h.Entidade == PeDominios.EntidadeHistorico.FluxoModelo));
        await Fluxos.SalvarModeloAsync("preparacao", Corpo(definicao, ""), await ContextoDe(UserAdminGeral));
        Assert.Equal("Preparação da elaboração", ModeloNoBanco("preparacao").Nome);
    }

    [Fact]
    public async Task SalvarModelo_Invalido_400ComOsErros_ENaoExiste_404()
    {
        var definicao = DefinicaoDoModelo("preparacao");
        definicao.Ligacoes.RemoveAll(l => l.De == "d1" && l.Rotulo == "Não");
        var ex = await Assert.ThrowsAsync<PeFluxoInvalidoException>(async () => await Fluxos.SalvarModeloAsync("preparacao", Corpo(definicao), await Admin()));
        Assert.Contains("A decisão \"Plano de trabalho aprovado?\" precisa de pelo menos duas saídas (por exemplo, Sim e Não).", ex.Erros);

        Assert.Equal((int)ErrorCode.PeFluxoNaoEncontrado, await ErroAsync(async () =>
            await Fluxos.SalvarModeloAsync("nao_existe", Corpo(DefinicaoDoModelo("preparacao")), await Admin())));
        Assert.Equal((int)ErrorCode.PeFluxoNaoEncontrado, await ErroAsync(async () => await Fluxos.SvgDoModeloAsync("nao_existe", await Admin())));
    }

    [Fact]
    public async Task SvgDoModelo_ComOsNomesPadrao()
    {
        var svg = await Fluxos.SvgDoModeloAsync("planejamento", await ContextoDe(UserPeSgdi));
        var desc = Desc(svg);
        Assert.Contains("Raias: Autoridade Máxima, Comitê de Governança Digital, Equipe de Elaboração do PDTIC.", desc);
        Assert.Contains("3.10 Publicar o PDTIC (Autoridade Máxima)", desc);
        Assert.DoesNotContain("{nomes.", svg);
    }

    // ── Cópia do órgão ────────────────────────────────────────────────────────

    [Fact]
    public async Task SemCopia_VemOModelo_EPodeEditarSoAEquipe()
    {
        var id = await PdticSesAsync();

        var fluxo = await Fluxos.ObterAsync(id, "preparacao", await Orgao());
        Assert.False(fluxo.Personalizado);
        Assert.False(fluxo.ModeloMudou);
        Assert.True(fluxo.PodeEditar);
        Assert.Equal("Preparação", fluxo.Nome);
        Assert.Equal("Figura 6", fluxo.FiguraGuia);
        Assert.Equal(11, fluxo.Definicao.Elementos.Count);

        Assert.False((await Fluxos.ObterAsync(id, "preparacao", await ContextoDe(UserConsultaSes))).PodeEditar);
        Assert.False((await Fluxos.ObterAsync(id, "preparacao", await ContextoDe(UserPeSgdi))).PodeEditar);
        Assert.True((await Fluxos.ObterAsync(id, "preparacao", await ContextoDe(UserAdminGeral))).PodeEditar);
        // Outro órgão não vê
        Assert.Equal((int)ErrorCode.PeSemPermissao, await ErroAsync(async () => await Fluxos.ObterAsync(id, "preparacao", await ContextoDe(UserOrgaoSeec))));

        var lista = await Fluxos.DoPdticAsync(id, await Orgao());
        Assert.Equal(10, lista.Count);
        Assert.All(lista, f => Assert.False(f.Personalizado));
        Assert.All(lista, f => Assert.Null(f.AlteradoEm));
    }

    [Fact]
    public async Task Salvar_CriaACopia_IgualAoModeloVoltaAoModelo_ERestaurar()
    {
        var id = await PdticSesAsync();
        var ctx = await Orgao();

        var salvo = await Fluxos.SalvarAsync(id, "preparacao", Corpo(PreparacaoComTarefaExtra(), "Preparação do PDTIC da Saúde"), ctx);
        Assert.True(salvo.Personalizado);
        Assert.Equal("Preparação do PDTIC da Saúde", salvo.Nome);
        Assert.Equal("1.9", salvo.Definicao.Elementos.Single(e => e.Id == "extra").Numero);
        Assert.Equal(UserOrgaoSes.Email, salvo.AlteradoPor);
        Assert.Single(Context.PeFluxos.AsNoTracking());
        var lista = await Fluxos.DoPdticAsync(id, ctx);
        Assert.True(lista.Single(f => f.Chave == "preparacao").Personalizado);
        Assert.Equal("Preparação do PDTIC da Saúde", lista.Single(f => f.Chave == "preparacao").Nome);
        Assert.False(lista.Single(f => f.Chave == "diagnostico").Personalizado);
        // Gravar toca o PDTIC
        Assert.Equal(UserOrgaoSes.Email, Context.PePdtics.AsNoTracking().Single(p => p.Id == id).AlteradoPor);

        // Gravar igual ao modelo (nome e definição) apaga a cópia: volta a seguir o modelo
        var igual = await Fluxos.SalvarAsync(id, "preparacao", Corpo(DefinicaoDoModelo("preparacao"), ""), ctx);
        Assert.False(igual.Personalizado);
        Assert.Empty(Context.PeFluxos.AsNoTracking());

        await Fluxos.SalvarAsync(id, "preparacao", Corpo(PreparacaoComTarefaExtra()), ctx);
        Assert.Equal("Preparação", (await Fluxos.ObterAsync(id, "preparacao", ctx)).Nome);
        var restaurado = await Fluxos.RestaurarAsync(id, "preparacao", ctx);
        Assert.False(restaurado.Personalizado);
        Assert.Empty(Context.PeFluxos.AsNoTracking());
        // Restaurar sem cópia não quebra
        Assert.False((await Fluxos.RestaurarAsync(id, "preparacao", ctx)).Personalizado);
    }

    [Fact]
    public async Task ModeloMudou_AvisaAteOOrgaoGravarDeNovo()
    {
        var id = await PdticSesAsync();
        var ctx = await Orgao();
        await Fluxos.SalvarAsync(id, "preparacao", Corpo(PreparacaoComTarefaExtra()), ctx);
        Assert.False((await Fluxos.ObterAsync(id, "preparacao", ctx)).ModeloMudou);

        // O administrador muda o modelo: a cópia fica como está e ganha o aviso
        var novoModelo = DefinicaoDoModelo("preparacao");
        novoModelo.Elementos.Single(e => e.Id == "t4").Nome = "Reunir os documentos de referência";
        await Fluxos.SalvarModeloAsync("preparacao", Corpo(novoModelo), await Admin());
        var fluxo = await Fluxos.ObterAsync(id, "preparacao", ctx);
        Assert.True(fluxo.ModeloMudou);
        Assert.Equal("Consolidar os documentos de referência", fluxo.Definicao.Elementos.Single(e => e.Id == "t4").Nome);
        Assert.True((await Fluxos.DoPdticAsync(id, ctx)).Single(f => f.Chave == "preparacao").ModeloMudou);

        // O órgão grava a cópia de novo ("manter a minha"): o aviso some
        await Fluxos.SalvarAsync(id, "preparacao", Corpo(fluxo.Definicao), ctx);
        Assert.False((await Fluxos.ObterAsync(id, "preparacao", ctx)).ModeloMudou);

        // Sem cópia, o órgão vê o modelo novo na hora (decisão 14)
        var seec = await AbrirSeecAsync();
        var daSeec = await Fluxos.ObterAsync(seec.Id, "preparacao", await ContextoDe(UserOrgaoSeec));
        Assert.False(daSeec.Personalizado);
        Assert.Equal("Reunir os documentos de referência", daSeec.Definicao.Elementos.Single(e => e.Id == "t4").Nome);
    }

    [Fact]
    public async Task Salvar_SoAEquipeDoOrgao_ComOPdticAberto()
    {
        var id = await PdticSesAsync();
        var corpo = Corpo(PreparacaoComTarefaExtra());
        foreach (var user in new[] { UserConsultaSes, UserPeSgdi, UserPeAdmin, UserPeCgtic, UserOrgaoSeec })
        {
            Assert.Equal((int)ErrorCode.PeSemPermissao, await ErroAsync(async () => await Fluxos.SalvarAsync(id, "preparacao", corpo, await ContextoDe(user))));
            Assert.Equal((int)ErrorCode.PeSemPermissao, await ErroAsync(async () => await Fluxos.RestaurarAsync(id, "preparacao", await ContextoDe(user))));
        }
        Assert.True((await Fluxos.SalvarAsync(id, "preparacao", corpo, await ContextoDe(UserAdminGeral))).Personalizado);

        // PDTIC em aprovação: fechado
        var pdtic = Context.PePdtics.Single(p => p.Id == id);
        pdtic.Situacao = PeDominios.SituacaoPdtic.EmAprovacao;
        Context.SaveChanges();
        Context.ChangeTracker.Clear();
        Assert.Equal((int)ErrorCode.PePdticFechado, await ErroAsync(async () => await Fluxos.SalvarAsync(id, "preparacao", corpo, await Orgao())));
        Assert.Equal((int)ErrorCode.PePdticFechado, await ErroAsync(async () => await Fluxos.RestaurarAsync(id, "preparacao", await Orgao())));
        Assert.False((await Fluxos.ObterAsync(id, "preparacao", await Orgao())).PodeEditar);

        // Definição inválida: 400 com a lista
        Context.PePdtics.Single(p => p.Id == id).Situacao = PeDominios.SituacaoPdtic.Devolvido;
        Context.SaveChanges();
        Context.ChangeTracker.Clear();
        var ex = await Assert.ThrowsAsync<PeFluxoInvalidoException>(async () =>
            await Fluxos.SalvarAsync(id, "preparacao", new PeFluxoSalvarDTO { Definicao = ComoJson(new { Raias = 1 }) }, await Orgao()));
        Assert.Contains("Raias precisa ser uma lista.", ex.Erros);
        var longo = await Assert.ThrowsAsync<PeFluxoInvalidoException>(async () =>
            await Fluxos.SalvarAsync(id, "preparacao", Corpo(PreparacaoComTarefaExtra(), new string('n', 201)), await Orgao()));
        Assert.Contains("O nome do fluxo passa de 200 caracteres.", longo.Erros);
    }

    [Fact]
    public async Task Svg_ComOsNomesDoDicionarioDoOrgao()
    {
        var id = await PdticSesAsync();
        // Sem o dicionário preenchido: os nomes padrão
        Assert.Contains("Raias: Comitê de Governança Digital, Equipe de Elaboração do PDTIC.", Desc(await Fluxos.SvgAsync(id, "preparacao", await Orgao())));

        await PreencherNomesAsync(id);
        var desc = Desc(await Fluxos.SvgAsync(id, "planejamento", await ContextoDe(UserConsultaSes)));
        Assert.Contains("Raias: Secretária de Estado de Saúde, Subcomitê Gestor de TIC da Saúde, Equipe de Elaboração do PDTIC.", desc);
        // A cópia do órgão também usa o dicionário
        var definicao = PreparacaoComTarefaExtra();
        definicao.Elementos.Single(e => e.Id == "extra").Nome = "Enviar o plano ao {nomes.comite}";
        await Fluxos.SalvarAsync(id, "preparacao", Corpo(definicao), await Orgao());
        Assert.Contains("1.9 Enviar o plano ao Subcomitê Gestor de TIC da Saúde (Subcomitê Gestor de TIC da Saúde)",
            Desc(await Fluxos.SvgAsync(id, "preparacao", await ContextoDe(UserPeSgdi))));
    }

    // ── Prévia do editor e nomes ──────────────────────────────────────────────

    [Fact]
    public async Task Desenho_ConfereAntes_EUsaOsNomesDoPdtic()
    {
        var id = await PdticSesAsync();
        await PreencherNomesAsync(id);

        var ex = await Assert.ThrowsAsync<PeFluxoInvalidoException>(async () =>
            await Fluxos.DesenhoAsync(new PeFluxoDesenhoDTO { Definicao = ComoJson(new { Raias = Array.Empty<object>(), Elementos = Array.Empty<object>(), Ligacoes = Array.Empty<object>() }) }, await Orgao()));
        Assert.Contains("Inclua pelo menos uma raia (quem faz os passos).", ex.Erros);

        var padrao = await Fluxos.DesenhoAsync(new PeFluxoDesenhoDTO { Definicao = ComoJson(Simples()), Nome = "Teste" }, await ContextoDe(UserPeAdmin));
        Assert.Contains("Comitê de Governança Digital", Desc(padrao));
        var doOrgao = await Fluxos.DesenhoAsync(new PeFluxoDesenhoDTO { Definicao = ComoJson(Simples()), PdticId = id }, await Orgao());
        Assert.Contains("Subcomitê Gestor de TIC da Saúde", Desc(doOrgao));
        // PDTIC de outro órgão: 403
        Assert.Equal((int)ErrorCode.PeSemPermissao, await ErroAsync(async () =>
            await Fluxos.DesenhoAsync(new PeFluxoDesenhoDTO { Definicao = ComoJson(Simples()), PdticId = id }, await ContextoDe(UserOrgaoSeec))));
    }

    [Fact]
    public async Task Validacao_DevolveOsErrosOuADefinicaoNumerada()
    {
        var ctx = await Orgao();
        var boa = await Fluxos.ValidarAsync(new PeFluxoDesenhoDTO { Definicao = ComoJson(Simples("5")) }, ctx);
        Assert.True(boa.Valida);
        Assert.Empty(boa.Erros);
        Assert.Equal(new[] { "5.1", "5.2" }, boa.Definicao!.Elementos.Where(e => e.Numero != null).Select(e => e.Numero));

        var ruim = await Fluxos.ValidarAsync(new PeFluxoDesenhoDTO { Definicao = ComoJson(new { Raias = new[] { new { Id = "r1", Nome = "A", Ordem = 1 } }, Elementos = Array.Empty<object>(), Ligacoes = Array.Empty<object>() }) }, ctx);
        Assert.False(ruim.Valida);
        Assert.Null(ruim.Definicao);
        Assert.Contains("O fluxo precisa de um início.", ruim.Erros);
    }

    [Fact]
    public async Task Nomes_PadraoEDoOrgao()
    {
        var padrao = await Fluxos.NomesAsync(null, await ContextoDe(UserPeAdmin));
        var comite = padrao.Single(n => n.Marcador == "{nomes.comite}");
        Assert.Equal(("Comitê de Governança Digital", "Comitê de Governança Digital"), (comite.Nome, comite.Padrao));
        Assert.Equal("Autoridade Máxima", padrao.Single(n => n.Marcador == "{nomes.autoridade_cargo}").Nome);
        Assert.Contains(padrao, n => n.Marcador == "{nomes.equipe_acompanhamento}");

        var id = await PdticSesAsync();
        await PreencherNomesAsync(id);
        var doOrgao = await Fluxos.NomesAsync(id, await Orgao());
        Assert.Equal("Subcomitê Gestor de TIC da Saúde", doOrgao.Single(n => n.Marcador == "{nomes.comite}").Nome);
        Assert.Equal("Secretária de Estado de Saúde", doOrgao.Single(n => n.Marcador == "{nomes.autoridade_cargo}").Nome);
        Assert.Equal("Maria da Silva", doOrgao.Single(n => n.Marcador == "{nomes.autoridade}").Nome);
        Assert.Equal("SES", doOrgao.Single(n => n.Marcador == "{orgao.sigla}").Nome);
        Assert.Equal((int)ErrorCode.PeSemPermissao, await ErroAsync(async () => await Fluxos.NomesAsync(id, await ContextoDe(UserOrgaoSeec))));
    }

    // ── Cronograma sugerido ───────────────────────────────────────────────────

    [Fact]
    public async Task Cronograma_UmaLinhaPorTarefa_ComResponsavelEPredecessora()
    {
        await DefinirNivelDoOrgaoAsync(OrgaoSes, "avancado");
        var id = await PdticSesAsync();
        await PreencherNomesAsync(id);

        var linhas = await Fluxos.SugerirCronogramaAsync(id, await Orgao());

        Assert.Equal(8 + 14 + 10, linhas.Count);
        string Atividade(PeRegistroResponse r) => r.Rotulos["atividade"];
        Assert.Equal("1.1 Definir a abrangência e o período do PDTIC", Atividade(linhas[0]));
        Assert.Equal("3.10 Publicar o PDTIC", Atividade(linhas[^1]));
        Assert.Equal("Subcomitê Gestor de TIC da Saúde", linhas[0].Rotulos["responsavel"]);
        Assert.Equal("Equipe de Elaboração do PDTIC", linhas[2].Rotulos["responsavel"]);
        Assert.Equal("Secretária de Estado de Saúde", linhas[^1].Rotulos["responsavel"]);

        List<string> Antes(string numero)
        {
            var linha = linhas.Single(l => Atividade(l).StartsWith(numero + " "));
            return linha.Vinculos["predecessoras"].Select(v => linhas.Single(l => l.Id == v.RegistroId)).Select(l => Atividade(l).Split(' ')[0]).ToList();
        }
        Assert.Empty(Antes("1.1"));
        Assert.Equal(new[] { "1.1" }, Antes("1.2"));
        // A primeira tarefa do diagnóstico vem depois da última da preparação (como no Anexo IV do guia)
        Assert.Equal(new[] { "1.8" }, Antes("2.1"));
        Assert.Equal(new[] { "2.8" }, Antes("2.9"));
        Assert.Equal(new[] { "2.8" }, Antes("2.11"));
        Assert.Equal(new[] { "2.9", "2.10", "2.11" }, Antes("2.12"));
        Assert.Equal(new[] { "2.14" }, Antes("3.1"));
        Assert.Equal(new[] { "3.9" }, Antes("3.10"));

        // As datas ficam para o órgão: o passo do plano de trabalho fica pendente
        Assert.All(linhas, l => Assert.False(l.Dados.ContainsKey("inicio")));
        Assert.Equal(PeDominios.SituacaoPasso.Pendente, (await PassoAsync(id, "preparacao.plano-trabalho")).Situacao);

        // Só no cronograma vazio
        Assert.Equal((int)ErrorCode.PeCronogramaPreenchido, await ErroAsync(async () => await Fluxos.SugerirCronogramaAsync(id, await Orgao())));
    }

    [Fact]
    public async Task Cronograma_UsaACopiaDoOrgao_ESoAEquipeNoAvancado()
    {
        var idBasico = await PdticSesAsync();
        // No Básico o plano de trabalho não aparece
        Assert.Equal((int)ErrorCode.PeSecaoIndisponivel, await ErroAsync(async () => await Fluxos.SugerirCronogramaAsync(idBasico, await Orgao())));

        await DefinirNivelDoOrgaoAsync(OrgaoSes, "avancado");
        await Fluxos.SalvarAsync(idBasico, "preparacao", Corpo(PreparacaoComTarefaExtra()), await Orgao());
        foreach (var user in new[] { UserConsultaSes, UserPeSgdi, UserOrgaoSeec })
            Assert.Equal((int)ErrorCode.PeSemPermissao, await ErroAsync(async () => await Fluxos.SugerirCronogramaAsync(idBasico, await ContextoDe(user))));

        var linhas = await Fluxos.SugerirCronogramaAsync(idBasico, await Orgao());
        Assert.Equal(9 + 14 + 10, linhas.Count);
        var extra = linhas.Single(l => l.Rotulos["atividade"] == "1.9 Publicar o plano de trabalho no portal");
        // Agora a última da preparação é a tarefa nova: o diagnóstico começa depois dela
        var primeiraDoDiagnostico = linhas.Single(l => l.Rotulos["atividade"].StartsWith("2.1 "));
        Assert.Equal(new[] { extra.Id }, primeiraDoDiagnostico.Vinculos["predecessoras"].Select(v => v.RegistroId));
    }
}
