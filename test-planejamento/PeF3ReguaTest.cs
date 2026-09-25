using System.Text;
using api.Planejamento;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using Models.Planejamento;
using service.Planejamento;
using Xunit;

namespace test.planejamento;

/// <summary>
/// F3, correção da régua do nível alcançado (achado do orquestrador na integração com o banco
/// local): um passo atende a um nível quando está completo na forma desse nível ou numa forma
/// mais completa (a de um nível ativo acima dele em que o passo está ligado), e o nível alcançado
/// é o mais alto que o PDTIC atende. No modelo inicial, o passo 2.9 tem campos alternativos entre
/// os níveis (a prioridade simples só na forma do Básico; as notas GUT nas do Intermediário e do
/// Avançado): o PDTIC com as notas GUT e sem a prioridade simples não alcançava nível nenhum. No
/// que falta do próximo nível, o motivo sai da forma em uso quando ela é igual ou mais completa
/// que a do próximo; senão, da forma do próximo, com o "Use a forma do nível X neste passo.".
/// </summary>
public class PeF3ReguaTest : PePaineisTestBase
{
    private void Livre() => DefinirModoNiveis(PeDominios.ModoNiveis.Livre);

    private async Task<PeNivelDoPdticResponse> NivelAsync(long pdticId) => (await SituacaoAsync(pdticId)).Nivel;

    /// <summary>A forma de um passo da SES (modo livre); nulo volta à forma padrão.</summary>
    private async Task FormaAsync(string passo, string? nivel) =>
        await Orgaos.DefinirDetalheAsync(OrgaoSes.Id, Passo(passo).Id,
            new PePassoDetalheDTO { NivelId = nivel == null ? null : NivelId(nivel) }, EmailAdmin);

    private static readonly object NecessidadeComGut = new
    {
        descricao = "Substituir o sistema de regulação.", tipo = "servico", origem = "swot", areas = "Regulação", priorizada = true,
        gravidade = 5, urgencia = 5, tendencia = 4
    };

    /// <summary>
    /// Troca a necessidade do PDTIC pronto para enviar (feita na forma do Básico) por uma feita só
    /// na forma do Intermediário: as notas GUT preenchidas e a prioridade simples vazia (o caso da
    /// SES 1.1 e do REVC 5.1 no banco local). A meta passa a ligar a necessidade nova.
    /// </summary>
    private async Task SoComGutAsync(long pdticId)
    {
        var dono = PeDono.DoPdtic(pdticId);
        var ctx = await Orgao();
        var antiga = (await Registros.ListarAsync(dono, "necessidades", ctx)).Registros.Single();
        await FormaAsync("diagnostico.necessidades-tic", "intermediario");
        var nova = await IncluirNoPdticAsync(pdticId, "necessidades", NecessidadeComGut);
        var meta = (await Registros.ListarAsync(dono, "metas", ctx)).Registros.Single();
        await Registros.AtualizarAsync(dono, "metas", meta.Id, Salvar(vinculos: new { necessidades = new[] { nova.Id } }), ctx);
        await Registros.ExcluirAsync(dono, "necessidades", antiga.Id, ctx);
        Assert.DoesNotContain("prioridade_simples", RegistroNoBanco(nova.Id).Dados);
    }

    private static string Texto(Cell celula) =>
        celula.DataType?.Value == CellValues.InlineString ? celula.InlineString!.InnerText : celula.CellValue?.Text ?? string.Empty;

    /// <summary>Os itens da Leia-me (rótulo na coluna A, valor na B).</summary>
    private static List<(string Rotulo, string Valor)> ItensDaLeiaMe(PePlanilhaArquivo planilha)
    {
        using var documento = SpreadsheetDocument.Open(new MemoryStream(planilha.Conteudo), false);
        var livro = documento.WorkbookPart!;
        var aba = livro.Workbook!.Sheets!.Elements<Sheet>().Single(s => s.Name == "Leia-me");
        var folha = ((WorksheetPart)livro.GetPartById(aba.Id!)).Worksheet!;
        return folha.Descendants<Row>()
            .Select(r => r.Elements<Cell>().Select(Texto).ToList())
            .Where(c => c.Count == 2)
            .Select(c => (c[0], c[1]))
            .ToList();
    }

    [Fact]
    public async Task ONoveComGut_SemAPrioridadeSimples_ChegaAoBasico_EmTodasAsLeituras()
    {
        Livre();
        var pdtic = await ProntoParaEnviarAsync();
        await SoComGutAsync(pdtic.Id);
        var nove = Passo("diagnostico.necessidades-tic").Id;

        // Na forma do Intermediário (a do órgão) o 2.9 está feito; na régua, essa forma substitui a do Básico
        var situacao = await SituacaoAsync(pdtic.Id);
        Assert.Equal(PeDominios.SituacaoPasso.Feito, situacao.Passos.Single(p => p.PassoId == nove).Situacao);
        Assert.Equal(("Básico", "Intermediário"), (situacao.Nivel.AlcancadoNome, situacao.Nivel.ProximoNome));
        Assert.DoesNotContain(situacao.Nivel.Faltam, f => f.PassoId == nove);

        // O mesmo nível em todas as leituras da régua
        var sgdi = await Sgdi();
        Assert.Equal("Básico", (await LinhaAsync(OrgaoSes)).NivelAlcancadoNome);
        Assert.Contains((NivelId("basico"), 1), (await PainelAsync()).PorNivel.Select(n => (n.NivelId, n.Quantidade)));
        Assert.Equal("Básico", (await Orgaos.ListarAsync(new PeOrgaosConsulta())).Single(o => o.OrgaoId == OrgaoSes.Id).NivelAlcancadoNome);
        Assert.Equal("Básico", (await Paineis.ResumoAsync(OrgaoSes.Id, sgdi)).Nivel.AlcancadoNome);
        var consolidado = Encoding.UTF8.GetString((await Planilhas.ConsolidadoSecaoAsync("necessidades", "csv", sgdi)).Conteudo[3..]);
        Assert.Contains("Secretaria de Estado de Saúde;SES;Básico;", consolidado);
        Assert.Contains(("Nível alcançado", "Básico"), ItensDaLeiaMe(await Planilhas.PdticSecaoAsync(pdtic.Id, "necessidades", "xlsx", sgdi)));

        // De volta à forma do Básico: o passo fica pendente pela forma em uso (falta a prioridade simples), e o nível não cai
        await FormaAsync("diagnostico.necessidades-tic", null);
        situacao = await SituacaoAsync(pdtic.Id);
        Assert.Equal(PeDominios.SituacaoPasso.Pendente, situacao.Passos.Single(p => p.PassoId == nove).Situacao);
        Assert.Equal("Básico", situacao.Nivel.AlcancadoNome);

        // No modo definido, a mesma régua
        DefinirModoNiveis(PeDominios.ModoNiveis.Definido);
        Assert.Equal("Básico", (await NivelAsync(pdtic.Id)).AlcancadoNome);
        Assert.Equal("Básico", (await LinhaAsync(OrgaoSes)).NivelAlcancadoNome);
    }

    [Fact]
    public async Task AFormaDoBasico_ComAPrioridadeSimples_ContinuaChegandoAoBasico()
    {
        var pdtic = await ProntoParaEnviarAsync();
        var nove = Passo("diagnostico.necessidades-tic").Id;

        // Modo definido (o órgão no Básico): o 2.9 aparece no que falta do Intermediário pela forma dele (as notas GUT)
        var nivel = await NivelAsync(pdtic.Id);
        Assert.Equal(("Básico", "Intermediário"), (nivel.AlcancadoNome, nivel.ProximoNome));
        var falta = Assert.Single(nivel.Faltam, f => f.PassoId == nove);
        Assert.Contains("\"Gravidade\"", falta.Motivo);
        Assert.DoesNotContain("Use a forma", falta.Motivo);

        // Modo livre: o mesmo nível; a forma em uso (a do Básico) é mais simples que a do próximo, que o motivo manda usar
        Livre();
        nivel = await NivelAsync(pdtic.Id);
        Assert.Equal("Básico", nivel.AlcancadoNome);
        falta = Assert.Single(nivel.Faltam, f => f.PassoId == nove);
        Assert.Contains("\"Gravidade\"", falta.Motivo);
        Assert.EndsWith(" Use a forma do nível Intermediário neste passo.", falta.Motivo);
    }

    [Fact]
    public async Task Livre_AFormaEscolhidaMaisCompletaQueADoProximoNivel_DaOMotivo_ECompletaAtende()
    {
        Livre();
        var pdtic = await ProntoParaEnviarAsync();
        var ativos = Passo("diagnostico.ativos").Id;
        await FormaAsync("diagnostico.ativos", "avancado");
        var registro = (await Registros.ListarAsync(PeDono.DoPdtic(pdtic.Id), "ativos", await Orgao())).Registros.Single();

        // O próximo é o Intermediário e o 2.4 está na forma do Avançado (mais completa): o motivo sai dela, com a
        // criticidade (obrigatória só no Avançado), e sem mandar usar outra forma
        var nivel = await NivelAsync(pdtic.Id);
        Assert.Equal(("Básico", "Intermediário"), (nivel.AlcancadoNome, nivel.ProximoNome));
        var falta = Assert.Single(nivel.Faltam, f => f.PassoId == ativos);
        Assert.Equal($"{registro.Codigo}: preencha \"Hospedagem\", \"Usa a rede GDFNet?\" e \"Criticidade\".", falta.Motivo);

        // Completo na forma do Avançado: atende ao Intermediário (e o Básico continua)
        await Registros.AtualizarAsync(PeDono.DoPdtic(pdtic.Id), "ativos", registro.Id, Salvar(new
        {
            nome = "Prontuário Eletrônico", tipo = "sistema", situacao = "em_operacao", hospedagem = "cetic", usa_gdfnet = true, criticidade = "alta"
        }), await Orgao());
        nivel = await NivelAsync(pdtic.Id);
        Assert.Equal("Básico", nivel.AlcancadoNome);
        Assert.DoesNotContain(nivel.Faltam, f => f.PassoId == ativos);
    }

    [Fact]
    public async Task Livre_PdticNovo_AsFormasMaisCompletasQueADoBasico_ContamParaOBasico()
    {
        Livre();
        var pdtic = await AbrirSesAsync();
        await FormaAsync("diagnostico.necessidades-tic", "intermediario");
        await IncluirNoPdticAsync(pdtic.Id, "necessidades", NecessidadeComGut);
        await FormaAsync("preparacao.equipe", "avancado");

        var nivel = await NivelAsync(pdtic.Id);
        Assert.Null(nivel.AlcancadoId);
        Assert.Equal("Básico", nivel.ProximoNome);
        // O 2.9 completo na forma do Intermediário atende ao Básico, sem a prioridade simples
        Assert.DoesNotContain(nivel.Faltam, f => f.PassoId == Passo("diagnostico.necessidades-tic").Id);
        // O 1.4 na forma do Avançado e sem nada: o motivo sai da forma em uso (com o ato de designação, que o Básico não pede)
        var equipe = Assert.Single(nivel.Faltam, f => f.PassoId == Passo("preparacao.equipe").Id);
        Assert.Equal("Inclua pelo menos um item em \"Equipe de elaboração\". Preencha \"Ato de designação da equipe\".", equipe.Motivo);

        // Na forma do Básico, o motivo é o do Básico
        await FormaAsync("preparacao.equipe", null);
        equipe = Assert.Single((await NivelAsync(pdtic.Id)).Faltam, f => f.PassoId == Passo("preparacao.equipe").Id);
        Assert.Equal("Inclua pelo menos um item em \"Equipe de elaboração\".", equipe.Motivo);
    }

    [Fact]
    public async Task ONivelAlcancado_EOMaisAltoAtendido()
    {
        // Um nível acima do Básico que não pede a equipe (a cópia do Básico, com o passo opcional); só os dois ativos
        var piloto = await Modelo.CriarNivelAsync(new PeNivelCriarDTO { Nome = "Piloto", CopiarDe = NivelId("basico") }, EmailAdmin);
        await Modelo.DefinirSituacaoPassoAsync(Passo("preparacao.equipe").Id, So("piloto", PeDominios.Situacao.Opcional), EmailAdmin);
        foreach (var codigo in new[] { "intermediario", "avancado" })
            await Modelo.AtualizarNivelAsync(NivelId(codigo), new PeNivelAtualizarDTO { Ativo = false, Informados = new HashSet<string> { "Ativo" } },
                EmailAdmin);
        var pdtic = await ProntoParaEnviarAsync();
        Assert.Equal("Piloto", (await NivelAsync(pdtic.Id)).AlcancadoNome);

        // Sem a equipe, o Básico não é atendido, mas o Piloto (que não a pede) é: vale o mais alto atendido
        var dono = PeDono.DoPdtic(pdtic.Id);
        var equipe = (await Registros.ListarAsync(dono, "equipe_elaboracao", await Orgao())).Registros.Single();
        await Registros.ExcluirAsync(dono, "equipe_elaboracao", equipe.Id, await Orgao());
        var nivel = await NivelAsync(pdtic.Id);
        Assert.Equal<(long?, string?)>((piloto.Id, "Piloto"), (nivel.AlcancadoId, nivel.AlcancadoNome));
        Assert.Null(nivel.ProximoId);
        Assert.Empty(nivel.Faltam);
    }
}
