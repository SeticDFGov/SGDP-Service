using api.Pgia;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using service;

namespace Controllers.Pgia;

/// <summary>
/// Filtro de exceção ESCOPADO aos controllers do PGIA (todos os de
/// <c>Controllers/Pgia</c>, inclusive o público): o <see cref="ApiException"/> lançado
/// numa action vira <b>400 com <c>{ Code, Message }</c></b>, em vez do 500 sem corpo
/// herdado do sistema (não há middleware global). Os outros módulos não usam este
/// filtro e continuam como estavam. Sucessos e os <c>NotFound()</c>/<c>Forbid()</c>
/// explícitos das actions não passam por aqui. Exceção que não é
/// <see cref="ApiException"/> segue o caminho de sempre.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = true, AllowMultiple = false)]
public sealed class PgiaErrosAttribute : ExceptionFilterAttribute
{
    public override void OnException(ExceptionContext context)
    {
        if (context.Exception is not ApiException ex) return;

        context.Result = new BadRequestObjectResult(Corpo(ex));
        context.ExceptionHandled = true;
    }

    /// <summary>
    /// <c>Code</c> = o número do <see cref="ErrorCode"/>; <c>Message</c> = a mensagem da
    /// exceção ou, quando ela foi lançada só com o código, a descrição padrão dele.
    /// </summary>
    public static PgiaErroResponse Corpo(ApiException ex)
    {
        var codigo = ex.Error.Code;
        var nome = Enum.IsDefined(typeof(ErrorCode), codigo) ? ((ErrorCode)codigo).ToString() : null;
        var mensagem = ex.Error.Message;

        // Sem mensagem própria, o ApiException guarda o nome do código: troca pela descrição
        if (string.IsNullOrWhiteSpace(mensagem) || mensagem == nome)
            mensagem = DescricaoPadrao(codigo) ?? nome ?? "Não foi possível concluir a operação.";

        return new PgiaErroResponse { Code = codigo, Message = mensagem };
    }

    /// <summary>Descrição padrão, em linguagem simples, dos códigos do PGIA.</summary>
    public static string? DescricaoPadrao(int codigo) => (ErrorCode)codigo switch
    {
        ErrorCode.PgiaOrgaoNaoEncontrado => "Órgão não encontrado.",
        ErrorCode.PgiaOrgaoJaExiste => "Este órgão já está cadastrado.",
        ErrorCode.PgiaUsuarioNaoEncontrado => "Pessoa não encontrada.",
        ErrorCode.PgiaPapelInvalido => "Papel do PGIA inválido.",
        ErrorCode.PgiaDominioInvalido => "Há um valor fora das opções permitidas.",
        ErrorCode.PgiaDesignacaoNaoEncontrada => "Designação não encontrada.",
        ErrorCode.PgiaAgenteDeOutroOrgao => "A pessoa não pertence a este órgão.",
        ErrorCode.PgiaPrazoNaoEncontrado => "Prazo não encontrado.",
        ErrorCode.PgiaUnidadeNaoEncontrada => "Unidade não encontrada.",
        ErrorCode.PgiaOrgaoSemUnidadeVinculada => "O órgão ainda não tem unidade vinculada.",
        ErrorCode.PgiaDesignacaoVigenteImpedeTroca => "Encerre a designação vigente antes desta troca.",
        ErrorCode.PgiaSistemaNaoEncontrado => "Sistema de IA não encontrado.",
        ErrorCode.PgiaSistemaJaExiste => "Este sistema de IA já está cadastrado.",
        ErrorCode.PgiaChecklistInvalido => "O questionário de risco está incompleto.",
        ErrorCode.PgiaClassificacaoInvalida => "Classificação de risco inválida.",
        ErrorCode.PgiaRiscoExcessivoBloqueado => "Sistema de Risco Excessivo não pode entrar em uso.",
        ErrorCode.PgiaResponsavelNaoDesignado => "O órgão ainda não tem Responsável de IA designado.",
        ErrorCode.PgiaDocumentoNaoEncontrado => "Documento não encontrado.",
        ErrorCode.PgiaArquivoInvalido => "Arquivo inválido.",
        ErrorCode.PgiaAiaNaoEncontrada => "Avaliação de impacto não encontrada.",
        ErrorCode.PgiaDeliberacaoNaoEncontrada => "Deliberação não encontrada.",
        ErrorCode.PgiaPlataformaNaoEncontrada => "Plataforma não encontrada.",
        ErrorCode.PgiaPlataformaJaExiste => "Esta plataforma já está cadastrada.",
        ErrorCode.PgiaNormaNaoEncontrada => "Norma não encontrada.",
        ErrorCode.PgiaAutorizacaoNaoEncontrada => "Autorização não encontrada.",
        ErrorCode.PgiaHomologacaoIndevida => "Este sistema não está na fila desta homologação.",
        ErrorCode.PgiaImplantacaoBloqueada => "O sistema ainda não pode entrar em uso.",
        ErrorCode.PgiaIncidenteNaoEncontrado => "Incidente não encontrado.",
        ErrorCode.PgiaIncidenteNaoComunicado => "O incidente ainda não foi comunicado à SGDI.",
        ErrorCode.PgiaNaoConformidadeNaoEncontrada => "Não conformidade não encontrada.",
        ErrorCode.PgiaCapacitacaoNaoEncontrada => "Capacitação não encontrada.",
        ErrorCode.PgiaCapacitacaoJaExiste => "Esta capacitação já está registrada para a pessoa.",
        ErrorCode.PgiaRegistroUsoInvalido => "Registro de uso de IA inválido.",
        ErrorCode.PgiaPlataformaNaoHomologada => "A plataforma não está homologada pela SGDI.",
        ErrorCode.PgiaRevisaoHumanaObrigatoria => "Confirme a revisão humana do conteúdo.",
        ErrorCode.PgiaSemOrgaoResolvido => "A sua unidade ainda não está vinculada a um órgão do PGIA.",
        ErrorCode.PgiaContratoNaoEncontrado => "Contrato não encontrado.",
        ErrorCode.PgiaContratoInvalido => "Contrato inválido.",
        ErrorCode.PgiaLegadoNaoEncontrado => "Instrumento não encontrado.",
        ErrorCode.PgiaIndicadorNaoEncontrado => "Indicador não encontrado.",
        ErrorCode.PgiaPeriodoInvalido => "Período inválido.",
        ErrorCode.PgiaRelatorioNaoEncontrado => "Relatório não encontrado.",
        ErrorCode.PgiaRelatorioJaExiste => "Este relatório já existe.",
        ErrorCode.PgiaAuditoriaNaoEncontrada => "Auditoria não encontrada.",
        ErrorCode.PgiaAuditoriaNaoDesignada => "Esta auditoria não está designada a você.",
        ErrorCode.PgiaSolicitacaoNaoEncontrada => "Solicitação não encontrada.",
        ErrorCode.PgiaSolicitacaoInvalida => "Solicitação inválida.",
        ErrorCode.PgiaSistemaNaoPublicado => "O sistema não está no Registro Público.",
        _ => null
    };
}
