using System.IO.Compression;
using System.Text;
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
/// O PDTIC aprovado fora do sistema (E7, rodada A; decisão 20): a equipe do órgão (ou o admin
/// geral) registra o PDTIC já publicado, com a vigência, a aprovação e a publicação, e o PDF
/// enviado vira a versão 1 do documento (publicada); aprovado pelo CGTIC, vira uma deliberação
/// aprovada com o ato; por outra instância, a aprovação vai para a seção do SGTIC. As etapas 1
/// a 3 ficam "externas" (menos as metas e ações e os riscos, que a equipe preenche para
/// acompanhar), e as ligações com as seções externas deixam de ser obrigatórias.
/// </summary>
public class PeRegistroExternoTest : PeAprovacaoTestBase
{
    /// <summary>Um PDF de verdade com as páginas pedidas (o QuestPDF monta).</summary>
    private static byte[] PdfComPaginas(int paginas)
    {
        QuestPDF.Settings.License = LicenseType.Community;
        return Document.Create(c =>
        {
            for (var i = 1; i <= paginas; i++)
                c.Page(p => p.Content().Text($"Página {i} do PDTIC aprovado fora do sistema."));
        }).GeneratePdf();
    }

    private async Task<PeRegistroExternoDTO> CorpoAsync(string instancia = "cgtic", app.Models.User? quemEnvia = null, int paginas = 3)
    {
        var arquivo = await EnviarArquivoAsync(quemEnvia ?? UserOrgaoSes, "PDTIC SES 2025-2028.pdf", PdfComPaginas(paginas));
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
            PublicacaoEndereco = Endereco
        };
    }

    private async Task<PePdticResponse> RegistrarAsync(PeRegistroExternoDTO dto, app.Models.User? user = null) =>
        await Aprovacao.RegistrarExternoAsync(dto, await ContextoDe(user ?? UserOrgaoSes));

    [Fact]
    public async Task Registrar_AprovadoPeloCgtic_PublicadoComOPdfEADeliberacao()
    {
        var pdtic = await RegistrarAsync(await CorpoAsync());

        Assert.Equal("2.0", pdtic.Versao);
        Assert.Equal(PeDominios.SituacaoPdtic.Publicado, pdtic.Situacao);
        Assert.True(pdtic.RegistradoExternamente);
        Assert.Equal(new DateOnly(2025, 1, 1), pdtic.VigenciaInicio);
        Assert.Equal(new DateOnly(2028, 12, 31), pdtic.VigenciaFim);
        Assert.Equal(new DateTime(2025, 3, 10), demanda_service.Helpers.DateTimeHelper.ToBrasilia(pdtic.AprovadoEm!.Value).Date);
        Assert.Equal(new DateTime(2025, 3, 20), demanda_service.Helpers.DateTimeHelper.ToBrasilia(pdtic.PublicadoEm!.Value).Date);
        Assert.Null(pdtic.EnviadoEm);
        Assert.False(pdtic.PodeEditar);
        Assert.Null(pdtic.Revisao);

        // A deliberação aprovada, com o ato e o PDF; na fila, entre as decididas
        var deliberacao = pdtic.Deliberacao!;
        Assert.Equal("aprovado", deliberacao.Situacao);
        Assert.Equal(("Resolução", "5/2025", new DateOnly(2025, 3, 10)), (deliberacao.AtoTipo, deliberacao.AtoNumero, deliberacao.AtoData));
        Assert.Equal("00060-00001234/2025-11", deliberacao.Sei);
        Assert.Equal("PDTIC SES 2.0", deliberacao.Titulo);
        Assert.Equal((pdtic.Id, 1), (deliberacao.Documento!.PdticId, deliberacao.Documento.Numero));
        Assert.Single((await Deliberacoes.ListarAsync(new PeDeliberacoesConsulta { Situacao = "aprovado", ObjetoTipo = "pdtic" })).Items);

        // O PDF é a versão 1, publicada, com as páginas contadas; passa a ser do PDTIC
        var versao = Assert.Single(await Documentos.VersoesAsync(pdtic.Id, await ContextoDe(UserConsultaSes)));
        Assert.Equal((1, "publicada", 3), (versao.Numero, versao.Situacao, versao.Paginas));
        var arquivo = await Documentos.ArquivoDaVersaoAsync(pdtic.Id, 1, await ContextoDe(UserPeSgdi));
        Assert.Equal("PDTIC_SES_v2.0_1.pdf", arquivo.NomeArquivo);
        var arquivoId = Context.PeDocVersoes.AsNoTracking().Single(v => v.PdticId == pdtic.Id).ArquivoId;
        var noBanco = Context.PeArquivos.AsNoTracking().Single(a => a.Id == arquivoId);
        Assert.Equal(("pdtic", (long?)pdtic.Id), (noBanco.DonoTipo, noBanco.DonoId));

        // As seções: a vigência (1.1) e a publicação (3.13)
        var dono = PeDono.DoPdtic(pdtic.Id);
        var abrangencia = (await Registros.ListarAsync(dono, "abrangencia", await Orgao())).Registros.Single();
        Assert.Equal("2025-01-01", Valor(abrangencia, "vigencia_inicio"));
        var publicacao = (await Registros.ListarAsync(dono, "publicacao", await Orgao())).Registros.Single();
        Assert.Equal(("2025-03-20", Endereco), (Valor(publicacao, "data"), Valor(publicacao, "endereco")));

        // Um PDTIC por vez: o atual é ele; abrir ou registrar outro, não
        Assert.Equal(pdtic.Id, (await Pdtics.AtualAsync(null, await Orgao()))!.Id);
        Assert.Equal(Codigo(ErrorCode.PePdticJaExiste), await ErroAsync(AbrirSesAsync));
        Assert.Equal(Codigo(ErrorCode.PePdticJaExiste), await ErroAsync(async () => await RegistrarAsync(await CorpoAsync())));
    }

    [Fact]
    public async Task Registrar_AsEtapas1a3FicamExternas_MenosMetasAcoesERiscos()
    {
        var pdtic = await RegistrarAsync(await CorpoAsync());
        var situacao = await SituacaoAsync(pdtic.Id);
        PePassoSituacaoResponse De(string chave) => situacao.Passos.Single(p => p.Chave == chave);

        foreach (var chave in new[] { "preparacao.abrangencia", "diagnostico.necessidades-tic", "planejamento.documento",
                     "planejamento.aprovacao-sgtic", "planejamento.deliberacao-cgtic", "planejamento.publicacao" })
        {
            Assert.Equal(PeDominios.SituacaoPasso.Externo, De(chave).Situacao);
            Assert.Equal("Feito fora do sistema", De(chave).Motivo);
            Assert.False(De(chave).PodeEditar);
        }
        Assert.Equal(PeDominios.SituacaoPasso.Pendente, De("planejamento.metas-acoes").Situacao);
        Assert.True(De("planejamento.metas-acoes").PodeEditar);
        Assert.Equal(PeDominios.SituacaoPasso.Pendente, De("plano-acompanhamento.quem-acompanha").Situacao);
        // O próximo passo nunca é externo: as metas e ações
        Assert.Equal(De("planejamento.metas-acoes").Numero, situacao.ProximoPasso);

        // As metas se gravam sem a necessidade (a ligação com a seção externa fica opcional)
        var meta = await IncluirNoPdticAsync(pdtic.Id, "metas", new { descricao = "Implantar o prontuário.", indicador = "Unidades", valor = "100%", prazo = "2027-12-31" });
        Assert.Empty(meta.Vinculos["necessidades"]);
        var campo = (await Registros.ListarAsync(PeDono.DoPdtic(pdtic.Id), "metas", await Orgao())).Secao.Campos.Single(c => c.Chave == "necessidades");
        Assert.False(campo.Obrigatorio);

        // As seções externas não se gravam
        var ex = await Assert.ThrowsAsync<ApiException>(() => IncluirNoPdticAsync(pdtic.Id, "necessidades",
            new { descricao = "Outra necessidade.", tipo = "servico", prioridade_simples = "alta" }));
        Assert.Equal((int)ErrorCode.PePdticFechado, ex.Error.Code);
        Assert.Equal("Este passo foi feito fora do sistema: o PDTIC foi aprovado fora dele, e aqui a equipe só acompanha.", ex.Error.Message);
        Assert.Equal(Codigo(ErrorCode.PePdticFechado), await ErroAsync(async () => await Documentos.GerarPdfAsync(pdtic.Id, await Orgao())));
        Assert.False((await Aprovacao.EnvioAsync(pdtic.Id, await Orgao())).PodeEnviar);
    }

    [Fact]
    public async Task Registrar_AprovadoPorOutraInstancia_VaiParaAAprovacaoDoSgtic()
    {
        var corpo = await CorpoAsync("outra");
        corpo.AprovacaoAtoTipo = "PORTARIA";
        var pdtic = await RegistrarAsync(corpo);

        Assert.Null(pdtic.Deliberacao);
        Assert.Empty(Context.PeDeliberacoes.Where(d => d.ObjetoTipo == "pdtic"));
        var aprovacao = (await Registros.ListarAsync(PeDono.DoPdtic(pdtic.Id), "aprovacao_sgtic", await Orgao())).Registros.Single();
        Assert.Equal("aprovado", Valor(aprovacao, "decisao"));
        Assert.Equal("2025-03-10", Valor(aprovacao, "data"));
        Assert.Equal("outra", Valor(aprovacao, "instancia"));
        Assert.Equal("portaria", Valor(aprovacao, "ato_tipo"));
        Assert.Equal("5/2025", Valor(aprovacao, "ato_numero"));
        Assert.False(aprovacao.Dados.ContainsKey("observacao"));

        // O tipo que não está na lista vira "outro", com o texto na observação
        Context.PePdtics.Single(p => p.Id == pdtic.Id).Situacao = PeDominios.SituacaoPdtic.Encerrado;
        Context.PePdtics.Single(p => p.Id == pdtic.Id).EncerradoEm = DateTime.UtcNow;
        Context.SaveChanges();
        var outro = await CorpoAsync("outra");
        outro.Versao = "3.0";
        outro.AprovacaoAtoTipo = "Ofício circular";
        outro.AprovacaoAtoNumero = null;
        var segundo = await RegistrarAsync(outro);
        var registro = (await Registros.ListarAsync(PeDono.DoPdtic(segundo.Id), "aprovacao_sgtic", await Orgao())).Registros.Single();
        Assert.Equal("outro", Valor(registro, "ato_tipo"));
        Assert.Equal("Tipo do ato: Ofício circular.", Valor(registro, "observacao"));
        Assert.Equal(pdtic.Id, segundo.AnteriorId);
    }

    [Fact]
    public async Task Registrar_Validacoes_PorCampo()
    {
        var (codigo, campos) = await ValidacaoAsync(() => RegistrarAsync(new PeRegistroExternoDTO()));
        Assert.Equal(Codigo(ErrorCode.PeRegistroExternoInvalido), codigo);
        Assert.Equal(new[] { "AprovacaoData", "AprovacaoInstancia", "ArquivoId", "PublicacaoData", "PublicacaoEndereco", "Versao", "VigenciaFim", "VigenciaInicio" },
            campos.Keys.OrderBy(k => k, StringComparer.Ordinal));

        var corpo = await CorpoAsync();
        corpo.Versao = "v2";
        corpo.VigenciaFim = "2024-12-31";
        corpo.AprovacaoData = DateTime.UtcNow.AddDays(5).ToString("yyyy-MM-dd");
        corpo.AprovacaoSei = "123";
        corpo.AprovacaoAtoNumero = null;
        (_, campos) = await ValidacaoAsync(() => RegistrarAsync(corpo));
        Assert.Equal("Informe a versão no formato 1.0 ou 2.1.", campos["Versao"]);
        Assert.Equal("O fim da vigência não pode ser antes do início.", campos["VigenciaFim"]);
        Assert.Equal("A data não pode ser depois de hoje.", campos["AprovacaoData"]);
        Assert.Equal("Informe o processo SEI no formato 00000-00000000/0000-00.", campos["AprovacaoSei"]);
        Assert.Equal("Informe o número do ato do CGTIC que aprovou o PDTIC.", campos["AprovacaoAtoNumero"]);

        corpo = await CorpoAsync();
        corpo.PublicacaoData = "2025-03-01";
        (_, campos) = await ValidacaoAsync(() => RegistrarAsync(corpo));
        Assert.Equal("A publicação não pode ser antes da aprovação.", Assert.Single(campos).Value);

        // O arquivo: PDF, enviado por quem registra e ainda sem dono
        var png = await EnviarArquivoAsync(UserOrgaoSes, "pdtic.png", Png());
        corpo = await CorpoAsync();
        corpo.ArquivoId = png.Id;
        (_, campos) = await ValidacaoAsync(() => RegistrarAsync(corpo));
        Assert.Equal("Envie o PDTIC aprovado em PDF.", campos["ArquivoId"]);
        corpo = await CorpoAsync(quemEnvia: UserAdminGeral);
        (_, campos) = await ValidacaoAsync(() => RegistrarAsync(corpo));
        Assert.Equal("Este arquivo não pode ser usado aqui. Envie o PDF de novo.", campos["ArquivoId"]);

        Assert.Empty(Context.PePdtics.Where(p => p.OrgaoId == OrgaoSes.Id));
    }

    [Fact]
    public async Task Registrar_QuemRegistra_EAVersaoQueOOrgaoJaTem()
    {
        foreach (var user in new[] { UserConsultaSes, UserPeSgdi, UserPeCgtic, UserPeAdmin })
            Assert.Equal(Codigo(ErrorCode.PeSemPermissao), await ErroAsync(async () => await RegistrarAsync(await CorpoAsync(), user)));
        var outroOrgao = await CorpoAsync();
        outroOrgao.OrgaoId = OrgaoSes.Id;
        Assert.Equal(Codigo(ErrorCode.PeSemPermissao), await ErroAsync(() => RegistrarAsync(outroOrgao, UserOrgaoSeec)));

        // Admin geral: precisa dizer o órgão
        var doAdmin = await CorpoAsync(quemEnvia: UserAdminGeral);
        Assert.Equal(Codigo(ErrorCode.PeOrgaoObrigatorio), await ErroAsync(() => RegistrarAsync(doAdmin, UserAdminGeral)));

        // O órgão já teve um PDTIC 2.0 (encerrado): a mesma versão é recusada
        var antigo = await AbrirSesAsync();
        Context.PePdtics.Single(p => p.Id == antigo.Id).Versao = "2.0";
        Context.PePdtics.Single(p => p.Id == antigo.Id).Situacao = PeDominios.SituacaoPdtic.Encerrado;
        Context.PePdtics.Single(p => p.Id == antigo.Id).EncerradoEm = DateTime.UtcNow;
        Context.SaveChanges();
        doAdmin.OrgaoId = OrgaoSes.Id;
        Assert.Equal(Codigo(ErrorCode.PeVersaoPdticDuplicada), await ErroAsync(() => RegistrarAsync(doAdmin, UserAdminGeral)));
        doAdmin.Versao = "2.1";
        Assert.Equal(PeDominios.SituacaoPdtic.Publicado, (await RegistrarAsync(doAdmin, UserAdminGeral)).Situacao);
    }

    [Fact]
    public void Paginas_DoPdfEnviado()
    {
        Assert.Equal(3, PeDocumentoPdf.ContarPaginasDoArquivo(PdfComPaginas(3)));

        // PDF 1.5 com a árvore de páginas dentro de um fluxo de objetos comprimido
        var objetos = Encoding.Latin1.GetBytes("2 0 << /Type /Pages /Kids [3 0 R] /Count 7 >>");
        byte[] comprimido;
        using (var saida = new MemoryStream())
        {
            using (var zlib = new ZLibStream(saida, CompressionLevel.Optimal)) zlib.Write(objetos);
            comprimido = saida.ToArray();
        }
        var pdf = Encoding.Latin1.GetBytes($"%PDF-1.5\n1 0 obj\n<< /Type /ObjStm /N 1 /First 4 /Filter /FlateDecode /Length {comprimido.Length} >>\nstream\n")
            .Concat(comprimido)
            .Concat(Encoding.Latin1.GetBytes("\nendstream\nendobj\n%%EOF\n"))
            .ToArray();
        Assert.Equal(7, PeDocumentoPdf.ContarPaginasDoArquivo(pdf));

        // Sem árvore de páginas que se leia: no mínimo 1
        Assert.Equal(1, PeDocumentoPdf.ContarPaginasDoArquivo(Pdf()));
    }
}
