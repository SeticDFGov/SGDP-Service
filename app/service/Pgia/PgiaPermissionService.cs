using app.Auth;
using Microsoft.EntityFrameworkCore;
using Models;
using Models.Pgia;
using service.Interface;

namespace service.Pgia;

/// <summary>
/// Contexto do usuário no módulo PGIA. O papel efetivo considera o perfil admin
/// do SGDP (acesso total) e, fora isso, apenas Users.PapelPgia.
/// </summary>
public class PgiaUserContext
{
    public Guid UserId { get; set; }

    public string Email { get; set; } = string.Empty;

    public string Perfil { get; set; } = string.Empty;

    public string? PapelPgia { get; set; }

    public Guid? UnidadeId { get; set; }

    // Órgão PGIA vinculado à Unidade do usuário (null se não houver)
    public long? OrgaoId { get; set; }

    public bool IsAdmin => Perfil == Perfis.Admin;

    public string PapelEfetivo => IsAdmin ? Perfis.Admin : PapelPgia ?? string.Empty;
}

/// <summary>
/// Recursos do PGIA usados na matriz papel x ação x recurso (crescem por fase).
/// </summary>
public static class PgiaResources
{
    public const string Orgao = "orgao";               // cadastro de órgãos (admin)
    public const string OrgaoDados = "orgao_dados";    // dados do órgão (etapa 1) — edição da SGDI
    public const string Designacao = "designacao";     // Responsável de IA e Encarregado de Dados
    public const string AgenteInfo = "agente_info";    // matrícula, cargo e vínculo das pessoas
    // Papel PGIA e unidade de um usuário. Sem visualização própria na matriz
    // (CanView é permissivo por papel): quem administra o vínculo é quem o enxerga,
    // então os endpoints de gestão de pessoas usam CanEdit(PessoaVinculo).
    public const string PessoaVinculo = "pessoa_vinculo";
    public const string Prazo = "prazo";               // painel de prazos de conformidade
    public const string Sistema = "sistema";           // inventário de sistemas de IA (etapa 2)
    public const string Classificacao = "classificacao"; // classificação de risco (arts. 14 a 18)
    public const string Documento = "documento";       // metadados de documentos (sem upload)
    public const string Aia = "aia";                   // Avaliação de Impacto Algorítmico (art. 22)
    public const string Deliberacao = "deliberacao";   // deliberações do CGTIC (art. 7º)
    public const string Plataforma = "plataforma";     // plataformas públicas de IA generativa (arts. 18 e 20)
    public const string Autorizacao = "autorizacao";   // autorizações excepcionais (arts. 18, § 2º e 21)
    public const string Norma = "norma";               // normas complementares (arts. 8º, III e 26)
    public const string Homologacao = "homologacao";   // avaliação SGDI/CGTIC do inventário (desenho da chefia)
    public const string Publicacao = "publicacao";     // Registro Público e publicações no Portal (art. 24)
    public const string Incidente = "incidente";       // comunicação de incidente grave (arts. 13, II e 30)
    public const string Apuracao = "apuracao";         // apuração pela SGDI (art. 30, § único)
    public const string NaoConformidade = "nao_conformidade"; // descumprimentos (arts. 8º, X e 9º, II)
    public const string Capacitacao = "capacitacao";   // trilhas ProCapIA/DF (arts. 28 e 29)
    public const string RegistroUso = "uso";           // registro de uso de IA (art. 13, IV e V)
    public const string Contrato = "contrato";         // contratos de IA (arts. 21, 25 a 27)
    public const string Legado = "legado";             // triagem de instrumentos anteriores (art. 37)
    public const string Indicador = "indicador";       // indicadores de desempenho (arts. 24, V e 31)
    public const string Relatorio = "relatorio";       // relatórios semestral e anual (arts. 32 e 33)
    public const string AuditoriaTecnica = "auditoria_tecnica"; // auditorias de Alto Risco (arts. 25, § 2º e 34)
    public const string Solicitacao = "solicitacao";   // solicitações do cidadão (art. 23) — resposta pelo órgão
}

/// <summary>
/// Autorização do módulo PGIA, espelhando o desenho do PermissionService do SGDP
/// (papel + ação + recurso, com filtro de dados por órgão) sem tocá-lo.
/// Toda validação acontece no backend; a checagem do front é só usabilidade.
///
/// Modelo de administração (decisão do dono do produto, a SGDI): a SGDI pré-cadastra
/// tudo e entrega o órgão pronto — dados do órgão (orgao_dados), dados de agente
/// público (agente_info) e vínculo de acesso das pessoas (pessoa_vinculo) são
/// escritos pela SGDI/admin; ao papel do órgão restam as designações e a operação.
/// </summary>
public class PgiaPermissionService : IPgiaPermissionService
{
    private readonly AppDbContext _context;

    public PgiaPermissionService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<PgiaUserContext?> GetContextAsync(string email)
    {
        var user = await _context.Users
            .Include(u => u.Unidade)
            .FirstOrDefaultAsync(u => u.Email == email);

        if (user == null) return null;

        var ctx = new PgiaUserContext
        {
            UserId = user.Id,
            Email = user.Email,
            Perfil = user.Perfil,
            PapelPgia = user.PapelPgia,
            UnidadeId = user.Unidade?.id
        };

        if (ctx.UnidadeId != null)
        {
            // Só órgão ATIVO resolve o escopo: órgão desativado não pode continuar
            // dando acesso ao papel de órgão (falha fechada).
            ctx.OrgaoId = await _context.PgiaOrgaos
                .Where(o => o.UnidadeId == ctx.UnidadeId && o.Ativo)
                .Select(o => (long?)o.Id)
                .FirstOrDefaultAsync();
        }

        return ctx;
    }

    public bool CanView(PgiaUserContext ctx, string resource)
    {
        return ctx.PapelEfetivo switch
        {
            Perfis.Admin => true,
            PapeisPgia.Sgdi => true,
            PapeisPgia.Cgtic => true,
            PapeisPgia.Orgao => true, // restrito ao próprio órgão via CanAccessOrgao
            // A auditoria externa só enxerga as auditorias designadas a ela (filtro no service)
            PapeisPgia.Auditoria => resource is PgiaResources.AuditoriaTecnica,
            _ => false
        };
    }

    public bool CanCreate(PgiaUserContext ctx, string resource)
    {
        return ctx.PapelEfetivo switch
        {
            Perfis.Admin => true,
            // A SGDI pré-cadastra as pessoas do órgão (agente_info) junto com o órgão
            PapeisPgia.Sgdi => resource is PgiaResources.Orgao or PgiaResources.AgenteInfo
                or PgiaResources.Plataforma or PgiaResources.Documento
                or PgiaResources.Autorizacao or PgiaResources.Norma or PgiaResources.NaoConformidade
                or PgiaResources.Relatorio or PgiaResources.AuditoriaTecnica,
            // O comitê registra a ata da própria deliberação (documento central)
            PapeisPgia.Cgtic => resource is PgiaResources.Deliberacao or PgiaResources.Norma
                or PgiaResources.Documento,
            // O inventário, a classificação, a AIA e a operação contínua são do órgão
            PapeisPgia.Orgao => resource is PgiaResources.Designacao
                or PgiaResources.Sistema or PgiaResources.Classificacao or PgiaResources.Documento
                or PgiaResources.Aia or PgiaResources.Incidente or PgiaResources.NaoConformidade
                or PgiaResources.Capacitacao or PgiaResources.Contrato or PgiaResources.Legado
                or PgiaResources.Indicador or PgiaResources.Relatorio,
            _ => false
        };
    }

    public bool CanEdit(PgiaUserContext ctx, string resource)
    {
        return ctx.PapelEfetivo switch
        {
            Perfis.Admin => true,
            // Cadastro do órgão pronto: dados do órgão, dados de agente público e
            // vínculo de acesso (papel PGIA + unidade) são escrita da SGDI, em qualquer órgão
            PapeisPgia.Sgdi => resource is PgiaResources.Orgao or PgiaResources.OrgaoDados
                or PgiaResources.AgenteInfo or PgiaResources.PessoaVinculo or PgiaResources.Prazo
                or PgiaResources.Plataforma or PgiaResources.Autorizacao or PgiaResources.Norma
                or PgiaResources.Homologacao or PgiaResources.Publicacao
                or PgiaResources.Apuracao or PgiaResources.NaoConformidade
                or PgiaResources.Relatorio or PgiaResources.AuditoriaTecnica
                or PgiaResources.Documento,
            PapeisPgia.Cgtic => resource is PgiaResources.Deliberacao or PgiaResources.Norma
                or PgiaResources.Documento,
            // Ao órgão restam as designações e a operação: ele vê os próprios dados
            // e as próprias pessoas (CanView), mas não edita orgao_dados nem agente_info
            PapeisPgia.Orgao => resource is PgiaResources.Designacao
                or PgiaResources.Prazo or PgiaResources.Sistema
                or PgiaResources.Aia or PgiaResources.Incidente or PgiaResources.NaoConformidade
                or PgiaResources.Capacitacao or PgiaResources.Contrato or PgiaResources.Legado
                or PgiaResources.Indicador or PgiaResources.Relatorio or PgiaResources.Solicitacao
                // Anexar/substituir o arquivo é escrita do documento (rodada de anexos):
                // o órgão anexa nos documentos do próprio órgão (escopo em CanAccessOrgao)
                or PgiaResources.Documento,
            // A auditoria externa edita só parecer e datas das auditorias designadas (restrição no service)
            PapeisPgia.Auditoria => resource is PgiaResources.AuditoriaTecnica,
            _ => false
        };
    }

    /// <summary>
    /// Art. 13: qualquer agente autenticado cujo órgão foi resolvido pela Unidade
    /// pode registrar uso de IA e avisar o Responsável sobre um problema — sem papel PGIA.
    /// </summary>
    public bool PodeRegistrarUso(PgiaUserContext ctx)
    {
        return ctx.OrgaoId != null;
    }

    public bool CanAccessOrgao(PgiaUserContext ctx, long orgaoId)
    {
        return ctx.PapelEfetivo switch
        {
            Perfis.Admin => true,
            PapeisPgia.Sgdi => true,
            PapeisPgia.Cgtic => true,
            PapeisPgia.Orgao => ctx.OrgaoId == orgaoId,
            _ => false
        };
    }

    public IQueryable<PgiaOrgao> GetFilteredOrgaosQuery(PgiaUserContext ctx)
    {
        var query = _context.PgiaOrgaos
            .Include(o => o.Unidade)
            .AsQueryable();

        return ctx.PapelEfetivo switch
        {
            Perfis.Admin => query,
            PapeisPgia.Sgdi => query,
            PapeisPgia.Cgtic => query,
            PapeisPgia.Orgao => query.Where(o => ctx.OrgaoId != null && o.Id == ctx.OrgaoId),
            _ => query.Where(o => false)
        };
    }

    public IQueryable<PgiaSistemaIa> GetFilteredSistemasQuery(PgiaUserContext ctx)
    {
        var query = _context.PgiaSistemasIa
            .Include(s => s.Orgao)
            .AsQueryable();

        return ctx.PapelEfetivo switch
        {
            Perfis.Admin => query,
            PapeisPgia.Sgdi => query,
            PapeisPgia.Cgtic => query,
            PapeisPgia.Orgao => query.Where(s => ctx.OrgaoId != null && s.OrgaoId == ctx.OrgaoId),
            _ => query.Where(s => false)
        };
    }
}
