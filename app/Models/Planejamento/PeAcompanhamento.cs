namespace Models.Planejamento;

/// <summary>
/// Um ciclo do acompanhamento do PDTIC (E7, rodada B): o ciclo de monitoramento (criado sozinho,
/// depois da publicação, pela periodicidade do passo 4.3 ou pela padrão; rótulo "2027 · 1º
/// trimestre") ou a avaliação intermediária (aberta pela equipe do órgão; uma aberta por vez). Os
/// registros das seções por ciclo (pe_secao.por_ciclo) pertencem a um ciclo (pe_registro.ciclo_id).
/// Número sequencial por PDTIC e tipo. O prazo de fechamento é o fim mais os dias da configuração
/// prazo_fechamento_ciclo_dias (só no monitoramento; a avaliação não tem fim até ser fechada).
/// A situação guardada é aberto ou fechado (token de concorrência: gravar um dado e fechar ao
/// mesmo tempo não passam os dois); a tela mostra também futuro e atrasado, calculados. Tabela
/// pe_ciclo; mapeamento em PeModelConfiguration.
/// </summary>
public class PeCiclo : IPeAuditavel
{
    public long Id { get; set; }

    public long PdticId { get; set; }

    public PePdtic? Pdtic { get; set; }

    // PeDominios.TipoCiclo
    public string Tipo { get; set; } = PeDominios.TipoCiclo.Monitoramento;

    // 1, 2, 3... por PDTIC e tipo, na ordem do início
    public int Numero { get; set; }

    // "2027 · 1º trimestre", "2027 · março"; na avaliação, o que a equipe escreveu
    public string Rotulo { get; set; } = string.Empty;

    public DateOnly Inicio { get; set; }

    // Monitoramento: o fim do período; avaliação: o dia em que foi fechada (nulo enquanto aberta)
    public DateOnly? Fim { get; set; }

    // Monitoramento: o fim mais os dias de prazo; avaliação: nulo
    public DateOnly? Prazo { get; set; }

    // PeDominios.SituacaoCiclo; token de concorrência
    public string Situacao { get; set; } = PeDominios.SituacaoCiclo.Aberto;

    public DateTime? FechadoEm { get; set; }

    public string? FechadoPor { get; set; }

    // A última reabertura (quem e quando)
    public DateTime? ReabertoEm { get; set; }

    public string? ReabertoPor { get; set; }

    public DateTime CriadoEm { get; set; }

    public string CriadoPor { get; set; } = string.Empty;

    // Última gravação de um dado do ciclo, do fechamento ou da reabertura
    public DateTime? AlteradoEm { get; set; }

    public string? AlteradoPor { get; set; }
}

// ── Colunas novas em tabelas que já existiam (table splitting) ────────────────
//
// As colunas da rodada B em pe_registro, pe_secao, pe_doc_orgao, pe_doc_orgao_bloco e
// pe_doc_versao ficam em entidades à parte que dividem a tabela com a entidade de sempre (o
// mesmo recurso do PeArquivoConteudo). Consultar a entidade de sempre nunca seleciona as
// colunas novas: é o que deixa a API da rodada B funcionar no intervalo do deploy (o PR publica
// o código antes da migration, que só roda no merge) sem cair tudo o que já funcionava. As
// consultas que precisam das colunas novas só rodam depois que o carregador trouxe a versão 6
// do modelo inicial (PeAcompanhamentoAtivo), o que só acontece com a migration aplicada.

/// <summary>
/// O ciclo de um registro de uma seção por ciclo (pe_registro.ciclo_id; FK CASCADE para
/// pe_ciclo). Só existe para esses registros: o registro sem ciclo não tem esta parte.
/// </summary>
public class PeRegistroCiclo
{
    public long Id { get; set; }

    public PeRegistro? Registro { get; set; }

    public long CicloId { get; set; }

    public PeCiclo? Ciclo { get; set; }
}

/// <summary>
/// A seção é por ciclo (pe_secao.por_ciclo): de monitoramento ou de avaliação
/// (PeDominios.TipoCiclo). Só existe para essas seções; quem marca é o carregador (versão 6).
/// </summary>
public class PeSecaoCiclo
{
    public long Id { get; set; }

    public PeSecao? Secao { get; set; }

    public string PorCiclo { get; set; } = PeDominios.TipoCiclo.Monitoramento;
}

/// <summary>O documento de uma linha da cópia do órgão ou de uma versão: o tipo e, no RA, o ciclo.</summary>
public interface IPeDocDaLinha
{
    long Id { get; }

    // PeDominios.TipoDocumento
    string DocTipo { get; }

    // Só no RA (relatório de acompanhamento de um ciclo)
    long? CicloId { get; }
}

/// <summary>pe_doc_orgao.doc_tipo e ciclo_id (padrão pdtic, sem ciclo).</summary>
public class PeDocOrgaoDocumento : IPeDocDaLinha
{
    public long Id { get; set; }

    public PeDocOrgao? Linha { get; set; }

    public string DocTipo { get; set; } = PeDominios.TipoDocumento.Pdtic;

    public long? CicloId { get; set; }

    public PeCiclo? Ciclo { get; set; }
}

/// <summary>pe_doc_orgao_bloco.doc_tipo e ciclo_id (padrão pdtic, sem ciclo).</summary>
public class PeDocOrgaoBlocoDocumento : IPeDocDaLinha
{
    public long Id { get; set; }

    public PeDocOrgaoBloco? Linha { get; set; }

    public string DocTipo { get; set; } = PeDominios.TipoDocumento.Pdtic;

    public long? CicloId { get; set; }

    public PeCiclo? Ciclo { get; set; }
}

/// <summary>pe_doc_versao.doc_tipo e ciclo_id (padrão pdtic, sem ciclo).</summary>
public class PeDocVersaoDocumento : IPeDocDaLinha
{
    public long Id { get; set; }

    public PeDocVersao? Linha { get; set; }

    public string DocTipo { get; set; } = PeDominios.TipoDocumento.Pdtic;

    public long? CicloId { get; set; }

    public PeCiclo? Ciclo { get; set; }
}
