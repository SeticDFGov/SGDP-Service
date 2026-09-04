using System.ComponentModel.DataAnnotations;

namespace api.Pgia;

/// <summary>
/// Designação do Encarregado de Dados (art. 11) e base da designação do Responsável de IA.
/// Os StringLength espelham os varchar do schema; o [ApiController] devolve 400 em violação.
/// </summary>
public class PgiaDesignacaoCreateDTO
{
    // Usuário do SGDP designado (deve pertencer à Unidade do órgão)
    public Guid AgenteId { get; set; }

    [StringLength(30)]
    public string AtoTipo { get; set; } = string.Empty;

    [StringLength(30)]
    public string AtoNumero { get; set; } = string.Empty;

    public DateOnly AtoData { get; set; }

    [StringLength(25)]
    public string ProcessoSeiComunicacao { get; set; } = string.Empty;

    public DateOnly DataComunicacaoSgdi { get; set; }

    // Cópia do ato de designação já registrada como documento DO MESMO ÓRGÃO
    public long? DocumentoId { get; set; }

    public DateOnly InicioVigencia { get; set; }
}

/// <summary>
/// Designação do Responsável de IA (art. 10), com a acumulação da função de TIC.
/// </summary>
public class PgiaResponsavelIaCreateDTO : PgiaDesignacaoCreateDTO
{
    public bool AcumulaFuncaoTic { get; set; }

    // Condicional: só quando acumula a função de TIC (art. 10, § 2º)
    public bool? CapacitacaoAdequada { get; set; }
}

public class PgiaDesignacaoResponse
{
    public long Id { get; set; }

    public long OrgaoId { get; set; }

    public Guid AgenteId { get; set; }

    public string AgenteNome { get; set; } = string.Empty;

    public string AgenteEmail { get; set; } = string.Empty;

    public string AtoTipo { get; set; } = string.Empty;

    public string AtoNumero { get; set; } = string.Empty;

    public DateOnly AtoData { get; set; }

    public string ProcessoSeiComunicacao { get; set; } = string.Empty;

    public DateOnly DataComunicacaoSgdi { get; set; }

    // Preenchidos só na designação de Responsável de IA
    public bool? AcumulaFuncaoTic { get; set; }

    public bool? CapacitacaoAdequada { get; set; }

    // Cópia do ato de designação anexada (null = ainda não anexada)
    public long? DocumentoId { get; set; }

    public DateOnly InicioVigencia { get; set; }

    public DateOnly? FimVigencia { get; set; }

    public bool Ativo { get; set; }
}
