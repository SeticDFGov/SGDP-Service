using System.Globalization;
using System.Text.Json;
using api.Planejamento;
using Microsoft.EntityFrameworkCore;
using Models;
using Models.Planejamento;
using service.Interface;

namespace service.Planejamento;

/// <summary>
/// O acompanhamento do PDTIC (E7, rodada B; seções 7 e 8 do plano e o guia, 3.3 a 3.6):
/// <list type="bullet">
/// <item>ciclos de monitoramento: depois da publicação, existem pela periodicidade do passo 4.3
/// (ou a padrão) e são criados, de forma idempotente, quando a lista é lida (sem botão de gerar);
/// a troca da periodicidade recria só os ciclos sem registro e não fechados; a leitura
/// simultânea é serializada por uma trava por PDTIC no PostgreSQL;</item>
/// <item>avaliação intermediária: a equipe abre (uma aberta por vez, índice único); o fim é o dia
/// em que foi fechada;</item>
/// <item>fechar exige a situação de todas as ações no ciclo e, nos níveis com o passo 5.2, o
/// resumo do ciclo (na avaliação, as seções obrigatórias de 6.1 a 6.3), senão 400 com as
/// pendências; fecha e gera o RA do ciclo em minuta (quando o modelo ra existe e o nível tem o
/// passo 5.2; na avaliação, sempre), numa gravação só; reabrir guarda quem e quando;</item>
/// <item>grades: a situação das ações e as medições do ciclo, gravadas pelo motor de registros
/// (validação do nível, erros por "id.campo"), com o registro do ciclo anterior ao lado;</item>
/// <item>painel do PDTIC (AC-PDTIC): as metas com as ações, os percentuais e as situações no
/// ciclo de referência, e os quadros dos riscos (<see cref="PeRegrasDoPainel"/>).</item>
/// </list>
/// Tudo só funciona com a versão 6 do modelo inicial carregada (<see cref="PeAcompanhamentoAtivo"/>);
/// antes (e no intervalo do deploy), 409 PeModeloIndisponivel com corpo.
/// </summary>
public class PeAcompanhamentoService : IPeAcompanhamentoService
{
    private const int MaximoRotulo = 100;

    // Trava por PDTIC da criação dos ciclos (pg_advisory_xact_lock(chave, pdtic))
    private const int ChaveDaTrava = 48900726;

    private readonly AppDbContext _context;
    private readonly IPeRegistroService _registros;
    private readonly IPePermissionService _permissoes;
    private readonly IPeDocumentoService _documentos;

    public PeAcompanhamentoService(AppDbContext context, IPeRegistroService registros, IPePermissionService permissoes,
        IPeDocumentoService documentos)
    {
        _context = context;
        _registros = registros;
        _permissoes = permissoes;
        _documentos = documentos;
    }

    // ── Ciclos ──────────────────────────────────────────────────────────────

    public async Task<List<PeCicloResponse>> ListarCiclosAsync(long pdticId, string? tipo, PeUserContext ctx)
    {
        var pdtic = await PePdticService.LerAsync(_context, _permissoes, pdticId, ctx);
        var trilha = await PeTrilhaOrgao.DoPdticAsync(_context, pdtic);
        trilha.Dados.Acompanhamento.Exigir();
        var filtro = string.IsNullOrWhiteSpace(tipo) ? null : tipo.Trim();
        // Fora do domínio: lista vazia, nunca "todos" em silêncio
        if (filtro != null && !PeDominios.TipoCiclo.Todos.Contains(filtro)) return new List<PeCicloResponse>();

        if (PeDominios.SituacaoPdtic.Vigentes.Contains(pdtic.Situacao)) await SincronizarAsync(pdtic, trilha, ctx.Email);
        var ciclos = await _context.PeCiclos.AsNoTracking()
            .Where(c => c.PdticId == pdtic.Id && (filtro == null || c.Tipo == filtro))
            .ToListAsync();
        return await RespostasAsync(pdtic, trilha, PeCiclosDoPdtic.Ordenar(ciclos), ctx);
    }

    /// <summary>
    /// Cria os ciclos de monitoramento que faltam e apaga os que saíram da periodicidade (sem
    /// registro e não fechados), numerando todos de novo pela ordem do início. Idempotente; a trava
    /// por PDTIC faz duas leituras simultâneas esperarem uma pela outra.
    /// </summary>
    private async Task SincronizarAsync(PePdtic pdtic, PeTrilhaOrgao trilha, string autor)
    {
        try
        {
            await using var transacao = _context.Database.IsRelational() ? await _context.Database.BeginTransactionAsync() : null;
            if (_context.Database.IsNpgsql())
                await _context.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock({0}, {1})", ChaveDaTrava, (int)(pdtic.Id % int.MaxValue));

            var gravados = await _context.PeCiclos.Where(c => c.PdticId == pdtic.Id).ToListAsync();
            var plano = await PeCiclosDoPdtic.PlanoAsync(_context, _registros, pdtic, trilha, gravados, autor);
            if (!plano.MudaAlgo)
            {
                if (transacao != null) await transacao.CommitAsync();
                return;
            }

            // Os números mudam de lugar (o índice único não deixa dois iguais nem por um instante):
            // primeiro saem os apagados e os que ficam vão para longe; depois, a numeração nova
            _context.PeCiclos.RemoveRange(plano.Apagar);
            foreach (var ciclo in plano.Manter) ciclo.Numero += 100000;
            await _context.SaveChangesAsync();
            var numero = 0;
            foreach (var ciclo in plano.Resultado())
            {
                ciclo.Numero = ++numero;
                if (ciclo.Id == 0) _context.PeCiclos.Add(ciclo);
            }
            await _context.SaveChangesAsync();
            if (transacao != null) await transacao.CommitAsync();
        }
        catch (DbUpdateException)
        {
            // Outra pessoa mexeu nos ciclos ao mesmo tempo: a lista sai com o que ficou gravado
            _context.ChangeTracker.Clear();
        }
    }

    public async Task<PeCicloResponse> CriarCicloAsync(long pdticId, PeCicloCriarDTO dto, PeUserContext ctx)
    {
        var tipo = dto.Tipo?.Trim();
        if (tipo == PeDominios.TipoCiclo.Monitoramento)
            throw new ApiException(ErrorCode.PeCicloInvalido,
                "Os ciclos de monitoramento são criados sozinhos pela periodicidade do monitoramento. Aqui se abre uma avaliação intermediária.");
        if (tipo != PeDominios.TipoCiclo.Avaliacao)
            throw new ApiException(ErrorCode.PeDadosInvalidos, "Diga o tipo do ciclo: avaliacao.");

        var pdtic = await PePdticService.LerAsync(_context, _permissoes, pdticId, ctx, rastrear: true);
        var trilha = await PeTrilhaOrgao.DoPdticAsync(_context, pdtic);
        trilha.Dados.Acompanhamento.Exigir();
        ConferirGravacao(pdtic, ctx, "Quem abre a avaliação intermediária é a equipe do órgão.");
        if (!TemSecoesDoTipo(trilha, PeDominios.TipoCiclo.Avaliacao))
            throw new ApiException(ErrorCode.PePassoIndisponivel, "A avaliação intermediária não está na trilha do nível do órgão.");

        var avaliacoes = await _context.PeCiclos.AsNoTracking()
            .Where(c => c.PdticId == pdtic.Id && c.Tipo == PeDominios.TipoCiclo.Avaliacao)
            .ToListAsync();
        if (avaliacoes.FirstOrDefault(c => c.Situacao == PeDominios.SituacaoCiclo.Aberto) is { } aberta)
            throw new ApiException(ErrorCode.PeAvaliacaoAberta,
                $"A avaliação \"{aberta.Rotulo}\" ainda está aberta. Feche essa antes de abrir outra.");

        var numero = avaliacoes.Select(c => c.Numero).DefaultIfEmpty(0).Max() + 1;
        var rotulo = dto.Rotulo?.Trim();
        if (string.IsNullOrEmpty(rotulo)) rotulo = $"Avaliação intermediária {numero.ToString(CultureInfo.InvariantCulture)}";
        if (rotulo.Length > MaximoRotulo)
            throw new ApiException(ErrorCode.PeDadosInvalidos, $"O nome da avaliação tem no máximo {MaximoRotulo} caracteres.");

        var agora = DateTime.UtcNow;
        var ciclo = new PeCiclo
        {
            PdticId = pdtic.Id,
            Tipo = PeDominios.TipoCiclo.Avaliacao,
            Numero = numero,
            Rotulo = rotulo,
            Inicio = PeCiclos.Hoje(),
            Situacao = PeDominios.SituacaoCiclo.Aberto,
            CriadoEm = agora,
            CriadoPor = ctx.Email
        };
        _context.PeCiclos.Add(ciclo);
        pdtic.AlteradoEm = agora;
        pdtic.AlteradoPor = ctx.Email;
        await _context.SaveChangesAsync();
        return (await RespostasAsync(pdtic, trilha, new List<PeCiclo> { ciclo }, ctx))[0];
    }

    public async Task<PeCicloResponse> FecharCicloAsync(long cicloId, PeUserContext ctx)
    {
        var (ciclo, pdtic, trilha) = await CicloParaGravarAsync(cicloId, ctx, "Quem fecha o ciclo é a equipe do órgão.");
        var hoje = PeCiclos.Hoje();
        if (ciclo.Situacao == PeDominios.SituacaoCiclo.Fechado)
            throw new ApiException(ErrorCode.PeCicloFechado,
                $"O ciclo {ciclo.Rotulo} já foi fechado em {PePdticService.DataBrasilia(ciclo.FechadoEm)}.");
        if (ciclo.Inicio > hoje)
            throw new ApiException(ErrorCode.PeCicloFechado, $"O ciclo {ciclo.Rotulo} começa em {PeCiclos.Data(ciclo.Inicio)}. Ele fecha depois de começar.");

        var pendencias = await PendenciasDoFechamentoAsync(pdtic, trilha, ciclo);
        if (pendencias.Count > 0)
            throw new PePendenciasException(ErrorCode.PeCicloComPendencias, pendencias.Count == 1
                ? "Falta uma coisa para fechar o ciclo. Confira a lista."
                : $"Faltam {pendencias.Count} coisas para fechar o ciclo. Confira a lista.", pendencias);

        var agora = DateTime.UtcNow;
        ciclo.Situacao = PeDominios.SituacaoCiclo.Fechado;
        ciclo.FechadoEm = agora;
        ciclo.FechadoPor = ctx.Email;
        ciclo.AlteradoEm = agora;
        ciclo.AlteradoPor = ctx.Email;
        if (ciclo.Tipo == PeDominios.TipoCiclo.Avaliacao) ciclo.Fim = hoje;
        // O RA do ciclo em minuta, na mesma gravação do fechamento
        if (await RaLigadoAsync(trilha, ciclo))
            await _documentos.GerarVersaoAsync(pdtic, PeDocAlvo.Ra(pdtic.Id, ciclo.Id), ctx, PeDominios.SituacaoVersaoDoc.Minuta);
        await _context.SaveChangesAsync();
        return (await RespostasAsync(pdtic, trilha, new List<PeCiclo> { ciclo }, ctx))[0];
    }

    public async Task<PeCicloResponse> ReabrirCicloAsync(long cicloId, PeUserContext ctx)
    {
        var (ciclo, pdtic, trilha) = await CicloParaGravarAsync(cicloId, ctx, "Quem reabre o ciclo é a equipe do órgão.");
        if (ciclo.Situacao != PeDominios.SituacaoCiclo.Fechado)
            throw new ApiException(ErrorCode.PeCicloFechado, $"O ciclo {ciclo.Rotulo} já está aberto.");
        if (ciclo.Tipo == PeDominios.TipoCiclo.Avaliacao
            && await _context.PeCiclos.AsNoTracking().FirstOrDefaultAsync(c => c.PdticId == pdtic.Id && c.Id != ciclo.Id
                && c.Tipo == PeDominios.TipoCiclo.Avaliacao && c.Situacao == PeDominios.SituacaoCiclo.Aberto) is { } aberta)
            throw new ApiException(ErrorCode.PeAvaliacaoAberta,
                $"A avaliação \"{aberta.Rotulo}\" está aberta. Feche essa antes de reabrir outra.");

        var agora = DateTime.UtcNow;
        ciclo.Situacao = PeDominios.SituacaoCiclo.Aberto;
        ciclo.FechadoEm = null;
        ciclo.FechadoPor = null;
        ciclo.ReabertoEm = agora;
        ciclo.ReabertoPor = ctx.Email;
        ciclo.AlteradoEm = agora;
        ciclo.AlteradoPor = ctx.Email;
        if (ciclo.Tipo == PeDominios.TipoCiclo.Avaliacao) ciclo.Fim = null;
        await _context.SaveChangesAsync();
        return (await RespostasAsync(pdtic, trilha, new List<PeCiclo> { ciclo }, ctx))[0];
    }

    /// <summary>
    /// O que falta para fechar. Monitoramento: a situação de cada ação do PDTIC no ciclo (e os
    /// obrigatórios do registro, pelo nível) e, com o passo 5.2 na trilha, o resumo do ciclo.
    /// Avaliação: as seções obrigatórias dos passos da avaliação (6.1 a 6.3), com a decisão do
    /// comitê. Passo marcado "não se aplica" não pede nada.
    /// </summary>
    private async Task<List<PePendenciaResponse>> PendenciasDoFechamentoAsync(PePdtic pdtic, PeTrilhaOrgao trilha, PeCiclo ciclo)
    {
        var pendencias = new List<PePendenciaResponse>();
        var naoSeAplica = (await _context.PePdticPassos.AsNoTracking()
                .Where(p => p.PdticId == pdtic.Id && p.NaoSeAplica)
                .Select(p => p.PassoId)
                .ToListAsync())
            .Where(id => trilha.Passo(id) is { } passo && PePdticService.RecusaDoNaoSeAplica(passo) == null)
            .ToHashSet();
        var dono = PeDono.DoPdtic(pdtic.Id);

        if (ciclo.Tipo == PeDominios.TipoCiclo.Monitoramento)
        {
            if (trilha.Secao(PeDominios.ChaveAcompanhamento.SecaoMonitoramentoAcoes) is { } situacao && !naoSeAplica.Contains(situacao.Passo.Id))
            {
                var dados = await PeDadosDoAcompanhamento.CarregarAsync(_registros, pdtic, trilha,
                    PeDominios.TemaDecreto.SecaoAcoes, PeDominios.ChaveAcompanhamento.SecaoMonitoramentoAcoes);
                var doCiclo = dados.DoCiclo(PeDominios.ChaveAcompanhamento.SecaoMonitoramentoAcoes, ciclo.Id);
                var comSituacao = doCiclo
                    .Where(r => PeDadosDoAcompanhamento.Texto(r, PeDominios.ChaveAcompanhamento.CampoSituacao) != null)
                    .SelectMany(r => PeDadosDoAcompanhamento.Ligados(r, PeDominios.ChaveAcompanhamento.CampoAcao))
                    .ToHashSet();
                var faltam = dados.Registros(PeDominios.TemaDecreto.SecaoAcoes)
                    .Where(a => !comSituacao.Contains(a.Id))
                    .Select(a => a.Codigo ?? PeValores.Resumo(PeDadosDoAcompanhamento.Texto(a, PeDominios.TemaDecreto.CampoDescricao) ?? "ação"))
                    .ToList();
                if (faltam.Count > 0)
                    pendencias.Add(Pendencia(situacao.Passo, faltam.Count == 1
                        ? $"Registre a situação da ação {faltam[0]} neste ciclo."
                        : $"Registre a situação das ações {PePdticService.Lista(faltam)} neste ciclo."));

                // Registros do ciclo com obrigatório vazio pelo nível de hoje
                var analisada = (await _registros.AnalisarAsync(dono, new[] { trilha.Montar(situacao.Secao) }, ciclo.Id)).Secoes[0];
                foreach (var (registro, campos) in analisada.Incompletos.Take(3))
                {
                    var acao = await CodigoDaAcaoAsync(registro.Id);
                    pendencias.Add(Pendencia(situacao.Passo,
                        $"{acao}: preencha {PePdticService.Lista(campos.Select(c => $"\"{c.Rotulo}\"").ToList())}."));
                }
            }

            if (trilha.Passos.FirstOrDefault(p => p.Chave == PeDominios.ChaveAcompanhamento.PassoRelatorioAcompanhamento) is { } resumo
                && !naoSeAplica.Contains(resumo.Id))
            {
                var secoes = resumo.Secoes.Select(trilha.Montar).ToList();
                var analise = await _registros.AnalisarAsync(dono, secoes, ciclo.Id);
                if (PePdticService.FaltaNasSecoes(resumo, analise.Secoes.ToDictionary(s => s.Secao.Secao.Id)) is string falta)
                    pendencias.Add(Pendencia(resumo, falta));
            }
            return pendencias;
        }

        // Avaliação intermediária: os passos com seção da avaliação
        foreach (var passo in trilha.Passos.Where(p => p.Secoes.Any(s => s.PorCiclo == PeDominios.TipoCiclo.Avaliacao)))
        {
            if (naoSeAplica.Contains(passo.Id)) continue;
            var secoes = passo.Secoes.Select(trilha.Montar).ToList();
            var porSecao = (await _registros.AnalisarAsync(dono, secoes, ciclo.Id)).Secoes.ToDictionary(s => s.Secao.Secao.Id);
            var falta = passo.Tipo == PeDominios.TipoPasso.Aprovacao
                ? PePdticService.FaltaNaAprovacao(passo, porSecao, new List<string>())
                : PePdticService.FaltaNasSecoes(passo, porSecao);
            if (falta != null) pendencias.Add(Pendencia(passo, falta));
        }
        return pendencias;
    }

    private async Task<string> CodigoDaAcaoAsync(long registroId)
    {
        var ligada = await (from v in _context.PeVinculos.AsNoTracking()
                            join r in _context.PeRegistros.AsNoTracking() on v.RegistroDestinoId equals r.Id
                            where v.RegistroOrigemId == registroId
                            select r.Codigo)
            .FirstOrDefaultAsync();
        return ligada ?? "Uma ação";
    }

    private static PePendenciaResponse Pendencia(PeTrilhaPasso passo, string motivo) => new()
    {
        PassoId = passo.Id,
        PassoNumero = passo.Numero,
        PassoTitulo = passo.Titulo,
        Motivo = motivo
    };

    /// <summary>O RA do ciclo sai no fechamento quando o modelo ra existe e, no monitoramento, o nível tem o passo 5.2.</summary>
    private async Task<bool> RaLigadoAsync(PeTrilhaOrgao trilha, PeCiclo ciclo)
    {
        if (await PeDocumentoService.ModeloAtivoAsync(_context, PeDominios.TipoDocumento.Ra) == null) return false;
        return ciclo.Tipo == PeDominios.TipoCiclo.Avaliacao
               || trilha.Passos.Any(p => p.Chave == PeDominios.ChaveAcompanhamento.PassoRelatorioAcompanhamento);
    }

    // ── Grades do ciclo ─────────────────────────────────────────────────────

    public async Task<List<PeCicloAcaoResponse>> AcoesAsync(long cicloId, PeUserContext ctx)
    {
        var (ciclo, pdtic, trilha) = await CicloParaLerAsync(cicloId, ctx, PeDominios.TipoCiclo.Monitoramento,
            "A situação das ações é registrada nos ciclos de monitoramento.");
        SecaoDaGrade(trilha, PeDominios.ChaveAcompanhamento.SecaoMonitoramentoAcoes);
        return await GradeDasAcoesAsync(pdtic, trilha, ciclo);
    }

    public async Task<List<PeCicloAcaoResponse>> SalvarAcoesAsync(long cicloId, PeCicloAcoesDTO dto, PeUserContext ctx)
    {
        var (ciclo, pdtic, trilha) = await CicloParaLerAsync(cicloId, ctx, PeDominios.TipoCiclo.Monitoramento,
            "A situação das ações é registrada nos ciclos de monitoramento.");
        SecaoDaGrade(trilha, PeDominios.ChaveAcompanhamento.SecaoMonitoramentoAcoes);
        if (!_permissoes.PodeEditarPdtic(ctx, pdtic.OrgaoId))
            throw new ApiException(ErrorCode.PeSemPermissao, "Quem registra a situação das ações é a equipe do órgão.");
        var itens = dto.Itens ?? new List<PeCicloAcaoItemDTO>();
        var grade = await GradeDasAcoesAsync(pdtic, trilha, ciclo);
        var porAcao = grade.ToDictionary(g => g.AcaoId);

        var erros = new Dictionary<string, string>();
        var linhas = new List<PeLinhaDoCiclo>();
        var vistas = new HashSet<long>();
        foreach (var item in itens)
        {
            if (item.AcaoId is not long acaoId)
            {
                erros["acao"] = "Cada linha precisa do AcaoId.";
                continue;
            }
            var prefixo = acaoId.ToString(CultureInfo.InvariantCulture);
            if (!vistas.Add(acaoId))
            {
                erros[$"{prefixo}.acao"] = "Esta ação está repetida na lista.";
                continue;
            }
            if (!porAcao.TryGetValue(acaoId, out var atual))
            {
                erros[$"{prefixo}.acao"] = "Esta ação não é do PDTIC. Atualize a tela.";
                continue;
            }
            var vazio = string.IsNullOrWhiteSpace(item.Situacao) && item.ExecucaoFisica == null && item.ExecucaoOrcamentaria == null
                        && string.IsNullOrWhiteSpace(item.Observacao);
            if (vazio && atual.RegistroId == null) continue;
            linhas.Add(new PeLinhaDoCiclo
            {
                Prefixo = prefixo,
                RegistroId = atual.RegistroId,
                // Tudo vazio num registro que já existe: a linha volta a ficar sem registro
                Dados = vazio ? null : new PeRegistroSalvarDTO
                {
                    Dados = new Dictionary<string, JsonElement>
                    {
                        [PeDominios.ChaveAcompanhamento.CampoSituacao] = Json(item.Situacao?.Trim()),
                        [PeDominios.ChaveAcompanhamento.CampoExecucaoFisica] = Json(item.ExecucaoFisica),
                        [PeDominios.ChaveAcompanhamento.CampoExecucaoOrcamentaria] = Json(item.ExecucaoOrcamentaria),
                        [PeDominios.ChaveAcompanhamento.CampoObservacao] = Json(item.Observacao)
                    },
                    Vinculos = new Dictionary<string, List<long>> { [PeDominios.ChaveAcompanhamento.CampoAcao] = new() { acaoId } }
                }
            });
        }
        if (erros.Count > 0) throw new PeValidacaoException(erros);
        await _registros.SalvarNoCicloAsync(PeDono.DoPdtic(pdtic.Id), PeDominios.ChaveAcompanhamento.SecaoMonitoramentoAcoes, ciclo.Id, linhas, ctx);
        _context.ChangeTracker.Clear();
        return await GradeDasAcoesAsync(await PdticAsync(pdtic.Id), trilha, ciclo);
    }

    private async Task<List<PeCicloAcaoResponse>> GradeDasAcoesAsync(PePdtic pdtic, PeTrilhaOrgao trilha, PeCiclo ciclo)
    {
        var dados = await PeDadosDoAcompanhamento.CarregarAsync(_registros, pdtic, trilha,
            PeDominios.TemaDecreto.SecaoAcoes, PeDominios.ChaveAcompanhamento.SecaoMonitoramentoAcoes);
        var anteriores = await CiclosAnterioresAsync(pdtic, ciclo);
        const string secao = PeDominios.ChaveAcompanhamento.SecaoMonitoramentoAcoes;

        // O registro de cada ação por ciclo (o primeiro pela ordem, se houver mais de um)
        PeRegistroResponse? DaAcao(long cicloId, long acaoId) => dados.DoCiclo(secao, cicloId)
            .FirstOrDefault(r => PeDadosDoAcompanhamento.Ligados(r, PeDominios.ChaveAcompanhamento.CampoAcao).Contains(acaoId));

        return dados.Registros(PeDominios.TemaDecreto.SecaoAcoes).Select(acao =>
        {
            var registro = DaAcao(ciclo.Id, acao.Id);
            var (anterior, deOnde) = anteriores
                .Select(c => (Registro: DaAcao(c.Id, acao.Id), Ciclo: c))
                .FirstOrDefault(x => x.Registro != null);
            return new PeCicloAcaoResponse
            {
                AcaoId = acao.Id,
                Codigo = acao.Codigo,
                Descricao = PeDadosDoAcompanhamento.Texto(acao, PeDominios.TemaDecreto.CampoDescricao) ?? string.Empty,
                Metas = PeDadosDoAcompanhamento.CodigosLigados(acao, PeDominios.ChaveAcompanhamento.CampoMetas),
                InicioPrevisto = PeDadosDoAcompanhamento.Data(acao, PeDominios.ChaveAcompanhamento.CampoInicio),
                ConclusaoPrevista = PeDadosDoAcompanhamento.Data(acao, PeDominios.ChaveAcompanhamento.CampoConclusao),
                RegistroId = registro?.Id,
                Situacao = registro == null ? null : PeDadosDoAcompanhamento.Texto(registro, PeDominios.ChaveAcompanhamento.CampoSituacao),
                ExecucaoFisica = registro == null ? null : PeDadosDoAcompanhamento.Numero(registro, PeDominios.ChaveAcompanhamento.CampoExecucaoFisica),
                ExecucaoOrcamentaria = registro == null ? null
                    : PeDadosDoAcompanhamento.Numero(registro, PeDominios.ChaveAcompanhamento.CampoExecucaoOrcamentaria),
                Observacao = registro == null ? null : PeDadosDoAcompanhamento.Texto(registro, PeDominios.ChaveAcompanhamento.CampoObservacao),
                Anterior = anterior == null
                    ? null
                    : new PeCicloAcaoAnteriorResponse
                    {
                        Situacao = PeDadosDoAcompanhamento.Texto(anterior, PeDominios.ChaveAcompanhamento.CampoSituacao),
                        ExecucaoFisica = PeDadosDoAcompanhamento.Numero(anterior, PeDominios.ChaveAcompanhamento.CampoExecucaoFisica),
                        ExecucaoOrcamentaria = PeDadosDoAcompanhamento.Numero(anterior, PeDominios.ChaveAcompanhamento.CampoExecucaoOrcamentaria),
                        Ciclo = deOnde!.Rotulo
                    }
            };
        }).ToList();
    }

    public async Task<List<PeCicloMedicaoResponse>> MedicoesAsync(long cicloId, PeUserContext ctx)
    {
        var (ciclo, pdtic, trilha) = await CicloParaLerAsync(cicloId, ctx, PeDominios.TipoCiclo.Monitoramento,
            "As medições dos indicadores são registradas nos ciclos de monitoramento.");
        SecaoDaGrade(trilha, PeDominios.ChaveAcompanhamento.SecaoMedicoes);
        return await GradeDasMedicoesAsync(pdtic, trilha, ciclo);
    }

    public async Task<List<PeCicloMedicaoResponse>> SalvarMedicoesAsync(long cicloId, PeCicloMedicoesDTO dto, PeUserContext ctx)
    {
        var (ciclo, pdtic, trilha) = await CicloParaLerAsync(cicloId, ctx, PeDominios.TipoCiclo.Monitoramento,
            "As medições dos indicadores são registradas nos ciclos de monitoramento.");
        SecaoDaGrade(trilha, PeDominios.ChaveAcompanhamento.SecaoMedicoes);
        if (!_permissoes.PodeEditarPdtic(ctx, pdtic.OrgaoId))
            throw new ApiException(ErrorCode.PeSemPermissao, "Quem registra as medições é a equipe do órgão.");
        var grade = await GradeDasMedicoesAsync(pdtic, trilha, ciclo);
        var porIndicador = grade.ToDictionary(g => g.IndicadorId);

        var erros = new Dictionary<string, string>();
        var linhas = new List<PeLinhaDoCiclo>();
        var vistos = new HashSet<long>();
        foreach (var item in dto.Itens ?? new List<PeCicloMedicaoItemDTO>())
        {
            if (item.IndicadorId is not long indicadorId)
            {
                erros["indicador"] = "Cada linha precisa do IndicadorId.";
                continue;
            }
            var prefixo = indicadorId.ToString(CultureInfo.InvariantCulture);
            if (!vistos.Add(indicadorId))
            {
                erros[$"{prefixo}.indicador"] = "Este indicador está repetido na lista.";
                continue;
            }
            if (!porIndicador.TryGetValue(indicadorId, out var atual))
            {
                erros[$"{prefixo}.indicador"] = "Este indicador não é do plano de monitoramento do PDTIC. Atualize a tela.";
                continue;
            }
            var vazio = item.ValorApurado == null && string.IsNullOrWhiteSpace(item.Data) && string.IsNullOrWhiteSpace(item.Observacao);
            if (vazio && atual.RegistroId == null) continue;
            linhas.Add(new PeLinhaDoCiclo
            {
                Prefixo = prefixo,
                RegistroId = atual.RegistroId,
                Dados = vazio ? null : new PeRegistroSalvarDTO
                {
                    Dados = new Dictionary<string, JsonElement>
                    {
                        [PeDominios.ChaveAcompanhamento.CampoValorApurado] = Json(item.ValorApurado),
                        [PeDominios.ChaveAcompanhamento.CampoData] = Json(item.Data?.Trim()),
                        [PeDominios.ChaveAcompanhamento.CampoObservacao] = Json(item.Observacao)
                    },
                    Vinculos = new Dictionary<string, List<long>> { [PeDominios.ChaveAcompanhamento.CampoIndicador] = new() { indicadorId } }
                }
            });
        }
        if (erros.Count > 0) throw new PeValidacaoException(erros);
        await _registros.SalvarNoCicloAsync(PeDono.DoPdtic(pdtic.Id), PeDominios.ChaveAcompanhamento.SecaoMedicoes, ciclo.Id, linhas, ctx);
        _context.ChangeTracker.Clear();
        return await GradeDasMedicoesAsync(await PdticAsync(pdtic.Id), trilha, ciclo);
    }

    private async Task<List<PeCicloMedicaoResponse>> GradeDasMedicoesAsync(PePdtic pdtic, PeTrilhaOrgao trilha, PeCiclo ciclo)
    {
        var dados = await PeDadosDoAcompanhamento.CarregarAsync(_registros, pdtic, trilha,
            PeDominios.ChaveAcompanhamento.SecaoIndicadoresMonitoramento, PeDominios.ChaveAcompanhamento.SecaoMedicoes);
        var anteriores = await CiclosAnterioresAsync(pdtic, ciclo);
        const string secao = PeDominios.ChaveAcompanhamento.SecaoMedicoes;

        PeRegistroResponse? DoIndicador(long cicloId, long indicadorId) => dados.DoCiclo(secao, cicloId)
            .FirstOrDefault(r => PeDadosDoAcompanhamento.Ligados(r, PeDominios.ChaveAcompanhamento.CampoIndicador).Contains(indicadorId));

        return dados.Registros(PeDominios.ChaveAcompanhamento.SecaoIndicadoresMonitoramento).Select(indicador =>
        {
            var registro = DoIndicador(ciclo.Id, indicador.Id);
            var (anterior, deOnde) = anteriores
                .Select(c => (Registro: DoIndicador(c.Id, indicador.Id), Ciclo: c))
                .FirstOrDefault(x => x.Registro != null);
            return new PeCicloMedicaoResponse
            {
                IndicadorId = indicador.Id,
                Codigo = indicador.Codigo,
                Indicador = PeDadosDoAcompanhamento.Texto(indicador, PeDominios.ChaveAcompanhamento.CampoIndicador) ?? string.Empty,
                ValoresReferencia = PeDadosDoAcompanhamento.Texto(indicador, PeDominios.ChaveAcompanhamento.CampoValoresReferencia),
                RegistroId = registro?.Id,
                ValorApurado = registro == null ? null : PeDadosDoAcompanhamento.Numero(registro, PeDominios.ChaveAcompanhamento.CampoValorApurado),
                Data = registro == null ? null : PeDadosDoAcompanhamento.Data(registro, PeDominios.ChaveAcompanhamento.CampoData),
                Observacao = registro == null ? null : PeDadosDoAcompanhamento.Texto(registro, PeDominios.ChaveAcompanhamento.CampoObservacao),
                Anterior = anterior == null
                    ? null
                    : new PeCicloMedicaoAnteriorResponse
                    {
                        ValorApurado = PeDadosDoAcompanhamento.Numero(anterior, PeDominios.ChaveAcompanhamento.CampoValorApurado),
                        Data = PeDadosDoAcompanhamento.Data(anterior, PeDominios.ChaveAcompanhamento.CampoData),
                        Ciclo = deOnde!.Rotulo
                    }
            };
        }).ToList();
    }

    /// <summary>Os ciclos de monitoramento que começaram antes deste, do mais recente para o mais antigo.</summary>
    private async Task<List<PeCiclo>> CiclosAnterioresAsync(PePdtic pdtic, PeCiclo ciclo) =>
        (await _context.PeCiclos.AsNoTracking()
            .Where(c => c.PdticId == pdtic.Id && c.Tipo == PeDominios.TipoCiclo.Monitoramento && c.Inicio < ciclo.Inicio)
            .ToListAsync())
        .OrderByDescending(c => c.Inicio)
        .ToList();

    /// <summary>A seção da grade precisa estar na trilha do órgão (senão 404, como no motor).</summary>
    private static void SecaoDaGrade(PeTrilhaOrgao trilha, string chave)
    {
        if (trilha.Secao(chave) == null)
            throw new ApiException(ErrorCode.PeSecaoIndisponivel, "Esta seção não aparece no nível do órgão.");
    }

    private static JsonElement Json(object? valor) => JsonSerializer.SerializeToElement(valor);

    // ── Painel do PDTIC (AC-PDTIC) ──────────────────────────────────────────

    public async Task<PePainelResponse> PainelAsync(long pdticId, long? cicloId, PeUserContext ctx)
    {
        var pdtic = await PePdticService.LerAsync(_context, _permissoes, pdticId, ctx);
        var trilha = await PeTrilhaOrgao.DoPdticAsync(_context, pdtic);
        trilha.Dados.Acompanhamento.Exigir();

        var ciclos = (await _context.PeCiclos.AsNoTracking()
                .Where(c => c.PdticId == pdtic.Id && c.Tipo == PeDominios.TipoCiclo.Monitoramento)
                .ToListAsync())
            .OrderBy(c => c.Inicio).ThenBy(c => c.Id)
            .ToList();
        var dados = await PeDadosDoAcompanhamento.CarregarAsync(_registros, pdtic, trilha,
            PeDominios.ChaveAcompanhamento.SecaoMetas, PeDominios.TemaDecreto.SecaoAcoes, PeDominios.ChaveAcompanhamento.SecaoProjetos,
            PeDominios.ChaveAcompanhamento.SecaoMonitoramentoAcoes, PeDominios.ChavePdtic.SecaoRiscos,
            PeDominios.ChaveAcompanhamento.SecaoRiscosOcorridos);

        PeCiclo? referencia;
        if (cicloId != null)
        {
            var escolhido = await _context.PeCiclos.AsNoTracking().FirstOrDefaultAsync(c => c.Id == cicloId && c.PdticId == pdtic.Id)
                            ?? throw PeCiclos.NaoEncontrado();
            if (escolhido.Tipo != PeDominios.TipoCiclo.Monitoramento)
                throw new ApiException(ErrorCode.PeCicloInvalido, "O painel é de um ciclo de monitoramento.");
            referencia = escolhido;
        }
        else
        {
            // O último ciclo com dado (a situação de uma ação ou a ocorrência de um risco)
            referencia = ciclos.LastOrDefault(c =>
                dados.DoCiclo(PeDominios.ChaveAcompanhamento.SecaoMonitoramentoAcoes, c.Id).Count > 0
                || dados.DoCiclo(PeDominios.ChaveAcompanhamento.SecaoRiscosOcorridos, c.Id).Count > 0);
        }
        var ate = referencia == null
            ? new List<PeCiclo>()
            : ciclos.Where(c => c.Inicio <= referencia.Inicio).OrderByDescending(c => c.Inicio).ToList();
        return Painel(dados, referencia, ate, PeRegrasDoPainel.Referencia(referencia, PeCiclos.Hoje()));
    }

    /// <summary>O painel com os dados lidos: cada ação pelo último registro nos ciclos até a referência (do mais recente para o mais antigo).</summary>
    public static PePainelResponse Painel(PeDadosDoAcompanhamento dados, PeCiclo? referencia, IReadOnlyList<PeCiclo> ateAReferencia, DateOnly dataReferencia)
    {
        const string monitoramento = PeDominios.ChaveAcompanhamento.SecaoMonitoramentoAcoes;
        PeRegistroResponse? UltimoDaAcao(long acaoId) => ateAReferencia
            .Select(c => dados.DoCiclo(monitoramento, c.Id)
                .FirstOrDefault(r => PeDadosDoAcompanhamento.Ligados(r, PeDominios.ChaveAcompanhamento.CampoAcao).Contains(acaoId)))
            .FirstOrDefault(r => r != null);

        // O peso da ação na meta (plano de execução, 4.2): o primeiro projeto da ação que diz
        var pesos = new Dictionary<long, decimal>();
        foreach (var projeto in dados.Registros(PeDominios.ChaveAcompanhamento.SecaoProjetos))
            if (PeDadosDoAcompanhamento.Numero(projeto, PeDominios.ChaveAcompanhamento.CampoPesoDaAcaoNaMeta) is decimal peso)
                foreach (var acaoId in PeDadosDoAcompanhamento.Ligados(projeto, PeDominios.ChaveAcompanhamento.CampoAcao))
                    pesos.TryAdd(acaoId, peso);

        var acoes = dados.Registros(PeDominios.TemaDecreto.SecaoAcoes).Select(acao =>
        {
            var registro = UltimoDaAcao(acao.Id);
            var situacao = registro == null ? null : PeDadosDoAcompanhamento.Texto(registro, PeDominios.ChaveAcompanhamento.CampoSituacao);
            var naoSeAplica = PeRegrasDoPainel.OrcamentoNaoSeAplica(
                PeDadosDoAcompanhamento.Numero(acao, PeDominios.ChaveAcompanhamento.CampoInvestimento),
                PeDadosDoAcompanhamento.Numero(acao, PeDominios.ChaveAcompanhamento.CampoCusteio));
            var resposta = new PePainelAcaoResponse
            {
                AcaoId = acao.Id,
                Codigo = acao.Codigo,
                Descricao = PeDadosDoAcompanhamento.Texto(acao, PeDominios.TemaDecreto.CampoDescricao) ?? string.Empty,
                PercentualExecucao = registro == null
                    ? null
                    : PeRegrasDoPainel.PercentualDaAcao(PeDadosDoAcompanhamento.Numero(registro, PeDominios.ChaveAcompanhamento.CampoExecucaoFisica), situacao),
                SituacaoFisica = PeRegrasDoPainel.SituacaoFisica(registro != null, situacao,
                    PeDadosDoAcompanhamento.Data(acao, PeDominios.ChaveAcompanhamento.CampoInicio),
                    PeDadosDoAcompanhamento.Data(acao, PeDominios.ChaveAcompanhamento.CampoConclusao), dataReferencia),
                OrcamentoNaoSeAplica = naoSeAplica,
                ExecucaoOrcamentaria = naoSeAplica || registro == null
                    ? null
                    : PeDadosDoAcompanhamento.Numero(registro, PeDominios.ChaveAcompanhamento.CampoExecucaoOrcamentaria)
            };
            return (Acao: acao, Resposta: resposta, Metas: PeDadosDoAcompanhamento.Ligados(acao, PeDominios.ChaveAcompanhamento.CampoMetas));
        }).ToList();

        var metas = dados.Registros(PeDominios.ChaveAcompanhamento.SecaoMetas).Select(meta =>
        {
            var ligadas = acoes.Where(a => a.Metas.Contains(meta.Id)).ToList();
            return new PePainelMetaResponse
            {
                MetaId = meta.Id,
                Codigo = meta.Codigo,
                Descricao = PeDadosDoAcompanhamento.Texto(meta, PeDominios.ChaveAcompanhamento.CampoDescricao) ?? string.Empty,
                Indicador = PeDadosDoAcompanhamento.Texto(meta, PeDominios.ChaveAcompanhamento.CampoIndicador),
                Valor = PeDadosDoAcompanhamento.Texto(meta, PeDominios.ChaveAcompanhamento.CampoValorDaMeta),
                Prazo = PeDadosDoAcompanhamento.Data(meta, PeDominios.ChaveAcompanhamento.CampoPrazo),
                Necessidades = PeDadosDoAcompanhamento.CodigosLigados(meta, PeDominios.ChaveAcompanhamento.CampoNecessidades),
                PercentualExecucao = PeRegrasDoPainel.PercentualDaMeta(ligadas
                    .Select(a => (a.Resposta.PercentualExecucao, pesos.TryGetValue(a.Acao.Id, out var p) ? (decimal?)p : null,
                        a.Resposta.SituacaoFisica == PeDominios.SituacaoFisica.Cancelada))
                    .ToList()),
                Acoes = ligadas.Select(a => a.Resposta).ToList()
            };
        }).ToList();

        return new PePainelResponse
        {
            Ciclo = referencia == null ? null : new PeCicloReferenciaResponse { Id = referencia.Id, Rotulo = referencia.Rotulo },
            Metas = metas,
            AcoesSemMeta = acoes.Where(a => a.Metas.Count == 0).Select(a => a.Resposta).ToList(),
            Riscos = Riscos(dados, ateAReferencia)
        };
    }

    /// <summary>
    /// Os quadros dos riscos: cada risco do plano pela última ocorrência nos ciclos até a
    /// referência (a data mais recente; no empate, o ciclo mais recente), sem ocorrência à parte;
    /// por nível (o calculado ou, no Básico, o simples) e a matriz das duas coisas.
    /// </summary>
    private static PePainelRiscosResponse Riscos(PeDadosDoAcompanhamento dados, IReadOnlyList<PeCiclo> ateAReferencia)
    {
        var ordemDoCiclo = ateAReferencia.Select((c, i) => (c.Id, i)).ToDictionary(x => x.Id, x => x.i);
        var ocorrencias = dados.Registros(PeDominios.ChaveAcompanhamento.SecaoRiscosOcorridos)
            .Select(r => (Registro: r, Ciclo: dados.CicloDe(PeDominios.ChaveAcompanhamento.SecaoRiscosOcorridos, r.Id)))
            .Where(x => x.Ciclo is long c && ordemDoCiclo.ContainsKey(c))
            .OrderByDescending(x => PeDadosDoAcompanhamento.Data(x.Registro, PeDominios.ChaveAcompanhamento.CampoData) ?? DateOnly.MinValue)
            .ThenBy(x => ordemDoCiclo[x.Ciclo!.Value])
            .ThenByDescending(x => x.Registro.Ordem)
            .ToList();

        var porSituacao = PeDominios.ChaveAcompanhamento.SituacoesDoRisco.ToDictionary(s => s, _ => 0);
        var porNivel = PeDominios.ChaveAcompanhamento.NiveisDoRisco.ToDictionary(n => n, _ => 0);
        var matriz = new Dictionary<(string, string), int>();
        foreach (var risco in dados.Registros(PeDominios.ChavePdtic.SecaoRiscos))
        {
            var ultima = ocorrencias.FirstOrDefault(o =>
                PeDadosDoAcompanhamento.Ligados(o.Registro, PeDominios.ChaveAcompanhamento.CampoRisco).Contains(risco.Id)).Registro;
            var situacao = ultima == null
                ? PeDominios.ChaveAcompanhamento.SemOcorrencia
                : PeDadosDoAcompanhamento.Texto(ultima, PeDominios.ChaveAcompanhamento.CampoSituacao) ?? PeDominios.ChaveAcompanhamento.RiscoAberto;
            if (!porSituacao.ContainsKey(situacao)) situacao = PeDominios.ChaveAcompanhamento.RiscoAberto;
            porSituacao[situacao]++;

            var nivel = PeDadosDoAcompanhamento.Texto(risco, PeDominios.ChaveAcompanhamento.CampoNivelRisco)
                        ?? PeDadosDoAcompanhamento.Texto(risco, PeDominios.ChaveAcompanhamento.CampoNivelRiscoSimples);
            if (nivel == null || !porNivel.ContainsKey(nivel)) continue;
            porNivel[nivel]++;
            matriz[(situacao, nivel)] = matriz.GetValueOrDefault((situacao, nivel)) + 1;
        }

        return new PePainelRiscosResponse
        {
            PorSituacao = porSituacao,
            PorNivel = porNivel,
            Matriz = PeDominios.ChaveAcompanhamento.SituacoesDoRisco
                .SelectMany(s => PeDominios.ChaveAcompanhamento.NiveisDoRisco.Select(n => new PePainelMatrizResponse
                {
                    Situacao = s,
                    Nivel = n,
                    Quantidade = matriz.GetValueOrDefault((s, n))
                }))
                .ToList()
        };
    }

    // ── Apoio ───────────────────────────────────────────────────────────────

    /// <summary>O ciclo para ler (quem vê o órgão), do tipo dado (senão 400), com o PDTIC e a trilha.</summary>
    private async Task<(PeCiclo Ciclo, PePdtic Pdtic, PeTrilhaOrgao Trilha)> CicloParaLerAsync(long cicloId, PeUserContext ctx, string tipo,
        string mensagemDoTipo)
    {
        var ciclo = await CicloAsync(cicloId, rastrear: false);
        var pdtic = await PePdticService.LerAsync(_context, _permissoes, ciclo.PdticId, ctx);
        var trilha = await PeTrilhaOrgao.DoPdticAsync(_context, pdtic);
        trilha.Dados.Acompanhamento.Exigir();
        if (ciclo.Tipo != tipo) throw new ApiException(ErrorCode.PeCicloInvalido, mensagemDoTipo);
        return (ciclo, pdtic, trilha);
    }

    /// <summary>O ciclo para fechar ou reabrir: a equipe do órgão, com o PDTIC vigente; ciclo e PDTIC rastreados.</summary>
    private async Task<(PeCiclo Ciclo, PePdtic Pdtic, PeTrilhaOrgao Trilha)> CicloParaGravarAsync(long cicloId, PeUserContext ctx, string soAEquipe)
    {
        var ciclo = await CicloAsync(cicloId, rastrear: true);
        var pdtic = await PePdticService.LerAsync(_context, _permissoes, ciclo.PdticId, ctx, rastrear: true);
        var trilha = await PeTrilhaOrgao.DoPdticAsync(_context, pdtic);
        trilha.Dados.Acompanhamento.Exigir();
        ConferirGravacao(pdtic, ctx, soAEquipe);
        return (ciclo, pdtic, trilha);
    }

    private async Task<PeCiclo> CicloAsync(long cicloId, bool rastrear)
    {
        // Sem a tabela (intervalo do deploy), a consulta cai no 42P01 e o controller responde 409 com corpo
        var consulta = rastrear ? _context.PeCiclos : _context.PeCiclos.AsNoTracking();
        return await consulta.FirstOrDefaultAsync(c => c.Id == cicloId) ?? throw PeCiclos.NaoEncontrado();
    }

    /// <summary>Gravar no acompanhamento: a equipe do órgão (e o admin geral), com o PDTIC vigente (publicado ou em acompanhamento).</summary>
    private void ConferirGravacao(PePdtic pdtic, PeUserContext ctx, string soAEquipe)
    {
        if (!_permissoes.PodeEditarPdtic(ctx, pdtic.OrgaoId)) throw new ApiException(ErrorCode.PeSemPermissao, soAEquipe);
        if (!PeDominios.SituacaoPdtic.Vigentes.Contains(pdtic.Situacao))
            throw new ApiException(ErrorCode.PePdticSituacaoInvalida, PeDominios.SituacaoPdtic.DaElaboracao.Contains(pdtic.Situacao)
                ? "O acompanhamento começa depois da publicação do PDTIC."
                : $"Este PDTIC está {PeDominios.SituacaoPdtic.RotuloMinusculo(pdtic.Situacao)} e o acompanhamento dele não muda mais.");
    }

    private static bool TemSecoesDoTipo(PeTrilhaOrgao trilha, string tipo) =>
        trilha.Passos.SelectMany(p => p.Secoes).Any(s => s.PorCiclo == tipo);

    private async Task<PePdtic> PdticAsync(long id) => await _context.PePdtics.AsNoTracking().FirstAsync(p => p.Id == id);

    /// <summary>As respostas dos ciclos: a situação exibida, o resumo do que foi registrado, o último RA e se quem chama grava.</summary>
    private async Task<List<PeCicloResponse>> RespostasAsync(PePdtic pdtic, PeTrilhaOrgao trilha, IReadOnlyList<PeCiclo> ciclos, PeUserContext ctx)
    {
        if (ciclos.Count == 0) return new List<PeCicloResponse>();
        var hoje = PeCiclos.Hoje();
        var chaves = new List<string>
        {
            PeDominios.TemaDecreto.SecaoAcoes, PeDominios.ChaveAcompanhamento.SecaoMonitoramentoAcoes,
            PeDominios.ChaveAcompanhamento.SecaoMedicoes, PeDominios.ChaveAcompanhamento.SecaoRiscosOcorridos
        };
        // F1 (I04 e C23): o que a avaliação intermediária tem (resultados das metas, análise e decisão do comitê)
        if (ciclos.Any(c => c.Tipo == PeDominios.TipoCiclo.Avaliacao))
            chaves.AddRange(new[]
            {
                PeDominios.ChaveAcompanhamento.SecaoResultadosIntermediarios, PeDominios.ChaveAcompanhamento.SecaoAnaliseIntermediaria,
                PeDominios.ChavePdtic.SecaoAvaliacaoComite
            });
        var dados = await PeDadosDoAcompanhamento.CarregarAsync(_registros, pdtic, trilha, chaves.ToArray());
        var nomes = await PeNomes.CarregarAsync(_context, ciclos.SelectMany(c => new[] { c.FechadoPor, c.ReabertoPor }));
        var ids = ciclos.Select(c => c.Id).ToList();
        var relatorios = (await (from v in _context.PeDocVersoes.AsNoTracking()
                                 join d in _context.PeDocVersoesDocumento.AsNoTracking() on v.Id equals d.Id
                                 where d.DocTipo == PeDominios.TipoDocumento.Ra && d.CicloId != null && ids.Contains(d.CicloId.Value)
                                 select new { CicloId = d.CicloId!.Value, v.Numero, v.Situacao, v.GeradoEm })
                .ToListAsync())
            .GroupBy(v => v.CicloId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(v => v.Numero).First());
        var papelEdita = _permissoes.PodeEditarPdtic(ctx, pdtic.OrgaoId) && PeDominios.SituacaoPdtic.Vigentes.Contains(pdtic.Situacao);
        var totalAcoes = dados.Registros(PeDominios.TemaDecreto.SecaoAcoes).Count;

        return ciclos.Select(c =>
        {
            var acoesComSituacao = dados.DoCiclo(PeDominios.ChaveAcompanhamento.SecaoMonitoramentoAcoes, c.Id)
                .Where(r => PeDadosDoAcompanhamento.Texto(r, PeDominios.ChaveAcompanhamento.CampoSituacao) != null)
                .SelectMany(r => PeDadosDoAcompanhamento.Ligados(r, PeDominios.ChaveAcompanhamento.CampoAcao))
                .Distinct()
                .Count();
            var decisao = c.Tipo == PeDominios.TipoCiclo.Avaliacao
                ? dados.DoCiclo(PeDominios.ChavePdtic.SecaoAvaliacaoComite, c.Id).FirstOrDefault()
                : null;
            return new PeCicloResponse
            {
                Id = c.Id,
                Tipo = c.Tipo,
                Numero = c.Numero,
                Rotulo = c.Rotulo,
                Inicio = c.Inicio,
                Fim = c.Fim,
                Prazo = c.Prazo,
                Situacao = PeCiclos.Exibida(c, hoje),
                FechadoEm = c.FechadoEm,
                FechadoPor = c.FechadoPor,
                FechadoPorNome = nomes.De(c.FechadoPor),
                ReabertoEm = c.ReabertoEm,
                ReabertoPor = c.ReabertoPor,
                ReabertoPorNome = nomes.De(c.ReabertoPor),
                PodeEditar = papelEdita && PeCiclos.RecusaDeDados(c, hoje) == null,
                Resumo = new PeCicloResumoResponse
                {
                    AcoesComSituacao = acoesComSituacao,
                    TotalAcoes = totalAcoes,
                    Medicoes = dados.DoCiclo(PeDominios.ChaveAcompanhamento.SecaoMedicoes, c.Id).Count,
                    RiscosOcorridos = dados.DoCiclo(PeDominios.ChaveAcompanhamento.SecaoRiscosOcorridos, c.Id).Count,
                    ResultadosMetas = c.Tipo == PeDominios.TipoCiclo.Avaliacao
                        ? dados.DoCiclo(PeDominios.ChaveAcompanhamento.SecaoResultadosIntermediarios, c.Id).Count
                        : 0,
                    AnaliseRegistrada = c.Tipo == PeDominios.TipoCiclo.Avaliacao
                                        && dados.DoCiclo(PeDominios.ChaveAcompanhamento.SecaoAnaliseIntermediaria, c.Id).Count > 0,
                    DecisaoComite = decisao == null ? null : PeDadosDoAcompanhamento.Texto(decisao, PeDominios.ChavePdtic.CampoDecisao),
                    DecisaoComiteRotulo = decisao != null && decisao.Rotulos.TryGetValue(PeDominios.ChavePdtic.CampoDecisao, out var rotulo)
                        ? rotulo
                        : null
                },
                Relatorio = relatorios.TryGetValue(c.Id, out var relatorio)
                    ? new PeCicloRelatorioResponse { Numero = relatorio.Numero, Situacao = relatorio.Situacao, GeradoEm = relatorio.GeradoEm }
                    : null
            };
        }).ToList();
    }
}
