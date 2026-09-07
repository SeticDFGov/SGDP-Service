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

    // Perfil nunca é persistido (vem da claim do token a cada requisição); a
    // fixture guarda o perfil de cada persona à parte, para os testes passarem
    // ao GetContextAsync do mesmo jeito que um controller leria do token.
    protected readonly Dictionary<Guid, string> PerfilPorUserId = new();
    protected readonly Dictionary<string, string> PerfilPorEmail = new();

    protected PgiaTestBase()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
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

    private User NovoUser(string email, string nome, string perfil, string? papelPgia, Unidade? unidade)
    {
        var user = new User
        {
            Email = email,
            Nome = nome,
            PapelPgia = papelPgia,
            Unidade = unidade
        };
        Context.Users.Add(user);
        PerfilPorUserId[user.Id] = perfil;
        PerfilPorEmail[email] = perfil;
        return user;
    }

    /// <summary>Perfil da persona (equivalente ao que um controller leria da claim do token).</summary>
    protected string PerfilDe(User user) => PerfilPorUserId[user.Id];

    /// <summary>Perfil da persona pelo e-mail (para testes parametrizados por [InlineData]).</summary>
    protected string PerfilDe(string email) => PerfilPorEmail[email];

    public void Dispose()
    {
        Context.Database.EnsureDeleted();
        Context.Dispose();
        GC.SuppressFinalize(this);
    }
}
