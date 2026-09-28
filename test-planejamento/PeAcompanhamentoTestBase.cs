using api.Planejamento;
using app.Models;
using Controllers.Planejamento;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Models.Planejamento;
using service.Planejamento;

namespace test.planejamento;

/// <summary>
/// Base dos testes da E7, rodada B (o acompanhamento do PDTIC): a base da aprovação (rodada A),
/// com atalhos para pôr o PDTIC publicado numa data (o acompanhamento começa nela), ler os
/// ciclos, abrir a avaliação intermediária, gravar as grades e preencher o que o fechamento
/// pede. O PDTIC completo do Avançado (o da E5) publicado em 10/01/2026, com a vigência de
/// 01/01/2026 a 31/12/2029, tem 16 ciclos trimestrais: "2026 · 1º trimestre" a "2029 · 4º
/// trimestre". As ações dele: A01 (em andamento), A02 e A03 (não iniciadas), com início em
/// 01/03/2026 e conclusão em 30/06/2027; as metas: M01 (A01) e M02 (A02 e A03).
/// </summary>
public abstract class PeAcompanhamentoTestBase : PeAprovacaoTestBase
{
    // 10/01/2026 ao meio-dia em Brasília
    protected static readonly DateTime Publicacao = new(2026, 1, 10, 15, 0, 0, DateTimeKind.Utc);

    protected const string Trimestre1 = "2026 · 1º trimestre";
    protected const string Trimestre2 = "2026 · 2º trimestre";
    protected const string UltimoTrimestre = "2029 · 4º trimestre";

    /// <summary>Põe o PDTIC publicado (ou em acompanhamento) numa data, à mão.</summary>
    protected void PublicarEm(long pdticId, DateTime quando, string situacao = PeDominios.SituacaoPdtic.Publicado)
    {
        var pdtic = Context.PePdtics.Single(p => p.Id == pdticId);
        pdtic.Situacao = situacao;
        pdtic.EnviadoEm ??= quando;
        pdtic.AprovadoEm ??= quando;
        pdtic.PublicadoEm = quando;
        Context.SaveChanges();
        Context.ChangeTracker.Clear();
    }

    /// <summary>O PDTIC completo do Avançado (E5), publicado em 10/01/2026 (ou na data dada).</summary>
    protected async Task<long> AcompanhadoAsync(DateTime? publicadoEm = null)
    {
        var pdtic = await PdticCompletoAsync();
        PublicarEm(pdtic.Id, publicadoEm ?? Publicacao);
        return pdtic.Id;
    }

    /// <summary>O PDTIC mínimo do Básico (a ação A01, não iniciada), publicado em 10/01/2026 (ou na data dada).</summary>
    protected async Task<long> AcompanhadoNoBasicoAsync(DateTime? publicadoEm = null)
    {
        var pdtic = await ProntoParaEnviarAsync();
        PublicarEm(pdtic.Id, publicadoEm ?? Publicacao);
        return pdtic.Id;
    }

    protected async Task<List<PeCicloResponse>> CiclosAsync(long pdticId, string? tipo = null, User? user = null) =>
        await Acompanhamento.ListarCiclosAsync(pdticId, tipo, await ContextoDe(user ?? UserOrgaoSes));

    protected async Task<PeCicloResponse> CicloAsync(long pdticId, string rotulo) =>
        (await CiclosAsync(pdticId)).Single(c => c.Rotulo == rotulo);

    protected async Task<PeCicloResponse> AbrirAvaliacaoAsync(long pdticId, string? rotulo = null, User? user = null) =>
        await Acompanhamento.CriarCicloAsync(pdticId, new PeCicloCriarDTO { Tipo = PeDominios.TipoCiclo.Avaliacao, Rotulo = rotulo },
            await ContextoDe(user ?? UserOrgaoSes));

    protected async Task<PeCicloResponse> FecharAsync(long cicloId, User? user = null) =>
        await Acompanhamento.FecharCicloAsync(cicloId, await ContextoDe(user ?? UserOrgaoSes));

    /// <summary>O id de um registro do PDTIC pelo código ("A01", "M02", "R01").</summary>
    protected long IdDe(long pdticId, string codigo) =>
        Context.PeRegistros.AsNoTracking().Single(r => r.PdticId == pdticId && r.Codigo == codigo).Id;

    /// <summary>Uma linha da grade das ações.</summary>
    protected static PeCicloAcaoItemDTO Linha(long acaoId, string? situacao, decimal? fisica = null, decimal? orcamentaria = null,
        string? observacao = null) => new()
    {
        AcaoId = acaoId,
        Situacao = situacao,
        ExecucaoFisica = fisica,
        ExecucaoOrcamentaria = orcamentaria,
        Observacao = observacao
    };

    protected async Task<List<PeCicloAcaoResponse>> GravarAcoesAsync(long cicloId, params PeCicloAcaoItemDTO[] itens) =>
        await Acompanhamento.SalvarAcoesAsync(cicloId, new PeCicloAcoesDTO { Itens = itens.ToList() }, await Orgao());

    protected async Task<List<PeCicloMedicaoResponse>> GravarMedicoesAsync(long cicloId, params PeCicloMedicaoItemDTO[] itens) =>
        await Acompanhamento.SalvarMedicoesAsync(cicloId, new PeCicloMedicoesDTO { Itens = itens.ToList() }, await Orgao());

    /// <summary>
    /// O que o fechamento do ciclo de monitoramento pede no Avançado: a situação de cada ação
    /// (com a execução física, obrigatória no nível) e o resumo do ciclo (passo 5.2).
    /// </summary>
    protected async Task PreencherCicloAsync(long pdticId, long cicloId, string situacao = "em_andamento")
    {
        var grade = await Acompanhamento.AcoesAsync(cicloId, await Orgao());
        await GravarAcoesAsync(cicloId, grade.Select(a => Linha(a.AcaoId, situacao, 40)).ToArray());
        await IncluirNoPdticAsync(pdticId, "relatorio_ciclo", new { resumo = "O ciclo andou como o planejado." }, cicloId: cicloId);
    }

    /// <summary>O plano de monitoramento (passo 4.3): a periodicidade e um indicador (IM01).</summary>
    protected async Task<PeRegistroResponse> PlanoDeMonitoramentoAsync(long pdticId, string periodicidade = "trimestral")
    {
        await IncluirNoPdticAsync(pdticId, "periodicidade_monitoramento", new { periodicidade });
        return await IncluirNoPdticAsync(pdticId, "indicadores_monitoramento", new
        {
            indicador = "Unidades com o sistema de regulação",
            objeto = "Meta M01",
            formula = "Unidades com o sistema / total de unidades",
            responsavel = "Coordenação de Sistemas",
            valores_referencia = "25% no 1º trimestre, 50% no 2º"
        });
    }

    protected PeAcompanhamentoController ControladorAcompanhamento(User user) => new(Acompanhamento, Permissoes)
    {
        ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = Principal(user) } }
    };

    /// <summary>A versão do conteúdo semeado, à mão (5 = antes da rodada B: o acompanhamento desligado).</summary>
    protected void VersaoDoModelo(int versao)
    {
        var linha = Context.PeConfiguracoes.Single(c => c.Chave == PeConfiguracao.ChaveVersaoModelo);
        linha.Valor = versao.ToString(System.Globalization.CultureInfo.InvariantCulture);
        Context.SaveChanges();
        Context.ChangeTracker.Clear();
    }
}
