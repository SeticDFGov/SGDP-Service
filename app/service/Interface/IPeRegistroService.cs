using api.Planejamento;
using service.Planejamento;

namespace service.Interface;

/// <summary>
/// Motor de registros do módulo Governança Estratégica (E3): grava os dados descritos pelo
/// modelo (seções e campos) para um dono (o catálogo do DF ou uma versão do PETIC-DF; a E4
/// acrescenta o PDTIC). Valida pelo modelo (só campos visíveis, obrigatórios, tipos, opções
/// ativas, ligações do mesmo dono ou do catálogo), guarda o valor de campo desligado sem
/// exigir (decisão 13), gera o código pela sequência do dono, calcula os campos calculados
/// e devolve os rótulos prontos. A autorização fica aqui (quem lê, quem edita e se a versão
/// está em rascunho), pelo IPePermissionService. Erros como ApiException (1030 a 1049).
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

    /// <summary>Itens de um catálogo (petic_objetivo e petic_eixo da vigente; principio do DF).</summary>
    Task<List<PeCatalogoItemResponse>> CatalogoAsync(string catalogo, PeUserContext ctx);

    /// <summary>
    /// O que falta para o dono ir ao CGTIC: seção obrigatória sem registro e registro com
    /// campo obrigatório vazio (pelo modelo de hoje). Lista vazia = pronto.
    /// </summary>
    Task<List<string>> PendenciasAsync(PeDono dono);

    /// <summary>Uma seção para a planilha: as colunas (visíveis e marcadas "na planilha") e os registros.</summary>
    Task<PeSecaoExportada> ExportarSecaoAsync(PeDono dono, string secaoChave, PeUserContext ctx);

    /// <summary>Todas as seções do dono que vão para a planilha, na ordem.</summary>
    Task<List<PeSecaoExportada>> ExportarSecoesAsync(PeDono dono, PeUserContext ctx);
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
/// Planilhas do PETIC-DF e do catálogo do DF: CSV (UTF-8 com BOM, ponto e vírgula, proteção
/// contra fórmula) e XLSX (cabeçalho destacado e fixo, filtro, datas e valores como
/// números, listas com validação e a aba Leia-me).
/// </summary>
public interface IPePlanilhaService
{
    Task<PePlanilhaArquivo> PeticSecaoAsync(long peticId, string secaoChave, string? formato, PeUserContext ctx);

    Task<PePlanilhaArquivo> PeticCompletaAsync(long peticId, string? formato, PeUserContext ctx);

    Task<PePlanilhaArquivo> DfSecaoAsync(string secaoChave, string? formato, PeUserContext ctx);
}
