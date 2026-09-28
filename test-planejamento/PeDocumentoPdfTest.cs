using System.Security.Cryptography;
using System.Text.RegularExpressions;
using api.Planejamento;
using Microsoft.EntityFrameworkCore;
using Models.Planejamento;
using service;
using service.Planejamento;
using Xunit;

namespace test.planejamento;

/// <summary>
/// O PDF do documento (QuestPDF): o roteiro (capa, páginas especiais, sumário com os capítulos,
/// páginas deitadas para as tabelas largas) e a geração pelo serviço, que guarda a versão
/// minuta com o arquivo, o hash e as páginas. O PDF é conferido pelo que dá para ler sem
/// abrir o conteúdo: páginas, orientação (/MediaBox), destinos do sumário, links e imagens.
/// Com a variável PE_EXEMPLO_PDF, o PDF do PDTIC completo é gravado nesse caminho (o de
/// exemplo para conferir a olho).
/// </summary>
public class PeDocumentoPdfTest : PeDocumentoTestBase
{
    private static int Codigo(ApiException ex) => ex.Error.Code;

    private static int Retrato(string pdf) => Regex.Matches(pdf, @"/MediaBox\s*\[0 0 595(\.\d+)? 842(\.\d+)?\]").Count;

    private static int Deitadas(string pdf) => Regex.Matches(pdf, @"/MediaBox\s*\[0 0 842(\.\d+)? 595(\.\d+)?\]").Count;

    /// <summary>O que vai junto com o título do capítulo (o título e os blocos, pelo tipo e pela seção).</summary>
    private static List<string> Comeco(PeDocumentoPdf.Roteiro roteiro, string chave)
    {
        var pecas = roteiro.Grupos.Single(g => g.Pecas.Any(p => p.Tipo == PeDocumentoPdf.TipoPeca.Titulo && p.Capitulo!.Chave == chave)).Pecas;
        var inicio = pecas.ToList().FindIndex(p => p.Tipo == PeDocumentoPdf.TipoPeca.Titulo && p.Capitulo!.Chave == chave);
        var fim = PeDocumentoPdf.FimDoComeco(pecas, inicio);
        return pecas.Skip(inicio).Take(fim - inicio + 1)
            .Select(p => p.Tipo == PeDocumentoPdf.TipoPeca.Titulo
                ? $"titulo {p.Capitulo!.Chave}"
                : $"{p.Bloco!.Tipo} {p.Bloco.Tabela?.SecaoChave}".Trim())
            .ToList();
    }

    [Fact]
    public void Moeda_ORealNaoSeSeparaDoValor()
    {
        var semQuebra = (char)0xA0;
        Assert.Equal($"R${semQuebra}1.200.000,00", PeDocumentoPdf.SemQuebra("R$ 1.200.000,00"));
        Assert.Equal($"De R${semQuebra}10,00 a R${semQuebra}20,00", PeDocumentoPdf.SemQuebra("De R$ 10,00 a R$ 20,00"));
        Assert.Equal("Sem valor", PeDocumentoPdf.SemQuebra("Sem valor"));
    }

    [Fact]
    public async Task Roteiro_CapaEspeciais_Sumario_EPaginasDeitadas()
    {
        await DefinirNivelDoOrgaoAsync(OrgaoSes, "avancado");
        var pdtic = await AbrirSesAsync();
        var documento = await DocumentoAsync(pdtic.Id);

        var roteiro = PeDocumentoPdf.Montar(documento);

        var capa = roteiro.Grupos[0];
        Assert.True(capa.Capa);
        Assert.Equal(PeDocumentoPdf.TipoPeca.Capa, Assert.Single(capa.Pecas).Tipo);
        // Folha de rosto, histórico e sumário: cada um numa página (uma quebra antes de cada)
        var pecas = roteiro.Grupos.Skip(1).SelectMany(g => g.Pecas).ToList();
        Assert.Contains(pecas, p => p.Tipo == PeDocumentoPdf.TipoPeca.Historico);
        Assert.Contains(pecas, p => p.Tipo == PeDocumentoPdf.TipoPeca.Sumario);
        Assert.All(roteiro.Grupos, g => Assert.NotEqual(PeDocumentoPdf.TipoPeca.Quebra, g.Pecas[0].Tipo));
        Assert.All(roteiro.Grupos, g => Assert.NotEqual(PeDocumentoPdf.TipoPeca.Quebra, g.Pecas[^1].Tipo));

        // Sumário: os 21 capítulos numerados, os subcapítulos, a apresentação e os anexos
        Assert.Equal(Enumerable.Range(1, 21).Select(n => n.ToString()), roteiro.Sumario.Where(i => i.Nivel == 1 && i.Numero != null).Select(i => i.Numero));
        Assert.Contains(roteiro.Sumario, i => i.Numero == "6.4" && i.Titulo == "Análise SWOT");
        Assert.Contains(roteiro.Sumario, i => i.Titulo == "Apresentação" && i.Numero == null);
        Assert.Contains(roteiro.Sumario, i => i.Titulo == "Anexos");
        Assert.DoesNotContain(roteiro.Sumario, i => i.Titulo is "Capa" or "Folha de rosto" or "Sumário" or "Histórico de versões");
        foreach (var travado in documento.Capitulos.Where(c => c.Travado))
            Assert.Contains(roteiro.Sumario, i => i.Secao == PeDocumentoPdf.NomeDaSecao(travado));

        // Sem dado, a tabela larga fica em pé, junto do capítulo (F1, C26): nenhuma página deitada só com "Nenhum item"
        Assert.Empty(roteiro.Grupos.Where(g => g.Deitada).SelectMany(g => g.Pecas).Where(p => p.Bloco?.Tabela != null));

        // O começo do capítulo não se separa do título: os títulos e os textos seguidos, até o
        // primeiro bloco que não é texto; para na quebra de página (a conclusão, antes dos anexos)
        Assert.Equal(new[] { "titulo orcamento", "texto", "tabela_secao orcamento_acoes" }, Comeco(roteiro, "orcamento"));
        Assert.Equal(new[] { "titulo diagnostico", "texto", "titulo diagnostico_organizacao", "tabela_secao diagnostico_ambiente" },
            Comeco(roteiro, "diagnostico"));
        Assert.Equal(new[] { "titulo conclusao", "texto" }, Comeco(roteiro, "conclusao"));

        // Capítulo oculto não entra
        await Documentos.AtualizarCapituloAsync(pdtic.Id, CapituloDoModelo("termos").Id,
            new PeDocCapituloOrgaoDTO { Oculto = true, Informados = new HashSet<string> { "Oculto" } }, await Orgao());
        var semTermos = PeDocumentoPdf.Montar(await DocumentoAsync(pdtic.Id));
        Assert.DoesNotContain(semTermos.Sumario, i => i.Titulo == "Termos e abreviações");
        Assert.Equal(20, semTermos.Sumario.Count(i => i.Nivel == 1 && i.Numero != null));
    }

    [Fact]
    public async Task Roteiro_TabelasLargasComDado_VaoParaAPaginaDeitada()
    {
        var pdtic = await PdticCompletoAsync();
        var roteiro = PeDocumentoPdf.Montar(await DocumentoAsync(pdtic.Id));

        // As tabelas largas com dado vão para páginas deitadas; o resto fica em pé
        var deitadas = roteiro.Grupos.Where(g => g.Deitada).SelectMany(g => g.Pecas)
            .Where(p => p.Bloco?.Tabela != null).Select(p => p.Bloco!.Tabela!.SecaoChave).ToList();
        Assert.Equal(new[] { "ativos", "necessidades", "contratacoes", "metas", "acoes", "riscos" }, deitadas);
        Assert.All(roteiro.Grupos.Where(g => g.Deitada).SelectMany(g => g.Pecas), p => Assert.True(p.Deitada));
        // O título e o texto que vêm antes da tabela larga vão para a mesma página deitada
        var ativos = roteiro.Grupos.Single(g => g.Pecas.Any(p => p.Bloco?.Tabela?.SecaoChave == "ativos"));
        Assert.Equal(new[] { PeDocumentoPdf.TipoPeca.Titulo, PeDocumentoPdf.TipoPeca.Bloco, PeDocumentoPdf.TipoPeca.Bloco },
            ativos.Pecas.Where(p => p.Capitulo?.Chave == "ativos").Select(p => p.Tipo));
    }

    [Fact]
    public async Task PdfDoPdticCompleto_GuardaAVersao_EOExemplo()
    {
        var pdtic = await PdticCompletoAsync();
        var ctx = await Orgao();
        await Documentos.SalvarTextoAsync(pdtic.Id, BlocoDoModelo("introducao").Id, Json(Doc(
            Paragrafo("Este PDTIC organiza as prioridades de TIC do órgão ({orgao.sigla}) para a vigência de {vigencia.inicio} a {vigencia.fim}."),
            new
            {
                type = "paragraph",
                content = new object[]
                {
                    new { type = "text", text = "O plano segue o " },
                    new { type = "text", text = "Decreto nº 48.900/2026", marks = new object[] { new { type = "link", attrs = new { href = "https://www.sinj.df.gov.br/decreto-48900" } } } },
                    new { type = "text", text = "." }
                }
            })), ctx);

        var versao = await Documentos.GerarPdfAsync(pdtic.Id, ctx);

        Assert.Equal(1, versao.Numero);
        Assert.Equal(PeDominios.SituacaoVersaoDoc.Minuta, versao.Situacao);
        Assert.Equal(UserOrgaoSes.Email, versao.GeradoPor);
        Assert.True(versao.Paginas >= 15, $"páginas: {versao.Paginas}");

        var arquivo = await Documentos.ArquivoDaVersaoAsync(pdtic.Id, 1, ctx);
        var pdf = arquivo.Conteudo;
        // O exemplo sai antes das conferências (PE_EXEMPLO_PDF aponta o caminho)
        var caminho = Environment.GetEnvironmentVariable("PE_EXEMPLO_PDF");
        if (!string.IsNullOrWhiteSpace(caminho)) await File.WriteAllBytesAsync(caminho, pdf);
        var texto = TextoDoPdf(pdf);
        Assert.Equal("PDTIC_SES_v1.0_1.pdf", arquivo.NomeArquivo);
        Assert.StartsWith("%PDF-", texto);
        Assert.Equal(versao.Tamanho, pdf.Length);
        Assert.Equal(versao.Paginas, PeDocumentoPdf.ContarPaginas(pdf));
        Assert.True(Retrato(texto) >= 10);
        // As tabelas largas em seguida dividem a mesma página deitada (9.3 e 10; metas e ações)
        Assert.True(Deitadas(texto) >= 4);

        // O sumário leva a cada capítulo: um destino por seção no PDF
        var documento = await DocumentoAsync(pdtic.Id);
        foreach (var capitulo in documento.Capitulos.Where(c => !c.Oculto && !PeDominios.CapituloEspecial.EhPreTextual(c.Chave)))
            Assert.Contains(PeDocumentoPdf.NomeDaSecao(capitulo), texto);
        // Links do texto do órgão e do texto rico do diagnóstico; o logotipo e o organograma
        Assert.Contains("/URI (https://www.sinj.df.gov.br/decreto-48900)", texto);
        Assert.Contains("/URI (https://www.saude.df.gov.br/organograma)", texto);
        Assert.True(Regex.Matches(texto, @"/Subtype\s*/Image").Count >= 2);
        Assert.Matches(@"/Title\s*[(<]", texto);

        // Guardado como arquivo do PDTIC, com o hash
        var guardada = Context.PeDocVersoes.AsNoTracking().Single(v => v.PdticId == pdtic.Id);
        var metadados = Context.PeArquivos.AsNoTracking().Single(a => a.Id == guardada.ArquivoId);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(pdf)).ToLowerInvariant(), guardada.Hash);
        Assert.Equal(guardada.Hash, metadados.Hash);
        Assert.Equal((PeDominios.DonoArquivo.Pdtic, (long?)pdtic.Id, "application/pdf"), (metadados.DonoTipo, metadados.DonoId, metadados.TipoMime));

        // A segunda geração é a versão 2; a lista vem da mais nova para a mais antiga
        var segunda = await Documentos.GerarPdfAsync(pdtic.Id, ctx);
        Assert.Equal(2, segunda.Numero);
        Assert.Equal(new[] { 2, 1 }, (await Documentos.VersoesAsync(pdtic.Id, await ContextoDe(UserConsultaSes))).Select(v => v.Numero));
        Assert.Equal(new[] { 2, 1 }, (await DocumentoAsync(pdtic.Id)).Versoes.Select(v => v.Numero));
    }

    [Fact]
    public async Task PdfDoBasico_SemDados_SaiComAsTabelasVazias()
    {
        var pdtic = await AbrirSesAsync();

        var versao = await Documentos.GerarPdfAsync(pdtic.Id, await Orgao());

        Assert.True(versao.Paginas >= 6, $"páginas: {versao.Paginas}");
        var pdf = TextoDoPdf((await Documentos.ArquivoDaVersaoAsync(pdtic.Id, versao.Numero, await Orgao())).Conteudo);
        // Sem ativos, contratações nem metas, as tabelas largas ficam deitadas mesmo vazias
        Assert.True(Deitadas(pdf) >= 1);
    }

    [Fact]
    public async Task Gerar_SoAEquipeDoOrgao_ComOPdticAberto()
    {
        var pdtic = await AbrirSesAsync();

        foreach (var user in new[] { UserConsultaSes, UserPeSgdi, UserPeAdmin, UserPeCgtic, UserOrgaoSeec })
        {
            var ex = await Assert.ThrowsAsync<ApiException>(async () => await Documentos.GerarPdfAsync(pdtic.Id, await ContextoDe(user)));
            Assert.Equal((int)ErrorCode.PeSemPermissao, Codigo(ex));
        }

        var entidade = Context.PePdtics.Single(p => p.Id == pdtic.Id);
        entidade.Situacao = PeDominios.SituacaoPdtic.EmAprovacao;
        await Context.SaveChangesAsync();
        var fechado = await Assert.ThrowsAsync<ApiException>(async () => await Documentos.GerarPdfAsync(pdtic.Id, await Orgao()));
        Assert.Equal((int)ErrorCode.PePdticFechado, Codigo(fechado));
        Assert.Empty(Context.PeDocVersoes);
    }

    [Fact]
    public async Task Versoes_EDownload_QuemVeOOrgao()
    {
        var pdtic = await AbrirSesAsync();
        await Documentos.GerarPdfAsync(pdtic.Id, await ContextoDe(UserAdminGeral));

        var versoes = await Documentos.VersoesAsync(pdtic.Id, await ContextoDe(UserPeSgdi));
        var versao = Assert.Single(versoes);
        Assert.Equal(UserAdminGeral.Email, versao.GeradoPor);
        Assert.True(versao.Tamanho > 1000);

        Assert.NotEmpty((await Documentos.ArquivoDaVersaoAsync(pdtic.Id, 1, await ContextoDe(UserConsultaSes))).Conteudo);
        var outro = await Assert.ThrowsAsync<ApiException>(async () => await Documentos.ArquivoDaVersaoAsync(pdtic.Id, 1, await ContextoDe(UserOrgaoSeec)));
        Assert.Equal((int)ErrorCode.PeSemPermissao, Codigo(outro));
        var inexistente = await Assert.ThrowsAsync<ApiException>(async () => await Documentos.ArquivoDaVersaoAsync(pdtic.Id, 9, await Orgao()));
        Assert.Equal((int)ErrorCode.PeDocVersaoNaoEncontrada, Codigo(inexistente));

        // O PDF também abre pelo endereço de arquivos do módulo para quem vê o órgão
        var arquivoId = Context.PeDocVersoes.AsNoTracking().Single().ArquivoId;
        Assert.NotEmpty((await Arquivos.BaixarAsync(arquivoId, await ContextoDe(UserConsultaSes))).Conteudo);
        await Assert.ThrowsAsync<ApiException>(async () => await Arquivos.BaixarAsync(arquivoId, await ContextoDe(UserOrgaoSeec)));
    }
}
