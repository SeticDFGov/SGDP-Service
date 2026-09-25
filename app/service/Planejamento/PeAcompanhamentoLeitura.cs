using System.Globalization;
using System.Text.Json;
using api.Planejamento;
using Microsoft.EntityFrameworkCore;
using Models;
using Models.Planejamento;
using service.Interface;

namespace service.Planejamento;

/// <summary>
/// Os dados do acompanhamento de um PDTIC lidos de uma vez pelo motor de registros (as seções
/// visíveis para o órgão, com o ciclo de cada registro das seções por ciclo), para as grades, o
/// painel (AC-PDTIC) e os blocos do RA e do RR. Não confere quem chama: quem chama já conferiu.
/// </summary>
public sealed class PeDadosDoAcompanhamento
{
    private readonly Dictionary<string, PeSecaoExportada> _secoes;

    private PeDadosDoAcompanhamento(Dictionary<string, PeSecaoExportada> secoes)
    {
        _secoes = secoes;
    }

    /// <summary>As seções pedidas que o órgão vê, com os registros de todos os ciclos.</summary>
    public static async Task<PeDadosDoAcompanhamento> CarregarAsync(IPeRegistroService registros, PePdtic pdtic, PeTrilhaOrgao trilha,
        params string[] chaves)
    {
        var montadas = chaves.Distinct()
            .Select(c => trilha.Secao(c))
            .Where(s => s != null)
            .Select(s => trilha.Montar(s!.Value.Secao))
            .ToList();
        var exportadas = montadas.Count == 0
            ? new List<PeSecaoExportada>()
            : await registros.ExportarAsync(PeDono.DoPdtic(pdtic.Id), montadas);
        return new PeDadosDoAcompanhamento(exportadas.ToDictionary(s => s.Modelo.Secao.Chave));
    }

    /// <summary>Os dados já lidos (a resolução do documento lê de uma vez as seções de todos os blocos).</summary>
    public static PeDadosDoAcompanhamento De(IReadOnlyDictionary<string, PeSecaoExportada> secoes) =>
        new(secoes.ToDictionary(s => s.Key, s => s.Value));

    /// <summary>A seção, quando o órgão a vê.</summary>
    public PeSecaoExportada? Secao(string chave) => _secoes.GetValueOrDefault(chave);

    public bool Tem(string chave) => _secoes.ContainsKey(chave);

    /// <summary>O campo está visível na seção para o órgão.</summary>
    public bool CampoVisivel(string secao, string campo) =>
        Secao(secao)?.Colunas.Any(c => c.Campo.Chave == campo) == true;

    /// <summary>Os registros de uma seção (na seção por ciclo, de todos os ciclos).</summary>
    public List<PeRegistroResponse> Registros(string chave) => Secao(chave)?.Registros ?? new List<PeRegistroResponse>();

    /// <summary>Os registros de uma seção por ciclo num ciclo.</summary>
    public List<PeRegistroResponse> DoCiclo(string chave, long cicloId)
    {
        var secao = Secao(chave);
        return secao == null
            ? new List<PeRegistroResponse>()
            : secao.Registros.Where(r => secao.CicloDoRegistro.TryGetValue(r.Id, out var c) && c == cicloId).ToList();
    }

    /// <summary>O ciclo de um registro de seção por ciclo, ou nulo.</summary>
    public long? CicloDe(string chave, long registroId) =>
        Secao(chave)?.CicloDoRegistro.TryGetValue(registroId, out var c) == true ? c : null;

    // ── Leitura dos valores de um registro (a resposta do motor) ────────────

    public static string? Texto(PeRegistroResponse registro, string campo) =>
        registro.Dados.TryGetValue(campo, out var valor) && valor.ValueKind == JsonValueKind.String
            ? string.IsNullOrWhiteSpace(valor.GetString()) ? null : valor.GetString()!.Trim()
            : null;

    public static decimal? Numero(PeRegistroResponse registro, string campo) =>
        registro.Dados.TryGetValue(campo, out var valor) && valor.ValueKind == JsonValueKind.Number && valor.TryGetDecimal(out var n) ? n : null;

    public static DateOnly? Data(PeRegistroResponse registro, string campo) =>
        Texto(registro, campo) is string texto
        && DateOnly.TryParseExact(texto, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var data)
            ? data
            : null;

    /// <summary>O texto pronto do campo (rótulo da opção, data, valor), ou nulo.</summary>
    public static string? Rotulo(PeRegistroResponse registro, string campo) =>
        registro.Rotulos.TryGetValue(campo, out var texto) && !string.IsNullOrWhiteSpace(texto) ? texto.Trim() : null;

    /// <summary>Os ids dos registros ligados por um campo de ligação.</summary>
    public static List<long> Ligados(PeRegistroResponse registro, string campo) =>
        registro.Vinculos.TryGetValue(campo, out var lista) ? lista.Select(v => v.RegistroId).ToList() : new List<long>();

    /// <summary>Os códigos dos registros ligados (sem código, o resumo).</summary>
    public static List<string> CodigosLigados(PeRegistroResponse registro, string campo) =>
        registro.Vinculos.TryGetValue(campo, out var lista) ? lista.Select(v => v.Codigo ?? v.Resumo).ToList() : new List<string>();
}

/// <summary>
/// As regras do painel do PDTIC (AC-PDTIC, Anexo XIII do guia) e dos grupos do relatório de
/// acompanhamento, puras:
/// <list type="bullet">
/// <item>situação física da ação: cancelada e concluída pela situação registrada; atrasada quando
/// não está concluída e a conclusão prevista venceu, ou não começou e o início previsto venceu;
/// senão em dia; sem registro, sem_registro. A data de referência é o fim do ciclo, quando ele já
/// passou; senão, hoje;</item>
/// <item>% da ação: a execução física registrada (a concluída sem o percentual conta 100);</item>
/// <item>% da meta: a média das ações ligadas que têm o percentual (a cancelada fica de fora),
/// ponderada pelo peso da ação na meta do plano de execução (passo 4.2) quando todas têm peso;</item>
/// <item>orçamento: não se aplica quando a ação não tem investimento nem custeio.</item>
/// </list>
/// </summary>
public static class PeRegrasDoPainel
{
    /// <summary>A data de referência de um ciclo: o fim dele, quando já passou; senão, hoje.</summary>
    public static DateOnly Referencia(PeCiclo? ciclo, DateOnly hoje) =>
        ciclo?.Fim is DateOnly fim && fim < hoje ? fim : hoje;

    public static string SituacaoFisica(bool temRegistro, string? situacao, DateOnly? inicioPrevisto, DateOnly? conclusaoPrevista, DateOnly referencia)
    {
        if (!temRegistro) return PeDominios.SituacaoFisica.SemRegistro;
        if (situacao == PeDominios.ChaveAcompanhamento.AcaoCancelada) return PeDominios.SituacaoFisica.Cancelada;
        if (situacao == PeDominios.ChaveAcompanhamento.AcaoConcluida) return PeDominios.SituacaoFisica.Concluida;
        var conclusaoVencida = conclusaoPrevista is DateOnly conclusao && conclusao < referencia;
        var inicioVencido = inicioPrevisto is DateOnly inicio && inicio < referencia;
        if (situacao == PeDominios.ChaveAcompanhamento.AcaoNaoIniciada)
            return inicioVencido || conclusaoVencida ? PeDominios.SituacaoFisica.Atrasada : PeDominios.SituacaoFisica.EmDia;
        return conclusaoVencida ? PeDominios.SituacaoFisica.Atrasada : PeDominios.SituacaoFisica.EmDia;
    }

    public static decimal? PercentualDaAcao(decimal? execucaoFisica, string? situacao) =>
        execucaoFisica ?? (situacao == PeDominios.ChaveAcompanhamento.AcaoConcluida ? 100m : null);

    /// <summary>A média das ações (percentual e peso), ponderada quando todas têm peso; nulo sem percentual.</summary>
    public static decimal? PercentualDaMeta(IReadOnlyList<(decimal? Percentual, decimal? Peso, bool Cancelada)> acoes)
    {
        var validas = acoes.Where(a => !a.Cancelada && a.Percentual != null).ToList();
        if (validas.Count == 0) return null;
        decimal media;
        if (validas.All(a => a.Peso is > 0))
        {
            var pesos = validas.Sum(a => a.Peso!.Value);
            media = validas.Sum(a => a.Percentual!.Value * a.Peso!.Value) / pesos;
        }
        else
        {
            media = validas.Average(a => a.Percentual!.Value);
        }
        return Math.Round(media, 2, MidpointRounding.AwayFromZero);
    }

    public static bool OrcamentoNaoSeAplica(decimal? investimento, decimal? custeio) =>
        (investimento ?? 0) == 0 && (custeio ?? 0) == 0;
}

/// <summary>Os ciclos de um PDTIC como a situação dos passos e os relatórios os veem (sem gravar nada).</summary>
public static class PeCiclosDoPdtic
{
    /// <summary>
    /// Os ciclos que valem para o PDTIC: os gravados e, no PDTIC vigente, o plano dos ciclos de
    /// monitoramento (os que faltam entram, os sem registro fora da periodicidade saem), sem gravar
    /// (a lista dos ciclos grava; a situação dos passos só calcula). Antes da publicação, nenhum.
    /// </summary>
    public static async Task<List<PeCiclo>> EfetivosAsync(AppDbContext context, IPeRegistroService registros, PePdtic pdtic,
        PeTrilhaOrgao trilha)
    {
        if (PeDominios.SituacaoPdtic.DaElaboracao.Contains(pdtic.Situacao)) return new List<PeCiclo>();
        var gravados = await context.PeCiclos.AsNoTracking().Where(c => c.PdticId == pdtic.Id).ToListAsync();
        if (!PeDominios.SituacaoPdtic.Vigentes.Contains(pdtic.Situacao)) return Ordenar(gravados);

        var plano = await PlanoAsync(context, registros, pdtic, trilha, gravados, "situacao");
        return Ordenar(plano.Resultado().Concat(gravados.Where(c => c.Tipo == PeDominios.TipoCiclo.Avaliacao)).ToList());
    }

    /// <summary>
    /// Os ciclos efetivos com o que já foi lido (a mesma regra do <see cref="EfetivosAsync"/>, sem
    /// gravar): os gravados do PDTIC, os ciclos que têm registro e a periodicidade do passo 4.3. A
    /// situação de vários PDTICs de uma vez (E8) usa.
    /// </summary>
    public static List<PeCiclo> Efetivos(PePdtic pdtic, PeTrilhaOrgao trilha, IReadOnlyList<PeCiclo> gravados, ISet<long> comRegistros,
        string periodicidade)
    {
        if (PeDominios.SituacaoPdtic.DaElaboracao.Contains(pdtic.Situacao)) return new List<PeCiclo>();
        if (!PeDominios.SituacaoPdtic.Vigentes.Contains(pdtic.Situacao)) return Ordenar(gravados);

        var meses = PeDominios.Periodicidade.Meses(periodicidade) ?? 3;
        var plano = PeCiclos.PlanejarMonitoramento(pdtic, gravados, comRegistros, meses, trilha.Dados.Acompanhamento.PrazoFechamentoDias,
            "situacao", DateTime.UtcNow);
        return Ordenar(plano.Resultado().Concat(gravados.Where(c => c.Tipo == PeDominios.TipoCiclo.Avaliacao)).ToList());
    }

    /// <summary>
    /// A periodicidade do monitoramento a partir da seção já analisada (a do passo 4.3, quando o
    /// órgão a vê, com o campo visível e preenchido com um valor que o módulo conhece); senão a padrão.
    /// </summary>
    public static string Periodicidade(PeTrilhaOrgao trilha, PeSecaoAnalisada? secao)
    {
        var padrao = trilha.Dados.Acompanhamento.PeriodicidadePadrao;
        if (secao == null || secao.Secao.Visiveis.All(v => v.Campo.Chave != PeDominios.ChaveAcompanhamento.CampoPeriodicidade)) return padrao;
        var valor = PeRegistroDados.Texto(PeRegistroDados.Ler(secao.Registros.FirstOrDefault()?.Dados)[PeDominios.ChaveAcompanhamento.CampoPeriodicidade]);
        return PeDominios.Periodicidade.Meses(valor) != null ? valor! : padrao;
    }

    /// <summary>O plano dos ciclos de monitoramento do PDTIC vigente, pela periodicidade do passo 4.3 (ou a padrão).</summary>
    public static async Task<PeCiclos.Plano> PlanoAsync(AppDbContext context, IPeRegistroService registros, PePdtic pdtic,
        PeTrilhaOrgao trilha, IReadOnlyList<PeCiclo> gravados, string autor)
    {
        var ids = gravados.Select(c => c.Id).ToList();
        var comRegistros = ids.Count == 0
            ? new HashSet<long>()
            : (await context.PeRegistrosCiclo.AsNoTracking().Where(r => ids.Contains(r.CicloId)).Select(r => r.CicloId).Distinct().ToListAsync())
            .ToHashSet();
        var meses = PeDominios.Periodicidade.Meses(await PeriodicidadeAsync(registros, pdtic, trilha)) ?? 3;
        return PeCiclos.PlanejarMonitoramento(pdtic, gravados, comRegistros, meses, trilha.Dados.Acompanhamento.PrazoFechamentoDias,
            autor, DateTime.UtcNow);
    }

    /// <summary>A periodicidade do monitoramento: a do passo 4.3, quando o órgão a vê e preencheu; senão a padrão.</summary>
    public static async Task<string> PeriodicidadeAsync(IPeRegistroService registros, PePdtic pdtic, PeTrilhaOrgao trilha)
    {
        var padrao = trilha.Dados.Acompanhamento.PeriodicidadePadrao;
        if (trilha.Secao(PeDominios.ChaveAcompanhamento.SecaoPeriodicidade) is not { } visivel) return padrao;
        var secao = trilha.Montar(visivel.Secao);
        if (secao.Visiveis.All(v => v.Campo.Chave != PeDominios.ChaveAcompanhamento.CampoPeriodicidade)) return padrao;
        var registro = (await registros.AnalisarAsync(PeDono.DoPdtic(pdtic.Id), new[] { secao })).Secoes[0].Registros.FirstOrDefault();
        var valor = PeRegistroDados.Texto(PeRegistroDados.Ler(registro?.Dados)[PeDominios.ChaveAcompanhamento.CampoPeriodicidade]);
        return PeDominios.Periodicidade.Meses(valor) != null ? valor! : padrao;
    }

    /// <summary>O monitoramento pela ordem do início; depois as avaliações, pelo número.</summary>
    public static List<PeCiclo> Ordenar(IEnumerable<PeCiclo> ciclos) =>
        ciclos.OrderBy(c => c.Tipo == PeDominios.TipoCiclo.Monitoramento ? 0 : 1)
            .ThenBy(c => c.Tipo == PeDominios.TipoCiclo.Monitoramento ? c.Inicio.DayNumber : c.Numero)
            .ThenBy(c => c.Id)
            .ToList();
}
