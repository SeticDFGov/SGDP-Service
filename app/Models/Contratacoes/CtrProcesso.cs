namespace Models.Contratacoes;

/// <summary>
/// Processo SEI de contratação de TIC em análise pela SGDI (uma linha da planilha
/// legada "Análises de contratações"). Tabela ctr_processo.
///
/// A situação do processo é DERIVADA das datas (CtrProcessoService.CalcularSituacao)
/// e nunca é gravada aqui.
/// </summary>
public class CtrProcesso
{
    public long Id { get; set; }

    // Formato SEI 00000-00000000/AAAA-DD; único entre os ativos
    public string NumeroProcesso { get; set; } = string.Empty;

    // Órgão comunicante em texto livre: o universo aqui é maior que o dos
    // órgãos aderentes ao PGIA, então não há FK para pgia_orgao.
    public string OrgaoNome { get; set; } = string.Empty;

    public string OrgaoSigla { get; set; } = string.Empty;

    public string? ComplementoArea { get; set; }

    public string Objeto { get; set; } = string.Empty;

    // CtrDominios.CategoriaObjeto
    public string CategoriaObjeto { get; set; } = string.Empty;

    // Os cinco checkpoints do trâmite (SGDI -> SUBGD -> UGTIC -> Gab SGDI -> órgão)
    public DateOnly? ChegadaSgdi { get; set; }

    public DateOnly? ChegadaSubgd { get; set; }

    public DateOnly? ChegadaUgtic { get; set; }

    // A célula "-" da planilha: etapa pulada de propósito
    public bool UgticNaoSeAplica { get; set; }

    public DateOnly? RetornoGabSgdi { get; set; }

    public DateOnly? RetornoOrgao { get; set; }

    // Restituição ao órgão: conceito de primeira classe (hoje vive na Observação)
    public bool Restituido { get; set; }

    public DateOnly? RestituidoEm { get; set; }

    public string? RestituidoMotivo { get; set; }

    public string? Observacao { get; set; }

    // Soft delete: some das listas, a linha permanece auditável
    public bool Ativo { get; set; } = true;

    public DateTime CriadoEm { get; set; }

    public string CriadoPor { get; set; } = string.Empty;

    public DateTime? AlteradoEm { get; set; }

    public string? AlteradoPor { get; set; }

    public ICollection<CtrManifestacaoTcdf> Manifestacoes { get; set; } = new List<CtrManifestacaoTcdf>();
}
