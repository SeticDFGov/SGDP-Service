using api.Planejamento;
using Microsoft.EntityFrameworkCore;
using Models.Planejamento;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using service;
using service.Planejamento;
using Xunit;

namespace test.planejamento;

/// <summary>
/// F1, a rodada de correções da revisão final, no caminho da aprovação: o reenvio depois da
/// devolução do CGTIC pede a aprovação do SGTIC de novo (decisão D3); a data da decisão não pode
/// ser futura e o processo SEI tem o formato do SEI (C05); o endereço da íntegra é de internet
/// (C33); a deliberação do PDTIC registrado fora do sistema tem a data do ato e a marca do
/// registro (C40); os nomes das pessoas ao lado dos e-mails (C19); o porquê do próximo passo na
/// revisão recém-aberta (B12); e o rótulo único da devolução (B15).
/// </summary>
public class PeF1AprovacaoTest : PeAprovacaoTestBase
{
    private static byte[] PdfDeUmaPagina()
    {
        QuestPDF.Settings.License = LicenseType.Community;
        return Document.Create(c => c.Page(p => p.Content().Text("PDTIC aprovado fora do sistema."))).GeneratePdf();
    }

    private async Task<PeRegistroExternoDTO> RegistroExternoAsync(string instancia = "cgtic", string endereco = Endereco)
    {
        var arquivo = await EnviarArquivoAsync(UserOrgaoSes, "PDTIC SES.pdf", PdfDeUmaPagina());
        return new PeRegistroExternoDTO
        {
            Versao = "2.0",
            VigenciaInicio = "2025-01-01",
            VigenciaFim = "2028-12-31",
            ArquivoId = arquivo.Id,
            AprovacaoInstancia = instancia,
            AprovacaoData = "2025-03-10",
            AprovacaoAtoTipo = "Resolução",
            AprovacaoAtoNumero = "5/2025",
            AprovacaoSei = "00060-00001234/2025-11",
            PublicacaoData = "2025-03-20",
            PublicacaoEndereco = endereco
        };
    }

    private static string Amanha() => DateOnly.FromDateTime(demanda_service.Helpers.DateTimeHelper.TodayBrasilia()).AddDays(1)
        .ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);

    // ── D3: depois da devolução, a aprovação do SGTIC de novo ──────────────

    [Fact]
    public async Task Reenvio_DepoisDaDevolucao_PedeAAprovacaoDoSgticDeNovo()
    {
        var pdtic = await ProntoParaEnviarAsync();
        await EnviarAsync(pdtic.Id);
        await DevolverNoCgticAsync(pdtic.Id);

        // A aprovação do SGTIC de antes da devolução (01/09/2026) não vale para o reenvio
        var envio = await Aprovacao.EnvioAsync(pdtic.Id, await Orgao());
        Assert.False(envio.PodeEnviar);
        var pendencia = Assert.Single(envio.Pendencias);
        Assert.Equal(PePdticAprovacaoService.PendenciaSgticDepoisDaDevolucao, pendencia.Motivo);
        Assert.Equal("Registre de novo a aprovação do SGTIC: a versão ajustada depois da devolução do CGTIC precisa passar pelo comitê interno.",
            pendencia.Motivo);
        Assert.Equal("planejamento.aprovacao-sgtic", Context.PePassos.AsNoTracking().Single(p => p.Id == pendencia.PassoId).Chave);

        // O POST recusa com a mesma lista (400 com Pendencias)
        var ex = await Assert.ThrowsAsync<PePendenciasException>(() => EnviarAsync(pdtic.Id));
        Assert.Equal(PePdticAprovacaoService.PendenciaSgticDepoisDaDevolucao, Assert.Single(ex.Pendencias).Motivo);

        // A aprovação registrada no dia da devolução (ou depois) vale
        await AprovacaoSgticAsync(pdtic.Id, data: HojeIso());
        Assert.True((await Aprovacao.EnvioAsync(pdtic.Id, await Orgao())).PodeEnviar);
        Assert.Equal(PeDominios.SituacaoPdtic.EmAprovacao, (await EnviarAsync(pdtic.Id)).Situacao);
    }

    [Fact]
    public async Task PrimeiroEnvio_SemDevolucao_AceitaAAprovacaoDeAntes()
    {
        var pdtic = await ProntoParaEnviarAsync();
        Assert.True((await Aprovacao.EnvioAsync(pdtic.Id, await Orgao())).PodeEnviar);
    }

    // ── C05: data da decisão não futura e processo SEI com formato ────────

    [Fact]
    public async Task AprovacaoDoSgtic_DataFutura_RecusadaNoMotor_ENoEnvio()
    {
        var pdtic = await ProntoParaEnviarAsync();
        var dono = PeDono.DoPdtic(pdtic.Id);
        var registro = (await Registros.ListarAsync(dono, "aprovacao_sgtic", await Orgao())).Registros.Single();

        // O motor recusa pela regra do config do campo (naoFutura)
        var campos = await CamposComErroAsync(async () => await Registros.AtualizarAsync(dono, "aprovacao_sgtic", registro.Id,
            Salvar(new { decisao = "aprovado", data = Amanha() }), await Orgao()));
        Assert.Equal(PeValores.MensagemDataFutura, campos["data"]);
        Assert.Equal("A data não pode ser depois de hoje.", campos["data"]);

        // A data futura gravada antes da regra: o envio recusa
        var linha = Context.PeRegistros.Single(r => r.Id == registro.Id);
        linha.Dados = linha.Dados.Replace("2026-09-01", Amanha());
        Context.SaveChanges();
        Context.ChangeTracker.Clear();
        var envio = await Aprovacao.EnvioAsync(pdtic.Id, await Orgao());
        Assert.Equal(PePdticAprovacaoService.PendenciaSgticDataFutura, Assert.Single(envio.Pendencias).Motivo);
        await Assert.ThrowsAsync<PePendenciasException>(() => EnviarAsync(pdtic.Id));
    }

    [Fact]
    public async Task SecoesDeAprovacao_DataNaoFutura_ESeiNoFormato()
    {
        var pdtic = await ProntoParaEnviarAsync();
        var dono = PeDono.DoPdtic(pdtic.Id);
        var registro = (await Registros.ListarAsync(dono, "aprovacao_sgtic", await Orgao())).Registros.Single();

        var campos = await CamposComErroAsync(async () => await Registros.AtualizarAsync(dono, "aprovacao_sgtic", registro.Id,
            Salvar(new { decisao = "aprovado", data = "2026-09-01", sei = "123" }), await Orgao()));
        Assert.Equal("Informe o processo SEI no formato 00000-00000000/0000-00.", campos["sei"]);

        var salvo = await Registros.AtualizarAsync(dono, "aprovacao_sgtic", registro.Id,
            Salvar(new { decisao = "aprovado", data = HojeIso(), sei = "00060-00001234/2026-11" }), await Orgao());
        Assert.Equal("00060-00001234/2026-11", Valor(salvo, "sei"));

        // O ato de criação do SGTIC também (como os de designação): a data não é futura e o SEI tem o formato
        var sgtic = (await Registros.ListarAsync(dono, "sgtic", await Orgao())).Registros.Single();
        var ato = await CamposComErroAsync(async () => await Registros.AtualizarAsync(dono, "sgtic", sgtic.Id,
            Salvar(new { forma = "subcomite", ato_tipo = "portaria", ato_numero = "12/2026", ato_data = Amanha(), sei = "abc" }), await Orgao()));
        Assert.Equal(PeValores.MensagemDataFutura, ato["ato_data"]);
        Assert.Equal(PeValores.MensagemSei, ato["sei"]);
    }

    // ── C33: o endereço da íntegra é um endereço de internet ──────────────

    [Fact]
    public async Task RegistroExterno_EnderecoQueNaoEhDeInternet_400NoCampo()
    {
        var (codigo, campos) = await ValidacaoAsync(async () =>
            await Aprovacao.RegistrarExternoAsync(await RegistroExternoAsync(endereco: "site do órgão"), await Orgao()));
        Assert.Equal((int)ErrorCode.PeRegistroExternoInvalido, codigo);
        Assert.Equal(PeValores.MensagemEndereco, campos[nameof(PeRegistroExternoDTO.PublicacaoEndereco)]);

        // O prefixo repetido (https://https://...) também não vale (B02)
        (_, campos) = await ValidacaoAsync(async () =>
            await Aprovacao.RegistrarExternoAsync(await RegistroExternoAsync(endereco: "https://https://www.saude.df.gov.br"), await Orgao()));
        Assert.True(campos.ContainsKey(nameof(PeRegistroExternoDTO.PublicacaoEndereco)));

        var registrado = await Aprovacao.RegistrarExternoAsync(await RegistroExternoAsync(), await Orgao());
        Assert.Equal(PeDominios.SituacaoPdtic.Publicado, registrado.Situacao);
    }

    [Fact]
    public async Task Publicacao_EnderecoDeInternet_NoMotorENaConfirmacao()
    {
        var pdtic = await ProntoParaEnviarAsync();
        await EnviarAsync(pdtic.Id);
        await AprovarNoCgticAsync(pdtic.Id);

        var campos = await CamposComErroAsync(() => IncluirNoPdticAsync(pdtic.Id, "publicacao", new { data = "2026-09-15", endereco = "site do órgão" }));
        Assert.Equal(PeValores.MensagemEndereco, campos["endereco"]);
        // A data da publicação também não é futura
        campos = await CamposComErroAsync(() => IncluirNoPdticAsync(pdtic.Id, "publicacao", new { data = Amanha(), endereco = Endereco }));
        Assert.Equal(PeValores.MensagemDataFutura, campos["data"]);

        // O endereço gravado antes da regra: a confirmação da publicação recusa no campo
        var registro = await IncluirNoPdticAsync(pdtic.Id, "publicacao", new { data = "2026-09-15", endereco = Endereco });
        var linha = Context.PeRegistros.Single(r => r.Id == registro.Id);
        linha.Dados = linha.Dados.Replace(Endereco, "site do órgão");
        Context.SaveChanges();
        Context.ChangeTracker.Clear();
        var (codigo, erros) = await ValidacaoAsync(async () => await Aprovacao.PublicarAsync(pdtic.Id, await Orgao()));
        Assert.Equal((int)ErrorCode.PePublicacaoIncompleta, codigo);
        Assert.Equal(PeValores.MensagemEndereco, erros["endereco"]);
    }

    // ── C40: a deliberação do registro fora do sistema ─────────────────────

    [Fact]
    public async Task RegistroExterno_ADeliberacaoTemADataDoAto_EAMarcaDoRegistro()
    {
        var pdtic = await Aprovacao.RegistrarExternoAsync(await RegistroExternoAsync(), await Orgao());

        var deliberacao = pdtic.Deliberacao!;
        Assert.True(deliberacao.RegistradaForaDoSistema);
        Assert.Equal(new DateTime(2025, 3, 10, 12, 0, 0), demanda_service.Helpers.DateTimeHelper.ToBrasilia(deliberacao.DecididoEm!.Value));
        // O registro no sistema fica em EnviadoEm (hoje)
        Assert.Equal(DateOnly.FromDateTime(demanda_service.Helpers.DateTimeHelper.TodayBrasilia()),
            DateOnly.FromDateTime(demanda_service.Helpers.DateTimeHelper.ToBrasilia(deliberacao.EnviadoEm)));
        Assert.Equal("Otávio da Saúde", deliberacao.EnviadoPorNome);

        // Linha gravada antes da F1 (com o dia do registro): a resposta usa o dia do ato
        var linha = Context.PeDeliberacoes.Single(d => d.ObjetoTipo == "pdtic" && d.ObjetoId == pdtic.Id);
        linha.DecididoEm = DateTime.UtcNow;
        Context.SaveChanges();
        Context.ChangeTracker.Clear();
        var fila = (await Deliberacoes.ListarAsync(new PeDeliberacoesConsulta { ObjetoTipo = "pdtic" })).Items.Single();
        Assert.True(fila.RegistradaForaDoSistema);
        Assert.Equal(new DateOnly(2025, 3, 10), DateOnly.FromDateTime(demanda_service.Helpers.DateTimeHelper.ToBrasilia(fila.DecididoEm!.Value)));
    }

    [Fact]
    public async Task DeliberacaoDoEnvio_NaoEhDoRegistroForaDoSistema_EtemOsNomes()
    {
        var pdtic = await ProntoParaEnviarAsync();
        await EnviarAsync(pdtic.Id);
        var aprovada = await AprovarNoCgticAsync(pdtic.Id);

        Assert.False(aprovada.RegistradaForaDoSistema);
        Assert.Equal(("otavio@saude.df.gov.br", "Otávio da Saúde"), (aprovada.EnviadoPor, aprovada.EnviadoPorNome));
        Assert.Equal(("carla@cgtic.df.gov.br", "Carla do CGTIC"), (aprovada.DecididoPor, aprovada.DecididoPorNome));
    }

    // ── C19: nomes ao lado dos e-mails ─────────────────────────────────────

    [Fact]
    public async Task Nomes_NoRegistro_NoPdtic_ENaMarcaDeNaoSeAplica_ComOEmailQuandoNaoHaCadastro()
    {
        var pdtic = await AbrirSesAsync();
        var registro = await IncluirNoPdticAsync(pdtic.Id, "equipe_elaboracao", new { nome = "Ana Souza" });
        Assert.Equal(("otavio@saude.df.gov.br", "Otávio da Saúde"), (registro.CriadoPor, registro.CriadoPorNome));
        Assert.Null(registro.AlteradoPorNome);
        var lido = (await Registros.ListarAsync(PeDono.DoPdtic(pdtic.Id), "equipe_elaboracao", await Orgao())).Registros.Single();
        Assert.Equal("Otávio da Saúde", lido.CriadoPorNome);
        Assert.Equal("Otávio da Saúde", (await Pdtics.ObterAsync(pdtic.Id, await Orgao())).CriadoPorNome);

        // Sem cadastro com o e-mail (um autor do sistema): vale o próprio e-mail
        var linha = Context.PeRegistros.Single(r => r.Id == registro.Id);
        linha.AlteradoEm = DateTime.UtcNow;
        linha.AlteradoPor = "carregador-modelo";
        Context.SaveChanges();
        Context.ChangeTracker.Clear();
        lido = (await Registros.ListarAsync(PeDono.DoPdtic(pdtic.Id), "equipe_elaboracao", await Orgao())).Registros.Single();
        Assert.Equal("carregador-modelo", lido.AlteradoPorNome);

        // O e-mail com outra caixa acha o mesmo cadastro
        var nomes = await PeNomes.CarregarAsync(Context, new[] { "OTAVIO@saude.df.gov.br", null, "  " });
        Assert.Equal("Otávio da Saúde", nomes.De("Otavio@Saude.df.gov.br"));
        Assert.Null(nomes.De(null));
    }

    // ── B12: o porquê do próximo passo na revisão recém-aberta ─────────────

    [Fact]
    public async Task RevisaoRecemAberta_OProximoPassoVemComOMotivo()
    {
        var vigente = await PublicadoAsync();
        var revisao = await Aprovacao.RevisarAsync(vigente.Id, new PeRevisaoDTO { Justificativa = "Mudou a estrutura." }, await Orgao());

        var situacao = await SituacaoAsync(revisao.Id);
        Assert.Equal(PePdticService.MotivoInicioDaRevisao, situacao.ProximoPassoMotivo);
        Assert.NotNull(situacao.ProximoPasso);

        // Depois da primeira gravação, o próximo passo é o de sempre, sem o motivo
        await IncluirNoPdticAsync(revisao.Id, "ativos", new { nome = "Firewall", tipo = "infraestrutura", situacao = "em_operacao" });
        Assert.Null((await SituacaoAsync(revisao.Id)).ProximoPassoMotivo);
    }

    // ── B15: o rótulo da devolução ─────────────────────────────────────────

    [Fact]
    public void RotuloDaDevolucao_OMesmoDaTela_ComASiglaNoMeioDaFrase()
    {
        Assert.Equal("Devolvido pelo CGTIC", PeDominios.SituacaoPdtic.Rotulo(PeDominios.SituacaoPdtic.Devolvido));
        Assert.Equal("devolvido pelo CGTIC", PeDominios.SituacaoPdtic.RotuloMinusculo(PeDominios.SituacaoPdtic.Devolvido));
        Assert.Equal("em elaboração", PeDominios.SituacaoPdtic.RotuloMinusculo(PeDominios.SituacaoPdtic.EmElaboracao));
    }
}
