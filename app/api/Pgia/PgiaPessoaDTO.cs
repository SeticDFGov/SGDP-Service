namespace api.Pgia;

/// <summary>
/// Pessoa do órgão: usuário do SGDP (mesma Unidade do órgão) com os dados
/// de agente público do PGIA (pgia_agente_info), quando preenchidos.
/// </summary>
public class PgiaPessoaResponse
{
    public Guid UserId { get; set; }

    public string Nome { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string? PapelPgia { get; set; }

    public string? Matricula { get; set; }

    public string? CargoFuncao { get; set; }

    public string? Vinculo { get; set; }
}

/// <summary>
/// Dados de agente público (art. 3º) complementares ao cadastro do usuário.
/// </summary>
public class PgiaAgenteInfoDTO
{
    [System.ComponentModel.DataAnnotations.StringLength(20)]
    public string? Matricula { get; set; }

    public string? CargoFuncao { get; set; }

    // D11 (PgiaDominios.Vinculo)
    [System.ComponentModel.DataAnnotations.StringLength(30)]
    public string Vinculo { get; set; } = string.Empty;
}
