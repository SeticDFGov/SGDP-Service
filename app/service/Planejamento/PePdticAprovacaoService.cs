using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using api.Planejamento;
using app.Auth;
using demanda_service.Helpers;
using Microsoft.EntityFrameworkCore;
using Models;
using Models.Planejamento;
using service.Interface;

namespace service.Planejamento;

/// <summary>
/// O caminho da aprovação do PDTIC (E7, rodada A; decisão 18 do plano):
/// <list type="bullet">
/// <item>enviar ao CGTIC (em elaboração ou devolvido, pela equipe do órgão): exige os passos
/// obrigatórios das etapas 1 a 3 anteriores ao envio feitos (ou "não se aplica") e a aprovação
/// do SGTIC com a decisão "aprovado" e a data; gera o PDF como a versão "enviada", cria a
/// deliberação na fila da Secretaria (com o PDF) e põe o PDTIC em aprovação. O envio vale
/// como a comunicação do art. 7º, V, do Decreto nº 48.899/2026. A decisão fica no
/// PeDeliberacaoService;</item>
/// <item>publicar (aprovado, pela equipe): exige a data e o endereço da seção da publicação;
/// a versão aprovada do documento vira "publicada";</item>
/// <item>encerrar: a equipe, com a aprovação da autoridade máxima (7.4) "aprovado", no PDTIC
/// publicado ou em acompanhamento; o administrador do módulo e o admin geral, com o motivo,
/// depois do fim da vigência;</item>
/// <item>revisar (o PDTIC vigente, pela equipe): cria a versão seguinte ("1.1") em
/// elaboração, com a cópia dos registros (códigos, sequências e ligações refeitas), dos
/// fluxos, dos textos e capítulos do documento e dos "não se aplica"; sem os registros dos
/// passos de aprovação, de envio e de publicação, sem comentários, deliberações nem versões do
/// documento. Exige a avaliação do comitê (6.3) com a decisão "revisar" quando o passo está na
/// trilha do órgão; senão, a justificativa;</item>
/// <item>registrar um PDTIC aprovado fora do sistema (decisão 20): já publicado, com a
/// vigência, a publicação, o PDF como a versão 1 publicada e, quando o CGTIC aprovou, a
/// deliberação aprovada com o ato; quando outra instância aprovou, a aprovação vai para a
/// seção da aprovação do SGTIC (instância "outra").</item>
/// </list>
/// A situação do PDTIC é token de concorrência: duas transições ao mesmo tempo não passam as
/// duas (a segunda recebe 409 PeConflitoGravacao).
/// </summary>
public partial class PePdticAprovacaoService : IPePdticAprovacaoService
{
    public const int MaximoTexto = 1000;
    private const int MaximoEndereco = 500;

    [GeneratedRegex(@"^\d{1,3}\.\d{1,3}$")]
    private static partial Regex FormatoVersao();

    [GeneratedRegex(@"^\d{5}-\d{8}/\d{4}-\d{2}$")]
    private static partial Regex FormatoSei();

    private readonly AppDbContext _context;
    private readonly IPeRegistroService _registros;
    private readonly IPePermissionService _permissoes;
    private readonly IPePdticService _pdtics;
    private readonly IPeDocumentoService _documentos;

    public PePdticAprovacaoService(AppDbContext context, IPeRegistroService registros, IPePermissionService permissoes,
        IPePdticService pdtics, IPeDocumentoService documentos)
    {
        _context = context;
        _registros = registros;
        _permissoes = permissoes;
        _pdtics = pdtics;
        _documentos = documentos;
    }

    // ── Envio ao CGTIC ──────────────────────────────────────────────────────

    public async Task<PeEnvioResponse> EnvioAsync(long id, PeUserContext ctx)
    {
        var pdtic = await PePdticService.LerAsync(_context, _permissoes, id, ctx);
        var pendencias = PodeReceberEnvio(pdtic)
            ? await PendenciasAsync(pdtic, await PeTrilhaOrgao.DoPdticAsync(_context, pdtic), ctx)
            : new List<PePendenciaResponse>();
        var motivo = PorQueNaoEnvia(pdtic, ctx)
                     ?? (pendencias.Count > 0 ? "Resolva o que falta na lista antes de enviar o PDTIC ao CGTIC." : null);
        return new PeEnvioResponse { PodeEnviar = motivo == null, Pendencias = pendencias, Motivo = motivo };
    }

    public async Task<PePdticResponse> EnviarAsync(long id, PeUserContext ctx)
    {
        var pdtic = await PePdticService.LerAsync(_context, _permissoes, id, ctx, rastrear: true);
        if (!_permissoes.PodeEditarPdtic(ctx, pdtic.OrgaoId))
            throw new ApiException(ErrorCode.PeSemPermissao, "Quem envia o PDTIC ao CGTIC é a equipe do órgão.");
        if (!PodeReceberEnvio(pdtic)) throw SituacaoInvalida(PorQueNaoEnvia(pdtic, ctx) ?? "Este PDTIC não pode ser enviado agora.");

        var pendencias = await PendenciasAsync(pdtic, await PeTrilhaOrgao.DoPdticAsync(_context, pdtic), ctx);
        if (pendencias.Count > 0) throw new PePendenciasException(pendencias);

        var agora = DateTime.UtcNow;
        // O PDF que vai ao CGTIC: a versão "enviada", gravada junto com o envio e a deliberação
        var (versao, _) = await _documentos.GerarVersaoAsync(pdtic, ctx, PeDominios.SituacaoVersaoDoc.Enviada);
        pdtic.Situacao = PeDominios.SituacaoPdtic.EmAprovacao;
        pdtic.EnviadoEm = agora;
        pdtic.AlteradoEm = agora;
        pdtic.AlteradoPor = ctx.Email;
        _context.PeDeliberacoes.Add(new PeDeliberacao
        {
            ObjetoTipo = PeDominios.ObjetoDeliberacao.Pdtic,
            ObjetoId = pdtic.Id,
            VersaoObjeto = pdtic.Versao,
            EnviadoEm = agora,
            EnviadoPor = ctx.Email,
            Situacao = PeDominios.SituacaoDeliberacao.Aguardando,
            DocVersao = versao,
            CriadoEm = agora,
            CriadoPor = ctx.Email
        });
        await _context.SaveChangesAsync();
        return await _pdtics.ResponderAsync(pdtic.Id, ctx);
    }

    private static bool PodeReceberEnvio(PePdtic pdtic) =>
        !pdtic.RegistradoExternamente && PeDominios.SituacaoPdtic.Editaveis.Contains(pdtic.Situacao);

    /// <summary>Por que o PDTIC não vai ao CGTIC agora (a situação ou o papel), ou nulo.</summary>
    private string? PorQueNaoEnvia(PePdtic pdtic, PeUserContext ctx)
    {
        if (pdtic.RegistradoExternamente) return "Este PDTIC foi aprovado fora do sistema e não passa pelo envio ao CGTIC.";
        var situacao = pdtic.Situacao switch
        {
            PeDominios.SituacaoPdtic.EmAprovacao =>
                $"O PDTIC foi enviado em {PePdticService.DataBrasilia(pdtic.EnviadoEm)} e aguarda a deliberação do CGTIC.",
            PeDominios.SituacaoPdtic.Aprovado or PeDominios.SituacaoPdtic.Publicado or PeDominios.SituacaoPdtic.EmAcompanhamento =>
                "O PDTIC já foi aprovado pelo CGTIC.",
            PeDominios.SituacaoPdtic.Encerrado => "Este PDTIC foi encerrado.",
            PeDominios.SituacaoPdtic.Substituido => "Esta versão do PDTIC foi substituída.",
            _ => null
        };
        if (situacao != null) return situacao;
        return _permissoes.PodeEditarPdtic(ctx, pdtic.OrgaoId) ? null : "Quem envia o PDTIC ao CGTIC é a equipe do órgão.";
    }

    /// <summary>
    /// O que falta para o envio: cada passo obrigatório das etapas 1 a 3 anterior ao passo do
    /// envio que não está feito nem "não se aplica" (com o que falta nele), e a aprovação do SGTIC
    /// (a decisão "aprovado" e a data), no passo do envio.
    /// </summary>
    private async Task<List<PePendenciaResponse>> PendenciasAsync(PePdtic pdtic, PeTrilhaOrgao trilha, PeUserContext ctx)
    {
        var detalhe = await _pdtics.DetalharSituacaoAsync(pdtic, trilha, ctx);
        trilha = detalhe.Trilha;
        var etapas = PeEdicaoPdtic.EtapasDosPassos(trilha);
        var situacoes = detalhe.Resposta.Passos.ToDictionary(p => p.PassoId);
        var envio = trilha.Passos.FirstOrDefault(p => p.Tipo == PeDominios.TipoPasso.Envio);
        var pendencias = new List<PePendenciaResponse>();

        foreach (var passo in trilha.Passos)
        {
            if (envio != null && passo.Id == envio.Id) break;
            if (PeEdicaoPdtic.GrupoDe(etapas.GetValueOrDefault(passo.Id), passo.Tipo) != PeEdicaoPdtic.Grupo.Elaboracao) continue;
            if (passo.Situacao != PeDominios.Situacao.Obrigatorio) continue;
            var situacao = situacoes[passo.Id].Situacao;
            if (situacao is PeDominios.SituacaoPasso.Feito or PeDominios.SituacaoPasso.NaoSeAplica or PeDominios.SituacaoPasso.Continuo) continue;
            pendencias.Add(Pendencia(passo, detalhe.OQueFalta.GetValueOrDefault(passo.Id) ?? "Conclua este passo."));
        }

        // A aprovação do SGTIC, no passo do envio
        if (trilha.Secao(PeDominios.ChavePdtic.SecaoAprovacaoSgtic) is { } sgtic)
        {
            var analisada = detalhe.Analise.Secao(PeDominios.ChavePdtic.SecaoAprovacaoSgtic);
            var dados = PeRegistroDados.Ler(analisada?.Registros.FirstOrDefault()?.Dados);
            var decisao = PeRegistroDados.Texto(dados[PeDominios.ChavePdtic.CampoDecisao]);
            var motivo = decisao == PeDominios.Decisao.Devolvido
                ? "O SGTIC devolveu o PDTIC: ajuste o que foi pedido e registre a nova decisão."
                : decisao != PeDominios.Decisao.Aprovado || PeRegistroDados.EhVazio(dados[PeDominios.ChavePdtic.CampoData])
                    ? "Registre a aprovação do SGTIC: a decisão \"Aprovado\" e a data."
                    : analisada is { Incompletos.Count: > 0 }
                        ? "Complete os campos obrigatórios da aprovação do SGTIC."
                        : null;
            if (motivo != null) pendencias.Add(Pendencia(sgtic.Passo, motivo));
        }
        return pendencias;
    }

    private static PePendenciaResponse Pendencia(PeTrilhaPasso passo, string motivo) => new()
    {
        PassoId = passo.Id,
        PassoNumero = passo.Numero,
        PassoTitulo = passo.Titulo,
        Motivo = motivo
    };

    // ── Publicação ──────────────────────────────────────────────────────────

    public async Task<PePdticResponse> PublicarAsync(long id, PeUserContext ctx)
    {
        var pdtic = await PePdticService.LerAsync(_context, _permissoes, id, ctx, rastrear: true);
        if (!_permissoes.PodeEditarPdtic(ctx, pdtic.OrgaoId))
            throw new ApiException(ErrorCode.PeSemPermissao, "Quem registra a publicação é a equipe do órgão.");
        if (pdtic.Situacao != PeDominios.SituacaoPdtic.Aprovado)
            throw SituacaoInvalida(pdtic.PublicadoEm != null
                ? $"O PDTIC já foi publicado (registrado em {PePdticService.DataBrasilia(pdtic.PublicadoEm)})."
                : "O PDTIC é publicado depois da aprovação do CGTIC.");

        // A seção da publicação com a data e o endereço (e os outros obrigatórios dela)
        var trilha = await PeTrilhaOrgao.DoPdticAsync(_context, pdtic);
        if (trilha.Secao(PeDominios.ChavePdtic.SecaoPublicacao) is { } visivel)
        {
            var secao = trilha.Montar(visivel.Secao);
            var analisada = (await _registros.AnalisarAsync(PeDono.DoPdtic(pdtic.Id), new[] { secao })).Secoes[0];
            var dados = PeRegistroDados.Ler(analisada.Registros.FirstOrDefault()?.Dados);
            var campos = new Dictionary<string, string>();
            foreach (var (chave, mensagem) in new[]
                     {
                         (PeDominios.ChavePdtic.CampoData, "Informe a data da publicação."),
                         (PeDominios.ChavePdtic.CampoEndereco, "Informe o endereço da íntegra do PDTIC na internet.")
                     })
                if (secao.Visiveis.Any(v => v.Campo.Chave == chave) && PeRegistroDados.EhVazio(dados[chave]))
                    campos[chave] = mensagem;
            foreach (var campo in analisada.Incompletos.SelectMany(i => i.Faltando))
                campos.TryAdd(campo.Chave, PeValores.MensagemObrigatorio(campo));
            if (campos.Count > 0)
                throw new PeValidacaoException(campos, $"Registre a data e o endereço da publicação no passo {visivel.Passo.Numero} antes de confirmar.",
                    ErrorCode.PePublicacaoIncompleta);
        }

        var agora = DateTime.UtcNow;
        pdtic.Situacao = PeDominios.SituacaoPdtic.Publicado;
        pdtic.PublicadoEm = agora;
        pdtic.AlteradoEm = agora;
        pdtic.AlteradoPor = ctx.Email;
        // A versão do documento que o CGTIC aprovou é a que foi publicada (o PDF não muda)
        var versaoAprovada = await _context.PeDeliberacoes.AsNoTracking()
            .Where(d => d.ObjetoTipo == PeDominios.ObjetoDeliberacao.Pdtic && d.ObjetoId == pdtic.Id
                        && d.Situacao == PeDominios.SituacaoDeliberacao.Aprovado && d.DocVersaoId != null)
            .OrderByDescending(d => d.Id)
            .Select(d => d.DocVersaoId)
            .FirstOrDefaultAsync();
        if (versaoAprovada != null && await _context.PeDocVersoes.FirstOrDefaultAsync(v => v.Id == versaoAprovada) is { } versao)
            versao.Situacao = PeDominios.SituacaoVersaoDoc.Publicada;
        await _context.SaveChangesAsync();
        return await _pdtics.ResponderAsync(pdtic.Id, ctx);
    }

    // ── Encerramento ────────────────────────────────────────────────────────

    public async Task<PePdticResponse> EncerrarAsync(long id, PeEncerrarDTO dto, PeUserContext ctx)
    {
        var pdtic = await PePdticService.LerAsync(_context, _permissoes, id, ctx, rastrear: true);
        var motivo = Texto(dto.Motivo, MaximoTexto, "O motivo");
        var equipe = _permissoes.PodeEditarPdtic(ctx, pdtic.OrgaoId);
        var administra = ctx.EhAdminGeral || ctx.Papel == PapeisPlanejamento.Admin;
        if (!equipe && !administra)
            throw new ApiException(ErrorCode.PeSemPermissao,
                "Quem encerra o PDTIC é a equipe do órgão, com a aprovação da autoridade máxima, ou o administrador do módulo, depois do fim da vigência.");
        if (PeDominios.SituacaoPdtic.Encerradas.Contains(pdtic.Situacao))
            throw SituacaoInvalida(pdtic.Situacao == PeDominios.SituacaoPdtic.Encerrado
                ? "Este PDTIC já foi encerrado."
                : "Esta versão do PDTIC foi substituída e não se encerra.");

        // A equipe do órgão: o PDTIC publicado, com a aprovação da autoridade máxima (7.4)
        string? recusa = null;
        if (equipe)
        {
            recusa = await RecusaDaEquipeAsync(pdtic);
            if (recusa == null) return await EncerrarAsync(pdtic, motivo, ctx);
        }

        // O administrador do módulo (e o admin geral): com o motivo, depois do fim da vigência
        if (!administra) throw new ApiException(ErrorCode.PeEncerramentoRecusado, recusa!);
        if (motivo == null)
            throw new ApiException(ErrorCode.PeJustificativaObrigatoria,
                recusa == null ? "Diga o motivo do encerramento." : $"{recusa} Para encerrar pela vigência vencida, diga o motivo.");
        if (pdtic.Situacao == PeDominios.SituacaoPdtic.EmAprovacao)
            throw SituacaoInvalida("O PDTIC está com o CGTIC. Espere a deliberação para encerrar.");
        var hoje = DateOnly.FromDateTime(DateTimeHelper.TodayBrasilia());
        if (pdtic.VigenciaFim is not DateOnly fim || fim >= hoje)
            throw new ApiException(ErrorCode.PeEncerramentoRecusado, pdtic.VigenciaFim is DateOnly ate
                ? $"A vigência do PDTIC vai até {PeFormato.Data(ate)}. O administrador encerra o PDTIC só depois do fim da vigência."
                : "O PDTIC não tem o fim da vigência (passo da abrangência). O administrador encerra o PDTIC só depois do fim da vigência.");
        return await EncerrarAsync(pdtic, motivo, ctx);
    }

    /// <summary>Por que a equipe ainda não encerra (o PDTIC fora do acompanhamento ou sem a aprovação da autoridade máxima), ou nulo.</summary>
    private async Task<string?> RecusaDaEquipeAsync(PePdtic pdtic)
    {
        if (!PeDominios.SituacaoPdtic.Vigentes.Contains(pdtic.Situacao))
            return "A equipe encerra o PDTIC depois da publicação, com a aprovação da autoridade máxima.";
        var trilha = await PeTrilhaOrgao.DoPdticAsync(_context, pdtic);
        var autoridade = trilha.Secao(PeDominios.ChavePdtic.SecaoAprovacaoAutoridade);
        var onde = autoridade == null ? string.Empty : $" (passo {autoridade.Value.Passo.Numero})";
        var decisao = autoridade == null
            ? null
            : PePdticService.Decisao((await _registros.AnalisarAsync(PeDono.DoPdtic(pdtic.Id), new[] { trilha.Montar(autoridade.Value.Secao) })).Secoes[0]);
        return decisao == PeDominios.Decisao.Aprovado
            ? null
            : $"Para encerrar o PDTIC, registre a aprovação da autoridade máxima{onde} com a decisão \"Aprovado\".";
    }

    private async Task<PePdticResponse> EncerrarAsync(PePdtic pdtic, string? motivo, PeUserContext ctx)
    {
        var agora = DateTime.UtcNow;
        pdtic.Situacao = PeDominios.SituacaoPdtic.Encerrado;
        pdtic.EncerradoEm = agora;
        pdtic.EncerramentoMotivo = motivo;
        pdtic.AlteradoEm = agora;
        pdtic.AlteradoPor = ctx.Email;
        await _context.SaveChangesAsync();
        return await _pdtics.ResponderAsync(pdtic.Id, ctx);
    }

    // ── Revisão ─────────────────────────────────────────────────────────────

    public async Task<PePdticResponse> RevisarAsync(long id, PeRevisaoDTO dto, PeUserContext ctx)
    {
        var atual = await PePdticService.LerAsync(_context, _permissoes, id, ctx);
        if (!_permissoes.PodeEditarPdtic(ctx, atual.OrgaoId))
            throw new ApiException(ErrorCode.PeSemPermissao, "Quem abre a revisão do PDTIC é a equipe do órgão.");
        if (!PeDominios.SituacaoPdtic.Vigentes.Contains(atual.Situacao))
            throw SituacaoInvalida("Só o PDTIC vigente (publicado ou em acompanhamento) abre uma revisão.");
        var justificativa = Texto(dto.Justificativa, MaximoTexto, "A justificativa");

        var doOrgao = await _context.PePdtics.AsNoTracking().Where(p => p.OrgaoId == atual.OrgaoId).ToListAsync();
        if (doOrgao.FirstOrDefault(p => PeDominios.SituacaoPdtic.DaElaboracao.Contains(p.Situacao)) is { } emAndamento)
            throw new ApiException(ErrorCode.PeRevisaoEmAndamento,
                $"O órgão já tem a versão {emAndamento.Versao} em andamento ({PeDominios.SituacaoPdtic.Rotulo(emAndamento.Situacao).ToLowerInvariant()}). "
                + "Termine aquela antes de abrir outra revisão.");

        // A decisão do comitê (6.3) libera a revisão; sem o passo na trilha, a justificativa.
        // Rodada A: a seção avaliacao_comite do PDTIC, como seção comum (na rodada B, a da avaliação mais recente)
        var trilha = await PeTrilhaOrgao.DoPdticAsync(_context, atual);
        var comite = trilha.Passos.FirstOrDefault(p => p.Chave == PeDominios.ChavePdtic.PassoAvaliacaoComite);
        if (comite != null && PePdticService.RecusaDoNaoSeAplica(comite) == null
            && await _context.PePdticPassos.AsNoTracking().AnyAsync(p => p.PdticId == atual.Id && p.PassoId == comite.Id && p.NaoSeAplica))
            comite = null;
        if (comite != null)
        {
            var secao = comite.Secoes.FirstOrDefault(s => s.Chave == PeDominios.ChavePdtic.SecaoAvaliacaoComite);
            var decisao = secao == null
                ? null
                : PePdticService.Decisao((await _registros.AnalisarAsync(PeDono.DoPdtic(atual.Id), new[] { trilha.Montar(secao) })).Secoes[0]);
            if (decisao != PeDominios.Decisao.Revisar)
                throw new ApiException(ErrorCode.PeRevisaoRecusada,
                    $"Para abrir a revisão, registre no passo {comite.Numero} a avaliação do comitê com a decisão \"Revisar o PDTIC\".");
        }
        else if (justificativa == null)
        {
            throw new ApiException(ErrorCode.PeJustificativaObrigatoria, "Explique por que o PDTIC será revisto.");
        }

        var agora = DateTime.UtcNow;
        var nova = new PePdtic
        {
            OrgaoId = atual.OrgaoId,
            Versao = PeEdicaoPdtic.ProximaRevisao(atual.Versao, doOrgao.Select(p => p.Versao)),
            Situacao = PeDominios.SituacaoPdtic.EmElaboracao,
            VigenciaInicio = atual.VigenciaInicio,
            VigenciaFim = atual.VigenciaFim,
            RegistradoExternamente = false,
            AnteriorId = atual.Id,
            RevisaoJustificativa = justificativa,
            CriadoEm = agora,
            CriadoPor = ctx.Email
        };
        _context.PePdtics.Add(nova);

        await using var transacao = _context.Database.IsRelational() ? await _context.Database.BeginTransactionAsync() : null;
        await CopiarAsync(atual, nova, trilha.Dados, ctx.Email, agora);
        await _context.SaveChangesAsync();
        // A sequência dos códigos usa o id da versão nova, que só existe depois da primeira gravação
        var origem = PeDono.DoPdtic(atual.Id).Chave;
        var destino = PeDono.DoPdtic(nova.Id).Chave;
        foreach (var sequencia in await _context.PeRegistroSequencias.AsNoTracking().Where(s => s.Dono == origem).ToListAsync())
            _context.PeRegistroSequencias.Add(new PeRegistroSequencia { SecaoId = sequencia.SecaoId, Dono = destino, Ultimo = sequencia.Ultimo });
        await _context.SaveChangesAsync();
        if (transacao != null) await transacao.CommitAsync();

        return await _pdtics.ResponderAsync(nova.Id, ctx);
    }

    /// <summary>
    /// A cópia da revisão: os registros (menos os das seções dos passos de aprovação, de envio e
    /// de publicação), com os mesmos códigos e a mesma ordem, e as ligações entre eles refeitas
    /// para as cópias (a ligação com um catálogo continua no mesmo item; a ligação com um registro
    /// que não foi copiado sai); os fluxos adaptados; os capítulos e os textos do documento; e os
    /// "não se aplica" marcados. As imagens e os arquivos ficam com o dono de antes e servem às
    /// duas versões (mesmo órgão).
    /// </summary>
    private async Task CopiarAsync(PePdtic atual, PePdtic nova, PeModeloDados modelo, string autor, DateTime agora)
    {
        var tiposSemCopia = new[] { PeDominios.TipoPasso.Aprovacao, PeDominios.TipoPasso.Envio, PeDominios.TipoPasso.Publicacao };
        var passosSemCopia = modelo.Passos.Where(p => tiposSemCopia.Contains(p.Tipo)).Select(p => p.Id).ToHashSet();
        var secoesSemCopia = modelo.Secoes.Where(s => s.PassoId != null && passosSemCopia.Contains(s.PassoId.Value)).Select(s => s.Id).ToHashSet();

        var registros = await _context.PeRegistros.AsNoTracking().Where(r => r.PdticId == atual.Id).ToListAsync();
        var todos = registros.Select(r => r.Id).ToHashSet();
        var copias = new Dictionary<long, PeRegistro>();
        foreach (var registro in registros.Where(r => !secoesSemCopia.Contains(r.SecaoId)))
        {
            var copia = new PeRegistro
            {
                SecaoId = registro.SecaoId,
                Pdtic = nova,
                Codigo = registro.Codigo,
                Ordem = registro.Ordem,
                Dados = registro.Dados,
                Sistema = registro.Sistema,
                CriadoEm = agora,
                CriadoPor = autor
            };
            copias[registro.Id] = copia;
            _context.PeRegistros.Add(copia);
        }

        if (copias.Count > 0)
        {
            var ids = copias.Keys.ToList();
            foreach (var vinculo in await _context.PeVinculos.AsNoTracking().Where(v => ids.Contains(v.RegistroOrigemId)).ToListAsync())
            {
                var novo = new PeVinculo { RegistroOrigem = copias[vinculo.RegistroOrigemId], CampoId = vinculo.CampoId };
                if (copias.TryGetValue(vinculo.RegistroDestinoId, out var destinoCopiado)) novo.RegistroDestino = destinoCopiado;
                else if (todos.Contains(vinculo.RegistroDestinoId)) continue;
                else novo.RegistroDestinoId = vinculo.RegistroDestinoId;
                _context.PeVinculos.Add(novo);
            }
        }

        foreach (var fluxo in await _context.PeFluxos.AsNoTracking().Where(f => f.PdticId == atual.Id).ToListAsync())
            _context.PeFluxos.Add(new PeFluxo
            {
                Pdtic = nova,
                ModeloId = fluxo.ModeloId,
                Nome = fluxo.Nome,
                Definicao = fluxo.Definicao,
                ModeloHash = fluxo.ModeloHash,
                CriadoEm = agora,
                CriadoPor = autor
            });

        foreach (var capitulo in await _context.PeDocOrgaos.AsNoTracking().Where(o => o.PdticId == atual.Id).ToListAsync())
            _context.PeDocOrgaos.Add(new PeDocOrgao
            {
                Pdtic = nova,
                CapituloId = capitulo.CapituloId,
                Oculto = capitulo.Oculto,
                TituloProprio = capitulo.TituloProprio,
                CriadoEm = agora,
                CriadoPor = autor
            });

        foreach (var texto in await _context.PeDocOrgaoBlocos.AsNoTracking().Where(o => o.PdticId == atual.Id).ToListAsync())
            _context.PeDocOrgaoBlocos.Add(new PeDocOrgaoBloco
            {
                Pdtic = nova,
                BlocoId = texto.BlocoId,
                Texto = texto.Texto,
                ModeloHash = texto.ModeloHash,
                EditadoEm = texto.EditadoEm,
                EditadoPor = texto.EditadoPor
            });

        foreach (var marca in await _context.PePdticPassos.AsNoTracking().Where(p => p.PdticId == atual.Id && p.NaoSeAplica).ToListAsync())
            _context.PePdticPassos.Add(new PePdticPasso
            {
                Pdtic = nova,
                PassoId = marca.PassoId,
                NaoSeAplica = true,
                Justificativa = marca.Justificativa,
                MarcadoEm = marca.MarcadoEm,
                MarcadoPor = marca.MarcadoPor
            });
    }

    // ── PDTIC aprovado fora do sistema ─────────────────────────────────────

    public async Task<PePdticResponse> RegistrarExternoAsync(PeRegistroExternoDTO dto, PeUserContext ctx)
    {
        var orgaoId = PePdticService.OrgaoParaCriar(dto.OrgaoId, ctx,
            "Só a equipe do órgão registra o PDTIC aprovado fora do sistema.", "Você só registra o PDTIC do seu próprio órgão.");
        var orgao = await _context.PgiaOrgaos.AsNoTracking().FirstOrDefaultAsync(o => o.Id == orgaoId && o.Ativo)
            ?? throw new ApiException(ErrorCode.PeOrgaoNaoEncontrado, "Órgão não encontrado ou desativado.");

        var campos = new Dictionary<string, string>();
        var hoje = DateOnly.FromDateTime(DateTimeHelper.TodayBrasilia());
        var versao = dto.Versao?.Trim();
        if (string.IsNullOrEmpty(versao)) campos[nameof(dto.Versao)] = "Informe a versão do PDTIC (por exemplo, 1.0).";
        else if (!FormatoVersao().IsMatch(versao)) campos[nameof(dto.Versao)] = "Informe a versão no formato 1.0 ou 2.1.";
        var inicio = Data(dto.VigenciaInicio, nameof(dto.VigenciaInicio), "Informe o início da vigência.", campos);
        var fim = Data(dto.VigenciaFim, nameof(dto.VigenciaFim), "Informe o fim da vigência.", campos);
        if (inicio != null && fim != null && fim < inicio) campos[nameof(dto.VigenciaFim)] = "O fim da vigência não pode ser antes do início.";
        if (dto.ArquivoId == null) campos[nameof(dto.ArquivoId)] = "Envie o PDF do PDTIC aprovado.";

        var instancia = dto.AprovacaoInstancia?.Trim();
        if (instancia == null || !PeDominios.InstanciaAprovacaoExterna.Todas.Contains(instancia))
            campos[nameof(dto.AprovacaoInstancia)] = "Diga quem aprovou o PDTIC: o CGTIC ou outra instância.";
        var dataAprovacao = Data(dto.AprovacaoData, nameof(dto.AprovacaoData), "Informe a data da aprovação.", campos, ate: hoje);
        var atoTipo = TextoDoCampo(dto.AprovacaoAtoTipo, 60, nameof(dto.AprovacaoAtoTipo), "O tipo do ato", campos);
        var atoNumero = TextoDoCampo(dto.AprovacaoAtoNumero, 60, nameof(dto.AprovacaoAtoNumero), "O número do ato", campos);
        if (instancia == PeDominios.InstanciaAprovacaoExterna.Cgtic && atoNumero == null && !campos.ContainsKey(nameof(dto.AprovacaoAtoNumero)))
            campos[nameof(dto.AprovacaoAtoNumero)] = "Informe o número do ato do CGTIC que aprovou o PDTIC.";
        var sei = dto.AprovacaoSei?.Trim();
        if (string.IsNullOrEmpty(sei)) sei = null;
        else if (!FormatoSei().IsMatch(sei)) campos[nameof(dto.AprovacaoSei)] = "Informe o processo SEI no formato 00000-00000000/0000-00.";

        var dataPublicacao = Data(dto.PublicacaoData, nameof(dto.PublicacaoData), "Informe a data da publicação.", campos, ate: hoje);
        if (dataAprovacao != null && dataPublicacao != null && dataPublicacao < dataAprovacao)
            campos[nameof(dto.PublicacaoData)] = "A publicação não pode ser antes da aprovação.";
        var endereco = TextoDoCampo(dto.PublicacaoEndereco, MaximoEndereco, nameof(dto.PublicacaoEndereco), "O endereço", campos);
        if (endereco == null && !campos.ContainsKey(nameof(dto.PublicacaoEndereco)))
            campos[nameof(dto.PublicacaoEndereco)] = "Informe o endereço da íntegra do PDTIC na internet.";

        // O PDF: enviado pela mesma pessoa e ainda sem dono
        PeArquivo? arquivo = null;
        if (dto.ArquivoId is long arquivoId)
        {
            arquivo = await _context.PeArquivos.FirstOrDefaultAsync(a => a.Id == arquivoId);
            if (arquivo == null || arquivo.DonoTipo != null || !MesmaPessoa(arquivo.CriadoPor, ctx.Email))
                campos[nameof(dto.ArquivoId)] = "Este arquivo não pode ser usado aqui. Envie o PDF de novo.";
            else if (PeArquivoService.TipoDoArquivo(arquivo.Nome) != "pdf")
                campos[nameof(dto.ArquivoId)] = "Envie o PDTIC aprovado em PDF.";
        }
        if (campos.Count > 0) throw new PeValidacaoException(campos, null, ErrorCode.PeRegistroExternoInvalido);

        // Só sem PDTIC em andamento no órgão, e com uma versão que ele ainda não tem
        var doOrgao = await _context.PePdtics.AsNoTracking().Where(p => p.OrgaoId == orgaoId).OrderByDescending(p => p.Id).ToListAsync();
        if (doOrgao.FirstOrDefault(p => !PeDominios.SituacaoPdtic.Encerradas.Contains(p.Situacao)) is { } atual)
            throw PePdticService.JaTemAtual(atual);
        if (doOrgao.Any(p => p.Versao == versao))
            throw new ApiException(ErrorCode.PeVersaoPdticDuplicada, $"O órgão já tem um PDTIC na versão {versao}. Informe outra versão.");

        var trilha = await PeTrilhaOrgao.CarregarAsync(_context, orgao.Id, soAtivo: false);
        var agora = DateTime.UtcNow;
        var pdtic = new PePdtic
        {
            OrgaoId = orgao.Id,
            Versao = versao!,
            Situacao = PeDominios.SituacaoPdtic.Publicado,
            VigenciaInicio = inicio,
            VigenciaFim = fim,
            RegistradoExternamente = true,
            AnteriorId = doOrgao.FirstOrDefault()?.Id,
            AprovadoEm = MeioDia(dataAprovacao!.Value),
            PublicadoEm = MeioDia(dataPublicacao!.Value),
            CriadoEm = agora,
            CriadoPor = ctx.Email
        };
        _context.PePdtics.Add(pdtic);

        // As seções: a vigência (1.1), a publicação (3.13) e, quando outra instância aprovou, a aprovação no passo do envio
        Gravar(pdtic, trilha.Dados, PeDominios.ChavePdtic.SecaoAbrangencia, ctx.Email, agora, new Dictionary<string, object?>
        {
            [PeDominios.ChavePdtic.CampoVigenciaInicio] = Iso(inicio!.Value),
            [PeDominios.ChavePdtic.CampoVigenciaFim] = Iso(fim!.Value)
        });
        Gravar(pdtic, trilha.Dados, PeDominios.ChavePdtic.SecaoPublicacao, ctx.Email, agora, new Dictionary<string, object?>
        {
            [PeDominios.ChavePdtic.CampoData] = Iso(dataPublicacao.Value),
            [PeDominios.ChavePdtic.CampoEndereco] = endereco
        });
        if (instancia == PeDominios.InstanciaAprovacaoExterna.Outra)
        {
            var (tipo, observacao) = TipoDoAto(trilha.Dados, atoTipo);
            Gravar(pdtic, trilha.Dados, PeDominios.ChavePdtic.SecaoAprovacaoSgtic, ctx.Email, agora, new Dictionary<string, object?>
            {
                [PeDominios.ChavePdtic.CampoDecisao] = PeDominios.Decisao.Aprovado,
                [PeDominios.ChavePdtic.CampoData] = Iso(dataAprovacao.Value),
                [PeDominios.ChavePdtic.CampoInstancia] = PeDominios.ChavePdtic.InstanciaOutra,
                [PeDominios.ChavePdtic.CampoAtoTipo] = tipo,
                [PeDominios.ChavePdtic.CampoAtoNumero] = atoNumero,
                [PeDominios.ChavePdtic.CampoSei] = sei,
                [PeDominios.ChavePdtic.CampoObservacao] = observacao
            });
        }

        await using var transacao = _context.Database.IsRelational() ? await _context.Database.BeginTransactionAsync() : null;
        await _context.SaveChangesAsync();

        // O PDF enviado é a versão 1 do documento, já publicada; o arquivo passa a ser do PDTIC
        var conteudo = await _context.PeArquivosConteudo.AsNoTracking().Where(c => c.Id == arquivo!.Id).Select(c => c.Conteudo).FirstAsync();
        arquivo!.DonoTipo = PeDominios.DonoArquivo.Pdtic;
        arquivo.DonoId = pdtic.Id;
        arquivo.AlteradoEm = agora;
        arquivo.AlteradoPor = ctx.Email;
        var versaoDoc = new PeDocVersao
        {
            PdticId = pdtic.Id,
            Numero = 1,
            Situacao = PeDominios.SituacaoVersaoDoc.Publicada,
            ArquivoId = arquivo.Id,
            Hash = arquivo.Hash,
            Paginas = PeDocumentoPdf.ContarPaginasDoArquivo(conteudo),
            GeradoEm = agora,
            GeradoPor = ctx.Email
        };
        _context.PeDocVersoes.Add(versaoDoc);
        if (instancia == PeDominios.InstanciaAprovacaoExterna.Cgtic)
            _context.PeDeliberacoes.Add(new PeDeliberacao
            {
                ObjetoTipo = PeDominios.ObjetoDeliberacao.Pdtic,
                ObjetoId = pdtic.Id,
                VersaoObjeto = pdtic.Versao,
                EnviadoEm = agora,
                EnviadoPor = ctx.Email,
                Situacao = PeDominios.SituacaoDeliberacao.Aprovado,
                DecididoEm = agora,
                DecididoPor = ctx.Email,
                AtoTipo = atoTipo,
                AtoNumero = atoNumero,
                AtoData = dataAprovacao,
                Sei = sei,
                Observacao = "PDTIC aprovado fora do sistema e registrado para o acompanhamento.",
                DocVersao = versaoDoc,
                CriadoEm = agora,
                CriadoPor = ctx.Email
            });
        await _context.SaveChangesAsync();
        if (transacao != null) await transacao.CommitAsync();

        return await _pdtics.ResponderAsync(pdtic.Id, ctx);
    }

    /// <summary>
    /// O registro de uma seção formulário do PDTIC com os valores dados, conferidos como o motor
    /// confere (só os campos que existem na seção; o vazio não é guardado). Sem passar pela regra
    /// da edição: é o sistema que grava, na criação do PDTIC registrado fora do sistema.
    /// </summary>
    private void Gravar(PePdtic pdtic, PeModeloDados modelo, string secaoChave, string autor, DateTime agora, IReadOnlyDictionary<string, object?> valores)
    {
        var secao = modelo.SecaoPorChave(secaoChave);
        if (secao == null || secao.ExcluidoEm != null) return;
        var dados = new JsonObject();
        foreach (var campo in modelo.CamposDaSecao(secao.Id, incluirExcluidos: false))
        {
            if (!valores.TryGetValue(campo.Chave, out var valor) || valor == null) continue;
            var resultado = PeValores.Normalizar(campo, modelo.OpcoesDoCampo(campo.Id).ToList(), JsonSerializer.SerializeToElement(valor), null);
            if (resultado.Erro == null && resultado.Valor != null) dados[campo.Chave] = resultado.Valor;
        }
        _context.PeRegistros.Add(new PeRegistro
        {
            SecaoId = secao.Id,
            Pdtic = pdtic,
            Ordem = 1,
            Dados = dados.ToJsonString(PeModeloService.JsonHistorico),
            CriadoEm = agora,
            CriadoPor = autor
        });
    }

    /// <summary>
    /// O tipo do ato informado como opção da lista da aprovação do SGTIC (pelo valor ou pelo
    /// rótulo, sem diferenciar acentos nem maiúsculas); fora da lista, "outro", com o texto na observação.
    /// </summary>
    private static (string? Valor, string? Observacao) TipoDoAto(PeModeloDados modelo, string? texto)
    {
        if (texto == null) return (null, null);
        var secao = modelo.SecaoPorChave(PeDominios.ChavePdtic.SecaoAprovacaoSgtic);
        var campo = secao == null ? null : modelo.CamposDaSecao(secao.Id, incluirExcluidos: false).FirstOrDefault(c => c.Chave == PeDominios.ChavePdtic.CampoAtoTipo);
        var opcoes = campo == null ? new List<PeOpcao>() : modelo.OpcoesDoCampo(campo.Id).Where(o => o.Ativa).ToList();
        var procurado = SemAcento(texto);
        var achada = opcoes.FirstOrDefault(o => SemAcento(o.Valor) == procurado || SemAcento(o.Rotulo) == procurado);
        if (achada != null) return (achada.Valor, null);
        return (opcoes.Any(o => o.Valor == PeDominios.ChavePdtic.AtoTipoOutro) ? PeDominios.ChavePdtic.AtoTipoOutro : null, $"Tipo do ato: {texto}.");
    }

    private static string SemAcento(string texto)
    {
        var decomposto = texto.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        return new string(decomposto.Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark).ToArray());
    }

    // ── Apoio ───────────────────────────────────────────────────────────────

    private static ApiException SituacaoInvalida(string mensagem) => new(ErrorCode.PePdticSituacaoInvalida, mensagem);

    /// <summary>A data (aaaa-mm-dd) de um campo do corpo, obrigatória; com "ate", não depois dela (hoje).</summary>
    private static DateOnly? Data(string? valor, string campo, string obrigatoria, Dictionary<string, string> campos, DateOnly? ate = null)
    {
        var texto = valor?.Trim();
        if (string.IsNullOrEmpty(texto))
        {
            campos[campo] = obrigatoria;
            return null;
        }
        if (!DateOnly.TryParseExact(texto, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var data)
            || data.Year < 1900 || data.Year > 2199)
        {
            campos[campo] = "Informe uma data válida, no formato aaaa-mm-dd.";
            return null;
        }
        if (ate != null && data > ate)
        {
            campos[campo] = "A data não pode ser depois de hoje.";
            return null;
        }
        return data;
    }

    private static string? TextoDoCampo(string? valor, int maximo, string campo, string oQue, Dictionary<string, string> campos)
    {
        var texto = valor?.Trim();
        if (string.IsNullOrEmpty(texto)) return null;
        if (texto.Length <= maximo) return texto;
        campos[campo] = $"{oQue} tem no máximo {maximo} caracteres.";
        return null;
    }

    private static string? Texto(string? valor, int maximo, string oQue)
    {
        var texto = valor?.Trim();
        if (string.IsNullOrEmpty(texto)) return null;
        if (texto.Length > maximo) throw new ApiException(ErrorCode.PeDadosInvalidos, $"{oQue} tem no máximo {maximo} caracteres.");
        return texto;
    }

    private static string Iso(DateOnly data) => data.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    /// <summary>A data informada como o meio-dia de Brasília (em UTC), para não mudar de dia na exibição.</summary>
    private static DateTime MeioDia(DateOnly data) => DateTimeHelper.ToUtc(new DateTime(data.Year, data.Month, data.Day, 12, 0, 0));

    private static bool MesmaPessoa(string? a, string? b) =>
        !string.IsNullOrWhiteSpace(a) && string.Equals(a.Trim(), b?.Trim(), StringComparison.OrdinalIgnoreCase);
}
