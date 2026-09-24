using Models.Pgia;

namespace Models.Planejamento;

/// <summary>
/// O PDTIC de um órgão (art. 12 do Decreto nº 48.900/2026), numa versão ("1.0"). Nasce em
/// elaboração quando a equipe do órgão abre (ou o admin geral, para qualquer órgão) e segue o
/// ciclo do plano (decisão 18): em aprovação, devolvido, aprovado, publicado, em
/// acompanhamento, encerrado ou substituído (as transições depois da elaboração chegam na
/// E7). Um PDTIC "atual" por órgão (nem substituído nem encerrado), garantido por índice
/// único parcial. Os dados de cada seção são registros (pe_registro com pdtic_id), e a
/// vigência é copiada do passo 1.1 (seção abrangencia) a cada gravação. Tabela pe_pdtic;
/// mapeamento em PeModelConfiguration.
/// </summary>
public class PePdtic : IPeAuditavel
{
    public long Id { get; set; }

    public long OrgaoId { get; set; }

    public PgiaOrgao? Orgao { get; set; }

    // "1.0"; um novo ciclo depois de um encerrado vai para "2.0" (a revisão "1.1" é da E7)
    public string Versao { get; set; } = "1.0";

    // PeDominios.SituacaoPdtic; token de concorrência (gravar e enviar ao mesmo tempo não passam os dois)
    public string Situacao { get; set; } = PeDominios.SituacaoPdtic.EmElaboracao;

    // Copiados da seção abrangencia (passo 1.1) ao gravar
    public DateOnly? VigenciaInicio { get; set; }

    public DateOnly? VigenciaFim { get; set; }

    // PDTIC aprovado fora do sistema e só registrado para o acompanhamento (E7)
    public bool RegistradoExternamente { get; set; }

    // O PDTIC anterior do órgão (a versão que este substitui ou sucede)
    public long? AnteriorId { get; set; }

    public PePdtic? Anterior { get; set; }

    public DateTime CriadoEm { get; set; }

    public string CriadoPor { get; set; } = string.Empty;

    // Última mudança no PDTIC ou num registro dele
    public DateTime? AlteradoEm { get; set; }

    public string? AlteradoPor { get; set; }
}

/// <summary>
/// O que o órgão marcou num passo do PDTIC: por enquanto, o "não se aplica" (decisão 15),
/// com a justificativa. Só passo opcional no nível do órgão, que aceite "não se aplica" e
/// não seja travado. Desfazer mantém a linha com nao_se_aplica falso (quem desfez e
/// quando). Tabela pe_pdtic_passo; chave (pdtic_id, passo_id).
/// </summary>
public class PePdticPasso
{
    public long PdticId { get; set; }

    public PePdtic? Pdtic { get; set; }

    public long PassoId { get; set; }

    public PePasso? Passo { get; set; }

    public bool NaoSeAplica { get; set; }

    // Obrigatória quando marcado; nula quando desfeito
    public string? Justificativa { get; set; }

    // Última marcação ou desmarcação
    public DateTime MarcadoEm { get; set; }

    public string MarcadoPor { get; set; } = string.Empty;
}

/// <summary>
/// Comentário num passo do PDTIC: os papéis globais (SGDI, Secretaria do CGTIC,
/// administrador do módulo) comentam; a equipe do órgão responde e marca como resolvido;
/// o autor também resolve. Um nível de resposta (a resposta aponta para o comentário
/// principal e não é resolvida sozinha). Comentário aberto põe o passo em "atenção".
/// Tabela pe_comentario.
/// </summary>
public class PeComentario
{
    public long Id { get; set; }

    public long PdticId { get; set; }

    public PePdtic? Pdtic { get; set; }

    public long PassoId { get; set; }

    public PePasso? Passo { get; set; }

    // Nulo no comentário principal; na resposta, o comentário respondido
    public long? PaiId { get; set; }

    public PeComentario? Pai { get; set; }

    // Até 2000 caracteres
    public string Texto { get; set; } = string.Empty;

    public string AutorEmail { get; set; } = string.Empty;

    public string AutorNome { get; set; } = string.Empty;

    public DateTime CriadoEm { get; set; }

    // Só no comentário principal
    public DateTime? ResolvidoEm { get; set; }

    public string? ResolvidoPor { get; set; }
}
