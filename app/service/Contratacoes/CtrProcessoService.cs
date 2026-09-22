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

    /// <summary>Máximo de concluídos na relação do painel (os de assinatura mais recente).</summary>
    public const int LimiteConcluidos = 50;

    private const int PageSizeMaximo = 100;

    // Formato SEI: 00000-00000000/AAAA-DD
    private static readonly Regex FormatoSei = new(@"^\d{5}-\d{8}/\d{4}-\d{2}$", RegexOptions.Compiled);

    private static readonly string[] OrdenacoesValidas =
    {
        "NumeroProcesso", "OrgaoSigla", "ChegadaSgdi", "CategoriaObjeto", "CriadoEm", "Criticidade"
    };

    /// <summary>
    /// Ordem da criticidade na lista: Alta, Média, Baixa e por fim os sem criticidade.
    /// Expressão (não método) para o EF traduzir em CASE no banco.
    /// </summary>
    private static readonly Expression<Func<CtrProcesso, int>> OrdemCriticidade = p =>
        p.Criticidade == CtrDominios.Criticidade.Alta ? 0
        : p.Criticidade == CtrDominios.Criticidade.Media ? 1
        : p.Criticidade == CtrDominios.Criticidade.Baixa ? 2
        : 3;

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
        p.DataAssinaturaContrato, p.Restituido, p.ChegadaSgdi, p.ChegadaSubgd, p.ChegadaUgtic, p.RetornoGabSgdi,
        p.RetornoOrgao, p.RetornoOrgaoNaoSeAplica);

    /// <inheritdoc cref="CalcularSituacao(CtrProcesso)"/>
    // Sem valor default de propósito: um chamador que esquecesse um dos flags receberia a
    // situação ANTIGA em silêncio (o processo com devolução dispensada não concluiria)
    public static string CalcularSituacao(DateOnly? dataAssinaturaContrato, bool restituido, DateOnly? chegadaSgdi,
        DateOnly? chegadaSubgd, DateOnly? chegadaUgtic, DateOnly? retornoGabSgdi, DateOnly? retornoOrgao,
        bool retornoOrgaoNaoSeAplica)
    {
        // Contrato assinado = processo Concluído, seja qual for o resto do trâmite: é a
        // data FINAL (o processo sai da lista e vai para a relação de concluídos do painel)
        if (dataAssinaturaContrato != null) return CtrDominios.Situacao.Concluido;
        if (restituido) return CtrDominios.Situacao.Restituido;
        // Análise concluída pela devolução ao órgão OU pela devolução dispensada — mas só
        // com o retorno ao Gab SGDI: marcar "não se aplica" sozinho não conclui nada,
        // o processo segue na etapa em que está.
        if (retornoOrgao != null || (retornoOrgaoNaoSeAplica && retornoGabSgdi != null))
            return CtrDominios.Situacao.AnaliseConcluida;
        if (retornoGabSgdi != null) return CtrDominios.Situacao.RetornadoGabSgdi;
        if (chegadaUgtic != null) return CtrDominios.Situacao.EmAnaliseUgtic;
        // Vale também quando a UGTIC "não se aplica" (a etapa foi pulada de propósito)
        if (chegadaSubgd != null) return CtrDominios.Situacao.EmAnaliseSubgd;
        if (chegadaSgdi != null) return CtrDominios.Situacao.EmAnaliseSgdi;
        return CtrDominios.Situacao.SemMovimentacao;
    }

    /// <summary>
    /// Pedido de esclarecimento feito e ainda sem resposta — DERIVADO, nunca gravado.
    /// Sinal paralelo ao trâmite: não entra na situação.
    /// </summary>
    public static bool CalcularEsclarecimentoPendente(CtrProcesso p) =>
        CalcularEsclarecimentoPendente(p.EsclarecimentoSolicitadoEm, p.EsclarecimentoRespondidoEm);

    /// <inheritdoc cref="CalcularEsclarecimentoPendente(CtrProcesso)"/>
    public static bool CalcularEsclarecimentoPendente(DateOnly? solicitadoEm, DateOnly? respondidoEm) =>
        solicitadoEm != null && respondidoEm == null;

    /// <summary>
    /// Dias entre o pedido de esclarecimento e hoje; null quando não há pendência.
    /// Nunca negativo (o pedido não pode ser futuro, mas o piso fica explícito).
    /// </summary>
    public static int? CalcularDiasEsclarecimentoPendente(DateOnly? solicitadoEm, DateOnly? respondidoEm,
        DateOnly hoje)
    {
        if (!CalcularEsclarecimentoPendente(solicitadoEm, respondidoEm)) return null;

        var dias = hoje.DayNumber - solicitadoEm!.Value.DayNumber;
        return dias < 0 ? 0 : dias;
    }

    /// <summary>
    /// Maior data entre os cinco checkpoints, a restituição e o esclarecimento;
    /// null sem nenhuma. Pedir e responder esclarecimento É movimentação do processo.
    /// </summary>
    public static DateOnly? CalcularUltimaMovimentacao(CtrProcesso p) => CalcularUltimaMovimentacao(
        p.ChegadaSgdi, p.ChegadaSubgd, p.ChegadaUgtic, p.RetornoGabSgdi, p.RetornoOrgao, p.RestituidoEm,
        p.EsclarecimentoSolicitadoEm, p.EsclarecimentoRespondidoEm);

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
        // A assinatura tem precedência sobre tudo: as demais situações a exigem nula
        CtrDominios.Situacao.Concluido => p => p.DataAssinaturaContrato != null,
        CtrDominios.Situacao.Restituido => p => p.DataAssinaturaContrato == null && p.Restituido,
        // Análise concluída inclui a devolução dispensada com o retorno ao Gab SGDI feito
        CtrDominios.Situacao.AnaliseConcluida => p =>
            p.DataAssinaturaContrato == null && !p.Restituido
            && (p.RetornoOrgao != null || (p.RetornoOrgaoNaoSeAplica && p.RetornoGabSgdi != null)),
        CtrDominios.Situacao.RetornadoGabSgdi => p =>
            p.DataAssinaturaContrato == null && !p.Restituido && p.RetornoOrgao == null
            && !p.RetornoOrgaoNaoSeAplica && p.RetornoGabSgdi != null,
        // Sem retorno ao Gab SGDI o "não se aplica" não muda nada: as demais
        // situações já exigem RetornoGabSgdi == null
        CtrDominios.Situacao.EmAnaliseUgtic => p =>
            p.DataAssinaturaContrato == null && !p.Restituido && p.RetornoOrgao == null
            && p.RetornoGabSgdi == null && p.ChegadaUgtic != null,
        CtrDominios.Situacao.EmAnaliseSubgd => p =>
            p.DataAssinaturaContrato == null && !p.Restituido && p.RetornoOrgao == null
            && p.RetornoGabSgdi == null && p.ChegadaUgtic == null && p.ChegadaSubgd != null,
        CtrDominios.Situacao.EmAnaliseSgdi => p =>
            p.DataAssinaturaContrato == null && !p.Restituido && p.RetornoOrgao == null
            && p.RetornoGabSgdi == null && p.ChegadaUgtic == null && p.ChegadaSubgd == null
            && p.ChegadaSgdi != null,
        CtrDominios.Situacao.SemMovimentacao => p =>
            p.DataAssinaturaContrato == null && !p.Restituido && p.RetornoOrgao == null
            && p.RetornoGabSgdi == null && p.ChegadaUgtic == null && p.ChegadaSubgd == null
            && p.ChegadaSgdi == null,
        _ => null
    };

    /// <summary>
    /// Pendência de esclarecimento como predicado sobre as duas colunas (é função
    /// delas), para o filtro rodar no banco.
    /// </summary>
    public static Expression<Func<CtrProcesso, bool>> PredicadoEsclarecimentoPendente(bool pendente) =>
        pendente
            ? p => p.EsclarecimentoSolicitadoEm != null && p.EsclarecimentoRespondidoEm == null
            : p => p.EsclarecimentoSolicitadoEm == null || p.EsclarecimentoRespondidoEm != null;

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
        p.EtapaPlanejamento = Limpar(p.EtapaPlanejamento);
        p.Criticidade = Limpar(p.Criticidade);
        p.EsclarecimentoDescricao = Limpar(p.EsclarecimentoDescricao);

        // Com as respostas aos critérios, a criticidade é DERIVADA delas (fonte única
        // CtrCriticidade): o valor que veio no corpo é descartado. Sem respostas vale o
        // que veio (planilha antiga, processo classificado antes da regra automática).
        var criterios = CtrCriticidade.Desserializar(p.CriteriosCriticidade);
        if (criterios != null)
        {
            var normalizados = CtrCriticidade.Normalizar(criterios);
            p.CriteriosCriticidade = CtrCriticidade.Serializar(normalizados);
            p.Criticidade = CtrCriticidade.Calcular(normalizados);
        }

        // Limpar a data do pedido ANULA a descrição (normalização, como a restituição:
        // é gesto explícito do usuário, não erro). A RESPOSTA não entra aqui: ela é
        // fato datado, e apagá-la em silêncio esconderia perda de dado — na importação
        // a linha passaria como "Atualizar" sem nada aparecer na prévia. Vira erro,
        // logo abaixo.
        if (p.EsclarecimentoSolicitadoEm == null) p.EsclarecimentoDescricao = null;
        // Origem vazia = o caminho normal (o processo veio do órgão comunicante)
        p.Origem = Limpar(p.Origem) ?? CtrDominios.Origem.OrgaoComunicante;

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

        if (p.RetornoOrgaoNaoSeAplica && p.RetornoOrgao != null)
            throw new ApiException(ErrorCode.CtrProcessoInvalido,
                "Retorno ao órgão comunicante: marque \"não se aplica\" ou informe a data, não os dois.");

        if (p.EtapaPlanejamento != null && !CtrDominios.EtapaPlanejamento.Todos.Contains(p.EtapaPlanejamento))
            throw new ApiException(ErrorCode.CtrDominioInvalido,
                $"Etapa do planejamento inválida: {p.EtapaPlanejamento}");

        if (p.Criticidade != null && !CtrDominios.Criticidade.Todos.Contains(p.Criticidade))
            throw new ApiException(ErrorCode.CtrDominioInvalido, $"Criticidade inválida: {p.Criticidade}");

        if (!CtrDominios.Origem.Todos.Contains(p.Origem))
            throw new ApiException(ErrorCode.CtrDominioInvalido, $"Origem inválida: {p.Origem}");

        // Resposta sem pedido é RECUSADA (não normalizada): é o que o contrato chama
        // de "resposta exige pedido", e a recusa é o que faz a linha aparecer na
        // prévia da importação em vez de sumir em silêncio
        if (p.EsclarecimentoSolicitadoEm == null && p.EsclarecimentoRespondidoEm != null)
            throw new ApiException(ErrorCode.CtrProcessoInvalido,
                "Informe a data do pedido de esclarecimento antes de registrar a resposta.");

        if (p.EsclarecimentoSolicitadoEm != null && p.EsclarecimentoDescricao == null)
            throw new ApiException(ErrorCode.CtrProcessoInvalido,
                "Informe o que foi pedido ao órgão no pedido de esclarecimentos.");

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

        // A assinatura NÃO entra na cronologia do trâmite: o TCDF também analisa
        // contrato já assinado (processo de origem TCDF), então assinatura anterior
        // à chegada na SGDI é legítima. Só não pode ser futura.
        if (p.DataAssinaturaContrato != null && p.DataAssinaturaContrato.Value > hoje)
            throw new ApiException(ErrorCode.CtrDatasIncoerentes,
                $"A data de assinatura do contrato ({p.DataAssinaturaContrato.Value:dd/MM/yyyy}) "
                + "não pode ser futura.");

        // Esclarecimento é sinal paralelo: não entra na cronologia do trâmite, mas
        // as duas datas são coerentes entre si e não podem ser futuras
        if (p.EsclarecimentoSolicitadoEm != null && p.EsclarecimentoSolicitadoEm.Value > hoje)
            throw new ApiException(ErrorCode.CtrDatasIncoerentes,
                $"A data do pedido de esclarecimentos ({p.EsclarecimentoSolicitadoEm.Value:dd/MM/yyyy}) "
                + "não pode ser futura.");

        if (p.EsclarecimentoRespondidoEm != null && p.EsclarecimentoRespondidoEm.Value > hoje)
            throw new ApiException(ErrorCode.CtrDatasIncoerentes,
                $"A data da resposta ao esclarecimento ({p.EsclarecimentoRespondidoEm.Value:dd/MM/yyyy}) "
                + "não pode ser futura.");

        if (p.EsclarecimentoRespondidoEm != null && p.EsclarecimentoSolicitadoEm != null
            && p.EsclarecimentoRespondidoEm.Value < p.EsclarecimentoSolicitadoEm.Value)
            throw new ApiException(ErrorCode.CtrDatasIncoerentes,
                $"A data da resposta ao esclarecimento ({p.EsclarecimentoRespondidoEm.Value:dd/MM/yyyy}) não pode "
                + $"ser anterior à data do pedido ({p.EsclarecimentoSolicitadoEm.Value:dd/MM/yyyy}).");

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

    /// <summary>
    /// Apagar a criticidade de um processo que já tem manifestação do inciso I é
    /// RECUSADO: o despacho a reporta e sairia com o "[Alta/Média/Baixa]" do papel.
    /// Simétrica à exigência da criação da manifestação — e protege também o CHECK
    /// antigo que o Down da migration da rodada anterior recria.
    /// </summary>
    public static void ValidarCriticidadeNaoRemovida(string? anterior, string? nova, bool temIncisoI)
    {
        if (!temIncisoI) return;
        if (string.IsNullOrWhiteSpace(anterior)) return;
        if (!string.IsNullOrWhiteSpace(nova)) return;

        throw new ApiException(ErrorCode.CtrProcessoInvalido,
            "Não é possível remover a criticidade: o processo já tem manifestação do inciso I, "
            + "que a reporta ao TCDF.");
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

        // Concluídos (contrato assinado) ficam FORA da lista e do export por padrão: entram
        // só quando pedidos (IncluirConcluidos) ou quando a situação filtrada é a deles
        if (!filtro.IncluirConcluidos && string.IsNullOrWhiteSpace(filtro.Situacao))
            query = query.Where(p => p.DataAssinaturaContrato == null);

        if (!string.IsNullOrWhiteSpace(filtro.EtapaPlanejamento))
        {
            var etapa = filtro.EtapaPlanejamento.Trim();
            query = CtrDominios.EtapaPlanejamento.Todos.Contains(etapa)
                ? query.Where(p => p.EtapaPlanejamento == etapa)
                : query.Where(p => false);
        }

        if (!string.IsNullOrWhiteSpace(filtro.Criticidade))
        {
            var criticidade = filtro.Criticidade.Trim();
            query = CtrDominios.Criticidade.Todos.Contains(criticidade)
                ? query.Where(p => p.Criticidade == criticidade)
                : query.Where(p => false);
        }

        if (!string.IsNullOrWhiteSpace(filtro.Origem))
        {
            var origem = filtro.Origem.Trim();
            query = CtrDominios.Origem.Todos.Contains(origem)
                ? query.Where(p => p.Origem == origem)
                : query.Where(p => false);
        }

        if (filtro.EsclarecimentoPendente != null)
            query = query.Where(PredicadoEsclarecimentoPendente(filtro.EsclarecimentoPendente.Value));

        if (!string.IsNullOrWhiteSpace(filtro.NivelRiscoDeclarado))
        {
            // Nível MÁXIMO como predicado EF sobre os pares da matriz (nada materializado);
            // mesma regra dos demais filtros de domínio: fora dele, lista vazia
            var predicado = CtrClassificacaoRisco.PredicadoNivelRiscoDeclarado(filtro.NivelRiscoDeclarado.Trim());
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
            // ChegadaSgdi: nulos SEMPRE por último
            "ChegadaSgdi" when asc => query
                .OrderBy(p => p.ChegadaSgdi == null).ThenBy(p => p.ChegadaSgdi).ThenByDescending(p => p.Id),
            "ChegadaSgdi" => query
                .OrderBy(p => p.ChegadaSgdi == null).ThenByDescending(p => p.ChegadaSgdi).ThenByDescending(p => p.Id),
            // Criticidade (e o padrão): da mais alta para a mais baixa, os sem criticidade no
            // fim e, dentro de cada uma, a chegada mais recente primeiro (nulos por último)
            "Criticidade" when !asc => query
                .OrderByDescending(OrdemCriticidade)
                .ThenBy(p => p.ChegadaSgdi == null).ThenByDescending(p => p.ChegadaSgdi).ThenByDescending(p => p.Id),
            _ => query
                .OrderBy(OrdemCriticidade)
                .ThenBy(p => p.ChegadaSgdi == null).ThenByDescending(p => p.ChegadaSgdi).ThenByDescending(p => p.Id)
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

        return await MapearDetalheAsync(processo);
    }

    public async Task<CtrProcessoResponse> CriarAsync(CtrProcessoCreateDTO dto, CtrUserContext ctx)
    {
        var agora = DateTime.UtcNow;
        var processo = new CtrProcesso
        {
            CriadoEm = agora,
            CriadoPor = ctx.Email
        };
        AplicarDto(processo, dto);

        ValidarProcesso(processo, await _repositorio.NumeroDuplicadoAsync(processo.NumeroProcesso, null));

        // Nula ou vazia = processo sem riscos da contratação; enviada, cada risco é validado
        var riscos = dto.ClassificacaoRisco != null
            ? NovosRiscos(processo, CtrClassificacaoRisco.Avaliar(dto.ClassificacaoRisco), ctx.Email, agora)
            : new List<CtrRiscoDeclarado>();

        _repositorio.Add(processo);
        foreach (var risco in riscos) _repositorio.AddRiscoDeclarado(risco);
        await _repositorio.SaveChangesAsync();

        return MapDetalhe(processo, riscos, HojeBrasilia());
    }

    public async Task<CtrProcessoResponse> AtualizarAsync(long id, CtrProcessoUpdateDTO dto, CtrUserContext ctx)
    {
        var processo = await GetEntidadeAsync(id)
            ?? throw new ApiException(ErrorCode.CtrProcessoNaoEncontrado);

        // Substituir e remover os riscos no mesmo pedido é ambíguo: recusado
        if (dto.ClassificacaoRisco != null && dto.LimparClassificacaoRisco)
            throw new ApiException(ErrorCode.CtrClassificacaoRiscoInvalida,
                "Envie os riscos da contratação ou o pedido para removê-los, não os dois.");

        // Valida num candidato solto: edição recusada não pode deixar rastro na
        // entidade rastreada pelo contexto
        var candidato = Clonar(processo);
        AplicarDto(candidato, dto);

        // Origem ausente no corpo PRESERVA a gravada: "vazio = Órgão comunicante"
        // vale só na criação (o front antigo não manda o campo, e resetar a origem
        // de um processo do TCDF numa edição qualquer é perda de dado silenciosa)
        if (string.IsNullOrWhiteSpace(dto.Origem)) candidato.Origem = processo.Origem;

        // Respostas aos critérios ausentes no corpo PRESERVAM as gravadas (o front só as
        // manda quando o usuário as avaliou); com elas gravadas a criticidade é recalculada
        // na validação, e o Criticidade do corpo só vale para processo ainda sem respostas
        if (dto.CriteriosCriticidade == null) candidato.CriteriosCriticidade = processo.CriteriosCriticidade;

        ValidarProcesso(candidato, await _repositorio.NumeroDuplicadoAsync(candidato.NumeroProcesso, processo.Id));
        ValidarCriticidadeNaoRemovida(processo.Criticidade, candidato.Criticidade,
            await _repositorio.TemManifestacaoIncisoIAsync(processo.Id));

        // Os riscos também são validados por inteiro ANTES de qualquer escrita (Avaliar é
        // pura). Limpar equivale a mandar a lista vazia. Sem nenhum dos dois, os gravados
        // ficam intactos: ausência de dado nunca apaga dado gravado.
        var mexeNosRiscos = dto.ClassificacaoRisco != null || dto.LimparClassificacaoRisco;
        var riscosEnviados = dto.ClassificacaoRisco != null
            ? CtrClassificacaoRisco.Avaliar(dto.ClassificacaoRisco)
            : new List<CtrClassificacaoRisco.RiscoAvaliado>();
        var riscosAnteriores = mexeNosRiscos
            ? await _repositorio.ListarRiscosDeclaradosAsync(processo.Id)
            : new List<CtrRiscoDeclarado>();

        // Reenvio IDÊNTICO ao gravado (conteúdo normalizado, sem ordem) não substitui
        // nada: nem os Ids dos riscos nem o registro de quem/quando mudam
        var substituiRiscos = mexeNosRiscos
            && !CtrClassificacaoRisco.MesmosRiscos(riscosAnteriores, riscosEnviados);

        var agora = DateTime.UtcNow;
        AplicarDto(processo, DtoDe(candidato));
        processo.AlteradoEm = agora;
        processo.AlteradoPor = ctx.Email;

        if (mexeNosRiscos)
        {
            // O questionário antigo do PGIA sai de vez quando os riscos são mexidos
            DescartarQuestionarioLegado(processo);

            if (substituiRiscos)
            {
                _repositorio.RemoverRiscosDeclarados(riscosAnteriores);
                foreach (var risco in NovosRiscos(processo, riscosEnviados, ctx.Email, agora))
                    _repositorio.AddRiscoDeclarado(risco);
            }
        }

        await _repositorio.SaveChangesAsync();
        return await MapearDetalheAsync(processo);
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

        if (dto.NaoSeAplica != null
            && etapa != CtrDominios.Etapa.ChegadaUgtic && etapa != CtrDominios.Etapa.RetornoOrgao)
            throw new ApiException(ErrorCode.CtrProcessoInvalido,
                "\"Não se aplica\" só vale para a chegada à UGTIC e para o retorno ao órgão comunicante.");

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
                // Mesma simetria da UGTIC: marcar "não se aplica" limpa a data e
                // informar a data desmarca o flag (é o gesto de reativar a etapa)
                if (dto.NaoSeAplica == true)
                {
                    candidato.RetornoOrgaoNaoSeAplica = true;
                    candidato.RetornoOrgao = null;
                }
                else
                {
                    if (dto.NaoSeAplica == false || dto.Data != null) candidato.RetornoOrgaoNaoSeAplica = false;
                    candidato.RetornoOrgao = dto.Data;
                }
                break;
            case CtrDominios.Etapa.AssinaturaContrato:
                // A data final: com ela o processo fica Concluído (limpar reabre)
                candidato.DataAssinaturaContrato = dto.Data;
                break;
        }

        ValidarProcesso(candidato, await _repositorio.NumeroDuplicadoAsync(candidato.NumeroProcesso, processo.Id));

        // O checkpoint nunca toca a classificação de riscos (AplicarDto não a copia)
        AplicarDto(processo, DtoDe(candidato));
        processo.AlteradoEm = DateTime.UtcNow;
        processo.AlteradoPor = ctx.Email;

        await _repositorio.SaveChangesAsync();
        return await MapearDetalheAsync(processo);
    }

    /// <summary>
    /// Copia o DTO para a entidade (create e update têm a mesma forma). NÃO copia a
    /// classificação de riscos de propósito: importação e checkpoint passam por aqui e
    /// não podem tocá-la — ela só entra por <see cref="AplicarClassificacao"/>.
    /// </summary>
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
        processo.RetornoOrgaoNaoSeAplica = dto.RetornoOrgaoNaoSeAplica;
        processo.EtapaPlanejamento = dto.EtapaPlanejamento;
        processo.DataAssinaturaContrato = dto.DataAssinaturaContrato;
        processo.Criticidade = dto.Criticidade;
        processo.CriteriosCriticidade = dto.CriteriosCriticidade == null
            ? null
            : CtrCriticidade.Serializar(dto.CriteriosCriticidade);
        processo.Origem = dto.Origem ?? CtrDominios.Origem.OrgaoComunicante;
        processo.EsclarecimentoSolicitadoEm = dto.EsclarecimentoSolicitadoEm;
        processo.EsclarecimentoDescricao = dto.EsclarecimentoDescricao;
        processo.EsclarecimentoRespondidoEm = dto.EsclarecimentoRespondidoEm;
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
        RetornoOrgaoNaoSeAplica = p.RetornoOrgaoNaoSeAplica,
        EtapaPlanejamento = p.EtapaPlanejamento,
        DataAssinaturaContrato = p.DataAssinaturaContrato,
        Criticidade = p.Criticidade,
        CriteriosCriticidade = CtrCriticidade.Desserializar(p.CriteriosCriticidade),
        Origem = p.Origem,
        EsclarecimentoSolicitadoEm = p.EsclarecimentoSolicitadoEm,
        EsclarecimentoDescricao = p.EsclarecimentoDescricao,
        EsclarecimentoRespondidoEm = p.EsclarecimentoRespondidoEm,
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
                RetornoOrgaoNaoSeAplica = p.RetornoOrgaoNaoSeAplica,
                DataAssinaturaContrato = p.DataAssinaturaContrato,
                RestituidoEm = p.RestituidoEm,
                EsclarecimentoSolicitadoEm = p.EsclarecimentoSolicitadoEm,
                EsclarecimentoRespondidoEm = p.EsclarecimentoRespondidoEm,
                CriadoEm = p.CriadoEm
            })
            .ToListAsync();

        // Nível máximo declarado de cada processo: UMA consulta enxuta (sem os textos)
        var nivelMaximoPorProcesso = (await _repositorio.ListarEscalasDeRiscoDeProcessosAtivosAsync())
            .GroupBy(r => r.ProcessoId)
            .ToDictionary(g => g.Key, g => CtrClassificacaoRisco.CalcularNivelMaximo(
                g.Select(r => (r.Probabilidade, r.Consequencia))));

        var comSituacao = resumos.Select(r => new
        {
            Resumo = r,
            Situacao = CalcularSituacao(r.DataAssinaturaContrato, r.Restituido, r.ChegadaSgdi, r.ChegadaSubgd,
                r.ChegadaUgtic, r.RetornoGabSgdi, r.RetornoOrgao, r.RetornoOrgaoNaoSeAplica),
            Dias = CalcularDiasSemMovimento(
                CalcularUltimaMovimentacao(r.ChegadaSgdi, r.ChegadaSubgd, r.ChegadaUgtic,
                    r.RetornoGabSgdi, r.RetornoOrgao, r.RestituidoEm,
                    r.EsclarecimentoSolicitadoEm, r.EsclarecimentoRespondidoEm),
                r.CriadoEm, hoje)
        }).ToList();

        var painel = new CtrPainelResponse
        {
            TotalAtivos = resumos.Count,
            LimiteDias = limiteDias,
            TotalEsclarecimentoPendente = resumos.Count(r => CalcularEsclarecimentoPendente(
                r.EsclarecimentoSolicitadoEm, r.EsclarecimentoRespondidoEm)),
            // As 8 situações aparecem sempre, mesmo com zero. Ordem determinística:
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
            TotalConcluidos = resumos.Count(r => r.DataAssinaturaContrato != null),
            // Nível MÁXIMO dos riscos por processo: Baixo..Extremo + "Sem riscos declarados",
            // sempre presentes, com o mesmo desempate
            PorNivelRiscoDeclarado = ContarNoDominio(
                CtrDominios.NivelRisco.Todos.Append(CtrDominios.NivelRisco.SemRiscosDeclarados),
                resumos.Select(r => nivelMaximoPorProcesso.GetValueOrDefault(r.Id)
                                    ?? CtrDominios.NivelRisco.SemRiscosDeclarados).ToList()),
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
                ChegadaParaConclusao = Media(resumos, r => r.ChegadaSgdi, r => r.RetornoOrgao),
                ChegadaParaAssinatura = Media(resumos, r => r.ChegadaSgdi, r => r.DataAssinaturaContrato)
            }
        };

        // Parados: nem os concluídos, nem os de análise concluída, nem os restituídos
        var gargalos = comSituacao
            .Where(x => x.Situacao != CtrDominios.Situacao.Concluido
                        && x.Situacao != CtrDominios.Situacao.AnaliseConcluida
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

        // Relação de concluídos: os de assinatura mais recente, com os mesmos dados da lista
        var idsConcluidos = resumos
            .Where(r => r.DataAssinaturaContrato != null)
            .OrderByDescending(r => r.DataAssinaturaContrato)
            .ThenByDescending(r => r.Id)
            .Take(LimiteConcluidos)
            .Select(r => r.Id)
            .ToList();

        if (idsConcluidos.Count > 0)
        {
            var processos = await _repositorio.QueryAtivos().Where(p => idsConcluidos.Contains(p.Id)).ToListAsync();
            var mapeados = (await MapearComManifestacoesAsync(processos)).ToDictionary(p => p.Id);

            painel.Concluidos = idsConcluidos
                .Where(mapeados.ContainsKey)
                .Select(id => mapeados[id])
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

    /// <summary>
    /// Contagem com TODAS as chaves do domínio (mesmo as com zero): quantidade desc e,
    /// no empate, a ordem do domínio.
    /// </summary>
    private static List<CtrContagem> ContarNoDominio(IEnumerable<string> dominio, IReadOnlyCollection<string> valores) =>
        dominio
            .Select((chave, ordem) => new
            {
                Contagem = new CtrContagem { Chave = chave, Quantidade = valores.Count(v => v == chave) },
                Ordem = ordem
            })
            .OrderByDescending(c => c.Contagem.Quantidade).ThenBy(c => c.Ordem)
            .Select(c => c.Contagem)
            .ToList();

    // ── Mapeamento ────────────────────────────────────────────────────────────

    /// <summary>
    /// Mapeia os processos resolvendo TotalManifestacoes/UltimoEstagioTcdf e o
    /// NivelMaximoRiscoDeclarado em LOTE (uma consulta de manifestações e uma de escalas
    /// de risco para a página inteira, sem N+1). A classificação aninhada NÃO vem aqui.
    /// </summary>
    private async Task<List<CtrProcessoResponse>> MapearComManifestacoesAsync(List<CtrProcesso> processos)
    {
        var hoje = HojeBrasilia();
        if (processos.Count == 0) return new List<CtrProcessoResponse>();

        var ids = processos.Select(p => p.Id).ToList();
        var manifestacoes = await _repositorio.ListarManifestacoesPorProcessosAsync(ids);

        var nivelMaximoPorProcesso = (await _repositorio.ListarEscalasDeRiscoPorProcessosAsync(ids))
            .GroupBy(r => r.ProcessoId)
            .ToDictionary(g => g.Key, g => CtrClassificacaoRisco.CalcularNivelMaximo(
                g.Select(r => (r.Probabilidade, r.Consequencia))));

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
            resposta.NivelMaximoRiscoDeclarado = nivelMaximoPorProcesso.GetValueOrDefault(p.Id);
            if (porProcesso.TryGetValue(p.Id, out var agregado))
            {
                resposta.TotalManifestacoes = agregado.Total;
                resposta.UltimoEstagioTcdf = CtrManifestacaoService.CalcularEstagio(agregado.Ultima);
            }
            return resposta;
        }).ToList();
    }

    /// <summary>
    /// Leitura de UM processo (GET {id}, PUT e checkpoint): os campos planos, mais os
    /// riscos da contratação aninhados.
    /// </summary>
    private async Task<CtrProcessoResponse> MapearDetalheAsync(CtrProcesso processo)
    {
        var resposta = (await MapearComManifestacoesAsync(new List<CtrProcesso> { processo })).Single();
        var riscos = await _repositorio.ListarRiscosDeclaradosAsync(processo.Id);
        resposta.ClassificacaoRisco = CtrClassificacaoRisco.MapearClassificacao(riscos);
        return resposta;
    }

    /// <summary>Mesma forma do detalhe, sem consulta (processo recém-criado, sem manifestações).</summary>
    private static CtrProcessoResponse MapDetalhe(CtrProcesso processo, IReadOnlyCollection<CtrRiscoDeclarado> riscos,
        DateOnly hoje)
    {
        var resposta = MapProcesso(processo, hoje);
        resposta.NivelMaximoRiscoDeclarado = CtrClassificacaoRisco.CalcularNivelMaximo(
            riscos.Select(r => (r.Probabilidade, r.Consequencia)));
        resposta.ClassificacaoRisco = CtrClassificacaoRisco.MapearClassificacao(riscos);
        return resposta;
    }

    /// <summary>
    /// Entidades dos riscos JÁ VALIDADOS, com quem registrou e quando, para quem chama
    /// incluir no contexto (a lista anterior é removida por quem chama, na edição).
    /// </summary>
    private static List<CtrRiscoDeclarado> NovosRiscos(CtrProcesso processo,
        IEnumerable<CtrClassificacaoRisco.RiscoAvaliado> riscos, string email, DateTime agora)
    {
        return riscos
            .Select(r => new CtrRiscoDeclarado
            {
                Processo = processo,
                ProcessoId = processo.Id,
                DescricaoRisco = r.DescricaoRisco,
                AcaoMitigacao = r.AcaoMitigacao,
                ResponsavelNome = r.ResponsavelNome,
                ResponsavelEmail = r.ResponsavelEmail,
                Probabilidade = r.Probabilidade,
                Consequencia = r.Consequencia,
                CriadoEm = agora,
                CriadoPor = email
            })
            .ToList();
    }

    /// <summary>
    /// Zera as colunas LEGADAS do questionário dos arts. 15 a 17 do PGIA (sem uso desde
    /// 2026-09-21, ver CtrProcesso). Todas nulas juntas é o que o CHECK tudo-ou-nada aceita.
    /// </summary>
    private static void DescartarQuestionarioLegado(CtrProcesso processo)
    {
        processo.ChecklistRisco = null;
        processo.RiscoClassificado = null;
        processo.EnquadramentoRisco = null;
        processo.PontuacaoRisco = null;
        processo.RiscoClassificadoEm = null;
        processo.RiscoClassificadoPor = null;
    }

    public static CtrProcessoResponse MapProcesso(CtrProcesso p, DateOnly hoje)
    {
        var ultimaMovimentacao = CalcularUltimaMovimentacao(p);
        var criterios = CtrCriticidade.Desserializar(p.CriteriosCriticidade);

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
            RetornoOrgaoNaoSeAplica = p.RetornoOrgaoNaoSeAplica,
            EtapaPlanejamento = p.EtapaPlanejamento,
            DataAssinaturaContrato = p.DataAssinaturaContrato,
            Criticidade = p.Criticidade,
            CriteriosCriticidade = criterios,
            PontosCriticidade = criterios == null ? null : CtrCriticidade.PontosTotais(criterios),
            Origem = p.Origem,
            EsclarecimentoSolicitadoEm = p.EsclarecimentoSolicitadoEm,
            EsclarecimentoDescricao = p.EsclarecimentoDescricao,
            EsclarecimentoRespondidoEm = p.EsclarecimentoRespondidoEm,
            EsclarecimentoPendente = CalcularEsclarecimentoPendente(p),
            DiasEsclarecimentoPendente = CalcularDiasEsclarecimentoPendente(
                p.EsclarecimentoSolicitadoEm, p.EsclarecimentoRespondidoEm, hoje),
            Restituido = p.Restituido,
            RestituidoEm = p.RestituidoEm,
            RestituidoMotivo = p.RestituidoMotivo,
            Observacao = p.Observacao,
            Situacao = CalcularSituacao(p),
            UltimaMovimentacao = ultimaMovimentacao,
            DiasSemMovimento = CalcularDiasSemMovimento(ultimaMovimentacao, p.CriadoEm, hoje),
            // O nível máximo e os riscos aninhados dependem dos riscos: quem chama resolve
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
        public bool RetornoOrgaoNaoSeAplica { get; set; }
        public DateOnly? DataAssinaturaContrato { get; set; }
        public DateOnly? RestituidoEm { get; set; }
        public DateOnly? EsclarecimentoSolicitadoEm { get; set; }
        public DateOnly? EsclarecimentoRespondidoEm { get; set; }
        public DateTime CriadoEm { get; set; }
    }
}
