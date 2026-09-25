using System.Text.Json;
using api.Planejamento;
using Microsoft.EntityFrameworkCore;
using Models;
using Models.Pgia;
using Models.Planejamento;
using service.Interface;

namespace service.Planejamento;

/// <summary>
/// Nível de maturidade e ajustes de cada órgão (pgia_orgao ativo, o mesmo cadastro do
/// PGIA). O nível é escolhido pelo administrador com justificativa, e cada troca fica no
/// pe_modelo_historico (entidade orgao_nivel, entidade_id = id do órgão). Órgão sem nível
/// escolhido usa o padrão (o primeiro nível ativo pela ordem). Os ajustes mudam a
/// situação de um passo, seção ou campo do PDTIC só para o órgão; item travado nunca
/// desliga, nem por ajuste.
/// </summary>
public class PeOrgaoService : IPeOrgaoService
{
    private readonly AppDbContext _context;

    public PeOrgaoService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<PeOrgaoNivelResponse>> ListarAsync(PeOrgaosConsulta consulta)
    {
        var query = _context.PgiaOrgaos.AsNoTracking().Where(o => o.Ativo);
        if (!string.IsNullOrWhiteSpace(consulta.Filtro))
        {
            // ToLower().Contains() vale no Npgsql e no InMemory
            var filtro = consulta.Filtro.Trim().ToLower();
            query = query.Where(o => o.Sigla.ToLower().Contains(filtro) || o.Nome.ToLower().Contains(filtro));
        }
        var orgaos = await query.OrderBy(o => o.Sigla).ThenBy(o => o.Nome).ToListAsync();

        var ids = orgaos.Select(o => o.Id).ToList();
        var escolhidos = await _context.PeOrgaosConfig.AsNoTracking()
            .Where(c => ids.Contains(c.OrgaoId))
            .ToDictionaryAsync(c => c.OrgaoId, c => c.NivelId);
        var ajustes = (await _context.PeOrgaosAjuste.AsNoTracking()
                .Where(a => ids.Contains(a.OrgaoId))
                .Select(a => a.OrgaoId)
                .ToListAsync())
            .GroupBy(a => a)
            .ToDictionary(g => g.Key, g => g.Count());
        var niveis = await NiveisAsync();

        // F3: o nível que o PDTIC de cada órgão alcançou (o vigente ou o da elaboração), no mesmo
        // lote da situação dos passos do painel (poucas consultas para todos os órgãos)
        var base_ = await PeBaseDosPaineis.CarregarAsync(_context, orgaos, comSituacao: true, comInadimplencias: false);
        var alcancados = base_.Orgaos.ToDictionary(r => r.Orgao.Id, r => r.Conformidade?.Linha);

        return orgaos
            .Select(o =>
            {
                var item = Item(o, escolhidos.TryGetValue(o.Id, out var n) ? (long?)n : null, niveis, ajustes.GetValueOrDefault(o.Id));
                var linha = alcancados.GetValueOrDefault(o.Id);
                item.NivelAlcancadoId = linha?.NivelAlcancadoId;
                item.NivelAlcancadoNome = linha?.NivelAlcancadoNome;
                return item;
            })
            .ToList();
    }

    // ── Forma de cada passo no modo livre (F3) ──────────────────────────────

    /// <summary>
    /// Escolhe a forma de um passo para o órgão (o nível cujas seções e campos valem nele) e devolve
    /// o passo a passo do órgão. NivelId nulo, ou o da forma padrão, volta à forma padrão (apaga a
    /// escolha). Só no modo livre (409 PeDetalheRecusado no definido); o passo aparece para o órgão
    /// (404); o nível é uma das formas oferecidas (400); e o passo pode mudar no PDTIC atual do órgão
    /// (a versão em elaboração, senão a vigente), pela regra de quem edita o passo (409
    /// PeDetalheRecusado com a mensagem da situação; sem PDTIC atual, pode). Mudar a forma não apaga
    /// dado: o campo que sai guarda o valor e volta quando a forma volta (decisão 13).
    /// </summary>
    public async Task<PeTrilhaResponse> DefinirDetalheAsync(long orgaoId, long passoId, PePassoDetalheDTO dto, string autor)
    {
        var orgao = await OrgaoAtivoAsync(orgaoId);
        var dados = await PeModeloDados.CarregarAsync(_context);
        dados.ModoNiveis.Exigir();
        if (!dados.ModoNiveis.Livre)
            throw new ApiException(ErrorCode.PeDetalheRecusado, "No modo definido, a forma dos passos segue o nível que o administrador escolheu para o órgão.");

        var semVigente = await PeTrilhaOrgao.SemPeticVigenteAsync(_context);
        var trilha = await PeTrilhaOrgao.DoOrgaoAsync(_context, dados, orgao, semVigente);
        var passo = trilha.Passo(passoId) ?? throw PePdticService.PassoIndisponivel();
        if (dto.NivelId != null && passo.OpcoesDetalhe.All(o => o.NivelId != dto.NivelId))
            throw new ApiException(ErrorCode.PeDadosInvalidos, "Escolha uma das formas deste passo.");

        var atuais = await _context.PePdtics.AsNoTracking()
            .Where(p => p.OrgaoId == orgaoId && !PeDominios.SituacaoPdtic.Encerradas.Contains(p.Situacao))
            .OrderByDescending(p => p.Id)
            .ToListAsync();
        var atual = atuais.FirstOrDefault(p => PeDominios.SituacaoPdtic.DaElaboracao.Contains(p.Situacao)) ?? atuais.FirstOrDefault();
        if (atual != null && PeEdicaoPdtic.Recusa(atual, PeEdicaoPdtic.GrupoDoPasso(trilha, passo), passo.Chave) is string recusa)
            throw new ApiException(ErrorCode.PeDetalheRecusado, recusa);

        // A forma padrão é a primeira oferecida: escolhê-la é voltar ao padrão
        long? nivelId = dto.NivelId != null && dto.NivelId != passo.OpcoesDetalhe.FirstOrDefault()?.NivelId ? dto.NivelId : null;
        var linha = await _context.PeOrgaosPassoDetalhe.FirstOrDefaultAsync(d => d.OrgaoId == orgaoId && d.PassoId == passoId);
        if (nivelId == null)
        {
            if (linha != null) _context.PeOrgaosPassoDetalhe.Remove(linha);
        }
        else if (linha == null)
        {
            _context.PeOrgaosPassoDetalhe.Add(new PeOrgaoPassoDetalhe
            {
                OrgaoId = orgaoId,
                PassoId = passoId,
                NivelId = nivelId.Value,
                AlteradoEm = DateTime.UtcNow,
                AlteradoPor = autor
            });
        }
        else if (linha.NivelId != nivelId)
        {
            linha.NivelId = nivelId.Value;
            linha.AlteradoEm = DateTime.UtcNow;
            linha.AlteradoPor = autor;
        }
        await _context.SaveChangesAsync();

        return PeModeloService.Trilha(await PeTrilhaOrgao.DoOrgaoAsync(_context, dados, orgao, semVigente));
    }

    public async Task<PeOrgaoNivelResponse> DefinirNivelAsync(long orgaoId, PeOrgaoNivelDTO dto, string autor)
    {
        var orgao = await OrgaoAtivoAsync(orgaoId);
        var niveis = await NiveisAsync();
        var justificativa = dto.Justificativa?.Trim();

        // Sem nível: o órgão volta ao nível padrão (F1, achado A21), com a justificativa
        if (dto.NivelId == null)
        {
            if (string.IsNullOrEmpty(justificativa))
                throw new ApiException(ErrorCode.PeJustificativaObrigatoria, "Explique por que o órgão volta ao nível padrão.");
            if (justificativa.Length > 1000)
                throw new ApiException(ErrorCode.PeDadosInvalidos, "A justificativa tem no máximo 1000 caracteres.");
            return await VoltarAoPadraoAsync(orgao, niveis, justificativa, autor);
        }

        var nivel = niveis.FirstOrDefault(n => n.Id == dto.NivelId)
            ?? throw new ApiException(ErrorCode.PeDadosInvalidos, "O nível escolhido não existe. Atualize a tela.");
        if (!nivel.Ativo)
            throw new ApiException(ErrorCode.PeNivelInativo, $"O nível \"{nivel.Nome}\" está desativado. Escolha um nível ativo.");

        if (string.IsNullOrEmpty(justificativa))
            throw new ApiException(ErrorCode.PeJustificativaObrigatoria, "Explique por que o órgão fica neste nível.");
        if (justificativa.Length > 1000)
            throw new ApiException(ErrorCode.PeDadosInvalidos, "A justificativa tem no máximo 1000 caracteres.");

        var config = await _context.PeOrgaosConfig.FirstOrDefaultAsync(c => c.OrgaoId == orgaoId);
        var anterior = (config != null ? niveis.FirstOrDefault(n => n.Id == config.NivelId) : null) ?? niveis.FirstOrDefault(n => n.Ativo);
        if (config != null && config.NivelId == nivel.Id && config.Justificativa == justificativa)
            return Item(orgao, config.NivelId, niveis, await TotalAjustesAsync(orgaoId));

        var antes = new
        {
            NivelId = anterior?.Id,
            NivelNome = anterior?.Nome,
            Padrao = config == null,
            config?.Justificativa
        };

        var agora = DateTime.UtcNow;
        if (config == null)
        {
            config = new PeOrgaoConfig { OrgaoId = orgaoId };
            _context.PeOrgaosConfig.Add(config);
        }
        config.NivelId = nivel.Id;
        config.Justificativa = justificativa;
        config.DefinidoEm = agora;
        config.DefinidoPor = autor;

        _context.PeModeloHistorico.Add(new PeModeloHistorico
        {
            Entidade = PeDominios.EntidadeHistorico.OrgaoNivel,
            EntidadeId = orgaoId,
            Acao = PeDominios.AcaoHistorico.Alteracao,
            Antes = JsonSerializer.Serialize(antes, PeModeloService.JsonHistorico),
            Depois = JsonSerializer.Serialize(new { NivelId = (long?)nivel.Id, NivelNome = nivel.Nome, Padrao = false, Justificativa = justificativa },
                PeModeloService.JsonHistorico),
            AlteradoEm = agora,
            AlteradoPor = autor
        });
        await _context.SaveChangesAsync();

        return Item(orgao, nivel.Id, niveis, await TotalAjustesAsync(orgaoId));
    }

    /// <summary>
    /// O órgão volta ao nível padrão (o primeiro ativo pela ordem): a escolha sai e a troca fica no
    /// histórico com o novo nível marcado como padrão. Órgão que já está no padrão não muda nada.
    /// </summary>
    private async Task<PeOrgaoNivelResponse> VoltarAoPadraoAsync(PgiaOrgao orgao, List<PeNivel> niveis, string justificativa, string autor)
    {
        var config = await _context.PeOrgaosConfig.FirstOrDefaultAsync(c => c.OrgaoId == orgao.Id);
        if (config == null) return Item(orgao, null, niveis, await TotalAjustesAsync(orgao.Id));

        var anterior = niveis.FirstOrDefault(n => n.Id == config.NivelId);
        var padrao = niveis.FirstOrDefault(n => n.Ativo);
        var agora = DateTime.UtcNow;
        _context.PeOrgaosConfig.Remove(config);
        _context.PeModeloHistorico.Add(new PeModeloHistorico
        {
            Entidade = PeDominios.EntidadeHistorico.OrgaoNivel,
            EntidadeId = orgao.Id,
            Acao = PeDominios.AcaoHistorico.Alteracao,
            Antes = JsonSerializer.Serialize(new { NivelId = anterior?.Id, NivelNome = anterior?.Nome, Padrao = false, config.Justificativa },
                PeModeloService.JsonHistorico),
            Depois = JsonSerializer.Serialize(new { NivelId = padrao?.Id, NivelNome = padrao?.Nome, Padrao = true, Justificativa = justificativa },
                PeModeloService.JsonHistorico),
            AlteradoEm = agora,
            AlteradoPor = autor
        });
        await _context.SaveChangesAsync();
        return Item(orgao, null, niveis, await TotalAjustesAsync(orgao.Id));
    }

    public async Task<List<PeOrgaoNivelHistoricoResponse>> HistoricoNivelAsync(long orgaoId)
    {
        if (!await _context.PgiaOrgaos.AnyAsync(o => o.Id == orgaoId))
            throw new ApiException(ErrorCode.PeOrgaoNaoEncontrado, "Órgão não encontrado.");

        var linhas = await _context.PeModeloHistorico.AsNoTracking()
            .Where(h => h.Entidade == PeDominios.EntidadeHistorico.OrgaoNivel && h.EntidadeId == orgaoId)
            .OrderByDescending(h => h.AlteradoEm).ThenByDescending(h => h.Id)
            .ToListAsync();
        var nomes = await PeNomes.CarregarAsync(_context, linhas.Select(h => h.AlteradoPor));

        return linhas.Select(h =>
        {
            var antes = Ler(h.Antes);
            var depois = Ler(h.Depois);
            return new PeOrgaoNivelHistoricoResponse
            {
                NivelAnteriorId = antes?.NivelId,
                NivelAnterior = antes?.NivelNome,
                NivelAnteriorPadrao = antes?.Padrao ?? false,
                NivelNovoId = depois?.NivelId,
                NivelNovo = depois?.NivelNome,
                NivelNovoPadrao = depois?.Padrao ?? false,
                Justificativa = depois?.Justificativa,
                DefinidoEm = h.AlteradoEm,
                DefinidoPor = h.AlteradoPor,
                DefinidoPorNome = nomes.DeObrigatorio(h.AlteradoPor)
            };
        }).ToList();
    }

    public async Task<List<PeOrgaoAjusteResponse>> AjustesAsync(long orgaoId)
    {
        await OrgaoAtivoAsync(orgaoId);
        var ajustes = await _context.PeOrgaosAjuste.AsNoTracking().Where(a => a.OrgaoId == orgaoId).ToListAsync();

        var passos = await _context.PePassos.AsNoTracking().ToDictionaryAsync(p => p.Id);
        var secoes = await _context.PeSecoes.AsNoTracking().ToDictionaryAsync(s => s.Id);
        var campos = await _context.PeCampos.AsNoTracking().ToDictionaryAsync(c => c.Id);
        var nomes = await PeNomes.CarregarAsync(_context, ajustes.Select(a => a.AlteradoPor ?? a.CriadoPor));

        return ajustes
            .OrderBy(a => Array.IndexOf(PeDominios.AlvoAjuste.Todos, a.AlvoTipo)).ThenBy(a => a.AlvoId)
            .Select(a =>
            {
                (string? titulo, bool excluido) = a.AlvoTipo switch
                {
                    PeDominios.AlvoAjuste.Passo => passos.TryGetValue(a.AlvoId, out var p) ? (p.Titulo, p.ExcluidoEm != null) : (null, true),
                    PeDominios.AlvoAjuste.Secao => secoes.TryGetValue(a.AlvoId, out var s) ? (s.Titulo, s.ExcluidoEm != null) : (null, true),
                    _ => campos.TryGetValue(a.AlvoId, out var c) ? (c.Rotulo, c.ExcluidoEm != null) : (null, true)
                };
                return new PeOrgaoAjusteResponse
                {
                    AlvoTipo = a.AlvoTipo,
                    AlvoId = a.AlvoId,
                    Situacao = a.Situacao,
                    Justificativa = a.Justificativa,
                    AlvoTitulo = titulo,
                    AlvoExcluido = excluido,
                    AlteradoEm = a.AlteradoEm ?? a.CriadoEm,
                    AlteradoPor = a.AlteradoPor ?? a.CriadoPor,
                    AlteradoPorNome = nomes.DeObrigatorio(a.AlteradoPor ?? a.CriadoPor)
                };
            })
            .ToList();
    }

    public async Task<List<PeOrgaoAjusteResponse>> DefinirAjustesAsync(long orgaoId, List<PeOrgaoAjusteDTO> ajustes, string autor)
    {
        await OrgaoAtivoAsync(orgaoId);
        if (ajustes == null) throw new ApiException(ErrorCode.PeDadosInvalidos, "Envie a lista de ajustes (vazia para tirar todos).");

        // A lista pedida: situação nula = sem ajuste para o item
        var pedidos = new Dictionary<(string, long), (string? Situacao, string? Justificativa)>();
        foreach (var item in ajustes)
        {
            var tipo = item.AlvoTipo?.Trim() ?? string.Empty;
            if (!PeDominios.AlvoAjuste.Todos.Contains(tipo))
                throw new ApiException(ErrorCode.PeDadosInvalidos, "O tipo do item ajustado é passo, secao ou campo.");
            var situacao = string.IsNullOrWhiteSpace(item.Situacao) ? null : item.Situacao.Trim();
            if (situacao != null && !PeDominios.Situacao.Todas.Contains(situacao))
                throw new ApiException(ErrorCode.PeDadosInvalidos, $"Situação inválida: \"{item.Situacao}\". Use obrigatorio, opcional ou desligado.");
            var justificativa = string.IsNullOrWhiteSpace(item.Justificativa) ? null : item.Justificativa.Trim();
            if (justificativa?.Length > 1000)
                throw new ApiException(ErrorCode.PeDadosInvalidos, "A justificativa do ajuste tem no máximo 1000 caracteres.");
            if (!pedidos.TryAdd((tipo, item.AlvoId), (situacao, justificativa)))
                throw new ApiException(ErrorCode.PeDadosInvalidos, "A lista repete um item. Mande um ajuste por item.");
        }

        await ValidarAlvosAsync(pedidos.Where(p => p.Value.Situacao != null)
            .Select(p => (p.Key.Item1, p.Key.Item2, p.Value.Situacao!)).ToList());

        var agora = DateTime.UtcNow;
        var existentes = await _context.PeOrgaosAjuste.Where(a => a.OrgaoId == orgaoId).ToListAsync();

        foreach (var ajuste in existentes)
        {
            if (pedidos.TryGetValue((ajuste.AlvoTipo, ajuste.AlvoId), out var pedido) && pedido.Situacao != null) continue;
            _context.PeOrgaosAjuste.Remove(ajuste);
            Historico(orgaoId, PeDominios.AcaoHistorico.Remocao, Snap(ajuste), null, autor, agora);
        }

        foreach (var ((tipo, alvoId), (situacao, justificativa)) in pedidos.Where(p => p.Value.Situacao != null))
        {
            var ajuste = existentes.FirstOrDefault(a => a.AlvoTipo == tipo && a.AlvoId == alvoId);
            if (ajuste == null)
            {
                ajuste = new PeOrgaoAjuste
                {
                    OrgaoId = orgaoId,
                    AlvoTipo = tipo,
                    AlvoId = alvoId,
                    Situacao = situacao!,
                    Justificativa = justificativa,
                    CriadoEm = agora,
                    CriadoPor = autor
                };
                _context.PeOrgaosAjuste.Add(ajuste);
                Historico(orgaoId, PeDominios.AcaoHistorico.Criacao, null, Snap(ajuste), autor, agora);
                continue;
            }

            if (ajuste.Situacao == situacao && ajuste.Justificativa == justificativa) continue;
            var antes = Snap(ajuste);
            ajuste.Situacao = situacao!;
            ajuste.Justificativa = justificativa;
            ajuste.AlteradoEm = agora;
            ajuste.AlteradoPor = autor;
            Historico(orgaoId, PeDominios.AcaoHistorico.Alteracao, antes, Snap(ajuste), autor, agora);
        }

        await _context.SaveChangesAsync();
        return await AjustesAsync(orgaoId);
    }

    // ── Apoio ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// O item existe, não foi apagado e é do PDTIC (as seções do PETIC-DF e do DF não
    /// dependem de nível nem de órgão); travado não recebe "desligado".
    /// </summary>
    private async Task ValidarAlvosAsync(List<(string Tipo, long Id, string Situacao)> alvos)
    {
        if (alvos.Count == 0) return;

        var passos = await _context.PePassos.AsNoTracking().ToDictionaryAsync(p => p.Id);
        var secoes = await _context.PeSecoes.AsNoTracking().ToDictionaryAsync(s => s.Id);
        var campos = await _context.PeCampos.AsNoTracking().ToDictionaryAsync(c => c.Id);

        bool SecaoValida(long id) =>
            secoes.TryGetValue(id, out var s) && s.ExcluidoEm == null && s.Escopo == PeDominios.Escopo.Pdtic
            && s.PassoId != null && passos.TryGetValue(s.PassoId.Value, out var p) && p.ExcluidoEm == null;

        foreach (var (tipo, id, situacao) in alvos)
        {
            string? travado = null;
            switch (tipo)
            {
                case PeDominios.AlvoAjuste.Passo:
                    if (!passos.TryGetValue(id, out var passo) || passo.ExcluidoEm != null) throw AlvoInexistente(tipo, id);
                    if (passo.Travado) travado = $"O passo \"{passo.Titulo}\" é um dos nove conteúdos mínimos do PDTIC (art. 12, § 2º) e não pode ser desligado para o órgão.";
                    break;
                case PeDominios.AlvoAjuste.Secao:
                    if (!SecaoValida(id)) throw AlvoInexistente(tipo, id);
                    var secao = secoes[id];
                    if (secao.Travada) travado = $"A seção \"{secao.Titulo}\" é um dos nove conteúdos mínimos do PDTIC (art. 12, § 2º) e não pode ser desligada para o órgão.";
                    break;
                default:
                    if (!campos.TryGetValue(id, out var campo) || campo.ExcluidoEm != null || !SecaoValida(campo.SecaoId))
                        throw AlvoInexistente(tipo, id);
                    if (campo.Travado || (campo.Principal && secoes[campo.SecaoId].Travada))
                        travado = $"O campo \"{campo.Rotulo}\" não pode ser desligado para o órgão: é travado ou é o principal de uma seção travada (art. 12, § 2º).";
                    break;
            }
            if (travado != null && situacao == PeDominios.Situacao.Desligado)
                throw new ApiException(ErrorCode.PeItemTravado, travado);
        }
    }

    private static ApiException AlvoInexistente(string tipo, long id) =>
        new(ErrorCode.PeDadosInvalidos, $"O item ajustado ({tipo} {id}) não existe, foi apagado ou não é do PDTIC.");

    private void Historico(long orgaoId, string acao, object? antes, object? depois, string autor, DateTime quando) =>
        _context.PeModeloHistorico.Add(new PeModeloHistorico
        {
            Entidade = PeDominios.EntidadeHistorico.OrgaoAjuste,
            EntidadeId = orgaoId,
            Acao = acao,
            Antes = antes == null ? null : JsonSerializer.Serialize(antes, PeModeloService.JsonHistorico),
            Depois = depois == null ? null : JsonSerializer.Serialize(depois, PeModeloService.JsonHistorico),
            AlteradoEm = quando,
            AlteradoPor = autor
        });

    private static object Snap(PeOrgaoAjuste a) => new { a.AlvoTipo, a.AlvoId, a.Situacao, a.Justificativa };

    private async Task<PgiaOrgao> OrgaoAtivoAsync(long orgaoId) =>
        await _context.PgiaOrgaos.AsNoTracking().FirstOrDefaultAsync(o => o.Id == orgaoId && o.Ativo)
        ?? throw new ApiException(ErrorCode.PeOrgaoNaoEncontrado, "Órgão não encontrado ou desativado.");

    private Task<List<PeNivel>> NiveisAsync() =>
        _context.PeNiveis.AsNoTracking().OrderBy(n => n.Ordem).ThenBy(n => n.Id).ToListAsync();

    private Task<int> TotalAjustesAsync(long orgaoId) => _context.PeOrgaosAjuste.CountAsync(a => a.OrgaoId == orgaoId);

    private static PeOrgaoNivelResponse Item(PgiaOrgao orgao, long? escolhido, List<PeNivel> niveis, int ajustes)
    {
        var nivel = escolhido != null ? niveis.FirstOrDefault(n => n.Id == escolhido) : null;
        var efetivo = nivel ?? niveis.FirstOrDefault(n => n.Ativo);
        return new PeOrgaoNivelResponse
        {
            OrgaoId = orgao.Id,
            Sigla = orgao.Sigla,
            Nome = orgao.Nome,
            NivelId = efetivo?.Id,
            NivelNome = efetivo?.Nome,
            NivelPadrao = nivel == null,
            NivelAtivo = efetivo?.Ativo ?? false,
            Ajustes = ajustes
        };
    }

    private sealed class NivelGravado
    {
        public long? NivelId { get; set; }
        public string? NivelNome { get; set; }
        public bool Padrao { get; set; }
        public string? Justificativa { get; set; }
    }

    private static NivelGravado? Ler(string? json) =>
        string.IsNullOrWhiteSpace(json) ? null : JsonSerializer.Deserialize<NivelGravado>(json);
}
