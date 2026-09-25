using System.Globalization;
using api.Planejamento;
using demanda_service.Helpers;
using Microsoft.EntityFrameworkCore;
using Models;
using Models.Pgia;
using Models.Planejamento;
using service.Interface;

namespace service.Planejamento;

/// <summary>
/// Os painéis da SGDI (E8; plano, seção 12): o painel com os números dos órgãos, a conformidade
/// (só itens de TIC) e a planilha dela, a árvore do PETIC-DF e a página de cada órgão.
/// <list type="bullet">
/// <item>Quem vê: painel, conformidade e árvore, os papéis globais (pe_admin, pe_sgdi, pe_cgtic) e o
/// admin geral; a página de um órgão, também a equipe e a consulta do próprio órgão.</item>
/// <item>O painel e a conformidade leem todos os órgãos ativos de uma vez
/// (<see cref="PeBaseDosPaineis"/>: o modelo uma vez, a trilha de cada órgão resolvida em memória e a
/// situação dos passos de todos os PDTICs pela <see cref="PeLeituraDaSituacao"/>) e não gravam nada:
/// os ciclos de monitoramento que faltam entram só no cálculo.</item>
/// <item>O PDTIC de referência de cada órgão é a versão vigente; sem ela, a da elaboração; sem as
/// duas, a mais recente (encerrada). A conformidade avalia a vigente ou a da elaboração (sem
/// nenhuma das duas, o órgão fica sem PDTIC: 0% e grupo baixa).</item>
/// <item>Nada disso está no caminho de cada requisição: são endpoints do módulo. Sem a tabela da
/// E8 (o intervalo do deploy), o painel, a conformidade e a página do órgão respondem 409 com
/// corpo; a árvore, que não lê a tabela nova, funciona.</item>
/// </list>
/// </summary>
public class PePainelService : IPePainelService
{
    private const string SemProximoPasso = "Sem próximo passo";

    private readonly AppDbContext _context;
    private readonly IPePermissionService _permissoes;
    private readonly IPePdticService _pdtics;
    private readonly IPeAcompanhamentoService _acompanhamento;
    private readonly IPeOrgaoService _orgaos;

    public PePainelService(AppDbContext context, IPePermissionService permissoes, IPePdticService pdtics,
        IPeAcompanhamentoService acompanhamento, IPeOrgaoService orgaos)
    {
        _context = context;
        _permissoes = permissoes;
        _pdtics = pdtics;
        _acompanhamento = acompanhamento;
        _orgaos = orgaos;
    }

    // ── Painel ──────────────────────────────────────────────────────────────

    public async Task<PePainelGeralResponse> PainelAsync(PePainelConsulta consulta, PeUserContext ctx)
    {
        ConferirPaineis(ctx, "O painel é da SGDI, da Secretaria do CGTIC e do administrador do módulo.");
        var base_ = await PeBaseDosPaineis.CarregarAsync(_context, await PeBaseDosPaineis.OrgaosAtivosAsync(_context), comSituacao: true);
        var dados = base_.Dados;

        // Os filtros valem para tudo; fora do domínio (nível que não existe, situação desconhecida), nenhum órgão
        var situacaoFiltro = string.IsNullOrWhiteSpace(consulta.Situacao) ? null : consulta.Situacao.Trim();
        var filtrados = base_.Orgaos
            .Where(r => consulta.NivelId == null || r.Nivel?.Id == consulta.NivelId)
            .Where(r => situacaoFiltro == null || (PeDominios.SituacaoPainel.Todas.Contains(situacaoFiltro) && r.SituacaoNoPainel == situacaoFiltro))
            .ToList();
        var comDados = filtrados.Where(r => r.EmVigor != null && r.Trilha != null && r.Avaliado?.Id == r.EmVigor.Id).ToList();

        var resposta = new PePainelGeralResponse
        {
            GeradoEm = DateTime.UtcNow,
            TotalOrgaos = filtrados.Count,
            PorSituacao = PeDominios.SituacaoPainel.Todas
                .Select(s => new PePainelSituacaoResponse
                {
                    Chave = s,
                    Rotulo = PeDominios.SituacaoPainel.Rotulo(s),
                    Quantidade = filtrados.Count(r => r.SituacaoNoPainel == s)
                })
                .ToList(),
            EmElaboracaoPorEtapa = PorEtapa(dados, filtrados),
            PorNivel = dados.Niveis
                .Select(n => new PePainelNivelResponse { NivelId = n.Id, Nome = n.Nome, Quantidade = filtrados.Count(r => r.Nivel?.Id == n.Id) })
                .Where(n => n.Quantidade > 0 || dados.Niveis.First(x => x.Id == n.NivelId).Ativo)
                .ToList(),
            PorObjetivoPetic = await PorObjetivoAsync(dados, comDados, base_.Leitura!),
            AcoesPorTema = AcoesPorTema(dados, comDados, base_.Leitura!),
            RiscosPorNivel = RiscosPorNivel(dados, comDados, base_.Leitura!),
            ExecucaoUltimoCiclo = ExecucaoUltimoCiclo(dados, comDados, base_.Leitura!),
            Alertas = await AlertasAsync(filtrados),
            Filtros = new PePainelFiltrosResponse
            {
                Niveis = dados.Niveis.Select(n => new PePainelFiltroNivelResponse { Id = n.Id, Nome = n.Nome }).ToList(),
                Situacoes = PeDominios.SituacaoPainel.Todas
                    .Select(s => new PePainelFiltroSituacaoResponse { Chave = s, Rotulo = PeDominios.SituacaoPainel.Rotulo(s) })
                    .ToList()
            }
        };
        return resposta;
    }

    /// <summary>
    /// Os órgãos em elaboração pela etapa do próximo passo (a posição da etapa no modelo, a numeração
    /// do Avançado). As etapas da elaboração (1 a 3) sempre aparecem; as outras, só com órgão; sem
    /// próximo passo, a linha "Sem próximo passo" (etapa 0), só quando há.
    /// </summary>
    private static List<PePainelEtapaResponse> PorEtapa(PeModeloDados dados, IReadOnlyList<PeRetratoDoOrgao> filtrados)
    {
        var contagem = new Dictionary<long, int>();
        var semProximo = 0;
        foreach (var retrato in filtrados.Where(r => r.SituacaoNoPainel == PeDominios.SituacaoPdtic.EmElaboracao))
        {
            var proximo = retrato.Situacao?.Resposta.ProximoPasso;
            var etapa = proximo == null ? null : retrato.Trilha?.Etapas.FirstOrDefault(e => e.Passos.Any(p => p.Numero == proximo));
            if (etapa == null) semProximo++;
            else contagem[etapa.Id] = contagem.GetValueOrDefault(etapa.Id) + 1;
        }

        var etapas = dados.Etapas
            .Select((e, i) => (Etapa: e, Posicao: i + 1))
            .Where(x => PeDominios.EtapaPdtic.Elaboracao.Contains(x.Etapa.Chave) || contagem.ContainsKey(x.Etapa.Id))
            .Select(x => new PePainelEtapaResponse { Etapa = x.Posicao, Titulo = x.Etapa.Titulo, Quantidade = contagem.GetValueOrDefault(x.Etapa.Id) })
            .ToList();
        if (semProximo > 0) etapas.Add(new PePainelEtapaResponse { Etapa = 0, Titulo = SemProximoPasso, Quantidade = semProximo });
        return etapas;
    }

    /// <summary>Os registros de uma seção que o órgão vê no PDTIC em vigor (vazio quando não vê).</summary>
    private static List<PeRegistro> Registros(PeRetratoDoOrgao retrato, PeLeituraDaSituacao leitura, string secaoChave)
    {
        var visivel = retrato.Trilha!.Secao(secaoChave);
        if (visivel == null) return new List<PeRegistro>();
        var id = visivel.Value.Secao.Id;
        return leitura.Registros(retrato.EmVigor!.Id).Where(r => r.SecaoId == id).ToList();
    }

    private static List<PePainelValorResponse> AcoesPorTema(PeModeloDados dados, IReadOnlyList<PeRetratoDoOrgao> orgaos, PeLeituraDaSituacao leitura)
    {
        var campo = CampoDoModelo(dados, PeDominios.TemaDecreto.SecaoAcoes, PeDominios.TemaDecreto.CampoTema);
        if (campo == null) return new List<PePainelValorResponse>();
        var contagem = new Dictionary<string, int>();
        foreach (var retrato in orgaos)
        {
            if (retrato.Trilha!.Campo(PeDominios.TemaDecreto.SecaoAcoes, PeDominios.TemaDecreto.CampoTema) == null) continue;
            foreach (var acao in Registros(retrato, leitura, PeDominios.TemaDecreto.SecaoAcoes))
                foreach (var valor in PeRegistroDados.Textos(PeRegistroDados.Ler(acao.Dados)[PeDominios.TemaDecreto.CampoTema]).Distinct())
                    contagem[valor] = contagem.GetValueOrDefault(valor) + 1;
        }
        return dados.OpcoesDoCampo(campo.Id)
            .Where(o => o.Ativa || contagem.ContainsKey(o.Valor))
            .Select(o => new PePainelValorResponse { Valor = o.Valor, Rotulo = o.Rotulo, Quantidade = contagem.GetValueOrDefault(o.Valor) })
            .ToList();
    }

    private static List<PePainelRiscoResponse> RiscosPorNivel(PeModeloDados dados, IReadOnlyList<PeRetratoDoOrgao> orgaos, PeLeituraDaSituacao leitura)
    {
        const string semNivel = "sem_nivel";
        var contagem = new Dictionary<string, int>();
        foreach (var retrato in orgaos)
        {
            var calculado = retrato.Trilha!.Campo(PeDominios.ChavePdtic.SecaoRiscos, PeDominios.ChaveAcompanhamento.CampoNivelRisco) != null;
            var simples = retrato.Trilha.Campo(PeDominios.ChavePdtic.SecaoRiscos, PeDominios.ChaveAcompanhamento.CampoNivelRiscoSimples) != null;
            foreach (var risco in Registros(retrato, leitura, PeDominios.ChavePdtic.SecaoRiscos))
            {
                var valores = PeRegistroDados.Ler(risco.Dados);
                var nivel = (calculado ? PeRegistroDados.Texto(valores[PeDominios.ChaveAcompanhamento.CampoNivelRisco]) : null)
                            ?? (simples ? PeRegistroDados.Texto(valores[PeDominios.ChaveAcompanhamento.CampoNivelRiscoSimples]) : null);
                var chave = nivel != null && PeDominios.ChaveAcompanhamento.NiveisDoRisco.Contains(nivel) ? nivel : semNivel;
                contagem[chave] = contagem.GetValueOrDefault(chave) + 1;
            }
        }
        var rotulos = Rotulos(dados, PeDominios.ChavePdtic.SecaoRiscos, PeDominios.ChaveAcompanhamento.CampoNivelRisco);
        foreach (var (valor, rotulo) in Rotulos(dados, PeDominios.ChavePdtic.SecaoRiscos, PeDominios.ChaveAcompanhamento.CampoNivelRiscoSimples))
            rotulos.TryAdd(valor, rotulo);
        string Rotulo(string nivel) => rotulos.TryGetValue(nivel, out var r) ? r : nivel switch
        {
            PeDominios.ChaveAcompanhamento.NivelAlto => "Alto",
            PeDominios.ChaveAcompanhamento.NivelMedio => "Médio",
            PeDominios.ChaveAcompanhamento.NivelBaixo => "Baixo",
            _ => nivel
        };
        return PeDominios.ChaveAcompanhamento.NiveisDoRisco.Append(semNivel)
            .Select(n => new PePainelRiscoResponse { Nivel = n, Rotulo = n == semNivel ? "Sem nível" : Rotulo(n), Quantidade = contagem.GetValueOrDefault(n) })
            .ToList();
    }

    /// <summary>
    /// A situação de cada ação dos PDTICs vigentes no último ciclo de monitoramento com dado (a
    /// mesma referência do painel do PDTIC, AC-PDTIC): o último registro da ação até esse ciclo; sem
    /// registro (ou sem ciclo com dado), sem_registro.
    /// </summary>
    private static List<PePainelExecucaoResponse> ExecucaoUltimoCiclo(PeModeloDados dados, IReadOnlyList<PeRetratoDoOrgao> orgaos,
        PeLeituraDaSituacao leitura)
    {
        const string semRegistro = PeDominios.SituacaoFisica.SemRegistro;
        var situacoes = new[]
        {
            PeDominios.ChaveAcompanhamento.AcaoNaoIniciada, PeDominios.ChaveAcompanhamento.AcaoEmAndamento,
            PeDominios.ChaveAcompanhamento.AcaoConcluida, PeDominios.ChaveAcompanhamento.AcaoCancelada
        };
        var contagem = new Dictionary<string, int>();
        foreach (var retrato in orgaos.Where(r => PeDominios.SituacaoPdtic.Vigentes.Contains(r.EmVigor!.Situacao)))
        {
            var acoes = Registros(retrato, leitura, PeDominios.TemaDecreto.SecaoAcoes);
            if (acoes.Count == 0) continue;
            var monitoramento = Registros(retrato, leitura, PeDominios.ChaveAcompanhamento.SecaoMonitoramentoAcoes);
            var ocorridos = Registros(retrato, leitura, PeDominios.ChaveAcompanhamento.SecaoRiscosOcorridos);
            var campoAcao = retrato.Trilha!.Campo(PeDominios.ChaveAcompanhamento.SecaoMonitoramentoAcoes, PeDominios.ChaveAcompanhamento.CampoAcao)?.Id;
            var ciclos = leitura.CiclosGravados(retrato.EmVigor!.Id)
                .Where(c => c.Tipo == PeDominios.TipoCiclo.Monitoramento)
                .OrderBy(c => c.Inicio).ThenBy(c => c.Id)
                .ToList();
            var comDado = monitoramento.Concat(ocorridos)
                .Select(r => leitura.CicloDe(r.Id))
                .Where(c => c != null)
                .Select(c => c!.Value)
                .ToHashSet();
            var referencia = ciclos.LastOrDefault(c => comDado.Contains(c.Id));
            var ate = referencia == null
                ? new List<PeCiclo>()
                : ciclos.Where(c => c.Inicio <= referencia.Inicio).OrderByDescending(c => c.Inicio).ToList();

            // O registro de cada ação em cada ciclo (o primeiro na ordem da seção), num índice só:
            // sem ele, cada ação percorreria os registros de todos os ciclos
            var registroDaAcao = new Dictionary<(long Ciclo, long Acao), PeRegistro>();
            if (campoAcao != null)
                foreach (var r in monitoramento)
                {
                    var ciclo = leitura.CicloDe(r.Id);
                    if (ciclo == null) continue;
                    foreach (var vinculo in leitura.Vinculos(r.Id).Where(v => v.CampoId == campoAcao))
                        registroDaAcao.TryAdd((ciclo.Value, vinculo.RegistroDestinoId), r);
                }

            foreach (var acao in acoes)
            {
                PeRegistro? registro = null;
                foreach (var ciclo in ate)
                    if (registroDaAcao.TryGetValue((ciclo.Id, acao.Id), out registro))
                        break;
                var situacao = registro == null ? null : PeRegistroDados.Texto(PeRegistroDados.Ler(registro.Dados)[PeDominios.ChaveAcompanhamento.CampoSituacao]);
                var chave = situacao != null && situacoes.Contains(situacao) ? situacao : semRegistro;
                contagem[chave] = contagem.GetValueOrDefault(chave) + 1;
            }
        }
        var rotulos = Rotulos(dados, PeDominios.ChaveAcompanhamento.SecaoMonitoramentoAcoes, PeDominios.ChaveAcompanhamento.CampoSituacao);
        string Rotulo(string s) => rotulos.TryGetValue(s, out var r) ? r : s switch
        {
            PeDominios.ChaveAcompanhamento.AcaoNaoIniciada => "Não iniciada",
            PeDominios.ChaveAcompanhamento.AcaoEmAndamento => "Em andamento",
            PeDominios.ChaveAcompanhamento.AcaoConcluida => "Concluída",
            PeDominios.ChaveAcompanhamento.AcaoCancelada => "Cancelada",
            _ => s
        };
        return situacoes.Append(semRegistro)
            .Select(s => new PePainelExecucaoResponse { Situacao = s, Rotulo = s == semRegistro ? "Sem registro" : Rotulo(s), Quantidade = contagem.GetValueOrDefault(s) })
            .ToList();
    }

    private async Task<PePainelAlertasResponse> AlertasAsync(IReadOnlyList<PeRetratoDoOrgao> filtrados)
    {
        // As deliberações aguardando de qualquer versão dos órgãos filtrados (a revisão em aprovação inclusive)
        var pdtics = filtrados.SelectMany(r => r.Pdtics).Select(p => p.Id).ToHashSet();
        var aguardando = pdtics.Count == 0
            ? 0
            : (await _context.PeDeliberacoes.AsNoTracking()
                .Where(d => d.ObjetoTipo == PeDominios.ObjetoDeliberacao.Pdtic && d.Situacao == PeDominios.SituacaoDeliberacao.Aguardando)
                .Select(d => d.ObjetoId)
                .ToListAsync())
            .Count(pdtics.Contains);
        return new PePainelAlertasResponse
        {
            VigenciaVencida = filtrados.Count(r => r.Conformidade?.VigenciaVencida == true),
            RevisaoVencida = filtrados.Count(r => r.Conformidade?.RevisaoVencida == true),
            CicloAtrasado = filtrados.Count(r => r.Conformidade?.CicloAtrasado == true),
            Inadimplentes = filtrados.Count(r => r.Inadimplencias.Any(i => i.Situacao == PeDominios.SituacaoInadimplencia.Inadimplente)),
            DeliberacoesAguardando = aguardando
        };
    }

    // ── Ligações com o PETIC-DF vigente (painel e árvore) ───────────────────

    /// <summary>
    /// Os objetivos do PETIC-DF vigente e o mapa das ligações: a necessidade ou a meta ligada ao
    /// objetivo pelo id; ou, quando a ligação é de uma versão anterior do PETIC-DF (a ligação com
    /// catálogo continua no item da versão da época), pelo código do objetivo, que passa de uma
    /// versão para a outra sem mudar.
    /// </summary>
    private sealed class ObjetivosDaVigente
    {
        public PePetic? Petic { get; init; }

        public List<PeRegistro> Objetivos { get; init; } = new();

        public PeSecao? Secao { get; init; }

        private Dictionary<long, PeRegistro> _porId = new();
        private Dictionary<string, PeRegistro> _porCodigo = new();
        private Dictionary<long, string?> _codigosDeOutrasVersoes = new();

        public void Indexar()
        {
            _porId = Objetivos.ToDictionary(o => o.Id);
            _porCodigo = Objetivos.Where(o => o.Codigo != null).GroupBy(o => o.Codigo!).ToDictionary(g => g.Key, g => g.First());
        }

        public IEnumerable<long> Desconhecidos(IEnumerable<long> destinos) => destinos.Where(d => !_porId.ContainsKey(d)).Distinct();

        public void OutrasVersoes(Dictionary<long, string?> codigos) => _codigosDeOutrasVersoes = codigos;

        public PeRegistro? Objetivo(long destino) =>
            _porId.TryGetValue(destino, out var direto) ? direto
            : _codigosDeOutrasVersoes.TryGetValue(destino, out var codigo) && codigo != null && _porCodigo.TryGetValue(codigo, out var porCodigo) ? porCodigo
            : null;
    }

    private async Task<ObjetivosDaVigente> ObjetivosAsync(PeModeloDados dados)
    {
        var petic = await _context.PePetics.AsNoTracking()
            .Where(p => p.Situacao == PeDominios.SituacaoPetic.Aprovado)
            .OrderByDescending(p => p.Id)
            .FirstOrDefaultAsync();
        var secao = dados.SecaoPorChave(PeDominios.Catalogo.SecaoDoCatalogo[PeDominios.Catalogo.PeticObjetivo]);
        if (petic == null || secao == null) return new ObjetivosDaVigente { Petic = petic, Secao = secao };
        var objetivos = await _context.PeRegistros.AsNoTracking()
            .Where(r => r.PeticId == petic.Id && r.SecaoId == secao.Id)
            .OrderBy(r => r.Ordem).ThenBy(r => r.Id)
            .ToListAsync();
        var resultado = new ObjetivosDaVigente { Petic = petic, Objetivos = objetivos, Secao = secao };
        resultado.Indexar();
        return resultado;
    }

    /// <summary>Os códigos dos objetivos de outras versões do PETIC-DF que as ligações apontam (uma consulta).</summary>
    private async Task CodigosDeOutrasVersoesAsync(ObjetivosDaVigente objetivos, IEnumerable<long> destinos)
    {
        var desconhecidos = objetivos.Desconhecidos(destinos).ToList();
        if (desconhecidos.Count == 0 || objetivos.Secao == null) return;
        var secaoId = objetivos.Secao.Id;
        objetivos.OutrasVersoes(await _context.PeRegistros.AsNoTracking()
            .Where(r => desconhecidos.Contains(r.Id) && r.SecaoId == secaoId)
            .ToDictionaryAsync(r => r.Id, r => r.Codigo));
    }

    /// <summary>
    /// As ligações de uma seção do PDTIC (necessidades ou metas) com os objetivos do PETIC-DF, pelo
    /// campo objetivo_petic, quando o órgão vê a seção e o campo: (registro, id do objetivo ligado).
    /// </summary>
    private static List<(PeRegistro Registro, long Destino)> LigacoesAoPetic(PeRetratoDoOrgao retrato, IEnumerable<PeRegistro> registros,
        Func<long, IEnumerable<PeVinculo>> vinculos, string secaoChave)
    {
        var campo = retrato.Trilha!.Campo(secaoChave, CampoObjetivoPetic);
        if (campo == null) return new List<(PeRegistro, long)>();
        return registros
            .SelectMany(r => vinculos(r.Id).Where(v => v.CampoId == campo.Id).Select(v => (Registro: r, Destino: v.RegistroDestinoId)))
            .ToList();
    }

    private const string CampoObjetivoPetic = "objetivo_petic";

    private async Task<List<PePainelObjetivoResponse>> PorObjetivoAsync(PeModeloDados dados, IReadOnlyList<PeRetratoDoOrgao> orgaos,
        PeLeituraDaSituacao leitura)
    {
        var objetivos = await ObjetivosAsync(dados);
        if (objetivos.Petic == null || objetivos.Objetivos.Count == 0) return new List<PePainelObjetivoResponse>();

        var ligacoes = orgaos.SelectMany(r =>
                LigacoesAoPetic(r, Registros(r, leitura, PeDominios.ChavePdtic.SecaoNecessidades), leitura.Vinculos, PeDominios.ChavePdtic.SecaoNecessidades)
                    .Select(l => (Tipo: "n", l.Registro, l.Destino))
                    .Concat(LigacoesAoPetic(r, Registros(r, leitura, PeDominios.ChaveAcompanhamento.SecaoMetas), leitura.Vinculos,
                            PeDominios.ChaveAcompanhamento.SecaoMetas)
                        .Select(l => (Tipo: "m", l.Registro, l.Destino))))
            .ToList();
        await CodigosDeOutrasVersoesAsync(objetivos, ligacoes.Select(l => l.Destino));

        var porObjetivo = ligacoes
            .Select(l => (l.Tipo, l.Registro.Id, Objetivo: objetivos.Objetivo(l.Destino)))
            .Where(l => l.Objetivo != null)
            .Distinct()
            .ToLookup(l => l.Objetivo!.Id);
        var campoTexto = CampoPrincipal(dados, objetivos.Secao!);
        return objetivos.Objetivos.Select(o => new PePainelObjetivoResponse
        {
            RegistroId = o.Id,
            Codigo = o.Codigo,
            Texto = TextoDo(o, campoTexto),
            Necessidades = porObjetivo[o.Id].Count(l => l.Tipo == "n"),
            Metas = porObjetivo[o.Id].Count(l => l.Tipo == "m")
        }).ToList();
    }

    // ── Conformidade ────────────────────────────────────────────────────────

    public async Task<PeConformidadeResponse> ConformidadeAsync(PeConformidadeConsulta consulta, PeUserContext ctx)
    {
        ConferirPaineis(ctx, "A conformidade é da SGDI, da Secretaria do CGTIC e do administrador do módulo.");
        var base_ = await PeBaseDosPaineis.CarregarAsync(_context, await PeBaseDosPaineis.OrgaosAtivosAsync(_context), comSituacao: true);

        var filtro = string.IsNullOrWhiteSpace(consulta.Filtro) ? null : consulta.Filtro.Trim().ToLowerInvariant();
        var linhas = base_.Orgaos
            .Where(r => consulta.NivelId == null || r.Nivel?.Id == consulta.NivelId)
            .Where(r => filtro == null || r.Orgao.Sigla.ToLowerInvariant().Contains(filtro) || r.Orgao.Nome.ToLowerInvariant().Contains(filtro))
            .Select(r => r.Conformidade!.Linha)
            .ToList();
        // O resumo conta os grupos com o nível e a busca, sem o filtro do grupo
        var resumo = new PeConformidadeResumoResponse
        {
            Alta = linhas.Count(l => l.Grupo == PeDominios.GrupoConformidade.Alta),
            Media = linhas.Count(l => l.Grupo == PeDominios.GrupoConformidade.Media),
            Baixa = linhas.Count(l => l.Grupo == PeDominios.GrupoConformidade.Baixa)
        };
        if (!string.IsNullOrWhiteSpace(consulta.Grupo))
        {
            // Fora do domínio: lista vazia, nunca "todos" em silêncio
            var grupo = consulta.Grupo.Trim();
            linhas = PeDominios.GrupoConformidade.Todos.Contains(grupo) ? linhas.Where(l => l.Grupo == grupo).ToList() : new List<PeConformidadeOrgaoResponse>();
        }

        return new PeConformidadeResponse
        {
            Itens = PeDominios.ItemConformidade.Todos
                .Select(i => new PeConformidadeItemResponse { Chave = i.Chave, Rotulo = i.Rotulo, Base = i.Base, AtendeQuando = i.AtendeQuando })
                .ToList(),
            Resumo = resumo,
            Orgaos = linhas
        };
    }

    public async Task<PePlanilhaArquivo> ConformidadePlanilhaAsync(PeConformidadeConsulta consulta, string? formato, PeUserContext ctx)
    {
        var tipo = PePlanilhaService.Formato(formato, completa: false);
        var conformidade = await ConformidadeAsync(consulta, ctx);
        var nome = $"PDTIC_conformidade_{PePlanilhaService.Hoje()}.{tipo}";
        var itens = PeDominios.ItemConformidade.Todos;

        if (tipo == "csv")
        {
            var csv = new CsvEscritor();
            var cabecalho = new List<string> { "Órgão", "Sigla", "Nível", "Versão do PDTIC", "Situação do PDTIC" };
            cabecalho.AddRange(itens.Select(i => i.Rotulo));
            cabecalho.AddRange(new[] { "Atendidos", "Aplicáveis", "Percentual", "Grupo", "Inadimplência", "Prazo da notificação" });
            csv.Linha(cabecalho);
            foreach (var linha in conformidade.Orgaos)
            {
                // Nome, sigla e nível foram digitados por alguém: protegidos como texto livre
                var celulas = new List<(string, bool)>
                {
                    (linha.Nome, true), (linha.Sigla, true), (linha.NivelNome ?? string.Empty, true), (linha.PdticVersao ?? string.Empty, false),
                    (linha.PdticSituacao == null ? "Sem PDTIC" : PeDominios.SituacaoPdtic.Rotulo(linha.PdticSituacao), false)
                };
                celulas.AddRange(itens.Select(i => (PeConformidadeRegras.Texto(linha.Itens.GetValueOrDefault(i.Chave)?.Atende), false)));
                celulas.Add((linha.Atendidos.ToString(CultureInfo.InvariantCulture), false));
                celulas.Add((linha.Aplicaveis.ToString(CultureInfo.InvariantCulture), false));
                celulas.Add((linha.Percentual.ToString(CultureInfo.InvariantCulture), false));
                celulas.Add((PeDominios.GrupoConformidade.Rotulo(linha.Grupo), false));
                celulas.Add((linha.Inadimplencia == null ? string.Empty : PeDominios.SituacaoInadimplencia.Rotulo(linha.Inadimplencia.Situacao), false));
                celulas.Add((linha.Inadimplencia == null ? string.Empty : PeFormato.Data(linha.Inadimplencia.Prazo), false));
                csv.Linha(celulas);
            }
            return new PePlanilhaArquivo(csv.ParaBytes(), PePlanilhaService.MimeCsv, nome);
        }

        var colunas = new List<PeXlsxColuna>
        {
            new() { Titulo = "Órgão", Largura = 40, Ajuda = "Nome do órgão ou entidade." },
            new() { Titulo = "Sigla", Largura = 10, Ajuda = "Sigla do órgão." },
            new() { Titulo = "Nível", Largura = 16, Ajuda = "Nível de maturidade do órgão hoje." },
            new() { Titulo = "Versão do PDTIC", Largura = 12, Ajuda = "A versão avaliada: a vigente; sem ela, a da elaboração." },
            new() { Titulo = "Situação do PDTIC", Largura = 20, Ajuda = "Situação da versão avaliada; sem PDTIC em vigor nem em andamento, \"Sem PDTIC\"." }
        };
        colunas.AddRange(itens.Select(i => new PeXlsxColuna
        {
            Titulo = i.Rotulo,
            Largura = 18,
            Ajuda = $"{i.AtendeQuando} Base: {i.Base}. Sim, Não ou Não se aplica (fica fora da conta)."
        }));
        colunas.AddRange(new[]
        {
            new PeXlsxColuna { Titulo = "Atendidos", Tipo = PeXlsxTipo.Numero, Casas = 0, Largura = 11, Ajuda = "Itens atendidos." },
            new PeXlsxColuna { Titulo = "Aplicáveis", Tipo = PeXlsxTipo.Numero, Casas = 0, Largura = 11, Ajuda = "Itens que se aplicam ao órgão (os que não se aplicam saem da conta)." },
            new PeXlsxColuna { Titulo = "Percentual", Tipo = PeXlsxTipo.Percentual, Largura = 11, Ajuda = "Atendidos sobre aplicáveis, arredondado." },
            new PeXlsxColuna { Titulo = "Grupo", Largura = 10, Ajuda = "Alta a partir de 70%, média de 25% a 69% e baixa abaixo de 25%." },
            new PeXlsxColuna { Titulo = "Inadimplência", Largura = 16, Ajuda = "A inadimplência vigente do art. 11 do Decreto nº 48.899/2026: notificado ou inadimplente." },
            new PeXlsxColuna { Titulo = "Prazo da notificação", Tipo = PeXlsxTipo.Data, Largura = 14, Ajuda = "O último dia para regularizar ou justificar (5 dias úteis depois da notificação)." }
        });
        var linhas = conformidade.Orgaos.Select(linha =>
        {
            var celulas = new List<object?>
            {
                linha.Nome, linha.Sigla, linha.NivelNome, linha.PdticVersao,
                linha.PdticSituacao == null ? "Sem PDTIC" : PeDominios.SituacaoPdtic.Rotulo(linha.PdticSituacao)
            };
            celulas.AddRange(itens.Select(i => (object?)PeConformidadeRegras.Texto(linha.Itens.GetValueOrDefault(i.Chave)?.Atende)));
            celulas.Add((decimal)linha.Atendidos);
            celulas.Add((decimal)linha.Aplicaveis);
            celulas.Add((decimal)linha.Percentual);
            celulas.Add(PeDominios.GrupoConformidade.Rotulo(linha.Grupo));
            celulas.Add(linha.Inadimplencia == null ? null : PeDominios.SituacaoInadimplencia.Rotulo(linha.Inadimplencia.Situacao));
            celulas.Add(linha.Inadimplencia?.Prazo);
            return celulas.ToArray();
        }).ToList();
        var leiaMe = new List<(string, string)>
        {
            ("Conteúdo", "Conformidade dos PDTICs dos órgãos com os itens de TIC acompanhados pela SGDI"),
            ("Órgãos", conformidade.Orgaos.Count == 1 ? "1 órgão" : $"{conformidade.Orgaos.Count} órgãos"),
            ("Resumo", $"Alta: {conformidade.Resumo.Alta} · Média: {conformidade.Resumo.Media} · Baixa: {conformidade.Resumo.Baixa}"),
            ("Extraído em", PePlanilhaService.Agora()),
            ("Como ler", "Cada linha é um órgão, avaliado pelo PDTIC vigente (sem ele, pelo da elaboração). Cada item mostra Sim, Não ou "
                         + "Não se aplica; o que não se aplica fica fora da conta. O percentual é atendidos sobre aplicáveis.")
        };
        var aba = new PeXlsxAba { Nome = "Conformidade", Colunas = colunas, Linhas = linhas };
        return new PePlanilhaArquivo(PeXlsx.Gerar(new[] { aba }, leiaMe), PePlanilhaService.MimeXlsx, nome);
    }

    // ── Árvore do PETIC-DF ──────────────────────────────────────────────────

    public async Task<PeArvorePeticResponse> ArvoreAsync(long? orgaoId, PeUserContext ctx)
    {
        ConferirPaineis(ctx, "A árvore do PETIC-DF com os órgãos é da SGDI, da Secretaria do CGTIC e do administrador do módulo.");
        var orgaos = await PeBaseDosPaineis.OrgaosAtivosAsync(_context);
        if (orgaoId != null)
        {
            orgaos = orgaos.Where(o => o.Id == orgaoId).ToList();
            if (orgaos.Count == 0) throw new ApiException(ErrorCode.PeOrgaoNaoEncontrado, "Órgão não encontrado ou desativado.");
        }
        var base_ = await PeBaseDosPaineis.CarregarAsync(_context, orgaos, comSituacao: false, comInadimplencias: false);
        var dados = base_.Dados;
        var objetivos = await ObjetivosAsync(dados);
        if (objetivos.Petic == null) return new PeArvorePeticResponse();

        // Os registros das necessidades e das metas dos PDTICs em vigor e as ligações deles (poucas consultas para todos)
        var comPdtic = base_.Orgaos.Where(r => r.EmVigor != null && r.Trilha != null).ToList();
        var secoes = new[] { PeDominios.ChavePdtic.SecaoNecessidades, PeDominios.ChaveAcompanhamento.SecaoMetas }
            .Select(dados.SecaoPorChave)
            .Where(s => s != null)
            .Select(s => s!.Id)
            .ToList();
        var pdticIds = comPdtic.Select(r => r.EmVigor!.Id).ToList();
        var registros = pdticIds.Count == 0 || secoes.Count == 0
            ? new List<PeRegistro>()
            : await _context.PeRegistros.AsNoTracking()
                .Where(r => r.PdticId != null && pdticIds.Contains(r.PdticId.Value) && secoes.Contains(r.SecaoId))
                .OrderBy(r => r.Ordem).ThenBy(r => r.Id)
                .ToListAsync();
        var camposObjetivo = dados.Campos.Where(c => c.Chave == CampoObjetivoPetic && secoes.Contains(c.SecaoId)).Select(c => c.Id).ToList();
        var vinculos = (await (from v in _context.PeVinculos.AsNoTracking()
                               join r in _context.PeRegistros.AsNoTracking() on v.RegistroOrigemId equals r.Id
                               where r.PdticId != null && pdticIds.Contains(r.PdticId.Value) && camposObjetivo.Contains(v.CampoId)
                               select v)
                .ToListAsync())
            .ToLookup(v => v.RegistroOrigemId);
        var porPdtic = registros.ToLookup(r => r.PdticId!.Value);

        var ligacoes = comPdtic.Select(r =>
        {
            List<PeRegistro> DaSecao(string chave) =>
                r.Trilha!.Secao(chave) is { } visivel ? porPdtic[r.EmVigor!.Id].Where(x => x.SecaoId == visivel.Secao.Id).ToList() : new List<PeRegistro>();
            return (Retrato: r,
                Necessidades: LigacoesAoPetic(r, DaSecao(PeDominios.ChavePdtic.SecaoNecessidades), id => vinculos[id], PeDominios.ChavePdtic.SecaoNecessidades),
                Metas: LigacoesAoPetic(r, DaSecao(PeDominios.ChaveAcompanhamento.SecaoMetas), id => vinculos[id], PeDominios.ChaveAcompanhamento.SecaoMetas));
        }).ToList();
        await CodigosDeOutrasVersoesAsync(objetivos, ligacoes.SelectMany(l => l.Necessidades.Concat(l.Metas)).Select(l => l.Destino));

        // Os indicadores do próprio PETIC-DF, pelo objetivo ligado
        var indicadores = await IndicadoresAsync(dados, objetivos.Petic);
        var campoTexto = CampoPrincipal(dados, objetivos.Secao!);
        var necessidade = Campos(dados, PeDominios.ChavePdtic.SecaoNecessidades);
        var meta = Campos(dados, PeDominios.ChaveAcompanhamento.SecaoMetas);
        var situacoesDaMeta = Rotulos(dados, PeDominios.ChaveAcompanhamento.SecaoMetas, PeDominios.ChaveAcompanhamento.CampoSituacao);

        return new PeArvorePeticResponse
        {
            Petic = new PeArvorePeticVersaoResponse { Id = objetivos.Petic.Id, Versao = objetivos.Petic.Versao, Titulo = objetivos.Petic.Titulo },
            Objetivos = objetivos.Objetivos.Select(o =>
            {
                var doObjetivo = ligacoes.Select(l =>
                    {
                        var trilha = l.Retrato.Trilha!;
                        var necessidades = l.Necessidades.Where(n => objetivos.Objetivo(n.Destino)?.Id == o.Id).Select(n => n.Registro).DistinctBy(n => n.Id).ToList();
                        var metas = l.Metas.Where(m => objetivos.Objetivo(m.Destino)?.Id == o.Id).Select(m => m.Registro).DistinctBy(m => m.Id).ToList();
                        var priorizada = trilha.Campo(PeDominios.ChavePdtic.SecaoNecessidades, CampoPriorizada) != null;
                        var situacao = trilha.Campo(PeDominios.ChaveAcompanhamento.SecaoMetas, PeDominios.ChaveAcompanhamento.CampoSituacao) != null;
                        return new PeArvoreOrgaoResponse
                        {
                            OrgaoId = l.Retrato.Orgao.Id,
                            Sigla = l.Retrato.Orgao.Sigla,
                            PdticId = l.Retrato.EmVigor!.Id,
                            Necessidades = necessidades.Select(n =>
                            {
                                var valores = PeRegistroDados.Ler(n.Dados);
                                return new PeArvoreNecessidadeResponse
                                {
                                    Codigo = n.Codigo,
                                    Descricao = PeRegistroDados.Texto(valores[necessidade.Descricao]) ?? string.Empty,
                                    Priorizada = priorizada && valores[CampoPriorizada] is System.Text.Json.Nodes.JsonValue v && v.TryGetValue<bool>(out var b) ? b : null
                                };
                            }).ToList(),
                            Metas = metas.Select(m =>
                            {
                                var valores = PeRegistroDados.Ler(m.Dados);
                                var valorSituacao = situacao ? PeRegistroDados.Texto(valores[PeDominios.ChaveAcompanhamento.CampoSituacao]) : null;
                                return new PeArvoreMetaResponse
                                {
                                    Codigo = m.Codigo,
                                    Descricao = PeRegistroDados.Texto(valores[meta.Descricao]) ?? string.Empty,
                                    Indicador = PeRegistroDados.Texto(valores[PeDominios.ChaveAcompanhamento.CampoIndicador]),
                                    Valor = PeRegistroDados.Texto(valores[PeDominios.ChaveAcompanhamento.CampoValorDaMeta]),
                                    Prazo = PeValores.DataGuardada(valores[PeDominios.ChaveAcompanhamento.CampoPrazo]),
                                    Situacao = valorSituacao == null ? null : situacoesDaMeta.GetValueOrDefault(valorSituacao, valorSituacao)
                                };
                            }).ToList()
                        };
                    })
                    .Where(x => x.Necessidades.Count > 0 || x.Metas.Count > 0)
                    .ToList();
                return new PeArvoreObjetivoResponse
                {
                    RegistroId = o.Id,
                    Codigo = o.Codigo,
                    Texto = TextoDo(o, campoTexto),
                    Indicadores = indicadores[o.Id].ToList(),
                    TotalNecessidades = doObjetivo.Sum(x => x.Necessidades.Count),
                    TotalMetas = doObjetivo.Sum(x => x.Metas.Count),
                    Orgaos = doObjetivo
                };
            }).ToList()
        };
    }

    private const string CampoPriorizada = "priorizada";

    /// <summary>O campo principal (a descrição) de uma seção do PDTIC, pela chave do modelo.</summary>
    private static (string Descricao, long SecaoId) Campos(PeModeloDados dados, string secaoChave)
    {
        var secao = dados.SecaoPorChave(secaoChave);
        var principal = secao == null ? null : dados.CamposDaSecao(secao.Id, incluirExcluidos: true).FirstOrDefault(c => c.Principal);
        return (principal?.Chave ?? PeDominios.ChaveAcompanhamento.CampoDescricao, secao?.Id ?? 0);
    }

    /// <summary>Os indicadores do PETIC-DF vigente, pelo objetivo ligado (campo objetivo de petic_indicador).</summary>
    private async Task<ILookup<long, PeArvoreIndicadorResponse>> IndicadoresAsync(PeModeloDados dados, PePetic petic)
    {
        var secao = dados.SecaoPorChave(SecaoIndicadorPetic);
        if (secao == null) return Array.Empty<(long, PeArvoreIndicadorResponse)>().ToLookup(x => x.Item1, x => x.Item2);
        var campos = dados.CamposDaSecao(secao.Id, incluirExcluidos: true).ToList();
        var campoObjetivo = campos.FirstOrDefault(c => c.Chave == "objetivo");
        var campoNome = campos.FirstOrDefault(c => c.Principal)?.Chave ?? "nome";
        var registros = await _context.PeRegistros.AsNoTracking()
            .Where(r => r.PeticId == petic.Id && r.SecaoId == secao.Id)
            .OrderBy(r => r.Ordem).ThenBy(r => r.Id)
            .ToListAsync();
        var ids = registros.Select(r => r.Id).ToList();
        var ligacoes = campoObjetivo == null || ids.Count == 0
            ? new List<PeVinculo>()
            : await _context.PeVinculos.AsNoTracking().Where(v => ids.Contains(v.RegistroOrigemId) && v.CampoId == campoObjetivo.Id).ToListAsync();
        var porRegistro = ligacoes.ToLookup(v => v.RegistroOrigemId);
        return registros
            .SelectMany(r =>
            {
                var valores = PeRegistroDados.Ler(r.Dados);
                var indicador = new PeArvoreIndicadorResponse
                {
                    Codigo = r.Codigo,
                    Nome = PeRegistroDados.Texto(valores[campoNome]) ?? string.Empty,
                    Meta = MetaDoIndicador(valores)
                };
                return porRegistro[r.Id].Select(v => (Objetivo: v.RegistroDestinoId, Indicador: indicador));
            })
            .ToLookup(x => x.Objetivo, x => x.Indicador);
    }

    private const string SecaoIndicadorPetic = "petic_indicador";

    /// <summary>A meta do indicador do PETIC-DF em texto: "80 % até 31/12/2027" (o número, a unidade e o prazo).</summary>
    internal static string? MetaDoIndicador(System.Text.Json.Nodes.JsonObject valores)
    {
        var numero = PeRegistroDados.Numero(valores["meta"]);
        if (numero == null) return null;
        var unidade = PeRegistroDados.Texto(valores["unidade"])?.Trim();
        var prazo = PeValores.DataGuardada(valores["prazo"]);
        return PeFormato.Numero(numero.Value, null)
               + (string.IsNullOrEmpty(unidade) ? string.Empty : " " + unidade)
               + (prazo is DateOnly data ? " até " + PeFormato.Data(data) : string.Empty);
    }

    // ── Página do órgão ─────────────────────────────────────────────────────

    public async Task<PeOrgaoResumoResponse> ResumoAsync(long orgaoId, PeUserContext ctx)
    {
        if (!_permissoes.PodeLerReferenciais(ctx))
            throw new ApiException(ErrorCode.PeSemPermissao, "Você ainda não tem papel na Governança Estratégica. Fale com o administrador do módulo.");
        if (!_permissoes.PodeVerOrgao(ctx, orgaoId))
            throw new ApiException(ErrorCode.PeSemPermissao, "Você só vê a página do seu próprio órgão.");
        var orgao = await _context.PgiaOrgaos.AsNoTracking().FirstOrDefaultAsync(o => o.Id == orgaoId)
            ?? throw new ApiException(ErrorCode.PeOrgaoNaoEncontrado, "Órgão não encontrado.");

        // As inadimplências primeiro: sem a tabela da E8 (intervalo do deploy), a página responde 409
        // antes de ler ou gravar qualquer outra coisa
        var inadimplencias = await PeInadimplenciaService.Ordenar(_context.PeInadimplencias.AsNoTracking().Where(i => i.OrgaoId == orgaoId))
            .ToListAsync();
        var base_ = await PeBaseDosPaineis.CarregarAsync(_context, new[] { orgao }, comSituacao: true, daReferencia: true);
        var retrato = base_.Orgaos[0];
        var leitura = base_.Leitura!;
        var pdtic = retrato.Referencia;
        var trilha = retrato.Trilha;
        var situacao = retrato.Situacao;
        var hoje = base_.Hoje;

        var versoes = await _pdtics.VersoesAsync(orgaoId, ctx);
        var historico = await _orgaos.HistoricoNivelAsync(orgaoId);
        var nomesInadimplencias = await PeInadimplenciaService.NomesAsync(_context, inadimplencias);

        var resposta = new PeOrgaoResumoResponse
        {
            Orgao = new PeOrgaoResumoOrgaoResponse { Id = orgao.Id, Sigla = orgao.Sigla, Nome = orgao.Nome },
            Nivel = new PeOrgaoResumoNivelResponse
            {
                Id = retrato.Nivel?.Id,
                Nome = retrato.Nivel?.Nome,
                Padrao = retrato.NivelPadrao,
                Historico = historico.Select(h => new PeOrgaoNivelTrocaResponse
                {
                    NivelNome = h.NivelNovo,
                    Padrao = h.NivelNovoPadrao,
                    NivelAnterior = h.NivelAnterior,
                    NivelAnteriorPadrao = h.NivelAnteriorPadrao,
                    Justificativa = h.Justificativa,
                    AlteradoEm = h.DefinidoEm,
                    AlteradoPor = h.DefinidoPor,
                    AlteradoPorNome = h.DefinidoPorNome
                }).ToList()
            },
            Pdtic = pdtic == null ? null : versoes.FirstOrDefault(v => v.Id == pdtic.Id),
            Versoes = versoes,
            ProximoPasso = situacao?.Resposta.ProximoPasso,
            Conformidade = retrato.Conformidade!.Linha,
            Inadimplencias = inadimplencias.Select(i => PeInadimplenciaService.Resposta(i, orgao, hoje, nomesInadimplencias)).ToList()
        };

        if (pdtic != null && trilha != null && situacao != null)
        {
            var porId = situacao.Resposta.Passos.ToDictionary(p => p.PassoId);
            resposta.Andamento = trilha.Etapas.Select(e =>
            {
                var passos = e.Passos.Select(p => porId.GetValueOrDefault(p.Id)).Where(p => p != null).Select(p => p!).ToList();
                return new PeOrgaoAndamentoResponse
                {
                    Etapa = e.Numero,
                    Titulo = e.Titulo,
                    Feitos = passos.Count(p => p.Situacao is PeDominios.SituacaoPasso.Feito or PeDominios.SituacaoPasso.Externo
                        or PeDominios.SituacaoPasso.NaoSeAplica),
                    Total = passos.Count,
                    Atrasados = passos.Count(p => p.Situacao == PeDominios.SituacaoPasso.Atrasado),
                    Aguardando = passos.Count(p => p.Situacao == PeDominios.SituacaoPasso.Aguardando)
                };
            }).ToList();
            resposta.NaoSeAplica = situacao.Resposta.Passos
                .Where(p => p.NaoSeAplica != null)
                .Select(p => new PeOrgaoNaoSeAplicaResponse
                {
                    PassoNumero = p.Numero,
                    PassoTitulo = trilha.Passo(p.PassoId)?.Titulo ?? string.Empty,
                    Justificativa = p.NaoSeAplica!.Justificativa,
                    MarcadoEm = p.NaoSeAplica.MarcadoEm,
                    MarcadoPor = p.NaoSeAplica.MarcadoPor,
                    MarcadoPorNome = p.NaoSeAplica.MarcadoPorNome
                })
                .ToList();
            resposta.ComentariosAbertos = (await _context.PeComentarios.AsNoTracking()
                    .Where(c => c.PdticId == pdtic.Id && c.PaiId == null && c.ResolvidoEm == null)
                    .OrderBy(c => c.CriadoEm).ThenBy(c => c.Id)
                    .ToListAsync())
                .Select(c => new PeOrgaoComentarioAbertoResponse
                {
                    Id = c.Id,
                    PassoId = c.PassoId,
                    PassoNumero = trilha.Passo(c.PassoId)?.Numero,
                    Texto = c.Texto,
                    AutorNome = c.AutorNome,
                    CriadoEm = c.CriadoEm
                })
                .ToList();
            resposta.Aprovacoes = Aprovacoes(base_.Dados, pdtic, trilha, leitura);
        }

        var idsDasVersoes = retrato.Pdtics.Select(p => p.Id).ToList();
        if (idsDasVersoes.Count > 0)
        {
            var deliberacoes = await _context.PeDeliberacoes.AsNoTracking()
                .Where(d => d.ObjetoTipo == PeDominios.ObjetoDeliberacao.Pdtic && idsDasVersoes.Contains(d.ObjetoId))
                .OrderByDescending(d => d.Id)
                .ToListAsync();
            resposta.Deliberacoes = await PeDeliberacaoService.RespostasAsync(_context, deliberacoes);
            resposta.Documentos = await DocumentosAsync(retrato.Pdtics, base_.Dados.Acompanhamento.Ativo);
        }

        // Os ciclos do PDTIC vigente (ou do encerrado): a lista da E7, que cria os ciclos de
        // monitoramento que faltam, como a tela do acompanhamento do órgão
        if (pdtic != null && base_.Dados.Acompanhamento.Ativo && !PeDominios.SituacaoPdtic.DaElaboracao.Contains(pdtic.Situacao))
            resposta.Ciclos = (await _acompanhamento.ListarCiclosAsync(pdtic.Id, null, ctx))
                .Select(c => new PeOrgaoCicloResponse
                {
                    Id = c.Id,
                    Tipo = c.Tipo,
                    Numero = c.Numero,
                    Rotulo = c.Rotulo,
                    Inicio = c.Inicio,
                    Fim = c.Fim,
                    Prazo = c.Prazo,
                    Situacao = c.Situacao,
                    FechadoEm = c.FechadoEm,
                    FechadoPor = c.FechadoPor,
                    FechadoPorNome = c.FechadoPorNome,
                    Relatorio = c.Relatorio,
                    ReabertoEm = c.ReabertoEm,
                    ReabertoPor = c.ReabertoPor,
                    ReabertoPorNome = c.ReabertoPorNome,
                    PodeEditar = c.PodeEditar,
                    PdticId = pdtic.Id
                })
                .ToList();
        return resposta;
    }

    /// <summary>
    /// As decisões registradas nos passos de aprovação e na aprovação do SGTIC (no passo do envio),
    /// na ordem da trilha; na seção por ciclo (a avaliação do comitê), uma por ciclo, com o rótulo dele.
    /// </summary>
    private static List<PeOrgaoAprovacaoResponse> Aprovacoes(PeModeloDados dados, PePdtic pdtic, PeTrilhaOrgao trilha, PeLeituraDaSituacao leitura)
    {
        var registros = leitura.Registros(pdtic.Id).ToList();
        var ciclos = leitura.CiclosGravados(pdtic.Id).ToDictionary(c => c.Id);
        var saida = new List<PeOrgaoAprovacaoResponse>();
        foreach (var passo in trilha.Passos.Where(p => p.Tipo is PeDominios.TipoPasso.Aprovacao or PeDominios.TipoPasso.Envio))
            foreach (var secao in passo.Secoes.Where(s => s.Campos.Any(c => c.Chave == PeDominios.ChavePdtic.CampoDecisao)))
            {
                var decisoes = Rotulos(dados, secao.Id, PeDominios.ChavePdtic.CampoDecisao);
                var atos = Rotulos(dados, secao.Id, PeDominios.ChavePdtic.CampoAtoTipo);
                var doSecao = registros.Where(r => r.SecaoId == secao.Id)
                    .Select(r => (Registro: r, Ciclo: leitura.CicloDe(r.Id) is long c ? ciclos.GetValueOrDefault(c) : null))
                    .Where(x => secao.PorCiclo == null || x.Ciclo != null)
                    .OrderBy(x => x.Ciclo?.Inicio ?? DateOnly.MinValue).ThenBy(x => x.Ciclo?.Numero ?? 0).ThenBy(x => x.Registro.Ordem);
                foreach (var (registro, ciclo) in doSecao)
                {
                    var valores = PeRegistroDados.Ler(registro.Dados);
                    var decisao = PeRegistroDados.Texto(valores[PeDominios.ChavePdtic.CampoDecisao]);
                    if (string.IsNullOrWhiteSpace(decisao)) continue;
                    var atoTipo = PeRegistroDados.Texto(valores[PeDominios.ChavePdtic.CampoAtoTipo]);
                    saida.Add(new PeOrgaoAprovacaoResponse
                    {
                        PassoNumero = passo.Numero,
                        Rotulo = ciclo == null ? secao.Titulo : $"{secao.Titulo} · {ciclo.Rotulo}",
                        Decisao = decisoes.GetValueOrDefault(decisao, decisao),
                        DecisaoValor = decisao,
                        Data = PeValores.DataGuardada(valores[PeDominios.ChavePdtic.CampoData]),
                        Ato = PeDocumentoService.Ato(atoTipo == null ? null : atos.GetValueOrDefault(atoTipo, atoTipo),
                            PeRegistroDados.Texto(valores[PeDominios.ChavePdtic.CampoAtoNumero])),
                        Sei = PeRegistroDados.Texto(valores[PeDominios.ChavePdtic.CampoSei])
                    });
                }
            }
        return saida;
    }

    /// <summary>Todas as versões geradas dos documentos (PDTIC, RA e RR) das versões do PDTIC do órgão, da mais nova para a mais antiga.</summary>
    private async Task<List<PeOrgaoDocumentoResponse>> DocumentosAsync(IReadOnlyList<PePdtic> pdtics, bool acompanhamentoAtivo)
    {
        var ids = pdtics.Select(p => p.Id).ToList();
        var versoes = await _context.PeDocVersoes.AsNoTracking().Where(v => ids.Contains(v.PdticId)).ToListAsync();
        if (versoes.Count == 0) return new List<PeOrgaoDocumentoResponse>();
        var idsVersoes = versoes.Select(v => v.Id).ToList();
        // O documento de cada versão (a coluna da rodada B da E7): só com o acompanhamento ligado
        var documentos = acompanhamentoAtivo
            ? await _context.PeDocVersoesDocumento.AsNoTracking().Where(d => idsVersoes.Contains(d.Id)).ToDictionaryAsync(d => d.Id)
            : new Dictionary<long, PeDocVersaoDocumento>();
        var idsCiclos = documentos.Values.Where(d => d.CicloId != null).Select(d => d.CicloId!.Value).Distinct().ToList();
        var ciclos = idsCiclos.Count == 0
            ? new Dictionary<long, string>()
            : await _context.PeCiclos.AsNoTracking().Where(c => idsCiclos.Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => c.Rotulo);
        var porId = pdtics.ToDictionary(p => p.Id);

        return versoes
            .Select(v =>
            {
                var documento = documentos.GetValueOrDefault(v.Id);
                var tipo = documento?.DocTipo ?? PeDominios.TipoDocumento.Pdtic;
                var cicloId = tipo == PeDominios.TipoDocumento.Ra ? documento?.CicloId : null;
                var versaoPdtic = porId.TryGetValue(v.PdticId, out var p) ? p.Versao : string.Empty;
                var numero = v.Numero.ToString(CultureInfo.InvariantCulture);
                var raiz = $"api/planejamento/pdtic/{v.PdticId.ToString(CultureInfo.InvariantCulture)}";
                return new PeOrgaoDocumentoResponse
                {
                    Tipo = tipo,
                    Rotulo = tipo switch
                    {
                        PeDominios.TipoDocumento.Ra => $"Relatório de acompanhamento · {(cicloId is long c && ciclos.TryGetValue(c, out var rotulo) ? rotulo : "ciclo")} (PDTIC {versaoPdtic})",
                        PeDominios.TipoDocumento.Rr => $"Relatório de resultados (PDTIC {versaoPdtic})",
                        _ => $"PDTIC {versaoPdtic}"
                    },
                    Numero = v.Numero,
                    Situacao = v.Situacao,
                    GeradoEm = v.GeradoEm,
                    Paginas = v.Paginas,
                    Url = tipo switch
                    {
                        PeDominios.TipoDocumento.Ra when cicloId != null =>
                            $"{raiz}/ciclos/{cicloId.Value.ToString(CultureInfo.InvariantCulture)}/relatorio/versoes/{numero}/arquivo",
                        PeDominios.TipoDocumento.Rr => $"{raiz}/relatorio-resultados/versoes/{numero}/arquivo",
                        _ => $"{raiz}/documento/versoes/{numero}/arquivo"
                    },
                    PdticId = v.PdticId,
                    CicloId = cicloId
                };
            })
            .OrderByDescending(d => d.GeradoEm).ThenByDescending(d => d.PdticId).ThenByDescending(d => d.Numero)
            .ToList();
    }

    // ── Apoio ───────────────────────────────────────────────────────────────

    private void ConferirPaineis(PeUserContext ctx, string mensagem)
    {
        if (!_permissoes.PodeVerPaineis(ctx)) throw new ApiException(ErrorCode.PeSemPermissao, mensagem);
    }

    private static PeCampo? CampoDoModelo(PeModeloDados dados, string secaoChave, string campoChave) =>
        dados.SecaoPorChave(secaoChave) is PeSecao secao
            ? dados.CamposDaSecao(secao.Id, incluirExcluidos: true).FirstOrDefault(c => c.Chave == campoChave)
            : null;

    /// <summary>Os rótulos das opções de um campo (valor para rótulo), inclusive as desativadas.</summary>
    private static Dictionary<string, string> Rotulos(PeModeloDados dados, string secaoChave, string campoChave) =>
        CampoDoModelo(dados, secaoChave, campoChave) is PeCampo campo
            ? dados.OpcoesDoCampo(campo.Id).GroupBy(o => o.Valor).ToDictionary(g => g.Key, g => g.First().Rotulo)
            : new Dictionary<string, string>();

    private static Dictionary<string, string> Rotulos(PeModeloDados dados, long secaoId, string campoChave) =>
        dados.CamposDaSecao(secaoId, incluirExcluidos: true).FirstOrDefault(c => c.Chave == campoChave) is PeCampo campo
            ? dados.OpcoesDoCampo(campo.Id).GroupBy(o => o.Valor).ToDictionary(g => g.Key, g => g.First().Rotulo)
            : new Dictionary<string, string>();

    private static string CampoPrincipal(PeModeloDados dados, PeSecao secao) =>
        dados.CamposDaSecao(secao.Id, incluirExcluidos: true).FirstOrDefault(c => c.Principal)?.Chave ?? "texto";

    private static string TextoDo(PeRegistro registro, string campo) =>
        PeRegistroDados.Texto(PeRegistroDados.Ler(registro.Dados)[campo]) ?? string.Empty;
}
