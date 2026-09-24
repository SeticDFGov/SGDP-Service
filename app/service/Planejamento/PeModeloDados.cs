using System.Text.Json;
using api.Planejamento;
using Microsoft.EntityFrameworkCore;
using Models;
using Models.Planejamento;

namespace service.Planejamento;

/// <summary>
/// O modelo inteiro lido de uma vez (é pequeno: dezenas de passos, poucas centenas de
/// campos), sem rastreamento, com os índices que o GET modelo, a trilha e as regras
/// usam. Monta as respostas no formato do contrato.
/// </summary>
public sealed class PeModeloDados
{
    public List<PeNivel> Niveis { get; private init; } = new();
    public List<PeEtapa> Etapas { get; private init; } = new();
    public List<PePasso> Passos { get; private init; } = new();
    public List<PeSecao> Secoes { get; private init; } = new();
    public List<PeCampo> Campos { get; private init; } = new();
    public List<PeOpcao> Opcoes { get; private init; } = new();

    // Situação por nível: id do item → id do nível → situação
    public Dictionary<long, Dictionary<long, string>> SituacaoPasso { get; private init; } = new();
    public Dictionary<long, Dictionary<long, string>> SituacaoSecao { get; private init; } = new();
    public Dictionary<long, Dictionary<long, string>> SituacaoCampo { get; private init; } = new();

    // Órgãos por nível escolhido
    public Dictionary<long, int> OrgaosPorNivel { get; private init; } = new();

    public static async Task<PeModeloDados> CarregarAsync(AppDbContext context)
    {
        var passoNivel = await context.PePassosNivel.AsNoTracking().ToListAsync();
        var secaoNivel = await context.PeSecoesNivel.AsNoTracking().ToListAsync();
        var campoNivel = await context.PeCamposNivel.AsNoTracking().ToListAsync();
        var orgaos = await context.PeOrgaosConfig.AsNoTracking()
            .GroupBy(c => c.NivelId)
            .Select(g => new { NivelId = g.Key, Total = g.Count() })
            .ToListAsync();

        return new PeModeloDados
        {
            Niveis = await context.PeNiveis.AsNoTracking().OrderBy(n => n.Ordem).ThenBy(n => n.Id).ToListAsync(),
            Etapas = await context.PeEtapas.AsNoTracking().OrderBy(e => e.Ordem).ThenBy(e => e.Id).ToListAsync(),
            Passos = await context.PePassos.AsNoTracking().OrderBy(p => p.Ordem).ThenBy(p => p.Id).ToListAsync(),
            Secoes = await context.PeSecoes.AsNoTracking().OrderBy(s => s.Ordem).ThenBy(s => s.Id).ToListAsync(),
            Campos = await context.PeCampos.AsNoTracking().OrderBy(c => c.Ordem).ThenBy(c => c.Id).ToListAsync(),
            Opcoes = await context.PeOpcoes.AsNoTracking().OrderBy(o => o.Ordem).ThenBy(o => o.Id).ToListAsync(),
            SituacaoPasso = Agrupar(passoNivel.Select(n => (n.PassoId, n.NivelId, n.Situacao))),
            SituacaoSecao = Agrupar(secaoNivel.Select(n => (n.SecaoId, n.NivelId, n.Situacao))),
            SituacaoCampo = Agrupar(campoNivel.Select(n => (n.CampoId, n.NivelId, n.Situacao))),
            OrgaosPorNivel = orgaos.ToDictionary(o => o.NivelId, o => o.Total)
        };
    }

    private static Dictionary<long, Dictionary<long, string>> Agrupar(IEnumerable<(long Item, long Nivel, string Situacao)> linhas) =>
        linhas.GroupBy(l => l.Item).ToDictionary(g => g.Key, g => g.ToDictionary(l => l.Nivel, l => l.Situacao));

    // ── Índices ─────────────────────────────────────────────────────────────

    public IEnumerable<PePasso> PassosDaEtapa(long etapaId, bool incluirExcluidos) =>
        Passos.Where(p => p.EtapaId == etapaId && (incluirExcluidos || p.ExcluidoEm == null));

    public IEnumerable<PeSecao> SecoesDoPasso(long passoId, bool incluirExcluidos) =>
        Secoes.Where(s => s.PassoId == passoId && (incluirExcluidos || s.ExcluidoEm == null));

    public IEnumerable<PeCampo> CamposDaSecao(long secaoId, bool incluirExcluidos) =>
        Campos.Where(c => c.SecaoId == secaoId && (incluirExcluidos || c.ExcluidoEm == null));

    public IEnumerable<PeOpcao> OpcoesDoCampo(long campoId) => Opcoes.Where(o => o.CampoId == campoId);

    public PeSecao? SecaoPorChave(string chave) => Secoes.FirstOrDefault(s => s.Chave == chave);

    /// <summary>Campo travado ou principal de seção travada: não desliga.</summary>
    public bool CampoTravado(PeCampo campo) =>
        campo.Travado || (campo.Principal && Secoes.FirstOrDefault(s => s.Id == campo.SecaoId)?.Travada == true);

    /// <summary>O primeiro nível ativo pela ordem (o padrão de quem não tem nível escolhido).</summary>
    public PeNivel? NivelPadrao() => Niveis.FirstOrDefault(n => n.Ativo);

    // ── Respostas do GET modelo ─────────────────────────────────────────────

    public PeModeloResponse Modelo(bool incluirExcluidos) => new()
    {
        Niveis = Niveis.Select(Nivel).ToList(),
        Etapas = Etapas.Select(e => Etapa(e, incluirExcluidos)).ToList(),
        SecoesForaDoPdtic = Secoes
            .Where(s => s.Escopo != PeDominios.Escopo.Pdtic && (incluirExcluidos || s.ExcluidoEm == null))
            .OrderBy(s => s.Escopo).ThenBy(s => s.Ordem).ThenBy(s => s.Id)
            .Select(s => Secao(s, incluirExcluidos))
            .ToList()
    };

    public PeNivelResponse Nivel(PeNivel n) => new()
    {
        Id = n.Id,
        Codigo = n.Codigo,
        Nome = n.Nome,
        Descricao = n.Descricao,
        Ordem = n.Ordem,
        Ativo = n.Ativo,
        Orgaos = OrgaosPorNivel.GetValueOrDefault(n.Id)
    };

    public PeEtapaResponse Etapa(PeEtapa e, bool incluirExcluidos) => new()
    {
        Id = e.Id,
        Chave = e.Chave,
        Titulo = e.Titulo,
        Descricao = e.Descricao,
        ReferenciaGuia = e.ReferenciaGuia,
        Ordem = e.Ordem,
        Sistema = e.Sistema,
        Passos = PassosDaEtapa(e.Id, incluirExcluidos).Select(p => Passo(p, incluirExcluidos)).ToList()
    };

    public PePassoResponse Passo(PePasso p, bool incluirExcluidos) => new()
    {
        Id = p.Id,
        EtapaId = p.EtapaId,
        Chave = p.Chave,
        Titulo = p.Titulo,
        OQueFazer = p.OQueFazer,
        BaseLegal = p.BaseLegal,
        ReferenciaGuia = p.ReferenciaGuia,
        Tipo = p.Tipo,
        IncisoDecreto = p.IncisoDecreto,
        Travado = p.Travado,
        AceitaNaoSeAplica = p.AceitaNaoSeAplica,
        Ordem = p.Ordem,
        Sistema = p.Sistema,
        Excluido = p.ExcluidoEm != null,
        Niveis = MapaNiveis(SituacaoPasso.GetValueOrDefault(p.Id)),
        Secoes = SecoesDoPasso(p.Id, incluirExcluidos).Select(s => Secao(s, incluirExcluidos)).ToList()
    };

    public PeSecaoResponse Secao(PeSecao s, bool incluirExcluidos)
    {
        var doPdtic = s.Escopo == PeDominios.Escopo.Pdtic;
        return new PeSecaoResponse
        {
            Id = s.Id,
            PassoId = s.PassoId,
            Chave = s.Chave,
            Escopo = s.Escopo,
            Titulo = s.Titulo,
            Ajuda = s.Ajuda,
            Tipo = s.Tipo,
            PrefixoCodigo = s.PrefixoCodigo,
            Ordem = s.Ordem,
            NoDocumento = s.NoDocumento,
            NaPlanilha = s.NaPlanilha,
            Travada = s.Travada,
            IncisoDecreto = s.IncisoDecreto,
            Sistema = s.Sistema,
            Excluido = s.ExcluidoEm != null,
            SituacaoGeral = doPdtic ? null : s.SituacaoGeral,
            Niveis = doPdtic ? MapaNiveis(SituacaoSecao.GetValueOrDefault(s.Id)) : new Dictionary<string, string>(),
            Campos = CamposDaSecao(s.Id, incluirExcluidos).Select(c => Campo(c, doPdtic)).ToList()
        };
    }

    public PeCampoResponse Campo(PeCampo c, bool doPdtic) => new()
    {
        Id = c.Id,
        SecaoId = c.SecaoId,
        Chave = c.Chave,
        Rotulo = c.Rotulo,
        Ajuda = c.Ajuda,
        Tipo = c.Tipo,
        Config = Json(c.Config),
        Principal = c.Principal,
        Travado = CampoTravado(c),
        Ordem = c.Ordem,
        NoDocumento = c.NoDocumento,
        NaPlanilha = c.NaPlanilha,
        Largura = c.Largura,
        Sistema = c.Sistema,
        Excluido = c.ExcluidoEm != null,
        SituacaoGeral = doPdtic ? null : c.SituacaoGeral,
        Niveis = doPdtic ? MapaNiveis(SituacaoCampo.GetValueOrDefault(c.Id)) : new Dictionary<string, string>(),
        Opcoes = OpcoesDoCampo(c.Id).Select(Opcao).ToList()
    };

    public static PeOpcaoResponse Opcao(PeOpcao o) => new()
    {
        Id = o.Id,
        CampoId = o.CampoId,
        Valor = o.Valor,
        Rotulo = o.Rotulo,
        Ordem = o.Ordem,
        Ativa = o.Ativa,
        Cor = o.Cor,
        Travada = o.Travada,
        Sistema = o.Sistema
    };

    /// <summary>Todos os níveis, com "desligado" onde não há linha.</summary>
    private Dictionary<string, string> MapaNiveis(Dictionary<long, string>? situacoes) =>
        Niveis.ToDictionary(n => n.Id.ToString(),
            n => situacoes != null && situacoes.TryGetValue(n.Id, out var s) ? s : PeDominios.Situacao.Desligado);

    public static JsonElement Json(string? json)
    {
        using var documento = JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? PeConfigCampo.Vazio : json);
        return documento.RootElement.Clone();
    }
}

/// <summary>
/// Resolução da trilha efetiva de um órgão. Para cada passo, seção e campo: o ajuste do
/// órgão, senão a situação no nível do órgão, senão desligado; item travado (e o campo
/// principal de seção travada) nunca desliga: vira obrigatório. A etapa aparece se tiver
/// passo visível; a seção, se o passo aparece; o campo, se a seção aparece (e o campo de
/// ligação, se a seção que ele liga também aparece). Itens apagados não entram. A
/// numeração é pela posição entre os visíveis: etapa N, passo N.M.
/// </summary>
public static class PeTrilhaResolver
{
    public static List<PeTrilhaEtapa> Resolver(PeModeloDados dados, long nivelId,
        IReadOnlyDictionary<(string Tipo, long Id), string> ajustes)
    {
        string Efetiva(string tipo, long id, Dictionary<long, string>? porNivel, bool travado)
        {
            var situacao = ajustes.TryGetValue((tipo, id), out var ajustada)
                ? ajustada
                : porNivel != null && porNivel.TryGetValue(nivelId, out var doNivel) ? doNivel : PeDominios.Situacao.Desligado;
            return travado && situacao == PeDominios.Situacao.Desligado ? PeDominios.Situacao.Obrigatorio : situacao;
        }

        // Primeiro, o que fica visível: passos e seções (as ligações dependem delas)
        var passosVisiveis = new Dictionary<long, string>();
        var secoesVisiveis = new Dictionary<long, string>();
        foreach (var passo in dados.Passos.Where(p => p.ExcluidoEm == null))
        {
            var situacao = Efetiva(PeDominios.AlvoAjuste.Passo, passo.Id, dados.SituacaoPasso.GetValueOrDefault(passo.Id), passo.Travado);
            if (situacao == PeDominios.Situacao.Desligado) continue;
            passosVisiveis[passo.Id] = situacao;

            foreach (var secao in dados.SecoesDoPasso(passo.Id, false).Where(s => s.Escopo == PeDominios.Escopo.Pdtic))
            {
                var sit = Efetiva(PeDominios.AlvoAjuste.Secao, secao.Id, dados.SituacaoSecao.GetValueOrDefault(secao.Id), secao.Travada);
                if (sit != PeDominios.Situacao.Desligado) secoesVisiveis[secao.Id] = sit;
            }
        }
        var chavesVisiveis = dados.Secoes.Where(s => secoesVisiveis.ContainsKey(s.Id)).Select(s => s.Chave).ToHashSet();

        var etapas = new List<PeTrilhaEtapa>();
        foreach (var etapa in dados.Etapas)
        {
            var passos = new List<PeTrilhaPasso>();
            foreach (var passo in dados.PassosDaEtapa(etapa.Id, false).Where(p => passosVisiveis.ContainsKey(p.Id)))
            {
                passos.Add(new PeTrilhaPasso
                {
                    Id = passo.Id,
                    Chave = passo.Chave,
                    Titulo = passo.Titulo,
                    OQueFazer = passo.OQueFazer,
                    BaseLegal = passo.BaseLegal,
                    ReferenciaGuia = passo.ReferenciaGuia,
                    Tipo = passo.Tipo,
                    IncisoDecreto = passo.IncisoDecreto,
                    Travado = passo.Travado,
                    AceitaNaoSeAplica = passo.AceitaNaoSeAplica,
                    Situacao = passosVisiveis[passo.Id],
                    AjustadoParaOrgao = ajustes.ContainsKey((PeDominios.AlvoAjuste.Passo, passo.Id)),
                    Secoes = dados.SecoesDoPasso(passo.Id, false)
                        .Where(s => secoesVisiveis.ContainsKey(s.Id))
                        .Select(s => Secao(dados, s, secoesVisiveis[s.Id], chavesVisiveis, Efetiva))
                        .ToList()
                });
            }
            if (passos.Count == 0) continue;

            var numero = etapas.Count + 1;
            for (var i = 0; i < passos.Count; i++) passos[i].Numero = $"{numero}.{i + 1}";
            etapas.Add(new PeTrilhaEtapa
            {
                Numero = numero,
                Id = etapa.Id,
                Chave = etapa.Chave,
                Titulo = etapa.Titulo,
                Descricao = etapa.Descricao,
                ReferenciaGuia = etapa.ReferenciaGuia,
                Passos = passos
            });
        }
        return etapas;
    }

    private static PeTrilhaSecao Secao(PeModeloDados dados, PeSecao secao, string situacao, ISet<string> chavesVisiveis,
        Func<string, long, Dictionary<long, string>?, bool, string> efetiva)
    {
        var campos = new List<PeTrilhaCampo>();
        foreach (var campo in dados.CamposDaSecao(secao.Id, false))
        {
            var sit = efetiva(PeDominios.AlvoAjuste.Campo, campo.Id, dados.SituacaoCampo.GetValueOrDefault(campo.Id),
                campo.Travado || (campo.Principal && secao.Travada));
            if (sit == PeDominios.Situacao.Desligado) continue;

            // Ligação com uma seção que o órgão não vê: o campo também some
            if (campo.Tipo == PeDominios.TipoCampo.LigacaoSecao)
            {
                var alvo = PeConfigCampo.SecaoDaLigacao(campo.Config);
                if (alvo == null || !chavesVisiveis.Contains(alvo)) continue;
            }

            campos.Add(new PeTrilhaCampo
            {
                Id = campo.Id,
                Chave = campo.Chave,
                Rotulo = campo.Rotulo,
                Ajuda = campo.Ajuda,
                Tipo = campo.Tipo,
                Config = PeModeloDados.Json(campo.Config),
                Obrigatorio = sit == PeDominios.Situacao.Obrigatorio,
                Principal = campo.Principal,
                Largura = campo.Largura,
                Opcoes = dados.OpcoesDoCampo(campo.Id).Where(o => o.Ativa)
                    .Select(o => new PeTrilhaOpcao { Valor = o.Valor, Rotulo = o.Rotulo, Cor = o.Cor })
                    .ToList()
            });
        }

        return new PeTrilhaSecao
        {
            Id = secao.Id,
            Chave = secao.Chave,
            Titulo = secao.Titulo,
            Ajuda = secao.Ajuda,
            Tipo = secao.Tipo,
            PrefixoCodigo = secao.PrefixoCodigo,
            Situacao = situacao,
            Travada = secao.Travada,
            Campos = campos
        };
    }
}
