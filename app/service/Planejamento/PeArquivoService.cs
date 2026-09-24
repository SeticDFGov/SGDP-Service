using System.Security.Cryptography;
using api.Planejamento;
using Microsoft.EntityFrameworkCore;
using Models;
using Models.Planejamento;
using service.Interface;
using service.Pgia;

namespace service.Planejamento;

/// <summary>
/// Anexos do módulo Governança Estratégica, com o binário no próprio banco (padrão dos
/// anexos do PGIA). Regras do envio:
/// <list type="bullet">
/// <item>até 25 MB; imagens (PNG e JPEG) até 5 MB;</item>
/// <item>lista fechada de extensões: pdf, png, jpg e jpeg (os tipos que um campo de arquivo aceita);</item>
/// <item>o conteúdo precisa ser mesmo do tipo da extensão (assinatura do PDF, do PNG e do JPEG);</item>
/// <item>o tipo servido sai da extensão, nunca do que o navegador mandou; o nome é saneado
/// (sem caminho) e só serve para exibir.</item>
/// </list>
/// O arquivo nasce sem dono; ganha dono quando um registro o usa num campo de arquivo (ou,
/// desde a E4, como imagem de um texto rico). Baixa quem pode ver o dono do registro
/// (PETIC-DF e catálogo do DF: qualquer papel do módulo; PDTIC: quem vê o órgão); sem dono
/// (ou com o registro apagado), só quem enviou.
/// </summary>
public class PeArquivoService : IPeArquivoService
{
    public const long TamanhoMaximoBytes = 25L * 1024 * 1024;
    public const long TamanhoMaximoImagemBytes = 5L * 1024 * 1024;

    /// <summary>Folga do multipart no [RequestSizeLimit] (cabeçalhos e delimitadores além do binário).</summary>
    public const long LimiteRequisicaoBytes = TamanhoMaximoBytes + (1L * 1024 * 1024);

    /// <summary>Extensão aceita, o tipo do campo (config.tipos) e o MIME servido.</summary>
    private static readonly IReadOnlyDictionary<string, (string Tipo, string Mime)> Extensoes =
        new Dictionary<string, (string, string)>(StringComparer.Ordinal)
        {
            [".pdf"] = ("pdf", "application/pdf"),
            [".png"] = ("png", "image/png"),
            [".jpg"] = ("jpg", "image/jpeg"),
            [".jpeg"] = ("jpg", "image/jpeg")
        };

    private readonly AppDbContext _context;
    private readonly IPePermissionService _permissoes;

    public PeArquivoService(AppDbContext context, IPePermissionService permissoes)
    {
        _context = context;
        _permissoes = permissoes;
    }

    /// <summary>O tipo do campo de arquivo ("pdf", "png", "jpg") pela extensão do nome, ou nulo.</summary>
    public static string? TipoDoArquivo(string? nome) =>
        Extensoes.TryGetValue(Path.GetExtension(nome ?? string.Empty).ToLowerInvariant(), out var e) ? e.Tipo : null;

    public async Task<PeArquivoResponse> EnviarAsync(PeArquivoUpload upload, PeUserContext ctx, CancellationToken ct = default)
    {
        if (!_permissoes.PodeEnviarArquivo(ctx))
            throw new ApiException(ErrorCode.PeSemPermissao, "Seu papel no módulo não envia arquivos.");
        if (upload.Tamanho <= 0) throw Invalido("Envie um arquivo no campo \"arquivo\".");
        if (upload.Tamanho > TamanhoMaximoBytes) throw Invalido("O arquivo passa de 25 MB.");

        var nome = PgiaDocumentoService.SanearNomeArquivo(upload.NomeOriginal);
        if (string.IsNullOrWhiteSpace(nome)) throw Invalido("O arquivo enviado não tem nome.");
        var extensao = Path.GetExtension(nome).ToLowerInvariant();
        if (!Extensoes.TryGetValue(extensao, out var tipo))
            throw Invalido($"Tipo de arquivo não aceito ({(extensao.Length == 0 ? "sem extensão" : extensao)}). Envie PDF, PNG ou JPEG.");

        // O tamanho real manda (o do formulário é só uma dica)
        byte[] conteudo;
        using (var memoria = new MemoryStream())
        {
            await upload.Conteudo.CopyToAsync(memoria, ct);
            conteudo = memoria.ToArray();
        }
        if (conteudo.Length == 0) throw Invalido("O arquivo está vazio.");
        if (conteudo.Length > TamanhoMaximoBytes) throw Invalido("O arquivo passa de 25 MB.");
        if (tipo.Tipo != "pdf" && conteudo.Length > TamanhoMaximoImagemBytes) throw Invalido("A imagem passa de 5 MB.");
        if (!ConteudoBate(tipo.Tipo, conteudo))
            throw Invalido($"O conteúdo do arquivo não é {tipo.Tipo.ToUpperInvariant()}, apesar do nome. Confira o arquivo e envie de novo.");

        var arquivo = new PeArquivo
        {
            Nome = nome,
            TipoMime = tipo.Mime,
            Tamanho = conteudo.Length,
            Hash = Convert.ToHexString(SHA256.HashData(conteudo)).ToLowerInvariant(),
            CriadoEm = DateTime.UtcNow,
            CriadoPor = ctx.Email
        };
        _context.PeArquivosConteudo.Add(new PeArquivoConteudo { Arquivo = arquivo, Conteudo = conteudo });
        await _context.SaveChangesAsync(ct);

        return new PeArquivoResponse { Id = arquivo.Id, Nome = arquivo.Nome, TipoMime = arquivo.TipoMime, Tamanho = arquivo.Tamanho };
    }

    public async Task<PeArquivoDownload> BaixarAsync(long id, PeUserContext ctx)
    {
        var arquivo = await _context.PeArquivos.AsNoTracking().FirstOrDefaultAsync(a => a.Id == id)
            ?? throw new ApiException(ErrorCode.PeArquivoNaoEncontrado, "Arquivo não encontrado.");

        if (!await PodeBaixarAsync(arquivo, ctx))
            throw new ApiException(ErrorCode.PeSemPermissao, "Você não pode baixar este arquivo.");

        // Só o download seleciona o binário
        var conteudo = await _context.PeArquivosConteudo.AsNoTracking()
            .Where(c => c.Id == id)
            .Select(c => c.Conteudo)
            .FirstAsync();
        return new PeArquivoDownload(conteudo, arquivo.TipoMime, arquivo.Nome);
    }

    private async Task<bool> PodeBaixarAsync(PeArquivo arquivo, PeUserContext ctx)
    {
        var enviou = string.Equals(arquivo.CriadoPor.Trim(), ctx.Email.Trim(), StringComparison.OrdinalIgnoreCase);
        if (arquivo.DonoTipo != PeDominios.DonoArquivo.Registro || arquivo.DonoId == null) return enviou;

        var registro = await _context.PeRegistros.AsNoTracking()
            .Where(r => r.Id == arquivo.DonoId)
            .Select(r => new { r.PeticId, r.PdticId })
            .FirstOrDefaultAsync();
        if (registro == null) return enviou;
        if (enviou) return true;

        // PETIC-DF e catálogo do DF: qualquer papel do módulo lê, mas o papel de órgão só vê as
        // versões aprovadas do PETIC-DF. PDTIC (E4): quem vê o órgão dele
        if (!_permissoes.PodeLerReferenciais(ctx)) return false;
        if (registro.PdticId != null)
        {
            var orgaoId = await _context.PePdtics.AsNoTracking()
                .Where(p => p.Id == registro.PdticId)
                .Select(p => (long?)p.OrgaoId)
                .FirstOrDefaultAsync();
            return orgaoId != null && _permissoes.PodeVerOrgao(ctx, orgaoId.Value);
        }
        if (registro.PeticId == null) return true;
        var situacao = await _context.PePetics.AsNoTracking()
            .Where(p => p.Id == registro.PeticId)
            .Select(p => p.Situacao)
            .FirstOrDefaultAsync();
        return situacao != null && _permissoes.PodeVerVersaoPetic(ctx, situacao);
    }

    /// <summary>A assinatura do começo do arquivo bate com o tipo da extensão.</summary>
    private static bool ConteudoBate(string tipo, byte[] conteudo) => tipo switch
    {
        "pdf" => Comeca(conteudo, 0x25, 0x50, 0x44, 0x46, 0x2D),                    // %PDF-
        "png" => Comeca(conteudo, 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A),  // \x89PNG\r\n\x1a\n
        "jpg" => Comeca(conteudo, 0xFF, 0xD8, 0xFF),
        _ => false
    };

    private static bool Comeca(byte[] conteudo, params byte[] assinatura) =>
        conteudo.Length >= assinatura.Length && conteudo.AsSpan(0, assinatura.Length).SequenceEqual(assinatura);

    private static ApiException Invalido(string mensagem) => new(ErrorCode.PeArquivoInvalido, mensagem);
}
