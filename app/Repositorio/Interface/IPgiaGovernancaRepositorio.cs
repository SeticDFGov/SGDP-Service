using Models.Pgia;

namespace Repositorio.Interface;

public interface IPgiaGovernancaRepositorio
{
    // Deliberações do CGTIC (art. 7º)
    Task<PgiaDeliberacaoCgtic?> GetDeliberacaoByIdAsync(long id);
    Task<List<PgiaDeliberacaoCgtic>> ListarDeliberacoesAsync();
    Task<List<PgiaDeliberacaoCgtic>> ListarDeliberacoesPorSistemaAsync(long sistemaId);
    void AddDeliberacao(PgiaDeliberacaoCgtic deliberacao);

    // Plataformas públicas de IA generativa (arts. 8º, IV, 18 e 20)
    Task<PgiaPlataformaIaGenerativa?> GetPlataformaByIdAsync(long id);
    Task<PgiaPlataformaIaGenerativa?> GetPlataformaByNomeAsync(string nome);
    Task<List<PgiaPlataformaIaGenerativa>> ListarPlataformasAsync();
    void AddPlataforma(PgiaPlataformaIaGenerativa plataforma);

    // Normas complementares (arts. 8º, III e 26)
    Task<PgiaNormaComplementar?> GetNormaByIdAsync(long id);
    Task<List<PgiaNormaComplementar>> ListarNormasAsync();
    void AddNorma(PgiaNormaComplementar norma);

    // Autorizações excepcionais (arts. 18, § 2º e 21)
    Task<PgiaAutorizacaoExcepcional?> GetAutorizacaoByIdAsync(long id);
    /// <param name="orgaoId">null lista todas (escopo central); com valor, só as do órgão.</param>
    Task<List<PgiaAutorizacaoExcepcional>> ListarAutorizacoesAsync(long? orgaoId);
    void AddAutorizacao(PgiaAutorizacaoExcepcional autorizacao);

    /// <summary>Todos os sistemas do inventário, para o painel central escolher sobre quem deliberar.</summary>
    Task<List<PgiaSistemaIa>> ListarSistemasAsync();

    // Apoio: existência das entidades referenciadas
    Task<PgiaSistemaIa?> GetSistemaByIdAsync(long id);
    Task<PgiaOrgao?> GetOrgaoByIdAsync(long id);
    Task<PgiaDocumento?> GetDocumentoByIdAsync(long id);
    Task<PgiaContratoIa?> GetContratoByIdAsync(long id);

    Task SaveChangesAsync();
}
