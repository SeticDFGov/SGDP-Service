using api.Pgia;
using Models.Pgia;
using service.Pgia;

namespace service.Interface;

public interface IPgiaGovernancaService
{
    // Deliberações do CGTIC (art. 7º)
    Task<List<PgiaDeliberacaoResponse>> ListarDeliberacoesAsync();
    Task<List<PgiaDeliberacaoResponse>> ListarDeliberacoesPorSistemaAsync(long sistemaId);
    Task<PgiaDeliberacaoResponse> CriarDeliberacaoAsync(PgiaDeliberacaoCreateDTO dto, PgiaUserContext ctx);

    // Plataformas públicas de IA generativa (arts. 8º, IV, 18 e 20)
    Task<List<PgiaPlataformaResponse>> ListarPlataformasAsync();
    Task<PgiaPlataformaResponse> CriarPlataformaAsync(PgiaPlataformaCreateDTO dto, PgiaUserContext ctx);
    Task<PgiaPlataformaResponse> AtualizarPlataformaAsync(long id, PgiaPlataformaUpdateDTO dto, PgiaUserContext ctx);

    // Normas complementares (arts. 8º, III e 26)
    Task<List<PgiaNormaResponse>> ListarNormasAsync();
    Task<PgiaNormaResponse> CriarNormaAsync(PgiaNormaCreateDTO dto, PgiaUserContext ctx);
    Task<PgiaNormaResponse> AtualizarNormaAsync(long id, PgiaNormaUpdateDTO dto, PgiaUserContext ctx);

    // Autorizações excepcionais (arts. 18, § 2º e 21)
    Task<List<PgiaAutorizacaoResponse>> ListarAutorizacoesAsync(long? orgaoId);
    Task<PgiaAutorizacaoResponse> CriarAutorizacaoAsync(PgiaAutorizacaoCreateDTO dto, PgiaUserContext ctx);
    Task<PgiaAutorizacaoResponse> AtualizarAutorizacaoAsync(long id, PgiaAutorizacaoUpdateDTO dto, PgiaUserContext ctx);

    /// <summary>Resumo de todos os sistemas, para o painel das instâncias centrais.</summary>
    Task<List<PgiaSistemaResumoResponse>> ListarSistemasResumoAsync();

    // Histórico de decisões do CGTIC (relatório de auditoria)

    /// <summary>Casos submetidos ao comitê, contagens e demais deliberações.</summary>
    Task<PgiaCgticHistoricoResponse> ObterHistoricoCgticAsync();

    /// <summary>O mesmo histórico em PDF, para juntada ao processo.</summary>
    Task<byte[]> GerarPdfHistoricoCgticAsync();

    /// <summary>Entidade crua do sistema, para o controller checar o escopo de órgão.</summary>
    Task<PgiaSistemaIa?> GetSistemaEntidadeAsync(long sistemaId);
}
