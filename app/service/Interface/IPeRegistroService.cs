using api.Planejamento;
using service.Planejamento;

namespace service.Interface;

/// <summary>
/// Motor de registros do módulo Governança Estratégica (E3): grava os dados descritos pelo
/// modelo (seções e campos) para um dono (o catálogo do DF, uma versão do PETIC-DF ou, desde
/// a E4, o PDTIC de um órgão, pela trilha do órgão). Valida pelo modelo (só campos visíveis,
/// obrigatórios, tipos, opções ativas, texto rico pela lista fechada, ligações do mesmo dono
/// ou do catálogo), guarda o valor de campo desligado sem exigir (decisão 13), gera o código
/// pela sequência do dono, calcula os campos calculados e devolve os rótulos prontos. A
/// autorização fica aqui (quem lê, quem edita e se a versão ou o PDTIC está aberto), pelo
/// IPePermissionService. Erros como ApiException (1030 a 1049 e 1050 a 1069).
/// </summary>
public interface IPeRegistroService
{
    Task<PeRegistrosResponse> ListarAsync(PeDono dono, string secaoChave, PeUserContext ctx);

    /// <summary>Inclui (tabela) ou preenche pela primeira vez (formulário; já preenchido: 409).</summary>
    Task<PeRegistroResponse> CriarAsync(PeDono dono, string secaoChave, PeRegistroSalvarDTO dto, PeUserContext ctx);

    Task<PeRegistroResponse> AtualizarAsync(PeDono dono, string secaoChave, long id, PeRegistroSalvarDTO dto, PeUserContext ctx);

    /// <summary>Apaga; ligado por outro registro ou do sistema: 409.</summary>
    Task ExcluirAsync(PeDono dono, string secaoChave, long id, PeUserContext ctx);

    /// <summary>Nova ordem ({ Ids }, todos os registros da seção); devolve a lista.</summary>
    Task<PeRegistrosResponse> OrdenarAsync(PeDono dono, string secaoChave, PeOrdemDTO dto, PeUserContext ctx);

    /// <summary>
    /// Itens de um catálogo (petic_objetivo e petic_eixo da vigente; principio do DF;
    /// pgia_sistema, os sistemas de IA do inventário do PGIA do órgão do PDTIC pdticId).
    /// </summary>
    Task<List<PeCatalogoItemResponse>> CatalogoAsync(string catalogo, PeUserContext ctx, long? pdticId = null);

    /// <summary>
    /// O que falta para o dono ir ao CGTIC: seção obrigatória sem registro e registro com
    /// campo obrigatório vazio (pelo modelo de hoje). Lista vazia = pronto.
    /// </summary>
    Task<List<string>> PendenciasAsync(PeDono dono);

    /// <summary>
    /// Os registros das seções dadas e o que falta em cada um, mais as ligações que saem deles
    /// (situação dos passos e avisos do PDTIC). Não confere quem chama: quem chama já conferiu.
    /// </summary>
    Task<PeAnaliseDono> AnalisarAsync(PeDono dono, IReadOnlyList<PeSecaoDoDono> secoes);

    /// <summary>Uma seção para a planilha: as colunas (visíveis e marcadas "na planilha") e os registros.</summary>
    Task<PeSecaoExportada> ExportarSecaoAsync(PeDono dono, string secaoChave, PeUserContext ctx);

    /// <summary>Todas as seções do dono que vão para a planilha, na ordem.</summary>
    Task<List<PeSecaoExportada>> ExportarSecoesAsync(PeDono dono, PeUserContext ctx);

    /// <summary>
    /// Os registros de uma seção em vários PDTICs de uma vez (o consolidado), cada um com a
    /// seção como o órgão dele a vê; poucas consultas para todos os órgãos. Não confere quem
    /// chama: quem chama já conferiu.
    /// </summary>
    Task<Dictionary<long, List<PeRegistroResponse>>> ExportarDosPdticsAsync(long secaoId, IReadOnlyList<(long PdticId, PeSecaoDoDono Secao)> pdtics);

    /// <summary>
    /// Os registros de várias seções do dono de uma vez (o documento do PDTIC, E5), cada seção
    /// com todos os campos visíveis como colunas e os registros com os rótulos. Não confere quem
    /// chama: quem chama já conferiu.
    /// </summary>
    Task<List<PeSecaoExportada>> ExportarAsync(PeDono dono, IReadOnlyList<PeSecaoDoDono> secoes);
}

/// <summary>
/// Anexos do módulo (pe_arquivo): o envio (multipart) valida tamanho, extensão e conteúdo;
/// o download confere quem pode ver o dono do registro que aponta para o arquivo (sem
/// dono: só quem enviou).
/// </summary>
public interface IPeArquivoService
{
    Task<PeArquivoResponse> EnviarAsync(PeArquivoUpload upload, PeUserContext ctx, CancellationToken ct = default);

    Task<PeArquivoDownload> BaixarAsync(long id, PeUserContext ctx);
}

/// <summary>
/// Planilhas do PETIC-DF, do catálogo do DF e, desde a E4, do PDTIC de cada órgão (por seção
/// e completa) e do consolidado de todos os PDTICs atuais (papéis globais e admin geral): CSV
/// (UTF-8 com BOM, ponto e vírgula, proteção contra fórmula) e XLSX (cabeçalho destacado e
/// fixo, filtro, datas e valores como números, listas com validação e a aba Leia-me).
/// </summary>
public interface IPePlanilhaService
{
    Task<PePlanilhaArquivo> PeticSecaoAsync(long peticId, string secaoChave, string? formato, PeUserContext ctx);

    Task<PePlanilhaArquivo> PeticCompletaAsync(long peticId, string? formato, PeUserContext ctx);

    Task<PePlanilhaArquivo> DfSecaoAsync(string secaoChave, string? formato, PeUserContext ctx);

    /// <summary>Uma seção do PDTIC, com as colunas do nível do órgão (PDTIC_SIGLA_secao_data).</summary>
    Task<PePlanilhaArquivo> PdticSecaoAsync(long pdticId, string secaoChave, string? formato, PeUserContext ctx);

    /// <summary>O PDTIC inteiro, uma aba por seção visível (só xlsx).</summary>
    Task<PePlanilhaArquivo> PdticCompletaAsync(long pdticId, string? formato, PeUserContext ctx);

    /// <summary>
    /// Uma seção de todos os PDTICs atuais: as colunas do órgão (órgão, sigla, nível, versão e
    /// situação) antes dos campos, e os campos visíveis em qualquer nível ou órgão (célula vazia
    /// quando o campo não aparece para o órgão).
    /// </summary>
    Task<PePlanilhaArquivo> ConsolidadoSecaoAsync(string secaoChave, string? formato, PeUserContext ctx);

    /// <summary>O consolidado com todas as seções, uma aba por seção (só xlsx).</summary>
    Task<PePlanilhaArquivo> ConsolidadoCompletoAsync(string? formato, PeUserContext ctx);
}
