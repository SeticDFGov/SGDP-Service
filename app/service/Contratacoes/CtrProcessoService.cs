using System.Linq.Expressions;
using System.Text.RegularExpressions;
using api.Common;
using api.Contratacoes;
using demanda_service.Helpers;
using Microsoft.EntityFrameworkCore;
using Models.Contratacoes;
using Repositorio.Interface;
using service.Interface;

namespace service.Contratacoes;

/// <summary>
/// Regras dos processos de contratação em análise: situação derivada, validações
/// (formato SEI, unicidade entre ativos, cronologia dos checkpoints, restituição),
/// listagem filtrada, export CSV e painel de indicadores.
/// </summary>
public class CtrProcessoService : ICtrProcessoService
{
    /// <summary>Teto de linhas do export (a planilha da equipe é de dezenas de linhas).</summary>
    public const int LimiteExportacao = 5000;

    /// <summary>Máximo de processos parados listados no painel.</summary>
    public const int LimiteGargalos = 50;

    private const int PageSizeMaximo = 100;

    // Formato SEI: 00000-00000000/AAAA-DD
    private static readonly Regex FormatoSei = new(@"^\d{5}-\d{8}/\d{4}-\d{2}$", RegexOptions.Compiled);

    private static readonly string[] OrdenacoesValidas =
    {
        "NumeroProcesso", "OrgaoSigla", "ChegadaSgdi", "CategoriaObjeto", "CriadoEm"
    };

    private readonly ICtrProcessoRepositorio _repositorio;

    public CtrProcessoService(ICtrProcessoRepositorio repositorio)
    {
        _repositorio = repositorio;
    }

    // ── Situação derivada e tempos ────────────────────────────────────────────

    /// <summary>
    /// Situação do processo — FONTE ÚNICA. Nunca é gravada: é sempre função das
    /// datas e da restituição, na ordem do desenho.
    /// </summary>
    public static string CalcularSituacao(CtrProcesso p) => CalcularSituacao(
        p.Restituido, p.ChegadaSgdi, p.ChegadaSubgd, p.ChegadaUgtic, p.RetornoGabSgdi, p.RetornoOrgao);

    /// <inheritdoc cref="CalcularSituacao(CtrProcesso)"/>
    public static string CalcularSituacao(bool restituido, DateOnly? chegadaSgdi, DateOnly? chegadaSubgd,
        DateOnly? chegadaUgtic, DateOnly? retornoGabSgdi, DateOnly? retornoOrgao)
    {
        if (restituido) return CtrDominios.Situacao.Restituido;
        if (retornoOrgao != null) return CtrDominios.Situacao.Concluido;
        if (retornoGabSgdi != null) return CtrDominios.Situacao.RetornadoGabSgdi;
        if (chegadaUgtic != null) return CtrDominios.Situacao.EmAnaliseUgtic;
        // Vale também quando a UGTIC "não se aplica" (a etapa foi pulada de propósito)
        if (chegadaSubgd != null) return CtrDominios.Situacao.EmAnaliseSubgd;
        if (chegadaSgdi != null) return CtrDominios.Situacao.EmAnaliseSgdi;
        return CtrDominios.Situacao.SemMovimentacao;
    }

    /// <summary>Maior data entre os cinco checkpoints e a restituição; null sem nenhuma.</summary>
    public static DateOnly? CalcularUltimaMovimentacao(CtrProcesso p) => CalcularUltimaMovimentacao(
        p.ChegadaSgdi, p.ChegadaSubgd, p.ChegadaUgtic, p.RetornoGabSgdi, p.RetornoOrgao, p.RestituidoEm);

    /// <inheritdoc cref="CalcularUltimaMovimentacao(CtrProcesso)"/>
    public static DateOnly? CalcularUltimaMovimentacao(params DateOnly?[] datas)
    {
        DateOnly? maior = null;
        foreach (var data in datas)
        {
            if (data == null) continue;
            if (maior == null || data.Value > maior.Value) maior = data;
        }
        return maior;
    }

    /// <summary>
    /// Dias entre a última movimentação (ou a criação, no dia civil de Brasília,
    /// quando não há nenhuma) e hoje. Nunca negativo.
    /// </summary>
    public static int CalcularDiasSemMovimento(DateOnly? ultimaMovimentacao, DateTime criadoEm, DateOnly hoje)
    {
        var referencia = ultimaMovimentacao
            ?? DateOnly.FromDateTime(DateTimeHelper.ToBrasilia(criadoEm));
        var dias = hoje.DayNumber - referencia.DayNumber;
        return dias < 0 ? 0 : dias;
    }

    public static DateOnly HojeBrasilia() => DateOnly.FromDateTime(DateTimeHelper.TodayBrasilia());

    /// <summary>
    /// Traduz a situação derivada em predicado sobre as colunas, para o filtro rodar
    /// no banco (nunca materializando a tabela). Null quando o valor não é do domínio.
    /// </summary>
    public static Expression<Func<CtrProcesso, bool>>? PredicadoSituacao(string? situacao) => situacao switch
    {
        CtrDominios.Situacao.Restituido => p => p.Restituido,
        CtrDominios.Situacao.Concluido => p => !p.Restituido && p.RetornoOrgao != null,
        CtrDominios.Situacao.RetornadoGabSgdi => p =>
            !p.Restituido && p.RetornoOrgao == null && p.RetornoGabSgdi != null,
        CtrDominios.Situacao.EmAnaliseUgtic => p =>
            !p.Restituido && p.RetornoOrgao == null && p.RetornoGabSgdi == null && p.ChegadaUgtic != null,
        CtrDominios.Situacao.EmAnaliseSubgd => p =>
            !p.Restituido && p.RetornoOrgao == null && p.RetornoGabSgdi == null && p.ChegadaUgtic == null
            && p.ChegadaSubgd != null,
        CtrDominios.Situacao.EmAnaliseSgdi => p =>
            !p.Restituido && p.RetornoOrgao == null && p.RetornoGabSgdi == null && p.ChegadaUgtic == null
            && p.ChegadaSubgd == null && p.ChegadaSgdi != null,
        CtrDominios.Situacao.SemMovimentacao => p =>
            !p.Restituido && p.RetornoOrgao == null && p.RetornoGabSgdi == null && p.ChegadaUgtic == null
            && p.ChegadaSubgd == null && p.ChegadaSgdi == null,
        _ => null
    };

    // ── Validação (criar, editar, checkpoint e importação usam esta função) ────

    /// <summary>
    /// Normaliza (trim, sigla em caixa alta, restituição limpa quando desmarcada) e
    /// valida o processo. <paramref name="numeroDuplicado"/> vem de quem chama
    /// (consulta ao banco no CRUD, dicionário em memória na importação).
    /// </summary>
    public static void ValidarProcesso(CtrProcesso p, bool numeroDuplicado)
    {
        p.NumeroProcesso = (p.NumeroProcesso ?? string.Empty).Trim();
        p.OrgaoNome = (p.OrgaoNome ?? string.Empty).Trim();
        p.OrgaoSigla = (p.OrgaoSigla ?? string.Empty).Trim().ToUpperInvariant();
        p.ComplementoArea = Limpar(p.ComplementoArea);
        p.Objeto = (p.Objeto ?? string.Empty).Trim();
        p.CategoriaObjeto = (p.CategoriaObjeto ?? string.Empty).Trim();
        p.Observacao = Limpar(p.Observacao);
        p.RestituidoMotivo = Limpar(p.RestituidoMotivo);

        // Desmarcar a restituição ANULA data e motivo (normalização, não erro)
        if (!p.Restituido)
        {
            p.RestituidoEm = null;
            p.RestituidoMotivo = null;
        }

        if (string.IsNullOrWhiteSpace(p.NumeroProcesso))
            throw new ApiException(ErrorCode.CtrProcessoInvalido, "Informe o número do processo SEI.");

        if (!FormatoSei.IsMatch(p.NumeroProcesso))
            throw new ApiException(ErrorCode.CtrProcessoInvalido,
                $"Número de processo fora do formato SEI (00000-00000000/AAAA-DD): {p.NumeroProcesso}");

        if (numeroDuplicado)
            throw new ApiException(ErrorCode.CtrProcessoDuplicado,
                $"Já existe processo ativo com o número {p.NumeroProcesso}.");

        if (string.IsNullOrWhiteSpace(p.OrgaoNome))
            throw new ApiException(ErrorCode.CtrProcessoInvalido, "Informe o órgão comunicante.");

        if (string.IsNullOrWhiteSpace(p.OrgaoSigla))
            throw new ApiException(ErrorCode.CtrProcessoInvalido, "Informe a sigla do órgão.");

        if (string.IsNullOrWhiteSpace(p.Objeto))
            throw new ApiException(ErrorCode.CtrProcessoInvalido, "Informe o objeto da contratação.");

        if (!CtrDominios.CategoriaObjeto.Todos.Contains(p.CategoriaObjeto))
            throw new ApiException(ErrorCode.CtrDominioInvalido,
                $"Categoria do objeto inválida: {p.CategoriaObjeto}");

        if (p.UgticNaoSeAplica && p.ChegadaUgtic != null)
            throw new ApiException(ErrorCode.CtrProcessoInvalido,
                "Chegada à UGTIC: marque \"não se aplica\" ou informe a data, não os dois.");

        if (p.Restituido && p.RestituidoEm == null)
            throw new ApiException(ErrorCode.CtrProcessoInvalido,
                "Processo restituído exige a data da restituição.");

        if (p.Restituido && string.IsNullOrWhiteSpace(p.RestituidoMotivo))
            throw new ApiException(ErrorCode.CtrProcessoInvalido,
                "Processo restituído exige o motivo da restituição.");

        ValidarCronologia(p);
    }

    private static void ValidarCronologia(CtrProcesso p)
    {
        var hoje = HojeBrasilia();

        var etapas = new (string Nome, DateOnly? Data)[]
        {
            ("chegada à SGDI", p.ChegadaSgdi),
            ("chegada à SUBGD", p.ChegadaSubgd),
            ("chegada à UGTIC", p.ChegadaUgtic),
            ("retorno ao Gab SGDI", p.RetornoGabSgdi),
            ("retorno ao órgão comunicante", p.RetornoOrgao)
        };

        foreach (var (nome, data) in etapas)
        {
            if (data != null && data.Value > hoje)
                throw new ApiException(ErrorCode.CtrDatasIncoerentes,
                    $"A data de {nome} ({data.Value:dd/MM/yyyy}) não pode ser futura.");
        }

        if (p.RestituidoEm != null && p.RestituidoEm.Value > hoje)
            throw new ApiException(ErrorCode.CtrDatasIncoerentes,
                $"A data da restituição ({p.RestituidoEm.Value:dd/MM/yyyy}) não pode ser futura.");

        // Cada etapa preenchida deve ser >= todas as anteriores preenchidas (lacunas são permitidas)
        for (var atual = 1; atual < etapas.Length; atual++)
        {
            if (etapas[atual].Data == null) continue;

            for (var anterior = 0; anterior < atual; anterior++)
            {
                if (etapas[anterior].Data == null) continue;

                if (etapas[atual].Data!.Value < etapas[anterior].Data!.Value)
                    throw new ApiException(ErrorCode.CtrDatasIncoerentes,
                        $"A data de {etapas[atual].Nome} ({etapas[atual].Data!.Value:dd/MM/yyyy}) não pode ser "
                        + $"anterior à data de {etapas[anterior].Nome} ({etapas[anterior].Data!.Value:dd/MM/yyyy}).");
            }
        }

        if (p.RestituidoEm != null && p.ChegadaSgdi != null && p.RestituidoEm.Value < p.ChegadaSgdi.Value)
            throw new ApiException(ErrorCode.CtrDatasIncoerentes,
                $"A data da restituição ({p.RestituidoEm.Value:dd/MM/yyyy}) não pode ser anterior à data de "
                + $"chegada à SGDI ({p.ChegadaSgdi.Value:dd/MM/yyyy}).");
    }

    private static string? Limpar(string? valor) =>
        string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();

    // ── Listagem, siglas e export ─────────────────────────────────────────────

    public async Task<PagedResponse<CtrProcessoResponse>> ListarAsync(CtrProcessoFiltro filtro)
    {
        var query = AplicarFiltros(_repositorio.QueryAtivos(), filtro);

        // PagedRequest é compartilhado com o resto do SGDP e não valida os limites:
        // saneia aqui (piso e teto), sem tocar na classe comum.
        var pageSize = Math.Clamp(filtro.PageSize, 1, PageSizeMaximo);
        var page = Math.Clamp(filtro.Page, 1, int.MaxValue / pageSize);
        var skip = (page - 1) * pageSize;

        var totalItems = await query.CountAsync();
        var processos = await Ordenar(query, filtro).Skip(skip).Take(pageSize).ToListAsync();

        var itens = await MapearComManifestacoesAsync(processos);
        return new PagedResponse<CtrProcessoResponse>(itens, totalItems, page, pageSize);
    }

    public async Task<List<string>> ListarSiglasAsync() => await _repositorio.ListarSiglasAsync();

    public async Task<byte[]> ExportarCsvAsync(CtrProcessoFiltro filtro)
    {
        var query = AplicarFiltros(_repositorio.QueryAtivos(), filtro);
        var processos = await Ordenar(query, filtro).Take(LimiteExportacao).ToListAsync();

        var hoje = HojeBrasilia();
        var linhas = processos.Select(p => MapProcesso(p, hoje)).ToList();
        return CtrCsv.Escrever(linhas);
    }

    private static IQueryable<CtrProcesso> AplicarFiltros(IQueryable<CtrProcesso> query, CtrProcessoFiltro filtro)
    {
        if (!string.IsNullOrWhiteSpace(filtro.Filtro))
        {
            var termo = filtro.Filtro.Trim().ToLower();
            query = query.Where(p =>
                p.NumeroProcesso.ToLower().Contains(termo) ||
                p.OrgaoNome.ToLower().Contains(termo) ||
                p.OrgaoSigla.ToLower().Contains(termo) ||
                p.Objeto.ToLower().Contains(termo));
        }

        if (!string.IsNullOrWhiteSpace(filtro.Categoria))
            query = query.Where(p => p.CategoriaObjeto == filtro.Categoria);

        if (!string.IsNullOrWhiteSpace(filtro.Sigla))
        {
            var sigla = filtro.Sigla.Trim().ToUpperInvariant();
            query = query.Where(p => p.OrgaoSigla == sigla);
        }

        if (filtro.Restituidos != null)
        {
            // Compara bool com bool (e não com bool?): não depende da semântica de
            // nulo que cada provider traduz à sua maneira
            var restituidos = filtro.Restituidos.Value;
            query = query.Where(p => p.Restituido == restituidos);
        }

        if (filtro.De != null)
            query = query.Where(p => p.ChegadaSgdi != null && p.ChegadaSgdi >= filtro.De);

        if (filtro.Ate != null)
            query = query.Where(p => p.ChegadaSgdi != null && p.ChegadaSgdi <= filtro.Ate);

        if (!string.IsNullOrWhiteSpace(filtro.Situacao))
        {
            // Situação fora do domínio devolve VAZIO: filtro que o servidor não
            // entende não pode virar "todos" em silêncio
            var predicado = PredicadoSituacao(filtro.Situacao);
            query = predicado != null ? query.Where(predicado) : query.Where(p => false);
        }

        return query;
    }

    private static IQueryable<CtrProcesso> Ordenar(IQueryable<CtrProcesso> query, CtrProcessoFiltro filtro)
    {
        var campo = filtro.OrderBy != null && OrdenacoesValidas.Contains(filtro.OrderBy) ? filtro.OrderBy : null;
        var asc = filtro.IsAscending;

        return campo switch
        {
            "NumeroProcesso" => asc
                ? query.OrderBy(p => p.NumeroProcesso).ThenByDescending(p => p.Id)
                : query.OrderByDescending(p => p.NumeroProcesso).ThenByDescending(p => p.Id),
            "OrgaoSigla" => asc
                ? query.OrderBy(p => p.OrgaoSigla).ThenByDescending(p => p.Id)
                : query.OrderByDescending(p => p.OrgaoSigla).ThenByDescending(p => p.Id),
            "CategoriaObjeto" => asc
                ? query.OrderBy(p => p.CategoriaObjeto).ThenByDescending(p => p.Id)
                : query.OrderByDescending(p => p.CategoriaObjeto).ThenByDescending(p => p.Id),
            "CriadoEm" => asc
                ? query.OrderBy(p => p.CriadoEm).ThenByDescending(p => p.Id)
                : query.OrderByDescending(p => p.CriadoEm).ThenByDescending(p => p.Id),
            // ChegadaSgdi (e o padrão): nulos SEMPRE por último
            "ChegadaSgdi" when asc => query
                .OrderBy(p => p.ChegadaSgdi == null).ThenBy(p => p.ChegadaSgdi).ThenByDescending(p => p.Id),
            _ => query
                .OrderBy(p => p.ChegadaSgdi == null).ThenByDescending(p => p.ChegadaSgdi).ThenByDescending(p => p.Id)
        };
    }

    // ── CRUD ──────────────────────────────────────────────────────────────────

    public async Task<CtrProcesso?> GetEntidadeAsync(long id)
    {
        var processo = await _repositorio.GetByIdAsync(id);
        return processo is { Ativo: true } ? processo : null;
    }

    public async Task<CtrProcessoResponse> GetAsync(long id)
    {
        var processo = await GetEntidadeAsync(id)
            ?? throw new ApiException(ErrorCode.CtrProcessoNaoEncontrado);

        return (await MapearComManifestacoesAsync(new List<CtrProcesso> { processo })).Single();
    }

    public async Task<CtrProcessoResponse> CriarAsync(CtrProcessoCreateDTO dto, CtrUserContext ctx)
    {
        var processo = new CtrProcesso
        {
            CriadoEm = DateTime.UtcNow,
            CriadoPor = ctx.Email
        };
        AplicarDto(processo, dto);

        ValidarProcesso(processo, await _repositorio.NumeroDuplicadoAsync(processo.NumeroProcesso, null));

        _repositorio.Add(processo);
        await _repositorio.SaveChangesAsync();

        return MapProcesso(processo, HojeBrasilia());
    }

    public async Task<CtrProcessoResponse> AtualizarAsync(long id, CtrProcessoUpdateDTO dto, CtrUserContext ctx)
    {
        var processo = await GetEntidadeAsync(id)
            ?? throw new ApiException(ErrorCode.CtrProcessoNaoEncontrado);

        // Valida num candidato solto: edição recusada não pode deixar rastro na
        // entidade rastreada pelo contexto
        var candidato = Clonar(processo);
        AplicarDto(candidato, dto);
        ValidarProcesso(candidato, await _repositorio.NumeroDuplicadoAsync(candidato.NumeroProcesso, processo.Id));

        AplicarDto(processo, DtoDe(candidato));
        processo.AlteradoEm = DateTime.UtcNow;
        processo.AlteradoPor = ctx.Email;

        await _repositorio.SaveChangesAsync();
        return (await MapearComManifestacoesAsync(new List<CtrProcesso> { processo })).Single();
    }

    public async Task ExcluirAsync(long id, CtrUserContext ctx)
    {
        var processo = await GetEntidadeAsync(id)
            ?? throw new ApiException(ErrorCode.CtrProcessoNaoEncontrado);

        // Soft delete: as manifestações somem junto das listas (todas filtram por processo ativo)
        processo.Ativo = false;
        processo.AlteradoEm = DateTime.UtcNow;
        processo.AlteradoPor = ctx.Email;

        await _repositorio.SaveChangesAsync();
    }

    public async Task<CtrProcessoResponse> RegistrarCheckpointAsync(long id, CtrCheckpointDTO dto, CtrUserContext ctx)
    {
        var processo = await GetEntidadeAsync(id)
            ?? throw new ApiException(ErrorCode.CtrProcessoNaoEncontrado);

        var etapa = (dto.Etapa ?? string.Empty).Trim();
        if (!CtrDominios.Etapa.Todos.Contains(etapa))
            throw new ApiException(ErrorCode.CtrDominioInvalido, $"Etapa inválida: {dto.Etapa}");

        if (dto.NaoSeAplica != null && etapa != CtrDominios.Etapa.ChegadaUgtic)
            throw new ApiException(ErrorCode.CtrProcessoInvalido,
                "\"Não se aplica\" só vale para a chegada à UGTIC.");

        // Aplica no candidato e só grava depois de validar (checkpoint incoerente
        // não pode deixar rastro na entidade rastreada)
        var candidato = Clonar(processo);
        switch (etapa)
        {
            case CtrDominios.Etapa.ChegadaSgdi:
                candidato.ChegadaSgdi = dto.Data;
                break;
            case CtrDominios.Etapa.ChegadaSubgd:
                candidato.ChegadaSubgd = dto.Data;
                break;
            case CtrDominios.Etapa.ChegadaUgtic:
                if (dto.NaoSeAplica == true)
                {
                    // Marcar "não se aplica" limpa a data da etapa pulada
                    candidato.UgticNaoSeAplica = true;
                    candidato.ChegadaUgtic = null;
                }
                else
                {
                    // Informar a data É o gesto de reativar a etapa: desmarca o
                    // "não se aplica" em vez de cair na validação de incoerência
                    if (dto.NaoSeAplica == false || dto.Data != null) candidato.UgticNaoSeAplica = false;
                    candidato.ChegadaUgtic = dto.Data;
                }
                break;
            case CtrDominios.Etapa.RetornoGabSgdi:
                candidato.RetornoGabSgdi = dto.Data;
                break;
            case CtrDominios.Etapa.RetornoOrgao:
                candidato.RetornoOrgao = dto.Data;
                break;
        }

        ValidarProcesso(candidato, await _repositorio.NumeroDuplicadoAsync(candidato.NumeroProcesso, processo.Id));

        AplicarDto(processo, DtoDe(candidato));
        processo.AlteradoEm = DateTime.UtcNow;
        processo.AlteradoPor = ctx.Email;

        await _repositorio.SaveChangesAsync();
        return (await MapearComManifestacoesAsync(new List<CtrProcesso> { processo })).Single();
    }

    /// <summary>Copia o DTO para a entidade (create e update têm a mesma forma).</summary>
    public static void AplicarDto(CtrProcesso processo, CtrProcessoCreateDTO dto)
    {
        processo.NumeroProcesso = dto.NumeroProcesso;
        processo.OrgaoNome = dto.OrgaoNome;
        processo.OrgaoSigla = dto.OrgaoSigla;
        processo.ComplementoArea = dto.ComplementoArea;
        processo.Objeto = dto.Objeto;
        processo.CategoriaObjeto = dto.CategoriaObjeto;
        processo.ChegadaSgdi = dto.ChegadaSgdi;
        processo.ChegadaSubgd = dto.ChegadaSubgd;
        processo.ChegadaUgtic = dto.ChegadaUgtic;
        processo.UgticNaoSeAplica = dto.UgticNaoSeAplica;
        processo.RetornoGabSgdi = dto.RetornoGabSgdi;
        processo.RetornoOrgao = dto.RetornoOrgao;
        processo.Restituido = dto.Restituido;
        processo.RestituidoEm = dto.RestituidoEm;
        processo.RestituidoMotivo = dto.RestituidoMotivo;
        processo.Observacao = dto.Observacao;
    }

    /// <summary>DTO a partir da entidade (valores já normalizados pela validação).</summary>
    public static CtrProcessoCreateDTO DtoDe(CtrProcesso p) => new()
    {
        NumeroProcesso = p.NumeroProcesso,
        OrgaoNome = p.OrgaoNome,
        OrgaoSigla = p.OrgaoSigla,
        ComplementoArea = p.ComplementoArea,
        Objeto = p.Objeto,
        CategoriaObjeto = p.CategoriaObjeto,
        ChegadaSgdi = p.ChegadaSgdi,
        ChegadaSubgd = p.ChegadaSubgd,
        ChegadaUgtic = p.ChegadaUgtic,
        UgticNaoSeAplica = p.UgticNaoSeAplica,
        RetornoGabSgdi = p.RetornoGabSgdi,
        RetornoOrgao = p.RetornoOrgao,
        Restituido = p.Restituido,
        RestituidoEm = p.RestituidoEm,
        RestituidoMotivo = p.RestituidoMotivo,
        Observacao = p.Observacao
    };

    /// <summary>Cópia solta da entidade, para validar sem sujar o que o contexto rastreia.</summary>
    private static CtrProcesso Clonar(CtrProcesso p)
    {
        var copia = new CtrProcesso
        {
            Id = p.Id,
            Ativo = p.Ativo,
            CriadoEm = p.CriadoEm,
            CriadoPor = p.CriadoPor,
            AlteradoEm = p.AlteradoEm,
            AlteradoPor = p.AlteradoPor
        };
        AplicarDto(copia, DtoDe(p));
        return copia;
    }

    // ── Painel ────────────────────────────────────────────────────────────────

    public async Task<CtrPainelResponse> MontarPainelAsync(int diasSemMovimento)
    {
        var limiteDias = Math.Max(0, diasSemMovimento);
        var hoje = HojeBrasilia();

        // Uma consulta com projeção enxuta (sem os campos de texto longo) resolve
        // contagens, médias e a seleção dos gargalos.
        var resumos = await _repositorio.QueryAtivos()
            .Select(p => new ResumoProcesso
            {
                Id = p.Id,
                CategoriaObjeto = p.CategoriaObjeto,
                OrgaoSigla = p.OrgaoSigla,
                Restituido = p.Restituido,
                ChegadaSgdi = p.ChegadaSgdi,
                ChegadaSubgd = p.ChegadaSubgd,
                ChegadaUgtic = p.ChegadaUgtic,
                RetornoGabSgdi = p.RetornoGabSgdi,
                RetornoOrgao = p.RetornoOrgao,
                RestituidoEm = p.RestituidoEm,
                CriadoEm = p.CriadoEm
            })
            .ToListAsync();

        var comSituacao = resumos.Select(r => new
        {
            Resumo = r,
            Situacao = CalcularSituacao(r.Restituido, r.ChegadaSgdi, r.ChegadaSubgd,
                r.ChegadaUgtic, r.RetornoGabSgdi, r.RetornoOrgao),
            Dias = CalcularDiasSemMovimento(
                CalcularUltimaMovimentacao(r.ChegadaSgdi, r.ChegadaSubgd, r.ChegadaUgtic,
                    r.RetornoGabSgdi, r.RetornoOrgao, r.RestituidoEm),
                r.CriadoEm, hoje)
        }).ToList();

        var painel = new CtrPainelResponse
        {
            TotalAtivos = resumos.Count,
            LimiteDias = limiteDias,
            // As 7 situações aparecem sempre, mesmo com zero. Ordem determinística:
            // quantidade desc e, no empate, a ordem do domínio (do trâmite)
            PorSituacao = CtrDominios.Situacao.Todos
                .Select((s, ordem) => new
                {
                    Contagem = new CtrContagem { Chave = s, Quantidade = comSituacao.Count(x => x.Situacao == s) },
                    Ordem = ordem
                })
                .OrderByDescending(c => c.Contagem.Quantidade).ThenBy(c => c.Ordem)
                .Select(c => c.Contagem)
                .ToList(),
            PorCategoria = resumos
                .GroupBy(r => r.CategoriaObjeto)
                .Select(g => new CtrContagem { Chave = g.Key, Quantidade = g.Count() })
                .OrderByDescending(c => c.Quantidade).ThenBy(c => c.Chave)
                .ToList(),
            PorOrgao = resumos
                .GroupBy(r => r.OrgaoSigla)
                .Select(g => new CtrContagem { Chave = g.Key, Quantidade = g.Count() })
                .OrderByDescending(c => c.Quantidade).ThenBy(c => c.Chave)
                .ToList(),
            TemposMedios = new CtrTemposMedios
            {
                SgdiParaSubgd = Media(resumos, r => r.ChegadaSgdi, r => r.ChegadaSubgd),
                SubgdParaUgtic = Media(resumos, r => r.ChegadaSubgd, r => r.ChegadaUgtic),
                UgticParaRetornoGab = Media(resumos, r => r.ChegadaUgtic, r => r.RetornoGabSgdi),
                RetornoGabParaOrgao = Media(resumos, r => r.RetornoGabSgdi, r => r.RetornoOrgao),
                ChegadaParaConclusao = Media(resumos, r => r.ChegadaSgdi, r => r.RetornoOrgao)
            }
        };

        var gargalos = comSituacao
            .Where(x => x.Situacao != CtrDominios.Situacao.Concluido
                        && x.Situacao != CtrDominios.Situacao.Restituido
                        && x.Dias >= limiteDias)
            .OrderByDescending(x => x.Dias)
            .ThenBy(x => x.Resumo.Id)
            .Take(LimiteGargalos)
            .ToList();

        if (gargalos.Count > 0)
        {
            var ids = gargalos.Select(x => x.Resumo.Id).ToList();
            var processos = await _repositorio.QueryAtivos().Where(p => ids.Contains(p.Id)).ToListAsync();
            var mapeados = (await MapearComManifestacoesAsync(processos))
                .ToDictionary(p => p.Id);

            painel.Gargalos = gargalos
                .Where(x => mapeados.ContainsKey(x.Resumo.Id))
                .Select(x => mapeados[x.Resumo.Id])
                .ToList();
        }

        return painel;
    }

    private static double? Media(List<ResumoProcesso> resumos,
        Func<ResumoProcesso, DateOnly?> de, Func<ResumoProcesso, DateOnly?> ate)
    {
        var amostra = resumos
            .Where(r => de(r) != null && ate(r) != null)
            .Select(r => ate(r)!.Value.DayNumber - de(r)!.Value.DayNumber)
            .ToList();

        return amostra.Count == 0 ? null : Math.Round(amostra.Average(), 1);
    }

    // ── Mapeamento ────────────────────────────────────────────────────────────

    /// <summary>
    /// Mapeia os processos resolvendo TotalManifestacoes/UltimoEstagioTcdf em LOTE
    /// (uma consulta agrupada por processo_id, sem N+1).
    /// </summary>
    private async Task<List<CtrProcessoResponse>> MapearComManifestacoesAsync(List<CtrProcesso> processos)
    {
        var hoje = HojeBrasilia();
        if (processos.Count == 0) return new List<CtrProcessoResponse>();

        var ids = processos.Select(p => p.Id).ToList();
        var manifestacoes = await _repositorio.ListarManifestacoesPorProcessosAsync(ids);

        var porProcesso = manifestacoes
            .GroupBy(m => m.ProcessoId)
            .ToDictionary(g => g.Key, g => new
            {
                Total = g.Count(),
                Ultima = g.OrderByDescending(m => m.DataOficio).ThenByDescending(m => m.Id).First()
            });

        return processos.Select(p =>
        {
            var resposta = MapProcesso(p, hoje);
            if (porProcesso.TryGetValue(p.Id, out var agregado))
            {
                resposta.TotalManifestacoes = agregado.Total;
                resposta.UltimoEstagioTcdf = CtrManifestacaoService.CalcularEstagio(agregado.Ultima);
            }
            return resposta;
        }).ToList();
    }

    public static CtrProcessoResponse MapProcesso(CtrProcesso p, DateOnly hoje)
    {
        var ultimaMovimentacao = CalcularUltimaMovimentacao(p);

        return new CtrProcessoResponse
        {
            Id = p.Id,
            NumeroProcesso = p.NumeroProcesso,
            OrgaoNome = p.OrgaoNome,
            OrgaoSigla = p.OrgaoSigla,
            ComplementoArea = p.ComplementoArea,
            Objeto = p.Objeto,
            CategoriaObjeto = p.CategoriaObjeto,
            ChegadaSgdi = p.ChegadaSgdi,
            ChegadaSubgd = p.ChegadaSubgd,
            ChegadaUgtic = p.ChegadaUgtic,
            UgticNaoSeAplica = p.UgticNaoSeAplica,
            RetornoGabSgdi = p.RetornoGabSgdi,
            RetornoOrgao = p.RetornoOrgao,
            Restituido = p.Restituido,
            RestituidoEm = p.RestituidoEm,
            RestituidoMotivo = p.RestituidoMotivo,
            Observacao = p.Observacao,
            Situacao = CalcularSituacao(p),
            UltimaMovimentacao = ultimaMovimentacao,
            DiasSemMovimento = CalcularDiasSemMovimento(ultimaMovimentacao, p.CriadoEm, hoje),
            CriadoEm = p.CriadoEm,
            CriadoPor = p.CriadoPor,
            AlteradoEm = p.AlteradoEm,
            AlteradoPor = p.AlteradoPor
        };
    }

    /// <summary>Projeção enxuta usada pelo painel (sem os campos de texto longo).</summary>
    private sealed class ResumoProcesso
    {
        public long Id { get; set; }
        public string CategoriaObjeto { get; set; } = string.Empty;
        public string OrgaoSigla { get; set; } = string.Empty;
        public bool Restituido { get; set; }
        public DateOnly? ChegadaSgdi { get; set; }
        public DateOnly? ChegadaSubgd { get; set; }
        public DateOnly? ChegadaUgtic { get; set; }
        public DateOnly? RetornoGabSgdi { get; set; }
        public DateOnly? RetornoOrgao { get; set; }
        public DateOnly? RestituidoEm { get; set; }
        public DateTime CriadoEm { get; set; }
    }
}
