using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using service.Planejamento;
using service;

namespace Controllers.Planejamento;

/// <summary>
/// Respostas de erro dos controllers do módulo Governança Estratégica, sempre como
/// { Code, Message } com 400, 403, 404 ou 409 (faixa 1000 a 1099 do ErrorCode), nunca um
/// 500 sem corpo:
/// <list type="bullet">
/// <item>ApiException: pelo código (404 para "não encontrado", 403 para permissão, 409 para
/// travado, do sistema, chave repetida, nível inativo, em uso, apagado, modelo
/// indisponível, conflito, registro ligado ou do sistema, versão fechada ou já enviada e
/// deliberação decidida; 400 para o resto). A validação de registro (PeValidacaoException)
/// leva também Campos, com a mensagem de cada campo;</item>
/// <item>tabela ou coluna pe_ que ainda não existe (o PR publica o código antes da
/// migration, que só roda no merge; desde a E4, a coluna pdtic_id de pe_registro): 409
/// PeModeloIndisponivel, com mensagem para tentar de novo;</item>
/// <item>DbUpdateException (duas pessoas gravando o mesmo item ao mesmo tempo): 409
/// PeConflitoGravacao.</item>
/// </list>
/// </summary>
internal static class PeRespostas
{
    public static async Task<IActionResult> ExecutarAsync(Func<Task<IActionResult>> acao)
    {
        try
        {
            return await acao();
        }
        catch (ApiException ex)
        {
            return Erro(ex);
        }
        catch (Exception ex) when (PeBanco.TabelaAusente(ex))
        {
            return Corpo(StatusCodes.Status409Conflict, ErrorCode.PeModeloIndisponivel,
                "A Governança Estratégica está sendo atualizada. Tente de novo em alguns minutos.");
        }
        catch (DbUpdateException)
        {
            return Corpo(StatusCodes.Status409Conflict, ErrorCode.PeConflitoGravacao,
                "Outra pessoa mudou este item ao mesmo tempo. Atualize a tela e tente de novo.");
        }
    }

    public static IActionResult Erro(ApiException ex)
    {
        var status = (ErrorCode)ex.Error.Code switch
        {
            ErrorCode.PeUsuarioNaoEncontrado or ErrorCode.PeItemNaoEncontrado or ErrorCode.PeOrgaoNaoEncontrado
                or ErrorCode.AcessoUsuarioNaoEncontrado or ErrorCode.PeRegistroNaoEncontrado or ErrorCode.PeSecaoIndisponivel
                or ErrorCode.PePeticNaoEncontrado or ErrorCode.PeDeliberacaoNaoEncontrada or ErrorCode.PeArquivoNaoEncontrado
                or ErrorCode.PeCatalogoNaoEncontrado or ErrorCode.PePdticNaoEncontrado or ErrorCode.PePassoIndisponivel
                or ErrorCode.PeComentarioNaoEncontrado => StatusCodes.Status404NotFound,
            ErrorCode.PeSemPermissao => StatusCodes.Status403Forbidden,
            ErrorCode.PeAutoRebaixamento or ErrorCode.PeItemTravado or ErrorCode.PeItemDoSistema or ErrorCode.PeChaveDuplicada
                or ErrorCode.PeNivelInativo or ErrorCode.PeUltimoNivelAtivo or ErrorCode.PeItemExcluido or ErrorCode.PeItemEmUso
                or ErrorCode.PeModeloIndisponivel or ErrorCode.PeConflitoGravacao or ErrorCode.PeRegistroDoSistema
                or ErrorCode.PeRegistroLigado or ErrorCode.PeFormularioJaPreenchido or ErrorCode.PeVersaoFechada
                or ErrorCode.PeVersaoEmAndamento or ErrorCode.PeDeliberacaoJaDecidida
                or ErrorCode.PeVersaoJaEnviada or ErrorCode.PePdticJaExiste or ErrorCode.PePdticFechado
                or ErrorCode.PeNaoSeAplicaRecusado or ErrorCode.PeComentarioResolvido => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status400BadRequest
        };
        // Validação de registro: a mensagem de cada campo, pela chave
        if (ex is PeValidacaoException validacao)
            return new ObjectResult(new { ex.Error.Code, ex.Error.Message, validacao.Campos }) { StatusCode = status };
        return new ObjectResult(new { ex.Error.Code, ex.Error.Message }) { StatusCode = status };
    }

    public static IActionResult CadastroNaoEncontrado() => Corpo(StatusCodes.Status404NotFound, ErrorCode.PeUsuarioNaoEncontrado,
        "Seu cadastro ainda não foi encontrado. Saia e entre de novo no sistema.");

    public static IActionResult SemPermissao(string mensagem) =>
        Corpo(StatusCodes.Status403Forbidden, ErrorCode.PeSemPermissao, mensagem);

    private static IActionResult Corpo(int status, ErrorCode codigo, string mensagem) =>
        new ObjectResult(new { Code = (int)codigo, Message = mensagem }) { StatusCode = status };
}
