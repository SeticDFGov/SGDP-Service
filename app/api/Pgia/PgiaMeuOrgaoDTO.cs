namespace api.Pgia;

/// <summary>
/// Contexto do órgão do usuário na área PGIA: dados do órgão e designações vigentes.
/// Orgao nulo indica que a Unidade do usuário ainda não tem órgão PGIA cadastrado.
/// Os campos de sempre (ResponsavelIaVigente, EncarregadoDadosVigente) trazem o
/// TITULAR vigente; os suplentes e o Curador de Dados de Negócio vêm nos campos novos.
/// </summary>
public class PgiaMeuOrgaoResponse
{
    public PgiaOrgaoResponse? Orgao { get; set; }

    public PgiaDesignacaoResponse? ResponsavelIaVigente { get; set; }

    public PgiaDesignacaoResponse? ResponsavelIaSuplenteVigente { get; set; }

    public PgiaDesignacaoResponse? EncarregadoDadosVigente { get; set; }

    public PgiaDesignacaoResponse? EncarregadoDadosSuplenteVigente { get; set; }

    public PgiaDesignacaoResponse? CuradorDadosVigente { get; set; }

    public PgiaDesignacaoResponse? CuradorDadosSuplenteVigente { get; set; }
}
