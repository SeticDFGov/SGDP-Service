using Models.Pgia;

namespace Repositorio.Interface;

public interface IPgiaContratoRepositorio
{
    // Contratos de IA (arts. 21, 25 a 27)
    Task<PgiaContratoIa?> GetContratoByIdAsync(long id);
    Task<List<PgiaContratoIa>> ListarContratosPorOrgaoAsync(long orgaoId);
    void AddContrato(PgiaContratoIa contrato);

    // Triagem de instrumentos anteriores ao decreto (art. 37)
    Task<PgiaInstrumentoLegado?> GetLegadoByIdAsync(long id);
    Task<List<PgiaInstrumentoLegado>> ListarLegadosPorOrgaoAsync(long orgaoId);
    void AddLegado(PgiaInstrumentoLegado legado);

    // Apoio: existência das entidades referenciadas
    Task<PgiaSistemaIa?> GetSistemaByIdAsync(long id);
    Task<PgiaDocumento?> GetDocumentoByIdAsync(long id);
    Task<PgiaAutorizacaoExcepcional?> GetAutorizacaoByIdAsync(long id);

    Task SaveChangesAsync();
}
