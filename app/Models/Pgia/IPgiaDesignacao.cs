using app.Models;

namespace Models.Pgia;

/// <summary>
/// O que as três designações do órgão têm em comum (Responsável de IA, Encarregado
/// de Dados e Curador de Dados de Negócio): pessoa, posição (titular ou suplente),
/// ato, comunicação à SGDI, cópia do ato, vigência e auditoria. Serve só ao código
/// (serviço e repositório genéricos); o EF ignora a interface no mapeamento.
///
/// Ato pendente: as linhas importadas da planilha da SGDI (carga SQL) chegam sem o
/// ato, sem a comunicação à SGDI e sem o início da vigência; esses campos só podem
/// ficar nulos com <see cref="AtoPendente"/> ligado (CHECK no banco). A API sempre
/// exige tudo, e completar pela tela desliga a marca.
/// </summary>
public interface IPgiaDesignacao
{
    long Id { get; set; }

    long OrgaoId { get; set; }

    PgiaOrgao? Orgao { get; set; }

    Guid AgenteId { get; set; }

    User? Agente { get; set; }

    string Posicao { get; set; }

    bool AtoPendente { get; set; }

    string? AtoTipo { get; set; }

    string? AtoNumero { get; set; }

    DateOnly? AtoData { get; set; }

    string? ProcessoSeiComunicacao { get; set; }

    DateOnly? DataComunicacaoSgdi { get; set; }

    long? DocumentoId { get; set; }

    DateOnly? InicioVigencia { get; set; }

    DateOnly? FimVigencia { get; set; }

    bool Ativo { get; set; }

    DateTime CriadoEm { get; set; }

    string? CriadoPor { get; set; }

    DateTime? AlteradoEm { get; set; }

    string? AlteradoPor { get; set; }
}
