using Models.Pgia;

namespace Models.Planejamento;

/// <summary>
/// A forma de um passo escolhida pelo órgão no modo livre dos níveis (F3): o nível cujas seções
/// e campos valem para o passo (a escolha só vale quando é um nível ativo em que o passo está
/// ligado; senão, a forma padrão). Uma linha por órgão e passo; sem linha, a forma padrão (o
/// nível base, quando o passo está ligado nele, ou o primeiro nível ativo em que está). No modo
/// definido, a tabela é ignorada (fica guardada e volta a valer se o modo mudar). Só é lida com a
/// versão 8 do modelo inicial carregada (regra do deploy). Tabela pe_orgao_passo_detalhe;
/// mapeamento em PeModelConfiguration.
/// </summary>
public class PeOrgaoPassoDetalhe
{
    public long OrgaoId { get; set; }

    public PgiaOrgao? Orgao { get; set; }

    public long PassoId { get; set; }

    public PePasso? Passo { get; set; }

    // O nível da forma escolhida
    public long NivelId { get; set; }

    public PeNivel? Nivel { get; set; }

    public DateTime AlteradoEm { get; set; }

    public string AlteradoPor { get; set; } = string.Empty;
}

/// <summary>
/// A validação da equipe num passo da elaboração do PDTIC (F3): quem marcou e quando e, se os
/// dados do passo mudaram depois, a data da primeira mudança. São colunas novas de
/// pe_pdtic_passo, mapeadas por table splitting (a mesma linha do "não se aplica"), para as
/// consultas de sempre não selecionarem as colunas novas: no intervalo do deploy (o PR publica o
/// código antes da migration, que só roda no merge), o "não se aplica" continua funcionando. Só é
/// lida e gravada com a versão 8 do modelo inicial carregada. Marcar num passo sem linha cria a
/// linha (com o "não se aplica" desmarcado); desmarcar põe as três colunas em nulo.
/// </summary>
public class PePdticPassoValidacao
{
    public long PdticId { get; set; }

    public long PassoId { get; set; }

    public PePdticPasso? Linha { get; set; }

    // validado_em e validado_por (juntos; o CHECK do banco confere)
    public DateTime ValidadoEm { get; set; }

    public string ValidadoPor { get; set; } = string.Empty;

    // validacao_alterada_em: a primeira mudança nos dados do passo depois da validação (só com a validação)
    public DateTime? AlteradaEm { get; set; }
}
