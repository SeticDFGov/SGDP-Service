namespace api.Pgia;

public class PgiaPrazoResponse
{
    public long Id { get; set; }

    public string Obrigacao { get; set; } = string.Empty;

    public string BaseLegal { get; set; } = string.Empty;

    public long? OrgaoId { get; set; }

    public DateOnly DataLimite { get; set; }

    public DateOnly? CumpridoEm { get; set; }

    // PgiaDominios.SituacaoPrazo (v_prazos_situacao do schema)
    public string Situacao { get; set; } = string.Empty;
}

/// <summary>
/// Marca (ou desmarca, com CumpridoEm nulo) o cumprimento de uma obrigação.
/// </summary>
public class PgiaMarcarCumprimentoDTO
{
    public DateOnly? CumpridoEm { get; set; }
}
