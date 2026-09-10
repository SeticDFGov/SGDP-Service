using api.Contratacoes;
using Models.Contratacoes;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace service.Contratacoes;

/// <summary>
/// Despacho padrão da SGDI ao TCDF preenchido, em PDF, para juntada ao processo SEI.
/// Reproduz o documento oficial NA LITERALIDADE (textos em <see cref="CtrDespachoTextos"/>),
/// com todas as alternativas do formulário visíveis — as marcadas com "( X )" e as
/// demais com "(   )", como no papel. Nada é persistido.
/// </summary>
public static class CtrDespachoPdf
{
    static CtrDespachoPdf()
    {
        // O Program.cs já define a licença na API; aqui garante o mesmo fora dela
        // (testes e qualquer host que não passe pela inicialização da aplicação).
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public static byte[] Gerar(CtrProcessoResponse processo, CtrManifestacaoResponse manifestacao,
        string local, string nome, string cargo, DateOnly data)
    {
        var incisoI = manifestacao.SituacaoPortfolio == CtrDominios.SituacaoPortfolio.ComunicadaPreviamente;
        var incisoII = manifestacao.SituacaoPortfolio == CtrDominios.SituacaoPortfolio.NaoComunicadaPreviamente;
        var riscos = incisoI && manifestacao.ResultadoAnalise == CtrDominios.ResultadoAnalise.RiscosSignificativos;

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);
                page.DefaultTextStyle(t => t.FontSize(10));

                page.Header().Column(col =>
                {
                    col.Item().AlignCenter().Text(CtrDespachoTextos.Orgao).SemiBold();
                    col.Item().AlignCenter().PaddingTop(4).Text(CtrDespachoTextos.Titulo).SemiBold();
                });

                page.Content().PaddingVertical(8).Column(col =>
                {
                    // Espaçamento justo de propósito: com 8pt o despacho estourava para uma
                    // segunda página e o fecho ficava órfão lá.
                    col.Spacing(4);

                    col.Item().Text($"Processo SEI nº: {processo.NumeroProcesso}");
                    col.Item().Text("Referência: Ofício/Comunicação TCDF nº "
                        + $"{manifestacao.OficioTcdf}, de {manifestacao.DataOficio:dd/MM/yyyy}");
                    col.Item().Text($"Contratação: {Contratacao(processo)}");

                    col.Item().PaddingTop(4).Text(CtrDespachoTextos.Abertura).Justify();
                    col.Item().Text(CtrDespachoTextos.AlcanceSupervisao).Justify();

                    col.Item().PaddingTop(4).Text(CtrDespachoTextos.TituloSituacao).SemiBold();

                    // Inciso I — a contratação já era acompanhada
                    col.Item().Text($"I - {CtrDespachoTextos.Caixa(incisoI)} "
                        + CtrDespachoTextos.IncisoIPreenchido(
                            incisoI ? manifestacao.ComunicadaDesde : null,
                            incisoI ? manifestacao.Criticidade : null)).Justify();

                    Opcao(col, 1, incisoI && manifestacao.ResultadoAnalise == CtrDominios.ResultadoAnalise.Alinhada,
                        CtrDespachoTextos.ResultadoAlinhada);
                    Opcao(col, 1,
                        incisoI && manifestacao.ResultadoAnalise == CtrDominios.ResultadoAnalise.InformacoesComplementares,
                        CtrDespachoTextos.ResultadoInformacoesComplementares);
                    Opcao(col, 1, riscos, CtrDespachoTextos.ResultadoRiscosSignificativos);

                    // As cinco opções do "tendo a SGDI:" aparecem sempre, como no papel
                    Opcao(col, 2, riscos && manifestacao.RecomendouSuspensao,
                        CtrDespachoTextos.RiscoRecomendouSuspensao);
                    Opcao(col, 2, riscos && manifestacao.ComunicouControleInterno,
                        CtrDespachoTextos.RiscoComunicouControleInterno);
                    Opcao(col, 2, riscos && manifestacao.DesfechoRisco == CtrDominios.DesfechoRisco.AguardandoResposta,
                        CtrDespachoTextos.RiscoAguardandoResposta);
                    Opcao(col, 2, riscos && manifestacao.DesfechoRisco == CtrDominios.DesfechoRisco.RiscoResolvido,
                        CtrDespachoTextos.RiscoResolvido);
                    Opcao(col, 2, riscos && manifestacao.DesfechoRisco == CtrDominios.DesfechoRisco.NaoPodeProsseguir,
                        CtrDespachoTextos.RiscoNaoPodeProsseguir);

                    // Inciso II — não constava como comunicada
                    col.Item().PaddingTop(4).Text($"II - {CtrDespachoTextos.Caixa(incisoII)} "
                        + CtrDespachoTextos.IncisoIIPreenchido(
                            incisoII ? manifestacao.PrazoRegularizacaoDias : null)).Justify();

                    col.Item().PaddingTop(4).Text(CtrDespachoTextos.Ressalva).Justify();
                    col.Item().Text(CtrDespachoTextos.NaturezaTecnica).Justify();

                    // A Observacao da manifestação é anotação INTERNA da equipe e não
                    // entra no despacho: o template oficial do TCDF não tem esse campo.
                    //
                    // Fecho num bloco só, com ShowEntire para nunca ser partido entre
                    // páginas (antes ele escorregava sozinho para a página 2) e colado
                    // ao último parágrafo (padding moderado, sem espaçamento grande).
                    col.Item().PaddingTop(6).ShowEntire().Column(fecho =>
                    {
                        fecho.Item().Text(CtrDespachoTextos.Fecho);
                        fecho.Item().PaddingTop(8).Text(local);
                        fecho.Item().Text($"{data:dd/MM/yyyy}");
                        fecho.Item().Text($"{nome} — {cargo}");
                    });
                });

                page.Footer().AlignRight().Text(t =>
                {
                    t.DefaultTextStyle(s => s.FontSize(8));
                    t.CurrentPageNumber();
                    t.Span(" / ");
                    t.TotalPages();
                });
            });
        }).GeneratePdf();
    }

    /// <summary>"objeto — Órgão (SIGLA), complemento" do cabeçalho do despacho.</summary>
    private static string Contratacao(CtrProcessoResponse p)
    {
        var texto = $"{p.Objeto} — {p.OrgaoNome} ({p.OrgaoSigla})";
        return string.IsNullOrWhiteSpace(p.ComplementoArea) ? texto : $"{texto}, {p.ComplementoArea}";
    }

    /// <summary>Alternativa do formulário, recuada conforme o nível do papel.</summary>
    private static void Opcao(ColumnDescriptor col, int nivel, bool marcada, string texto)
    {
        col.Item().PaddingLeft(nivel * 12).PaddingTop(-1)
            .Text($"- {CtrDespachoTextos.Caixa(marcada)} {texto}").Justify();
    }
}
