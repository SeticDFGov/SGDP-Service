using System.Globalization;
using System.Text.RegularExpressions;
using api.Common;
using api.Planejamento;
using app.Auth;
using demanda_service.Helpers;
using Microsoft.EntityFrameworkCore;
using Models;
using Models.Pgia;
using Models.Planejamento;
using service.Interface;

namespace service.Planejamento;

/// <summary>
/// A inadimplência dos órgãos (E8; art. 7º, § 3º, e art. 11 do Decreto nº 48.899/2026):
/// <list type="bullet">
/// <item>notificar: a obrigação pendente, o prazo descumprido, a data, o documento e o SEI; o prazo
/// para regularizar ou justificar é de 5 dias úteis depois da notificação
/// (<see cref="DateTimeHelper.AdicionarDiasUteis"/>: sem sábado, domingo e feriado);</item>
/// <item>justificar: a justificativa aceita encerra a notificação (só a notificada);</item>
/// <item>registrar: só a notificada e só depois do prazo (senão 409), com o motivo (descumprimento de
/// prazo, omissão reiterada ou recusa injustificada) e a nota de motivação do art. 11, § 1º; a data
/// da comunicação ao controle interno é opcional;</item>
/// <item>sanear: da notificada ou da inadimplente, com a data da regularização; a marca sai do painel
/// (art. 11, § 2º).</item>
/// </list>
/// Ler: papéis globais e admin geral (todos os órgãos); equipe e consulta do órgão, o próprio.
/// Gravar: pe_admin, pe_sgdi e admin geral. Erros de campo em Campos, pelo nome da propriedade do corpo.
/// </summary>
public partial class PeInadimplenciaService : IPeInadimplenciaService
{
    public const int DiasUteisParaRegularizar = 5;
    private const int TamanhoPaginaPadrao = 20;
    private const int TamanhoPaginaMaximo = 100;
    public const int MaximoObrigacao = 500;
    public const int MaximoDocumento = 200;
    public const int MaximoTexto = 2000;
    public const int MaximoNota = 10000;

    [GeneratedRegex(@"^\d{5}-\d{8}/\d{4}-\d{2}$")]
    private static partial Regex FormatoSei();

    private readonly AppDbContext _context;
    private readonly IPePermissionService _permissoes;

    public PeInadimplenciaService(AppDbContext context, IPePermissionService permissoes)
    {
        _context = context;
        _permissoes = permissoes;
    }

    // ── Leitura ─────────────────────────────────────────────────────────────

    public async Task<PagedResponse<PeInadimplenciaResponse>> ListarAsync(PeInadimplenciasConsulta consulta, PeUserContext ctx)
    {
        long? orgaoId = consulta.OrgaoId;
        if (!_permissoes.PodeVerPaineis(ctx))
        {
            // Equipe e consulta do órgão: só as do próprio órgão
            if (!PapeisPlanejamento.EhDeOrgao(ctx.Papel))
                throw new ApiException(ErrorCode.PeSemPermissao, "A lista das inadimplências é da SGDI, da Secretaria do CGTIC e do administrador do módulo.");
            var proprio = ctx.OrgaoId ?? throw SemOrgao();
            if (orgaoId != null && orgaoId != proprio)
                throw new ApiException(ErrorCode.PeSemPermissao, "Você só vê as inadimplências do seu próprio órgão.");
            orgaoId = proprio;
        }

        var pageSize = consulta.PageSize < 1 ? TamanhoPaginaPadrao : Math.Min(consulta.PageSize, TamanhoPaginaMaximo);
        var page = Math.Clamp(consulta.Page, 1, int.MaxValue / pageSize);

        var query = _context.PeInadimplencias.AsNoTracking().AsQueryable();
        if (orgaoId != null) query = query.Where(i => i.OrgaoId == orgaoId);
        if (!string.IsNullOrWhiteSpace(consulta.Situacao))
        {
            // Fora do domínio: lista vazia, nunca "todas" em silêncio
            var situacao = consulta.Situacao.Trim();
            query = PeDominios.SituacaoInadimplencia.Todas.Contains(situacao)
                ? query.Where(i => i.Situacao == situacao)
                : query.Where(i => false);
        }

        var total = await query.CountAsync();
        var linhas = await Ordenar(query)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
        return new PagedResponse<PeInadimplenciaResponse>(await RespostasAsync(linhas), total, page, pageSize);
    }

    public async Task<List<PeInadimplenciaResponse>> DoOrgaoAsync(long orgaoId, PeUserContext ctx)
    {
        if (!_permissoes.PodeLerReferenciais(ctx))
            throw new ApiException(ErrorCode.PeSemPermissao, "Você ainda não tem papel na Governança Estratégica. Fale com o administrador do módulo.");
        if (!_permissoes.PodeVerOrgao(ctx, orgaoId))
            throw new ApiException(ErrorCode.PeSemPermissao, "Você só vê as inadimplências do seu próprio órgão.");
        if (!await _context.PgiaOrgaos.AnyAsync(o => o.Id == orgaoId))
            throw new ApiException(ErrorCode.PeOrgaoNaoEncontrado, "Órgão não encontrado.");
        var linhas = await Ordenar(_context.PeInadimplencias.AsNoTracking().Where(i => i.OrgaoId == orgaoId)).ToListAsync();
        return await RespostasAsync(linhas);
    }

    /// <summary>As vigentes primeiro (pelo prazo, a mais urgente antes); depois as outras, da notificação mais nova.</summary>
    internal static IQueryable<PeInadimplencia> Ordenar(IQueryable<PeInadimplencia> query) => query
        .OrderBy(i => i.Situacao == PeDominios.SituacaoInadimplencia.Notificado || i.Situacao == PeDominios.SituacaoInadimplencia.Inadimplente ? 0 : 1)
        .ThenBy(i => i.Situacao == PeDominios.SituacaoInadimplencia.Notificado || i.Situacao == PeDominios.SituacaoInadimplencia.Inadimplente
            ? i.Prazo
            : DateOnly.MaxValue)
        .ThenByDescending(i => i.NotificadoEm)
        .ThenByDescending(i => i.Id);

    // ── Escrita ─────────────────────────────────────────────────────────────

    public async Task<PeInadimplenciaResponse> NotificarAsync(long orgaoId, PeInadimplenciaNotificarDTO dto, PeUserContext ctx)
    {
        ConferirQuemGrava(ctx);
        var orgao = await _context.PgiaOrgaos.AsNoTracking().FirstOrDefaultAsync(o => o.Id == orgaoId && o.Ativo)
            ?? throw new ApiException(ErrorCode.PeOrgaoNaoEncontrado, "Órgão não encontrado ou desativado.");

        var hoje = PeCiclos.Hoje();
        var erros = new Dictionary<string, string>();
        var obrigacao = Texto(dto.Obrigacao, MaximoObrigacao, nameof(dto.Obrigacao), erros,
            "Diga a comunicação pendente: a obrigação que o órgão não cumpriu.", "A obrigação");
        var prazoDescumprido = Texto(dto.PrazoDescumprido, MaximoObrigacao, nameof(dto.PrazoDescumprido), erros,
            "Diga o prazo que o órgão descumpriu.", "O prazo descumprido");
        var notificadoEm = Data(dto.NotificadoEm, nameof(dto.NotificadoEm), erros, "Informe a data da notificação.", hoje,
            "A data da notificação não pode ser depois de hoje.");
        var documento = Texto(dto.Documento, MaximoDocumento, nameof(dto.Documento), erros,
            "Informe o documento da notificação (por exemplo, o número do ofício).", "O documento");
        var sei = Sei(dto.Sei, nameof(dto.Sei), erros);
        if (erros.Count > 0) throw Invalida(erros);

        var agora = DateTime.UtcNow;
        var inadimplencia = new PeInadimplencia
        {
            OrgaoId = orgao.Id,
            Obrigacao = obrigacao!,
            PrazoDescumprido = prazoDescumprido!,
            NotificadoEm = notificadoEm!.Value,
            Documento = documento!,
            Sei = sei,
            Prazo = Prazo(notificadoEm.Value),
            Situacao = PeDominios.SituacaoInadimplencia.Notificado,
            CriadoEm = agora,
            CriadoPor = ctx.Email
        };
        _context.PeInadimplencias.Add(inadimplencia);
        await _context.SaveChangesAsync();
        return Resposta(inadimplencia, orgao, hoje);
    }

    public async Task<PeInadimplenciaResponse> JustificarAsync(long id, PeInadimplenciaJustificarDTO dto, PeUserContext ctx)
    {
        ConferirQuemGrava(ctx);
        var inadimplencia = await ParaGravarAsync(id);
        if (inadimplencia.Situacao != PeDominios.SituacaoInadimplencia.Notificado)
            throw new ApiException(ErrorCode.PeInadimplenciaSituacaoInvalida,
                $"Esta notificação está {Rotulo(inadimplencia)}: só a notificação aberta aceita justificativa.");

        var erros = new Dictionary<string, string>();
        var justificativa = Texto(dto.Justificativa, MaximoTexto, nameof(dto.Justificativa), erros,
            "Escreva a justificativa que a SGDI aceitou.", "A justificativa");
        if (erros.Count > 0) throw Invalida(erros);

        inadimplencia.Situacao = PeDominios.SituacaoInadimplencia.Justificado;
        inadimplencia.Justificativa = justificativa;
        return await GravarAsync(inadimplencia, ctx);
    }

    public async Task<PeInadimplenciaResponse> RegistrarAsync(long id, PeInadimplenciaRegistrarDTO dto, PeUserContext ctx)
    {
        ConferirQuemGrava(ctx);
        var inadimplencia = await ParaGravarAsync(id);
        if (inadimplencia.Situacao != PeDominios.SituacaoInadimplencia.Notificado)
            throw new ApiException(ErrorCode.PeInadimplenciaSituacaoInvalida,
                $"Esta notificação está {Rotulo(inadimplencia)}. A inadimplência só é registrada a partir de uma notificação aberta.");
        var hoje = PeCiclos.Hoje();
        if (hoje <= inadimplencia.Prazo)
            throw new ApiException(ErrorCode.PeInadimplenciaPrazoAberto,
                $"O prazo para regularizar ou justificar vai até {PeCiclos.Data(inadimplencia.Prazo)}. A inadimplência só pode ser registrada "
                + $"a partir de {PeCiclos.Data(inadimplencia.Prazo.AddDays(1))} (art. 11, II, do Decreto nº 48.899/2026).");

        var erros = new Dictionary<string, string>();
        var motivo = dto.Motivo?.Trim();
        if (string.IsNullOrEmpty(motivo) || !PeDominios.MotivoInadimplencia.Todos.Contains(motivo))
            erros[nameof(dto.Motivo)] = "Escolha o motivo: descumprimento de prazo, omissão reiterada ou recusa injustificada de comunicação.";
        var nota = Texto(dto.NotaMotivacao, MaximoNota, nameof(dto.NotaMotivacao), erros,
            "Escreva a nota de motivação que acompanha a comunicação ao controle interno (art. 11, § 1º).", "A nota de motivação");
        var comunicado = Data(dto.ComunicadoControleEm, nameof(dto.ComunicadoControleEm), erros, null, hoje,
            "A data da comunicação ao controle interno não pode ser depois de hoje.");
        if (comunicado is DateOnly data && data < inadimplencia.NotificadoEm)
            erros[nameof(dto.ComunicadoControleEm)] = "A comunicação ao controle interno não pode ser antes da notificação.";
        if (erros.Count > 0) throw Invalida(erros);

        inadimplencia.Situacao = PeDominios.SituacaoInadimplencia.Inadimplente;
        inadimplencia.Motivo = motivo;
        inadimplencia.NotaMotivacao = nota;
        inadimplencia.ComunicadoControleEm = comunicado;
        inadimplencia.RegistradoEm = DateTime.UtcNow;
        inadimplencia.RegistradoPor = ctx.Email;
        return await GravarAsync(inadimplencia, ctx);
    }

    public async Task<PeInadimplenciaResponse> SanearAsync(long id, PeInadimplenciaSanearDTO dto, PeUserContext ctx)
    {
        ConferirQuemGrava(ctx);
        var inadimplencia = await ParaGravarAsync(id);
        if (!PeDominios.SituacaoInadimplencia.Vigentes.Contains(inadimplencia.Situacao))
            throw new ApiException(ErrorCode.PeInadimplenciaSituacaoInvalida,
                $"Esta notificação está {Rotulo(inadimplencia)} e não tem o que sanear.");

        var hoje = PeCiclos.Hoje();
        var erros = new Dictionary<string, string>();
        var saneadoEm = Data(dto.SaneadoEm, nameof(dto.SaneadoEm), erros, "Informe a data em que o órgão regularizou a comunicação.", hoje,
            "A data do saneamento não pode ser depois de hoje.");
        if (saneadoEm is DateOnly data && data < inadimplencia.NotificadoEm)
            erros[nameof(dto.SaneadoEm)] = "A data do saneamento não pode ser antes da notificação.";
        var observacao = Texto(dto.Observacao, MaximoTexto, nameof(dto.Observacao), erros, null, "A observação");
        if (erros.Count > 0) throw Invalida(erros);

        inadimplencia.Situacao = PeDominios.SituacaoInadimplencia.Saneado;
        inadimplencia.SaneadoEm = saneadoEm;
        inadimplencia.Observacao = observacao;
        return await GravarAsync(inadimplencia, ctx);
    }

    // ── Regras ──────────────────────────────────────────────────────────────

    /// <summary>O último dia para regularizar ou justificar: 5 dias úteis depois da notificação (art. 11, II).</summary>
    public static DateOnly Prazo(DateOnly notificadoEm) =>
        DateOnly.FromDateTime(DateTimeHelper.AdicionarDiasUteis(notificadoEm.ToDateTime(TimeOnly.MinValue), DiasUteisParaRegularizar));

    /// <summary>Os dias úteis depois de hoje até o prazo, inclusive (0 no dia do prazo e depois dele).</summary>
    public static int DiasUteisAte(DateOnly prazo, DateOnly hoje)
    {
        var dias = 0;
        for (var dia = hoje.AddDays(1); dia <= prazo; dia = dia.AddDays(1))
            if (DateTimeHelper.EhDiaUtil(dia.ToDateTime(TimeOnly.MinValue))) dias++;
        return dias;
    }

    internal static PeInadimplenciaResponse Resposta(PeInadimplencia i, PgiaOrgao? orgao, DateOnly hoje) => new()
    {
        Id = i.Id,
        OrgaoId = i.OrgaoId,
        OrgaoSigla = orgao?.Sigla ?? string.Empty,
        OrgaoNome = orgao?.Nome ?? string.Empty,
        Obrigacao = i.Obrigacao,
        PrazoDescumprido = i.PrazoDescumprido,
        NotificadoEm = i.NotificadoEm,
        Documento = i.Documento,
        Sei = i.Sei,
        Prazo = i.Prazo,
        DiasUteisRestantes = i.Situacao == PeDominios.SituacaoInadimplencia.Notificado ? DiasUteisAte(i.Prazo, hoje) : 0,
        Vencido = hoje > i.Prazo,
        Situacao = i.Situacao,
        SituacaoRotulo = PeDominios.SituacaoInadimplencia.Rotulo(i.Situacao),
        Justificativa = i.Justificativa,
        Motivo = i.Motivo,
        MotivoRotulo = i.Motivo == null ? null : PeDominios.MotivoInadimplencia.Rotulo(i.Motivo),
        NotaMotivacao = i.NotaMotivacao,
        ComunicadoControleEm = i.ComunicadoControleEm,
        RegistradoEm = i.RegistradoEm,
        RegistradoPor = i.RegistradoPor,
        SaneadoEm = i.SaneadoEm,
        Observacao = i.Observacao,
        CriadoEm = i.CriadoEm,
        CriadoPor = i.CriadoPor
    };

    /// <summary>As respostas de uma lista, com os órgãos lidos de uma vez.</summary>
    internal async Task<List<PeInadimplenciaResponse>> RespostasAsync(IReadOnlyList<PeInadimplencia> linhas)
    {
        var ids = linhas.Select(i => i.OrgaoId).Distinct().ToList();
        var orgaos = ids.Count == 0
            ? new Dictionary<long, PgiaOrgao>()
            : await _context.PgiaOrgaos.AsNoTracking().Where(o => ids.Contains(o.Id)).ToDictionaryAsync(o => o.Id);
        var hoje = PeCiclos.Hoje();
        return linhas.Select(i => Resposta(i, orgaos.GetValueOrDefault(i.OrgaoId), hoje)).ToList();
    }

    // ── Apoio ───────────────────────────────────────────────────────────────

    private void ConferirQuemGrava(PeUserContext ctx)
    {
        if (!_permissoes.PodeRegistrarInadimplencia(ctx))
            throw new ApiException(ErrorCode.PeSemPermissao, "Quem registra a notificação e a inadimplência é a SGDI ou o administrador do módulo.");
    }

    private async Task<PeInadimplencia> ParaGravarAsync(long id) =>
        await _context.PeInadimplencias.FirstOrDefaultAsync(i => i.Id == id)
        ?? throw new ApiException(ErrorCode.PeInadimplenciaNaoEncontrada, "Registro de inadimplência não encontrado. Atualize a tela.");

    private async Task<PeInadimplenciaResponse> GravarAsync(PeInadimplencia inadimplencia, PeUserContext ctx)
    {
        inadimplencia.AlteradoEm = DateTime.UtcNow;
        inadimplencia.AlteradoPor = ctx.Email;
        await _context.SaveChangesAsync();
        var orgao = await _context.PgiaOrgaos.AsNoTracking().FirstOrDefaultAsync(o => o.Id == inadimplencia.OrgaoId);
        return Resposta(inadimplencia, orgao, PeCiclos.Hoje());
    }

    private static string Rotulo(PeInadimplencia i) => PeDominios.SituacaoInadimplencia.Rotulo(i.Situacao).ToLowerInvariant();

    private static ApiException SemOrgao() => new(ErrorCode.PeOrgaoNaoEncontrado,
        "Seu usuário ainda não está ligado a um órgão. Fale com o administrador do módulo.");

    private static PeValidacaoException Invalida(IReadOnlyDictionary<string, string> erros) =>
        new(erros, erros.Count == 1 ? "Confira o campo destacado." : "Confira os campos destacados.", ErrorCode.PeInadimplenciaInvalida);

    /// <summary>O texto sem espaço nas pontas; vazio é nulo (obrigatório: o erro); longo demais: o erro.</summary>
    private static string? Texto(string? valor, int maximo, string campo, Dictionary<string, string> erros, string? obrigatorio, string oQue)
    {
        var texto = valor?.Trim();
        if (string.IsNullOrEmpty(texto))
        {
            if (obrigatorio != null) erros[campo] = obrigatorio;
            return null;
        }
        if (texto.Length > maximo)
        {
            erros[campo] = $"{oQue} tem no máximo {maximo.ToString(CultureInfo.InvariantCulture)} caracteres (o texto tem {texto.Length.ToString(CultureInfo.InvariantCulture)}).";
            return null;
        }
        return texto;
    }

    /// <summary>Uma data aaaa-mm-dd, a partir de 2000 e até hoje; vazia é nula (obrigatória: o erro).</summary>
    private static DateOnly? Data(string? valor, string campo, Dictionary<string, string> erros, string? obrigatoria, DateOnly hoje, string depoisDeHoje)
    {
        var texto = valor?.Trim();
        if (string.IsNullOrEmpty(texto))
        {
            if (obrigatoria != null) erros[campo] = obrigatoria;
            return null;
        }
        if (!DateOnly.TryParseExact(texto, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var data) || data.Year < 2000)
        {
            erros[campo] = "Use uma data válida, no formato aaaa-mm-dd.";
            return null;
        }
        if (data > hoje)
        {
            erros[campo] = depoisDeHoje;
            return null;
        }
        return data;
    }

    private static string? Sei(string? valor, string campo, Dictionary<string, string> erros)
    {
        var texto = valor?.Trim();
        if (string.IsNullOrEmpty(texto)) return null;
        if (FormatoSei().IsMatch(texto)) return texto;
        erros[campo] = "Informe o processo SEI no formato 00000-00000000/0000-00.";
        return null;
    }
}
