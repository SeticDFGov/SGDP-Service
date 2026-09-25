using System.Text.Json;
using api.Planejamento;
using Microsoft.EntityFrameworkCore;
using Models;
using Models.Planejamento;
using service.Interface;

namespace service.Planejamento;

/// <summary>
/// O modelo do documento da SGDI (E5), editado pelo administrador do módulo (e pelo admin
/// geral): capítulos e blocos, com histórico no pe_modelo_historico (entidades doc_capitulo e
/// doc_bloco). Regras:
/// <list type="bullet">
/// <item>os nove conteúdos do art. 12, § 2º (travados) continuam obrigatórios, não mudam de
/// passo e não são apagados; os blocos de dados que vieram com eles não mudam de seção nem de
/// tema e não são apagados;</item>
/// <item>capítulo do sistema (veio do carregador) muda de título, de numeração, de
/// obrigatoriedade e de passo, mas não é apagado; o criado pelo administrador é apagado de
/// forma lógica (com os subcapítulos), e a cópia dos órgãos fica guardada;</item>
/// <item>um nível de subcapítulo; chave única no modelo (gerada do título quando não vem);</item>
/// <item>o config do bloco passa pelo <see cref="PeDocConfig"/>; imagem do texto padrão é do
/// módulo (PNG ou JPEG), enviada por quem grava, e passa a ter o modelo como dono.</item>
/// </list>
/// Toda mudança vale na hora para os órgãos (decisão 14): o texto que o órgão não editou segue
/// o modelo; o editado ganha o aviso "o modelo mudou".
/// </summary>
public class PeDocModeloService : IPeDocModeloService
{
    private readonly AppDbContext _context;

    public PeDocModeloService(AppDbContext context)
    {
        _context = context;
    }

    // ── Leitura ─────────────────────────────────────────────────────────────

    public async Task<PeDocModeloResponse> ObterAsync(string? tipo)
    {
        var modelo = await ModeloAsync(tipo);
        return await RespostaAsync(modelo);
    }

    private async Task<PeDocModeloResponse> RespostaAsync(PeDocModelo modelo)
    {
        var capitulos = await _context.PeDocCapitulos.AsNoTracking()
            .Where(c => c.ModeloId == modelo.Id && c.ExcluidoEm == null)
            .ToListAsync();
        var ids = capitulos.Select(c => c.Id).ToList();
        var blocos = await _context.PeDocBlocos.AsNoTracking()
            .Where(b => ids.Contains(b.CapituloId) && b.ExcluidoEm == null)
            .OrderBy(b => b.Ordem).ThenBy(b => b.Id)
            .ToListAsync();
        var dicionario = await _context.PeCampos.AsNoTracking()
            .Where(c => c.Secao!.Chave == PeDominios.DicionarioNomes.Secao)
            .ToListAsync();

        // Onde cada marcador se preenche (F2): o passo das seções que dão valor aos marcadores e o
        // primeiro passo da deliberação do CGTIC, pelo modelo (sem órgão, sem o número)
        var secoes = PeDocMarcadores.SecoesDosMarcadores;
        var passosDasSecoes = (await (from s in _context.PeSecoes.AsNoTracking()
                                      join p in _context.PePassos.AsNoTracking() on s.PassoId equals (long?)p.Id
                                      where s.ExcluidoEm == null && p.ExcluidoEm == null && secoes.Contains(s.Chave)
                                      select new { Secao = s.Chave, Passo = p.Chave })
                .ToListAsync())
            .GroupBy(x => x.Secao)
            .ToDictionary(g => g.Key, g => g.First().Passo);
        var passosPorTipo = (await (from p in _context.PePassos.AsNoTracking()
                                    join e in _context.PeEtapas.AsNoTracking() on p.EtapaId equals e.Id
                                    where p.ExcluidoEm == null && p.Tipo == PeDominios.TipoPasso.Deliberacao
                                    orderby e.Ordem, p.Ordem, p.Id
                                    select new { p.Tipo, p.Chave })
                .ToListAsync())
            .GroupBy(x => x.Tipo)
            .ToDictionary(g => g.Key, g => g.First().Chave);

        return new PeDocModeloResponse
        {
            Id = modelo.Id,
            Tipo = modelo.Tipo,
            Nome = modelo.Nome,
            Capitulos = PeDocumentoService.Arvore(capitulos)
                .Select(x => Capitulo(x.Capitulo, blocos.Where(b => b.CapituloId == x.Capitulo.Id)))
                .ToList(),
            Marcadores = PeDocMarcadores.Respostas(PeDocMarcadores.Lista(dicionario, modelo.Tipo),
                secao => passosDasSecoes.TryGetValue(secao, out var passo) ? new PeDocMarcadores.PassoDoMarcador(passo, null) : null,
                tipo => passosPorTipo.TryGetValue(tipo, out var passo) ? new PeDocMarcadores.PassoDoMarcador(passo, null) : null)
        };
    }

    private static PeDocModeloCapituloResponse Capitulo(PeDocCapitulo c, IEnumerable<PeDocBloco> blocos) => new()
    {
        Id = c.Id,
        PaiId = c.PaiId,
        Chave = c.Chave,
        Titulo = c.Titulo,
        Numerado = c.Numerado,
        Ordem = c.Ordem,
        Obrigatorio = c.Obrigatorio || c.Travado,
        Travado = c.Travado,
        IncisoDecreto = c.IncisoDecreto,
        PassoChave = c.PassoChave,
        Sistema = c.Sistema,
        Blocos = blocos.Select(Bloco).ToList()
    };

    private static PeDocModeloBlocoResponse Bloco(PeDocBloco b) => new()
    {
        Id = b.Id,
        Ordem = b.Ordem,
        Tipo = b.Tipo,
        Config = PeModeloDados.Json(b.Config)
    };

    // ── Capítulos ───────────────────────────────────────────────────────────

    public async Task<PeDocModeloCapituloResponse> CriarCapituloAsync(PeDocCapituloCriarDTO dto, PeUserContext ctx)
    {
        var modelo = await ModeloAsync(dto.Tipo);
        var titulo = PeModeloService.Obrigatorio(dto.Titulo, 200, "o título do capítulo");

        long? paiId = null;
        if (dto.PaiId != null)
        {
            var pai = await _context.PeDocCapitulos.AsNoTracking()
                          .FirstOrDefaultAsync(c => c.Id == dto.PaiId && c.ModeloId == modelo.Id && c.ExcluidoEm == null)
                      ?? throw PeModeloService.Dados("O capítulo pai não existe no modelo.");
            if (pai.PaiId != null) throw PeModeloService.Dados("O subcapítulo não tem subcapítulos: escolha um capítulo como pai.");
            paiId = pai.Id;
        }

        var chaves = await _context.PeDocCapitulos.Where(c => c.ModeloId == modelo.Id).Select(c => c.Chave).ToListAsync();
        string chave;
        if (!string.IsNullOrWhiteSpace(dto.Chave))
        {
            // Como o código do nível: vale do jeito que vier, sem acento, em minúsculas e com
            // sublinhado ("Anexo técnico" e "anexo-tecnico" viram "anexo_tecnico")
            chave = PeChaves.Slug(dto.Chave, '_', PeChaves.MaximoSecao);
            if (chave.Length == 0 || !char.IsAsciiLetterLower(chave[0]) || !PeChaves.ChaveValida(chave, PeChaves.MaximoSecao))
                throw PeModeloService.Dados("A chave do capítulo precisa começar por letra.");
            if (chaves.Contains(chave))
                throw new ApiException(ErrorCode.PeChaveDuplicada, $"Já existe um capítulo com a chave \"{chave}\". Use outro título ou outra chave.");
        }
        else
        {
            var baseChave = PeChaves.Slug(titulo, '_', PeChaves.MaximoSecao);
            if (baseChave.Length == 0 || !char.IsLetter(baseChave[0])) baseChave = "capitulo";
            chave = PeChaves.Livre(baseChave, '_', PeChaves.MaximoSecao, chaves.Contains);
        }

        var passo = await PassoAsync(dto.PassoChave);
        var ordem = await _context.PeDocCapitulos
            .Where(c => c.ModeloId == modelo.Id && c.PaiId == paiId && c.ExcluidoEm == null)
            .MaxAsync(c => (int?)c.Ordem) ?? 0;

        var agora = DateTime.UtcNow;
        var capitulo = new PeDocCapitulo
        {
            ModeloId = modelo.Id,
            PaiId = paiId,
            Chave = chave,
            Titulo = titulo,
            Numerado = dto.Numerado ?? true,
            Obrigatorio = dto.Obrigatorio ?? false,
            Travado = false,
            PassoChave = passo,
            Ordem = ordem + 1,
            Sistema = false,
            CriadoEm = agora,
            CriadoPor = ctx.Email
        };
        _context.PeDocCapitulos.Add(capitulo);
        await SalvarCriacaoAsync(() => Registrar(PeDominios.EntidadeHistorico.DocCapitulo, capitulo.Id, PeDominios.AcaoHistorico.Criacao,
            null, Snap(capitulo), ctx.Email, agora));
        return Capitulo(capitulo, Array.Empty<PeDocBloco>());
    }

    public async Task<PeDocModeloCapituloResponse> AtualizarCapituloAsync(long id, PeDocCapituloAtualizarDTO dto, PeUserContext ctx)
    {
        var capitulo = await CapituloParaEscritaAsync(id);
        var antes = Snap(capitulo);
        const string travado = "Este capítulo é um dos nove conteúdos mínimos do PDTIC (art. 12, § 2º, do Decreto nº 48.900/2026)";

        if (dto.Informou(nameof(dto.Titulo))) capitulo.Titulo = PeModeloService.Obrigatorio(dto.Titulo, 200, "o título do capítulo");
        if (dto.Informou(nameof(dto.Numerado)) && dto.Numerado != null) capitulo.Numerado = dto.Numerado.Value;
        if (dto.Informou(nameof(dto.Obrigatorio)) && dto.Obrigatorio != null)
        {
            if (capitulo.Travado && !dto.Obrigatorio.Value)
                throw new ApiException(ErrorCode.PeItemTravado, $"{travado} e continua obrigatório.");
            capitulo.Obrigatorio = dto.Obrigatorio.Value;
        }
        if (dto.Informou(nameof(dto.PassoChave)))
        {
            var passo = await PassoAsync(dto.PassoChave);
            if (capitulo.Travado && passo != capitulo.PassoChave)
                throw new ApiException(ErrorCode.PeItemTravado, $"{travado}: o passo dele não muda.");
            capitulo.PassoChave = passo;
        }

        await SalvarAlteracaoAsync(capitulo, PeDominios.EntidadeHistorico.DocCapitulo, antes, Snap(capitulo), ctx.Email);
        return await CapituloRespostaAsync(capitulo.Id);
    }

    public async Task ExcluirCapituloAsync(long id, PeUserContext ctx)
    {
        var capitulo = await _context.PeDocCapitulos.FirstOrDefaultAsync(c => c.Id == id)
                       ?? throw CapituloNaoEncontrado();
        if (capitulo.ExcluidoEm != null) return;
        if (capitulo.Travado)
            throw new ApiException(ErrorCode.PeItemTravado,
                "Este capítulo é um dos nove conteúdos mínimos do PDTIC (art. 12, § 2º, do Decreto nº 48.900/2026) e não pode ser apagado.");
        if (capitulo.Sistema)
            throw new ApiException(ErrorCode.PeItemDoSistema,
                "Este capítulo veio do modelo da SGDI e não pode ser apagado. Você pode deixá-lo opcional, para o órgão esconder.");

        var filhos = await _context.PeDocCapitulos.Where(c => c.PaiId == id && c.ExcluidoEm == null).ToListAsync();
        if (filhos.Any(f => f.Sistema || f.Travado))
            throw new ApiException(ErrorCode.PeItemDoSistema, "Este capítulo tem subcapítulos do modelo da SGDI e não pode ser apagado.");

        var agora = DateTime.UtcNow;
        foreach (var item in filhos.Append(capitulo))
        {
            var antes = Snap(item);
            item.ExcluidoEm = agora;
            item.AlteradoEm = agora;
            item.AlteradoPor = ctx.Email;
            Registrar(PeDominios.EntidadeHistorico.DocCapitulo, item.Id, PeDominios.AcaoHistorico.Exclusao, antes, Snap(item), ctx.Email, agora);
        }
        await _context.SaveChangesAsync();
    }

    public async Task<PeDocModeloResponse> OrdenarCapitulosAsync(PeOrdemDTO dto, PeUserContext ctx)
    {
        var primeiro = dto.Ids is { Count: > 0 } ids
            ? ids[0]
            : throw new ApiException(ErrorCode.PeOrdemInvalida, "Mande os ids dos capítulos na nova ordem.");
        var referencia = await _context.PeDocCapitulos.AsNoTracking().FirstOrDefaultAsync(c => c.Id == primeiro && c.ExcluidoEm == null)
                         ?? throw new ApiException(ErrorCode.PeOrdemInvalida, "Um dos capítulos da lista não existe.");
        var grupo = await _context.PeDocCapitulos
            .Where(c => c.ModeloId == referencia.ModeloId && c.PaiId == referencia.PaiId && c.ExcluidoEm == null)
            .ToListAsync();
        var lista = ValidarOrdem(dto, grupo.Select(c => c.Id), "todos os capítulos do mesmo nível");
        Reordenar(grupo, lista, PeDominios.EntidadeHistorico.DocCapitulo, ctx.Email);
        await _context.SaveChangesAsync();
        return await RespostaAsync(await _context.PeDocModelos.AsNoTracking().FirstAsync(m => m.Id == referencia.ModeloId));
    }

    // ── Blocos ──────────────────────────────────────────────────────────────

    public async Task<PeDocModeloBlocoResponse> CriarBlocoAsync(PeDocBlocoCriarDTO dto, PeUserContext ctx)
    {
        if (dto.CapituloId == null) throw PeModeloService.Dados("Diga o capítulo do bloco.");
        var capitulo = await _context.PeDocCapitulos.AsNoTracking().FirstOrDefaultAsync(c => c.Id == dto.CapituloId && c.ExcluidoEm == null)
                       ?? throw CapituloNaoEncontrado();
        var tipo = dto.Tipo?.Trim() ?? string.Empty;
        if (!PeDominios.TipoBloco.Todos.Contains(tipo))
            throw new ApiException(ErrorCode.PeDocConfigInvalida, $"Tipo de bloco inválido. Use {string.Join(", ", PeDominios.TipoBloco.Todos)}.");

        var config = PeDocConfig.Normalizar(tipo, dto.Config, await ContextoConfigAsync());
        var imagens = await ImagensDoModeloAsync(config.Imagens, ctx);
        var ordem = await _context.PeDocBlocos.Where(b => b.CapituloId == capitulo.Id && b.ExcluidoEm == null).MaxAsync(b => (int?)b.Ordem) ?? 0;

        var agora = DateTime.UtcNow;
        var bloco = new PeDocBloco
        {
            CapituloId = capitulo.Id,
            Tipo = tipo,
            Config = config.Json,
            Ordem = ordem + 1,
            Sistema = false,
            CriadoEm = agora,
            CriadoPor = ctx.Email
        };
        _context.PeDocBlocos.Add(bloco);
        DarDono(imagens, capitulo.ModeloId, ctx, agora);
        await SalvarCriacaoAsync(() => Registrar(PeDominios.EntidadeHistorico.DocBloco, bloco.Id, PeDominios.AcaoHistorico.Criacao,
            null, Snap(bloco), ctx.Email, agora));
        return Bloco(bloco);
    }

    public async Task<PeDocModeloBlocoResponse> AtualizarBlocoAsync(long id, PeDocBlocoAtualizarDTO dto, PeUserContext ctx)
    {
        var (bloco, capitulo) = await BlocoParaEscritaAsync(id);
        var antes = Snap(bloco);
        var config = PeDocConfig.Normalizar(bloco.Tipo, dto.Config, await ContextoConfigAsync());

        // Bloco de dados dos nove conteúdos: a seção e o tema não mudam
        if (capitulo.Travado && bloco.Sistema && bloco.Tipo is PeDominios.TipoBloco.TabelaSecao or PeDominios.TipoBloco.ListaTema)
        {
            var atual = PeDocConfig.Ler(bloco.Config);
            var novo = PeDocConfig.Ler(config.Json);
            if (PeDocConfig.Secao(atual) != PeDocConfig.Secao(novo) || PeDocConfig.Tema(atual) != PeDocConfig.Tema(novo))
                throw new ApiException(ErrorCode.PeItemTravado,
                    "Este bloco mostra um dos nove conteúdos mínimos do PDTIC (art. 12, § 2º, do Decreto nº 48.900/2026): a seção e o tema não mudam.");
        }

        var imagens = await ImagensDoModeloAsync(config.Imagens, ctx);
        var agora = DateTime.UtcNow;
        bloco.Config = config.Json;
        DarDono(imagens, capitulo.ModeloId, ctx, agora);
        await SalvarAlteracaoAsync(bloco, PeDominios.EntidadeHistorico.DocBloco, antes, Snap(bloco), ctx.Email, agora);
        return Bloco(bloco);
    }

    public async Task ExcluirBlocoAsync(long id, PeUserContext ctx)
    {
        var bloco = await _context.PeDocBlocos.FirstOrDefaultAsync(b => b.Id == id) ?? throw BlocoNaoEncontrado();
        if (bloco.ExcluidoEm != null) return;
        var capitulo = await _context.PeDocCapitulos.AsNoTracking().FirstAsync(c => c.Id == bloco.CapituloId);
        if (capitulo.Travado && bloco.Sistema)
            throw new ApiException(ErrorCode.PeItemTravado,
                "Este bloco faz parte de um dos nove conteúdos mínimos do PDTIC (art. 12, § 2º, do Decreto nº 48.900/2026) e não pode ser apagado.");

        var agora = DateTime.UtcNow;
        var antes = Snap(bloco);
        bloco.ExcluidoEm = agora;
        bloco.AlteradoEm = agora;
        bloco.AlteradoPor = ctx.Email;
        Registrar(PeDominios.EntidadeHistorico.DocBloco, bloco.Id, PeDominios.AcaoHistorico.Exclusao, antes, Snap(bloco), ctx.Email, agora);
        await _context.SaveChangesAsync();
    }

    public async Task<PeDocModeloResponse> OrdenarBlocosAsync(PeOrdemDTO dto, PeUserContext ctx)
    {
        var primeiro = dto.Ids is { Count: > 0 } ids
            ? ids[0]
            : throw new ApiException(ErrorCode.PeOrdemInvalida, "Mande os ids dos blocos na nova ordem.");
        var referencia = await _context.PeDocBlocos.AsNoTracking().FirstOrDefaultAsync(b => b.Id == primeiro && b.ExcluidoEm == null)
                         ?? throw new ApiException(ErrorCode.PeOrdemInvalida, "Um dos blocos da lista não existe.");
        var grupo = await _context.PeDocBlocos.Where(b => b.CapituloId == referencia.CapituloId && b.ExcluidoEm == null).ToListAsync();
        var lista = ValidarOrdem(dto, grupo.Select(b => b.Id), "todos os blocos do capítulo");
        Reordenar(grupo, lista, PeDominios.EntidadeHistorico.DocBloco, ctx.Email);
        await _context.SaveChangesAsync();
        var modeloId = await _context.PeDocCapitulos.AsNoTracking().Where(c => c.Id == referencia.CapituloId).Select(c => c.ModeloId).FirstAsync();
        return await RespostaAsync(await _context.PeDocModelos.AsNoTracking().FirstAsync(m => m.Id == modeloId));
    }

    // ── Apoio ───────────────────────────────────────────────────────────────

    private async Task<PeDocModelo> ModeloAsync(string? tipo)
    {
        var chave = string.IsNullOrWhiteSpace(tipo) ? PeDominios.TipoDocumento.Pdtic : tipo.Trim();
        if (!PeDominios.TipoDocumento.Todos.Contains(chave))
            throw PeModeloService.Dados($"Tipo de documento inválido. Use {string.Join(", ", PeDominios.TipoDocumento.Todos)}.");
        return await PeDocumentoService.ModeloAtivoAsync(_context, chave) ?? throw PeDocumentoService.ModeloDoDocumentoIndisponivel();
    }

    /// <summary>A chave do passo (nula quando vem vazia); passo que não existe ou foi apagado: 400.</summary>
    private async Task<string?> PassoAsync(string? chave)
    {
        var texto = chave?.Trim();
        if (string.IsNullOrEmpty(texto)) return null;
        if (!await _context.PePassos.AnyAsync(p => p.Chave == texto && p.ExcluidoEm == null))
            throw PeModeloService.Dados($"O passo \"{texto}\" não existe (ou foi apagado).");
        return texto;
    }

    private async Task<PeDocCapitulo> CapituloParaEscritaAsync(long id)
    {
        var capitulo = await _context.PeDocCapitulos.FirstOrDefaultAsync(c => c.Id == id) ?? throw CapituloNaoEncontrado();
        if (capitulo.ExcluidoEm != null) throw new ApiException(ErrorCode.PeItemExcluido, "Este capítulo foi apagado.");
        return capitulo;
    }

    private async Task<(PeDocBloco Bloco, PeDocCapitulo Capitulo)> BlocoParaEscritaAsync(long id)
    {
        var bloco = await _context.PeDocBlocos.FirstOrDefaultAsync(b => b.Id == id) ?? throw BlocoNaoEncontrado();
        var capitulo = await _context.PeDocCapitulos.AsNoTracking().FirstAsync(c => c.Id == bloco.CapituloId);
        if (bloco.ExcluidoEm != null || capitulo.ExcluidoEm != null)
            throw new ApiException(ErrorCode.PeItemExcluido, "Este bloco (ou o capítulo dele) foi apagado.");
        return (bloco, capitulo);
    }

    private async Task<PeDocModeloCapituloResponse> CapituloRespostaAsync(long id)
    {
        var capitulo = await _context.PeDocCapitulos.AsNoTracking().FirstAsync(c => c.Id == id);
        var blocos = await _context.PeDocBlocos.AsNoTracking()
            .Where(b => b.CapituloId == id && b.ExcluidoEm == null)
            .OrderBy(b => b.Ordem).ThenBy(b => b.Id)
            .ToListAsync();
        return Capitulo(capitulo, blocos);
    }

    /// <summary>O que o config de um bloco precisa do modelo: as seções do PDTIC com os campos e os temas das ações.</summary>
    private async Task<PeDocConfig.Contexto> ContextoConfigAsync()
    {
        var secoes = await _context.PeSecoes.AsNoTracking()
            .Where(s => s.Escopo == PeDominios.Escopo.Pdtic && s.ExcluidoEm == null)
            .Select(s => new { s.Id, s.Chave })
            .ToListAsync();
        var ids = secoes.Select(s => s.Id).ToList();
        var campos = await _context.PeCampos.AsNoTracking()
            .Where(c => ids.Contains(c.SecaoId) && c.ExcluidoEm == null)
            .Select(c => new { c.Id, c.SecaoId, c.Chave, c.Tipo })
            .ToListAsync();
        var acoes = secoes.FirstOrDefault(s => s.Chave == PeDominios.TemaDecreto.SecaoAcoes);
        var tema = acoes == null ? null : campos.FirstOrDefault(c => c.SecaoId == acoes.Id && c.Chave == PeDominios.TemaDecreto.CampoTema);
        var temas = tema == null
            ? new List<string>()
            : await _context.PeOpcoes.AsNoTracking().Where(o => o.CampoId == tema.Id).Select(o => o.Valor).ToListAsync();

        return new PeDocConfig.Contexto
        {
            CamposDaSecao = chave => secoes.FirstOrDefault(s => s.Chave == chave) is { } secao
                ? campos.Where(c => c.SecaoId == secao.Id).Select(c => (c.Chave, c.Tipo)).ToList()
                : null,
            Temas = () => temas
        };
    }

    /// <summary>Imagens do texto padrão: do modelo (já aceitas) ou enviadas por quem grava e ainda sem dono.</summary>
    private async Task<List<PeArquivo>> ImagensDoModeloAsync(IReadOnlyList<long> ids, PeUserContext ctx)
    {
        var novas = new List<PeArquivo>();
        foreach (var id in ids)
        {
            var arquivo = await _context.PeArquivos.FirstOrDefaultAsync(a => a.Id == id)
                          ?? throw new ApiException(ErrorCode.PeDocConfigInvalida, "Uma das imagens não foi encontrada. Envie a imagem de novo.");
            if (PeArquivoService.TipoDoArquivo(arquivo.Nome) is not ("png" or "jpg"))
                throw new ApiException(ErrorCode.PeDocConfigInvalida, "No texto entram só imagens PNG ou JPEG.");
            if (arquivo.DonoTipo == PeDominios.DonoArquivo.DocModelo) continue;
            if (arquivo.DonoTipo != null || !string.Equals(arquivo.CriadoPor.Trim(), ctx.Email.Trim(), StringComparison.OrdinalIgnoreCase))
                throw new ApiException(ErrorCode.PeDocConfigInvalida, "Uma das imagens não pode ser usada aqui. Envie a imagem de novo.");
            if (!novas.Contains(arquivo)) novas.Add(arquivo);
        }
        return novas;
    }

    private static void DarDono(IEnumerable<PeArquivo> arquivos, long modeloId, PeUserContext ctx, DateTime agora)
    {
        foreach (var arquivo in arquivos)
        {
            arquivo.DonoTipo = PeDominios.DonoArquivo.DocModelo;
            arquivo.DonoId = modeloId;
            arquivo.AlteradoEm = agora;
            arquivo.AlteradoPor = ctx.Email;
        }
    }

    private static List<long> ValidarOrdem(PeOrdemDTO dto, IEnumerable<long> grupo, string oQue)
    {
        var ids = dto.Ids ?? new List<long>();
        var esperados = grupo.ToHashSet();
        if (ids.Count == 0 || ids.Distinct().Count() != ids.Count || !esperados.SetEquals(ids))
            throw new ApiException(ErrorCode.PeOrdemInvalida, $"Mande {oQue}, cada um uma vez, na nova ordem.");
        return ids;
    }

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

    /// <summary>Grava o item (para ter o id) e o histórico dele na mesma transação (no PostgreSQL).</summary>
    private async Task SalvarCriacaoAsync(Action registrarHistorico)
    {
        await using var transacao = _context.Database.IsRelational() ? await _context.Database.BeginTransactionAsync() : null;
        await _context.SaveChangesAsync();
        registrarHistorico();
        await _context.SaveChangesAsync();
        if (transacao != null) await transacao.CommitAsync();
    }

    /// <summary>Grava a alteração com o histórico, só se algo mudou de fato.</summary>
    private async Task SalvarAlteracaoAsync(IPeAuditavel item, string entidade, object antes, object depois, string autor, DateTime? quando = null)
    {
        var jsonAntes = JsonSerializer.Serialize(antes, PeModeloService.JsonHistorico);
        var jsonDepois = JsonSerializer.Serialize(depois, PeModeloService.JsonHistorico);
        if (jsonAntes != jsonDepois)
        {
            var agora = quando ?? DateTime.UtcNow;
            item.AlteradoEm = agora;
            item.AlteradoPor = autor;
            _context.PeModeloHistorico.Add(new PeModeloHistorico
            {
                Entidade = entidade,
                EntidadeId = ((IPeOrdenavel)item).Id,
                Acao = PeDominios.AcaoHistorico.Alteracao,
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
            Antes = antes == null ? null : JsonSerializer.Serialize(antes, PeModeloService.JsonHistorico),
            Depois = depois == null ? null : JsonSerializer.Serialize(depois, PeModeloService.JsonHistorico),
            AlteradoEm = quando,
            AlteradoPor = autor
        });

    private static object Snap(PeDocCapitulo c) => new
    {
        c.ModeloId, c.PaiId, c.Chave, c.Titulo, c.Numerado, c.Obrigatorio, c.Travado, c.IncisoDecreto, c.PassoChave,
        Excluido = c.ExcluidoEm != null
    };

    private static object Snap(PeDocBloco b) => new
    {
        b.CapituloId, b.Tipo, Config = PeModeloDados.Json(b.Config), Excluido = b.ExcluidoEm != null
    };

    private static ApiException CapituloNaoEncontrado() =>
        new(ErrorCode.PeDocCapituloNaoEncontrado, "Capítulo do modelo não encontrado. Atualize a tela.");

    private static ApiException BlocoNaoEncontrado() =>
        new(ErrorCode.PeDocBlocoNaoEncontrado, "Bloco do modelo não encontrado. Atualize a tela.");
}
