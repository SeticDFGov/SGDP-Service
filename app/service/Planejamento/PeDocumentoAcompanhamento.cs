using api.Planejamento;
using Models.Planejamento;

namespace service.Planejamento;

/// <summary>
/// Os blocos de dados do acompanhamento (E7, rodada B), que o RA e o RR usam e que saem em
/// grupos de tabelas (<see cref="PeDocBlocoResponse.Grupos"/>):
/// <list type="bullet">
/// <item><c>acoes_por_situacao</c>: as ações do plano pela situação no ciclo. No RA, pela
/// situação física na data de referência do ciclo (em dia e concluídas, atrasadas, não iniciadas,
/// canceladas); no RR, pela última situação registrada (concluídas, em andamento, não iniciadas,
/// canceladas), com o ciclo do registro. As sem registro vêm num grupo à parte, quando há.</item>
/// <item><c>metas_por_resultado</c>: no RA de uma avaliação, as metas pelos resultados
/// intermediários da avaliação; no RR, pelo resultado final (passo 7.1). No RA de um
/// monitoramento, não aparece.</item>
/// <item><c>riscos_ocorridos</c>: as ocorrências dos riscos (um grupo só): as do ciclo no RA de
/// um monitoramento, as dos ciclos até o fim da avaliação no RA de uma avaliação e as de todos
/// os ciclos no RR, com o ciclo.</item>
/// <item><c>medicoes</c>: as medições dos indicadores (um grupo só): as do ciclo (no RA de uma
/// avaliação, as do ciclo de monitoramento de referência) ou as de todos os ciclos, no RR.</item>
/// </list>
/// Seção que o órgão não vê, ou que o administrador tirou do documento, faz o bloco sumir, como
/// na tabela de uma seção. As colunas usam os rótulos dos campos de hoje e só os visíveis.
/// </summary>
public partial class PeDocumentoService
{
    /// <summary>As seções que cada bloco do acompanhamento lê (a resolução busca todas de uma vez).</summary>
    private static IEnumerable<string> SecoesDoBlocoDoAcompanhamento(string tipo) => tipo switch
    {
        PeDominios.TipoBloco.AcoesPorSituacao => new[] { PeDominios.TemaDecreto.SecaoAcoes, PeDominios.ChaveAcompanhamento.SecaoMonitoramentoAcoes },
        PeDominios.TipoBloco.MetasPorResultado => new[]
        {
            PeDominios.ChaveAcompanhamento.SecaoMetas, PeDominios.ChaveAcompanhamento.SecaoResultadosIntermediarios,
            PeDominios.ChaveAcompanhamento.SecaoResultadosMetas
        },
        PeDominios.TipoBloco.RiscosOcorridos => new[] { PeDominios.ChavePdtic.SecaoRiscos, PeDominios.ChaveAcompanhamento.SecaoRiscosOcorridos },
        PeDominios.TipoBloco.Medicoes => new[] { PeDominios.ChaveAcompanhamento.SecaoIndicadoresMonitoramento, PeDominios.ChaveAcompanhamento.SecaoMedicoes },
        _ => Array.Empty<string>()
    };

    /// <summary>Os grupos do bloco, ou nulo quando o bloco não aparece no documento.</summary>
    private static List<PeDocGrupoResponse>? BlocoDoAcompanhamento(string tipo, PeTrilhaOrgao trilha,
        IReadOnlyDictionary<string, PeSecaoExportada> dados, PeDocContexto documento)
    {
        var acompanhamento = PeDadosDoAcompanhamento.De(dados);
        return tipo switch
        {
            PeDominios.TipoBloco.AcoesPorSituacao => AcoesPorSituacao(trilha, acompanhamento, documento),
            PeDominios.TipoBloco.MetasPorResultado => MetasPorResultado(trilha, acompanhamento, documento),
            PeDominios.TipoBloco.RiscosOcorridos => RiscosOcorridos(trilha, acompanhamento, documento),
            PeDominios.TipoBloco.Medicoes => Medicoes(trilha, acompanhamento, documento),
            _ => null
        };
    }

    // ── Ações por situação ──────────────────────────────────────────────────

    private static List<PeDocGrupoResponse>? AcoesPorSituacao(PeTrilhaOrgao trilha, PeDadosDoAcompanhamento dados, PeDocContexto documento)
    {
        const string chave = PeDominios.ChaveAcompanhamento.SecaoMonitoramentoAcoes;
        var monitoramento = NoDocumento(dados, chave);
        var acoes = dados.Secao(PeDominios.TemaDecreto.SecaoAcoes);
        if (monitoramento == null || acoes == null) return null;
        var (todos, cicloId, esconde) = documento.CicloPara(PeDominios.TipoCiclo.Monitoramento);
        if (esconde) return null;

        var ciclo = cicloId is long id ? documento.Ciclos.GetValueOrDefault(id) : null;
        var referencia = PeRegrasDoPainel.Referencia(ciclo, PeCiclos.Hoje());
        var comOrcamento = dados.CampoVisivel(PeDominios.TemaDecreto.SecaoAcoes, PeDominios.ChaveAcompanhamento.CampoInvestimento)
                           || dados.CampoVisivel(PeDominios.TemaDecreto.SecaoAcoes, PeDominios.ChaveAcompanhamento.CampoCusteio);

        // O registro de cada ação: o do ciclo (RA) ou o do ciclo mais recente (RR)
        var registros = todos
            ? dados.Registros(chave)
                .Where(r => dados.CicloDe(chave, r.Id) is long c && documento.Ciclos.ContainsKey(c))
                .OrderByDescending(r => documento.Ciclos[dados.CicloDe(chave, r.Id)!.Value].Inicio)
                .ThenBy(r => r.Ordem)
                .ToList()
            : cicloId is long doCiclo ? dados.DoCiclo(chave, doCiclo) : new List<PeRegistroResponse>();
        PeRegistroResponse? DaAcao(long acaoId) => registros.FirstOrDefault(r =>
            PeDadosDoAcompanhamento.Ligados(r, PeDominios.ChaveAcompanhamento.CampoAcao).Contains(acaoId));

        var colunas = new List<PeDocColunaResponse>();
        if (todos) colunas.Add(new PeDocColunaResponse { Chave = ColunaDoCiclo, Rotulo = "Ciclo" });
        colunas.Add(new PeDocColunaResponse { Chave = PeDominios.ChaveAcompanhamento.CampoAcao, Rotulo = "Ação" });
        colunas.Add(Coluna(monitoramento, PeDominios.ChaveAcompanhamento.CampoSituacao, "Situação"));
        if (!todos && dados.CampoVisivel(PeDominios.TemaDecreto.SecaoAcoes, PeDominios.ChaveAcompanhamento.CampoConclusao))
            colunas.Add(Coluna(acoes, PeDominios.ChaveAcompanhamento.CampoConclusao, "Conclusão prevista"));
        foreach (var campo in new[]
                 {
                     PeDominios.ChaveAcompanhamento.CampoExecucaoFisica, PeDominios.ChaveAcompanhamento.CampoExecucaoOrcamentaria,
                     PeDominios.ChaveAcompanhamento.CampoObservacao
                 })
            if (dados.CampoVisivel(chave, campo)) colunas.Add(Coluna(monitoramento, campo, campo));

        var noCiclo = documento.EhRaDeAvaliacao && ciclo != null ? $"Pela situação registrada no ciclo {ciclo.Rotulo}: " : string.Empty;
        var grupos = todos
            ? new List<(string Chave, string Titulo, string? Texto)>
            {
                ("concluidas", "Ações concluídas", null),
                ("em_andamento", "Ações em andamento", "Ações começadas e ainda não concluídas no último registro."),
                ("nao_iniciadas", "Ações não iniciadas", "Ações que não tinham começado no último registro."),
                ("canceladas", "Ações canceladas", null),
                ("sem_registro", "Ações sem registro no acompanhamento", "Ações sem a situação registrada em nenhum ciclo de monitoramento.")
            }
            : new List<(string Chave, string Titulo, string? Texto)>
            {
                ("em_dia", "Ações em dia e concluídas", Capitalizada($"{noCiclo}ações dentro do prazo previsto e ações concluídas.")),
                ("atrasadas", "Ações atrasadas", Capitalizada(
                    $"{noCiclo}ações com a conclusão prevista (ou, nas não iniciadas, o início previsto) antes de {PeCiclos.Data(referencia)}.")),
                ("nao_iniciadas", "Ações não iniciadas", Capitalizada($"{noCiclo}ações que ainda não começaram, dentro do prazo previsto.")),
                ("canceladas", "Ações canceladas", null),
                ("sem_registro", "Ações sem registro no ciclo", ciclo != null
                    ? $"Ações sem a situação registrada no ciclo {ciclo.Rotulo}."
                    : "Nenhum ciclo de monitoramento teve registro até esta avaliação.")
            };
        var linhas = grupos.ToDictionary(g => g.Chave, _ => new List<PeDocLinhaResponse>());

        foreach (var acao in acoes.Registros)
        {
            var registro = DaAcao(acao.Id);
            var situacao = registro == null ? null : PeDadosDoAcompanhamento.Texto(registro, PeDominios.ChaveAcompanhamento.CampoSituacao);
            string grupo;
            if (registro == null) grupo = "sem_registro";
            else if (todos)
                grupo = situacao switch
                {
                    PeDominios.ChaveAcompanhamento.AcaoConcluida => "concluidas",
                    PeDominios.ChaveAcompanhamento.AcaoCancelada => "canceladas",
                    PeDominios.ChaveAcompanhamento.AcaoNaoIniciada => "nao_iniciadas",
                    _ => "em_andamento"
                };
            else
                grupo = PeRegrasDoPainel.SituacaoFisica(true, situacao,
                        PeDadosDoAcompanhamento.Data(acao, PeDominios.ChaveAcompanhamento.CampoInicio),
                        PeDadosDoAcompanhamento.Data(acao, PeDominios.ChaveAcompanhamento.CampoConclusao), referencia) switch
                    {
                        PeDominios.SituacaoFisica.Cancelada => "canceladas",
                        PeDominios.SituacaoFisica.Atrasada => "atrasadas",
                        _ when situacao == PeDominios.ChaveAcompanhamento.AcaoNaoIniciada => "nao_iniciadas",
                        _ => "em_dia"
                    };

            var linha = new PeDocLinhaResponse { Codigo = acao.Codigo };
            if (todos) linha.Celulas[ColunaDoCiclo] = RotuloDoCiclo(documento, registro == null ? null : dados.CicloDe(chave, registro.Id));
            linha.Celulas[PeDominios.ChaveAcompanhamento.CampoAcao] = Celula(PeDadosDoAcompanhamento.Texto(acao, PeDominios.TemaDecreto.CampoDescricao));
            linha.Celulas[PeDominios.ChaveAcompanhamento.CampoSituacao] =
                Celula(registro == null ? null : PeDadosDoAcompanhamento.Rotulo(registro, PeDominios.ChaveAcompanhamento.CampoSituacao));
            linha.Celulas[PeDominios.ChaveAcompanhamento.CampoConclusao] = Celula(PeDadosDoAcompanhamento.Rotulo(acao, PeDominios.ChaveAcompanhamento.CampoConclusao));
            linha.Celulas[PeDominios.ChaveAcompanhamento.CampoExecucaoFisica] =
                Celula(registro == null ? null : PeDadosDoAcompanhamento.Rotulo(registro, PeDominios.ChaveAcompanhamento.CampoExecucaoFisica));
            var naoSeAplica = comOrcamento && PeRegrasDoPainel.OrcamentoNaoSeAplica(
                PeDadosDoAcompanhamento.Numero(acao, PeDominios.ChaveAcompanhamento.CampoInvestimento),
                PeDadosDoAcompanhamento.Numero(acao, PeDominios.ChaveAcompanhamento.CampoCusteio));
            linha.Celulas[PeDominios.ChaveAcompanhamento.CampoExecucaoOrcamentaria] = naoSeAplica
                ? "Não se aplica"
                : Celula(registro == null ? null : PeDadosDoAcompanhamento.Rotulo(registro, PeDominios.ChaveAcompanhamento.CampoExecucaoOrcamentaria));
            linha.Celulas[PeDominios.ChaveAcompanhamento.CampoObservacao] =
                Celula(registro == null ? null : PeDadosDoAcompanhamento.Rotulo(registro, PeDominios.ChaveAcompanhamento.CampoObservacao));
            linhas[grupo].Add(linha);
        }

        var passo = trilha.Secao(chave)?.Passo.Numero;
        return grupos
            .Where(g => g.Chave != "sem_registro" || linhas[g.Chave].Count > 0)
            .Select(g => Grupo(g.Chave, g.Titulo, g.Texto, chave, passo, colunas, linhas[g.Chave]))
            .ToList();
    }

    // ── Metas por resultado ─────────────────────────────────────────────────

    private static List<PeDocGrupoResponse>? MetasPorResultado(PeTrilhaOrgao trilha, PeDadosDoAcompanhamento dados, PeDocContexto documento)
    {
        var metas = dados.Secao(PeDominios.ChaveAcompanhamento.SecaoMetas);
        if (metas == null || documento.EhRaDeMonitoramento) return null;
        var intermediaria = documento.EhRaDeAvaliacao;
        var chave = intermediaria ? PeDominios.ChaveAcompanhamento.SecaoResultadosIntermediarios : PeDominios.ChaveAcompanhamento.SecaoResultadosMetas;
        var resultados = NoDocumento(dados, chave);
        if (resultados == null) return null;
        var campoDoResultado = intermediaria ? PeDominios.ChaveAcompanhamento.CampoSituacao : PeDominios.ChaveAcompanhamento.CampoResultado;

        var registros = intermediaria
            ? documento.Ciclo is { } avaliacao ? dados.DoCiclo(chave, avaliacao.Id) : new List<PeRegistroResponse>()
            : resultados.Registros;
        PeRegistroResponse? DaMeta(long metaId) => registros.FirstOrDefault(r =>
            PeDadosDoAcompanhamento.Ligados(r, PeDominios.ChaveAcompanhamento.CampoMeta).Contains(metaId)
            && PeDadosDoAcompanhamento.Texto(r, campoDoResultado) != null);

        var colunas = new List<PeDocColunaResponse> { new() { Chave = PeDominios.ChaveAcompanhamento.CampoMeta, Rotulo = "Meta" } };
        var daMeta = new List<string>();
        foreach (var campo in new[]
                 {
                     PeDominios.ChaveAcompanhamento.CampoIndicador, PeDominios.ChaveAcompanhamento.CampoValorDaMeta,
                     PeDominios.ChaveAcompanhamento.CampoPrazo
                 })
        {
            if (!dados.CampoVisivel(PeDominios.ChaveAcompanhamento.SecaoMetas, campo)) continue;
            colunas.Add(Coluna(metas, campo, campo, prefixo: "meta_"));
            daMeta.Add(campo);
        }
        var doResultado = (intermediaria
                ? new[] { PeDominios.ChaveAcompanhamento.CampoValorAlcancado, PeDominios.ChaveAcompanhamento.CampoData, PeDominios.ChaveAcompanhamento.CampoObservacao }
                : new[] { PeDominios.ChaveAcompanhamento.CampoMotivo })
            .Where(c => dados.CampoVisivel(chave, c))
            .ToList();
        colunas.AddRange(doResultado.Select(c => Coluna(resultados, c, c)));

        var grupos = intermediaria
            ? new List<(string Chave, string Titulo, string? Texto)>
            {
                ("alcancadas", "Metas alcançadas", null),
                ("em_andamento", "Metas em andamento", "Metas ainda em execução, sem o resultado final."),
                ("nao_alcancadas", "Metas não alcançadas", null),
                ("canceladas", "Metas canceladas", null),
                ("sem_registro", "Metas sem resultado registrado", "Metas sem o resultado registrado nesta avaliação.")
            }
            : new List<(string Chave, string Titulo, string? Texto)>
            {
                ("alcancadas", "Metas alcançadas", null),
                ("nao_alcancadas", "Metas não alcançadas", null),
                ("canceladas", "Metas canceladas", null),
                ("sem_registro", "Metas sem resultado registrado", "Metas sem o resultado final registrado.")
            };
        var linhas = grupos.ToDictionary(g => g.Chave, _ => new List<PeDocLinhaResponse>());

        foreach (var meta in metas.Registros)
        {
            var registro = DaMeta(meta.Id);
            var grupo = (registro == null ? null : PeDadosDoAcompanhamento.Texto(registro, campoDoResultado)) switch
            {
                PeDominios.ChaveAcompanhamento.MetaAlcancada => "alcancadas",
                PeDominios.ChaveAcompanhamento.MetaNaoAlcancada => "nao_alcancadas",
                PeDominios.ChaveAcompanhamento.MetaCancelada => "canceladas",
                PeDominios.ChaveAcompanhamento.MetaEmAndamento when intermediaria => "em_andamento",
                _ => "sem_registro"
            };
            var linha = new PeDocLinhaResponse { Codigo = meta.Codigo };
            linha.Celulas[PeDominios.ChaveAcompanhamento.CampoMeta] = Celula(PeDadosDoAcompanhamento.Texto(meta, PeDominios.ChaveAcompanhamento.CampoDescricao));
            foreach (var campo in daMeta) linha.Celulas["meta_" + campo] = Celula(PeDadosDoAcompanhamento.Rotulo(meta, campo));
            foreach (var campo in doResultado) linha.Celulas[campo] = Celula(registro == null ? null : PeDadosDoAcompanhamento.Rotulo(registro, campo));
            linhas[grupo].Add(linha);
        }

        var passo = trilha.Secao(chave)?.Passo.Numero;
        return grupos
            .Where(g => g.Chave != "sem_registro" || linhas[g.Chave].Count > 0)
            .Select(g => Grupo(g.Chave, g.Titulo, g.Texto, chave, passo, colunas, linhas[g.Chave]))
            .ToList();
    }

    // ── Riscos que ocorreram ────────────────────────────────────────────────

    private static List<PeDocGrupoResponse>? RiscosOcorridos(PeTrilhaOrgao trilha, PeDadosDoAcompanhamento dados, PeDocContexto documento)
    {
        const string chave = PeDominios.ChaveAcompanhamento.SecaoRiscosOcorridos;
        var ocorridos = NoDocumento(dados, chave);
        if (ocorridos == null) return null;
        var (todos, cicloId, esconde) = documento.CicloPara(PeDominios.TipoCiclo.Monitoramento);
        if (esconde) return null;

        // No RA de uma avaliação: as ocorrências dos ciclos de monitoramento até o fim dela
        HashSet<long>? dosCiclos = null;
        if (documento.EhRaDeAvaliacao)
        {
            var ate = documento.Ciclo!.Fim ?? PeCiclos.Hoje();
            dosCiclos = documento.Ciclos.Values.Where(c => c.Tipo == PeDominios.TipoCiclo.Monitoramento && c.Inicio <= ate).Select(c => c.Id).ToHashSet();
            todos = true;
        }
        var registros = ocorridos.Registros
            .Select(r => (Registro: r, Ciclo: dados.CicloDe(chave, r.Id)))
            .Where(x => x.Ciclo is long c && documento.Ciclos.ContainsKey(c)
                        && (dosCiclos?.Contains(c) ?? (todos || c == cicloId)))
            .OrderBy(x => documento.Ciclos[x.Ciclo!.Value].Inicio)
            .ThenBy(x => PeDadosDoAcompanhamento.Data(x.Registro, PeDominios.ChaveAcompanhamento.CampoData) ?? DateOnly.MaxValue)
            .ThenBy(x => x.Registro.Ordem)
            .ToList();

        var riscos = dados.Secao(PeDominios.ChavePdtic.SecaoRiscos);
        var porId = riscos?.Registros.ToDictionary(r => r.Id) ?? new Dictionary<long, PeRegistroResponse>();
        var comNivel = riscos != null
                       && (dados.CampoVisivel(PeDominios.ChavePdtic.SecaoRiscos, PeDominios.ChaveAcompanhamento.CampoNivelRisco)
                           || dados.CampoVisivel(PeDominios.ChavePdtic.SecaoRiscos, PeDominios.ChaveAcompanhamento.CampoNivelRiscoSimples));

        var colunas = new List<PeDocColunaResponse>();
        if (todos) colunas.Add(new PeDocColunaResponse { Chave = ColunaDoCiclo, Rotulo = "Ciclo" });
        colunas.Add(new PeDocColunaResponse { Chave = PeDominios.ChaveAcompanhamento.CampoRisco, Rotulo = "Risco" });
        if (comNivel) colunas.Add(new PeDocColunaResponse { Chave = "nivel_do_risco", Rotulo = "Nível" });
        var doRegistro = new[]
            {
                PeDominios.ChaveAcompanhamento.CampoData, PeDominios.ChaveAcompanhamento.CampoSituacao,
                PeDominios.ChaveAcompanhamento.CampoAcoesRealizadas, PeDominios.ChaveAcompanhamento.CampoResponsavel,
                PeDominios.ChaveAcompanhamento.CampoResultado
            }
            .Where(c => dados.CampoVisivel(chave, c))
            .ToList();
        colunas.AddRange(doRegistro.Select(c => Coluna(ocorridos, c, c)));

        var linhas = registros.Select(x =>
        {
            var ligado = x.Registro.Vinculos.GetValueOrDefault(PeDominios.ChaveAcompanhamento.CampoRisco)?.FirstOrDefault();
            var risco = ligado != null ? porId.GetValueOrDefault(ligado.RegistroId) : null;
            var linha = new PeDocLinhaResponse { Codigo = ligado?.Codigo };
            if (todos) linha.Celulas[ColunaDoCiclo] = RotuloDoCiclo(documento, x.Ciclo);
            linha.Celulas[PeDominios.ChaveAcompanhamento.CampoRisco] = Celula(risco != null
                ? PeDadosDoAcompanhamento.Texto(risco, PeDominios.ChaveAcompanhamento.CampoDescricao)
                : ligado?.Resumo);
            if (comNivel)
                linha.Celulas["nivel_do_risco"] = Celula(risco == null
                    ? null
                    : PeDadosDoAcompanhamento.Rotulo(risco, PeDominios.ChaveAcompanhamento.CampoNivelRisco)
                      ?? PeDadosDoAcompanhamento.Rotulo(risco, PeDominios.ChaveAcompanhamento.CampoNivelRiscoSimples));
            foreach (var campo in doRegistro) linha.Celulas[campo] = Celula(PeDadosDoAcompanhamento.Rotulo(x.Registro, campo));
            return linha;
        }).ToList();

        var titulo = documento.EhRaDeMonitoramento ? "Riscos que ocorreram no ciclo" : "Riscos que ocorreram";
        var texto = documento.EhRaDeMonitoramento
            ? null
            : documento.EhRaDeAvaliacao
                ? documento.Ciclo!.Fim == null
                    ? "As ocorrências registradas nos ciclos de monitoramento até hoje."
                    : "As ocorrências registradas nos ciclos de monitoramento até o fim desta avaliação."
                : "As ocorrências registradas em todos os ciclos de monitoramento.";
        return new List<PeDocGrupoResponse> { Grupo("riscos", titulo, texto, chave, trilha.Secao(chave)?.Passo.Numero, colunas, linhas) };
    }

    // ── Medições dos indicadores ────────────────────────────────────────────

    private static List<PeDocGrupoResponse>? Medicoes(PeTrilhaOrgao trilha, PeDadosDoAcompanhamento dados, PeDocContexto documento)
    {
        const string chave = PeDominios.ChaveAcompanhamento.SecaoMedicoes;
        var medicoes = NoDocumento(dados, chave);
        if (medicoes == null) return null;
        var (todos, cicloId, esconde) = documento.CicloPara(PeDominios.TipoCiclo.Monitoramento);
        if (esconde) return null;

        var indicadores = dados.Secao(PeDominios.ChaveAcompanhamento.SecaoIndicadoresMonitoramento);
        var registros = medicoes.Registros
            .Select(r => (Registro: r, Ciclo: dados.CicloDe(chave, r.Id)))
            .Where(x => x.Ciclo is long c && documento.Ciclos.ContainsKey(c) && (todos || c == cicloId))
            .OrderBy(x => documento.Ciclos[x.Ciclo!.Value].Inicio)
            .ThenBy(x => x.Registro.Ordem)
            .ToList();
        List<(PeRegistroResponse Registro, long? Ciclo)> DoIndicador(long indicadorId) => registros
            .Where(x => PeDadosDoAcompanhamento.Ligados(x.Registro, PeDominios.ChaveAcompanhamento.CampoIndicador).Contains(indicadorId))
            .ToList();

        var colunas = new List<PeDocColunaResponse>();
        if (todos) colunas.Add(new PeDocColunaResponse { Chave = ColunaDoCiclo, Rotulo = "Ciclo" });
        colunas.Add(new PeDocColunaResponse { Chave = PeDominios.ChaveAcompanhamento.CampoIndicador, Rotulo = "Indicador" });
        var comReferencia = indicadores != null
                            && dados.CampoVisivel(PeDominios.ChaveAcompanhamento.SecaoIndicadoresMonitoramento,
                                PeDominios.ChaveAcompanhamento.CampoValoresReferencia);
        if (comReferencia) colunas.Add(Coluna(indicadores!, PeDominios.ChaveAcompanhamento.CampoValoresReferencia, "Valores de referência"));
        var doRegistro = new[]
            {
                PeDominios.ChaveAcompanhamento.CampoValorApurado, PeDominios.ChaveAcompanhamento.CampoData,
                PeDominios.ChaveAcompanhamento.CampoObservacao
            }
            .Where(c => dados.CampoVisivel(chave, c))
            .ToList();
        colunas.AddRange(doRegistro.Select(c => Coluna(medicoes, c, c)));

        PeDocLinhaResponse Linha(PeRegistroResponse? indicador, PeVinculoResponse? ligado, PeRegistroResponse? medicao, long? ciclo)
        {
            var linha = new PeDocLinhaResponse { Codigo = indicador?.Codigo ?? ligado?.Codigo };
            if (todos) linha.Celulas[ColunaDoCiclo] = RotuloDoCiclo(documento, ciclo);
            linha.Celulas[PeDominios.ChaveAcompanhamento.CampoIndicador] = Celula(indicador != null
                ? PeDadosDoAcompanhamento.Texto(indicador, PeDominios.ChaveAcompanhamento.CampoIndicador)
                : ligado?.Resumo);
            if (comReferencia)
                linha.Celulas[PeDominios.ChaveAcompanhamento.CampoValoresReferencia] = Celula(indicador == null
                    ? null
                    : PeDadosDoAcompanhamento.Rotulo(indicador, PeDominios.ChaveAcompanhamento.CampoValoresReferencia));
            foreach (var campo in doRegistro) linha.Celulas[campo] = Celula(medicao == null ? null : PeDadosDoAcompanhamento.Rotulo(medicao, campo));
            return linha;
        }

        var linhas = new List<PeDocLinhaResponse>();
        if (indicadores != null)
        {
            // Cada indicador do plano, com a medição do ciclo (ou as de todos os ciclos); sem medição, a linha vazia
            foreach (var indicador in indicadores.Registros)
            {
                var doIndicador = DoIndicador(indicador.Id);
                if (doIndicador.Count == 0) linhas.Add(Linha(indicador, null, null, null));
                else if (todos) linhas.AddRange(doIndicador.Select(x => Linha(indicador, null, x.Registro, x.Ciclo)));
                else linhas.Add(Linha(indicador, null, doIndicador[0].Registro, doIndicador[0].Ciclo));
            }
        }
        else
        {
            linhas.AddRange(registros.Select(x => Linha(null,
                x.Registro.Vinculos.GetValueOrDefault(PeDominios.ChaveAcompanhamento.CampoIndicador)?.FirstOrDefault(), x.Registro, x.Ciclo)));
        }

        string? texto = null;
        if (todos) texto = "As medições de todos os ciclos de monitoramento, por indicador.";
        else if (documento.EhRaDeAvaliacao)
            texto = documento.CicloDeMonitoramento is { } referencia
                ? $"As medições do ciclo {referencia.Rotulo}, o último ciclo de monitoramento com dado até esta avaliação."
                : "Nenhum ciclo de monitoramento teve registro até esta avaliação.";
        return new List<PeDocGrupoResponse>
        {
            Grupo("medicoes", "Medições dos indicadores", texto, chave, trilha.Secao(chave)?.Passo.Numero, colunas, linhas)
        };
    }

    // ── Apoio ───────────────────────────────────────────────────────────────

    /// <summary>A seção por ciclo do bloco, quando o órgão a vê e ela vai para o documento.</summary>
    private static PeSecaoExportada? NoDocumento(PeDadosDoAcompanhamento dados, string chave) =>
        dados.Secao(chave) is { } secao && secao.Modelo.Secao.NoDocumento ? secao : null;

    /// <summary>A coluna com o rótulo de hoje do campo (o padrão quando o campo não está na seção).</summary>
    private static PeDocColunaResponse Coluna(PeSecaoExportada secao, string campo, string padrao, string prefixo = "") => new()
    {
        Chave = prefixo + campo,
        Rotulo = secao.Colunas.FirstOrDefault(c => c.Campo.Chave == campo)?.Campo.Rotulo ?? padrao
    };

    private static string Celula(string? texto) => string.IsNullOrWhiteSpace(texto) ? "-" : texto.Trim();

    /// <summary>A frase com a primeira letra maiúscula (o começo muda quando o ciclo vem na frente).</summary>
    private static string Capitalizada(string texto) =>
        texto.Length == 0 ? texto : char.ToUpperInvariant(texto[0]) + texto[1..];

    private static string RotuloDoCiclo(PeDocContexto documento, long? cicloId) =>
        cicloId is long id && documento.Ciclos.TryGetValue(id, out var ciclo) ? ciclo.Rotulo : "-";

    private static PeDocGrupoResponse Grupo(string chave, string titulo, string? texto, string secao, string? passo,
        List<PeDocColunaResponse> colunas, List<PeDocLinhaResponse> linhas) => new()
    {
        Chave = chave,
        Titulo = titulo,
        Texto = texto,
        Tabela = new PeDocTabelaResponse
        {
            SecaoChave = secao,
            SecaoTitulo = titulo,
            SecaoTipo = PeDominios.TipoSecao.Tabela,
            PassoNumero = passo,
            Colunas = colunas,
            Linhas = linhas,
            Vazia = linhas.Count == 0
        }
    };
}
