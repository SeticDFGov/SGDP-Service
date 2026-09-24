using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Models;
using Models.Planejamento;

namespace service.Planejamento;

// ── Formato do modelo inicial (Models/Planejamento/Seed/modelo-inicial.json) ──

/// <summary>
/// O modelo inicial em JSON: níveis, configurações gerais e as etapas com passos,
/// seções, campos e opções. A ordem de cada lista é a ordem do item. "niveis" de passo e
/// de seção é um texto (a mesma situação em todos os níveis) ou um objeto com a situação
/// de cada nível pelo código; no campo, sem "niveis", vale a situação da seção. As seções
/// fora do PDTIC (escopos df e petic, desde a versão 2) não têm passo nem nível: usam
/// "situacao" (a geral; no campo, sem ela, vale a da seção) e podem trazer registros do
/// sistema (só no catálogo do DF: os princípios do art. 4º).
/// </summary>
public sealed class PeSeedModelo
{
    public int Versao { get; set; }

    public List<PeSeedNivel> Niveis { get; set; } = new();

    public Dictionary<string, JsonElement> Configuracoes { get; set; } = new();

    public List<PeSeedEtapa> Etapas { get; set; } = new();

    public List<PeSeedSecao> SecoesForaDoPdtic { get; set; } = new();
}

public sealed class PeSeedNivel
{
    public string Codigo { get; set; } = string.Empty;
    public string Nome { get; set; } = string.Empty;
    public string? Descricao { get; set; }
}

public sealed class PeSeedEtapa
{
    public string Chave { get; set; } = string.Empty;
    public string Titulo { get; set; } = string.Empty;
    public string? Descricao { get; set; }
    public string? ReferenciaGuia { get; set; }
    public List<PeSeedPasso> Passos { get; set; } = new();
}

public sealed class PeSeedPasso
{
    public string Chave { get; set; } = string.Empty;
    public string Titulo { get; set; } = string.Empty;
    public string OQueFazer { get; set; } = string.Empty;
    public string? BaseLegal { get; set; }
    public string? ReferenciaGuia { get; set; }
    public string Tipo { get; set; } = PeDominios.TipoPasso.Dados;
    public string? Inciso { get; set; }
    public bool Travado { get; set; }
    public bool AceitaNaoSeAplica { get; set; }
    public JsonElement Niveis { get; set; }
    public List<PeSeedSecao> Secoes { get; set; } = new();
}

public sealed class PeSeedSecao
{
    // Só nas seções fora do PDTIC: df ou petic
    public string? Escopo { get; set; }
    public string Chave { get; set; } = string.Empty;
    public string Titulo { get; set; } = string.Empty;
    public string? Ajuda { get; set; }
    public string Tipo { get; set; } = PeDominios.TipoSecao.Formulario;
    public string? Prefixo { get; set; }
    public bool Travada { get; set; }
    public string? Inciso { get; set; }
    public bool NoDocumento { get; set; } = true;
    public bool NaPlanilha { get; set; } = true;
    public JsonElement Niveis { get; set; }
    // Situação geral (só fora do PDTIC)
    public string? Situacao { get; set; }
    public List<PeSeedCampo> Campos { get; set; } = new();
    // Registros do sistema (só no catálogo do DF)
    public List<PeSeedRegistro> Registros { get; set; } = new();
}

/// <summary>Registro do sistema semeado com a seção (não se edita nem se apaga).</summary>
public sealed class PeSeedRegistro
{
    public string Codigo { get; set; } = string.Empty;
    public Dictionary<string, JsonElement> Dados { get; set; } = new();
}

public sealed class PeSeedCampo
{
    public string Chave { get; set; } = string.Empty;
    public string Rotulo { get; set; } = string.Empty;
    public string? Ajuda { get; set; }
    public string Tipo { get; set; } = PeDominios.TipoCampo.TextoCurto;
    public JsonElement? Config { get; set; }
    public bool Principal { get; set; }
    public bool Travado { get; set; }
    public string? Largura { get; set; }
    public bool NoDocumento { get; set; } = true;
    public bool NaPlanilha { get; set; } = true;
    public JsonElement? Niveis { get; set; }
    // Situação geral (só fora do PDTIC; sem ela, vale a da seção)
    public string? Situacao { get; set; }
    public List<PeSeedOpcao> Opcoes { get; set; } = new();
}

public sealed class PeSeedOpcao
{
    public string Valor { get; set; } = string.Empty;
    public string Rotulo { get; set; } = string.Empty;
    public string? Cor { get; set; }
    public bool Travada { get; set; }
}

/// <summary>O que uma carga fez (zeros quando a versão já estava carregada).</summary>
public sealed record PeCarregamentoResultado(
    int VersaoAnterior, int Versao, bool Executou,
    int Niveis, int Etapas, int Passos, int Secoes, int Campos, int Opcoes, int Configuracoes, int Registros = 0,
    int Documentos = 0, int Capitulos = 0, int Blocos = 0, int Fluxos = 0);

/// <summary>
/// Carregador do modelo inicial do módulo Governança Estratégica: a trilha da seção 7 do
/// plano (7 etapas e os passos com a situação em cada nível) e os campos da seção 8, com
/// o cálculo da prioridade (GUT, produto) e do nível de risco (matriz probabilidade x
/// impacto), os temas das ações (os três do decreto travados) e a periodicidade padrão do
/// monitoramento; desde a versão 2 (E3), as seções do catálogo do DF (princípios e
/// diretrizes do ciclo) e do PETIC-DF, e os 11 princípios do art. 4º do Decreto nº
/// 48.900/2026 como registros do sistema; desde a versão 3 (E5), o campo do logotipo no
/// dicionário de nomes e o modelo do documento do PDTIC (capítulos, textos padrão e blocos de
/// dados, em documento-inicial.json); desde a versão 4 (E6), os fluxos do guia como modelo
/// (figuras 4 a 22, em fluxos-inicial.json). O conteúdo fica em JSON embutido na aplicação.
/// <list type="bullet">
/// <item>Idempotente: se a versão gravada em pe_configuracao (seed_modelo_versao) já é a do
/// JSON, não faz nada; senão insere só o que falta, achando cada item pela chave (nível
/// pelo código, etapa, passo e seção pela chave, campo pela chave na seção, opção pelo
/// valor no campo), e grava a versão.</item>
/// <item>Nunca sobrescreve: item que já existe não é tocado (nem título, nem situação, nem
/// ordem), inclusive o que o administrador mudou ou desativou. Itens do sistema nunca
/// são apagados de verdade, então não voltam por engano.</item>
/// <item>Uma transação só, com trava (advisory lock) no PostgreSQL: duas instâncias da API
/// subindo juntas não carregam em dobro.</item>
/// </list>
/// Quem chama na subida da API é o <see cref="PeCarregadorModeloHostedService"/>, que não
/// derruba o boot se as tabelas ainda não existirem.
/// </summary>
public sealed class PeCarregadorModelo
{
    public const string Autor = "carregador-modelo";
    public const string Recurso = "Planejamento.modelo-inicial.json";

    /// <summary>Versão do modelo inicial que trouxe as seções do DF e do PETIC-DF e os princípios (E3).</summary>
    public const int VersaoDosReferenciais = 2;

    /// <summary>Versão do modelo inicial que trouxe o modelo do documento do PDTIC e o logotipo (E5).</summary>
    public const int VersaoDoDocumento = 3;

    /// <summary>Versão do modelo inicial que trouxe os fluxos do guia (E6).</summary>
    public const int VersaoDosFluxos = 4;

    // Trava do carregador no PostgreSQL: segura até o fim da transação
    private const string SqlTrava = "SELECT pg_advisory_xact_lock(4890020260924)";

    private static readonly JsonSerializerOptions Opcoes = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    private readonly AppDbContext _context;

    public PeCarregadorModelo(AppDbContext context)
    {
        _context = context;
    }

    // ── Leitura e validação do JSON ─────────────────────────────────────────

    /// <summary>O modelo inicial embutido na aplicação, já validado.</summary>
    public static PeSeedModelo LerSeed()
    {
        using var stream = typeof(PeCarregadorModelo).Assembly.GetManifestResourceStream(Recurso)
            ?? throw new InvalidOperationException($"Recurso {Recurso} não encontrado no assembly.");
        using var leitor = new StreamReader(stream);
        return LerSeed(leitor.ReadToEnd());
    }

    public static PeSeedModelo LerSeed(string json)
    {
        var seed = JsonSerializer.Deserialize<PeSeedModelo>(json, Opcoes)
            ?? throw new InvalidOperationException("Modelo inicial vazio.");
        ValidarSeed(seed);
        return seed;
    }

    /// <summary>
    /// Confere a estrutura do JSON (chaves únicas e no formato, domínios, um campo principal
    /// por seção, travado nunca desligado). O config de cada campo é conferido na carga,
    /// com o modelo inteiro em volta.
    /// </summary>
    public static void ValidarSeed(PeSeedModelo seed)
    {
        void Falha(string mensagem) => throw new InvalidOperationException("Modelo inicial inválido: " + mensagem);

        if (seed.Versao < 1) Falha("versão ausente.");
        if (seed.Niveis.Count == 0) Falha("sem níveis.");
        var codigos = seed.Niveis.Select(n => n.Codigo).ToList();
        if (codigos.Distinct().Count() != codigos.Count || codigos.Any(c => !PeChaves.ChaveValida(c, PeChaves.MaximoNivel)))
            Falha("códigos de nível repetidos ou fora do formato.");

        var etapas = new HashSet<string>();
        var passos = new HashSet<string>();
        var secoes = new HashSet<string>();
        foreach (var etapa in seed.Etapas)
        {
            if (!etapas.Add(etapa.Chave) || !System.Text.RegularExpressions.Regex.IsMatch(etapa.Chave, "^[a-z][a-z0-9-]*$"))
                Falha($"etapa \"{etapa.Chave}\" repetida ou fora do formato.");
            foreach (var passo in etapa.Passos)
            {
                if (!passos.Add(passo.Chave) || !PeChaves.ChavePassoValida(passo.Chave) || !passo.Chave.StartsWith(etapa.Chave + "."))
                    Falha($"passo \"{passo.Chave}\" repetido ou fora do formato.");
                if (!PeDominios.TipoPasso.Todos.Contains(passo.Tipo)) Falha($"tipo do passo \"{passo.Chave}\".");
                if (passo.Inciso != null && !PeDominios.Inciso.EhValido(passo.Inciso)) Falha($"inciso do passo \"{passo.Chave}\".");
                if (string.IsNullOrWhiteSpace(passo.Titulo) || string.IsNullOrWhiteSpace(passo.OQueFazer))
                    Falha($"passo \"{passo.Chave}\" sem título ou sem o que fazer.");
                var sitPasso = Situacoes(passo.Niveis, codigos, null, $"passo {passo.Chave}");
                if (passo.Travado && sitPasso.ContainsValue(PeDominios.Situacao.Desligado)) Falha($"passo travado \"{passo.Chave}\" desligado.");

                foreach (var secao in passo.Secoes)
                {
                    if (!secoes.Add(secao.Chave) || !PeChaves.ChaveValida(secao.Chave, PeChaves.MaximoSecao))
                        Falha($"seção \"{secao.Chave}\" repetida ou fora do formato.");
                    if (!PeDominios.TipoSecao.Todos.Contains(secao.Tipo)) Falha($"tipo da seção \"{secao.Chave}\".");
                    if (secao.Inciso != null && !PeDominios.Inciso.EhValido(secao.Inciso)) Falha($"inciso da seção \"{secao.Chave}\".");
                    if (secao.Prefixo != null && !System.Text.RegularExpressions.Regex.IsMatch(secao.Prefixo, "^[A-Z][A-Z0-9]{0,4}$"))
                        Falha($"prefixo da seção \"{secao.Chave}\".");
                    var sitSecao = Situacoes(secao.Niveis, codigos, null, $"seção {secao.Chave}");
                    if (secao.Travada && sitSecao.ContainsValue(PeDominios.Situacao.Desligado)) Falha($"seção travada \"{secao.Chave}\" desligada.");
                    if (secao.Campos.Count(c => c.Principal) != 1) Falha($"a seção \"{secao.Chave}\" precisa de exatamente um campo principal.");

                    var campos = new HashSet<string>();
                    foreach (var campo in secao.Campos)
                    {
                        ValidarCampo(secao, campo, campos, Falha);
                        var sitCampo = Situacoes(campo.Niveis, codigos, sitSecao, $"campo {secao.Chave}.{campo.Chave}");
                        if ((campo.Travado || (campo.Principal && secao.Travada)) && sitCampo.ContainsValue(PeDominios.Situacao.Desligado))
                            Falha($"campo travado \"{secao.Chave}.{campo.Chave}\" desligado.");
                    }
                    if (secao.Registros.Count > 0) Falha($"a seção \"{secao.Chave}\" é do PDTIC e não traz registros.");
                }
            }
        }

        // Seções fora do PDTIC (escopos df e petic): situação geral, sem passo, nível ou trava
        foreach (var secao in seed.SecoesForaDoPdtic)
        {
            if (!secoes.Add(secao.Chave) || !PeChaves.ChaveValida(secao.Chave, PeChaves.MaximoSecao))
                Falha($"seção \"{secao.Chave}\" repetida ou fora do formato.");
            if (secao.Escopo is not (PeDominios.Escopo.Petic or PeDominios.Escopo.Df))
                Falha($"escopo da seção \"{secao.Chave}\" (petic ou df).");
            if (!PeDominios.TipoSecao.Todos.Contains(secao.Tipo)) Falha($"tipo da seção \"{secao.Chave}\".");
            if (secao.Travada || secao.Inciso != null || secao.Niveis.ValueKind is not (JsonValueKind.Undefined or JsonValueKind.Null))
                Falha($"a seção \"{secao.Chave}\" é fora do PDTIC: sem trava, inciso nem níveis.");
            if (secao.Situacao == null || !PeDominios.Situacao.Todas.Contains(secao.Situacao))
                Falha($"situação da seção \"{secao.Chave}\".");
            if (secao.Prefixo != null && !System.Text.RegularExpressions.Regex.IsMatch(secao.Prefixo, "^[A-Z][A-Z0-9]{0,4}$"))
                Falha($"prefixo da seção \"{secao.Chave}\".");
            if (secao.Campos.Count(c => c.Principal) != 1) Falha($"a seção \"{secao.Chave}\" precisa de exatamente um campo principal.");

            var campos = new HashSet<string>();
            foreach (var campo in secao.Campos)
            {
                ValidarCampo(secao, campo, campos, Falha);
                var situacao = campo.Situacao ?? secao.Situacao;
                if (campo.Travado || campo.Niveis is { ValueKind: not (JsonValueKind.Undefined or JsonValueKind.Null) }
                    || !PeDominios.Situacao.Todas.Contains(situacao!))
                    Falha($"campo \"{secao.Chave}.{campo.Chave}\": fora do PDTIC vale só a situação (sem trava nem níveis).");
            }

            if (secao.Registros.Count == 0) continue;
            if (secao.Escopo != PeDominios.Escopo.Df || secao.Tipo != PeDominios.TipoSecao.Tabela || secao.Prefixo == null)
                Falha($"só tabela do catálogo do DF, com prefixo, traz registros do sistema (seção \"{secao.Chave}\").");
            var codigosRegistro = new HashSet<string>();
            foreach (var registro in secao.Registros)
            {
                if (!codigosRegistro.Add(registro.Codigo)
                    || !System.Text.RegularExpressions.Regex.IsMatch(registro.Codigo, $"^{secao.Prefixo}[0-9]{{2,}}$"))
                    Falha($"código \"{registro.Codigo}\" da seção \"{secao.Chave}\" repetido ou fora do formato.");
                foreach (var chave in registro.Dados.Keys)
                {
                    var campo = secao.Campos.FirstOrDefault(c => c.Chave == chave);
                    if (campo == null || campo.Tipo is PeDominios.TipoCampo.LigacaoSecao or PeDominios.TipoCampo.LigacaoCatalogo
                            or PeDominios.TipoCampo.Calculado or PeDominios.TipoCampo.Arquivo)
                        Falha($"registro \"{registro.Codigo}\": o campo \"{chave}\" não existe ou não aceita valor semeado.");
                }
            }
        }
    }

    /// <summary>Chave, tipo, largura e opções de um campo do JSON.</summary>
    private static void ValidarCampo(PeSeedSecao secao, PeSeedCampo campo, HashSet<string> chaves, Action<string> falha)
    {
        if (!chaves.Add(campo.Chave) || !PeChaves.ChaveValida(campo.Chave, PeChaves.MaximoCampo))
            falha($"campo \"{secao.Chave}.{campo.Chave}\" repetido ou fora do formato.");
        if (!PeDominios.TipoCampo.Todos.Contains(campo.Tipo)) falha($"tipo do campo \"{secao.Chave}.{campo.Chave}\".");
        if (campo.Largura != null && !PeDominios.Largura.Todas.Contains(campo.Largura))
            falha($"largura do campo \"{secao.Chave}.{campo.Chave}\".");

        var valores = new HashSet<string>();
        foreach (var opcao in campo.Opcoes)
        {
            if (!valores.Add(opcao.Valor) || !PeChaves.ValorValido(opcao.Valor))
                falha($"opção \"{opcao.Valor}\" do campo \"{secao.Chave}.{campo.Chave}\" repetida ou fora do formato.");
            if (opcao.Cor != null && !PeDominios.Cor.Todas.Contains(opcao.Cor))
                falha($"cor da opção \"{opcao.Valor}\" do campo \"{secao.Chave}.{campo.Chave}\".");
        }
        var aceitaOpcoes = PeDominios.TipoCampo.TemOpcoes(campo.Tipo) || campo.Tipo == PeDominios.TipoCampo.Calculado;
        if (campo.Opcoes.Count > 0 && !aceitaOpcoes) falha($"o campo \"{secao.Chave}.{campo.Chave}\" não aceita opções.");
        if (PeDominios.TipoCampo.TemOpcoes(campo.Tipo) && campo.Opcoes.Count == 0)
            falha($"a lista \"{secao.Chave}.{campo.Chave}\" não tem opções.");
    }

    /// <summary>
    /// A situação em cada nível: texto (vale para todos), objeto com todos os códigos, ou,
    /// no campo, nada (herda a da seção).
    /// </summary>
    public static Dictionary<string, string> Situacoes(JsonElement? niveis, IReadOnlyList<string> codigos,
        Dictionary<string, string>? herdado, string item)
    {
        Exception Erro(string m) => new InvalidOperationException($"Modelo inicial inválido: {item}: {m}");

        if (niveis == null || niveis.Value.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
            return herdado != null ? new Dictionary<string, string>(herdado) : throw Erro("sem a situação nos níveis.");

        if (niveis.Value.ValueKind == JsonValueKind.String)
        {
            var todas = niveis.Value.GetString()!;
            if (!PeDominios.Situacao.Todas.Contains(todas)) throw Erro($"situação \"{todas}\".");
            return codigos.ToDictionary(c => c, _ => todas);
        }

        if (niveis.Value.ValueKind != JsonValueKind.Object) throw Erro("\"niveis\" precisa ser texto ou objeto.");
        var mapa = niveis.Value.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.GetString() ?? string.Empty);
        if (mapa.Count != codigos.Count || codigos.Any(c => !mapa.ContainsKey(c)))
            throw Erro("\"niveis\" precisa ter exatamente os níveis " + string.Join(", ", codigos) + ".");
        if (mapa.Values.Any(s => !PeDominios.Situacao.Todas.Contains(s))) throw Erro("situação fora do domínio.");
        return mapa;
    }

    // ── Carga ───────────────────────────────────────────────────────────────

    public Task<PeCarregamentoResultado> CarregarAsync(CancellationToken ct = default) => CarregarAsync(LerSeed(), ct);

    public Task<PeCarregamentoResultado> CarregarAsync(PeSeedModelo seed, CancellationToken ct = default) =>
        CarregarAsync(seed, null, ct);

    /// <summary>A carga com o modelo do documento dado (nulo = o embutido na aplicação).</summary>
    public Task<PeCarregamentoResultado> CarregarAsync(PeSeedModelo seed, PeSeedDocumentos? documentos, CancellationToken ct = default) =>
        CarregarAsync(seed, documentos, null, ct);

    /// <summary>A carga com o modelo do documento e os fluxos dados (nulo = os embutidos na aplicação).</summary>
    public async Task<PeCarregamentoResultado> CarregarAsync(PeSeedModelo seed, PeSeedDocumentos? documentos, PeSeedFluxos? fluxos,
        CancellationToken ct = default)
    {
        ValidarSeed(seed);

        await using var transacao = _context.Database.IsRelational() ? await _context.Database.BeginTransactionAsync(ct) : null;
        if (_context.Database.IsNpgsql())
            await _context.Database.ExecuteSqlRawAsync(SqlTrava, ct);

        var configuracoes = await _context.PeConfiguracoes.ToListAsync(ct);
        var registroVersao = configuracoes.FirstOrDefault(c => c.Chave == PeConfiguracao.ChaveVersaoModelo);
        var versaoAnterior = LerVersao(registroVersao?.Valor);
        if (versaoAnterior >= seed.Versao)
            return new PeCarregamentoResultado(versaoAnterior, versaoAnterior, false, 0, 0, 0, 0, 0, 0, 0);

        var agora = DateTime.UtcNow;
        int nNiveis = 0, nEtapas = 0, nPassos = 0, nSecoes = 0, nCampos = 0, nOpcoes = 0, nConfig = 0;

        // Níveis (pelo código)
        var niveis = await _context.PeNiveis.ToListAsync(ct);
        var ordemNivel = niveis.Select(n => n.Ordem).DefaultIfEmpty(0).Max();
        var semNiveis = niveis.Count == 0;
        foreach (var (s, i) in seed.Niveis.Select((s, i) => (s, i)))
        {
            if (niveis.Any(n => n.Codigo == s.Codigo)) continue;
            var nivel = new PeNivel
            {
                Codigo = s.Codigo,
                Nome = s.Nome,
                Descricao = s.Descricao,
                Ordem = semNiveis ? i + 1 : ++ordemNivel,
                Ativo = true,
                CriadoEm = agora,
                CriadoPor = Autor
            };
            niveis.Add(nivel);
            _context.PeNiveis.Add(nivel);
            nNiveis++;
        }
        var codigos = seed.Niveis.Select(n => n.Codigo).ToList();
        var nivelPorCodigo = niveis.ToDictionary(n => n.Codigo);

        // Configurações gerais (pela chave)
        foreach (var (chave, valor) in seed.Configuracoes)
        {
            if (configuracoes.Any(c => c.Chave == chave)) continue;
            _context.PeConfiguracoes.Add(new PeConfiguracao { Chave = chave, Valor = valor.GetRawText(), CriadoEm = agora, CriadoPor = Autor });
            nConfig++;
        }

        // Estrutura: o que já existe é lido para achar pela chave e para conferir os configs
        var etapas = await _context.PeEtapas.ToListAsync(ct);
        var passos = await _context.PePassos.ToListAsync(ct);
        var secoes = await _context.PeSecoes.ToListAsync(ct);
        var campos = await _context.PeCampos.ToListAsync(ct);
        var opcoes = await _context.PeOpcoes.ToListAsync(ct);
        var novosCampos = new List<(PeCampo Campo, PeSeedCampo Seed)>();

        foreach (var (se, ie) in seed.Etapas.Select((e, i) => (e, i)))
        {
            var etapa = etapas.FirstOrDefault(e => e.Chave == se.Chave);
            if (etapa == null)
            {
                etapa = new PeEtapa
                {
                    Chave = se.Chave,
                    Titulo = se.Titulo,
                    Descricao = se.Descricao,
                    ReferenciaGuia = se.ReferenciaGuia,
                    Ordem = ie + 1,
                    Sistema = true,
                    CriadoEm = agora,
                    CriadoPor = Autor
                };
                etapas.Add(etapa);
                _context.PeEtapas.Add(etapa);
                nEtapas++;
            }

            foreach (var (sp, ip) in se.Passos.Select((p, i) => (p, i)))
            {
                var passo = passos.FirstOrDefault(p => p.Chave == sp.Chave);
                if (passo == null)
                {
                    passo = new PePasso
                    {
                        Etapa = etapa,
                        Chave = sp.Chave,
                        Titulo = sp.Titulo,
                        OQueFazer = sp.OQueFazer,
                        BaseLegal = sp.BaseLegal,
                        ReferenciaGuia = sp.ReferenciaGuia,
                        Tipo = sp.Tipo,
                        IncisoDecreto = sp.Inciso,
                        Travado = sp.Travado,
                        AceitaNaoSeAplica = sp.AceitaNaoSeAplica,
                        Ordem = ip + 1,
                        Sistema = true,
                        CriadoEm = agora,
                        CriadoPor = Autor
                    };
                    foreach (var (codigo, situacao) in Situacoes(sp.Niveis, codigos, null, sp.Chave))
                        passo.Niveis.Add(new PePassoNivel { Nivel = nivelPorCodigo[codigo], Situacao = situacao });
                    passos.Add(passo);
                    _context.PePassos.Add(passo);
                    nPassos++;
                }

                foreach (var (ss, iss) in sp.Secoes.Select((s, i) => (s, i)))
                {
                    var sitSecao = Situacoes(ss.Niveis, codigos, null, ss.Chave);
                    var secao = secoes.FirstOrDefault(s => s.Chave == ss.Chave);
                    if (secao == null)
                    {
                        secao = new PeSecao
                        {
                            Passo = passo,
                            Escopo = PeDominios.Escopo.Pdtic,
                            Chave = ss.Chave,
                            Titulo = ss.Titulo,
                            Ajuda = ss.Ajuda,
                            Tipo = ss.Tipo,
                            PrefixoCodigo = ss.Prefixo,
                            Ordem = iss + 1,
                            NoDocumento = ss.NoDocumento,
                            NaPlanilha = ss.NaPlanilha,
                            Travada = ss.Travada,
                            IncisoDecreto = ss.Inciso,
                            Sistema = true,
                            CriadoEm = agora,
                            CriadoPor = Autor
                        };
                        foreach (var (codigo, situacao) in sitSecao)
                            secao.Niveis.Add(new PeSecaoNivel { Nivel = nivelPorCodigo[codigo], Situacao = situacao });
                        secoes.Add(secao);
                        _context.PeSecoes.Add(secao);
                        nSecoes++;
                    }

                    CamposEOpcoes(ss, secao, sitSecao);
                }
            }
        }

        // Seções fora do PDTIC (catálogo do DF e PETIC-DF): sem passo nem nível, com a situação geral
        foreach (var ss in seed.SecoesForaDoPdtic)
        {
            var secao = secoes.FirstOrDefault(s => s.Chave == ss.Chave);
            if (secao == null)
            {
                secao = new PeSecao
                {
                    Escopo = ss.Escopo!,
                    Chave = ss.Chave,
                    Titulo = ss.Titulo,
                    Ajuda = ss.Ajuda,
                    Tipo = ss.Tipo,
                    PrefixoCodigo = ss.Prefixo,
                    Ordem = seed.SecoesForaDoPdtic.Where(s => s.Escopo == ss.Escopo).ToList().IndexOf(ss) + 1,
                    NoDocumento = ss.NoDocumento,
                    NaPlanilha = ss.NaPlanilha,
                    SituacaoGeral = ss.Situacao,
                    Sistema = true,
                    CriadoEm = agora,
                    CriadoPor = Autor
                };
                secoes.Add(secao);
                _context.PeSecoes.Add(secao);
                nSecoes++;
            }
            CamposEOpcoes(ss, secao, null);
        }

        // Campos (e as opções deles) que faltam numa seção. Com a situação de cada nível no
        // PDTIC; fora dele, com a situação geral (a do campo ou a da seção)
        void CamposEOpcoes(PeSeedSecao ss, PeSecao secao, Dictionary<string, string>? sitSecao)
        {
            foreach (var (sc, ic) in ss.Campos.Select((c, i) => (c, i)))
            {
                var campo = campos.FirstOrDefault(c => c.Chave == sc.Chave && (c.Secao == secao || (secao.Id != 0 && c.SecaoId == secao.Id)));
                if (campo == null)
                {
                    campo = new PeCampo
                    {
                        Secao = secao,
                        Chave = sc.Chave,
                        Rotulo = sc.Rotulo,
                        Ajuda = sc.Ajuda,
                        Tipo = sc.Tipo,
                        Principal = sc.Principal,
                        Travado = sc.Travado,
                        Ordem = ic + 1,
                        NoDocumento = sc.NoDocumento,
                        NaPlanilha = sc.NaPlanilha,
                        Largura = sc.Largura,
                        SituacaoGeral = sitSecao == null ? sc.Situacao ?? ss.Situacao : null,
                        Sistema = true,
                        CriadoEm = agora,
                        CriadoPor = Autor
                    };
                    if (sitSecao != null)
                        foreach (var (codigo, situacao) in Situacoes(sc.Niveis, codigos, sitSecao, $"{ss.Chave}.{sc.Chave}"))
                            campo.Niveis.Add(new PeCampoNivel { Nivel = nivelPorCodigo[codigo], Situacao = situacao });
                    campos.Add(campo);
                    _context.PeCampos.Add(campo);
                    novosCampos.Add((campo, sc));
                    nCampos++;
                }

                foreach (var (so, io) in sc.Opcoes.Select((o, i) => (o, i)))
                {
                    if (opcoes.Any(o => o.Valor == so.Valor && (o.Campo == campo || (campo.Id != 0 && o.CampoId == campo.Id)))) continue;
                    var opcao = new PeOpcao
                    {
                        Campo = campo,
                        Valor = so.Valor,
                        Rotulo = so.Rotulo,
                        Cor = so.Cor,
                        Travada = so.Travada,
                        Ordem = io + 1,
                        Ativa = true,
                        Sistema = true,
                        CriadoEm = agora,
                        CriadoPor = Autor
                    };
                    opcoes.Add(opcao);
                    _context.PeOpcoes.Add(opcao);
                    nOpcoes++;
                }
            }
        }

        // Config de cada campo novo, conferido com o modelo inteiro em volta (ligações e cálculos)
        foreach (var (campo, sc) in novosCampos)
        {
            var secao = campo.Secao!;
            PeCampoInfo Info(PeCampo c)
            {
                var suas = opcoes.Where(o => o.Campo == c || (c.Id != 0 && o.CampoId == c.Id)).ToList();
                return new PeCampoInfo(c.Chave, c.Tipo, suas.Select(o => o.Valor).ToList(), suas.Where(o => o.Ativa).Select(o => o.Valor).ToList());
            }
            var irmaos = campos.Where(c => c != campo && c.ExcluidoEm == null
                                           && (c.Secao == secao || (secao.Id != 0 && c.SecaoId == secao.Id))).ToList();
            var contexto = new PeContextoConfig
            {
                Escopo = secao.Escopo,
                ChaveDoCampo = campo.Chave,
                CamposDaSecao = irmaos.Select(Info).ToList(),
                SecaoPorChave = chave => secoes.Where(s => s.Chave == chave && s.ExcluidoEm == null)
                    .Select(s => new PeSecaoInfo(s.Chave, s.Escopo, s.Tipo)).FirstOrDefault(),
                OpcoesDoCampo = Info(campo).Opcoes
            };
            try
            {
                campo.Config = PeConfigCampo.Normalizar(campo.Tipo, sc.Config, contexto);
            }
            catch (ApiException ex)
            {
                throw new InvalidOperationException($"Modelo inicial inválido: config do campo \"{secao.Chave}.{campo.Chave}\": {ex.Error.Message}");
            }
        }

        // Registros do sistema (os princípios do art. 4º), no catálogo do DF: os que faltam,
        // achados pelo código; a sequência passa a começar depois do maior código semeado
        var nRegistros = 0;
        var comRegistros = seed.SecoesForaDoPdtic.Where(s => s.Registros.Count > 0).ToList();
        if (comRegistros.Count > 0)
        {
            var existentes = await _context.PeRegistros.Where(r => r.PeticId == null && r.PdticId == null)
                .Select(r => new { r.SecaoId, r.Codigo, r.Ordem })
                .ToListAsync(ct);
            var sequencias = await _context.PeRegistroSequencias.Where(s => s.Dono == PeDono.Df.Chave).ToListAsync(ct);
            foreach (var ss in comRegistros)
            {
                var secao = secoes.First(s => s.Chave == ss.Chave);
                var daSecao = existentes.Where(r => secao.Id != 0 && r.SecaoId == secao.Id).ToList();
                var ordem = daSecao.Select(r => r.Ordem).DefaultIfEmpty(0).Max();
                var camposDaSecao = campos.Where(c => c.ExcluidoEm == null && (c.Secao == secao || (secao.Id != 0 && c.SecaoId == secao.Id))).ToList();
                var maior = daSecao.Where(r => r.Codigo != null).Select(r => PeRegistroService.NumeroDoCodigo(r.Codigo!)).DefaultIfEmpty(0).Max();

                foreach (var sr in ss.Registros)
                {
                    maior = Math.Max(maior, PeRegistroService.NumeroDoCodigo(sr.Codigo));
                    if (daSecao.Any(r => r.Codigo == sr.Codigo)) continue;
                    _context.PeRegistros.Add(new PeRegistro
                    {
                        Secao = secao,
                        Codigo = sr.Codigo,
                        Ordem = ++ordem,
                        Dados = DadosDoRegistro(ss, sr, camposDaSecao, opcoes),
                        Sistema = true,
                        CriadoEm = agora,
                        CriadoPor = Autor
                    });
                    nRegistros++;
                }

                var sequencia = secao.Id == 0 ? null : sequencias.FirstOrDefault(s => s.SecaoId == secao.Id);
                if (sequencia == null)
                    _context.PeRegistroSequencias.Add(new PeRegistroSequencia { Secao = secao, Dono = PeDono.Df.Chave, Ultimo = maior });
                else if (sequencia.Ultimo < maior)
                    sequencia.Ultimo = maior;
            }
        }

        // Modelo do documento do PDTIC (versão 3, E5): o que falta, achado pelo tipo do modelo e
        // pela chave do capítulo; os blocos entram com o capítulo novo (capítulo que já existe não
        // é tocado, nem os blocos dele)
        var (nDocumentos, nCapitulos, nBlocos) = seed.Versao >= VersaoDoDocumento
            ? await CarregarDocumentosAsync(documentos ?? PeDocSeed.Ler(), passos, secoes, campos, opcoes, agora, ct)
            : (0, 0, 0);

        // Fluxos do guia (versão 4, E6): os que faltam, achados pela chave; o que já existe
        // (inclusive o que o administrador mudou) não é tocado
        var nFluxos = seed.Versao >= VersaoDosFluxos
            ? await CarregarFluxosAsync(fluxos ?? PeFluxoSeed.Ler(), agora, ct)
            : 0;

        // A versão carregada
        if (registroVersao == null)
        {
            _context.PeConfiguracoes.Add(new PeConfiguracao
            {
                Chave = PeConfiguracao.ChaveVersaoModelo,
                Valor = seed.Versao.ToString(),
                CriadoEm = agora,
                CriadoPor = Autor
            });
        }
        else
        {
            registroVersao.Valor = seed.Versao.ToString();
            registroVersao.AlteradoEm = agora;
            registroVersao.AlteradoPor = Autor;
        }

        await _context.SaveChangesAsync(ct);
        if (transacao != null) await transacao.CommitAsync(ct);

        return new PeCarregamentoResultado(versaoAnterior, seed.Versao, true, nNiveis, nEtapas, nPassos, nSecoes, nCampos, nOpcoes, nConfig,
            nRegistros, nDocumentos, nCapitulos, nBlocos, nFluxos);
    }

    /// <summary>
    /// Acrescenta os fluxos do guia que faltam (pela chave), com a definição conferida pela
    /// validação da API e numerada. Erro no JSON = modelo inicial inválido e nada é gravado.
    /// </summary>
    private async Task<int> CarregarFluxosAsync(PeSeedFluxos fluxos, DateTime agora, CancellationToken ct)
    {
        var validos = PeFluxoSeed.Validar(fluxos);
        var existentes = await _context.PeFluxosModelo.Select(f => f.Chave).ToListAsync(ct);
        var novos = 0;
        foreach (var ((seed, definicao), i) in validos.Select((v, i) => (v, i)))
        {
            if (existentes.Contains(seed.Chave)) continue;
            _context.PeFluxosModelo.Add(new PeFluxoModelo
            {
                Chave = seed.Chave,
                Nome = seed.Nome.Trim(),
                FiguraGuia = string.IsNullOrWhiteSpace(seed.FiguraGuia) ? null : seed.FiguraGuia.Trim(),
                Ordem = i + 1,
                Definicao = PeFluxoDefinicaoLeitor.ParaJson(definicao),
                CriadoEm = agora,
                CriadoPor = Autor
            });
            novos++;
        }
        return novos;
    }

    /// <summary>
    /// Acrescenta o modelo do documento (se o tipo ainda não tem modelo) e os capítulos que
    /// faltam, com os blocos. O config de cada bloco passa pelo PeDocConfig com as seções e os
    /// campos em volta (os que acabaram de entrar também); erro = modelo inicial inválido e
    /// nada é gravado.
    /// </summary>
    private async Task<(int Documentos, int Capitulos, int Blocos)> CarregarDocumentosAsync(PeSeedDocumentos documentos,
        List<PePasso> passos, List<PeSecao> secoes, List<PeCampo> campos, List<PeOpcao> opcoes, DateTime agora, CancellationToken ct)
    {
        PeDocSeed.Validar(documentos, passos.Where(p => p.ExcluidoEm == null).Select(p => p.Chave).ToHashSet());

        bool DaSecao(PeCampo c, PeSecao s) => c.Secao == s || (s.Id != 0 && c.SecaoId == s.Id);
        var contexto = new PeDocConfig.Contexto
        {
            CamposDaSecao = chave =>
            {
                var secao = secoes.FirstOrDefault(s => s.Chave == chave && s.Escopo == PeDominios.Escopo.Pdtic && s.ExcluidoEm == null);
                return secao == null
                    ? null
                    : campos.Where(c => c.ExcluidoEm == null && DaSecao(c, secao)).Select(c => (c.Chave, c.Tipo)).ToList();
            },
            Temas = () =>
            {
                var acoes = secoes.FirstOrDefault(s => s.Chave == PeDominios.TemaDecreto.SecaoAcoes);
                var tema = acoes == null ? null : campos.FirstOrDefault(c => c.Chave == PeDominios.TemaDecreto.CampoTema && DaSecao(c, acoes));
                return tema == null
                    ? Array.Empty<string>()
                    : opcoes.Where(o => o.Campo == tema || (tema.Id != 0 && o.CampoId == tema.Id)).Select(o => o.Valor).ToList();
            }
        };

        var modelos = await _context.PeDocModelos.ToListAsync(ct);
        var capitulos = await _context.PeDocCapitulos.ToListAsync(ct);
        int nDocumentos = 0, nCapitulos = 0, nBlocos = 0;

        foreach (var sm in documentos.Modelos)
        {
            var modelo = modelos.FirstOrDefault(m => m.Tipo == sm.Tipo && m.Ativo) ?? modelos.FirstOrDefault(m => m.Tipo == sm.Tipo);
            var modeloNovo = modelo == null;
            if (modelo == null)
            {
                modelo = new PeDocModelo { Tipo = sm.Tipo, Nome = sm.Nome, Ativo = true, CriadoEm = agora, CriadoPor = Autor };
                _context.PeDocModelos.Add(modelo);
                modelos.Add(modelo);
                nDocumentos++;
            }
            var doModelo = capitulos.Where(c => c.Modelo == modelo || (modelo.Id != 0 && c.ModeloId == modelo.Id)).ToList();

            void Carregar(PeSeedDocCapitulo sc, PeDocCapitulo? pai, int posicao, bool paiNovo)
            {
                var capitulo = doModelo.FirstOrDefault(c => c.Chave == sc.Chave);
                var novo = capitulo == null;
                if (capitulo == null)
                {
                    // No modelo novo, a ordem do JSON; num modelo que já existe, depois dos irmãos
                    var irmaos = doModelo.Where(c => pai == null
                        ? c.Pai == null && c.PaiId == null
                        : c.Pai == pai || (pai.Id != 0 && c.PaiId == pai.Id));
                    capitulo = new PeDocCapitulo
                    {
                        Modelo = modelo,
                        Pai = pai,
                        Chave = sc.Chave,
                        Titulo = sc.Titulo,
                        Numerado = sc.Numerado,
                        Obrigatorio = sc.Obrigatorio || sc.Travado,
                        Travado = sc.Travado,
                        IncisoDecreto = sc.Inciso,
                        PassoChave = sc.Passo,
                        Ordem = paiNovo ? posicao : irmaos.Select(c => c.Ordem).DefaultIfEmpty(0).Max() + 1,
                        Sistema = true,
                        CriadoEm = agora,
                        CriadoPor = Autor
                    };
                    foreach (var (sb, ib) in sc.Blocos.Select((b, i) => (b, i)))
                    {
                        PeDocConfig.Resultado config;
                        try
                        {
                            config = PeDocConfig.Normalizar(sb.Tipo, sb.Config(), contexto);
                        }
                        catch (ApiException ex)
                        {
                            throw new InvalidOperationException(
                                $"Modelo inicial do documento inválido: bloco {ib + 1} do capítulo \"{sc.Chave}\": {ex.Error.Message}");
                        }
                        if (config.Imagens.Count > 0)
                            throw new InvalidOperationException($"Modelo inicial do documento inválido: o capítulo \"{sc.Chave}\" traz imagem.");
                        capitulo.Blocos.Add(new PeDocBloco
                        {
                            Tipo = sb.Tipo,
                            Config = config.Json,
                            Ordem = ib + 1,
                            Sistema = true,
                            CriadoEm = agora,
                            CriadoPor = Autor
                        });
                        nBlocos++;
                    }
                    _context.PeDocCapitulos.Add(capitulo);
                    doModelo.Add(capitulo);
                    capitulos.Add(capitulo);
                    nCapitulos++;
                }
                foreach (var (sub, i) in sc.Subcapitulos.Select((s, i) => (s, i)))
                    Carregar(sub, capitulo, i + 1, novo);
            }

            foreach (var (sc, i) in sm.Capitulos.Select((c, i) => (c, i)))
                Carregar(sc, null, i + 1, modeloNovo);
        }
        return (nDocumentos, nCapitulos, nBlocos);
    }

    /// <summary>
    /// Os valores de um registro do JSON, conferidos como o motor de registros confere (tipo,
    /// tamanho, opção) e com os obrigatórios preenchidos. Erro = modelo inicial inválido.
    /// </summary>
    private static string DadosDoRegistro(PeSeedSecao ss, PeSeedRegistro sr, List<PeCampo> campos, List<PeOpcao> opcoes)
    {
        var dados = new System.Text.Json.Nodes.JsonObject();
        foreach (var campo in campos)
        {
            var situacao = campo.SituacaoGeral;
            if (!sr.Dados.TryGetValue(campo.Chave, out var entrada))
            {
                if (situacao == PeDominios.Situacao.Obrigatorio && campo.Tipo != PeDominios.TipoCampo.Calculado && !PeRegistroDados.EhLigacao(campo))
                    throw new InvalidOperationException($"Modelo inicial inválido: registro \"{sr.Codigo}\" sem o campo obrigatório \"{campo.Chave}\".");
                continue;
            }
            var doCampo = opcoes.Where(o => o.Campo == campo || (campo.Id != 0 && o.CampoId == campo.Id)).ToList();
            var resultado = PeValores.Normalizar(campo, doCampo, entrada, null);
            if (resultado.Erro != null || resultado.ArquivoId != null)
                throw new InvalidOperationException(
                    $"Modelo inicial inválido: registro \"{sr.Codigo}\" da seção \"{ss.Chave}\", campo \"{campo.Chave}\": {resultado.Erro ?? "arquivo não se semeia"}");
            if (resultado.Valor != null) dados[campo.Chave] = resultado.Valor;
        }
        return dados.ToJsonString(PeModeloService.JsonHistorico);
    }

    private static int LerVersao(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor)) return 0;
        try
        {
            using var documento = JsonDocument.Parse(valor);
            return documento.RootElement.ValueKind == JsonValueKind.Number && documento.RootElement.TryGetInt32(out var v) ? v : 0;
        }
        catch (JsonException)
        {
            return 0;
        }
    }
}
