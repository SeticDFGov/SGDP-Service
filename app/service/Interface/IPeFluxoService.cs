using api.Planejamento;
using service.Planejamento;

namespace service.Interface;

/// <summary>
/// Os fluxos do módulo Governança Estratégica (E6): os fluxos do guia como modelo (o
/// administrador do módulo edita), a cópia que o órgão adapta no PDTIC dele (só existe quando
/// ele muda alguma coisa), o desenho automático em SVG (com os nomes do dicionário do órgão) e o
/// cronograma sugerido do plano de trabalho. Autorização aqui, pelo IPePermissionService; erros
/// como ApiException (1090 a 1099 e os anteriores do módulo).
/// </summary>
public interface IPeFluxoService
{
    /// <summary>Os fluxos do guia como modelo, na ordem (qualquer papel do módulo lê).</summary>
    Task<List<PeFluxoModeloResponse>> ModelosAsync(PeUserContext ctx);

    /// <summary>Grava o nome e a definição de um modelo (pe_admin e admin geral), com histórico.</summary>
    Task<PeFluxoModeloResponse> SalvarModeloAsync(string chave, PeFluxoSalvarDTO dto, PeUserContext ctx);

    /// <summary>O desenho do modelo, com os nomes padrão no lugar dos marcadores.</summary>
    Task<string> SvgDoModeloAsync(string chave, PeUserContext ctx);

    /// <summary>Os fluxos do PDTIC (todos os modelos, com a situação da cópia do órgão).</summary>
    Task<List<PeFluxoResumoResponse>> DoPdticAsync(long pdticId, PeUserContext ctx);

    /// <summary>O fluxo do PDTIC: a cópia do órgão ou, sem cópia, o modelo.</summary>
    Task<PeFluxoResponse> ObterAsync(long pdticId, string chave, PeUserContext ctx);

    /// <summary>Grava a cópia do órgão (igual ao modelo: volta a seguir o modelo).</summary>
    Task<PeFluxoResponse> SalvarAsync(long pdticId, string chave, PeFluxoSalvarDTO dto, PeUserContext ctx);

    /// <summary>Apaga a cópia do órgão: o fluxo volta ao modelo do guia.</summary>
    Task<PeFluxoResponse> RestaurarAsync(long pdticId, string chave, PeUserContext ctx);

    /// <summary>O desenho do fluxo do PDTIC, com os nomes do dicionário do órgão.</summary>
    Task<string> SvgAsync(long pdticId, string chave, PeUserContext ctx);

    /// <summary>O desenho de uma definição ainda não gravada (prévia do editor); confere antes (400 com Erros).</summary>
    Task<string> DesenhoAsync(PeFluxoDesenhoDTO dto, PeUserContext ctx);

    /// <summary>Confere uma definição sem gravar: os erros ou, sem erro, a definição numerada.</summary>
    Task<PeFluxoValidacaoResponse> ValidarAsync(PeFluxoDesenhoDTO dto, PeUserContext ctx);

    /// <summary>Os nomes do dicionário que raias e passos podem usar (com os do órgão, quando há o PDTIC).</summary>
    Task<List<PeFluxoNomeResponse>> NomesAsync(long? pdticId, PeUserContext ctx);

    /// <summary>
    /// Cria o cronograma sugerido do plano de trabalho (uma linha por tarefa dos fluxos de
    /// preparação, diagnóstico e planejamento, com o responsável pela raia e a predecessora);
    /// só no cronograma vazio (senão 409). Devolve os registros criados.
    /// </summary>
    Task<List<PeRegistroResponse>> SugerirCronogramaAsync(long pdticId, PeUserContext ctx);
}
