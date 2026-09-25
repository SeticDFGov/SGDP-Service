using api.Planejamento;
using Microsoft.EntityFrameworkCore;
using Models.Planejamento;
using service;
using service.Planejamento;
using Xunit;

namespace test.planejamento;

/// <summary>
/// As grades do ciclo de monitoramento (E7, rodada B): a situação das ações e as medições dos
/// indicadores, gravadas pelo motor de registros (a validação do nível, os erros por
/// "id.campo"), só com as linhas enviadas e a grade inteira de volta; a linha vazia de um
/// registro que existe o apaga; o registro do ciclo anterior ao lado; os campos que o nível
/// esconde são ignorados; só a equipe do órgão grava, e só nos ciclos de monitoramento.
/// </summary>
public class PeGradesTest : PeAcompanhamentoTestBase
{
    [Fact]
    public async Task Acoes_AGradeTemOPlano_GravaSoAsLinhasEnviadas_EDevolveTudo()
    {
        var id = await AcompanhadoAsync();
        var t1 = await CicloAsync(id, Trimestre1);
        var (a1, a2, a3) = (IdDe(id, "A01"), IdDe(id, "A02"), IdDe(id, "A03"));

        var grade = await Acompanhamento.AcoesAsync(t1.Id, await Orgao());
        Assert.Equal(new[] { "A01", "A02", "A03" }, grade.Select(a => a.Codigo));
        var primeira = grade[0];
        Assert.Equal(("Contratar e implantar a solução de regulação.", new DateOnly(2026, 3, 1), new DateOnly(2027, 6, 30)),
            (primeira.Descricao, primeira.InicioPrevisto!.Value, primeira.ConclusaoPrevista!.Value));
        Assert.Equal(new[] { "M01" }, primeira.Metas);
        Assert.All(grade, a => Assert.Null(a.RegistroId));

        var gravada = await GravarAcoesAsync(t1.Id, Linha(a1, "em_andamento", 35.5m, 20, "Termo de referência aprovado."),
            Linha(a3, "cancelada", 0));

        Assert.Equal(3, gravada.Count);
        var linha1 = gravada.Single(a => a.AcaoId == a1);
        Assert.NotNull(linha1.RegistroId);
        Assert.Equal(("em_andamento", 35.5m, 20m, "Termo de referência aprovado."),
            (linha1.Situacao, linha1.ExecucaoFisica!.Value, linha1.ExecucaoOrcamentaria!.Value, linha1.Observacao));
        Assert.Null(gravada.Single(a => a.AcaoId == a2).RegistroId);
        Assert.Equal("cancelada", gravada.Single(a => a.AcaoId == a3).Situacao);

        // O registro é da seção monitoramento_acoes, ligado à ação e ao ciclo
        var registro = Context.PeRegistros.AsNoTracking().Single(r => r.Id == linha1.RegistroId);
        Assert.Equal(Secao("monitoramento_acoes").Id, registro.SecaoId);
        Assert.Equal(t1.Id, Context.PeRegistrosCiclo.AsNoTracking().Single(c => c.Id == registro.Id).CicloId);
        Assert.Equal(a1, Context.PeVinculos.AsNoTracking().Single(v => v.RegistroOrigemId == registro.Id).RegistroDestinoId);

        // Mudar uma linha não mexe nas outras; a linha vazia de um registro que existe o apaga
        await GravarAcoesAsync(t1.Id, Linha(a1, "concluida", 100));
        var depois = await GravarAcoesAsync(t1.Id, Linha(a3, null));
        Assert.Equal(("concluida", 100m, linha1.RegistroId), (depois[0].Situacao, depois[0].ExecucaoFisica!.Value, depois[0].RegistroId));
        Assert.Null(depois.Single(a => a.AcaoId == a3).RegistroId);
        Assert.Single(Context.PeRegistrosCiclo.AsNoTracking().Where(c => c.CicloId == t1.Id));

        // Lista vazia: nada muda
        Assert.Equal(depois.Select(a => a.RegistroId), (await GravarAcoesAsync(t1.Id)).Select(a => a.RegistroId));
    }

    [Fact]
    public async Task Acoes_ValidacaoDoNivel_ComOsErrosPorIdECampo()
    {
        var id = await AcompanhadoAsync();
        var t1 = await CicloAsync(id, Trimestre1);
        var (a1, a2) = (IdDe(id, "A01"), IdDe(id, "A02"));

        // Situação fora da lista, percentual acima de 100 e a execução física obrigatória no Avançado
        var campos = await CamposComErroAsync(() => GravarAcoesAsync(t1.Id, Linha(a1, "atrasada", 20), Linha(a2, "em_andamento", 120)));
        Assert.Equal(new[] { $"{a1}.situacao", $"{a2}.execucao_fisica" }, campos.Keys.OrderBy(k => k));
        campos = await CamposComErroAsync(() => GravarAcoesAsync(t1.Id, Linha(a1, "em_andamento")));
        Assert.Equal($"{a1}.execucao_fisica", Assert.Single(campos.Keys));

        // Ação repetida, que não é do PDTIC e sem o id
        campos = await CamposComErroAsync(() => GravarAcoesAsync(t1.Id, Linha(a1, "em_andamento", 10), Linha(a1, "concluida", 100),
            Linha(IdDe(id, "M01"), "em_andamento", 10), new PeCicloAcaoItemDTO { Situacao = "em_andamento" }));
        Assert.Equal(new[] { $"{a1}.acao", $"{IdDe(id, "M01")}.acao", "acao" }.OrderBy(k => k), campos.Keys.OrderBy(k => k));

        // Com erro, nada foi gravado
        Assert.Empty(Context.PeRegistrosCiclo.AsNoTracking().Where(c => c.CicloId == t1.Id));
    }

    [Fact]
    public async Task Acoes_NoBasico_OsCamposQueONivelEsconde_SaoIgnorados()
    {
        var id = await AcompanhadoNoBasicoAsync();
        var t1 = await CicloAsync(id, Trimestre1);

        // O Básico só registra a situação: a execução mandada (o front manda o que veio) não entra
        var grade = await GravarAcoesAsync(t1.Id, Linha(IdDe(id, "A01"), "em_andamento", 50, 10));

        var linha = Assert.Single(grade);
        Assert.Equal("em_andamento", linha.Situacao);
        Assert.Null(linha.ExecucaoFisica);
        Assert.Null(linha.ExecucaoOrcamentaria);
        // Sem o passo 5.2, o Básico fecha só com a situação das ações, e sem gerar o RA
        var fechado = await FecharAsync(t1.Id);
        Assert.Equal(PeDominios.SituacaoCicloExibida.Fechado, fechado.Situacao);
        Assert.Null(fechado.Relatorio);
    }

    [Fact]
    public async Task Acoes_ORegistroDoCicloAnterior_VemAoLado()
    {
        var id = await AcompanhadoAsync();
        var t1 = await CicloAsync(id, Trimestre1);
        var t2 = await CicloAsync(id, Trimestre2);
        var a1 = IdDe(id, "A01");
        await GravarAcoesAsync(t1.Id, Linha(a1, "em_andamento", 30, 10));

        var grade = await Acompanhamento.AcoesAsync(t2.Id, await Orgao());

        var anterior = grade.Single(a => a.AcaoId == a1).Anterior!;
        Assert.Equal(("em_andamento", 30m, 10m, Trimestre1), (anterior.Situacao, anterior.ExecucaoFisica!.Value, anterior.ExecucaoOrcamentaria!.Value, anterior.Ciclo));
        Assert.Null(grade.Single(a => a.AcaoId == IdDe(id, "A02")).Anterior);
        Assert.Null((await Acompanhamento.AcoesAsync(t1.Id, await Orgao())).Single(a => a.AcaoId == a1).Anterior);
    }

    [Fact]
    public async Task Grades_QuemLeEQuemGrava_ESoNoMonitoramento()
    {
        var id = await AcompanhadoAsync();
        var t1 = await CicloAsync(id, Trimestre1);
        var linha = Linha(IdDe(id, "A01"), "em_andamento", 10);

        // A consulta do órgão e os papéis globais leem; só a equipe grava
        foreach (var user in new[] { UserConsultaSes, UserPeSgdi, UserPeCgtic, UserPeAdmin })
        {
            Assert.Equal(3, (await Acompanhamento.AcoesAsync(t1.Id, await ContextoDe(user))).Count);
            Assert.Empty(await Acompanhamento.MedicoesAsync(t1.Id, await ContextoDe(user)));
            Assert.Equal(Codigo(ErrorCode.PeSemPermissao), await ErroAsync(async () =>
                await Acompanhamento.SalvarAcoesAsync(t1.Id, new PeCicloAcoesDTO { Itens = new() { linha } }, await ContextoDe(user))));
        }
        Assert.Equal(Codigo(ErrorCode.PeSemPermissao), await ErroAsync(async () => await Acompanhamento.AcoesAsync(t1.Id, await ContextoDe(UserOrgaoSeec))));

        // A avaliação não tem grade; ciclo que não existe, 404; ciclo que não começou, 409 ao gravar
        var avaliacao = await AbrirAvaliacaoAsync(id);
        Assert.Equal(Codigo(ErrorCode.PeCicloInvalido), await ErroAsync(async () => await Acompanhamento.AcoesAsync(avaliacao.Id, await Orgao())));
        Assert.Equal(Codigo(ErrorCode.PeCicloInvalido), await ErroAsync(async () => await Acompanhamento.MedicoesAsync(avaliacao.Id, await Orgao())));
        Assert.Equal(Codigo(ErrorCode.PeCicloNaoEncontrado), await ErroAsync(async () => await Acompanhamento.AcoesAsync(999999, await Orgao())));
        var ultimo = await CicloAsync(id, UltimoTrimestre);
        Assert.Equal(3, (await Acompanhamento.AcoesAsync(ultimo.Id, await Orgao())).Count);
        Assert.Equal(Codigo(ErrorCode.PeCicloFechado), await ErroAsync(() => GravarAcoesAsync(ultimo.Id, linha)));
    }

    [Fact]
    public async Task Medicoes_OsIndicadoresDoPlano_ComAMedicaoDoCicloEADoAnterior()
    {
        var id = await AcompanhadoAsync();
        var indicador = await PlanoDeMonitoramentoAsync(id);
        var t1 = await CicloAsync(id, Trimestre1);
        var t2 = await CicloAsync(id, Trimestre2);

        var grade = await Acompanhamento.MedicoesAsync(t1.Id, await Orgao());
        var linha = Assert.Single(grade);
        Assert.Equal(("IM01", "Unidades com o sistema de regulação", "25% no 1º trimestre, 50% no 2º"), (linha.Codigo, linha.Indicador, linha.ValoresReferencia));
        Assert.Null(linha.RegistroId);

        var gravada = await GravarMedicoesAsync(t1.Id, new PeCicloMedicaoItemDTO
        {
            IndicadorId = indicador.Id, ValorApurado = 22.5m, Data = "2026-03-31", Observacao = "Três unidades a menos que o previsto."
        });
        Assert.Equal((22.5m, new DateOnly(2026, 3, 31)), (gravada[0].ValorApurado!.Value, gravada[0].Data!.Value));

        // No ciclo seguinte, a medição anterior ao lado; data inválida volta no campo
        var seguinte = Assert.Single(await Acompanhamento.MedicoesAsync(t2.Id, await Orgao()));
        Assert.Equal((22.5m, Trimestre1), (seguinte.Anterior!.ValorApurado!.Value, seguinte.Anterior.Ciclo));
        var campos = await CamposComErroAsync(() => GravarMedicoesAsync(t2.Id,
            new PeCicloMedicaoItemDTO { IndicadorId = indicador.Id, ValorApurado = 40, Data = "31/06/2026" }));
        Assert.Equal($"{indicador.Id}.data", Assert.Single(campos.Keys));
        // O valor apurado é obrigatório no nível
        campos = await CamposComErroAsync(() => GravarMedicoesAsync(t2.Id, new PeCicloMedicaoItemDTO { IndicadorId = indicador.Id, Data = "2026-06-30" }));
        Assert.Equal($"{indicador.Id}.valor_apurado", Assert.Single(campos.Keys));
        campos = await CamposComErroAsync(() => GravarMedicoesAsync(t2.Id, new PeCicloMedicaoItemDTO { IndicadorId = IdDe(id, "A01"), ValorApurado = 1, Data = "2026-06-30" }));
        Assert.Equal($"{IdDe(id, "A01")}.indicador", Assert.Single(campos.Keys));

        // A linha vazia apaga a medição
        var vazia = await GravarMedicoesAsync(t1.Id, new PeCicloMedicaoItemDTO { IndicadorId = indicador.Id });
        Assert.Null(Assert.Single(vazia).RegistroId);
        Assert.Equal(0, (await CicloAsync(id, Trimestre1)).Resumo.Medicoes);
    }

    [Fact]
    public async Task Medicoes_NoBasico_404()
    {
        var id = await AcompanhadoNoBasicoAsync();
        var t1 = await CicloAsync(id, Trimestre1);

        Assert.Equal(Codigo(ErrorCode.PeSecaoIndisponivel), await ErroAsync(async () => await Acompanhamento.MedicoesAsync(t1.Id, await Orgao())));
    }
}
