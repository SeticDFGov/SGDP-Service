using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using api.Planejamento;
using Microsoft.EntityFrameworkCore;
using Models;
using Models.Planejamento;
using service.Interface;

namespace service.Planejamento;

/// <summary>
/// Os fluxos do guia como modelo e a cópia do órgão (E6):
/// <list type="bullet">
/// <item>Modelo: os fluxos do guia (figuras 4 a 22), semeados pelo carregador; o administrador
/// do módulo (e o admin geral) muda o nome e a definição, com histórico; todo papel lê.</item>
/// <item>Cópia do órgão: só existe quando o órgão muda alguma coisa (decisão 14: sem cópia, vale
/// o modelo na hora); gravar igual ao modelo apaga a cópia; "restaurar" volta ao modelo. Quem
/// grava: a equipe do órgão (e o admin geral), com o PDTIC em elaboração ou devolvido. Se o
/// modelo mudar depois que o órgão gravou, ModeloMudou avisa.</item>
/// <item>Desenho: <see cref="PeFluxoDesenho"/>, com os nomes do dicionário do órgão (ou os
/// padrão, no modelo).</item>
/// <item>Cronograma sugerido: as tarefas dos fluxos de preparação, diagnóstico e planejamento
/// viram as linhas do cronograma do plano de trabalho (atividade, responsável pela raia e
/// predecessora), só no cronograma vazio.</item>
/// </list>
/// Nada aqui está no caminho de cada requisição: só as actions dos fluxos e do documento leem
/// as tabelas pe_fluxo_modelo e pe_fluxo.
/// </summary>
public class PeFluxoService : IPeFluxoService
{
    public const string TipoSvg = "image/svg+xml";

    private readonly AppDbContext _context;
    private readonly IPeRegistroService _registros;
    private readonly IPePermissionService _permissoes;

    public PeFluxoService(AppDbContext context, IPeRegistroService registros, IPePermissionService permissoes)
    {
        _context = context;
        _registros = registros;
        _permissoes = permissoes;
    }

    // ── Modelos ─────────────────────────────────────────────────────────────

    public async Task<List<PeFluxoModeloResponse>> ModelosAsync(PeUserContext ctx)
    {
        if (!_permissoes.PodeLerModelo(ctx)) throw SemPapel();
        var modelos = await _context.PeFluxosModelo.AsNoTracking().OrderBy(m => m.Ordem).ThenBy(m => m.Id).ToListAsync();
        if (modelos.Count == 0 && await AindaNaoCarregadosAsync(_context)) throw Indisponivel();
        return modelos.Select(Modelo).ToList();
    }

    public async Task<PeFluxoModeloResponse> SalvarModeloAsync(string chave, PeFluxoSalvarDTO dto, PeUserContext ctx)
    {
        if (!_permissoes.PodeConfigurarModelo(ctx))
            throw new ApiException(ErrorCode.PeSemPermissao, "Só o administrador do módulo altera os fluxos do guia.");
        var modelo = await ModeloAsync(_context, chave, rastrear: true);
        var definicao = PeFluxoDefinicaoLeitor.LerValida(dto.Definicao);
        var nome = NomeInformado(dto.Nome, modelo.Nome, modelo.Nome);
        var json = PeFluxoDefinicaoLeitor.ParaJson(definicao);
        if (nome == modelo.Nome && Canonico(json) == Canonico(modelo.Definicao)) return Modelo(modelo);

        var agora = DateTime.UtcNow;
        var antes = Retrato(modelo.Nome, modelo.Definicao);
        modelo.Nome = nome;
        modelo.Definicao = json;
        modelo.AlteradoEm = agora;
        modelo.AlteradoPor = ctx.Email;
        _context.PeModeloHistorico.Add(new PeModeloHistorico
        {
            Entidade = PeDominios.EntidadeHistorico.FluxoModelo,
            EntidadeId = modelo.Id,
            Acao = PeDominios.AcaoHistorico.Alteracao,
            Antes = antes,
            Depois = Retrato(modelo.Nome, modelo.Definicao),
            AlteradoEm = agora,
            AlteradoPor = ctx.Email
        });
        await _context.SaveChangesAsync();
        return Modelo(modelo);
    }

    public async Task<string> SvgDoModeloAsync(string chave, PeUserContext ctx)
    {
        if (!_permissoes.PodeLerModelo(ctx)) throw SemPapel();
        var modelo = await ModeloAsync(_context, chave);
        var campos = await CamposDoDicionarioAsync(_context);
        return PeFluxoDesenho.Desenhar(PeFluxoDefinicaoLeitor.DoBanco(modelo.Definicao), modelo.Nome, PeFluxoNomes.Mapa(null, campos)).Svg;
    }

    // ── Cópia do órgão ──────────────────────────────────────────────────────

    public async Task<List<PeFluxoResumoResponse>> DoPdticAsync(long pdticId, PeUserContext ctx)
    {
        var pdtic = await PePdticService.LerAsync(_context, _permissoes, pdticId, ctx);
        var modelos = await _context.PeFluxosModelo.AsNoTracking().OrderBy(m => m.Ordem).ThenBy(m => m.Id).ToListAsync();
        if (modelos.Count == 0 && await AindaNaoCarregadosAsync(_context)) throw Indisponivel();
        var copias = await _context.PeFluxos.AsNoTracking().Where(f => f.PdticId == pdtic.Id).ToDictionaryAsync(f => f.ModeloId);
        return modelos.Select(m =>
        {
            copias.TryGetValue(m.Id, out var copia);
            return new PeFluxoResumoResponse
            {
                Chave = m.Chave,
                Nome = copia?.Nome ?? m.Nome,
                FiguraGuia = m.FiguraGuia,
                Personalizado = copia != null,
                ModeloMudou = copia != null && copia.ModeloHash != HashDoModelo(m),
                AlteradoEm = copia?.AlteradoEm ?? copia?.CriadoEm,
                AlteradoPor = copia?.AlteradoPor ?? copia?.CriadoPor,
                Ordem = m.Ordem
            };
        }).ToList();
    }

    public async Task<PeFluxoResponse> ObterAsync(long pdticId, string chave, PeUserContext ctx)
    {
        var pdtic = await PePdticService.LerAsync(_context, _permissoes, pdticId, ctx);
        var modelo = await ModeloAsync(_context, chave);
        var copia = await _context.PeFluxos.AsNoTracking().FirstOrDefaultAsync(f => f.PdticId == pdtic.Id && f.ModeloId == modelo.Id);
        return Resposta(pdtic, modelo, copia, ctx);
    }

    public async Task<PeFluxoResponse> SalvarAsync(long pdticId, string chave, PeFluxoSalvarDTO dto, PeUserContext ctx)
    {
        var pdtic = await PdticParaEditarAsync(pdticId, ctx);
        var modelo = await ModeloAsync(_context, chave);
        var definicao = PeFluxoDefinicaoLeitor.LerValida(dto.Definicao);
        var copia = await _context.PeFluxos.FirstOrDefaultAsync(f => f.PdticId == pdtic.Id && f.ModeloId == modelo.Id);
        var nome = NomeInformado(dto.Nome, copia?.Nome ?? modelo.Nome, modelo.Nome);
        var json = PeFluxoDefinicaoLeitor.ParaJson(definicao);
        var agora = DateTime.UtcNow;

        if (nome == modelo.Nome && Canonico(json) == Canonico(modelo.Definicao))
        {
            // Igual ao modelo: não é adaptação; o fluxo volta a seguir o modelo
            if (copia != null) _context.PeFluxos.Remove(copia);
        }
        else
        {
            if (copia == null)
            {
                copia = new PeFluxo { PdticId = pdtic.Id, ModeloId = modelo.Id, CriadoEm = agora, CriadoPor = ctx.Email };
                _context.PeFluxos.Add(copia);
            }
            else
            {
                copia.AlteradoEm = agora;
                copia.AlteradoPor = ctx.Email;
            }
            copia.Nome = nome;
            copia.Definicao = json;
            copia.ModeloHash = HashDoModelo(modelo);
        }
        Tocar(pdtic, ctx, agora);
        await _context.SaveChangesAsync();
        return await ObterDepoisDeGravarAsync(pdtic.Id, modelo, ctx);
    }

    public async Task<PeFluxoResponse> RestaurarAsync(long pdticId, string chave, PeUserContext ctx)
    {
        var pdtic = await PdticParaEditarAsync(pdticId, ctx);
        var modelo = await ModeloAsync(_context, chave);
        var copia = await _context.PeFluxos.FirstOrDefaultAsync(f => f.PdticId == pdtic.Id && f.ModeloId == modelo.Id);
        if (copia != null)
        {
            _context.PeFluxos.Remove(copia);
            Tocar(pdtic, ctx, DateTime.UtcNow);
            await _context.SaveChangesAsync();
        }
        return await ObterDepoisDeGravarAsync(pdtic.Id, modelo, ctx);
    }

    public async Task<string> SvgAsync(long pdticId, string chave, PeUserContext ctx)
    {
        var pdtic = await PePdticService.LerAsync(_context, _permissoes, pdticId, ctx);
        var modelo = await ModeloAsync(_context, chave);
        var copia = await _context.PeFluxos.AsNoTracking().FirstOrDefaultAsync(f => f.PdticId == pdtic.Id && f.ModeloId == modelo.Id);
        var (mapa, _) = await NomesDoOrgaoAsync(pdtic);
        var definicao = PeFluxoDefinicaoLeitor.DoBanco(copia?.Definicao ?? modelo.Definicao);
        return PeFluxoDesenho.Desenhar(definicao, copia?.Nome ?? modelo.Nome, mapa).Svg;
    }

    // ── Prévia do editor ────────────────────────────────────────────────────

    public async Task<string> DesenhoAsync(PeFluxoDesenhoDTO dto, PeUserContext ctx)
    {
        if (!_permissoes.PodeLerModelo(ctx)) throw SemPapel();
        var definicao = PeFluxoDefinicaoLeitor.LerValida(dto.Definicao);
        var mapa = await MapaAsync(dto.PdticId, ctx);
        return PeFluxoDesenho.Desenhar(definicao, dto.Nome, mapa).Svg;
    }

    public Task<PeFluxoValidacaoResponse> ValidarAsync(PeFluxoDesenhoDTO dto, PeUserContext ctx)
    {
        if (!_permissoes.PodeLerModelo(ctx)) throw SemPapel();
        var resultado = PeFluxoDefinicaoLeitor.Ler(dto.Definicao);
        return Task.FromResult(new PeFluxoValidacaoResponse
        {
            Valida = resultado.Valida,
            Erros = resultado.Erros,
            Definicao = resultado.Valida ? resultado.Definicao : null
        });
    }

    public async Task<List<PeFluxoNomeResponse>> NomesAsync(long? pdticId, PeUserContext ctx)
    {
        if (!_permissoes.PodeLerModelo(ctx)) throw SemPapel();
        if (pdticId is long id)
        {
            var pdtic = await PePdticService.LerAsync(_context, _permissoes, id, ctx);
            var (mapa, campos) = await NomesDoOrgaoAsync(pdtic);
            return PeFluxoNomes.Resposta(mapa, campos);
        }
        var doModelo = await CamposDoDicionarioAsync(_context);
        return PeFluxoNomes.Resposta(PeFluxoNomes.Mapa(null, doModelo), doModelo);
    }

    // ── Cronograma sugerido ─────────────────────────────────────────────────

    public async Task<List<PeRegistroResponse>> SugerirCronogramaAsync(long pdticId, PeUserContext ctx)
    {
        var pdtic = await PePdticService.LerAsync(_context, _permissoes, pdticId, ctx);
        if (!_permissoes.PodeEditarPdtic(ctx, pdtic.OrgaoId))
            throw new ApiException(ErrorCode.PeSemPermissao, "Só a equipe do órgão monta o cronograma do plano de trabalho.");
        if (!PeDominios.SituacaoPdtic.Editaveis.Contains(pdtic.Situacao)) throw PePdticService.Fechado(pdtic);

        var (mapa, _) = await NomesDoOrgaoAsync(pdtic);
        var modelos = await _context.PeFluxosModelo.AsNoTracking()
            .Where(m => PeDominios.FluxoGuia.DoCronograma.Contains(m.Chave))
            .ToListAsync();
        if (modelos.Count == 0 && await AindaNaoCarregadosAsync(_context)) throw Indisponivel();
        var ids = modelos.Select(m => m.Id).ToList();
        var copias = await _context.PeFluxos.AsNoTracking().Where(f => f.PdticId == pdtic.Id && ids.Contains(f.ModeloId)).ToDictionaryAsync(f => f.ModeloId);

        var linhas = new List<PeRegistroSugerido>();
        var ultimasDoAnterior = new List<int>();
        foreach (var chave in PeDominios.FluxoGuia.DoCronograma)
        {
            var modelo = modelos.FirstOrDefault(m => m.Chave == chave);
            if (modelo == null) continue;
            copias.TryGetValue(modelo.Id, out var copia);
            var definicao = PeFluxoDefinicaoLeitor.DoBanco(copia?.Definicao ?? modelo.Definicao);
            var ultimas = LinhasDoCronograma(definicao, mapa, linhas, ultimasDoAnterior);
            if (ultimas.Count > 0) ultimasDoAnterior = ultimas;
        }
        if (linhas.Count == 0)
            throw new ApiException(ErrorCode.PeCronogramaSemTarefas,
                "Os fluxos da elaboração (preparação, diagnóstico e planejamento) não têm tarefas para sugerir.");
        return await _registros.CriarSugeridosAsync(PeDono.DoPdtic(pdtic.Id), PeDominios.FluxoGuia.SecaoCronograma, linhas, ctx);
    }

    /// <summary>
    /// Acrescenta as tarefas de um fluxo (na ordem de leitura) às linhas do cronograma: a
    /// atividade ("1.3 Descrever a metodologia"), o responsável (o nome da raia) e as
    /// predecessoras (as tarefas imediatamente antes, atravessando decisões, paralelos e
    /// eventos; a primeira tarefa do fluxo depende das últimas do fluxo anterior). Devolve as
    /// posições das últimas tarefas deste fluxo (as que levam ao fim).
    /// </summary>
    public static List<int> LinhasDoCronograma(PeFluxoDefinicao definicao, IReadOnlyDictionary<string, string?> mapa,
        List<PeRegistroSugerido> linhas, IReadOnlyList<int> ultimasDoAnterior)
    {
        var analise = PeFluxoAnalise.Analisar(definicao);
        var tarefas = analise.OrdemDeLeitura.Where(e => PeDominios.TipoElementoFluxo.EhAtividade(e.Tipo)).ToList();
        var posicao = new Dictionary<string, int>();
        foreach (var t in tarefas)
        {
            var raia = analise.Raias.Count > 0 ? analise.Raias[analise.RaiaDe(t)].Nome : string.Empty;
            var nome = PeFluxoDefinicaoLeitor.Limpar(PeFluxoDesenho.ResolverNome(t.Nome, mapa));
            var atividade = t.Numero != null ? $"{t.Numero} {nome}" : nome;

            // As tarefas anteriores: volta pelas ligações de avanço, atravessando o que não é tarefa
            var anteriores = new List<int>();
            var comecoDoFluxo = false;
            var vistos = new HashSet<string> { t.Id };
            var fila = new Queue<string>(analise.EntradasDeAvanco(t.Id).Select(l => l.De));
            while (fila.Count > 0)
            {
                var id = fila.Dequeue();
                if (!vistos.Add(id)) continue;
                var e = analise.PorId[id];
                if (PeDominios.TipoElementoFluxo.EhAtividade(e.Tipo))
                {
                    if (posicao.TryGetValue(id, out var p)) anteriores.Add(p);
                    continue;
                }
                var entradas = analise.EntradasDeAvanco(id).ToList();
                if (entradas.Count == 0) comecoDoFluxo = true;
                foreach (var l in entradas) fila.Enqueue(l.De);
            }
            if (comecoDoFluxo || anteriores.Count == 0) anteriores.AddRange(ultimasDoAnterior);

            var linha = new PeRegistroSugerido();
            linha.Textos[PeDominios.FluxoGuia.CampoAtividade] = atividade;
            if (raia.Length > 0) linha.Textos[PeDominios.FluxoGuia.CampoResponsavel] = PeFluxoDefinicaoLeitor.Limpar(PeFluxoDesenho.ResolverNome(raia, mapa));
            var ligadas = anteriores.Distinct().OrderBy(i => i).ToList();
            if (ligadas.Count > 0) linha.Ligacoes[PeDominios.FluxoGuia.CampoPredecessoras] = ligadas;
            posicao[t.Id] = linhas.Count;
            linhas.Add(linha);
        }

        // As últimas: as tarefas de onde se chega ao fim sem passar por outra tarefa
        var ultimas = new List<int>();
        foreach (var t in tarefas)
        {
            var vistos = new HashSet<string> { t.Id };
            var fila = new Queue<string>(analise.SaidasDeAvanco(t.Id).Select(l => l.Para));
            var chega = false;
            while (fila.Count > 0 && !chega)
            {
                var id = fila.Dequeue();
                if (!vistos.Add(id)) continue;
                var e = analise.PorId[id];
                if (PeDominios.TipoElementoFluxo.EhAtividade(e.Tipo)) continue;
                var saidas = analise.SaidasDeAvanco(id).ToList();
                if (e.Tipo == PeDominios.TipoElementoFluxo.Fim || saidas.Count == 0) chega = true;
                foreach (var l in saidas) fila.Enqueue(l.Para);
            }
            if (chega) ultimas.Add(posicao[t.Id]);
        }
        return ultimas;
    }

    // ── Documento (E5): o desenho do bloco de fluxo ─────────────────────────

    /// <summary>O desenho de um fluxo para o documento: o nome com a figura, o SVG, o tamanho e a descrição.</summary>
    public sealed record ParaDocumento(string Nome, string Svg, double Largura, double Altura, bool Personalizado, List<string> Descricao);

    /// <summary>
    /// Os desenhos dos fluxos que o documento de um PDTIC mostra (a cópia do órgão ou o modelo),
    /// com os nomes do dicionário (os marcadores do documento; sem valor, o nome padrão).
    /// </summary>
    public static async Task<Dictionary<string, ParaDocumento>> ParaDocumentoAsync(AppDbContext context, long pdticId,
        IReadOnlyCollection<string> chaves, IReadOnlyDictionary<string, string?> marcadores, IEnumerable<PeCampo> camposDoDicionario)
    {
        var saida = new Dictionary<string, ParaDocumento>();
        if (chaves.Count == 0) return saida;
        var modelos = await context.PeFluxosModelo.AsNoTracking().Where(m => chaves.Contains(m.Chave)).ToListAsync();
        if (modelos.Count == 0) return saida;
        var ids = modelos.Select(m => m.Id).ToList();
        var copias = await context.PeFluxos.AsNoTracking().Where(f => f.PdticId == pdticId && ids.Contains(f.ModeloId)).ToDictionaryAsync(f => f.ModeloId);
        var mapa = PeFluxoNomes.Mapa(marcadores, camposDoDicionario);
        foreach (var modelo in modelos)
        {
            copias.TryGetValue(modelo.Id, out var copia);
            var nome = copia?.Nome ?? modelo.Nome;
            var desenho = PeFluxoDesenho.Desenhar(PeFluxoDefinicaoLeitor.DoBanco(copia?.Definicao ?? modelo.Definicao), nome, mapa);
            var legenda = PeFluxoDesenho.ResolverNome(nome, mapa);
            if (!string.IsNullOrWhiteSpace(modelo.FiguraGuia))
                legenda += copia != null
                    ? $" (adaptado da {modelo.FiguraGuia.Trim().ToLower(CultureInfo.GetCultureInfo("pt-BR"))} do guia)"
                    : $" ({modelo.FiguraGuia.Trim().ToLower(CultureInfo.GetCultureInfo("pt-BR"))} do guia)";
            saida[modelo.Chave] = new ParaDocumento(legenda, desenho.Svg, desenho.Largura, desenho.Altura, copia != null, desenho.Descricao);
        }
        return saida;
    }

    /// <summary>Os campos de texto do dicionário de nomes (os que o administrador criou entram como nome).</summary>
    public static List<PeCampo> CamposDoDicionario(PeModeloDados dados)
    {
        var secao = dados.SecaoPorChave(PeDominios.DicionarioNomes.Secao);
        return secao == null ? new List<PeCampo>() : dados.CamposDaSecao(secao.Id, incluirExcluidos: false).ToList();
    }

    // ── Apoio ───────────────────────────────────────────────────────────────

    private async Task<PeFluxoResponse> ObterDepoisDeGravarAsync(long pdticId, PeFluxoModelo modelo, PeUserContext ctx)
    {
        var pdtic = await _context.PePdtics.AsNoTracking().FirstAsync(p => p.Id == pdticId);
        var copia = await _context.PeFluxos.AsNoTracking().FirstOrDefaultAsync(f => f.PdticId == pdticId && f.ModeloId == modelo.Id);
        return Resposta(pdtic, modelo, copia, ctx);
    }

    private PeFluxoResponse Resposta(PePdtic pdtic, PeFluxoModelo modelo, PeFluxo? copia, PeUserContext ctx) => new()
    {
        Chave = modelo.Chave,
        Nome = copia?.Nome ?? modelo.Nome,
        FiguraGuia = modelo.FiguraGuia,
        Definicao = PeFluxoDefinicaoLeitor.DoBanco(copia?.Definicao ?? modelo.Definicao),
        Personalizado = copia != null,
        ModeloMudou = copia != null && copia.ModeloHash != HashDoModelo(modelo),
        PodeEditar = _permissoes.PodeEditarPdtic(ctx, pdtic.OrgaoId) && PeDominios.SituacaoPdtic.Editaveis.Contains(pdtic.Situacao),
        AlteradoEm = copia?.AlteradoEm ?? copia?.CriadoEm,
        AlteradoPor = copia?.AlteradoPor ?? copia?.CriadoPor
    };

    private static PeFluxoModeloResponse Modelo(PeFluxoModelo m) => new()
    {
        Chave = m.Chave,
        Nome = m.Nome,
        FiguraGuia = m.FiguraGuia,
        Ordem = m.Ordem,
        Definicao = PeFluxoDefinicaoLeitor.DoBanco(m.Definicao),
        AlteradoEm = m.AlteradoEm,
        AlteradoPor = m.AlteradoPor
    };

    private async Task<PePdtic> PdticParaEditarAsync(long pdticId, PeUserContext ctx)
    {
        var pdtic = await PePdticService.LerAsync(_context, _permissoes, pdticId, ctx, rastrear: true);
        if (!_permissoes.PodeEditarPdtic(ctx, pdtic.OrgaoId))
            throw new ApiException(ErrorCode.PeSemPermissao, "Só a equipe do órgão adapta os fluxos do PDTIC.");
        if (!PeDominios.SituacaoPdtic.Editaveis.Contains(pdtic.Situacao)) throw PePdticService.Fechado(pdtic);
        return pdtic;
    }

    /// <summary>Gravar um fluxo toca o PDTIC (alterado em e por), com a situação como token de concorrência.</summary>
    private static void Tocar(PePdtic pdtic, PeUserContext ctx, DateTime agora)
    {
        pdtic.AlteradoEm = agora;
        pdtic.AlteradoPor = ctx.Email;
    }

    /// <summary>O modelo pela chave; sem ele, 404 (ou 409, antes de o carregador trazer os fluxos).</summary>
    internal static async Task<PeFluxoModelo> ModeloAsync(AppDbContext context, string chave, bool rastrear = false)
    {
        var texto = (chave ?? string.Empty).Trim();
        var consulta = rastrear ? context.PeFluxosModelo : context.PeFluxosModelo.AsNoTracking();
        var modelo = await consulta.FirstOrDefaultAsync(m => m.Chave == texto);
        if (modelo != null) return modelo;
        if (await AindaNaoCarregadosAsync(context)) throw Indisponivel();
        throw new ApiException(ErrorCode.PeFluxoNaoEncontrado, "Fluxo não encontrado. Atualize a tela.");
    }

    /// <summary>A versão do modelo inicial gravada ainda é anterior à que traz os fluxos do guia.</summary>
    private static async Task<bool> AindaNaoCarregadosAsync(AppDbContext context)
    {
        var valor = await context.PeConfiguracoes.AsNoTracking()
            .Where(c => c.Chave == PeConfiguracao.ChaveVersaoModelo)
            .Select(c => c.Valor)
            .FirstOrDefaultAsync();
        return !int.TryParse(valor, NumberStyles.None, CultureInfo.InvariantCulture, out var versao) || versao < PeCarregadorModelo.VersaoDosFluxos;
    }

    private static ApiException Indisponivel() => new(ErrorCode.PeModeloIndisponivel,
        "Os fluxos do guia ainda não foram carregados. Tente de novo em alguns minutos.");

    private static ApiException SemPapel() => new(ErrorCode.PeSemPermissao,
        "Você ainda não tem papel na Governança Estratégica. Fale com o administrador do módulo.");

    private static string NomeInformado(string? informado, string atual, string doModelo)
    {
        if (informado == null) return atual;
        var nome = PeFluxoDefinicaoLeitor.Limpar(informado);
        if (nome.Length == 0) return doModelo;
        if (nome.Length > 200) throw new PeFluxoInvalidoException(new[] { "O nome do fluxo passa de 200 caracteres." });
        return nome;
    }

    private async Task<Dictionary<string, string?>> MapaAsync(long? pdticId, PeUserContext ctx)
    {
        if (pdticId is long id)
        {
            var pdtic = await PePdticService.LerAsync(_context, _permissoes, id, ctx);
            return (await NomesDoOrgaoAsync(pdtic)).Mapa;
        }
        return PeFluxoNomes.Mapa(null, await CamposDoDicionarioAsync(_context));
    }

    /// <summary>Os nomes do dicionário do órgão (os marcadores do documento, com os nomes padrão onde falta).</summary>
    private async Task<(Dictionary<string, string?> Mapa, List<PeCampo> Campos)> NomesDoOrgaoAsync(PePdtic pdtic)
    {
        var trilha = await PeTrilhaOrgao.CarregarAsync(_context, pdtic.OrgaoId, soAtivo: false);
        var (dicionario, _) = await PeDocumentoService.DicionarioAsync(_registros, pdtic, trilha);
        var marcadores = PeDocumentoService.Marcadores(pdtic, trilha.Orgao, dicionario);
        var campos = CamposDoDicionario(trilha.Dados);
        return (PeFluxoNomes.Mapa(marcadores, campos), campos);
    }

    private static async Task<List<PeCampo>> CamposDoDicionarioAsync(AppDbContext context)
    {
        var secao = await context.PeSecoes.AsNoTracking().FirstOrDefaultAsync(s => s.Chave == PeDominios.DicionarioNomes.Secao);
        return secao == null
            ? new List<PeCampo>()
            : await context.PeCampos.AsNoTracking().Where(c => c.SecaoId == secao.Id && c.ExcluidoEm == null).ToListAsync();
    }

    /// <summary>A definição no JSON canônico, normalizada e numerada (o jsonb reordena as chaves).</summary>
    public static string Canonico(string? definicao) =>
        PeDocMarcadores.Canonico(JsonNode.Parse(PeFluxoDefinicaoLeitor.ParaJson(PeFluxoDefinicaoLeitor.DoBanco(definicao))));

    /// <summary>SHA-256 do modelo (nome e definição canônica): o "modelo mudou" da cópia do órgão.</summary>
    public static string HashDoModelo(PeFluxoModelo modelo) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(modelo.Nome.Trim() + "\n" + Canonico(modelo.Definicao)))).ToLowerInvariant();

    private static string Retrato(string nome, string definicao) =>
        JsonSerializer.Serialize(new { Nome = nome, Definicao = JsonNode.Parse(definicao) }, PeModeloService.JsonHistorico);
}
