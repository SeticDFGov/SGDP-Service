using api.Pgia;
using app.Models;
using Microsoft.EntityFrameworkCore;
using Models;
using Models.Pgia;

namespace test.pgia;

/// <summary>
/// Base dos testes PGIA: AppDbContext InMemory com dois órgãos (SES e SEEC),
/// cada um vinculado a uma Unidade, e um usuário por papel relevante.
/// </summary>
public abstract class PgiaTestBase : IDisposable
{
    protected readonly AppDbContext Context;

    /// <summary>Nome do banco InMemory, para abrir um segundo contexto sobre a mesma base.</summary>
    protected readonly string NomeBanco;

    protected readonly Unidade UnidadeSes;
    protected readonly Unidade UnidadeSeec;
    protected readonly PgiaOrgao OrgaoSes;
    protected readonly PgiaOrgao OrgaoSeec;

    protected readonly User UserOrgaoSes;      // pgia_orgao na SES
    protected readonly User UserOrgaoSeec;     // pgia_orgao na SEEC
    protected readonly User UserSgdi;          // pgia_sgdi
    protected readonly User UserCgtic;         // pgia_cgtic
    protected readonly User UserAuditoria;     // pgia_auditoria
    protected readonly User UserAdmin;         // perfil admin do SGDP, sem papel PGIA
    protected readonly User UserSemPapel;      // usuário comum, sem papel PGIA

    protected PgiaTestBase()
    {
        NomeBanco = Guid.NewGuid().ToString();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: NomeBanco)
            .Options;
        Context = new AppDbContext(options);
        // Aplica o seed (HasData) das obrigações-modelo de prazo
        Context.Database.EnsureCreated();

        UnidadeSes = new Unidade { id = Guid.NewGuid(), Nome = "Secretaria de Saúde" };
        UnidadeSeec = new Unidade { id = Guid.NewGuid(), Nome = "Secretaria de Economia" };
        Context.Unidades.AddRange(UnidadeSes, UnidadeSeec);

        OrgaoSes = new PgiaOrgao
        {
            Sigla = "SES",
            Nome = "Secretaria de Estado de Saúde do Distrito Federal",
            NaturezaJuridica = PgiaDominios.NaturezaJuridica.AdministracaoDireta,
            UnidadeId = UnidadeSes.id,
            CriadoEm = DateTime.UtcNow
        };
        OrgaoSeec = new PgiaOrgao
        {
            Sigla = "SEEC",
            Nome = "Secretaria de Estado de Economia do Distrito Federal",
            NaturezaJuridica = PgiaDominios.NaturezaJuridica.AdministracaoDireta,
            UnidadeId = UnidadeSeec.id,
            CriadoEm = DateTime.UtcNow
        };
        Context.PgiaOrgaos.AddRange(OrgaoSes, OrgaoSeec);

        UserOrgaoSes = NovoUser("maria@ses.df.gov.br", "Maria Andrade", "gestor", "pgia_orgao", UnidadeSes);
        UserOrgaoSeec = NovoUser("joao@seec.df.gov.br", "João Pires", "basico", "pgia_orgao", UnidadeSeec);
        UserSgdi = NovoUser("sgdi@sgdi.df.gov.br", "Ana SGDI", "basico", "pgia_sgdi", UnidadeSeec);
        UserCgtic = NovoUser("cgtic@sgdi.df.gov.br", "Caio CGTIC", "basico", "pgia_cgtic", UnidadeSeec);
        UserAuditoria = NovoUser("aud@auditoria.com", "Alice Auditora", "basico", "pgia_auditoria", null);
        UserAdmin = NovoUser("admin@subgd.df.gov.br", "Admin", "admin", null, UnidadeSeec);
        UserSemPapel = NovoUser("comum@ses.df.gov.br", "Carlos Comum", "gestor", null, UnidadeSes);

        Context.SaveChanges();
    }

    /// <summary>
    /// Completa o checklist como a tela agrupada faz: os grupos sem nenhum inciso
    /// marcado recebem "Nenhuma das alternativas acima". O questionário exige
    /// resposta explícita por grupo (incisos XOR nenhuma), então os fixtures que
    /// só marcam um artigo precisam responder os outros dois.
    /// </summary>
    protected static PgiaChecklistDTO ChecklistRespondido(PgiaChecklistDTO? checklist = null)
    {
        var c = checklist ?? new PgiaChecklistDTO();
        c.Q15Nenhuma = c.Q15.Count == 0;
        c.Q16Nenhuma = c.Q16.Count == 0;
        c.Q17Nenhuma = c.Q17.Count == 0;
        return c;
    }

    /// <summary>
    /// Risco declarado válido para o grupo "Outros" (matriz da CGDF, dentro do
    /// subconjunto permitido). Serve às fixtures que respondem "nenhuma" nos três
    /// grupos, onde declarar ao menos um risco virou obrigatório.
    /// </summary>
    protected static PgiaRiscoOutroDTO RiscoOutroExemplo(
        string descricao = "Indisponibilidade do serviço em horário de pico") => new()
    {
        DescricaoRisco = descricao,
        AcaoMitigacao = "Monitoramento contínuo e plano de contingência",
        ResponsavelNome = "Maria Andrade",
        ResponsavelEmail = "maria@ses.df.gov.br",
        Probabilidade = PgiaDominios.EscalaCgdf.Probabilidade.Raro,
        Consequencia = PgiaDominios.EscalaCgdf.Consequencia.Menor
    };

    /// <summary>
    /// Riscos declarados a anexar à classificação: sistema sem nenhuma situação dos
    /// arts. 15 a 17 precisa declarar ao menos um risco próprio. Só acrescenta onde
    /// a regra exige, para não mexer no que cada fixture já exercitava.
    /// </summary>
    protected static List<PgiaRiscoOutroDTO> OutrosRiscosSeNecessario(PgiaChecklistDTO checklist) =>
        OutrosRiscosSeNecessario(checklist.Q15, checklist.Q16, checklist.Q17);

    /// <inheritdoc cref="OutrosRiscosSeNecessario(PgiaChecklistDTO)"/>
    protected static List<PgiaRiscoOutroDTO> OutrosRiscosSeNecessario(
        List<string>? q15 = null, List<string>? q16 = null, List<string>? q17 = null) =>
        (q15?.Count ?? 0) == 0 && (q16?.Count ?? 0) == 0 && (q17?.Count ?? 0) == 0
            ? new List<PgiaRiscoOutroDTO> { RiscoOutroExemplo() }
            : new List<PgiaRiscoOutroDTO>();

    private User NovoUser(string email, string nome, string perfil, string? papelPgia, Unidade? unidade)
    {
        var user = new User
        {
            Email = email,
            Nome = nome,
            Perfil = perfil,
            PapelPgia = papelPgia,
            Unidade = unidade
        };
        Context.Users.Add(user);
        return user;
    }

    public void Dispose()
    {
        Context.Database.EnsureDeleted();
        Context.Dispose();
        GC.SuppressFinalize(this);
    }
}
