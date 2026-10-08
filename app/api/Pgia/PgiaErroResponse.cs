namespace api.Pgia;

/// <summary>
/// Corpo do erro de regra de negócio do PGIA (400): o número do <see cref="ErrorCode"/>
/// e a mensagem para a pessoa ler. Mesmo formato <c>{ Code, Message }</c> dos módulos
/// que já respondem erro com corpo (gestão de acessos, Governança Estratégica).
/// </summary>
public class PgiaErroResponse
{
    public int Code { get; set; }

    public string Message { get; set; } = string.Empty;
}
