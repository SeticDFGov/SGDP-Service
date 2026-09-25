using api.Planejamento;
using Models.Planejamento;
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
    // Desde a E7 (rodada B), o cicloId: obrigatório nas seções por ciclo do PDTIC (400
    // PeCicloObrigatorio sem ele) e ignorado nas outras. Gravar exige o ciclo começado e aberto.

    Task<PeRegistrosResponse> ListarAsync(PeDono dono, string secaoChave, PeUserContext ctx, long? cicloId = null);

    /// <summary>Inclui (tabela) ou preenche pela primeira vez (formulário; já preenchido: 409).</summary>
    Task<PeRegistroResponse> CriarAsync(PeDono dono, string secaoChave, PeRegistroSalvarDTO dto, PeUserContext ctx, long? cicloId = null);

    Task<PeRegistroResponse> AtualizarAsync(PeDono dono, string secaoChave, long id, PeRegistroSalvarDTO dto, PeUserContext ctx,
        long? cicloId = null);

    /// <summary>Apaga; ligado por outro registro ou do sistema: 409.</summary>
    Task ExcluirAsync(PeDono dono, string secaoChave, long id, PeUserContext ctx, long? cicloId = null);

    /// <summary>Nova ordem ({ Ids }, todos os registros da seção); devolve a lista.</summary>
    Task<PeRegistrosResponse> OrdenarAsync(PeDono dono, string secaoChave, PeOrdemDTO dto, PeUserContext ctx, long? cicloId = null);

    /// <summary>
    /// Grava de uma vez as linhas de uma seção por ciclo (as grades do ciclo, E7 rodada B), com a
    /// validação de sempre em cada linha e os erros de todas juntos (chave "prefixo.campo").
    /// </summary>
    Task SalvarNoCicloAsync(PeDono dono, string secaoChave, long cicloId, IReadOnlyList<PeLinhaDoCiclo> linhas, PeUserContext ctx);

    /// <summary>O ciclo (id e rótulo) de cada registro de uma seção por ciclo (as planilhas mostram o ciclo).</summary>
    Task<Dictionary<long, (long CicloId, string Rotulo)>> CiclosDosRegistrosAsync(IReadOnlyCollection<long> registroIds);

    /// <summary>
    /// Linhas sugeridas pelo sistema numa tabela vazia (o cronograma que vem dos fluxos, E6), só
    /// com os textos e as ligações dados; os obrigatórios que faltam o órgão completa depois.
    /// Tabela com linhas: 409 PeCronogramaPreenchido.
    /// </summary>
    Task<List<PeRegistroResponse>> CriarSugeridosAsync(PeDono dono, string secaoChave, IReadOnlyList<PeRegistroSugerido> linhas, PeUserContext ctx);

    /// <summary>
    /// Os princípios do art. 4º do Decreto nº 48.900/2026 como sugestão na seção dos princípios e
    /// diretrizes (passo 1.8) do PDTIC que acabou de abrir (F2): registros comuns, que o órgão muda
    /// e apaga, com a origem "art. 4º". Quem chama já conferiu a permissão (é a abertura do PDTIC) e
    /// abre a transação que junta o PDTIC e as linhas. Devolve quantos entraram (zero quando a seção
    /// não aparece para o órgão, quando o catálogo do DF ainda não tem os princípios ou quando a
    /// seção já tem linhas).
    /// </summary>
    Task<int> SugerirPrincipiosDoArt4Async(PePdtic pdtic, PeUserContext ctx);

    /// <summary>
    /// Itens de um catálogo (petic_objetivo e petic_eixo da vigente; principio do DF;
    /// pgia_sistema, os sistemas de IA do inventário do PGIA do órgão do PDTIC pdticId).
    /// </summary>
    Task<List<PeCatalogoItemResponse>> CatalogoAsync(string catalogo, PeUserContext ctx, long? pdticId = null);

    /// <summary>
    /// O que falta para o dono ir ao CGTIC: seção obrigatória sem registro e registro com
    /// campo obrigatório vazio (pelo modelo de hoje), com a seção de cada pendência (F1). Lista
    /// vazia = pronto.
    /// </summary>
    Task<List<PePeticPendenciaResponse>> PendenciasAsync(PeDono dono);

    /// <summary>
    /// Os registros das seções dadas e o que falta em cada um, mais as ligações que saem deles
    /// (situação dos passos e avisos do PDTIC). Não confere quem chama: quem chama já conferiu.
    /// Seção por ciclo: só os registros do cicloId (sem ele, nenhum).
    /// </summary>
    Task<PeAnaliseDono> AnalisarAsync(PeDono dono, IReadOnlyList<PeSecaoDoDono> secoes, long? cicloId = null);

    /// <summary>Uma seção para a planilha: as colunas (visíveis e marcadas "na planilha") e os registros.</summary>
    Task<PeSecaoExportada> ExportarSecaoAsync(PeDono dono, string secaoChave, PeUserContext ctx);

    /// <summary>Todas as seções do dono que vão para a planilha, na ordem.</summary>
    Task<List<PeSecaoExportada>> ExportarSecoesAsync(PeDono dono, PeUserContext ctx);

    /// <summary>
    /// As seções de um passo do PDTIC para a planilha (E8): as visíveis para o órgão e marcadas "na
    /// planilha", na ordem do passo, cada uma como o <see cref="ExportarSecaoAsync"/> a exporta. Com
    /// seção por ciclo, o cicloId é obrigatório (400 PeCicloObrigatorio), do PDTIC (404) e do tipo
    /// dela (400), e ela sai só com os registros dele; as outras seções o ignoram. Passo fora da
    /// trilha: 404 PePassoIndisponivel; sem seção: 404 PePassoSemPlanilha. Lê: quem vê o órgão.
    /// </summary>
    Task<PePassoExportado> ExportarPassoAsync(long pdticId, string passoChave, long? cicloId, PeUserContext ctx);

    /// <summary>
    /// Os registros de uma seção em vários PDTICs de uma vez (o consolidado), cada um com a
    /// seção como o órgão dele a vê; poucas consultas para todos os órgãos. Não confere quem
    /// chama: quem chama já conferiu.
    /// </summary>
    Task<Dictionary<long, List<PeRegistroResponse>>> ExportarDosPdticsAsync(long secaoId, IReadOnlyList<(long PdticId, PeSecaoDoDono Secao)> pdtics);

    /// <summary>
    /// Os registros de várias seções do dono de uma vez (o documento do PDTIC, E5), cada seção
    /// com todos os campos visíveis como colunas e os registros com os rótulos. Não confere quem
    /// chama: quem chama já conferiu. Seção por ciclo (E7, rodada B): com cicloId, só os registros
    /// dele (o RA); sem, os de todos os ciclos (o PDTIC e o RR), com o ciclo de cada um.
    /// </summary>
    Task<List<PeSecaoExportada>> ExportarAsync(PeDono dono, IReadOnlyList<PeSecaoDoDono> secoes, long? cicloId = null);
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
    /// Um passo do PDTIC (E8): em XLSX, uma aba por seção do passo e a Leia-me com o passo; em CSV,
    /// o arquivo da seção quando o passo tem uma só, ou um ZIP com um CSV por seção
    /// (numero-do-passo-chave-da-secao.csv). Nome PDTIC_SIGLA_passo-N.M[_ciclo]_aaaa-mm-dd.
    /// </summary>
    Task<PePlanilhaArquivo> PdticPassoAsync(long pdticId, string passoChave, string? formato, long? cicloId, PeUserContext ctx);

    /// <summary>
    /// Uma seção de todos os PDTICs atuais: as colunas do órgão (órgão, sigla, nível, versão e
    /// situação) antes dos campos, e os campos visíveis em qualquer nível ou órgão (célula vazia
    /// quando o campo não aparece para o órgão).
    /// </summary>
    Task<PePlanilhaArquivo> ConsolidadoSecaoAsync(string secaoChave, string? formato, PeUserContext ctx);

    /// <summary>O consolidado com todas as seções, uma aba por seção (só xlsx).</summary>
    Task<PePlanilhaArquivo> ConsolidadoCompletoAsync(string? formato, PeUserContext ctx);
}
