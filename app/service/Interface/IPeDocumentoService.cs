using System.Text.Json;
using api.Planejamento;
using service.Planejamento;

namespace service.Interface;

/// <summary>
/// O documento do PDTIC de um órgão (E5): a estrutura resolvida (modelo da SGDI, cópia do
/// órgão, trilha do nível, marcadores e dados), a edição dos textos e dos capítulos pela
/// equipe do órgão e a geração do PDF com as versões. Desde a E7 (rodada B), o mesmo para os
/// relatórios do acompanhamento: o RA de um ciclo e o RR (as sobrecargas com o
/// <see cref="PeDocAlvo"/>; as com o id do PDTIC são o documento do PDTIC). Autorização aqui,
/// pelo IPePermissionService; erros como ApiException (1070 a 1089, 1120 a 1139 e os
/// anteriores do módulo).
/// </summary>
public interface IPeDocumentoService
{
    Task<PeDocumentoResponse> ObterAsync(long pdticId, PeUserContext ctx);

    Task<PeDocumentoResponse> ObterAsync(PeDocAlvo alvo, PeUserContext ctx);

    Task<PeDocBlocoResponse> SalvarTextoAsync(PeDocAlvo alvo, long blocoId, JsonElement texto, PeUserContext ctx);

    Task<PeDocBlocoResponse> RestaurarTextoAsync(PeDocAlvo alvo, long blocoId, PeUserContext ctx);

    Task<PeDocCapituloResponse> AtualizarCapituloAsync(PeDocAlvo alvo, long capituloId, PeDocCapituloOrgaoDTO dto, PeUserContext ctx);

    Task<PeDocVersaoResponse> GerarPdfAsync(PeDocAlvo alvo, PeUserContext ctx);

    /// <summary>Como a outra sobrecarga, para o documento dado (o RA do ciclo sai assim no fechamento).</summary>
    Task<(Models.Planejamento.PeDocVersao Versao, long Tamanho)> GerarVersaoAsync(Models.Planejamento.PePdtic pdtic, PeDocAlvo alvo,
        PeUserContext ctx, string situacao);

    Task<List<PeDocVersaoResponse>> VersoesAsync(PeDocAlvo alvo, PeUserContext ctx);

    Task<PeDocArquivo> ArquivoDaVersaoAsync(PeDocAlvo alvo, int numero, PeUserContext ctx);

    /// <summary>Grava o texto do órgão num bloco de texto; devolve o bloco resolvido.</summary>
    Task<PeDocBlocoResponse> SalvarTextoAsync(long pdticId, long blocoId, JsonElement texto, PeUserContext ctx);

    /// <summary>Volta o bloco ao texto do modelo (apaga o texto do órgão); devolve o bloco resolvido.</summary>
    Task<PeDocBlocoResponse> RestaurarTextoAsync(long pdticId, long blocoId, PeUserContext ctx);

    /// <summary>Esconde ou mostra o capítulo e grava o título próprio; devolve o capítulo resolvido.</summary>
    Task<PeDocCapituloResponse> AtualizarCapituloAsync(long pdticId, long capituloId, PeDocCapituloOrgaoDTO dto, PeUserContext ctx);

    /// <summary>Gera o PDF e guarda uma versão minuta; devolve a versão.</summary>
    Task<PeDocVersaoResponse> GerarPdfAsync(long pdticId, PeUserContext ctx);

    /// <summary>
    /// Gera o PDF com a situação dada e deixa a versão no contexto, sem gravar (E7: a versão
    /// "enviada" entra na mesma gravação do envio ao CGTIC e da deliberação). Não confere quem chama.
    /// </summary>
    Task<(Models.Planejamento.PeDocVersao Versao, long Tamanho)> GerarVersaoAsync(Models.Planejamento.PePdtic pdtic, PeUserContext ctx, string situacao);

    Task<List<PeDocVersaoResponse>> VersoesAsync(long pdticId, PeUserContext ctx);

    Task<PeDocArquivo> ArquivoDaVersaoAsync(long pdticId, int numero, PeUserContext ctx);
}

/// <summary>
/// O modelo do documento (administrador do módulo): ler (qualquer papel), criar, alterar,
/// apagar e ordenar capítulos e blocos, com histórico no pe_modelo_historico.
/// </summary>
public interface IPeDocModeloService
{
    Task<PeDocModeloResponse> ObterAsync(string? tipo);

    Task<PeDocModeloCapituloResponse> CriarCapituloAsync(PeDocCapituloCriarDTO dto, PeUserContext ctx);

    Task<PeDocModeloCapituloResponse> AtualizarCapituloAsync(long id, PeDocCapituloAtualizarDTO dto, PeUserContext ctx);

    Task ExcluirCapituloAsync(long id, PeUserContext ctx);

    Task<PeDocModeloResponse> OrdenarCapitulosAsync(PeOrdemDTO dto, PeUserContext ctx);

    Task<PeDocModeloBlocoResponse> CriarBlocoAsync(PeDocBlocoCriarDTO dto, PeUserContext ctx);

    Task<PeDocModeloBlocoResponse> AtualizarBlocoAsync(long id, PeDocBlocoAtualizarDTO dto, PeUserContext ctx);

    Task ExcluirBlocoAsync(long id, PeUserContext ctx);

    Task<PeDocModeloResponse> OrdenarBlocosAsync(PeOrdemDTO dto, PeUserContext ctx);
}
