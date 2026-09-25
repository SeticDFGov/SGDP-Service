using api.Planejamento;
using Microsoft.EntityFrameworkCore;
using Models.Planejamento;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using service.Planejamento;
using Xunit;

namespace test.planejamento;

/// <summary>
/// F3, parte C5: o nível que o PDTIC alcançou, pela régua dos níveis (a trilha de cada nível ativo
/// sem os ajustes do órgão), nos dois modos. Contam os passos das etapas 1 a 3 da elaboração dos
/// tipos dados, conferência dos temas e aprovação; o PDTIC atende a um nível quando todo passo que
/// conta e é obrigatório nele está feito; o alcançado é o mais alto atendido com os de antes; o
/// próximo vem com o que falta (e, no modo livre, a forma a usar). O PDTIC registrado fora do
/// sistema não é calculado.
/// </summary>
public class PeF3NivelAlcancadoTest : PePaineisTestBase
{
    private void Livre() => DefinirModoNiveis(PeDominios.ModoNiveis.Livre);

    private async Task<PeNivelDoPdticResponse> NivelAsync(long pdticId) => (await SituacaoAsync(pdticId)).Nivel;

    [Fact]
    public async Task PdticNovo_SemNivelAlcancado_OProximoEOBasico_ComOQueFalta()
    {
        var pdtic = await AbrirSesAsync();
        var nivel = await NivelAsync(pdtic.Id);

        Assert.Equal(PeDominios.ModoNiveis.Definido, nivel.Modo);
        Assert.Null(nivel.AlcancadoId);
        Assert.Null(nivel.Motivo);
        Assert.Equal((NivelId("basico"), "Básico"), (nivel.ProximoId, nivel.ProximoNome));
        // Os princípios do art. 4º já vêm no PDTIC novo: o passo 1.8 não falta
        Assert.DoesNotContain(nivel.Faltam, f => f.PassoId == Passo("preparacao.principios").Id);
        var abrangencia = Assert.Single(nivel.Faltam, f => f.PassoId == Passo("preparacao.abrangencia").Id);
        Assert.Equal(("1.1", Passo("preparacao.abrangencia").Titulo), (abrangencia.PassoNumero, abrangencia.PassoTitulo));
        Assert.Equal("Preencha \"Abrangência e vigência\".", abrangencia.Motivo);
        // Só contam os passos de dados, da conferência dos temas e de aprovação das etapas 1 a 3
        var contam = nivel.Faltam.Select(f => Context.PePassos.AsNoTracking().Single(p => p.Id == f.PassoId)).ToList();
        Assert.All(contam, p => Assert.Contains(p.Tipo, new[] { "dados", "conferencia_temas", "aprovacao" }));
        Assert.DoesNotContain(contam, p => p.Tipo == "documento");
    }

    [Fact]
    public async Task OMinimoDoBasico_AlcancaOBasico_EOProximoEOIntermediario()
    {
        var pdtic = await ProntoParaEnviarAsync();
        var nivel = await NivelAsync(pdtic.Id);

        Assert.Equal((NivelId("basico"), "Básico"), (nivel.AlcancadoId, nivel.AlcancadoNome));
        Assert.Equal((NivelId("intermediario"), "Intermediário"), (nivel.ProximoId, nivel.ProximoNome));
        // O passo que o órgão não vê no Básico (a SWOT) vem sem número
        var swot = Assert.Single(nivel.Faltam, f => f.PassoId == Passo("diagnostico.swot").Id);
        Assert.Null(swot.PassoNumero);
        // No modo definido, nada de "use a forma"
        Assert.DoesNotContain(nivel.Faltam, f => f.Motivo.Contains("Use a forma"));
        // O passo feito no Básico que o Intermediário pede com mais campos (os critérios GUT)
        var necessidades = Assert.Single(nivel.Faltam, f => f.PassoId == Passo("diagnostico.necessidades-tic").Id);
        Assert.Contains("\"Gravidade\"", necessidades.Motivo);
    }

    [Fact]
    public async Task Livre_OQueFaltaDizAFormaAUsar_EOsDadosDaFormaEscondidaContam()
    {
        Livre();
        var pdtic = await ProntoParaEnviarAsync();
        var nivel = await NivelAsync(pdtic.Id);
        Assert.Equal(PeDominios.ModoNiveis.Livre, nivel.Modo);
        Assert.Equal("Básico", nivel.AlcancadoNome);

        var necessidades = Assert.Single(nivel.Faltam, f => f.PassoId == Passo("diagnostico.necessidades-tic").Id);
        Assert.EndsWith(" Use a forma do nível Intermediário neste passo.", necessidades.Motivo);
        Assert.NotNull(necessidades.PassoNumero);
        // O passo que não está no Básico já está na forma do Intermediário: sem a frase
        var swot = Assert.Single(nivel.Faltam, f => f.PassoId == Passo("diagnostico.swot").Id);
        Assert.DoesNotContain("Use a forma", swot.Motivo);

        // Os campos do Intermediário preenchidos com a forma dele e o passo de volta ao Básico: contam
        await Orgaos.DefinirDetalheAsync(OrgaoSes.Id, Passo("diagnostico.necessidades-tic").Id,
            new PePassoDetalheDTO { NivelId = NivelId("intermediario") }, EmailAdmin);
        var registro = (await Registros.ListarAsync(PeDono.DoPdtic(pdtic.Id), "necessidades", await Orgao())).Registros.Single();
        await Registros.AtualizarAsync(PeDono.DoPdtic(pdtic.Id), "necessidades", registro.Id, Salvar(new
        {
            descricao = "Substituir o sistema de regulação.", tipo = "servico", origem = "swot", areas = "Regulação", priorizada = true,
            gravidade = 5, urgencia = 5, tendencia = 4
        }), await Orgao());
        await Orgaos.DefinirDetalheAsync(OrgaoSes.Id, Passo("diagnostico.necessidades-tic").Id, new PePassoDetalheDTO(), EmailAdmin);

        nivel = await NivelAsync(pdtic.Id);
        Assert.DoesNotContain(nivel.Faltam, f => f.PassoId == Passo("diagnostico.necessidades-tic").Id);
    }

    [Fact]
    public async Task ARegua_NaoUsaOsAjustesDoOrgao_EOComentarioAbertoPrende()
    {
        var pdtic = await ProntoParaEnviarAsync();
        Assert.Equal("Básico", (await NivelAsync(pdtic.Id)).AlcancadoNome);

        // Um passo do Básico opcional para o órgão (ajuste) continua contando na régua
        await AjustarPassosAsync(OrgaoSes, ("preparacao.equipe", "opcional"));
        var equipe = (await Registros.ListarAsync(PeDono.DoPdtic(pdtic.Id), "equipe_elaboracao", await Orgao())).Registros.Single();
        await Registros.ExcluirAsync(PeDono.DoPdtic(pdtic.Id), "equipe_elaboracao", equipe.Id, await Orgao());
        var nivel = await NivelAsync(pdtic.Id);
        Assert.Null(nivel.AlcancadoId);
        Assert.Contains(nivel.Faltam, f => f.PassoId == Passo("preparacao.equipe").Id);
        await AjustarPassosAsync(OrgaoSes);
        await IncluirNoPdticAsync(pdtic.Id, "equipe_elaboracao", new { nome = "Ana Souza" });

        // O comentário aberto num passo que conta: o passo não está feito (a mesma análise da situação)
        var comentario = await Comentarios.CriarAsync(pdtic.Id,
            new PeComentarioCriarDTO { PassoId = Passo("diagnostico.ativos").Id, Texto = "Faltou o sistema de RH." }, await Sgdi());
        nivel = await NivelAsync(pdtic.Id);
        Assert.Null(nivel.AlcancadoId);
        var ativos = Assert.Single(nivel.Faltam, f => f.PassoId == Passo("diagnostico.ativos").Id);
        Assert.Equal(PePdticService.FaltaComentarioAberto, ativos.Motivo);

        await Comentarios.ResolverAsync(comentario.Id, await Orgao());
        Assert.Equal("Básico", (await NivelAsync(pdtic.Id)).AlcancadoNome);
    }

    [Fact]
    public async Task OUltimoNivel_SemProximo_EListaVazia()
    {
        // Só o Básico ativo: quem completa o mínimo alcança o último nível
        await Modelo.AtualizarNivelAsync(NivelId("intermediario"), new PeNivelAtualizarDTO { Ativo = false, Informados = new HashSet<string> { "Ativo" } }, EmailAdmin);
        await Modelo.AtualizarNivelAsync(NivelId("avancado"), new PeNivelAtualizarDTO { Ativo = false, Informados = new HashSet<string> { "Ativo" } }, EmailAdmin);
        var pdtic = await ProntoParaEnviarAsync();

        var nivel = await NivelAsync(pdtic.Id);
        Assert.Equal("Básico", nivel.AlcancadoNome);
        Assert.Null(nivel.ProximoId);
        Assert.Empty(nivel.Faltam);
    }

    [Fact]
    public async Task RegistradoForaDoSistema_NaoECalculado()
    {
        var arquivo = await EnviarArquivoAsync(UserOrgaoSes, "pdtic.pdf", PdfDeUmaPagina());
        var pdtic = await Aprovacao.RegistrarExternoAsync(new PeRegistroExternoDTO
        {
            Versao = "1.0",
            VigenciaInicio = "2026-01-01",
            VigenciaFim = "2029-12-31",
            ArquivoId = arquivo.Id,
            AprovacaoInstancia = "cgtic",
            AprovacaoData = "2026-02-10",
            AprovacaoAtoTipo = "Resolução",
            AprovacaoAtoNumero = "3/2026",
            PublicacaoData = "2026-02-20",
            PublicacaoEndereco = Endereco
        }, await Orgao());

        var nivel = await NivelAsync(pdtic.Id);
        Assert.Equal((null, null, null), (nivel.AlcancadoId, nivel.ProximoId, nivel.ProximoNome));
        Assert.Empty(nivel.Faltam);
        Assert.Equal("O PDTIC foi aprovado fora do sistema: o nível não é calculado.", nivel.Motivo);

        // Na conformidade, sem nível alcançado
        var linha = await LinhaAsync(OrgaoSes);
        Assert.Null(linha.NivelAlcancadoId);
    }

    [Fact]
    public async Task ConformidadeOrgaosEPaginaDoOrgao_OMesmoNivelDoLote()
    {
        var pdtic = await ProntoParaEnviarAsync();
        NovoOrgao("SEDF");

        var linha = await LinhaAsync(OrgaoSes);
        Assert.Equal((NivelId("basico"), "Básico"), (linha.NivelAlcancadoId, linha.NivelAlcancadoNome));
        Assert.Null((await LinhaAsync(OrgaoSeec)).NivelAlcancadoId);

        var orgaos = await Orgaos.ListarAsync(new PeOrgaosConsulta());
        var ses = orgaos.Single(o => o.OrgaoId == OrgaoSes.Id);
        Assert.Equal((NivelId("basico"), "Básico"), (ses.NivelAlcancadoId, ses.NivelAlcancadoNome));
        Assert.Null(orgaos.Single(o => o.OrgaoId == OrgaoSeec.Id).NivelAlcancadoNome);
        // O nível de hoje continua o de sempre (o escolhido ou o padrão)
        Assert.Equal((NivelId("basico"), true), (ses.NivelId, ses.NivelPadrao));

        var resumo = await Paineis.ResumoAsync(OrgaoSes.Id, await Sgdi());
        Assert.Equal(PeDominios.ModoNiveis.Definido, resumo.Nivel.Modo);
        Assert.Equal(("Básico", "Intermediário"), (resumo.Nivel.AlcancadoNome, resumo.Nivel.ProximoNome));
        var situacao = await SituacaoAsync(pdtic.Id);
        Assert.Equal(situacao.Nivel.Faltam.Select(f => f.PassoId), resumo.Nivel.Faltam.Select(f => f.PassoId));
    }

    [Fact]
    public async Task Livre_PainelEConformidade_PeloNivelAlcancado_ComOSemNivel()
    {
        Livre();
        await ProntoParaEnviarAsync();
        var sgdi = await Sgdi();

        // O painel conta pelo nível alcançado, com "Sem nível alcançado" (0) primeiro (a SEEC não tem PDTIC)
        var painel = await PainelAsync();
        Assert.Equal(new[] { (0L, "Sem nível alcançado", 1), (NivelId("basico"), "Básico", 1), (NivelId("intermediario"), "Intermediário", 0), (NivelId("avancado"), "Avançado", 0) },
            painel.PorNivel.Select(n => (n.NivelId, n.Nome, n.Quantidade)));
        Assert.Equal(new[] { (0L, "Sem nível alcançado"), (NivelId("basico"), "Básico"), (NivelId("intermediario"), "Intermediário"), (NivelId("avancado"), "Avançado") },
            painel.Filtros.Niveis.Select(n => (n.Id, n.Nome)));

        // O filtro do nível é pelo alcançado (0 = sem), no painel e na conformidade
        Assert.Equal(1, (await PainelAsync(nivelId: NivelId("basico"))).TotalOrgaos);
        Assert.Equal(1, (await PainelAsync(nivelId: 0)).TotalOrgaos);
        Assert.Equal(0, (await PainelAsync(nivelId: NivelId("intermediario"))).TotalOrgaos);
        Assert.Equal(OrgaoSeec.Id, Assert.Single((await Paineis.ConformidadeAsync(new PeConformidadeConsulta { NivelId = 0 }, sgdi)).Orgaos).OrgaoId);
        Assert.Equal(OrgaoSes.Id,
            Assert.Single((await Paineis.ConformidadeAsync(new PeConformidadeConsulta { NivelId = NivelId("basico") }, sgdi)).Orgaos).OrgaoId);

        // Na planilha, a coluna do nível é "Nível alcançado"
        var csv = await Paineis.ConformidadePlanilhaAsync(new PeConformidadeConsulta(), "csv", sgdi);
        var linhas = System.Text.Encoding.UTF8.GetString(csv.Conteudo[3..]).Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        Assert.StartsWith("Órgão;Sigla;Nível alcançado;Versão do PDTIC;", linhas[0]);
        Assert.StartsWith("Secretaria de Estado de Economia;SEEC;Sem nível alcançado;", linhas[1]);
        Assert.StartsWith("Secretaria de Estado de Saúde;SES;Básico;", linhas[2]);

        // No modo definido, como antes: pelo nível de hoje e sem o item 0
        DefinirModoNiveis(PeDominios.ModoNiveis.Definido);
        var definido = await PainelAsync();
        Assert.DoesNotContain(definido.PorNivel, n => n.NivelId == 0);
        Assert.DoesNotContain(definido.Filtros.Niveis, n => n.Id == 0);
        Assert.Equal(("Básico", 2), (definido.PorNivel[0].Nome, definido.PorNivel[0].Quantidade));
        var planilha = await Paineis.ConformidadePlanilhaAsync(new PeConformidadeConsulta(), "csv", sgdi);
        Assert.StartsWith("Órgão;Sigla;Nível;", System.Text.Encoding.UTF8.GetString(planilha.Conteudo[3..]));
    }

    private static byte[] PdfDeUmaPagina()
    {
        QuestPDF.Settings.License = LicenseType.Community;
        return Document.Create(d => d.Page(p => p.Content().Text("PDTIC aprovado fora do sistema."))).GeneratePdf();
    }
}
