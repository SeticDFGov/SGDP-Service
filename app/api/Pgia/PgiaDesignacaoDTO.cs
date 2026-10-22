using System.ComponentModel.DataAnnotations;

namespace api.Pgia;

/// <summary>
/// Ato de uma designação (o que a criação exige e o que a atualização completa).
/// Os StringLength espelham os varchar do schema; o [ApiController] devolve 400 em violação.
/// A API exige tudo: só a carga SQL da planilha da SGDI grava designação com o ato pendente.
/// </summary>
public class PgiaDesignacaoAtoDTO
{
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
/// Designação do Encarregado de Dados (art. 11) e do Curador de Dados de Negócio, e
/// base da designação do Responsável de IA.
/// </summary>
public class PgiaDesignacaoCreateDTO : PgiaDesignacaoAtoDTO
{
    // Usuário do SGDP designado (deve pertencer à Unidade do órgão)
    public Guid AgenteId { get; set; }

    // "Titular" ou "Suplente"; ausente ou vazio = "Titular" (o front antigo não manda)
    [StringLength(10)]
    public string? Posicao { get; set; }
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

/// <summary>
/// Atualização do ato de uma designação (Encarregado de Dados e Curador de Dados de
/// Negócio): completa o ato da carga da planilha ou corrige o ato registrado. Pessoa
/// e posição não mudam (troca de pessoa é designação nova). Exige todos os campos;
/// salvo, a designação deixa de ter o ato pendente.
/// </summary>
public class PgiaDesignacaoAtualizarDTO : PgiaDesignacaoAtoDTO
{
}

/// <summary>Atualização do ato do Responsável de IA, com a acumulação da função de TIC.</summary>
public class PgiaResponsavelIaAtualizarDTO : PgiaDesignacaoAtualizarDTO
{
    public bool AcumulaFuncaoTic { get; set; }

    public bool? CapacitacaoAdequada { get; set; }
}

public class PgiaDesignacaoResponse
{
    public long Id { get; set; }

    public long OrgaoId { get; set; }

    public Guid AgenteId { get; set; }

    public string AgenteNome { get; set; } = string.Empty;

    public string AgenteEmail { get; set; } = string.Empty;

    // "Titular" ou "Suplente"
    public string Posicao { get; set; } = string.Empty;

    // Importada da planilha da SGDI com o ato a completar: os campos do ato, a
    // comunicação à SGDI e o início da vigência podem vir nulos
    public bool AtoPendente { get; set; }

    public string? AtoTipo { get; set; }

    public string? AtoNumero { get; set; }

    public DateOnly? AtoData { get; set; }

    public string? ProcessoSeiComunicacao { get; set; }

    public DateOnly? DataComunicacaoSgdi { get; set; }

    // Preenchidos só na designação de Responsável de IA
    public bool? AcumulaFuncaoTic { get; set; }

    public bool? CapacitacaoAdequada { get; set; }

    // Cópia do ato de designação anexada (null = ainda não anexada)
    public long? DocumentoId { get; set; }

    public DateOnly? InicioVigencia { get; set; }

    public DateOnly? FimVigencia { get; set; }

    public bool Ativo { get; set; }
}
