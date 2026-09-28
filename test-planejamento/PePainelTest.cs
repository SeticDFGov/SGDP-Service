using api.Planejamento;
using Models.Planejamento;
using service;
using Xunit;

namespace test.planejamento;

/// <summary>
/// O painel do PDTIC (AC-PDTIC, E7 rodada B): as metas com as ações ligadas, o percentual de
/// cada ação (a execução física; a concluída conta 100) e o da meta (a média, ponderada pelo peso
/// da ação na meta do plano de execução quando todas têm; a cancelada fica de fora), a situação
/// física na data de referência, o orçamento, e os quadros dos riscos pela última ocorrência. O
/// ciclo de referência é o dado ou o último com dado; cada ação vem do último registro até ele.
/// </summary>
public class PePainelTest : PeAcompanhamentoTestBase
{
    /// <summary>
    /// Dois trimestres de acompanhamento no PDTIC completo: no 1º, A01 em andamento (30%), A02
    /// não iniciada e A03 cancelada, e o risco R01 aberto; no 2º, A01 concluída e R01 fechado. O
    /// plano de execução dá peso 60 à A01 e 40 à A02.
    /// </summary>
    private async Task<(long Id, PeCicloResponse T1, PeCicloResponse T2)> DoisTrimestresAsync()
    {
        var id = await AcompanhadoAsync();
        var (a1, a2, a3) = (IdDe(id, "A01"), IdDe(id, "A02"), IdDe(id, "A03"));
        await IncluirNoPdticAsync(id, "projetos", new { nome = "Implantação", peso_na_acao = 100, peso_da_acao_na_meta = 60, inicio = "2026-03-01", conclusao = "2027-06-30" },
            new { acao = new[] { a1 } });
        await IncluirNoPdticAsync(id, "projetos", new { nome = "Política de backup", peso_na_acao = 100, peso_da_acao_na_meta = 40, inicio = "2026-03-01", conclusao = "2027-06-30" },
            new { acao = new[] { a2 } });
        var t1 = await CicloAsync(id, Trimestre1);
        var t2 = await CicloAsync(id, Trimestre2);
        await GravarAcoesAsync(t1.Id, Linha(a1, "em_andamento", 30, 25), Linha(a2, "nao_iniciada", 0), Linha(a3, "cancelada", 0));
        await GravarAcoesAsync(t2.Id, Linha(a1, "concluida", 100, 90));
        var r1 = IdDe(id, "R01");
        await IncluirNoPdticAsync(id, "riscos_ocorridos",
            new { data = "2026-02-10", situacao = "aberto", acoes_realizadas = "Estudo técnico antecipado.", responsavel = "Coordenação de Sistemas" },
            new { risco = new[] { r1 } }, cicloId: t1.Id);
        await IncluirNoPdticAsync(id, "riscos_ocorridos",
            new { data = "2026-05-10", situacao = "fechado", acoes_realizadas = "Contrato assinado.", responsavel = "Coordenação de Sistemas" },
            new { risco = new[] { r1 } }, cicloId: t2.Id);
        return (id, t1, t2);
    }

    [Fact]
    public async Task Painel_NoUltimoCicloComDado_AsMetasAsAcoesEOsPercentuais()
    {
        var (id, _, t2) = await DoisTrimestresAsync();

        var painel = await Acompanhamento.PainelAsync(id, null, await ContextoDe(UserConsultaSes));

        Assert.Equal((t2.Id, Trimestre2), (painel.Ciclo!.Id, painel.Ciclo.Rotulo));
        var m1 = painel.Metas.Single(m => m.Codigo == "M01");
        Assert.Equal(("Implantar o novo sistema de regulação.", "Unidades usando o sistema", "100%", new DateOnly(2027, 12, 31)),
            (m1.Descricao, m1.Indicador, m1.Valor, m1.Prazo!.Value));
        Assert.Equal(new[] { "N01" }, m1.Necessidades);
        Assert.Equal(100m, m1.PercentualExecucao);
        var a1 = Assert.Single(m1.Acoes);
        Assert.Equal(("A01", 100m, "concluida", 90m, false), (a1.Codigo, a1.PercentualExecucao, a1.SituacaoFisica, a1.ExecucaoOrcamentaria, a1.OrcamentoNaoSeAplica));

        // A02 sem registro no 2º trimestre: vale o do 1º (não iniciada e o início previsto já passou em 30/06); A03 cancelada
        var m2 = painel.Metas.Single(m => m.Codigo == "M02");
        Assert.Equal(new[] { ("A02", "atrasada", (decimal?)0m), ("A03", "cancelada", 0m) },
            m2.Acoes.Select(a => (a.Codigo!, a.SituacaoFisica, a.PercentualExecucao)));
        // A cancelada fica de fora da média; a A02 tem peso: 0%
        Assert.Equal(0m, m2.PercentualExecucao);
        Assert.Empty(painel.AcoesSemMeta);

        // Riscos: R01 pela última ocorrência (fechado); R02 sem ocorrência; os dois de nível alto (média e alto)
        Assert.Equal(new Dictionary<string, int> { ["aberto"] = 0, ["fechado"] = 1, ["excluido"] = 0, ["sem_ocorrencia"] = 1 }, painel.Riscos.PorSituacao);
        Assert.Equal(new Dictionary<string, int> { ["alto"] = 2, ["medio"] = 0, ["baixo"] = 0 }, painel.Riscos.PorNivel);
        Assert.Equal(12, painel.Riscos.Matriz.Count);
        Assert.Equal(1, painel.Riscos.Matriz.Single(c => c.Situacao == "fechado" && c.Nivel == "alto").Quantidade);
        Assert.Equal(1, painel.Riscos.Matriz.Single(c => c.Situacao == "sem_ocorrencia" && c.Nivel == "alto").Quantidade);
        Assert.Equal(2, painel.Riscos.Matriz.Sum(c => c.Quantidade));
    }

    [Fact]
    public async Task Painel_NoCicloDado_AReferenciaEOFimDele()
    {
        var (id, t1, _) = await DoisTrimestresAsync();

        var painel = await Acompanhamento.PainelAsync(id, t1.Id, await Orgao());

        Assert.Equal(Trimestre1, painel.Ciclo!.Rotulo);
        var m1 = painel.Metas.Single(m => m.Codigo == "M01");
        Assert.Equal(30m, m1.PercentualExecucao);
        Assert.Equal(("em_dia", 30m, 25m), (m1.Acoes[0].SituacaoFisica, m1.Acoes[0].PercentualExecucao, m1.Acoes[0].ExecucaoOrcamentaria));
        // Em 31/03 o R01 estava aberto
        Assert.Equal(1, painel.Riscos.PorSituacao["aberto"]);

        // Ciclo de avaliação: 400; ciclo de outro PDTIC ou que não existe: 404
        var avaliacao = await AbrirAvaliacaoAsync(id);
        Assert.Equal(Codigo(ErrorCode.PeCicloInvalido), await ErroAsync(async () => await Acompanhamento.PainelAsync(id, avaliacao.Id, await Orgao())));
        Assert.Equal(Codigo(ErrorCode.PeCicloNaoEncontrado), await ErroAsync(async () => await Acompanhamento.PainelAsync(id, 999999, await Orgao())));
        Assert.Equal(Codigo(ErrorCode.PeSemPermissao), await ErroAsync(async () => await Acompanhamento.PainelAsync(id, null, await ContextoDe(UserOrgaoSeec))));
    }

    [Fact]
    public async Task Painel_SemDado_SemCiclo_EAsAcoesSemRegistro_ENoBasicoSemMetaLigada()
    {
        var id = await AcompanhadoNoBasicoAsync();

        var painel = await Acompanhamento.PainelAsync(id, null, await Orgao());

        Assert.Null(painel.Ciclo);
        // No Básico a ação não liga à meta: ela vem nas ações sem meta; sem investimento nem custeio no nível, o orçamento não se aplica
        var meta = Assert.Single(painel.Metas);
        Assert.Empty(meta.Acoes);
        Assert.Null(meta.PercentualExecucao);
        var acao = Assert.Single(painel.AcoesSemMeta);
        Assert.Equal(("A01", "sem_registro", (decimal?)null, true), (acao.Codigo, acao.SituacaoFisica, acao.PercentualExecucao, acao.OrcamentoNaoSeAplica));
        Assert.Equal(0, painel.Riscos.Matriz.Sum(c => c.Quantidade));
    }
}
