using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using api.Planejamento;
using demanda_service.Helpers;
using Microsoft.EntityFrameworkCore;
using Models;
using Models.Pgia;
using Models.Planejamento;
using service.Interface;

namespace service.Planejamento;

/// <summary>
/// O documento do PDTIC de um órgão (E5): o modelo da SGDI resolvido para o órgão, a cópia do
/// órgão (só o que ele mudou), a edição dos textos e dos capítulos e o PDF com as versões.
/// <list type="bullet">
/// <item>Capítulo: aparece quando o passo dele (passo_chave) está na trilha do órgão (nível e
/// ajustes) e o capítulo pai aparece; o travado e o obrigatório não se escondem; o oculto vem
/// só com o título (sem número e sem blocos) e os subcapítulos dele não vêm; o número é pela
/// posição entre os visíveis (6, 6.1).</item>
/// <item>Texto: o do órgão, quando ele editou; senão o do modelo, na hora (decisão 14). Se o
/// modelo mudar depois da edição, ModeloMudou e o texto novo do modelo ao lado.</item>
/// <item>Marcadores: <see cref="PeDocMarcadores"/>; sem valor, continua escrito na prévia e sai
/// em branco no PDF.</item>
/// <item>Dados: tabela de uma seção (os campos visíveis do nível, com os rótulos prontos, e o
/// filtro do bloco), ações de um tema, matriz SWOT, inventário de IA do PGIA e fluxo (a E6
/// desenha); bloco de seção que o órgão não vê (ou que o administrador tirou do documento)
/// não aparece.</item>
/// <item>Ler: quem vê o órgão. Editar e gerar o PDF: a equipe do órgão (e o admin geral) com o
/// PDTIC em elaboração ou devolvido.</item>
/// </list>
/// Nada aqui está no caminho de cada requisição: só as actions do documento leem as tabelas.
/// </summary>
public class PeDocumentoService : IPeDocumentoService
{
    public const string MimePdf = "application/pdf";

    /// <summary>O texto guardado quando o órgão deixa o bloco em branco (o TipTap vazio).</summary>
    public const string TextoVazio = "{\"type\":\"doc\",\"content\":[{\"type\":\"paragraph\"}]}";

    private const string PassoDosSistemasDeIa = "diagnostico.sistemas-ia";

    /// <summary>
    /// Os fluxos do guia pela chave: o nome que o bloco mostra quando o fluxo não está em
    /// pe_fluxo_modelo (antes de a E6 carregar os fluxos, ou chave que não existe).
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> NomesDosFluxos = new Dictionary<string, string>
    {
        ["macroprocesso"] = "Macroprocesso do PDTIC (figura 4 do guia)",
        ["elaboracao"] = "Processo de elaboração do PDTIC (figura 5 do guia)",
        ["preparacao"] = "Preparação (figura 6 do guia)",
        ["diagnostico"] = "Diagnóstico (figura 7 do guia)",
        ["planejamento"] = "Planejamento (figura 14 do guia)",
        ["acompanhamento"] = "Processo de acompanhamento do PDTIC (figura 17 do guia)",
        ["planejamento_acompanhamento"] = "Planejamento do acompanhamento (figura 19 do guia)",
        ["monitoramento"] = "Monitoramento (figura 20 do guia)",
        ["avaliacao_intermediaria"] = "Avaliação intermediária (figura 21 do guia)",
        ["avaliacao_final"] = "Avaliação final (figura 22 do guia)"
    };

    private readonly AppDbContext _context;
    private readonly IPeRegistroService _registros;
    private readonly IPePermissionService _permissoes;

    public PeDocumentoService(AppDbContext context, IPeRegistroService registros, IPePermissionService permissoes)
    {
        _context = context;
        _registros = registros;
        _permissoes = permissoes;
    }

    // ── Leitura ─────────────────────────────────────────────────────────────

    public async Task<PeDocumentoResponse> ObterAsync(long pdticId, PeUserContext ctx)
    {
        var pdtic = await PePdticService.LerAsync(_context, _permissoes, pdticId, ctx);
        return (await ResolverAsync(pdtic, ctx)).Resposta;
    }

    public async Task<List<PeDocVersaoResponse>> VersoesAsync(long pdticId, PeUserContext ctx)
    {
        var pdtic = await PePdticService.LerAsync(_context, _permissoes, pdticId, ctx);
        return await VersoesDoPdticAsync(pdtic.Id);
    }

    public async Task<PeDocArquivo> ArquivoDaVersaoAsync(long pdticId, int numero, PeUserContext ctx)
    {
        var pdtic = await PePdticService.LerAsync(_context, _permissoes, pdticId, ctx);
        var versao = await _context.PeDocVersoes.AsNoTracking().FirstOrDefaultAsync(v => v.PdticId == pdtic.Id && v.Numero == numero)
                     ?? throw new ApiException(ErrorCode.PeDocVersaoNaoEncontrada, "Versão do documento não encontrada. Atualize a tela.");
        var conteudo = await _context.PeArquivosConteudo.AsNoTracking()
            .Where(c => c.Id == versao.ArquivoId)
            .Select(c => c.Conteudo)
            .FirstAsync();
        var sigla = await _context.PgiaOrgaos.AsNoTracking().Where(o => o.Id == pdtic.OrgaoId).Select(o => o.Sigla).FirstAsync();
        return new PeDocArquivo(conteudo, NomeDoArquivo(sigla, pdtic.Versao, versao.Numero));
    }

    /// <summary>PDTIC_SIGLA_v1.0_3.pdf (sigla só com letras, números, hífen e sublinhado).</summary>
    public static string NomeDoArquivo(string sigla, string versao, int numero)
    {
        var limpa = new string((sigla ?? string.Empty).Trim().Select(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' ? c : '_').ToArray());
        return $"PDTIC_{(limpa.Length == 0 ? "orgao" : limpa)}_v{versao}_{numero.ToString(CultureInfo.InvariantCulture)}.pdf";
    }

    // ── Edição pela equipe do órgão ─────────────────────────────────────────

    public async Task<PeDocBlocoResponse> SalvarTextoAsync(long pdticId, long blocoId, JsonElement texto, PeUserContext ctx)
    {
        var pdtic = await PdticParaEditarAsync(pdticId, ctx);
        var (bloco, _) = await BlocoDoOrgaoAsync(pdtic, blocoId);
        if (bloco.Tipo != PeDominios.TipoBloco.Texto)
            throw new ApiException(ErrorCode.PeDocBlocoNaoEditavel, "Só o texto do documento é editado aqui. Os dados das tabelas são editados nos passos da trilha.");

        if (texto.ValueKind != JsonValueKind.Object || texto.GetRawText().Length > PeDocConfig.MaximoTexto)
            throw new ApiException(ErrorCode.PeDocTextoInvalido, "O texto passou do tamanho máximo ou veio num formato que não serve.");
        var rico = PeTextoRico.Validar(texto);
        if (rico.Erro != null) throw new ApiException(ErrorCode.PeDocTextoInvalido, rico.Erro);
        // Imagens já aceitas: do modelo, deste PDTIC ou (desde a E7) de outra versão do PDTIC do
        // órgão, que a revisão copia com os textos
        var doOrgao = await PdticsDoOrgaoAsync(pdtic.OrgaoId);
        var arquivos = await ImagensParaOTextoAsync(rico.Imagens, ctx, a =>
            (a.DonoTipo == PeDominios.DonoArquivo.Pdtic && a.DonoId != null && doOrgao.Contains(a.DonoId.Value))
            || a.DonoTipo == PeDominios.DonoArquivo.DocModelo);

        var novo = rico.Documento ?? JsonNode.Parse(TextoVazio)!;
        var textoDoModelo = PeDocConfig.TextoDoBloco(PeDocConfig.Ler(bloco.Config));
        var agora = DateTime.UtcNow;
        var copia = await _context.PeDocOrgaoBlocos.FirstOrDefaultAsync(o => o.PdticId == pdtic.Id && o.BlocoId == bloco.Id);

        var igualAoModelo = PeDocMarcadores.Canonico(novo) == PeDocMarcadores.Canonico(textoDoModelo)
                            || (!PeTextoRico.TemConteudo(novo) && !PeTextoRico.TemConteudo(textoDoModelo));
        if (igualAoModelo)
        {
            // Igual ao texto do modelo: não é edição; o bloco volta a seguir o modelo
            if (copia != null) _context.PeDocOrgaoBlocos.Remove(copia);
        }
        else
        {
            if (copia == null)
            {
                copia = new PeDocOrgaoBloco { PdticId = pdtic.Id, BlocoId = bloco.Id };
                _context.PeDocOrgaoBlocos.Add(copia);
            }
            copia.Texto = novo.ToJsonString(PeModeloService.JsonHistorico);
            copia.ModeloHash = PeDocMarcadores.Hash(textoDoModelo);
            copia.EditadoEm = agora;
            copia.EditadoPor = ctx.Email;
        }

        // A imagem nova passa a ser do PDTIC (quem vê o órgão vê a imagem)
        foreach (var arquivo in arquivos)
        {
            arquivo.DonoTipo = PeDominios.DonoArquivo.Pdtic;
            arquivo.DonoId = pdtic.Id;
            arquivo.AlteradoEm = agora;
            arquivo.AlteradoPor = ctx.Email;
        }
        Tocar(pdtic, ctx, agora);
        await _context.SaveChangesAsync();
        return await BlocoResolvidoAsync(pdtic.Id, bloco.Id, ctx);
    }

    public async Task<PeDocBlocoResponse> RestaurarTextoAsync(long pdticId, long blocoId, PeUserContext ctx)
    {
        var pdtic = await PdticParaEditarAsync(pdticId, ctx);
        var (bloco, _) = await BlocoDoOrgaoAsync(pdtic, blocoId);
        if (bloco.Tipo != PeDominios.TipoBloco.Texto)
            throw new ApiException(ErrorCode.PeDocBlocoNaoEditavel, "Só o texto do documento volta ao texto do modelo.");

        var copia = await _context.PeDocOrgaoBlocos.FirstOrDefaultAsync(o => o.PdticId == pdtic.Id && o.BlocoId == bloco.Id);
        if (copia != null)
        {
            _context.PeDocOrgaoBlocos.Remove(copia);
            Tocar(pdtic, ctx, DateTime.UtcNow);
            await _context.SaveChangesAsync();
        }
        return await BlocoResolvidoAsync(pdtic.Id, bloco.Id, ctx);
    }

    public async Task<PeDocCapituloResponse> AtualizarCapituloAsync(long pdticId, long capituloId, PeDocCapituloOrgaoDTO dto, PeUserContext ctx)
    {
        var pdtic = await PdticParaEditarAsync(pdticId, ctx);
        var base_ = await BaseAsync(pdtic);
        var capitulo = base_.Capitulos.FirstOrDefault(c => c.Id == capituloId);
        if (capitulo == null || !base_.Presente(capitulo))
            throw new ApiException(ErrorCode.PeDocCapituloNaoEncontrado, "Este capítulo não está no documento do órgão. Atualize a tela.");

        var copia = await _context.PeDocOrgaos.FirstOrDefaultAsync(o => o.PdticId == pdtic.Id && o.CapituloId == capitulo.Id);
        var oculto = copia?.Oculto ?? false;
        var tituloProprio = copia?.TituloProprio;

        if (dto.Informou(nameof(dto.Oculto)) && dto.Oculto != null)
        {
            if (dto.Oculto.Value && capitulo.Travado)
                throw new ApiException(ErrorCode.PeDocCapituloObrigatorio,
                    "Este capítulo é um dos nove conteúdos mínimos do PDTIC (art. 12, § 2º, do Decreto nº 48.900/2026) e não pode ser escondido.");
            if (dto.Oculto.Value && capitulo.Obrigatorio)
                throw new ApiException(ErrorCode.PeDocCapituloObrigatorio, "Este capítulo é obrigatório no modelo da SGDI e não pode ser escondido.");
            oculto = dto.Oculto.Value;
        }
        if (dto.Informou(nameof(dto.TituloProprio)))
        {
            var titulo = dto.TituloProprio?.Trim();
            if (titulo is { Length: > 200 }) throw new ApiException(ErrorCode.PeDadosInvalidos, "O título tem no máximo 200 caracteres.");
            tituloProprio = string.IsNullOrEmpty(titulo) || titulo == capitulo.Titulo ? null : titulo;
        }

        var agora = DateTime.UtcNow;
        if (copia == null)
        {
            if (oculto || tituloProprio != null)
            {
                _context.PeDocOrgaos.Add(new PeDocOrgao
                {
                    PdticId = pdtic.Id,
                    CapituloId = capitulo.Id,
                    Oculto = oculto,
                    TituloProprio = tituloProprio,
                    CriadoEm = agora,
                    CriadoPor = ctx.Email
                });
                Tocar(pdtic, ctx, agora);
                await _context.SaveChangesAsync();
            }
        }
        else if (copia.Oculto != oculto || copia.TituloProprio != tituloProprio)
        {
            copia.Oculto = oculto;
            copia.TituloProprio = tituloProprio;
            copia.AlteradoEm = agora;
            copia.AlteradoPor = ctx.Email;
            Tocar(pdtic, ctx, agora);
            await _context.SaveChangesAsync();
        }

        var pdticLido = await _context.PePdtics.AsNoTracking().FirstAsync(p => p.Id == pdtic.Id);
        var resolvido = await ResolverAsync(pdticLido, ctx, soCapitulo: capitulo.Id);
        return resolvido.Capitulos[capitulo.Id];
    }

    // ── PDF ─────────────────────────────────────────────────────────────────

    public async Task<PeDocVersaoResponse> GerarPdfAsync(long pdticId, PeUserContext ctx)
    {
        var pdtic = await PePdticService.LerAsync(_context, _permissoes, pdticId, ctx);
        if (!_permissoes.PodeEditarPdtic(ctx, pdtic.OrgaoId))
            throw new ApiException(ErrorCode.PeSemPermissao, "Só a equipe do órgão gera o PDF do documento.");
        if (!PeEdicaoPdtic.ElaboracaoAberta(pdtic)) throw PePdticService.Fechado(pdtic);

        var (versao, tamanho) = await GerarVersaoAsync(pdtic, ctx, PeDominios.SituacaoVersaoDoc.Minuta);
        await _context.SaveChangesAsync();
        return Versao(versao, tamanho);
    }

    /// <summary>
    /// Gera o PDF do documento e deixa a versão pronta no contexto, sem gravar (quem chama grava:
    /// a minuta sozinha; a versão enviada ao CGTIC, na mesma gravação do envio e da deliberação).
    /// O PDF fica em pe_arquivo (dono o PDTIC), com o número seguinte e o hash; o rodapé diz a
    /// situação da versão.
    /// </summary>
    public async Task<(PeDocVersao Versao, long Tamanho)> GerarVersaoAsync(PePdtic pdtic, PeUserContext ctx, string situacao)
    {
        var resolvido = await ResolverAsync(pdtic, ctx);
        var anteriores = await _context.PeDocVersoes.AsNoTracking().Where(v => v.PdticId == pdtic.Id).OrderBy(v => v.Numero).ToListAsync();
        var numero = anteriores.Select(v => v.Numero).DefaultIfEmpty(0).Max() + 1;
        var agora = DateTime.UtcNow;
        var agoraBrasilia = DateTimeHelper.ToBrasilia(agora);

        // Histórico: as versões que não são minuta e esta
        var autores = anteriores.Select(v => v.GeradoPor).Append(ctx.Email).Distinct().ToList();
        var nomes = await _context.Users.AsNoTracking()
            .Where(u => autores.Contains(u.Email))
            .Select(u => new { u.Email, u.Nome })
            .ToListAsync();
        string Autor(string email) => nomes.FirstOrDefault(n => n.Email == email)?.Nome is string nome && !string.IsNullOrWhiteSpace(nome)
            ? nome.Trim()
            : email;
        var historico = anteriores
            .Where(v => v.Situacao != PeDominios.SituacaoVersaoDoc.Minuta)
            .Select(v => LinhaDoHistorico(pdtic.Versao, v.Numero, v.Situacao, DateTimeHelper.ToBrasilia(v.GeradoEm), Autor(v.GeradoPor)))
            .Append(LinhaDoHistorico(pdtic.Versao, numero, situacao, agoraBrasilia, Autor(ctx.Email)))
            .ToList();

        var entrada = new PeDocumentoPdf.Entrada
        {
            Documento = resolvido.Resposta,
            Marcadores = resolvido.Marcadores,
            Imagens = await ImagensDoDocumentoAsync(resolvido, pdtic),
            Logotipo = resolvido.LogotipoId is long logo ? (await ImagensAsync(new[] { logo }, pdtic, soDoRegistro: true)).GetValueOrDefault(logo) : null,
            Historico = historico,
            Rodape = Rodape(resolvido.Orgao.Sigla, pdtic.Versao, numero, situacao),
            GeradoEm = agoraBrasilia
        };

        PeDocumentoPdf.Resultado pdf;
        try
        {
            pdf = PeDocumentoPdf.Gerar(entrada);
        }
        catch (Exception ex) when (ex is not ApiException and not OperationCanceledException)
        {
            throw new ApiException(ErrorCode.PeDocGeracaoFalhou,
                "O PDF não pôde ser montado: algum conteúdo não coube na página (uma imagem ou uma tabela muito grande, por exemplo). "
                + "Confira os textos e as imagens e tente de novo.");
        }

        var hash = Convert.ToHexString(SHA256.HashData(pdf.Pdf)).ToLowerInvariant();
        var arquivo = new PeArquivo
        {
            Nome = NomeDoArquivo(resolvido.Orgao.Sigla, pdtic.Versao, numero),
            TipoMime = MimePdf,
            Tamanho = pdf.Pdf.Length,
            Hash = hash,
            DonoTipo = PeDominios.DonoArquivo.Pdtic,
            DonoId = pdtic.Id,
            CriadoEm = agora,
            CriadoPor = ctx.Email
        };
        _context.PeArquivosConteudo.Add(new PeArquivoConteudo { Arquivo = arquivo, Conteudo = pdf.Pdf });
        var versao = new PeDocVersao
        {
            PdticId = pdtic.Id,
            Numero = numero,
            Situacao = situacao,
            Arquivo = arquivo,
            Hash = hash,
            Paginas = Math.Max(1, pdf.Paginas),
            GeradoEm = agora,
            GeradoPor = ctx.Email
        };
        _context.PeDocVersoes.Add(versao);
        return (versao, arquivo.Tamanho);
    }

    /// <summary>
    /// O rodapé do PDF: "SES · PDTIC versão 1.0 · minuta nº 3" na minuta e "SES · PDTIC versão
    /// 1.0 · nº 4, enviada ao CGTIC" na versão que vai ao comitê (a situação com a primeira
    /// letra minúscula, sem mexer na sigla).
    /// </summary>
    public static string Rodape(string sigla, string versaoPdtic, int numero, string situacao)
    {
        var numeroTexto = numero.ToString(CultureInfo.InvariantCulture);
        if (situacao == PeDominios.SituacaoVersaoDoc.Minuta) return $"{sigla} · PDTIC versão {versaoPdtic} · minuta nº {numeroTexto}";
        var rotulo = PeDominios.SituacaoVersaoDoc.Rotulo(situacao);
        return $"{sigla} · PDTIC versão {versaoPdtic} · nº {numeroTexto}, {char.ToLowerInvariant(rotulo[0])}{rotulo[1..]}";
    }

    private static PeDocumentoPdf.LinhaHistorico LinhaDoHistorico(string versaoPdtic, int numero, string situacao, DateTime quando, string autor) =>
        new(quando.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture),
            $"{versaoPdtic} · nº {numero.ToString(CultureInfo.InvariantCulture)}",
            situacao switch
            {
                PeDominios.SituacaoVersaoDoc.Minuta => "Minuta gerada para conferência",
                PeDominios.SituacaoVersaoDoc.Enviada => "Versão enviada ao CGTIC",
                PeDominios.SituacaoVersaoDoc.Aprovada => "Versão aprovada pelo CGTIC",
                PeDominios.SituacaoVersaoDoc.Publicada => "Versão publicada",
                _ => situacao
            },
            autor);

    private async Task<List<PeDocVersaoResponse>> VersoesDoPdticAsync(long pdticId)
    {
        var versoes = await (from v in _context.PeDocVersoes.AsNoTracking()
                             join a in _context.PeArquivos.AsNoTracking() on v.ArquivoId equals a.Id
                             where v.PdticId == pdticId
                             orderby v.Numero descending
                             select new { Versao = v, a.Tamanho })
            .ToListAsync();
        return versoes.Select(x => Versao(x.Versao, x.Tamanho)).ToList();
    }

    private static PeDocVersaoResponse Versao(PeDocVersao v, long tamanho) => new()
    {
        Numero = v.Numero,
        Situacao = v.Situacao,
        GeradoEm = v.GeradoEm,
        GeradoPor = v.GeradoPor,
        Tamanho = tamanho,
        Paginas = v.Paginas
    };

    // ── Imagens ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Confere as imagens de um texto: PNG ou JPEG do módulo, já aceitas (pela regra do dono) ou
    /// enviadas por quem grava e ainda sem dono. Devolve as que vão ganhar dono.
    /// </summary>
    internal async Task<List<PeArquivo>> ImagensParaOTextoAsync(IReadOnlyList<long> ids, PeUserContext ctx, Func<PeArquivo, bool> jaAceita)
    {
        var novas = new List<PeArquivo>();
        foreach (var id in ids)
        {
            var arquivo = await _context.PeArquivos.FirstOrDefaultAsync(a => a.Id == id)
                          ?? throw new ApiException(ErrorCode.PeDocTextoInvalido, "Uma das imagens não foi encontrada. Envie a imagem de novo.");
            if (PeArquivoService.TipoDoArquivo(arquivo.Nome) is not ("png" or "jpg"))
                throw new ApiException(ErrorCode.PeDocTextoInvalido, "No texto entram só imagens PNG ou JPEG.");
            if (jaAceita(arquivo)) continue;
            if (arquivo.DonoTipo != null || !string.Equals(arquivo.CriadoPor.Trim(), ctx.Email.Trim(), StringComparison.OrdinalIgnoreCase))
                throw new ApiException(ErrorCode.PeDocTextoInvalido, "Uma das imagens não pode ser usada aqui. Envie a imagem de novo.");
            if (!novas.Contains(arquivo)) novas.Add(arquivo);
        }
        return novas;
    }

    /// <summary>As imagens dos textos e dos registros que o documento mostra, pelo id.</summary>
    private async Task<Dictionary<long, byte[]>> ImagensDoDocumentoAsync(Resolvido resolvido, PePdtic pdtic)
    {
        var ids = new HashSet<long>();
        foreach (var bloco in resolvido.Resposta.Capitulos.SelectMany(c => c.Blocos))
        {
            if (bloco.TextoBruto is JsonElement texto)
                foreach (var id in PeTextoRico.ImagensDe(JsonNode.Parse(texto.GetRawText()))) ids.Add(id);
            foreach (var rico in bloco.Tabela?.Linhas.Where(l => l.Ricos != null).SelectMany(l => l.Ricos!.Values) ?? Enumerable.Empty<JsonElement>())
                foreach (var id in PeTextoRico.ImagensDe(JsonNode.Parse(rico.GetRawText()))) ids.Add(id);
        }
        return await ImagensAsync(ids, pdtic, soDoRegistro: false);
    }

    /// <summary>
    /// O conteúdo das imagens que o documento deste PDTIC pode mostrar: do modelo, do PDTIC ou
    /// de um registro dele (texto rico e logotipo). Desde a E7, também as de outra versão do
    /// PDTIC do mesmo órgão (a revisão copia os textos e os registros com as imagens da versão
    /// revista). Outra imagem fica de fora.
    /// </summary>
    private async Task<Dictionary<long, byte[]>> ImagensAsync(IEnumerable<long> ids, PePdtic pdtic, bool soDoRegistro)
    {
        var lista = ids.Distinct().ToList();
        if (lista.Count == 0) return new Dictionary<long, byte[]>();
        var arquivos = await _context.PeArquivos.AsNoTracking()
            .Where(a => lista.Contains(a.Id))
            .Select(a => new { a.Id, a.DonoTipo, a.DonoId, a.TipoMime })
            .ToListAsync();
        var doOrgao = await PdticsDoOrgaoAsync(pdtic.OrgaoId);
        var idsRegistros = arquivos.Where(a => a.DonoTipo == PeDominios.DonoArquivo.Registro && a.DonoId != null).Select(a => a.DonoId!.Value).ToList();
        var registrosDoOrgao = idsRegistros.Count == 0
            ? new HashSet<long>()
            : (await _context.PeRegistros.AsNoTracking()
                .Where(r => idsRegistros.Contains(r.Id) && r.PdticId != null && doOrgao.Contains(r.PdticId.Value))
                .Select(r => r.Id)
                .ToListAsync()).ToHashSet();

        var validos = arquivos
            .Where(a => a.TipoMime is "image/png" or "image/jpeg")
            .Where(a => a.DonoTipo switch
            {
                PeDominios.DonoArquivo.Registro => a.DonoId != null && registrosDoOrgao.Contains(a.DonoId.Value),
                PeDominios.DonoArquivo.Pdtic => !soDoRegistro && a.DonoId != null && doOrgao.Contains(a.DonoId.Value),
                PeDominios.DonoArquivo.DocModelo => !soDoRegistro,
                _ => false
            })
            .Select(a => a.Id)
            .ToList();
        if (validos.Count == 0) return new Dictionary<long, byte[]>();
        return await _context.PeArquivosConteudo.AsNoTracking()
            .Where(c => validos.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => c.Conteudo);
    }

    /// <summary>Os ids de todas as versões do PDTIC do órgão (a família das imagens que a revisão copia).</summary>
    private async Task<HashSet<long>> PdticsDoOrgaoAsync(long orgaoId) =>
        (await _context.PePdtics.AsNoTracking().Where(p => p.OrgaoId == orgaoId).Select(p => p.Id).ToListAsync()).ToHashSet();

    // ── Apoio da edição ─────────────────────────────────────────────────────

    private async Task<PePdtic> PdticParaEditarAsync(long pdticId, PeUserContext ctx)
    {
        var pdtic = await PePdticService.LerAsync(_context, _permissoes, pdticId, ctx, rastrear: true);
        if (!_permissoes.PodeEditarPdtic(ctx, pdtic.OrgaoId))
            throw new ApiException(ErrorCode.PeSemPermissao, "Só a equipe do órgão edita o documento do PDTIC.");
        if (!PeEdicaoPdtic.ElaboracaoAberta(pdtic)) throw PePdticService.Fechado(pdtic);
        return pdtic;
    }

    /// <summary>O bloco do modelo ativo que está no documento do órgão (capítulo presente e não oculto).</summary>
    private async Task<(PeDocBloco Bloco, Base Base)> BlocoDoOrgaoAsync(PePdtic pdtic, long blocoId)
    {
        var base_ = await BaseAsync(pdtic);
        var bloco = base_.Blocos.FirstOrDefault(b => b.Id == blocoId);
        var capitulo = bloco == null ? null : base_.Capitulos.FirstOrDefault(c => c.Id == bloco.CapituloId);
        if (bloco == null || capitulo == null || !base_.Presente(capitulo) || base_.Oculto(capitulo))
            throw new ApiException(ErrorCode.PeDocBlocoNaoEncontrado, "Este bloco não está no documento do órgão. Atualize a tela.");
        return (bloco, base_);
    }

    /// <summary>Gravar no documento toca o PDTIC (alterado em e por), com a situação como token de concorrência.</summary>
    private static void Tocar(PePdtic pdtic, PeUserContext ctx, DateTime agora)
    {
        pdtic.AlteradoEm = agora;
        pdtic.AlteradoPor = ctx.Email;
    }

    private async Task<PeDocBlocoResponse> BlocoResolvidoAsync(long pdticId, long blocoId, PeUserContext ctx)
    {
        var pdtic = await _context.PePdtics.AsNoTracking().FirstAsync(p => p.Id == pdticId);
        var resolvido = await ResolverAsync(pdtic, ctx, soBloco: blocoId);
        return resolvido.Blocos[blocoId];
    }

    // ── Resolução ───────────────────────────────────────────────────────────

    /// <summary>O modelo ativo de um tipo, ou nulo.</summary>
    internal static Task<PeDocModelo?> ModeloAtivoAsync(AppDbContext context, string tipo) =>
        context.PeDocModelos.AsNoTracking().Where(m => m.Tipo == tipo && m.Ativo).OrderBy(m => m.Id).FirstOrDefaultAsync();

    internal static ApiException ModeloDoDocumentoIndisponivel() => new(ErrorCode.PeModeloIndisponivel,
        "O modelo do documento ainda não foi carregado. Tente de novo em alguns minutos.");

    /// <summary>O que a resolução usa: o PDTIC, o órgão, a trilha, o modelo e a cópia do órgão.</summary>
    internal sealed class Base
    {
        public required PePdtic Pdtic { get; init; }
        public required PgiaOrgao Orgao { get; init; }
        public required PeTrilhaOrgao Trilha { get; init; }
        public required PeDocModelo Modelo { get; init; }

        // Na ordem do documento: o capítulo e, logo depois, os subcapítulos dele
        public required List<(PeDocCapitulo Capitulo, int Nivel)> Arvore { get; init; }
        public required List<PeDocCapitulo> Capitulos { get; init; }
        public required List<PeDocBloco> Blocos { get; init; }
        public required Dictionary<long, PeDocOrgao> CopiaCapitulos { get; init; }
        public required Dictionary<long, PeDocOrgaoBloco> CopiaBlocos { get; init; }

        /// <summary>O capítulo aparece para o órgão: o passo dele está na trilha e o pai aparece sem estar oculto.</summary>
        public bool Presente(PeDocCapitulo capitulo)
        {
            if (capitulo.PassoChave != null && Trilha.Passos.All(p => p.Chave != capitulo.PassoChave)) return false;
            if (capitulo.PaiId == null) return true;
            var pai = Capitulos.FirstOrDefault(c => c.Id == capitulo.PaiId);
            return pai != null && Presente(pai) && !Oculto(pai);
        }

        /// <summary>Oculto pelo órgão (só vale no capítulo opcional; o obrigatório sempre aparece).</summary>
        public bool Oculto(PeDocCapitulo capitulo) =>
            !capitulo.Obrigatorio && !capitulo.Travado && CopiaCapitulos.TryGetValue(capitulo.Id, out var copia) && copia.Oculto;
    }

    internal async Task<Base> BaseAsync(PePdtic pdtic)
    {
        var orgao = await _context.PgiaOrgaos.AsNoTracking().FirstAsync(o => o.Id == pdtic.OrgaoId);
        var modelo = await ModeloAtivoAsync(_context, PeDominios.TipoDocumento.Pdtic) ?? throw ModeloDoDocumentoIndisponivel();
        var trilha = await PeTrilhaOrgao.CarregarAsync(_context, pdtic.OrgaoId, soAtivo: false);
        var capitulos = await _context.PeDocCapitulos.AsNoTracking()
            .Where(c => c.ModeloId == modelo.Id && c.ExcluidoEm == null)
            .ToListAsync();
        var ids = capitulos.Select(c => c.Id).ToList();
        var blocos = await _context.PeDocBlocos.AsNoTracking()
            .Where(b => ids.Contains(b.CapituloId) && b.ExcluidoEm == null)
            .OrderBy(b => b.Ordem).ThenBy(b => b.Id)
            .ToListAsync();
        return new Base
        {
            Pdtic = pdtic,
            Orgao = orgao,
            Trilha = trilha,
            Modelo = modelo,
            Arvore = Arvore(capitulos),
            Capitulos = capitulos,
            Blocos = blocos,
            CopiaCapitulos = await _context.PeDocOrgaos.AsNoTracking().Where(o => o.PdticId == pdtic.Id).ToDictionaryAsync(o => o.CapituloId),
            CopiaBlocos = await _context.PeDocOrgaoBlocos.AsNoTracking().Where(o => o.PdticId == pdtic.Id).ToDictionaryAsync(o => o.BlocoId)
        };
    }

    /// <summary>Os capítulos na ordem do documento (o subcapítulo cujo pai foi apagado não entra).</summary>
    internal static List<(PeDocCapitulo Capitulo, int Nivel)> Arvore(IReadOnlyList<PeDocCapitulo> capitulos)
    {
        var saida = new List<(PeDocCapitulo, int)>();
        foreach (var capitulo in capitulos.Where(c => c.PaiId == null).OrderBy(c => c.Ordem).ThenBy(c => c.Id))
        {
            saida.Add((capitulo, 1));
            foreach (var sub in capitulos.Where(c => c.PaiId == capitulo.Id).OrderBy(c => c.Ordem).ThenBy(c => c.Id))
                saida.Add((sub, 2));
        }
        return saida;
    }

    /// <summary>O documento resolvido e o que o PDF usa a mais.</summary>
    internal sealed class Resolvido
    {
        public required PgiaOrgao Orgao { get; init; }
        public required PeDocumentoResponse Resposta { get; init; }
        public required IReadOnlyDictionary<string, string?> Marcadores { get; init; }
        public long? LogotipoId { get; init; }
        public Dictionary<long, PeDocCapituloResponse> Capitulos { get; } = new();
        public Dictionary<long, PeDocBlocoResponse> Blocos { get; } = new();
    }

    /// <summary>
    /// Resolve o documento do órgão. Com soBloco ou soCapitulo, só aquele bloco (ou os blocos
    /// daquele capítulo) é resolvido com os dados; a numeração considera o documento inteiro.
    /// </summary>
    internal async Task<Resolvido> ResolverAsync(PePdtic pdtic, PeUserContext ctx, long? soBloco = null, long? soCapitulo = null)
    {
        var base_ = await BaseAsync(pdtic);
        var trilha = base_.Trilha;
        var (dicionario, logotipo) = await DicionarioAsync(_registros, pdtic, trilha);
        var marcadores = Marcadores(pdtic, base_.Orgao, dicionario, await AprovacoesAsync(_context, _registros, pdtic, trilha));

        bool Resolve(PeDocBloco bloco) =>
            (soBloco == null || bloco.Id == soBloco) && (soCapitulo == null || bloco.CapituloId == soCapitulo);

        // Capítulos presentes, com o número pela posição entre os visíveis
        var presentes = base_.Arvore.Where(x => base_.Presente(x.Capitulo)).ToList();
        var numeros = new Dictionary<long, string?>();
        var contador = 0;
        var contadorDoPai = new Dictionary<long, int>();
        foreach (var (capitulo, nivel) in presentes)
        {
            string? numero = null;
            if (capitulo.Numerado && !base_.Oculto(capitulo))
            {
                if (nivel == 1)
                {
                    numero = (++contador).ToString(CultureInfo.InvariantCulture);
                }
                else if (numeros.GetValueOrDefault(capitulo.PaiId!.Value) is string doPai)
                {
                    var n = contadorDoPai.GetValueOrDefault(capitulo.PaiId.Value) + 1;
                    contadorDoPai[capitulo.PaiId.Value] = n;
                    numero = $"{doPai}.{n.ToString(CultureInfo.InvariantCulture)}";
                }
            }
            numeros[capitulo.Id] = numero;
        }

        // Os dados de uma vez: as seções que os blocos a resolver usam e que o órgão vê
        var aResolver = presentes
            .Where(x => !base_.Oculto(x.Capitulo))
            .SelectMany(x => base_.Blocos.Where(b => b.CapituloId == x.Capitulo.Id))
            .Where(Resolve)
            .ToList();
        var chaves = new HashSet<string>();
        var chavesDosFluxos = new HashSet<string>();
        var comPgia = false;
        foreach (var bloco in aResolver)
        {
            var config = PeDocConfig.Ler(bloco.Config);
            switch (bloco.Tipo)
            {
                case PeDominios.TipoBloco.Fluxo when PeDocConfig.Fluxo(config) is string fluxo:
                    chavesDosFluxos.Add(fluxo);
                    break;
                case PeDominios.TipoBloco.TabelaSecao when PeDocConfig.Secao(config) is string secao:
                    if (secao == PeDocConfig.SecaoPgia) comPgia = true;
                    else chaves.Add(secao);
                    break;
                case PeDominios.TipoBloco.ListaTema:
                    chaves.Add(PeDominios.TemaDecreto.SecaoAcoes);
                    chaves.Add(PeDominios.TemaDecreto.SecaoJustificativas);
                    break;
                case PeDominios.TipoBloco.MatrizSwot:
                    foreach (var swot in PeDominios.SecoesSwot.Todas) chaves.Add(swot);
                    break;
            }
        }
        var montadas = chaves.Select(c => trilha.Secao(c)).Where(s => s != null).Select(s => trilha.Montar(s!.Value.Secao)).ToList();
        var dados = montadas.Count == 0
            ? new Dictionary<string, PeSecaoExportada>()
            : (await _registros.ExportarAsync(PeDono.DoPdtic(pdtic.Id), montadas)).ToDictionary(s => s.Modelo.Secao.Chave);
        var sistemasIa = comPgia
            ? await _context.PgiaSistemasIa.AsNoTracking()
                .Where(s => s.OrgaoId == pdtic.OrgaoId)
                .OrderBy(s => s.Denominacao).ThenBy(s => s.Id)
                .ToListAsync()
            : new List<PgiaSistemaIa>();
        var fluxos = await FluxosAsync(pdtic, chavesDosFluxos, marcadores, trilha);

        var resposta = new PeDocumentoResponse
        {
            PdticId = pdtic.Id,
            Versao = pdtic.Versao,
            OrgaoSigla = base_.Orgao.Sigla,
            OrgaoNome = base_.Orgao.Nome,
            Titulo = PeDominios.TipoDocumento.Titulo(base_.Modelo.Tipo),
            PodeEditar = _permissoes.PodeEditarPdtic(ctx, pdtic.OrgaoId) && PeEdicaoPdtic.ElaboracaoAberta(pdtic),
            Logotipo = logotipo is long logo ? $"api/planejamento/arquivos/{logo.ToString(CultureInfo.InvariantCulture)}" : null,
            Versoes = await VersoesDoPdticAsync(pdtic.Id)
        };
        var resolvido = new Resolvido { Orgao = base_.Orgao, Resposta = resposta, Marcadores = marcadores, LogotipoId = logotipo };

        foreach (var (capitulo, nivel) in presentes)
        {
            base_.CopiaCapitulos.TryGetValue(capitulo.Id, out var copia);
            var oculto = base_.Oculto(capitulo);
            var passo = capitulo.PassoChave == null ? null : trilha.Passos.FirstOrDefault(p => p.Chave == capitulo.PassoChave);
            var item = new PeDocCapituloResponse
            {
                Id = capitulo.Id,
                Chave = capitulo.Chave,
                Numero = numeros[capitulo.Id],
                Nivel = nivel,
                Titulo = copia?.TituloProprio ?? capitulo.Titulo,
                TituloModelo = capitulo.Titulo,
                TituloProprio = copia?.TituloProprio,
                Oculto = oculto,
                Obrigatorio = capitulo.Obrigatorio || capitulo.Travado,
                Travado = capitulo.Travado,
                IncisoDecreto = capitulo.IncisoDecreto,
                PassoChave = capitulo.PassoChave,
                PassoNumero = passo?.Numero
            };
            if (!oculto)
            {
                foreach (var bloco in base_.Blocos.Where(b => b.CapituloId == capitulo.Id && Resolve(b)))
                {
                    var resolvidoDoBloco = Bloco(bloco, base_, marcadores, dados, sistemasIa, fluxos);
                    if (resolvidoDoBloco == null) continue;
                    item.Blocos.Add(resolvidoDoBloco);
                    resolvido.Blocos[bloco.Id] = resolvidoDoBloco;
                }
            }
            resposta.Capitulos.Add(item);
            resolvido.Capitulos[capitulo.Id] = item;
        }
        return resolvido;
    }

    /// <summary>
    /// Os valores do dicionário de nomes (campos de texto visíveis do registro do passo 1.2) pela
    /// chave do marcador ("nomes.comite"), e o id do logotipo (campo de arquivo visível). Os
    /// fluxos (E6) usam os mesmos valores nas raias e nos passos.
    /// </summary>
    internal static async Task<(Dictionary<string, string?> Valores, long? Logotipo)> DicionarioAsync(IPeRegistroService registros,
        PePdtic pdtic, PeTrilhaOrgao trilha)
    {
        var valores = new Dictionary<string, string?>();
        var secao = trilha.Dados.SecaoPorChave(PeDominios.DicionarioNomes.Secao);
        if (secao == null) return (valores, null);
        foreach (var campo in trilha.Dados.CamposDaSecao(secao.Id, incluirExcluidos: false).Where(PeDocMarcadores.EhDeTexto))
            valores["nomes." + campo.Chave] = null;

        var visivel = trilha.Secao(PeDominios.DicionarioNomes.Secao);
        if (visivel == null) return (valores, null);
        var exportada = (await registros.ExportarAsync(PeDono.DoPdtic(pdtic.Id), new[] { trilha.Montar(visivel.Value.Secao) }))[0];
        var registro = exportada.Registros.FirstOrDefault();
        if (registro == null) return (valores, null);

        long? logotipo = null;
        foreach (var coluna in exportada.Colunas)
        {
            var campo = coluna.Campo;
            if (PeDocMarcadores.EhDeTexto(campo) && registro.Rotulos.TryGetValue(campo.Chave, out var texto) && !string.IsNullOrWhiteSpace(texto))
                valores["nomes." + campo.Chave] = texto.Trim();
            if (campo.Chave == PeDominios.DicionarioNomes.Logotipo && campo.Tipo == PeDominios.TipoCampo.Arquivo
                && registro.Dados.TryGetValue(campo.Chave, out var arquivo))
                logotipo = PeRegistroDados.ArquivoId(JsonNode.Parse(arquivo.GetRawText()));
        }
        return (valores, logotipo);
    }

    /// <summary>
    /// Todos os marcadores conhecidos com o valor para o órgão (nulo = sem valor). Os de
    /// aprovação e de publicação (E7) vêm em aprovacoes (sem ele, ficam sem valor).
    /// </summary>
    public static Dictionary<string, string?> Marcadores(PePdtic pdtic, PgiaOrgao orgao, IReadOnlyDictionary<string, string?> dicionario,
        IReadOnlyDictionary<string, string?>? aprovacoes = null)
    {
        var valores = new Dictionary<string, string?>(dicionario);
        foreach (var marcador in PeDocMarcadores.Fixos) valores.TryAdd(marcador.Chave, null);
        foreach (var (apelido, campo) in PeDocMarcadores.Apelidos) valores[apelido] = dicionario.GetValueOrDefault("nomes." + campo);

        valores["orgao.nome"] = orgao.Nome;
        var sigla = dicionario.GetValueOrDefault("nomes." + PeDominios.DicionarioNomes.SiglaOrgao);
        valores["orgao.sigla"] = string.IsNullOrWhiteSpace(sigla) ? orgao.Sigla : sigla;
        valores["vigencia.inicio"] = pdtic.VigenciaInicio is DateOnly inicio ? PeFormato.Data(inicio) : null;
        valores["vigencia.fim"] = pdtic.VigenciaFim is DateOnly fim ? PeFormato.Data(fim) : null;
        valores["pdtic.versao"] = pdtic.Versao;
        valores["hoje"] = PeFormato.Data(DateOnly.FromDateTime(DateTimeHelper.TodayBrasilia()));
        foreach (var chave in PeDocMarcadores.DaAprovacao) valores[chave] = aprovacoes?.GetValueOrDefault(chave);
        return valores;
    }

    /// <summary>
    /// Os valores dos marcadores de aprovação e de publicação (E7): a aprovação do SGTIC (a
    /// seção do passo do envio, só com a decisão "aprovado"), a deliberação aprovada do CGTIC
    /// (a mais recente) e a publicação (a seção do passo 3.13). O ato sai como "Ata de reunião
    /// nº 3/2027" (o tipo e o número; um só, quando falta o outro).
    /// </summary>
    internal static async Task<Dictionary<string, string?>> AprovacoesAsync(AppDbContext context, IPeRegistroService registros,
        PePdtic pdtic, PeTrilhaOrgao trilha)
    {
        var valores = PeDocMarcadores.DaAprovacao.ToDictionary(c => c, _ => (string?)null);

        var secoes = new[] { PeDominios.ChavePdtic.SecaoAprovacaoSgtic, PeDominios.ChavePdtic.SecaoPublicacao }
            .Select(chave => trilha.Secao(chave))
            .Where(s => s != null)
            .Select(s => trilha.Montar(s!.Value.Secao))
            .ToList();
        var exportadas = secoes.Count == 0
            ? new Dictionary<string, PeRegistroResponse?>()
            : (await registros.ExportarAsync(PeDono.DoPdtic(pdtic.Id), secoes))
                .ToDictionary(s => s.Modelo.Secao.Chave, s => s.Registros.FirstOrDefault());
        string? Rotulo(PeRegistroResponse? registro, string campo) =>
            registro != null && registro.Rotulos.TryGetValue(campo, out var texto) && !string.IsNullOrWhiteSpace(texto) ? texto.Trim() : null;

        var sgtic = exportadas.GetValueOrDefault(PeDominios.ChavePdtic.SecaoAprovacaoSgtic);
        if (sgtic != null && sgtic.Dados.TryGetValue(PeDominios.ChavePdtic.CampoDecisao, out var decisao)
            && decisao.ValueKind == System.Text.Json.JsonValueKind.String && decisao.GetString() == PeDominios.Decisao.Aprovado)
        {
            valores["aprovacao.sgtic.data"] = Rotulo(sgtic, PeDominios.ChavePdtic.CampoData);
            valores["aprovacao.sgtic.ato"] = Ato(Rotulo(sgtic, PeDominios.ChavePdtic.CampoAtoTipo), Rotulo(sgtic, PeDominios.ChavePdtic.CampoAtoNumero));
        }

        var cgtic = await context.PeDeliberacoes.AsNoTracking()
            .Where(d => d.ObjetoTipo == PeDominios.ObjetoDeliberacao.Pdtic && d.ObjetoId == pdtic.Id
                        && d.Situacao == PeDominios.SituacaoDeliberacao.Aprovado)
            .OrderByDescending(d => d.Id)
            .FirstOrDefaultAsync();
        if (cgtic != null)
        {
            valores["aprovacao.cgtic.data"] = cgtic.AtoData is DateOnly data ? PeFormato.Data(data) : null;
            valores["aprovacao.cgtic.ato"] = Ato(cgtic.AtoTipo, cgtic.AtoNumero);
        }

        var publicacao = exportadas.GetValueOrDefault(PeDominios.ChavePdtic.SecaoPublicacao);
        valores["publicacao.data"] = Rotulo(publicacao, PeDominios.ChavePdtic.CampoData);
        valores["publicacao.endereco"] = Rotulo(publicacao, PeDominios.ChavePdtic.CampoEndereco);
        return valores;
    }

    /// <summary>"Resolução nº 12/2027"; só o tipo ou "nº 12/2027" quando falta o outro; nulo sem nenhum.</summary>
    public static string? Ato(string? tipo, string? numero)
    {
        var t = tipo?.Trim();
        var n = numero?.Trim();
        if (string.IsNullOrEmpty(n)) return string.IsNullOrEmpty(t) ? null : t;
        return string.IsNullOrEmpty(t) ? $"nº {n}" : $"{t} nº {n}";
    }

    /// <summary>
    /// Os desenhos dos fluxos que o documento mostra (E6). Sem as tabelas dos fluxos (o intervalo
    /// entre o PR e a migration), o documento sai sem os desenhos, em vez de falhar inteiro.
    /// </summary>
    private async Task<Dictionary<string, PeFluxoService.ParaDocumento>> FluxosAsync(PePdtic pdtic, IReadOnlyCollection<string> chaves,
        IReadOnlyDictionary<string, string?> marcadores, PeTrilhaOrgao trilha)
    {
        if (chaves.Count == 0) return new Dictionary<string, PeFluxoService.ParaDocumento>();
        try
        {
            return await PeFluxoService.ParaDocumentoAsync(_context, pdtic.Id, chaves, marcadores, PeFluxoService.CamposDoDicionario(trilha.Dados));
        }
        catch (Exception ex) when (PeBanco.TabelaAusente(ex))
        {
            return new Dictionary<string, PeFluxoService.ParaDocumento>();
        }
    }

    /// <summary>
    /// Largura (em unidades do desenho) acima da qual o fluxo vai para uma página deitada no PDF:
    /// em pé, ele sairia pequeno demais para ler.
    /// </summary>
    public const double LarguraDoFluxoEmPe = 760;

    // ── Blocos ──────────────────────────────────────────────────────────────

    private static PeDocBlocoResponse? Bloco(PeDocBloco bloco, Base base_, IReadOnlyDictionary<string, string?> marcadores,
        IReadOnlyDictionary<string, PeSecaoExportada> dados, IReadOnlyList<PgiaSistemaIa> sistemasIa,
        IReadOnlyDictionary<string, PeFluxoService.ParaDocumento> fluxos)
    {
        var config = PeDocConfig.Ler(bloco.Config);
        var resposta = new PeDocBlocoResponse
        {
            Id = bloco.Id,
            Tipo = bloco.Tipo,
            Ordem = bloco.Ordem,
            PaginaDeitada = PeDocConfig.PaginaDeitada(config)
        };

        switch (bloco.Tipo)
        {
            case PeDominios.TipoBloco.Texto:
                Texto(resposta, bloco, config, base_, marcadores);
                break;
            case PeDominios.TipoBloco.TabelaSecao:
                resposta.Tabela = PeDocConfig.Secao(config) == PeDocConfig.SecaoPgia
                    ? TabelaDoPgia(config, base_.Trilha, sistemasIa)
                    : Tabela(config, base_.Trilha, dados);
                if (resposta.Tabela == null) return null;
                break;
            case PeDominios.TipoBloco.ListaTema:
                resposta.Lista = Lista(config, base_.Trilha, dados);
                if (resposta.Lista == null) return null;
                break;
            case PeDominios.TipoBloco.MatrizSwot:
                resposta.Swot = Swot(dados);
                if (resposta.Swot == null) return null;
                break;
            case PeDominios.TipoBloco.Fluxo:
            {
                var chave = PeDocConfig.Fluxo(config) ?? string.Empty;
                if (fluxos.TryGetValue(chave, out var desenho))
                {
                    resposta.Fluxo = new PeDocFluxoResponse
                    {
                        Chave = chave,
                        Nome = desenho.Nome,
                        Svg = desenho.Svg,
                        Personalizado = desenho.Personalizado,
                        Descricao = desenho.Descricao
                    };
                    // Fluxo largo vai para a página deitada, mesmo sem a marca do bloco
                    if (desenho.Largura > LarguraDoFluxoEmPe) resposta.PaginaDeitada = true;
                }
                else
                {
                    resposta.Fluxo = new PeDocFluxoResponse { Chave = chave, Nome = NomesDosFluxos.GetValueOrDefault(chave) ?? chave, Svg = null };
                }
                break;
            }
        }
        return resposta;
    }

    private static void Texto(PeDocBlocoResponse resposta, PeDocBloco bloco, JsonObject config, Base base_,
        IReadOnlyDictionary<string, string?> marcadores)
    {
        var doModelo = PeDocConfig.TextoDoBloco(config);
        base_.CopiaBlocos.TryGetValue(bloco.Id, out var copia);
        var bruto = copia != null ? JsonNode.Parse(copia.Texto) : doModelo;

        resposta.TextoBruto = Elemento(bruto);
        resposta.TextoResolvido = Elemento(PeDocMarcadores.Resolver(bruto, marcadores, emBranco: false));
        resposta.MarcadoresSemValor = PeDocMarcadores.SemValor(bruto, marcadores);
        resposta.EditadoPeloOrgao = copia != null;
        if (copia == null) return;

        resposta.EditadoEm = copia.EditadoEm;
        resposta.EditadoPor = copia.EditadoPor;
        resposta.ModeloMudou = PeDocMarcadores.Hash(doModelo) != copia.ModeloHash;
        if (resposta.ModeloMudou) resposta.TextoModeloAtual = Elemento(PeDocMarcadores.Resolver(doModelo, marcadores, emBranco: false));
    }

    private static JsonElement? Elemento(JsonNode? no) => no == null ? null : JsonSerializer.SerializeToElement(no);

    /// <summary>
    /// A tabela de uma seção que o órgão vê e que vai para o documento: as colunas escolhidas no
    /// bloco (entre as visíveis) ou todas as visíveis marcadas "no documento", e as linhas que
    /// passam no filtro, com o texto pronto ("-" quando vazio).
    /// </summary>
    private static PeDocTabelaResponse? Tabela(JsonObject config, PeTrilhaOrgao trilha, IReadOnlyDictionary<string, PeSecaoExportada> dados)
    {
        var chave = PeDocConfig.Secao(config);
        if (chave == null) return null;
        var visivel = trilha.Secao(chave);
        if (visivel == null || !dados.TryGetValue(chave, out var exportada) || !exportada.Modelo.Secao.NoDocumento) return null;

        var escolhidas = PeDocConfig.Colunas(config);
        var colunas = escolhidas == null
            ? exportada.Colunas.Where(v => v.Campo.NoDocumento).Select(v => v.Campo).ToList()
            : escolhidas.Select(k => exportada.Colunas.FirstOrDefault(v => v.Campo.Chave == k)?.Campo)
                .Where(c => c != null).Select(c => c!).ToList();

        IEnumerable<PeRegistroResponse> registros = exportada.Registros;
        if (PeDocConfig.Filtro(config) is { } filtro)
        {
            var campoVisivel = exportada.Colunas.Any(v => v.Campo.Chave == filtro.Campo);
            registros = registros.Where(r =>
            {
                var valor = campoVisivel && r.Dados.TryGetValue(filtro.Campo, out var el) ? JsonNode.Parse(el.GetRawText()) : null;
                var bate = PeDocConfig.Bate(valor, filtro.Valor);
                return filtro.Excluir ? !bate : bate;
            });
        }

        var linhas = registros.Select(r =>
        {
            var linha = new PeDocLinhaResponse { Codigo = r.Codigo };
            foreach (var campo in colunas)
            {
                linha.Celulas[campo.Chave] = r.Rotulos.TryGetValue(campo.Chave, out var texto) && !string.IsNullOrWhiteSpace(texto) ? texto : "-";
                if (campo.Tipo == PeDominios.TipoCampo.TextoRico && r.Dados.TryGetValue(campo.Chave, out var rico))
                    (linha.Ricos ??= new Dictionary<string, JsonElement>())[campo.Chave] = rico;
            }
            return linha;
        }).ToList();

        return new PeDocTabelaResponse
        {
            SecaoChave = chave,
            SecaoTitulo = exportada.Modelo.Secao.Titulo,
            SecaoTipo = exportada.Modelo.Secao.Tipo,
            PassoNumero = visivel.Value.Passo.Numero,
            Colunas = colunas.Select(c => new PeDocColunaResponse { Chave = c.Chave, Rotulo = c.Rotulo }).ToList(),
            Linhas = linhas,
            Vazia = linhas.Count == 0
        };
    }

    /// <summary>O inventário de IA do PGIA do órgão (só leitura), com as colunas escolhidas no bloco.</summary>
    private static PeDocTabelaResponse TabelaDoPgia(JsonObject config, PeTrilhaOrgao trilha, IReadOnlyList<PgiaSistemaIa> sistemas)
    {
        var escolhidas = PeDocConfig.Colunas(config);
        var colunas = PeDocConfig.ColunasPgia.Where(c => escolhidas == null || escolhidas.Contains(c.Chave))
            .OrderBy(c => escolhidas == null ? 0 : escolhidas.IndexOf(c.Chave))
            .ToList();
        static string Valor(string? texto) => string.IsNullOrWhiteSpace(texto) ? "-" : texto.Trim();
        var linhas = sistemas.Select(s =>
        {
            var celulas = new Dictionary<string, string>
            {
                ["nome"] = Valor(s.Denominacao),
                ["finalidade"] = Valor(s.Finalidade),
                ["classificacao"] = Valor(s.ClassificacaoRiscoAtual),
                ["base"] = Valor(s.EnquadramentoLegal),
                ["situacao"] = Valor(s.StatusCicloVida)
            };
            return new PeDocLinhaResponse { Codigo = null, Celulas = colunas.ToDictionary(c => c.Chave, c => celulas[c.Chave]) };
        }).ToList();
        return new PeDocTabelaResponse
        {
            SecaoChave = PeDocConfig.SecaoPgia,
            SecaoTitulo = "Sistemas de IA no inventário do PGIA",
            SecaoTipo = PeDominios.TipoSecao.Tabela,
            PassoNumero = trilha.Passos.FirstOrDefault(p => p.Chave == PassoDosSistemasDeIa)?.Numero,
            Colunas = colunas.Select(c => new PeDocColunaResponse { Chave = c.Chave, Rotulo = c.Rotulo }).ToList(),
            Linhas = linhas,
            Vazia = linhas.Count == 0
        };
    }

    /// <summary>As ações do PDTIC marcadas com o tema; sem ação, a justificativa do passo 3.4 (temas do decreto).</summary>
    private static PeDocListaResponse? Lista(JsonObject config, PeTrilhaOrgao trilha, IReadOnlyDictionary<string, PeSecaoExportada> dados)
    {
        var tema = PeDocConfig.Tema(config);
        if (tema == null || !dados.TryGetValue(PeDominios.TemaDecreto.SecaoAcoes, out var acoes)) return null;

        var campoTema = acoes.Modelo.Campos.FirstOrDefault(c => c.Chave == PeDominios.TemaDecreto.CampoTema);
        var rotulo = campoTema == null ? tema : acoes.Modelo.OpcoesDe(campoTema).FirstOrDefault(o => o.Valor == tema)?.Rotulo ?? tema;
        var alvo = JsonValue.Create(tema);
        var itens = acoes.Registros
            .Where(r => r.Dados.TryGetValue(PeDominios.TemaDecreto.CampoTema, out var el) && PeDocConfig.Bate(JsonNode.Parse(el.GetRawText()), alvo))
            .Select(r => new PeDocListaItemResponse
            {
                Codigo = r.Codigo,
                Texto = r.Rotulos.GetValueOrDefault(PeDominios.TemaDecreto.CampoDescricao) ?? string.Empty,
                Situacao = r.Rotulos.GetValueOrDefault(PeDominios.TemaDecreto.CampoSituacao)
            })
            .ToList();

        string? justificativa = null;
        if (itens.Count == 0
            && PeDominios.TemaDecreto.Todos.FirstOrDefault(t => t.Valor == tema) is { } doDecreto
            && dados.TryGetValue(PeDominios.TemaDecreto.SecaoJustificativas, out var justificativas)
            && justificativas.Registros.FirstOrDefault() is { } registro
            && registro.Rotulos.TryGetValue(doDecreto.CampoJustificativa, out var texto)
            && !string.IsNullOrWhiteSpace(texto))
            justificativa = texto.Trim();

        return new PeDocListaResponse { Tema = rotulo, Itens = itens, Justificativa = justificativa };
    }

    /// <summary>A matriz SWOT pelas quatro seções que o órgão vê ("F01 · texto"); nenhuma visível = sem bloco.</summary>
    private static PeDocSwotResponse? Swot(IReadOnlyDictionary<string, PeSecaoExportada> dados)
    {
        if (!PeDominios.SecoesSwot.Todas.Any(dados.ContainsKey)) return null;
        List<string> Itens(string chave) => dados.TryGetValue(chave, out var secao)
            ? secao.Registros
                .Select(r => (r.Codigo, Texto: r.Rotulos.GetValueOrDefault(PeDominios.SecoesSwot.CampoTexto)?.Trim()))
                .Where(x => !string.IsNullOrEmpty(x.Texto))
                .Select(x => x.Codigo == null ? x.Texto! : $"{x.Codigo} · {x.Texto}")
                .ToList()
            : new List<string>();
        return new PeDocSwotResponse
        {
            Forcas = Itens(PeDominios.SecoesSwot.Forcas),
            Fraquezas = Itens(PeDominios.SecoesSwot.Fraquezas),
            Oportunidades = Itens(PeDominios.SecoesSwot.Oportunidades),
            Ameacas = Itens(PeDominios.SecoesSwot.Ameacas)
        };
    }
}
