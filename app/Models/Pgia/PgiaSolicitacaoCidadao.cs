using app.Models;

namespace Models.Pgia;

/// <summary>
/// Direitos do cidadão afetado por IA e reclamações de titulares (arts. 11, IV e 23).
/// Tabela pgia_solicitacao_cidadao. Contém dados pessoais do solicitante: nunca são
/// devolvidos pelos endpoints públicos (minimização, art. 19, II).
/// </summary>
public class PgiaSolicitacaoCidadao
{
    public long Id { get; set; }

    // Gerado no servidor; é o que o cidadão usa para acompanhar
    public string Protocolo { get; set; } = string.Empty;

    public long SistemaIaId { get; set; }

    public PgiaSistemaIa? Sistema { get; set; }

    // D21 (arts. 23, I a IV e 11, IV)
    public string Tipo { get; set; } = string.Empty;

    public string SolicitanteNome { get; set; } = string.Empty;

    public string SolicitanteContato { get; set; } = string.Empty;

    public string? ReferenciaDecisao { get; set; }

    public string Descricao { get; set; } = string.Empty;

    public DateTime DataAbertura { get; set; }

    /// <summary>
    /// varchar(40) e não o varchar(20) do schema: "Encaminhada ao Encarregado de Dados"
    /// tem 35 caracteres e não caberia no tipo declarado lá (mesmo caso de origem_registro).
    /// </summary>
    public string Status { get; set; } = PgiaDominios.StatusSolicitacao.Recebida;

    // Em linguagem simples e acessível (art. 23, III e § único)
    public string? Resposta { get; set; }

    // Agente público competente (art. 23, II)
    public Guid? RespondidoPor { get; set; }

    public User? RespondidoPorUser { get; set; }

    public DateTime? DataResposta { get; set; }

    // Reclamações de titulares vão ao Encarregado de Dados (art. 11, IV)
    public bool EncaminhadaDpo { get; set; }

    public DateTime CriadoEm { get; set; }

    public string? CriadoPor { get; set; }

    public DateTime? AlteradoEm { get; set; }

    public string? AlteradoPor { get; set; }
}
