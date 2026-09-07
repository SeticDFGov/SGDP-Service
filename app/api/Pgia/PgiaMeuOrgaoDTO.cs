namespace api.Pgia;

/// <summary>
/// Contexto do órgão do usuário na área PGIA: dados do órgão e designações vigentes.
/// Orgao nulo indica que a Unidade do usuário ainda não tem órgão PGIA cadastrado.
/// </summary>
public class PgiaMeuOrgaoResponse
{
    public PgiaOrgaoResponse? Orgao { get; set; }

    public PgiaDesignacaoResponse? ResponsavelIaVigente { get; set; }

    public PgiaDesignacaoResponse? EncarregadoDadosVigente { get; set; }
}
