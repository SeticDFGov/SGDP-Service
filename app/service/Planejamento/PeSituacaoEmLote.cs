using Microsoft.EntityFrameworkCore;
using Models;
using Models.Planejamento;

namespace service.Planejamento;

/// <summary>
/// O que a situação dos passos lê do banco, de vários PDTICs de uma vez (E8): o painel e a
/// conformidade da SGDI passam por todos os órgãos, e ler PDTIC por PDTIC seria uma dezena de
/// consultas para cada um. Aqui são poucas consultas no total, qualquer que seja o número de
/// PDTICs: os registros (todas as seções, também as por ciclo), o ciclo de cada registro, as
/// ligações que saem deles, as marcas de "não se aplica", os comentários abertos, os ciclos
/// gravados, as versões do documento do PDTIC e as deliberações do CGTIC. A situação de um PDTIC
/// só (GET pdtic/{id}/situacao) passa pelo mesmo caminho, com uma lista de um.
/// <para>
/// Só lê: nada aqui grava (os ciclos de monitoramento que faltam entram pelo plano da
/// periodicidade, em memória, como a situação dos passos da E7 já fazia).
/// </para>
/// <para>
/// Regra do deploy: as colunas da rodada B da E7 (o ciclo de cada registro, o documento de cada
/// versão) só são lidas com o acompanhamento ligado (a versão 6 do modelo inicial carregada), como
/// no resto do módulo.
/// </para>
/// </summary>
public sealed class PeLeituraDaSituacao
{
    private readonly ILookup<long, PeRegistro> _registros;
    private readonly Dictionary<long, long> _cicloDoRegistro;
    private readonly ILookup<long, PeVinculo> _vinculos;
    private readonly Dictionary<long, Dictionary<long, PePdticPasso>> _marcas;
    private readonly Dictionary<long, Dictionary<long, int>> _abertos;
    private readonly ILookup<long, PeCiclo> _ciclos;
    private readonly HashSet<long> _comDocumento;
    private readonly ILookup<long, PeDeliberacao> _deliberacoes;
    private readonly HashSet<long> _ciclosComRegistro;

    private PeLeituraDaSituacao(ILookup<long, PeRegistro> registros, Dictionary<long, long> cicloDoRegistro, ILookup<long, PeVinculo> vinculos,
        Dictionary<long, Dictionary<long, PePdticPasso>> marcas, Dictionary<long, Dictionary<long, int>> abertos, ILookup<long, PeCiclo> ciclos,
        HashSet<long> comDocumento, ILookup<long, PeDeliberacao> deliberacoes, bool semPeticVigente)
    {
        _registros = registros;
        _cicloDoRegistro = cicloDoRegistro;
        _vinculos = vinculos;
        _marcas = marcas;
        _abertos = abertos;
        _ciclos = ciclos;
        _comDocumento = comDocumento;
        _deliberacoes = deliberacoes;
        _ciclosComRegistro = cicloDoRegistro.Values.ToHashSet();
        SemPeticVigente = semPeticVigente;
    }

    // Não há versão do PETIC-DF aprovada: a ligação com os catálogos dele fica opcional
    public bool SemPeticVigente { get; }

    /// <summary>
    /// Lê tudo de uma vez para os PDTICs dados. Com acompanhamentoAtivo (a versão 6 do modelo
    /// inicial carregada), também o ciclo de cada registro, os ciclos gravados e o documento de
    /// cada versão (sem ele, nenhum ciclo e toda versão conta como do PDTIC, como na rodada A).
    /// </summary>
    public static async Task<PeLeituraDaSituacao> CarregarAsync(AppDbContext context, IReadOnlyCollection<long> pdticIds, bool acompanhamentoAtivo)
    {
        var ids = pdticIds.Distinct().ToList();
        if (ids.Count == 0)
            return new PeLeituraDaSituacao(Array.Empty<PeRegistro>().ToLookup(r => 0L), new Dictionary<long, long>(),
                Array.Empty<PeVinculo>().ToLookup(v => 0L), new Dictionary<long, Dictionary<long, PePdticPasso>>(),
                new Dictionary<long, Dictionary<long, int>>(), Array.Empty<PeCiclo>().ToLookup(c => 0L), new HashSet<long>(),
                Array.Empty<PeDeliberacao>().ToLookup(d => 0L), !await context.PePetics.AnyAsync(p => p.Situacao == PeDominios.SituacaoPetic.Aprovado));

        // Os registros de todas as seções (na ordem da seção) e as ligações que saem deles
        var registros = await context.PeRegistros.AsNoTracking()
            .Where(r => r.PdticId != null && ids.Contains(r.PdticId.Value))
            .OrderBy(r => r.Ordem).ThenBy(r => r.Id)
            .ToListAsync();
        var vinculos = await (from v in context.PeVinculos.AsNoTracking()
                              join r in context.PeRegistros.AsNoTracking() on v.RegistroOrigemId equals r.Id
                              where r.PdticId != null && ids.Contains(r.PdticId.Value)
                              select v)
            .ToListAsync();

        // O ciclo de cada registro das seções por ciclo e os ciclos gravados (rodada B da E7)
        var cicloDoRegistro = new Dictionary<long, long>();
        var ciclos = new List<PeCiclo>();
        if (acompanhamentoAtivo)
        {
            cicloDoRegistro = await (from c in context.PeRegistrosCiclo.AsNoTracking()
                                     join r in context.PeRegistros.AsNoTracking() on c.Id equals r.Id
                                     where r.PdticId != null && ids.Contains(r.PdticId.Value)
                                     select new { c.Id, c.CicloId })
                .ToDictionaryAsync(c => c.Id, c => c.CicloId);
            ciclos = await context.PeCiclos.AsNoTracking().Where(c => ids.Contains(c.PdticId)).ToListAsync();
        }

        var marcas = (await context.PePdticPassos.AsNoTracking()
                .Where(p => ids.Contains(p.PdticId) && p.NaoSeAplica)
                .ToListAsync())
            .GroupBy(p => p.PdticId)
            .ToDictionary(g => g.Key, g => g.ToDictionary(p => p.PassoId));
        var abertos = (await context.PeComentarios.AsNoTracking()
                .Where(c => ids.Contains(c.PdticId) && c.PaiId == null && c.ResolvidoEm == null)
                .Select(c => new { c.PdticId, c.PassoId })
                .ToListAsync())
            .GroupBy(c => c.PdticId)
            .ToDictionary(g => g.Key, g => g.GroupBy(c => c.PassoId).ToDictionary(p => p.Key, p => p.Count()));

        // As versões do documento do PDTIC (as do RA e do RR não contam para o passo do documento)
        var versoes = context.PeDocVersoes.AsNoTracking().Where(v => ids.Contains(v.PdticId));
        if (acompanhamentoAtivo)
            versoes = versoes.Where(v => !context.PeDocVersoesDocumento.Any(d => d.Id == v.Id && d.DocTipo != PeDominios.TipoDocumento.Pdtic));
        var comDocumento = (await versoes.Select(v => v.PdticId).Distinct().ToListAsync()).ToHashSet();

        var deliberacoes = await context.PeDeliberacoes.AsNoTracking()
            .Where(d => d.ObjetoTipo == PeDominios.ObjetoDeliberacao.Pdtic && ids.Contains(d.ObjetoId))
            .ToListAsync();
        var semPeticVigente = !await context.PePetics.AnyAsync(p => p.Situacao == PeDominios.SituacaoPetic.Aprovado);

        return new PeLeituraDaSituacao(registros.ToLookup(r => r.PdticId!.Value), cicloDoRegistro, vinculos.ToLookup(v => v.RegistroOrigemId),
            marcas, abertos, ciclos.ToLookup(c => c.PdticId), comDocumento,
            deliberacoes.OrderByDescending(d => d.Id).ToLookup(d => d.ObjetoId), semPeticVigente);
    }

    /// <summary>A análise das seções dadas no PDTIC (na seção por ciclo, só os registros do cicloId; sem ele, nenhum).</summary>
    public PeAnaliseDono Analisar(long pdticId, IReadOnlyList<PeSecaoDoDono> secoes, long? cicloId = null) =>
        PeRegistroService.AnalisarLidos(secoes, _registros[pdticId], _cicloDoRegistro, cicloId, _vinculos, SemPeticVigente);

    /// <summary>Todos os registros do PDTIC, de todas as seções (também as por ciclo), na ordem da seção.</summary>
    public IEnumerable<PeRegistro> Registros(long pdticId) => _registros[pdticId];

    /// <summary>O ciclo de um registro de seção por ciclo, ou nulo.</summary>
    public long? CicloDe(long registroId) => _cicloDoRegistro.TryGetValue(registroId, out var ciclo) ? ciclo : null;

    /// <summary>As ligações que saem de um registro.</summary>
    public IEnumerable<PeVinculo> Vinculos(long registroId) => _vinculos[registroId];

    /// <summary>As marcas de "não se aplica" do PDTIC, pelo passo.</summary>
    public IReadOnlyDictionary<long, PePdticPasso> Marcas(long pdticId) =>
        _marcas.TryGetValue(pdticId, out var marcas) ? marcas : new Dictionary<long, PePdticPasso>();

    /// <summary>Quantos comentários principais abertos há em cada passo do PDTIC.</summary>
    public IReadOnlyDictionary<long, int> Abertos(long pdticId) =>
        _abertos.TryGetValue(pdticId, out var abertos) ? abertos : new Dictionary<long, int>();

    /// <summary>Os ciclos gravados do PDTIC.</summary>
    public IReadOnlyList<PeCiclo> CiclosGravados(long pdticId) => _ciclos[pdticId].ToList();

    /// <summary>
    /// Os ciclos que valem para o PDTIC (os gravados e, no vigente, o plano da periodicidade, sem
    /// gravar), com a periodicidade da análise dada (a do passo 4.3, quando o órgão a vê).
    /// </summary>
    public List<PeCiclo> CiclosEfetivos(PePdtic pdtic, PeTrilhaOrgao trilha, PeAnaliseDono analise) =>
        PeCiclosDoPdtic.Efetivos(pdtic, trilha, CiclosGravados(pdtic.Id), _ciclosComRegistro,
            PeCiclosDoPdtic.Periodicidade(trilha, analise.Secao(PeDominios.ChaveAcompanhamento.SecaoPeriodicidade)));

    /// <summary>O PDTIC tem alguma versão gerada do documento dele (o passo do documento).</summary>
    public bool TemDocumento(long pdticId) => _comDocumento.Contains(pdticId);

    /// <summary>As deliberações do CGTIC sobre o PDTIC, da mais nova para a mais antiga.</summary>
    public IEnumerable<PeDeliberacao> Deliberacoes(long pdticId) => _deliberacoes[pdticId];
}
