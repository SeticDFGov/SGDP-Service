using System.Text.Json.Nodes;
using api.Planejamento;
using service.Planejamento;
using Xunit;

namespace test.planejamento;

/// <summary>
/// Os marcadores de aprovação e de publicação (E7, rodada A) e a folha de rosto: a aprovação do
/// SGTIC (só com "aprovado"), a do CGTIC (a deliberação aprovada) e a publicação, cada uma com
/// valor no seu momento; e o texto do PDF com os marcadores sem valor limpos (pedido da
/// integração da E6): sem "()", sem linha vazia ou só com pontuação, sem rótulo sem valor, sem a
/// frase que dependia de um marcador sem valor ("O PDTIC vale de a ."), com as linhas em branco
/// juntadas. A prévia continua mostrando o marcador (destacado).
/// </summary>
public class PeMarcadoresAprovacaoTest : PeAprovacaoTestBase
{
    /// <summary>O bloco da aprovação e da publicação na folha de rosto (o último texto dela).</summary>
    private static PeDocBlocoResponse BlocoDaAprovacao(PeDocumentoResponse documento) =>
        Cap(documento, "folha_rosto").Blocos.Last(b => b.Tipo == "texto");

    /// <summary>O texto de cada bloco de linha do documento do TipTap (parágrafo e título), com a quebra de linha como "\n".</summary>
    private static List<string> Linhas(JsonNode? documento)
    {
        var saida = new List<string>();
        void Percorrer(JsonNode? no)
        {
            if (no is not JsonObject o) return;
            var tipo = o["type"]?.GetValue<string>();
            if (tipo is "paragraph" or "heading")
            {
                saida.Add(string.Concat((o["content"] as JsonArray ?? new JsonArray()).OfType<JsonObject>()
                    .Select(n => n["type"]?.GetValue<string>() == "hardBreak" ? "\n" : n["text"]?.GetValue<string>() ?? string.Empty)));
                return;
            }
            if (o["content"] is JsonArray filhos) foreach (var f in filhos) Percorrer(f);
        }
        Percorrer(documento);
        return saida;
    }

    private static Dictionary<string, string?> SemValor(params (string Chave, string? Valor)[] valores)
    {
        var mapa = PeDocMarcadores.Fixos.ToDictionary(m => m.Chave, _ => (string?)null);
        mapa["nomes.comite"] = null;
        mapa["nomes.equipe_elaboracao"] = null;
        foreach (var (chave, valor) in valores) mapa[chave] = valor;
        return mapa;
    }

    [Fact]
    public async Task Marcadores_DaAprovacaoEDaPublicacao_TemValorNoSeuMomento()
    {
        var pdtic = await AbrirSesAsync();
        var antes = BlocoDaAprovacao(await DocumentoAsync(pdtic.Id));
        Assert.Equal(new[] { "aprovacao.sgtic.data", "aprovacao.sgtic.ato", "aprovacao.cgtic.data", "aprovacao.cgtic.ato", "publicacao.data", "publicacao.endereco" },
            antes.MarcadoresSemValor);
        // Na prévia, o marcador sem valor continua escrito
        Assert.Contains("{aprovacao.cgtic.data}", TextoDe(antes.TextoResolvido));

        await PreencherElaboracaoAsync(pdtic.Id, sgtic: false);
        await AprovacaoSgticAsync(pdtic.Id, "devolvido");
        Assert.Contains("aprovacao.sgtic.data", BlocoDaAprovacao(await DocumentoAsync(pdtic.Id)).MarcadoresSemValor);

        await AprovacaoSgticAsync(pdtic.Id);
        var sgtic = TextoDe(BlocoDaAprovacao(await DocumentoAsync(pdtic.Id)).TextoResolvido);
        Assert.Contains("Aprovação pelo comitê interno de TIC (SGTIC): 01/09/2026 (Ata de reunião nº 3/2026)", sgtic);

        await EnviarAsync(pdtic.Id);
        await AprovarNoCgticAsync(pdtic.Id);
        await PublicarAsync(pdtic.Id);
        var publicado = BlocoDaAprovacao(await DocumentoAsync(pdtic.Id));
        Assert.Empty(publicado.MarcadoresSemValor);
        var texto = TextoDe(publicado.TextoResolvido);
        Assert.Contains("(CGTIC): 10/09/2026 (Resolução nº 12/2026)", texto);
        Assert.Contains("Publicação: 15/09/2026 (" + Endereco + ")", texto);
    }

    [Fact]
    public void Ato_TipoENumero()
    {
        Assert.Equal("Resolução nº 12/2026", PeDocumentoService.Ato("Resolução", "12/2026"));
        Assert.Equal("nº 12/2026", PeDocumentoService.Ato(null, "12/2026"));
        Assert.Equal("Ata", PeDocumentoService.Ato(" Ata ", " "));
        Assert.Null(PeDocumentoService.Ato(null, null));
    }

    [Fact]
    public void Rodape_DaMinutaEDaVersaoEnviada_SemMudarASigla()
    {
        Assert.Equal("SES · PDTIC versão 1.0 · minuta nº 3", PeDocumentoService.Rodape("SES", "1.0", 3, "minuta"));
        Assert.Equal("SES · PDTIC versão 1.0 · nº 4, enviada ao CGTIC", PeDocumentoService.Rodape("SES", "1.0", 4, "enviada"));
        Assert.Equal("SES · PDTIC versão 1.1 · nº 2, publicada", PeDocumentoService.Rodape("SES", "1.1", 2, "publicada"));
    }

    [Fact]
    public void Pdf_FolhaDeRostoComODicionarioVazio_SemRotuloSemValorNemPontuacaoSobrando()
    {
        var folha = PeDocTextoSimples.ParaTipTap(new[]
        {
            "**{orgao.nome}**",
            "{nomes.autoridade_cargo}: {nomes.autoridade}",
            "",
            "**Comitê interno de TIC**",
            "{nomes.comite}",
            "",
            "**Unidade de TIC**",
            "{nomes.unidade_tic}",
            "",
            "**Equipe de elaboração**",
            "{nomes.equipe}"
        });
        var valores = SemValor(("orgao.nome", "Secretaria de Estado de Saúde"));

        Assert.Equal(new[] { "Secretaria de Estado de Saúde" }, Linhas(PeDocMarcadores.ResolverParaPdf(folha, valores)));

        // Com o comitê e o cargo (sem o nome da autoridade): o cargo sozinho é rótulo sem valor e sai
        valores["nomes.comite"] = "Subcomitê Gestor de TIC";
        valores["nomes.autoridade_cargo"] = "Secretária de Estado de Saúde";
        Assert.Equal(new[] { "Secretaria de Estado de Saúde", "", "Comitê interno de TIC", "Subcomitê Gestor de TIC" },
            Linhas(PeDocMarcadores.ResolverParaPdf(folha, valores)));

        // A prévia não limpa: o marcador fica escrito
        Assert.Contains("{nomes.unidade_tic}", Linhas(PeDocMarcadores.Resolver(folha, valores, emBranco: false)));
    }

    [Fact]
    public void Pdf_AprovacoesSemValor_SaemInteiras_EComValor_SemParentesesVazios()
    {
        var bloco = PeDocTextoSimples.ParaTipTap(new[]
        {
            "**Aprovação e publicação**",
            "**Aprovação pelo comitê interno de TIC (SGTIC):** {aprovacao.sgtic.data} ({aprovacao.sgtic.ato})",
            "**Aprovação pelo Comitê Gestor de TIC do Distrito Federal (CGTIC):** {aprovacao.cgtic.data} ({aprovacao.cgtic.ato})",
            "**Publicação:** {publicacao.data} ({publicacao.endereco})"
        });

        var vazio = PeDocMarcadores.ResolverParaPdf(bloco, SemValor());
        Assert.Empty(Linhas(vazio));
        Assert.False(PeTextoRicoPdf.TemConteudo(vazio));

        var soSgtic = PeDocMarcadores.ResolverParaPdf(bloco, SemValor(("aprovacao.sgtic.data", "01/09/2026")));
        Assert.Equal(new[] { "Aprovação e publicação", "Aprovação pelo comitê interno de TIC (SGTIC): 01/09/2026" }, Linhas(soSgtic));

        var tudo = PeDocMarcadores.ResolverParaPdf(bloco, SemValor(
            ("aprovacao.sgtic.data", "01/09/2026"), ("aprovacao.sgtic.ato", "Ata nº 3"),
            ("aprovacao.cgtic.data", "10/09/2026"), ("aprovacao.cgtic.ato", "Resolução nº 12/2026"),
            ("publicacao.data", "15/09/2026"), ("publicacao.endereco", Endereco)));
        Assert.Equal(new[]
        {
            "Aprovação e publicação",
            "Aprovação pelo comitê interno de TIC (SGTIC): 01/09/2026 (Ata nº 3)",
            "Aprovação pelo Comitê Gestor de TIC do Distrito Federal (CGTIC): 10/09/2026 (Resolução nº 12/2026)",
            "Publicação: 15/09/2026 (" + Endereco + ")"
        }, Linhas(tudo));
    }

    [Fact]
    public void Pdf_TextoCorrido_ParentesesVaziosSaemComOEspaco_ELinhaCurtaSemValorSai()
    {
        var texto = PeDocTextoSimples.ParaTipTap(new[]
        {
            "Vigência: de {vigencia.inicio} a {vigencia.fim}",
            "Versão {pdtic.versao}",
            "O PDTIC foi elaborado pela equipe de elaboração ({nomes.equipe}), com o apoio das áreas do órgão.",
            "A equipe de acompanhamento ({nomes.equipe_acompanhamento}) apresenta os resultados ao comitê interno de TIC ({nomes.comite}).",
            "- {nomes.comite}",
            "- Item que fica",
            "| Comitê | Unidade |",
            "| {nomes.comite} ( ) | Unidade fixa |"
        });
        var saida = PeDocMarcadores.ResolverParaPdf(texto, SemValor(("pdtic.versao", "1.0")));
        Assert.Equal(new[]
        {
            "Versão 1.0",
            "O PDTIC foi elaborado pela equipe de elaboração, com o apoio das áreas do órgão.",
            "A equipe de acompanhamento apresenta os resultados ao comitê interno de TIC.",
            "Item que fica",
            "Comitê", "Unidade",
            "", "Unidade fixa"
        }, Linhas(saida));
        // A lista perdeu o item vazio; a tabela ficou com a célula (vazia)
        var lista = saida!["content"]!.AsArray().Single(n => n!["type"]!.GetValue<string>() == "bulletList");
        Assert.Single(lista!["content"]!.AsArray());
    }

    [Fact]
    public void Pdf_FraseComMarcadorSemValorForaDeParenteses_SaiInteira_EOrestoDoParagrafoFica()
    {
        var texto = PeDocTextoSimples.ParaTipTap(new[]
        {
            "O PDTIC vale de {vigencia.inicio} a {vigencia.fim}. A abrangência e a periodicidade da revisão estão no quadro abaixo.",
            "A TIC é parte essencial. Este plano organiza as prioridades para a vigência de {vigencia.inicio} a {vigencia.fim} e mostra como cada ação contribui.",
            "Conforme o art. 4º do Decreto nº 48.900/2026, o plano vale até {vigencia.fim}. Texto que fica.",
            "A equipe ({nomes.equipe}) apresenta o plano ao comitê {nomes.comite}! E segue o acompanhamento."
        });

        Assert.Equal(new[]
        {
            "A abrangência e a periodicidade da revisão estão no quadro abaixo.",
            "A TIC é parte essencial.",
            "Texto que fica.",
            "E segue o acompanhamento."
        }, Linhas(PeDocMarcadores.ResolverParaPdf(texto, SemValor())));

        // Com os valores, nenhuma frase sai ("art. 4º" e "48.900" não terminam frase)
        var cheio = Linhas(PeDocMarcadores.ResolverParaPdf(texto, SemValor(
            ("vigencia.inicio", "01/01/2026"), ("vigencia.fim", "31/12/2029"), ("nomes.equipe", "Equipe do PDTIC"), ("nomes.comite", "SGTIC"))));
        Assert.Equal("O PDTIC vale de 01/01/2026 a 31/12/2029. A abrangência e a periodicidade da revisão estão no quadro abaixo.", cheio[0]);
        Assert.Equal("Conforme o art. 4º do Decreto nº 48.900/2026, o plano vale até 31/12/2029. Texto que fica.", cheio[2]);
        Assert.Equal("A equipe (Equipe do PDTIC) apresenta o plano ao comitê SGTIC! E segue o acompanhamento.", cheio[3]);
    }

    [Fact]
    public void Pdf_LinhasDeUmParagrafoComQuebra_SaiSoALinhaVazia()
    {
        var documento = JsonNode.Parse("""
            {"type":"doc","content":[{"type":"paragraph","content":[
              {"type":"text","text":"Comitê: {nomes.comite}"},{"type":"hardBreak"},{"type":"text","text":"Texto que fica."}]}]}
            """);
        Assert.Equal(new[] { "Texto que fica." }, Linhas(PeDocMarcadores.ResolverParaPdf(documento, SemValor())));

        // Sem marcador nada muda, nem as linhas em branco
        var fixo = PeDocTextoSimples.ParaTipTap(new[] { "Um.", "", "", "Dois." });
        Assert.Equal(new[] { "Um.", "", "", "Dois." }, Linhas(PeDocMarcadores.ResolverParaPdf(fixo, SemValor())));
    }

    [Fact]
    public async Task Pdf_DoPdticSemDicionario_SaiSemSobras()
    {
        var pdtic = await AbrirSesAsync();
        var documento = await DocumentoAsync(pdtic.Id);
        var marcadores = PeDocumentoService.Marcadores(PdticNoBanco(pdtic.Id), OrgaoSes, new Dictionary<string, string?>());

        foreach (var bloco in documento.Capitulos.Where(c => !c.Oculto).SelectMany(c => c.Blocos).Where(b => b.Tipo == "texto"))
            foreach (var linha in Linhas(PeDocumentoPdf.TextoDoPdf(bloco, marcadores)))
            {
                Assert.DoesNotContain("()", linha);
                Assert.DoesNotContain("( )", linha);
                // A vigência em branco no meio da frase ("O PDTIC vale de a .")
                Assert.DoesNotContain("vale de a", linha);
                Assert.DoesNotContain("vigência de a", linha);
                Assert.False(linha.Length > 0 && !linha.Any(char.IsLetterOrDigit), $"linha só com pontuação: \"{linha}\"");
                Assert.False(linha.TrimEnd().EndsWith(':') && linha.Length < 60 && linha.Contains("Aprovação"), $"rótulo sem valor: \"{linha}\"");
            }
        // O PDF sai
        Assert.Equal(1, (await Documentos.GerarPdfAsync(pdtic.Id, await Orgao())).Numero);
    }
}
