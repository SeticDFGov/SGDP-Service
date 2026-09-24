using System.Text.Encodings.Web;
using System.Text.Json;
using api.Common;
using api.Planejamento;
using Microsoft.EntityFrameworkCore;
using Models;
using Models.Planejamento;
using service.Interface;

namespace service.Planejamento;

/// <summary>
/// Modelo configurável do módulo Governança Estratégica (E2). Lê o modelo inteiro de uma
/// vez (<see cref="PeModeloDados"/>) para o GET e para a trilha, e grava item a item com
/// as regras do plano (seção 6.2):
/// <list type="bullet">
/// <item>travado não desliga: passo ou seção dos nove conteúdos do art. 12, § 2º, o campo
/// travado (tema das ações) e o campo principal de uma seção travada;</item>
/// <item>item do sistema (veio do guia ou do decreto) pode ser renomeado, ganhar outra ajuda
/// e mudar de situação, mas não muda de tipo nem de chave e não é apagado;</item>
/// <item>item criado pelo administrador é apagado de forma lógica (os dados ficam
/// guardados); chave única (a do campo, dentro da seção, inclusive entre os apagados);</item>
/// <item>opção do sistema ou em uso (na matriz do nível de risco ou guardada num registro)
/// só é desativada; opção travada nem isso;</item>
/// <item>campo usado num cálculo e seção alvo de uma ligação não somem nem mudam de chave
/// ou de tipo;</item>
/// <item>com dados gravados (E3), o campo não muda de chave nem de tipo (nem de seção ou
/// catálogo ligado, se é ligação) e a seção com registros não muda de chave nem de tipo;</item>
/// <item>toda mudança grava o antes e o depois em pe_modelo_historico.</item>
/// </list>
/// Itens criados nascem desligados em todos os níveis, menos o passo, que nasce opcional
/// no nível ativo mais alto.
/// </summary>
public class PeModeloService : IPeModeloService
{
    private const int TamanhoPaginaPadrao = 20;
    private const int TamanhoPaginaMaximo = 100;

    // Antes e depois do histórico com os acentos legíveis (vai para jsonb, nunca para HTML)
    internal static readonly JsonSerializerOptions JsonHistorico = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    private readonly AppDbContext _context;

    public PeModeloService(AppDbContext context)
    {
        _context = context;
    }

    // ── Leitura ─────────────────────────────────────────────────────────────

    public async Task<PeModeloResponse> ObterModeloAsync(bool incluirExcluidos) =>
        (await PeModeloDados.CarregarAsync(_context)).Modelo(incluirExcluidos);

    public async Task<PeTrilhaResponse> TrilhaAsync(long orgaoId)
    {
        var orgao = await _context.PgiaOrgaos.AsNoTracking().FirstOrDefaultAsync(o => o.Id == orgaoId && o.Ativo)
            ?? throw new ApiException(ErrorCode.PeOrgaoNaoEncontrado, "Órgão não encontrado ou desativado.");

        var dados = await PeModeloDados.CarregarAsync(_context);
        var escolhido = await _context.PeOrgaosConfig.AsNoTracking()
            .Where(c => c.OrgaoId == orgaoId)
            .Select(c => (long?)c.NivelId)
            .FirstOrDefaultAsync();
        var nivel = (escolhido != null ? dados.Niveis.FirstOrDefault(n => n.Id == escolhido) : null)
            ?? dados.NivelPadrao()
            ?? throw ModeloIndisponivel();

        var ajustes = await _context.PeOrgaosAjuste.AsNoTracking()
            .Where(a => a.OrgaoId == orgaoId)
            .Select(a => new { a.AlvoTipo, a.AlvoId, a.Situacao })
            .ToListAsync();

        return new PeTrilhaResponse
        {
            OrgaoId = orgao.Id,
            OrgaoSigla = orgao.Sigla,
            OrgaoNome = orgao.Nome,
            NivelId = nivel.Id,
            NivelNome = nivel.Nome,
            NivelPadrao = escolhido != nivel.Id,
            NivelAtivo = nivel.Ativo,
            Etapas = PeTrilhaResolver.Resolver(dados, nivel.Id,
                ajustes.ToDictionary(a => (a.AlvoTipo, a.AlvoId), a => a.Situacao))
        };
    }

    public async Task<PagedResponse<PeHistoricoResponse>> HistoricoAsync(PeHistoricoConsulta consulta)
    {
        var pageSize = consulta.PageSize < 1 ? TamanhoPaginaPadrao : Math.Min(consulta.PageSize, TamanhoPaginaMaximo);
        var page = Math.Clamp(consulta.Page, 1, int.MaxValue / pageSize);

        var query = _context.PeModeloHistorico.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(consulta.Entidade))
        {
            // Entidade fora do domínio devolve lista vazia, nunca "todas" em silêncio
            var entidade = consulta.Entidade.Trim();
            query = PeDominios.EntidadeHistorico.Todas.Contains(entidade)
                ? query.Where(h => h.Entidade == entidade)
                : query.Where(h => false);
        }
        if (consulta.EntidadeId != null)
        {
            var entidadeId = consulta.EntidadeId.Value;
            query = query.Where(h => h.EntidadeId == entidadeId);
        }

        var total = await query.CountAsync();
        var linhas = await query
            .OrderByDescending(h => h.AlteradoEm).ThenByDescending(h => h.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new PagedResponse<PeHistoricoResponse>(linhas.Select(Historico).ToList(), total, page, pageSize);
    }

    internal static PeHistoricoResponse Historico(PeModeloHistorico h) => new()
    {
        Id = h.Id,
        Entidade = h.Entidade,
        EntidadeId = h.EntidadeId,
        Acao = h.Acao,
        Antes = h.Antes == null ? null : PeModeloDados.Json(h.Antes),
        Depois = h.Depois == null ? null : PeModeloDados.Json(h.Depois),
        AlteradoEm = h.AlteradoEm,
        AlteradoPor = h.AlteradoPor
    };

    // ── Níveis ──────────────────────────────────────────────────────────────

    public async Task<PeNivelResponse> CriarNivelAsync(PeNivelCriarDTO dto, string autor)
    {
        var nome = Obrigatorio(dto.Nome, 100, "o nome do nível");
        var descricao = Opcional(dto.Descricao, 1000, "A descrição");

        var codigos = await _context.PeNiveis.Select(n => n.Codigo).ToListAsync();
        string codigo;
        if (!string.IsNullOrWhiteSpace(dto.Codigo))
        {
            // O front gera o código a partir do nome: vale do jeito que vier, sem acento,
            // em minúsculas e com sublinhado ("Básico 2" e "basico-2" viram "basico_2")
            codigo = PeChaves.Slug(dto.Codigo, '_', PeChaves.MaximoNivel);
            if (codigo.Length == 0 || !char.IsAsciiLetterLower(codigo[0]))
                throw Dados("O código do nível precisa começar por letra.");
            if (codigos.Contains(codigo))
                throw new ApiException(ErrorCode.PeChaveDuplicada,
                    $"Já existe um nível com o código \"{codigo}\". Use outro nome ou outro código.");
        }
        else
        {
            var baseCodigo = PeChaves.Slug(nome, '_', PeChaves.MaximoNivel);
            if (baseCodigo.Length == 0 || !char.IsLetter(baseCodigo[0])) baseCodigo = "nivel";
            codigo = PeChaves.Livre(baseCodigo, '_', PeChaves.MaximoNivel, codigos.Contains);
        }

        PeNivel? origem = null;
        if (dto.CopiarDe != null)
        {
            origem = await _context.PeNiveis.AsNoTracking().FirstOrDefaultAsync(n => n.Id == dto.CopiarDe)
                ?? throw Dados("O nível de onde copiar não existe.");
        }

        var agora = DateTime.UtcNow;
        var nivel = new PeNivel
        {
            Codigo = codigo,
            Nome = nome,
            Descricao = descricao,
            Ordem = (await _context.PeNiveis.MaxAsync(n => (int?)n.Ordem) ?? 0) + 1,
            Ativo = true,
            CriadoEm = agora,
            CriadoPor = autor
        };
        _context.PeNiveis.Add(nivel);

        if (origem != null)
        {
            // Copia a situação de cada passo, seção e campo do nível de origem
            foreach (var l in await _context.PePassosNivel.AsNoTracking().Where(n => n.NivelId == origem.Id).ToListAsync())
                _context.PePassosNivel.Add(new PePassoNivel { PassoId = l.PassoId, Nivel = nivel, Situacao = l.Situacao });
            foreach (var l in await _context.PeSecoesNivel.AsNoTracking().Where(n => n.NivelId == origem.Id).ToListAsync())
                _context.PeSecoesNivel.Add(new PeSecaoNivel { SecaoId = l.SecaoId, Nivel = nivel, Situacao = l.Situacao });
            foreach (var l in await _context.PeCamposNivel.AsNoTracking().Where(n => n.NivelId == origem.Id).ToListAsync())
                _context.PeCamposNivel.Add(new PeCampoNivel { CampoId = l.CampoId, Nivel = nivel, Situacao = l.Situacao });
        }
        else
        {
            // Nível novo em branco: só os travados já vêm obrigatórios
            foreach (var id in await _context.PePassos.Where(p => p.Travado).Select(p => p.Id).ToListAsync())
                _context.PePassosNivel.Add(new PePassoNivel { PassoId = id, Nivel = nivel, Situacao = PeDominios.Situacao.Obrigatorio });
            foreach (var id in await _context.PeSecoes
                         .Where(s => s.Travada && s.Escopo == PeDominios.Escopo.Pdtic).Select(s => s.Id).ToListAsync())
                _context.PeSecoesNivel.Add(new PeSecaoNivel { SecaoId = id, Nivel = nivel, Situacao = PeDominios.Situacao.Obrigatorio });
            foreach (var id in await _context.PeCampos
                         .Where(c => c.Travado || (c.Principal && c.Secao!.Travada)).Select(c => c.Id).ToListAsync())
                _context.PeCamposNivel.Add(new PeCampoNivel { CampoId = id, Nivel = nivel, Situacao = PeDominios.Situacao.Obrigatorio });
        }

        await SalvarCriacaoAsync(() => Registrar(PeDominios.EntidadeHistorico.Nivel, nivel.Id, PeDominios.AcaoHistorico.Criacao,
            null, Snap(nivel, origem?.Codigo), autor, agora));

        return (await PeModeloDados.CarregarAsync(_context)).Nivel(nivel);
    }

    public async Task<PeNivelResponse> AtualizarNivelAsync(long id, PeNivelAtualizarDTO dto, string autor)
    {
        var nivel = await _context.PeNiveis.FirstOrDefaultAsync(n => n.Id == id) ?? throw NaoEncontrado("Nível não encontrado.");
        var antes = Snap(nivel);

        if (dto.Informou(nameof(dto.Nome))) nivel.Nome = Obrigatorio(dto.Nome, 100, "o nome do nível");
        if (dto.Informou(nameof(dto.Descricao))) nivel.Descricao = Opcional(dto.Descricao, 1000, "A descrição");
        if (dto.Informou(nameof(dto.Ativo)) && dto.Ativo != null && dto.Ativo != nivel.Ativo)
        {
            if (dto.Ativo == false && !await _context.PeNiveis.AnyAsync(n => n.Id != id && n.Ativo))
                throw new ApiException(ErrorCode.PeUltimoNivelAtivo,
                    "Este é o único nível ativo. Ative outro nível antes de desativar este.");
            nivel.Ativo = dto.Ativo.Value;
        }

        await SalvarAlteracaoAsync(nivel, PeDominios.EntidadeHistorico.Nivel, antes, Snap(nivel), autor);
        return (await PeModeloDados.CarregarAsync(_context)).Nivel(nivel);
    }

    public async Task<List<PeNivelResponse>> OrdenarNiveisAsync(PeOrdemDTO dto, string autor)
    {
        var niveis = await _context.PeNiveis.ToListAsync();
        var ids = ValidarOrdem(dto, niveis.Select(n => n.Id));
        Reordenar(niveis, ids, PeDominios.EntidadeHistorico.Nivel, autor);
        await _context.SaveChangesAsync();

        var dados = await PeModeloDados.CarregarAsync(_context);
        return dados.Niveis.Select(dados.Nivel).ToList();
    }

    // ── Etapas ──────────────────────────────────────────────────────────────

    public async Task<PeEtapaResponse> AtualizarEtapaAsync(long id, PeEtapaAtualizarDTO dto, string autor)
    {
        var etapas = await _context.PeEtapas.OrderBy(e => e.Ordem).ThenBy(e => e.Id).ToListAsync();
        var etapa = etapas.FirstOrDefault(e => e.Id == id) ?? throw NaoEncontrado("Etapa não encontrada.");
        var antes = Snap(etapa);

        if (dto.Informou(nameof(dto.Ordem)) && dto.Ordem != null && dto.Ordem != etapa.Ordem)
        {
            // A ordem é a posição da etapa (1 = primeira); as outras são renumeradas, e cada
            // mudança de lugar fica no histórico como "ordem". A ordem atual reenviada
            // (o front manda a etapa inteira) não mexe em nada
            if (dto.Ordem < 1 || dto.Ordem > etapas.Count)
                throw new ApiException(ErrorCode.PeOrdemInvalida, $"A posição da etapa vai de 1 a {etapas.Count}.");
            var ids = etapas.Where(e => e.Id != id).Select(e => e.Id).ToList();
            ids.Insert(dto.Ordem.Value - 1, id);
            Reordenar(etapas, ids, PeDominios.EntidadeHistorico.Etapa, autor);
        }

        if (dto.Informou(nameof(dto.Titulo))) etapa.Titulo = Obrigatorio(dto.Titulo, 200, "o título da etapa");
        if (dto.Informou(nameof(dto.Descricao))) etapa.Descricao = Opcional(dto.Descricao, 1000, "A descrição");
        if (dto.Informou(nameof(dto.ReferenciaGuia))) etapa.ReferenciaGuia = Opcional(dto.ReferenciaGuia, 100, "A referência do guia");

        await SalvarAlteracaoAsync(etapa, PeDominios.EntidadeHistorico.Etapa, antes, Snap(etapa), autor);
        var dados = await PeModeloDados.CarregarAsync(_context);
        return dados.Etapa(dados.Etapas.First(e => e.Id == id), false);
    }

    // ── Passos ──────────────────────────────────────────────────────────────

    public async Task<PePassoResponse> CriarPassoAsync(PePassoCriarDTO dto, string autor)
    {
        var etapa = await _context.PeEtapas.AsNoTracking().FirstOrDefaultAsync(e => e.Id == dto.EtapaId)
            ?? throw Dados("A etapa do passo não existe.");
        var titulo = Obrigatorio(dto.Titulo, 200, "o título do passo");
        var oQueFazer = Obrigatorio(dto.OQueFazer, 2000, "o que fazer no passo");

        var chaves = await _context.PePassos.Select(p => p.Chave).ToListAsync();
        var parte = PeChaves.Slug(titulo, '-', PeChaves.MaximoPasso - etapa.Chave.Length - 1);
        if (parte.Length == 0) parte = "passo";
        var chave = PeChaves.Livre($"{etapa.Chave}.{parte}", '-', PeChaves.MaximoPasso, chaves.Contains);

        var agora = DateTime.UtcNow;
        var passo = new PePasso
        {
            EtapaId = etapa.Id,
            Chave = chave,
            Titulo = titulo,
            OQueFazer = oQueFazer,
            BaseLegal = Opcional(dto.BaseLegal, 200, "A base legal"),
            ReferenciaGuia = Opcional(dto.ReferenciaGuia, 100, "A referência do guia"),
            Tipo = PeDominios.TipoPasso.Dados,
            AceitaNaoSeAplica = dto.AceitaNaoSeAplica ?? true,
            Ordem = (await _context.PePassos.Where(p => p.EtapaId == etapa.Id).MaxAsync(p => (int?)p.Ordem) ?? 0) + 1,
            Sistema = false,
            CriadoEm = agora,
            CriadoPor = autor
        };
        _context.PePassos.Add(passo);

        // Nasce desligado em todos os níveis, menos no nível ativo mais alto (opcional)
        var niveis = await _context.PeNiveis.AsNoTracking().OrderBy(n => n.Ordem).ThenBy(n => n.Id).ToListAsync();
        var maisAlto = niveis.LastOrDefault(n => n.Ativo);
        foreach (var n in niveis)
            _context.PePassosNivel.Add(new PePassoNivel
            {
                Passo = passo,
                NivelId = n.Id,
                Situacao = n.Id == maisAlto?.Id ? PeDominios.Situacao.Opcional : PeDominios.Situacao.Desligado
            });

        await SalvarCriacaoAsync(() => Registrar(PeDominios.EntidadeHistorico.Passo, passo.Id, PeDominios.AcaoHistorico.Criacao,
            null, Snap(passo), autor, agora));
        return await PassoAsync(passo.Id);
    }

    public async Task<PePassoResponse> AtualizarPassoAsync(long id, PePassoAtualizarDTO dto, string autor)
    {
        var passo = await PassoParaEscritaAsync(id);
        var antes = Snap(passo);

        if (dto.Informou(nameof(dto.Tipo)) && dto.Tipo != null && dto.Tipo.Trim() != passo.Tipo)
            throw passo.Sistema
                ? DoSistema("Este passo veio do guia ou do decreto e não muda de tipo.")
                : Dados("Passo criado pelo administrador é sempre do tipo \"dados\".");

        if (dto.Informou(nameof(dto.Chave)) && dto.Chave != null && dto.Chave.Trim() != passo.Chave)
        {
            if (passo.Sistema) throw DoSistema("Este passo veio do guia ou do decreto e não muda de chave.");
            var chave = dto.Chave.Trim();
            if (!PeChaves.ChavePassoValida(chave))
                throw Dados("A chave do passo tem a forma \"etapa.passo\", com letras minúsculas sem acento, números e hífen.");
            if (await _context.PePassos.AnyAsync(p => p.Id != id && p.Chave == chave))
                throw new ApiException(ErrorCode.PeChaveDuplicada, $"Já existe um passo com a chave \"{chave}\".");
            passo.Chave = chave;
        }

        if (dto.Informou(nameof(dto.Titulo))) passo.Titulo = Obrigatorio(dto.Titulo, 200, "o título do passo");
        if (dto.Informou(nameof(dto.OQueFazer))) passo.OQueFazer = Obrigatorio(dto.OQueFazer, 2000, "o que fazer no passo");
        if (dto.Informou(nameof(dto.BaseLegal))) passo.BaseLegal = Opcional(dto.BaseLegal, 200, "A base legal");
        if (dto.Informou(nameof(dto.ReferenciaGuia))) passo.ReferenciaGuia = Opcional(dto.ReferenciaGuia, 100, "A referência do guia");
        if (dto.Informou(nameof(dto.AceitaNaoSeAplica)) && dto.AceitaNaoSeAplica != null)
        {
            if (dto.AceitaNaoSeAplica == true && passo.Travado)
                throw Travado("Passo travado (um dos nove conteúdos mínimos do art. 12, § 2º) não aceita \"não se aplica\".");
            passo.AceitaNaoSeAplica = dto.AceitaNaoSeAplica.Value;
        }

        await SalvarAlteracaoAsync(passo, PeDominios.EntidadeHistorico.Passo, antes, Snap(passo), autor);
        return await PassoAsync(id);
    }

    public async Task<PePassoResponse> DefinirSituacaoPassoAsync(long id, PeSituacoesDTO dto, string autor)
    {
        var passo = await PassoParaEscritaAsync(id);
        if (dto.SituacaoGeral != null) throw Dados("O passo não tem situação geral: diga a situação em cada nível.");

        var novas = await LerSituacoesAsync(dto);
        if (passo.Travado && novas.ContainsValue(PeDominios.Situacao.Desligado))
            throw Travado("Este passo é um dos nove conteúdos mínimos do PDTIC (art. 12, § 2º) e não pode ser desligado em nenhum nível.");

        var linhas = await _context.PePassosNivel.Where(n => n.PassoId == id).ToListAsync();
        await AplicarSituacoesAsync(passo, linhas, novas, (nivelId, s) =>
        {
            var linha = new PePassoNivel { PassoId = id, NivelId = nivelId, Situacao = s };
            _context.PePassosNivel.Add(linha);
            return linha;
        }, PeDominios.EntidadeHistorico.Passo, autor);
        return await PassoAsync(id);
    }

    public async Task<PePassoResponse> ExcluirPassoAsync(long id, string autor)
    {
        var passo = await _context.PePassos.FirstOrDefaultAsync(p => p.Id == id) ?? throw NaoEncontrado("Passo não encontrado.");
        if (passo.Sistema) throw DoSistema("Este passo veio do guia ou do decreto e não pode ser apagado. Você pode desligá-lo nos níveis.");

        if (passo.ExcluidoEm == null)
        {
            var antes = Snap(passo);
            passo.ExcluidoEm = DateTime.UtcNow;
            await SalvarAlteracaoAsync(passo, PeDominios.EntidadeHistorico.Passo, antes, Snap(passo), autor,
                acao: PeDominios.AcaoHistorico.Exclusao);
        }
        return await PassoAsync(id, incluirExcluidos: true);
    }

    public async Task<PeEtapaResponse> OrdenarPassosAsync(PeOrdemDTO dto, string autor)
    {
        var primeiro = PrimeiroId(dto, "Mande os ids dos passos na nova ordem.");
        var etapaId = await _context.PePassos.Where(p => p.Id == primeiro).Select(p => (long?)p.EtapaId).FirstOrDefaultAsync()
            ?? throw new ApiException(ErrorCode.PeOrdemInvalida, "Um dos passos da lista não existe.");

        var passos = await _context.PePassos.Where(p => p.EtapaId == etapaId && p.ExcluidoEm == null).ToListAsync();
        var ids = ValidarOrdem(dto, passos.Select(p => p.Id), "todos os passos da etapa");
        Reordenar(passos, ids, PeDominios.EntidadeHistorico.Passo, autor);
        await _context.SaveChangesAsync();

        var dados = await PeModeloDados.CarregarAsync(_context);
        return dados.Etapa(dados.Etapas.First(e => e.Id == etapaId), false);
    }

    // ── Seções ──────────────────────────────────────────────────────────────

    public async Task<PeSecaoResponse> CriarSecaoAsync(PeSecaoCriarDTO dto, string autor)
    {
        string escopo;
        if (dto.PassoId != null)
        {
            var passo = await _context.PePassos.AsNoTracking().FirstOrDefaultAsync(p => p.Id == dto.PassoId)
                ?? throw Dados("O passo da seção não existe.");
            if (passo.ExcluidoEm != null) throw Excluido("O passo desta seção foi apagado.");
            if (!string.IsNullOrWhiteSpace(dto.Escopo) && dto.Escopo.Trim() != PeDominios.Escopo.Pdtic)
                throw Dados("Seção de um passo é do PDTIC: não informe outro escopo.");
            escopo = PeDominios.Escopo.Pdtic;
        }
        else
        {
            escopo = dto.Escopo?.Trim() ?? string.Empty;
            if (escopo is not (PeDominios.Escopo.Petic or PeDominios.Escopo.Df))
                throw Dados("Diga o passo da seção ou, fora do PDTIC, o escopo (petic ou df).");
        }

        var titulo = Obrigatorio(dto.Titulo, 200, "o título da seção");
        var tipo = Dominio(dto.Tipo, PeDominios.TipoSecao.Todos, "o tipo da seção (formulario ou tabela)");
        var prefixo = Prefixo(dto.PrefixoCodigo);

        var chaves = await _context.PeSecoes.Select(s => s.Chave).ToListAsync();
        string chave;
        if (!string.IsNullOrWhiteSpace(dto.Chave))
        {
            chave = dto.Chave.Trim();
            if (!PeChaves.ChaveValida(chave, PeChaves.MaximoSecao))
                throw Dados("A chave da seção usa só letras minúsculas sem acento, números e sublinhado, e começa por letra.");
            if (chaves.Contains(chave))
                throw new ApiException(ErrorCode.PeChaveDuplicada, $"Já existe uma seção com a chave \"{chave}\".");
        }
        else
        {
            var baseChave = PeChaves.Slug(titulo, '_', PeChaves.MaximoSecao);
            if (baseChave.Length == 0 || !char.IsLetter(baseChave[0])) baseChave = "secao";
            chave = PeChaves.Livre(baseChave, '_', PeChaves.MaximoSecao, chaves.Contains);
        }

        var ordem = (dto.PassoId != null
            ? await _context.PeSecoes.Where(s => s.PassoId == dto.PassoId).MaxAsync(s => (int?)s.Ordem)
            : await _context.PeSecoes.Where(s => s.PassoId == null && s.Escopo == escopo).MaxAsync(s => (int?)s.Ordem)) ?? 0;

        var agora = DateTime.UtcNow;
        var doPdtic = escopo == PeDominios.Escopo.Pdtic;
        var secao = new PeSecao
        {
            PassoId = dto.PassoId,
            Escopo = escopo,
            Chave = chave,
            Titulo = titulo,
            Ajuda = Opcional(dto.Ajuda, 2000, "A ajuda"),
            Tipo = tipo,
            PrefixoCodigo = prefixo,
            Ordem = ordem + 1,
            NoDocumento = true,
            NaPlanilha = true,
            SituacaoGeral = doPdtic ? null : PeDominios.Situacao.Desligado,
            CriadoEm = agora,
            CriadoPor = autor
        };
        _context.PeSecoes.Add(secao);
        if (doPdtic)
        {
            foreach (var nivelId in await _context.PeNiveis.Select(n => n.Id).ToListAsync())
                _context.PeSecoesNivel.Add(new PeSecaoNivel { Secao = secao, NivelId = nivelId, Situacao = PeDominios.Situacao.Desligado });
        }

        await SalvarCriacaoAsync(() => Registrar(PeDominios.EntidadeHistorico.Secao, secao.Id, PeDominios.AcaoHistorico.Criacao,
            null, Snap(secao), autor, agora));
        return await SecaoAsync(secao.Id);
    }

    public async Task<PeSecaoResponse> AtualizarSecaoAsync(long id, PeSecaoAtualizarDTO dto, string autor)
    {
        var secao = await SecaoParaEscritaAsync(id);
        var antes = Snap(secao);

        var mudaTipo = dto.Informou(nameof(dto.Tipo)) && dto.Tipo != null && dto.Tipo.Trim() != secao.Tipo;
        var mudaChave = dto.Informou(nameof(dto.Chave)) && dto.Chave != null && dto.Chave.Trim() != secao.Chave;
        if (secao.Sistema && mudaTipo) throw DoSistema("Esta seção veio do guia ou do decreto e não muda de tipo.");
        if (secao.Sistema && mudaChave) throw DoSistema("Esta seção veio do guia ou do decreto e não muda de chave.");

        if (mudaTipo || mudaChave)
        {
            var ligacoes = await LigacoesParaAsync(secao.Chave);
            if (ligacoes.Count > 0 && (mudaChave || dto.Tipo!.Trim() != PeDominios.TipoSecao.Tabela))
                throw EmUso($"Esta seção é ligada pelo campo \"{ligacoes[0].Rotulo}\". Tire a ligação antes de mudar a chave ou o tipo.");
            // Com registros gravados, a chave e o tipo não mudam mais (E3)
            var registros = await _context.PeRegistros.CountAsync(r => r.SecaoId == id);
            if (registros > 0)
                throw EmUso($"Esta seção já tem {Registros(registros)} gravado{(registros == 1 ? "" : "s")}: a chave e o tipo não mudam mais. "
                            + "Se precisar, crie outra seção.");
        }
        if (mudaTipo) secao.Tipo = Dominio(dto.Tipo, PeDominios.TipoSecao.Todos, "o tipo da seção (formulario ou tabela)");
        if (mudaChave)
        {
            var chave = dto.Chave!.Trim();
            if (!PeChaves.ChaveValida(chave, PeChaves.MaximoSecao))
                throw Dados("A chave da seção usa só letras minúsculas sem acento, números e sublinhado, e começa por letra.");
            if (await _context.PeSecoes.AnyAsync(s => s.Id != id && s.Chave == chave))
                throw new ApiException(ErrorCode.PeChaveDuplicada, $"Já existe uma seção com a chave \"{chave}\".");
            secao.Chave = chave;
        }

        if (dto.Informou(nameof(dto.Titulo))) secao.Titulo = Obrigatorio(dto.Titulo, 200, "o título da seção");
        if (dto.Informou(nameof(dto.Ajuda))) secao.Ajuda = Opcional(dto.Ajuda, 2000, "A ajuda");
        if (dto.Informou(nameof(dto.PrefixoCodigo))) secao.PrefixoCodigo = Prefixo(dto.PrefixoCodigo);
        if (dto.Informou(nameof(dto.NoDocumento)) && dto.NoDocumento != null) secao.NoDocumento = dto.NoDocumento.Value;
        if (dto.Informou(nameof(dto.NaPlanilha)) && dto.NaPlanilha != null) secao.NaPlanilha = dto.NaPlanilha.Value;

        await SalvarAlteracaoAsync(secao, PeDominios.EntidadeHistorico.Secao, antes, Snap(secao), autor);
        return await SecaoAsync(id);
    }

    public async Task<PeSecaoResponse> DefinirSituacaoSecaoAsync(long id, PeSituacoesDTO dto, string autor)
    {
        var secao = await SecaoParaEscritaAsync(id);
        const string mensagem = "Esta seção é um dos nove conteúdos mínimos do PDTIC (art. 12, § 2º) e não pode ser desligada.";

        if (secao.Escopo != PeDominios.Escopo.Pdtic)
        {
            var geral = SituacaoGeral(dto);
            if (secao.Travada && geral == PeDominios.Situacao.Desligado) throw Travado(mensagem);
            await AplicarSituacaoGeralAsync(secao, geral, PeDominios.EntidadeHistorico.Secao, autor);
            return await SecaoAsync(id);
        }

        if (dto.SituacaoGeral != null) throw Dados("Seção do PDTIC não tem situação geral: diga a situação em cada nível.");
        var novas = await LerSituacoesAsync(dto);
        if (secao.Travada && novas.ContainsValue(PeDominios.Situacao.Desligado)) throw Travado(mensagem);

        var linhas = await _context.PeSecoesNivel.Where(n => n.SecaoId == id).ToListAsync();
        await AplicarSituacoesAsync(secao, linhas, novas, (nivelId, s) =>
        {
            var linha = new PeSecaoNivel { SecaoId = id, NivelId = nivelId, Situacao = s };
            _context.PeSecoesNivel.Add(linha);
            return linha;
        }, PeDominios.EntidadeHistorico.Secao, autor);
        return await SecaoAsync(id);
    }

    public async Task<PeSecaoResponse> ExcluirSecaoAsync(long id, string autor)
    {
        var secao = await _context.PeSecoes.FirstOrDefaultAsync(s => s.Id == id) ?? throw NaoEncontrado("Seção não encontrada.");
        if (secao.Sistema) throw DoSistema("Esta seção veio do guia ou do decreto e não pode ser apagada. Você pode desligá-la nos níveis.");

        if (secao.ExcluidoEm == null)
        {
            var ligacoes = await LigacoesParaAsync(secao.Chave);
            if (ligacoes.Count > 0)
                throw EmUso($"Esta seção é ligada pelo campo \"{ligacoes[0].Rotulo}\". Tire a ligação antes de apagar a seção.");

            var antes = Snap(secao);
            secao.ExcluidoEm = DateTime.UtcNow;
            await SalvarAlteracaoAsync(secao, PeDominios.EntidadeHistorico.Secao, antes, Snap(secao), autor,
                acao: PeDominios.AcaoHistorico.Exclusao);
        }
        return await SecaoAsync(id, incluirExcluidos: true);
    }

    public async Task<List<PeSecaoResponse>> OrdenarSecoesAsync(PeOrdemDTO dto, string autor)
    {
        var primeiro = PrimeiroId(dto, "Mande os ids das seções na nova ordem.");
        var referencia = await _context.PeSecoes.AsNoTracking().FirstOrDefaultAsync(s => s.Id == primeiro)
            ?? throw new ApiException(ErrorCode.PeOrdemInvalida, "Uma das seções da lista não existe.");

        // O grupo: as seções do mesmo passo, ou as do mesmo escopo fora do PDTIC
        var grupo = _context.PeSecoes.Where(s => s.ExcluidoEm == null);
        grupo = referencia.PassoId != null
            ? grupo.Where(s => s.PassoId == referencia.PassoId)
            : grupo.Where(s => s.PassoId == null && s.Escopo == referencia.Escopo);
        var secoes = await grupo.ToListAsync();
        var ids = ValidarOrdem(dto, secoes.Select(s => s.Id),
            referencia.PassoId != null ? "todas as seções do passo" : "todas as seções do escopo");
        Reordenar(secoes, ids, PeDominios.EntidadeHistorico.Secao, autor);
        await _context.SaveChangesAsync();

        var dados = await PeModeloDados.CarregarAsync(_context);
        return ids.Select(i => dados.Secao(dados.Secoes.First(s => s.Id == i), false)).ToList();
    }

    // ── Campos ──────────────────────────────────────────────────────────────

    public async Task<PeCampoResponse> CriarCampoAsync(PeCampoCriarDTO dto, string autor)
    {
        var secao = await _context.PeSecoes.AsNoTracking().FirstOrDefaultAsync(s => s.Id == dto.SecaoId)
            ?? throw Dados("A seção do campo não existe.");
        await GarantirSecaoAtivaAsync(secao);

        var rotulo = Obrigatorio(dto.Rotulo, 200, "o rótulo do campo");
        var tipo = Dominio(dto.Tipo, PeDominios.TipoCampo.Todos, "o tipo do campo");
        var largura = Largura(dto.Largura);

        var campos = await _context.PeCampos.AsNoTracking().Where(c => c.SecaoId == secao.Id).ToListAsync();
        string chave;
        if (!string.IsNullOrWhiteSpace(dto.Chave))
        {
            chave = dto.Chave.Trim();
            if (!PeChaves.ChaveValida(chave, PeChaves.MaximoCampo))
                throw Dados("A chave do campo usa só letras minúsculas sem acento, números e sublinhado, e começa por letra.");
            if (campos.Any(c => c.Chave == chave))
                throw new ApiException(ErrorCode.PeChaveDuplicada, $"Já existe um campo com a chave \"{chave}\" nesta seção (inclusive entre os apagados).");
        }
        else
        {
            var baseChave = PeChaves.Slug(rotulo, '_', PeChaves.MaximoCampo);
            if (baseChave.Length == 0 || !char.IsLetter(baseChave[0])) baseChave = "campo";
            chave = PeChaves.Livre(baseChave, '_', PeChaves.MaximoCampo, k => campos.Any(c => c.Chave == k));
        }

        var config = PeConfigCampo.Normalizar(tipo, dto.Config, await ContextoConfigAsync(secao, chave, campos, Array.Empty<string>()));

        var agora = DateTime.UtcNow;
        var doPdtic = secao.Escopo == PeDominios.Escopo.Pdtic;
        var campo = new PeCampo
        {
            SecaoId = secao.Id,
            Chave = chave,
            Rotulo = rotulo,
            Ajuda = Opcional(dto.Ajuda, 2000, "A ajuda"),
            Tipo = tipo,
            Config = config,
            Ordem = (campos.Max(c => (int?)c.Ordem) ?? 0) + 1,
            NoDocumento = true,
            NaPlanilha = true,
            Largura = largura,
            SituacaoGeral = doPdtic ? null : PeDominios.Situacao.Desligado,
            CriadoEm = agora,
            CriadoPor = autor
        };
        _context.PeCampos.Add(campo);
        if (doPdtic)
        {
            foreach (var nivelId in await _context.PeNiveis.Select(n => n.Id).ToListAsync())
                _context.PeCamposNivel.Add(new PeCampoNivel { Campo = campo, NivelId = nivelId, Situacao = PeDominios.Situacao.Desligado });
        }

        await SalvarCriacaoAsync(() => Registrar(PeDominios.EntidadeHistorico.Campo, campo.Id, PeDominios.AcaoHistorico.Criacao,
            null, Snap(campo), autor, agora));
        return await CampoAsync(campo.Id);
    }

    public async Task<PeCampoResponse> AtualizarCampoAsync(long id, PeCampoAtualizarDTO dto, string autor)
    {
        var campo = await CampoParaEscritaAsync(id);
        var secao = campo.Secao!;
        var antes = Snap(campo);

        var irmaos = await _context.PeCampos.AsNoTracking().Where(c => c.SecaoId == secao.Id && c.Id != id).ToListAsync();
        var novoTipo = dto.Informou(nameof(dto.Tipo)) && dto.Tipo != null ? dto.Tipo.Trim() : campo.Tipo;
        var mudaTipo = novoTipo != campo.Tipo;
        var mudaChave = dto.Informou(nameof(dto.Chave)) && dto.Chave != null && dto.Chave.Trim() != campo.Chave;

        if (campo.Sistema && mudaTipo) throw DoSistema("Este campo veio do guia ou do decreto e não muda de tipo.");
        if (campo.Sistema && mudaChave) throw DoSistema("Este campo veio do guia ou do decreto e não muda de chave.");
        if (mudaTipo || mudaChave)
        {
            var calculos = CalculosQueUsam(irmaos, campo.Chave);
            if (calculos.Count > 0)
                throw EmUso($"Este campo entra no cálculo \"{calculos[0].Rotulo}\". Tire-o do cálculo antes de mudar a chave ou o tipo.");
            // Com valor gravado em algum registro, a chave e o tipo não mudam mais (E3)
            var comValor = await RegistrosComValorAsync(campo);
            if (comValor > 0)
                throw EmUso($"Este campo já tem dados em {Registros(comValor)}: a chave e o tipo não mudam mais. "
                            + "Se precisar, crie outro campo.");
        }
        var alvoAntes = AlvoDaLigacao(campo.Tipo, campo.Config);
        if (mudaTipo) Dominio(novoTipo, PeDominios.TipoCampo.Todos, "o tipo do campo");
        if (mudaChave)
        {
            var chave = dto.Chave!.Trim();
            if (!PeChaves.ChaveValida(chave, PeChaves.MaximoCampo))
                throw Dados("A chave do campo usa só letras minúsculas sem acento, números e sublinhado, e começa por letra.");
            if (irmaos.Any(c => c.Chave == chave))
                throw new ApiException(ErrorCode.PeChaveDuplicada, $"Já existe um campo com a chave \"{chave}\" nesta seção (inclusive entre os apagados).");
            campo.Chave = chave;
        }

        // Config: o enviado, ou o atual conferido de novo quando o tipo muda
        if (dto.Informou(nameof(dto.Config)) || mudaTipo)
        {
            JsonElement? config = dto.Informou(nameof(dto.Config)) ? dto.Config : PeModeloDados.Json(campo.Config);
            var opcoes = await _context.PeOpcoes.AsNoTracking().Where(o => o.CampoId == id).Select(o => o.Valor).ToListAsync();
            var novoConfig = PeConfigCampo.Normalizar(novoTipo, config, await ContextoConfigAsync(secao, campo.Chave, irmaos, opcoes));

            // Ligação com ligações gravadas não troca a seção nem o catálogo que liga (E3)
            if (!mudaTipo && alvoAntes != null && AlvoDaLigacao(novoTipo, novoConfig) != alvoAntes
                && await _context.PeVinculos.AnyAsync(v => v.CampoId == id))
                throw EmUso("Este campo já tem ligações gravadas: a seção ou o catálogo que ele liga não muda mais. "
                            + "Se precisar, crie outro campo.");
            campo.Config = novoConfig;
        }
        campo.Tipo = novoTipo;

        if (dto.Informou(nameof(dto.Rotulo))) campo.Rotulo = Obrigatorio(dto.Rotulo, 200, "o rótulo do campo");
        if (dto.Informou(nameof(dto.Ajuda))) campo.Ajuda = Opcional(dto.Ajuda, 2000, "A ajuda");
        if (dto.Informou(nameof(dto.Largura))) campo.Largura = Largura(dto.Largura);
        if (dto.Informou(nameof(dto.NoDocumento)) && dto.NoDocumento != null) campo.NoDocumento = dto.NoDocumento.Value;
        if (dto.Informou(nameof(dto.NaPlanilha)) && dto.NaPlanilha != null) campo.NaPlanilha = dto.NaPlanilha.Value;

        await SalvarAlteracaoAsync(campo, PeDominios.EntidadeHistorico.Campo, antes, Snap(campo), autor);
        return await CampoAsync(id);
    }

    public async Task<PeCampoResponse> DefinirSituacaoCampoAsync(long id, PeSituacoesDTO dto, string autor)
    {
        var campo = await CampoParaEscritaAsync(id);
        var secao = campo.Secao!;
        var travado = campo.Travado || (campo.Principal && secao.Travada);
        var mensagem = campo.Travado
            ? "Este campo é travado: carrega conteúdos mínimos do PDTIC (art. 12, § 2º) e não pode ser desligado."
            : "O campo principal de uma seção travada (art. 12, § 2º) não pode ser desligado.";

        if (secao.Escopo != PeDominios.Escopo.Pdtic)
        {
            var geral = SituacaoGeral(dto);
            if (travado && geral == PeDominios.Situacao.Desligado) throw Travado(mensagem);
            await AplicarSituacaoGeralAsync(campo, geral, PeDominios.EntidadeHistorico.Campo, autor);
            return await CampoAsync(id);
        }

        if (dto.SituacaoGeral != null) throw Dados("Campo de seção do PDTIC não tem situação geral: diga a situação em cada nível.");
        var novas = await LerSituacoesAsync(dto);
        if (travado && novas.ContainsValue(PeDominios.Situacao.Desligado)) throw Travado(mensagem);

        var linhas = await _context.PeCamposNivel.Where(n => n.CampoId == id).ToListAsync();
        await AplicarSituacoesAsync(campo, linhas, novas, (nivelId, s) =>
        {
            var linha = new PeCampoNivel { CampoId = id, NivelId = nivelId, Situacao = s };
            _context.PeCamposNivel.Add(linha);
            return linha;
        }, PeDominios.EntidadeHistorico.Campo, autor);
        return await CampoAsync(id);
    }

    public async Task<PeCampoResponse> ExcluirCampoAsync(long id, string autor)
    {
        var campo = await _context.PeCampos.FirstOrDefaultAsync(c => c.Id == id) ?? throw NaoEncontrado("Campo não encontrado.");
        if (campo.Sistema) throw DoSistema("Este campo veio do guia ou do decreto e não pode ser apagado. Você pode desligá-lo nos níveis.");

        if (campo.ExcluidoEm == null)
        {
            var irmaos = await _context.PeCampos.AsNoTracking().Where(c => c.SecaoId == campo.SecaoId && c.Id != id).ToListAsync();
            var calculos = CalculosQueUsam(irmaos, campo.Chave);
            if (calculos.Count > 0)
                throw EmUso($"Este campo entra no cálculo \"{calculos[0].Rotulo}\". Tire-o do cálculo antes de apagar.");

            var antes = Snap(campo);
            campo.ExcluidoEm = DateTime.UtcNow;
            await SalvarAlteracaoAsync(campo, PeDominios.EntidadeHistorico.Campo, antes, Snap(campo), autor,
                acao: PeDominios.AcaoHistorico.Exclusao);
        }
        return await CampoAsync(id);
    }

    public async Task<PeSecaoResponse> OrdenarCamposAsync(PeOrdemDTO dto, string autor)
    {
        var primeiro = PrimeiroId(dto, "Mande os ids dos campos na nova ordem.");
        var secaoId = await _context.PeCampos.Where(c => c.Id == primeiro).Select(c => (long?)c.SecaoId).FirstOrDefaultAsync()
            ?? throw new ApiException(ErrorCode.PeOrdemInvalida, "Um dos campos da lista não existe.");

        var campos = await _context.PeCampos.Where(c => c.SecaoId == secaoId && c.ExcluidoEm == null).ToListAsync();
        var ids = ValidarOrdem(dto, campos.Select(c => c.Id), "todos os campos da seção");
        Reordenar(campos, ids, PeDominios.EntidadeHistorico.Campo, autor);
        await _context.SaveChangesAsync();
        return await SecaoAsync(secaoId);
    }

    // ── Opções ──────────────────────────────────────────────────────────────

    public async Task<PeOpcaoResponse> CriarOpcaoAsync(long campoId, PeOpcaoCriarDTO dto, string autor)
    {
        var campo = await CampoParaEscritaAsync(campoId);
        var aceitaOpcoes = PeDominios.TipoCampo.TemOpcoes(campo.Tipo)
            || (campo.Tipo == PeDominios.TipoCampo.Calculado && PeConfigCampo.TipoDoCalculo(campo.Config) == PeDominios.Calculo.NivelRisco);
        if (!aceitaOpcoes)
            throw Dados("Só campos de lista (e o nível de risco calculado) têm opções.");

        var rotulo = Obrigatorio(dto.Rotulo, 200, "o rótulo da opção");
        var cor = Cor(dto.Cor);
        var opcoes = await _context.PeOpcoes.AsNoTracking().Where(o => o.CampoId == campoId).ToListAsync();

        // Lista que entra num cálculo numérico (a prioridade, por exemplo) só aceita valor numérico
        var irmaos = await _context.PeCampos.AsNoTracking().Where(c => c.SecaoId == campo.SecaoId && c.Id != campoId).ToListAsync();
        var numerica = CalculosQueUsam(irmaos, campo.Chave).Any(c => PeConfigCampo.CalculoNumerico(PeConfigCampo.TipoDoCalculo(c.Config)));

        string valor;
        if (!string.IsNullOrWhiteSpace(dto.Valor))
        {
            valor = dto.Valor.Trim();
            if (!PeChaves.ValorValido(valor))
                throw Dados("O valor da opção usa só letras minúsculas sem acento, números e sublinhado.");
            if (opcoes.Any(o => o.Valor == valor))
                throw new ApiException(ErrorCode.PeChaveDuplicada, $"Já existe uma opção com o valor \"{valor}\" neste campo.");
        }
        else
        {
            if (numerica) throw Dados("Este campo entra num cálculo: informe o valor numérico da opção (por exemplo, 6).");
            var baseValor = PeChaves.Slug(rotulo, '_', PeChaves.MaximoOpcao);
            if (baseValor.Length == 0) baseValor = "opcao";
            valor = PeChaves.Livre(baseValor, '_', PeChaves.MaximoOpcao, v => opcoes.Any(o => o.Valor == v));
        }
        if (numerica && !PeConfigCampo.EhNumero(valor))
            throw Dados("Este campo entra num cálculo: o valor da opção precisa ser um número.");

        var agora = DateTime.UtcNow;
        var opcao = new PeOpcao
        {
            CampoId = campoId,
            Valor = valor,
            Rotulo = rotulo,
            Cor = cor,
            Ordem = (opcoes.Max(o => (int?)o.Ordem) ?? 0) + 1,
            Ativa = true,
            CriadoEm = agora,
            CriadoPor = autor
        };
        _context.PeOpcoes.Add(opcao);

        await SalvarCriacaoAsync(() => Registrar(PeDominios.EntidadeHistorico.Opcao, opcao.Id, PeDominios.AcaoHistorico.Criacao,
            null, Snap(opcao), autor, agora));
        return PeModeloDados.Opcao(opcao);
    }

    public async Task<PeOpcaoResponse> AtualizarOpcaoAsync(long id, PeOpcaoAtualizarDTO dto, string autor)
    {
        var opcao = await _context.PeOpcoes.FirstOrDefaultAsync(o => o.Id == id) ?? throw NaoEncontrado("Opção não encontrada.");
        await CampoParaEscritaAsync(opcao.CampoId);
        var antes = Snap(opcao);

        if (dto.Informou(nameof(dto.Rotulo))) opcao.Rotulo = Obrigatorio(dto.Rotulo, 200, "o rótulo da opção");
        if (dto.Informou(nameof(dto.Cor))) opcao.Cor = Cor(dto.Cor);
        if (dto.Informou(nameof(dto.Ativa)) && dto.Ativa != null && dto.Ativa != opcao.Ativa)
        {
            if (dto.Ativa == false && opcao.Travada)
                throw Travado("Esta opção é um dos temas do decreto (art. 12, § 2º, V, VI e IX) e não pode ser desativada.");
            opcao.Ativa = dto.Ativa.Value;
        }

        await SalvarAlteracaoAsync(opcao, PeDominios.EntidadeHistorico.Opcao, antes, Snap(opcao), autor);
        return PeModeloDados.Opcao(opcao);
    }

    public async Task<PeOpcaoResponse?> ExcluirOpcaoAsync(long id, string autor)
    {
        var opcao = await _context.PeOpcoes.FirstOrDefaultAsync(o => o.Id == id) ?? throw NaoEncontrado("Opção não encontrada.");
        var campo = await CampoParaEscritaAsync(opcao.CampoId);
        if (opcao.Travada)
            throw Travado("Esta opção é um dos temas do decreto (art. 12, § 2º, V, VI e IX) e não pode ser apagada nem desativada.");

        var antes = Snap(opcao);
        if (opcao.Sistema || await OpcaoEmUsoAsync(opcao, campo))
        {
            // Do sistema ou em uso: só desativa (os registros que a usam continuam válidos)
            if (opcao.Ativa)
            {
                opcao.Ativa = false;
                await SalvarAlteracaoAsync(opcao, PeDominios.EntidadeHistorico.Opcao, antes, Snap(opcao), autor);
            }
            return PeModeloDados.Opcao(opcao);
        }

        _context.PeOpcoes.Remove(opcao);
        Registrar(PeDominios.EntidadeHistorico.Opcao, opcao.Id, PeDominios.AcaoHistorico.Remocao, antes, null, autor, DateTime.UtcNow);
        await _context.SaveChangesAsync();
        return null;
    }

    public async Task<PeCampoResponse> OrdenarOpcoesAsync(long campoId, PeOrdemDTO dto, string autor)
    {
        await CampoParaEscritaAsync(campoId);
        var opcoes = await _context.PeOpcoes.Where(o => o.CampoId == campoId).ToListAsync();
        var ids = ValidarOrdem(dto, opcoes.Select(o => o.Id), "todas as opções do campo");
        Reordenar(opcoes, ids, PeDominios.EntidadeHistorico.Opcao, autor);
        await _context.SaveChangesAsync();
        return await CampoAsync(campoId);
    }

    // ── Leitura para escrita (item e cadeia não apagados) ───────────────────

    private async Task<PePasso> PassoParaEscritaAsync(long id)
    {
        var passo = await _context.PePassos.FirstOrDefaultAsync(p => p.Id == id) ?? throw NaoEncontrado("Passo não encontrado.");
        if (passo.ExcluidoEm != null) throw Excluido("Este passo foi apagado.");
        return passo;
    }

    private async Task<PeSecao> SecaoParaEscritaAsync(long id)
    {
        var secao = await _context.PeSecoes.FirstOrDefaultAsync(s => s.Id == id) ?? throw NaoEncontrado("Seção não encontrada.");
        await GarantirSecaoAtivaAsync(secao);
        return secao;
    }

    private async Task<PeCampo> CampoParaEscritaAsync(long id)
    {
        var campo = await _context.PeCampos.Include(c => c.Secao).FirstOrDefaultAsync(c => c.Id == id)
            ?? throw NaoEncontrado("Campo não encontrado.");
        if (campo.ExcluidoEm != null) throw Excluido("Este campo foi apagado.");
        await GarantirSecaoAtivaAsync(campo.Secao!);
        return campo;
    }

    private async Task GarantirSecaoAtivaAsync(PeSecao secao)
    {
        if (secao.ExcluidoEm != null) throw Excluido("Esta seção foi apagada.");
        if (secao.PassoId != null
            && await _context.PePassos.AnyAsync(p => p.Id == secao.PassoId && p.ExcluidoEm != null))
            throw Excluido("O passo desta seção foi apagado.");
    }

    // ── Regras de uso (cálculo, ligação, opção) ─────────────────────────────

    private static List<PeCampo> CalculosQueUsam(IEnumerable<PeCampo> irmaos, string chave) =>
        irmaos.Where(c => c.ExcluidoEm == null && c.Tipo == PeDominios.TipoCampo.Calculado
                          && PeConfigCampo.CamposDoCalculo(c.Config).Contains(chave))
            .ToList();

    /// <summary>Campos (não apagados, de seções não apagadas) que ligam esta seção.</summary>
    private async Task<List<PeCampo>> LigacoesParaAsync(string chaveSecao)
    {
        var ligacoes = await _context.PeCampos.AsNoTracking()
            .Where(c => c.ExcluidoEm == null && c.Tipo == PeDominios.TipoCampo.LigacaoSecao && c.Secao!.ExcluidoEm == null)
            .ToListAsync();
        return ligacoes.Where(c => PeConfigCampo.SecaoDaLigacao(c.Config) == chaveSecao).ToList();
    }

    /// <summary>
    /// A opção está em uso quando aparece na matriz de um nível de risco da mesma seção ou
    /// quando algum registro a guarda no campo (lista, lista múltipla ou o resultado do nível
    /// de risco), de qualquer dono.
    /// </summary>
    private async Task<bool> OpcaoEmUsoAsync(PeOpcao opcao, PeCampo campo)
    {
        var calculos = await _context.PeCampos.AsNoTracking()
            .Where(c => c.SecaoId == campo.SecaoId && c.ExcluidoEm == null && c.Tipo == PeDominios.TipoCampo.Calculado)
            .ToListAsync();
        if (calculos.Any(c => PeConfigCampo.TipoDoCalculo(c.Config) == PeDominios.Calculo.NivelRisco
                              && (c.Id == campo.Id || PeConfigCampo.CamposDoCalculo(c.Config).Contains(campo.Chave))
                              && PeConfigCampo.ValoresDaMatriz(c.Config).Contains(opcao.Valor)))
            return true;

        var dados = await _context.PeRegistros.AsNoTracking()
            .Where(r => r.SecaoId == campo.SecaoId)
            .Select(r => r.Dados)
            .ToListAsync();
        return dados.Any(d =>
        {
            var valor = PeRegistroDados.Ler(d)[campo.Chave];
            return campo.Tipo == PeDominios.TipoCampo.ListaMultipla
                ? PeRegistroDados.Textos(valor).Contains(opcao.Valor)
                : PeRegistroDados.Texto(valor) == opcao.Valor;
        });
    }

    /// <summary>
    /// Em quantos registros (de qualquer dono) o campo tem valor: no jsonb, ou, no campo de
    /// ligação, nas ligações gravadas.
    /// </summary>
    private async Task<int> RegistrosComValorAsync(PeCampo campo)
    {
        if (PeRegistroDados.EhLigacao(campo))
            return await _context.PeVinculos.Where(v => v.CampoId == campo.Id)
                .Select(v => v.RegistroOrigemId).Distinct().CountAsync();

        var dados = await _context.PeRegistros.AsNoTracking()
            .Where(r => r.SecaoId == campo.SecaoId)
            .Select(r => r.Dados)
            .ToListAsync();
        return dados.Count(d => PeRegistroDados.TemValor(d, campo.Chave));
    }

    /// <summary>A seção ou o catálogo que um campo de ligação liga (nulo nos outros tipos).</summary>
    private static string? AlvoDaLigacao(string tipo, string config) => tipo switch
    {
        PeDominios.TipoCampo.LigacaoSecao => "secao:" + PeConfigCampo.SecaoDaLigacao(config),
        PeDominios.TipoCampo.LigacaoCatalogo => "catalogo:" + PeValores.Texto(config, "catalogo"),
        _ => null
    };

    private static string Registros(int quantidade) => quantidade == 1 ? "1 registro" : $"{quantidade} registros";

    private async Task<PeContextoConfig> ContextoConfigAsync(PeSecao secao, string chaveDoCampo, IEnumerable<PeCampo> irmaos,
        IReadOnlyList<string> opcoesDoCampo)
    {
        var ativos = irmaos.Where(c => c.ExcluidoEm == null && c.Chave != chaveDoCampo).ToList();
        var idsAtivos = ativos.Select(c => c.Id).ToList();
        var opcoes = await _context.PeOpcoes.AsNoTracking().Where(o => idsAtivos.Contains(o.CampoId)).ToListAsync();
        var secoes = await _context.PeSecoes.AsNoTracking().Where(s => s.ExcluidoEm == null)
            .Select(s => new PeSecaoInfo(s.Chave, s.Escopo, s.Tipo)).ToListAsync();

        return new PeContextoConfig
        {
            Escopo = secao.Escopo,
            ChaveDoCampo = chaveDoCampo,
            CamposDaSecao = ativos.Select(c => new PeCampoInfo(c.Chave, c.Tipo,
                opcoes.Where(o => o.CampoId == c.Id).Select(o => o.Valor).ToList(),
                opcoes.Where(o => o.CampoId == c.Id && o.Ativa).Select(o => o.Valor).ToList())).ToList(),
            SecaoPorChave = chave => secoes.FirstOrDefault(s => s.Chave == chave),
            OpcoesDoCampo = opcoesDoCampo
        };
    }

    // ── Situações ───────────────────────────────────────────────────────────

    private async Task<Dictionary<long, string>> LerSituacoesAsync(PeSituacoesDTO dto)
    {
        if (dto.Niveis == null || dto.Niveis.Count == 0)
            throw Dados("Informe a situação de pelo menos um nível.");

        var niveis = await _context.PeNiveis.Select(n => n.Id).ToListAsync();
        var saida = new Dictionary<long, string>();
        foreach (var (chave, valor) in dto.Niveis)
        {
            if (!long.TryParse(chave, out var nivelId) || !niveis.Contains(nivelId))
                throw Dados($"O nível {chave} não existe.");
            var situacao = valor?.Trim();
            if (situacao == null || !PeDominios.Situacao.Todas.Contains(situacao))
                throw Dados($"Situação inválida: \"{valor}\". Use obrigatorio, opcional ou desligado.");
            saida[nivelId] = situacao;
        }
        return saida;
    }

    private static string SituacaoGeral(PeSituacoesDTO dto)
    {
        if (dto.Niveis is { Count: > 0 }) throw Dados("Fora do PDTIC não há níveis: diga a situação geral.");
        var geral = dto.SituacaoGeral?.Trim();
        if (geral == null || !PeDominios.Situacao.Todas.Contains(geral))
            throw Dados("Informe a situação geral: obrigatorio, opcional ou desligado.");
        return geral;
    }

    private async Task AplicarSituacoesAsync<T>(IPeAuditavel item, List<T> linhas, Dictionary<long, string> novas,
        Func<long, string, T> criar, string entidade, string autor) where T : class, IPeSituacaoNivel
    {
        var niveis = await _context.PeNiveis.AsNoTracking().OrderBy(n => n.Ordem).ThenBy(n => n.Id).Select(n => n.Id).ToListAsync();
        var antes = MapaSituacoes(niveis, linhas);

        foreach (var (nivelId, situacao) in novas)
        {
            var linha = linhas.FirstOrDefault(l => l.NivelId == nivelId);
            if (linha == null) linhas.Add(criar(nivelId, situacao));
            else linha.Situacao = situacao;
        }

        var depois = MapaSituacoes(niveis, linhas);
        if (depois.SequenceEqual(antes)) return;

        var agora = DateTime.UtcNow;
        item.AlteradoEm = agora;
        item.AlteradoPor = autor;
        Registrar(entidade, IdDe(item), PeDominios.AcaoHistorico.Situacao, new { Niveis = antes }, new { Niveis = depois }, autor, agora);
        await _context.SaveChangesAsync();
    }

    private async Task AplicarSituacaoGeralAsync(IPeAuditavel item, string situacao, string entidade, string autor)
    {
        var anterior = item switch
        {
            PeSecao s => s.SituacaoGeral,
            PeCampo c => c.SituacaoGeral,
            _ => null
        };
        if (anterior == situacao) return;

        switch (item)
        {
            case PeSecao s: s.SituacaoGeral = situacao; break;
            case PeCampo c: c.SituacaoGeral = situacao; break;
        }
        var agora = DateTime.UtcNow;
        item.AlteradoEm = agora;
        item.AlteradoPor = autor;
        Registrar(entidade, IdDe(item), PeDominios.AcaoHistorico.Situacao,
            new { SituacaoGeral = anterior }, new { SituacaoGeral = situacao }, autor, agora);
        await _context.SaveChangesAsync();
    }

    private static Dictionary<string, string> MapaSituacoes<T>(IEnumerable<long> niveis, IEnumerable<T> linhas)
        where T : IPeSituacaoNivel =>
        niveis.ToDictionary(n => n.ToString(),
            n => linhas.FirstOrDefault(l => l.NivelId == n)?.Situacao ?? PeDominios.Situacao.Desligado);

    // ── Ordem ───────────────────────────────────────────────────────────────

    private static long PrimeiroId(PeOrdemDTO dto, string mensagem) =>
        dto.Ids is { Count: > 0 } ids ? ids[0] : throw new ApiException(ErrorCode.PeOrdemInvalida, mensagem);

    private static List<long> ValidarOrdem(PeOrdemDTO dto, IEnumerable<long> grupo, string oQue = "todos os níveis")
    {
        var ids = dto.Ids ?? new List<long>();
        var esperados = grupo.ToHashSet();
        if (ids.Count == 0 || ids.Distinct().Count() != ids.Count || !esperados.SetEquals(ids))
            throw new ApiException(ErrorCode.PeOrdemInvalida, $"Mande {oQue}, cada um uma vez, na nova ordem.");
        return ids;
    }

    /// <summary>
    /// Numera o grupo de 1 em diante na ordem dos ids e registra no histórico cada item que
    /// mudou de lugar.
    /// </summary>
    private void Reordenar<T>(List<T> grupo, List<long> ids, string entidade, string autor)
        where T : class, IPeOrdenavel, IPeAuditavel
    {
        var agora = DateTime.UtcNow;
        foreach (var item in grupo)
        {
            var nova = ids.IndexOf(item.Id) + 1;
            if (nova == 0 || nova == item.Ordem) continue;
            Registrar(entidade, item.Id, PeDominios.AcaoHistorico.Ordem, new { item.Ordem }, new { Ordem = nova }, autor, agora);
            item.Ordem = nova;
            item.AlteradoEm = agora;
            item.AlteradoPor = autor;
        }
    }

    // ── Gravação e histórico ────────────────────────────────────────────────

    /// <summary>
    /// Criação: grava o item (para ter o id) e o histórico dele na mesma transação (no
    /// PostgreSQL; o InMemory dos testes não tem transação).
    /// </summary>
    private async Task SalvarCriacaoAsync(Action registrarHistorico)
    {
        await using var transacao = _context.Database.IsRelational() ? await _context.Database.BeginTransactionAsync() : null;
        await _context.SaveChangesAsync();
        registrarHistorico();
        await _context.SaveChangesAsync();
        if (transacao != null) await transacao.CommitAsync();
    }

    /// <summary>Grava a alteração com o histórico, só se algo mudou de fato.</summary>
    private async Task SalvarAlteracaoAsync(IPeAuditavel item, string entidade, object antes, object depois, string autor,
        DateTime? quando = null, string acao = PeDominios.AcaoHistorico.Alteracao)
    {
        var jsonAntes = JsonSerializer.Serialize(antes, JsonHistorico);
        var jsonDepois = JsonSerializer.Serialize(depois, JsonHistorico);
        if (jsonAntes != jsonDepois)
        {
            var agora = quando ?? DateTime.UtcNow;
            item.AlteradoEm = agora;
            item.AlteradoPor = autor;
            _context.PeModeloHistorico.Add(new PeModeloHistorico
            {
                Entidade = entidade,
                EntidadeId = IdDe(item),
                Acao = acao,
                Antes = jsonAntes,
                Depois = jsonDepois,
                AlteradoEm = agora,
                AlteradoPor = autor
            });
        }
        await _context.SaveChangesAsync();
    }

    private void Registrar(string entidade, long id, string acao, object? antes, object? depois, string autor, DateTime quando) =>
        _context.PeModeloHistorico.Add(new PeModeloHistorico
        {
            Entidade = entidade,
            EntidadeId = id,
            Acao = acao,
            Antes = antes == null ? null : JsonSerializer.Serialize(antes, JsonHistorico),
            Depois = depois == null ? null : JsonSerializer.Serialize(depois, JsonHistorico),
            AlteradoEm = quando,
            AlteradoPor = autor
        });

    private static long IdDe(IPeAuditavel item) => item switch
    {
        IPeOrdenavel o => o.Id,
        PeOrgaoAjuste a => a.Id,
        _ => 0
    };

    private static object Snap(PeNivel n, string? copiadoDe = null) =>
        copiadoDe == null
            ? new { n.Codigo, n.Nome, n.Descricao, n.Ordem, n.Ativo }
            : new { n.Codigo, n.Nome, n.Descricao, n.Ordem, n.Ativo, CopiadoDe = copiadoDe };

    // Sem a ordem: a mudança de lugar tem registro próprio ("ordem")
    private static object Snap(PeEtapa e) => new { e.Chave, e.Titulo, e.Descricao, e.ReferenciaGuia };

    private static object Snap(PePasso p) => new
    {
        p.EtapaId, p.Chave, p.Titulo, p.OQueFazer, p.BaseLegal, p.ReferenciaGuia, p.Tipo, p.AceitaNaoSeAplica, p.Ordem,
        Excluido = p.ExcluidoEm != null
    };

    private static object Snap(PeSecao s) => new
    {
        s.PassoId, s.Escopo, s.Chave, s.Titulo, s.Ajuda, s.Tipo, s.PrefixoCodigo, s.NoDocumento, s.NaPlanilha, s.Ordem,
        Excluido = s.ExcluidoEm != null
    };

    private static object Snap(PeCampo c) => new
    {
        c.SecaoId, c.Chave, c.Rotulo, c.Ajuda, c.Tipo, Config = PeModeloDados.Json(c.Config), c.Largura, c.NoDocumento,
        c.NaPlanilha, c.Ordem, Excluido = c.ExcluidoEm != null
    };

    private static object Snap(PeOpcao o) => new { o.CampoId, o.Valor, o.Rotulo, o.Cor, o.Ativa, o.Ordem };

    // ── Respostas ───────────────────────────────────────────────────────────

    private async Task<PePassoResponse> PassoAsync(long id, bool incluirExcluidos = false)
    {
        var dados = await PeModeloDados.CarregarAsync(_context);
        return dados.Passo(dados.Passos.First(p => p.Id == id), incluirExcluidos);
    }

    private async Task<PeSecaoResponse> SecaoAsync(long id, bool incluirExcluidos = false)
    {
        var dados = await PeModeloDados.CarregarAsync(_context);
        return dados.Secao(dados.Secoes.First(s => s.Id == id), incluirExcluidos);
    }

    private async Task<PeCampoResponse> CampoAsync(long id)
    {
        var dados = await PeModeloDados.CarregarAsync(_context);
        var campo = dados.Campos.First(c => c.Id == id);
        var secao = dados.Secoes.First(s => s.Id == campo.SecaoId);
        return dados.Campo(campo, secao.Escopo == PeDominios.Escopo.Pdtic);
    }

    // ── Validação de entrada ────────────────────────────────────────────────

    /// <summary>Texto obrigatório: sem espaço nas pontas, não vazio, até o máximo.</summary>
    internal static string Obrigatorio(string? valor, int maximo, string oQue)
    {
        var texto = valor?.Trim();
        if (string.IsNullOrEmpty(texto)) throw Dados($"Informe {oQue}.");
        if (texto.Length > maximo) throw Dados($"{Capitular(oQue)} tem no máximo {maximo} caracteres.");
        return texto;
    }

    /// <summary>Texto opcional: vazio vira nulo.</summary>
    internal static string? Opcional(string? valor, int maximo, string oQue)
    {
        var texto = valor?.Trim();
        if (string.IsNullOrEmpty(texto)) return null;
        if (texto.Length > maximo) throw Dados($"{oQue} tem no máximo {maximo} caracteres.");
        return texto;
    }

    private static string Capitular(string texto) => texto.Length == 0 ? texto : char.ToUpperInvariant(texto[0]) + texto[1..];

    private static string Dominio(string? valor, string[] dominio, string oQue)
    {
        var texto = valor?.Trim();
        if (string.IsNullOrEmpty(texto) || !dominio.Contains(texto))
            throw Dados($"Informe {oQue}. Valores aceitos: {string.Join(", ", dominio)}.");
        return texto;
    }

    private static string? Prefixo(string? valor)
    {
        var texto = valor?.Trim().ToUpperInvariant();
        if (string.IsNullOrEmpty(texto)) return null;
        if (texto.Length > 5 || !char.IsAsciiLetterUpper(texto[0]) || texto.Any(c => !char.IsAsciiLetterUpper(c) && !char.IsAsciiDigit(c)))
            throw Dados("O prefixo do código tem de 1 a 5 letras ou números e começa por letra (por exemplo, N ou PD).");
        return texto;
    }

    private static string? Largura(string? valor)
    {
        var texto = valor?.Trim();
        if (string.IsNullOrEmpty(texto)) return null;
        if (!PeDominios.Largura.Todas.Contains(texto))
            throw Dados($"Largura inválida. Use {string.Join(", ", PeDominios.Largura.Todas)}.");
        return texto;
    }

    private static string? Cor(string? valor)
    {
        var texto = valor?.Trim();
        if (string.IsNullOrEmpty(texto)) return null;
        if (!PeDominios.Cor.Todas.Contains(texto))
            throw Dados($"Cor inválida. Use {string.Join(", ", PeDominios.Cor.Todas)}.");
        return texto;
    }

    // ── Erros ───────────────────────────────────────────────────────────────

    internal static ApiException Dados(string mensagem) => new(ErrorCode.PeDadosInvalidos, mensagem);

    private static ApiException NaoEncontrado(string mensagem) =>
        new(ErrorCode.PeItemNaoEncontrado, mensagem + " Atualize a tela.");

    private static ApiException Travado(string mensagem) => new(ErrorCode.PeItemTravado, mensagem);

    private static ApiException DoSistema(string mensagem) => new(ErrorCode.PeItemDoSistema, mensagem);

    private static ApiException Excluido(string mensagem) => new(ErrorCode.PeItemExcluido, mensagem);

    private static ApiException EmUso(string mensagem) => new(ErrorCode.PeItemEmUso, mensagem);

    internal static ApiException ModeloIndisponivel() => new(ErrorCode.PeModeloIndisponivel,
        "O modelo da Governança Estratégica ainda não está pronto. Tente de novo em alguns minutos.");
}
