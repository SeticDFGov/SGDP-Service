using System.Globalization;
using System.Text.RegularExpressions;
using demanda_service.Helpers;
using Models.Planejamento;

namespace service.Planejamento;

/// <summary>
/// As regras puras dos ciclos do acompanhamento (E7, rodada B), sem banco:
/// <list type="bullet">
/// <item>os períodos do monitoramento seguem o calendário pela periodicidade (1 mês, 2, 3, 6 ou
/// 12), do período em que o acompanhamento começa até o fim da vigência, cada um cortado pela
/// vigência; o rótulo é "2027 · 1º trimestre", "2027 · março", "2027 · 2º bimestre", "2027 · 1º
/// semestre" ou "2027"; o prazo de fechamento é o fim mais os dias da configuração;</item>
/// <item>o acompanhamento começa na publicação (no PDTIC registrado fora do sistema, no dia do
/// registro), nunca antes do início da vigência: o PDTIC publicado (ou registrado) no meio da
/// vigência não nasce com os ciclos passados atrasados;</item>
/// <item>o plano é idempotente: o ciclo que já existe com o mesmo período fica; o fechado ou com
/// registro fica sempre (mesmo que a periodicidade mude); o outro sai; e os períodos que faltam
/// entram, sem cobrir o que ficou;</item>
/// <item>a situação exibida: futuro (ainda não começou), aberto, atrasado (o prazo venceu sem
/// fechar) e fechado. Os dados só entram no ciclo que começou e não foi fechado.</item>
/// </list>
/// </summary>
public static partial class PeCiclos
{
    private static readonly string[] Meses =
    {
        "janeiro", "fevereiro", "março", "abril", "maio", "junho",
        "julho", "agosto", "setembro", "outubro", "novembro", "dezembro"
    };

    /// <summary>Hoje, em Brasília.</summary>
    public static DateOnly Hoje() => DateOnly.FromDateTime(DateTimeHelper.TodayBrasilia());

    /// <summary>A data em dd/mm/aaaa.</summary>
    public static string Data(DateOnly data) => data.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

    // ── Situação ────────────────────────────────────────────────────────────

    /// <summary>A situação que a tela mostra: futuro, aberto, atrasado ou fechado.</summary>
    public static string Exibida(PeCiclo ciclo, DateOnly hoje) =>
        ciclo.Situacao == PeDominios.SituacaoCiclo.Fechado ? PeDominios.SituacaoCicloExibida.Fechado
        : ciclo.Inicio > hoje ? PeDominios.SituacaoCicloExibida.Futuro
        : ciclo.Prazo is DateOnly prazo && prazo < hoje ? PeDominios.SituacaoCicloExibida.Atrasado
        : PeDominios.SituacaoCicloExibida.Aberto;

    /// <summary>Por que o ciclo não recebe dados agora (fechado ou ainda não começou), ou nulo quando recebe.</summary>
    public static string? RecusaDeDados(PeCiclo ciclo, DateOnly hoje)
    {
        if (ciclo.Situacao == PeDominios.SituacaoCiclo.Fechado)
            return ciclo.FechadoEm is DateTime fechado
                ? $"O ciclo {ciclo.Rotulo} foi fechado em {PePdticService.DataBrasilia(fechado)}. Para mudar os dados dele, reabra o ciclo."
                : $"O ciclo {ciclo.Rotulo} está fechado. Para mudar os dados dele, reabra o ciclo.";
        if (ciclo.Inicio > hoje)
            return $"O ciclo {ciclo.Rotulo} começa em {Data(ciclo.Inicio)}. Os dados entram a partir do início do ciclo.";
        return null;
    }

    internal static ApiException NaoEncontrado() =>
        new(ErrorCode.PeCicloNaoEncontrado, "Ciclo não encontrado neste PDTIC. Atualize a tela.");

    // ── Períodos do monitoramento ───────────────────────────────────────────

    public sealed record Periodo(DateOnly Inicio, DateOnly Fim, string Rotulo);

    /// <summary>
    /// O dia em que o acompanhamento começa: a publicação (no PDTIC registrado fora do sistema, o
    /// dia do registro), em Brasília, e nunca antes do início da vigência.
    /// </summary>
    public static DateOnly InicioDoAcompanhamento(PePdtic pdtic)
    {
        var base_ = pdtic.RegistradoExternamente ? pdtic.CriadoEm : pdtic.PublicadoEm ?? pdtic.CriadoEm;
        var dia = DateOnly.FromDateTime(DateTimeHelper.ToBrasilia(base_));
        return pdtic.VigenciaInicio is DateOnly inicio && inicio > dia ? inicio : dia;
    }

    /// <summary>
    /// Os períodos da periodicidade (meses por ciclo) do período que contém o começo do
    /// acompanhamento até o fim da vigência, cada um cortado pela vigência.
    /// </summary>
    public static List<Periodo> Periodos(DateOnly comeco, DateOnly vigenciaInicio, DateOnly vigenciaFim, int meses)
    {
        var saida = new List<Periodo>();
        if (meses is not (1 or 2 or 3 or 6 or 12)) meses = 3;
        var inicio = comeco > vigenciaInicio ? comeco : vigenciaInicio;
        if (inicio > vigenciaFim) return saida;

        var ano = inicio.Year;
        var indice = (inicio.Month - 1) / meses;
        while (true)
        {
            var periodoInicio = new DateOnly(ano, indice * meses + 1, 1);
            if (periodoInicio > vigenciaFim) break;
            var periodoFim = periodoInicio.AddMonths(meses).AddDays(-1);
            saida.Add(new Periodo(
                periodoInicio < vigenciaInicio ? vigenciaInicio : periodoInicio,
                periodoFim > vigenciaFim ? vigenciaFim : periodoFim,
                Rotulo(ano, indice, meses)));
            indice++;
            if (indice * meses >= 12)
            {
                ano++;
                indice = 0;
            }
        }
        return saida;
    }

    /// <summary>"2027 · 1º trimestre", "2027 · março", "2027 · 2º bimestre", "2027 · 1º semestre" ou "2027".</summary>
    public static string Rotulo(int ano, int indice, int meses)
    {
        var a = ano.ToString(CultureInfo.InvariantCulture);
        var n = (indice + 1).ToString(CultureInfo.InvariantCulture);
        return meses switch
        {
            1 => $"{a} · {Meses[indice]}",
            2 => $"{a} · {n}º bimestre",
            6 => $"{a} · {n}º semestre",
            12 => a,
            _ => $"{a} · {n}º trimestre"
        };
    }

    [GeneratedRegex(@"^(\d{4}) · (\d)º (bimestre|trimestre|semestre)$")]
    private static partial Regex RotuloPorOrdem();

    [GeneratedRegex(@"^(\d{4}) · ([a-zç]+)$")]
    private static partial Regex RotuloPorMes();

    /// <summary>
    /// O rótulo curto do ciclo, para o nome do arquivo do RA: "2027-T1", "2027-03", "2027-B2",
    /// "2027-S1", "2027"; na avaliação, "avaliacao-1"; fora desses formatos, "ciclo-3".
    /// </summary>
    public static string RotuloCurto(PeCiclo ciclo)
    {
        if (ciclo.Tipo == PeDominios.TipoCiclo.Avaliacao) return $"avaliacao-{ciclo.Numero.ToString(CultureInfo.InvariantCulture)}";
        var rotulo = ciclo.Rotulo.Trim();
        var ordem = RotuloPorOrdem().Match(rotulo);
        if (ordem.Success)
        {
            var letra = ordem.Groups[3].Value switch { "bimestre" => "B", "semestre" => "S", _ => "T" };
            return $"{ordem.Groups[1].Value}-{letra}{ordem.Groups[2].Value}";
        }
        var mes = RotuloPorMes().Match(rotulo);
        if (mes.Success && Array.IndexOf(Meses, mes.Groups[2].Value) is var i and >= 0)
            return $"{mes.Groups[1].Value}-{(i + 1).ToString("00", CultureInfo.InvariantCulture)}";
        if (rotulo.Length == 4 && rotulo.All(char.IsAsciiDigit)) return rotulo;
        return $"ciclo-{ciclo.Numero.ToString(CultureInfo.InvariantCulture)}";
    }

    // ── Plano dos ciclos de monitoramento ───────────────────────────────────

    /// <summary>O que fica, o que sai e o que entra para os ciclos de monitoramento baterem com a periodicidade.</summary>
    public sealed record Plano(List<PeCiclo> Manter, List<PeCiclo> Apagar, List<PeCiclo> Criar)
    {
        /// <summary>Os ciclos depois do plano (os que ficam e os novos), na ordem do início.</summary>
        public List<PeCiclo> Resultado() =>
            Manter.Concat(Criar).OrderBy(c => c.Inicio).ThenBy(c => c.Id == 0 ? long.MaxValue : c.Id).ToList();

        public bool MudaAlgo => Apagar.Count > 0 || Criar.Count > 0;
    }

    /// <summary>
    /// O plano dos ciclos de monitoramento de um PDTIC publicado: os períodos esperados (do começo
    /// do acompanhamento ao fim da vigência, pela periodicidade); o ciclo que já existe com o mesmo
    /// período fica; o fechado ou com registro fica sempre (mesmo fora da periodicidade nova); o
    /// outro sai; os períodos que faltam entram, sem a parte que um ciclo que ficou já cobre. Sem
    /// vigência, nada muda.
    /// </summary>
    public static Plano PlanejarMonitoramento(PePdtic pdtic, IReadOnlyList<PeCiclo> existentes, ISet<long> comRegistros,
        int meses, int prazoDias, string autor, DateTime agora)
    {
        var monitoramento = existentes.Where(c => c.Tipo == PeDominios.TipoCiclo.Monitoramento).OrderBy(c => c.Inicio).ThenBy(c => c.Id).ToList();
        if (pdtic.VigenciaInicio is not DateOnly vigenciaInicio || pdtic.VigenciaFim is not DateOnly vigenciaFim)
            return new Plano(monitoramento, new List<PeCiclo>(), new List<PeCiclo>());

        var esperados = Periodos(InicioDoAcompanhamento(pdtic), vigenciaInicio, vigenciaFim, meses);
        var manter = new List<PeCiclo>();
        var apagar = new List<PeCiclo>();
        var casados = new HashSet<Periodo>();
        foreach (var ciclo in monitoramento)
        {
            var igual = esperados.FirstOrDefault(p => p.Inicio == ciclo.Inicio && p.Fim == ciclo.Fim && !casados.Contains(p));
            if (igual != null)
            {
                casados.Add(igual);
                manter.Add(ciclo);
            }
            else if (ciclo.Situacao == PeDominios.SituacaoCiclo.Fechado || comRegistros.Contains(ciclo.Id))
            {
                manter.Add(ciclo);
            }
            else
            {
                apagar.Add(ciclo);
            }
        }

        var cobertos = manter.Where(c => c.Fim != null).Select(c => (c.Inicio, Fim: c.Fim!.Value)).ToList();
        var criar = new List<PeCiclo>();
        foreach (var periodo in esperados.Where(p => !casados.Contains(p)))
            foreach (var (inicio, fim) in Subtrair((periodo.Inicio, periodo.Fim), cobertos))
                criar.Add(new PeCiclo
                {
                    PdticId = pdtic.Id,
                    Tipo = PeDominios.TipoCiclo.Monitoramento,
                    Rotulo = periodo.Rotulo,
                    Inicio = inicio,
                    Fim = fim,
                    Prazo = fim.AddDays(prazoDias),
                    Situacao = PeDominios.SituacaoCiclo.Aberto,
                    CriadoEm = agora,
                    CriadoPor = autor
                });
        return new Plano(manter, apagar, criar);
    }

    /// <summary>O intervalo sem as partes que os outros cobrem (os dias que sobram, em pedaços seguidos).</summary>
    public static List<(DateOnly Inicio, DateOnly Fim)> Subtrair((DateOnly Inicio, DateOnly Fim) intervalo,
        IEnumerable<(DateOnly Inicio, DateOnly Fim)> cobertos)
    {
        var restos = new List<(DateOnly Inicio, DateOnly Fim)> { intervalo };
        foreach (var coberto in cobertos.OrderBy(c => c.Inicio))
        {
            var novos = new List<(DateOnly Inicio, DateOnly Fim)>();
            foreach (var resto in restos)
            {
                if (coberto.Fim < resto.Inicio || coberto.Inicio > resto.Fim)
                {
                    novos.Add(resto);
                    continue;
                }
                if (coberto.Inicio > resto.Inicio) novos.Add((resto.Inicio, coberto.Inicio.AddDays(-1)));
                if (coberto.Fim < resto.Fim) novos.Add((coberto.Fim.AddDays(1), resto.Fim));
            }
            restos = novos;
        }
        return restos;
    }
}
