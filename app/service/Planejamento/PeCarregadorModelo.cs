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
/// de cada nível pelo código; no campo, sem "niveis", vale a situação da seção.
/// </summary>
public sealed class PeSeedModelo
{
    public int Versao { get; set; }

    public List<PeSeedNivel> Niveis { get; set; } = new();

    public Dictionary<string, JsonElement> Configuracoes { get; set; } = new();

    public List<PeSeedEtapa> Etapas { get; set; } = new();
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
    public List<PeSeedCampo> Campos { get; set; } = new();
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
    int Niveis, int Etapas, int Passos, int Secoes, int Campos, int Opcoes, int Configuracoes);

/// <summary>
/// Carregador do modelo inicial do módulo Governança Estratégica: a trilha da seção 7 do
/// plano (7 etapas e os passos com a situação em cada nível) e os campos da seção 8, com
/// o cálculo da prioridade (GUT, produto) e do nível de risco (matriz probabilidade x
/// impacto), os temas das ações (os três do decreto travados) e a periodicidade padrão do
/// monitoramento. O conteúdo fica em JSON embutido na aplicação.
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
                        if (!campos.Add(campo.Chave) || !PeChaves.ChaveValida(campo.Chave, PeChaves.MaximoCampo))
                            Falha($"campo \"{secao.Chave}.{campo.Chave}\" repetido ou fora do formato.");
                        if (!PeDominios.TipoCampo.Todos.Contains(campo.Tipo)) Falha($"tipo do campo \"{secao.Chave}.{campo.Chave}\".");
                        if (campo.Largura != null && !PeDominios.Largura.Todas.Contains(campo.Largura))
                            Falha($"largura do campo \"{secao.Chave}.{campo.Chave}\".");
                        var sitCampo = Situacoes(campo.Niveis, codigos, sitSecao, $"campo {secao.Chave}.{campo.Chave}");
                        if ((campo.Travado || (campo.Principal && secao.Travada)) && sitCampo.ContainsValue(PeDominios.Situacao.Desligado))
                            Falha($"campo travado \"{secao.Chave}.{campo.Chave}\" desligado.");

                        var valores = new HashSet<string>();
                        foreach (var opcao in campo.Opcoes)
                        {
                            if (!valores.Add(opcao.Valor) || !PeChaves.ValorValido(opcao.Valor))
                                Falha($"opção \"{opcao.Valor}\" do campo \"{secao.Chave}.{campo.Chave}\" repetida ou fora do formato.");
                            if (opcao.Cor != null && !PeDominios.Cor.Todas.Contains(opcao.Cor))
                                Falha($"cor da opção \"{opcao.Valor}\" do campo \"{secao.Chave}.{campo.Chave}\".");
                        }
                        var aceitaOpcoes = PeDominios.TipoCampo.TemOpcoes(campo.Tipo) || campo.Tipo == PeDominios.TipoCampo.Calculado;
                        if (campo.Opcoes.Count > 0 && !aceitaOpcoes) Falha($"o campo \"{secao.Chave}.{campo.Chave}\" não aceita opções.");
                        if (PeDominios.TipoCampo.TemOpcoes(campo.Tipo) && campo.Opcoes.Count == 0)
                            Falha($"a lista \"{secao.Chave}.{campo.Chave}\" não tem opções.");
                    }
                }
            }
        }
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

    public async Task<PeCarregamentoResultado> CarregarAsync(PeSeedModelo seed, CancellationToken ct = default)
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
                                Sistema = true,
                                CriadoEm = agora,
                                CriadoPor = Autor
                            };
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

        return new PeCarregamentoResultado(versaoAnterior, seed.Versao, true, nNiveis, nEtapas, nPassos, nSecoes, nCampos, nOpcoes, nConfig);
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
