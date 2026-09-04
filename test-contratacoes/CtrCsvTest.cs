using api.Contratacoes;
using Models.Contratacoes;
using service.Contratacoes;
using Xunit;

namespace test.contratacoes;

/// <summary>
/// Parser da planilha (bytes Windows-1252 como o arquivo real, e UTF-8 com BOM
/// como o nosso export) e escrita do CSV.
/// </summary>
public class CtrCsvTest : CtrTestBase
{
    private const string Cabecalho =
        "Processo;Orgão;Sigla;Complemento / Área;Objeto;Categoria do Objeto;Chegada da analise - SGDI;"
        + "Chegada da analise- SUBGD;Chegada da analise- UGTIC;Data de Retorno ao Gab SGDI;"
        + "Data de Retorno ao Órgão Comunicante;Observação";

    private const string Titulo = "Analises de contratações;;;;;;;;;;;";

    [Fact]
    public void Ler_IgnoraTituloEUsaOCabecalhoDaSegundaLinha()
    {
        var csv = Titulo + "\r\n" + Cabecalho + "\r\n"
            + "04044-00002545/2024-62;Secretaria de Estado de Economia;SEEC;;Aquisição de switches;"
            + "Infraestrutura de Rede;22/05/2026;25/05/2026;26/05/2026;;;\r\n";

        var linhas = CtrCsv.Ler(Bytes1252(csv));

        var linha = Assert.Single(linhas);
        Assert.Null(linha.Erro);
        Assert.Equal(3, linha.Linha); // número físico da linha no arquivo
        Assert.Equal("04044-00002545/2024-62", linha.Dados!.NumeroProcesso);
        Assert.Equal("Secretaria de Estado de Economia", linha.Dados.OrgaoNome);
        Assert.Equal(new DateOnly(2026, 5, 22), linha.Dados.ChegadaSgdi);
        Assert.Equal(new DateOnly(2026, 5, 26), linha.Dados.ChegadaUgtic);
        Assert.False(linha.Dados.UgticNaoSeAplica);
    }

    [Fact]
    public void Ler_PreservaAcentuacaoDoWindows1252()
    {
        var csv = Titulo + "\r\n" + Cabecalho + "\r\n"
            + "00193-00000779/2026-54;Fundação de Apoio à Pesquisa do Distrito Federal;FAPDF;"
            + "Gerência de Informática;Solução de Voz sobre IP (VoIP);Telefonia / VoIP;09/06/2026;;;;;\r\n";

        var linha = Assert.Single(CtrCsv.Ler(Bytes1252(csv)));

        Assert.Equal("Fundação de Apoio à Pesquisa do Distrito Federal", linha.Dados!.OrgaoNome);
        Assert.Equal("Gerência de Informática", linha.Dados.ComplementoArea);
        Assert.Equal("Solução de Voz sobre IP (VoIP)", linha.Dados.Objeto);
        Assert.Equal(CtrDominios.CategoriaObjeto.TelefoniaVoip, linha.Dados.CategoriaObjeto);
    }

    [Fact]
    public void Ler_AspasComEscapeEPontoEVirgulaDentro()
    {
        var csv = Titulo + "\r\n" + Cabecalho + "\r\n"
            + "00080-00224827/2024-02;Secretaria de Estado de Educação;SEEDF;;"
            + "\"Serviços de desenvolvimento sob o modelo de \"\"fábrica de software\"\"; com medição por PF\";"
            + "Desenvolvimento / Fábrica de Software;13/05/2026;13/05/2026;15/05/2026;;;\r\n";

        var linha = Assert.Single(CtrCsv.Ler(Bytes1252(csv)));

        Assert.Null(linha.Erro);
        Assert.Equal("Serviços de desenvolvimento sob o modelo de \"fábrica de software\"; com medição por PF",
            linha.Dados!.Objeto);
    }

    [Fact]
    public void Ler_QuebraDeLinhaDentroDeAspas_NaoParteORegistro()
    {
        var csv = Titulo + "\r\n" + Cabecalho + "\r\n"
            + "00080-00224827/2024-02;Secretaria;SEEDF;;\"Objeto em\nduas linhas\";"
            + "Sistemas de Gestão;13/05/2026;;;;;\r\n"
            + "00080-00224828/2024-03;Secretaria;SEEDF;;Outro objeto;Sistemas de Gestão;14/05/2026;;;;;\r\n";

        var linhas = CtrCsv.Ler(Bytes1252(csv));

        Assert.Equal(2, linhas.Count);
        Assert.Contains("duas linhas", linhas[0].Dados!.Objeto);
        Assert.Equal("00080-00224828/2024-03", linhas[1].Dados!.NumeroProcesso);
    }

    [Fact]
    public void Ler_TracoNaUgticMarcaNaoSeAplica()
    {
        var csv = Titulo + "\r\n" + Cabecalho + "\r\n"
            + "00220-00008043/2026-13;Secretaria de Esporte e Lazer;SELDF;Gerência;Infraestrutura física;"
            + "Infraestrutura de Rede;13/07/2026;13/07/2026;-;16/07/2026;;\r\n";

        var linha = Assert.Single(CtrCsv.Ler(Bytes1252(csv)));

        Assert.True(linha.Dados!.UgticNaoSeAplica);
        Assert.Null(linha.Dados.ChegadaUgtic);
        Assert.Equal(new DateOnly(2026, 7, 16), linha.Dados.RetornoGabSgdi);
    }

    [Fact]
    public void Ler_CategoriaVaziaViraSemObjeto()
    {
        var csv = Titulo + "\r\n" + Cabecalho + "\r\n"
            + "04039-00000931/2025-05;Secretaria do Meio Ambiente;SEMA;Unidade;Renovação do contrato;;;;;;;\r\n";

        var linha = Assert.Single(CtrCsv.Ler(Bytes1252(csv)));

        Assert.Equal(CtrDominios.CategoriaObjeto.SemObjeto, linha.Dados!.CategoriaObjeto);
    }

    [Theory]
    [InlineData("infraestrutura de rede")]
    [InlineData("INFRAESTRUTURA DE REDE")]
    [InlineData("Infraestrutura  de   Rede")]
    [InlineData(" Infraestrutura de Rede ")]
    public void Ler_CategoriaEhComparadaPorNormalizacao(string categoria)
    {
        var csv = Titulo + "\r\n" + Cabecalho + "\r\n"
            + $"04044-00002545/2024-62;Economia;SEEC;;Switches;{categoria};22/05/2026;;;;;\r\n";

        var linha = Assert.Single(CtrCsv.Ler(Bytes1252(csv)));

        Assert.Equal(CtrDominios.CategoriaObjeto.InfraestruturaRede, linha.Dados!.CategoriaObjeto);
    }

    [Fact]
    public void Ler_CategoriaDesconhecida_RejeitaALinha()
    {
        var csv = Titulo + "\r\n" + Cabecalho + "\r\n"
            + "04044-00002545/2024-62;Economia;SEEC;;Switches;Computadores de mesa;22/05/2026;;;;;\r\n";

        var linha = Assert.Single(CtrCsv.Ler(Bytes1252(csv)));

        Assert.Null(linha.Dados);
        Assert.Contains("categoria desconhecida", linha.Erro);
    }

    [Fact]
    public void Ler_DataInvalidaOuNumeroInvalido_RejeitaALinha()
    {
        var csv = Titulo + "\r\n" + Cabecalho + "\r\n"
            + "04044-00002545/2024-62;Economia;SEEC;;Switches;Infraestrutura de Rede;32/13/2026;;;;;\r\n"
            + "1234;Economia;SEEC;;Switches;Infraestrutura de Rede;22/05/2026;;;;;\r\n"
            + ";Economia;SEEC;;Switches;Infraestrutura de Rede;22/05/2026;;;;;\r\n";

        var linhas = CtrCsv.Ler(Bytes1252(csv));

        Assert.Equal(3, linhas.Count);
        Assert.Contains("data inválida", linhas[0].Erro);
        Assert.Contains("número de processo inválido", linhas[1].Erro);
        Assert.Contains("ausente", linhas[2].Erro);
    }

    [Fact]
    public void Ler_NumeroRepetido_RejeitaASegundaOcorrencia()
    {
        var csv = Titulo + "\r\n" + Cabecalho + "\r\n"
            + "04044-00002545/2024-62;Economia;SEEC;;Switches;Infraestrutura de Rede;22/05/2026;;;;;\r\n"
            + "04044-00002545/2024-62;Economia;SEEC;;Switches (repetido);Infraestrutura de Rede;23/05/2026;;;;;\r\n";

        var linhas = CtrCsv.Ler(Bytes1252(csv));

        Assert.Null(linhas[0].Erro);
        Assert.Contains("duplicado no arquivo, linha 3", linhas[1].Erro);
    }

    [Fact]
    public void Ler_RestituicaoNaObservacaoComAnoCompleto()
    {
        var csv = Titulo + "\r\n" + Cabecalho + "\r\n"
            + "00113-00033274/2026-64;Departamento de Estradas e Rodagem;DER;Gerência;Backup e restore;"
            + "Infraestrutura de Rede;01/09/2026;01/09/2026;;;;"
            + "Processo Restituído ao DER em 03/09/2026, haja vista que o processo encaminhado não foi o de contratação.\r\n";

        var linha = Assert.Single(CtrCsv.Ler(Bytes1252(csv)));

        Assert.True(linha.Dados!.Restituido);
        Assert.Equal(new DateOnly(2026, 9, 3), linha.Dados.RestituidoEm);
        Assert.Contains("Restituído ao DER", linha.Dados.RestituidoMotivo);
        // A observação continua íntegra
        Assert.Contains("não foi o de contratação", linha.Dados.Observacao);
    }

    [Fact]
    public void Ler_RestituicaoNaObservacaoSemAno_CompletaComOAnoDaChegada()
    {
        var csv = Titulo + "\r\n" + Cabecalho + "\r\n"
            + "04039-00000663/2026-02;Secretaria do Meio Ambiente;SEMA;Diretoria;Videomonitoramento;"
            + "Captação Audiovisual;31/08/2026;01/09/2026;;;;"
            + "Processo restituído ao órgão em 03/09, informando sobre a necessidade de observância à IN 01/2026\r\n";

        var linha = Assert.Single(CtrCsv.Ler(Bytes1252(csv)));

        Assert.True(linha.Dados!.Restituido);
        Assert.Equal(new DateOnly(2026, 9, 3), linha.Dados.RestituidoEm);
    }

    [Fact]
    public void Ler_RestituicaoSemDataNenhuma_RejeitaALinha()
    {
        var csv = Titulo + "\r\n" + Cabecalho + "\r\n"
            + "04039-00000663/2026-02;Meio Ambiente;SEMA;;Videomonitoramento;Captação Audiovisual;;;;;;"
            + "Processo restituído ao órgão, sem data informada\r\n";

        var linha = Assert.Single(CtrCsv.Ler(Bytes1252(csv)));

        Assert.Null(linha.Dados);
        Assert.Equal("restituição sem data", linha.Erro);
    }

    [Fact]
    public void Ler_RestituicaoSemDataNaObservacao_UsaAChegadaSgdi()
    {
        var csv = Titulo + "\r\n" + Cabecalho + "\r\n"
            + "04039-00000663/2026-02;Meio Ambiente;SEMA;;Videomonitoramento;Captação Audiovisual;"
            + "31/08/2026;;;;;Processo restituído ao órgão para adequação\r\n";

        var linha = Assert.Single(CtrCsv.Ler(Bytes1252(csv)));

        Assert.True(linha.Dados!.Restituido);
        Assert.Equal(new DateOnly(2026, 8, 31), linha.Dados.RestituidoEm);
    }

    [Fact]
    public void Ler_Utf8ComBomEAsQuinzeColunas()
    {
        var csv = string.Join(";", CtrCsv.Cabecalho) + "\r\n"
            + "04039-00000663/2026-02;Meio Ambiente;SEMA;Diretoria;Videomonitoramento;Captação Audiovisual;"
            + "31/08/2026;01/09/2026;-;;;Observação preservada;Sim;03/09/2026;Motivo em coluna própria\r\n";

        var linha = Assert.Single(CtrCsv.Ler(BytesUtf8ComBom(csv)));

        Assert.Null(linha.Erro);
        Assert.Equal("Captação Audiovisual", linha.Dados!.CategoriaObjeto);
        Assert.True(linha.Dados.UgticNaoSeAplica);
        Assert.True(linha.Dados.Restituido);
        Assert.Equal(new DateOnly(2026, 9, 3), linha.Dados.RestituidoEm);
        Assert.Equal("Motivo em coluna própria", linha.Dados.RestituidoMotivo);
        Assert.Equal("Observação preservada", linha.Dados.Observacao);
    }

    [Fact]
    public void Ler_ColunaRestituidoNao_NaoInfereDaObservacao()
    {
        var csv = string.Join(";", CtrCsv.Cabecalho) + "\r\n"
            + "04039-00000663/2026-02;Meio Ambiente;SEMA;;Videomonitoramento;Captação Audiovisual;"
            + "31/08/2026;;;;;Não confundir: o processo restituído foi outro;Não;;\r\n";

        var linha = Assert.Single(CtrCsv.Ler(BytesUtf8ComBom(csv)));

        // A coluna explícita manda sobre a inferência pela observação
        Assert.False(linha.Dados!.Restituido);
        Assert.Null(linha.Dados.RestituidoEm);
    }

    [Fact]
    public void Escrever_EscapaSeparadorAspasEQuebraDeLinha()
    {
        var bytes = CtrCsv.Escrever(new List<CtrProcessoResponse>
        {
            new()
            {
                NumeroProcesso = "04044-00002545/2024-62",
                OrgaoNome = "Economia",
                OrgaoSigla = "SEEC",
                Objeto = "Switches; com \"garantia\"\ne suporte",
                CategoriaObjeto = CtrDominios.CategoriaObjeto.InfraestruturaRede,
                ChegadaSgdi = new DateOnly(2026, 5, 22),
                UgticNaoSeAplica = true
            }
        });

        var texto = System.Text.Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);

        Assert.Contains("\"Switches; com \"\"garantia\"\"\ne suporte\"", texto);
        Assert.Contains("22/05/2026", texto);
        Assert.Contains(";Não;;", texto); // não restituído
    }

    // ── Planilha real da equipe ───────────────────────────────────────────────

    [Fact]
    public void Ler_PlanilhaReal_InterpretaAs37LinhasSemRejeicao()
    {
        var linhas = CtrCsv.Ler(PlanilhaReal());

        Assert.Equal(37, linhas.Count);
        Assert.All(linhas, l => Assert.Null(l.Erro));
        Assert.All(linhas, l => Assert.NotNull(l.Dados));

        // Acentuação vinda do Windows-1252
        Assert.Contains(linhas, l => l.Dados!.OrgaoNome == "Serviço de Limpeza Urbana do Distrito Federal");
        Assert.Contains(linhas, l => l.Dados!.CategoriaObjeto == CtrDominios.CategoriaObjeto.SemObjeto);

        // As duas restituições descritas nas observações (uma delas com "restiuído",
        // erro de digitação da planilha, e sem o ano na data)
        var restituidos = linhas.Where(l => l.Dados!.Restituido).ToList();
        Assert.Equal(2, restituidos.Count);
        Assert.All(restituidos, l => Assert.Equal(new DateOnly(2026, 9, 3), l.Dados!.RestituidoEm));
        Assert.All(restituidos, l => Assert.False(string.IsNullOrWhiteSpace(l.Dados!.RestituidoMotivo)));
    }
}
