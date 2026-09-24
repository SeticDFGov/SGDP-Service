using System.Text.Json;
using api.Planejamento;
using service.Planejamento;

namespace service.Interface;

/// <summary>
/// O documento do PDTIC de um órgão (E5): a estrutura resolvida (modelo da SGDI, cópia do
/// órgão, trilha do nível, marcadores e dados), a edição dos textos e dos capítulos pela
/// equipe do órgão e a geração do PDF com as versões. Autorização aqui, pelo
/// IPePermissionService; erros como ApiException (1070 a 1089 e os anteriores do módulo).
/// </summary>
public interface IPeDocumentoService
{
    Task<PeDocumentoResponse> ObterAsync(long pdticId, PeUserContext ctx);

    /// <summary>Grava o texto do órgão num bloco de texto; devolve o bloco resolvido.</summary>
    Task<PeDocBlocoResponse> SalvarTextoAsync(long pdticId, long blocoId, JsonElement texto, PeUserContext ctx);

    /// <summary>Volta o bloco ao texto do modelo (apaga o texto do órgão); devolve o bloco resolvido.</summary>
    Task<PeDocBlocoResponse> RestaurarTextoAsync(long pdticId, long blocoId, PeUserContext ctx);

    /// <summary>Esconde ou mostra o capítulo e grava o título próprio; devolve o capítulo resolvido.</summary>
    Task<PeDocCapituloResponse> AtualizarCapituloAsync(long pdticId, long capituloId, PeDocCapituloOrgaoDTO dto, PeUserContext ctx);

    /// <summary>Gera o PDF e guarda uma versão minuta; devolve a versão.</summary>
    Task<PeDocVersaoResponse> GerarPdfAsync(long pdticId, PeUserContext ctx);

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
