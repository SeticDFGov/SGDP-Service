using System.ComponentModel.DataAnnotations;

namespace api.Pgia;

public class PgiaOrgaoCreateDTO
{
    [StringLength(20)]
    public string Sigla { get; set; } = string.Empty;

    public string Nome { get; set; } = string.Empty;

    // D10 (PgiaDominios.NaturezaJuridica)
    [StringLength(40)]
    public string NaturezaJuridica { get; set; } = string.Empty;

    // Só para empresa pública ou sociedade de economia mista (art. 1º, § único)
    public bool? PrestaServicoCidadao { get; set; }

    // Unidade do SGDP vinculada ao órgão (opcional)
    public Guid? UnidadeId { get; set; }
}

public class PgiaOrgaoUpdateDTO : PgiaOrgaoCreateDTO
{
    public bool Ativo { get; set; } = true;
}

/// <summary>
/// Atualização dos dados do órgão pela própria equipe (etapa 1 do formulário).
/// Não altera vínculo com Unidade nem o status ativo — isso é do admin.
/// </summary>
public class PgiaOrgaoDadosDTO
{
    [StringLength(20)]
    public string Sigla { get; set; } = string.Empty;

    public string Nome { get; set; } = string.Empty;

    [StringLength(40)]
    public string NaturezaJuridica { get; set; } = string.Empty;

    public bool? PrestaServicoCidadao { get; set; }
}

public class PgiaOrgaoResponse
{
    public long Id { get; set; }

    public string Sigla { get; set; } = string.Empty;

    public string Nome { get; set; } = string.Empty;

    public string NaturezaJuridica { get; set; } = string.Empty;

    public bool? PrestaServicoCidadao { get; set; }

    public bool Ativo { get; set; }

    public Guid? UnidadeId { get; set; }

    public string? UnidadeNome { get; set; }
}
