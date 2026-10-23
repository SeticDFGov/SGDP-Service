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
    // Pode ficar vazio desde a versão 9 (o título diz tudo)
    public string? OQueFazer { get; set; } = string.Empty;
    public string? BaseLegal { get; set; }
    public string? ReferenciaGuia { get; set; }
    public string Tipo { get; set; } = PeDominios.TipoPasso.Dados;
    public string? Inciso { get; set; }
    public bool Travado { get; set; }
    public bool AceitaNaoSeAplica { get; set; }
    public JsonElement Niveis { get; set; }
    public List<PeSeedSecao> Secoes { get; set; } = new();
    // Desde a versão 7 (F1): textos gravados por versões anteriores, trocados pelos de hoje
    public PeSeedAntes? Antes { get; set; }
}

/// <summary>
/// Os textos que versões anteriores do carregador gravaram num item do sistema (desde a versão
/// 7, F1). Quando o item já existe e o texto dele ainda é um destes (ninguém mudou), a carga
/// troca pelo texto de hoje do JSON; o texto que o administrador mudou fica como está.
/// </summary>
public sealed class PeSeedAntes
{
    public List<string>? Titulo { get; set; }
    public List<string>? OQueFazer { get; set; }
    public List<string>? Ajuda { get; set; }
    // O config inteiro de antes (comparado sem a ordem das chaves) e a largura de antes (nulo = sem largura)
    public List<JsonElement>? Config { get; set; }
    public List<string?>? Largura { get; set; }
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
    // Seção por ciclo (só no PDTIC, desde a versão 6): monitoramento ou avaliacao
    public string? PorCiclo { get; set; }
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
    // Desde a versão 7 (F1): a ajuda gravada por versões anteriores, trocada pela de hoje
    public PeSeedAntes? Antes { get; set; }
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
    int Documentos = 0, int Capitulos = 0, int Blocos = 0, int Fluxos = 0, int SecoesPorCiclo = 0, int Correcoes = 0,
    int Reestruturados = 0, int Textos = 0);

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
/// (figuras 4 a 22, em fluxos-inicial.json); na versão 5 (E7, rodada A), o documento do PDTIC
/// ganhou a aprovação e a publicação na folha de rosto e os blocos de fluxo das etapas na
/// metodologia e na revisão e acompanhamento (como bloco só entra com o capítulo novo, só o
/// banco novo recebe esses blocos); na versão 6 (E7, rodada B), as seções por ciclo (o
/// monitoramento em 5.1 e 5.2 e a avaliação intermediária em 6.1 a 6.3, marcadas também nas
/// seções que já existiam, porque a coluna é nova), as configurações do prazo de fechamento do
/// ciclo e da avaliação final e os modelos do relatório de acompanhamento (RA, Anexo XIV) e do
/// relatório de resultados (RR, Anexo XV); na versão 7 (F1, a rodada de correções da revisão
/// final), as regras novas do config nos campos que já existiam (a data de decisão que não pode
/// ser futura, o formato do processo SEI e o do endereço de internet) e a troca dos textos que o
/// próprio carregador gravou e que ninguém mudou (a ajuda de "Quem decidiu" em cada aprovação, os
/// textos do passo 5.1 e a capa do RA); na versão 8 (F3, o retorno do dono do produto), a
/// configuração modo_niveis = "livre" quando ela ainda não existe (só carrega com a migration
/// PeModoLivreValidacao aplicada); na versão 9 (revisão dos textos e dos passos, 2026-10-20), a
/// trilha do órgão com os passos juntados, a reestruturação do banco que já tinha o modelo e a
/// regravação dos textos sempre que o JSON muda. O conteúdo fica em JSON embutido na aplicação.
/// <list type="bullet">
/// <item>Idempotente: se a versão gravada em pe_configuracao (seed_modelo_versao) já é a do
/// JSON, não faz nada; senão insere só o que falta, achando cada item pela chave (nível
/// pelo código, etapa, passo e seção pela chave, campo pela chave na seção, opção pelo
/// valor no campo), e grava a versão.</item>
/// <item>Não sobrescreve a estrutura: item que já existe não muda de situação, de ordem nem
/// de config (inclusive o que o administrador mudou ou desativou), salvo a reestruturação
/// única da versão 9 (<see cref="VersaoDaReestruturacao"/>). Os textos, desde a versão 9,
/// são regravados a partir do JSON sempre que ele muda (versão nova ou o mesmo número com
/// outro conteúdo). Itens do sistema nunca são apagados de verdade, então não voltam por
/// engano.</item>
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

    /// <summary>
    /// Versão do modelo inicial da E7, rodada A: o documento do PDTIC com a aprovação e a
    /// publicação na folha de rosto e os fluxos das etapas nos capítulos (só em banco novo).
    /// </summary>
    public const int VersaoDaAprovacao = 5;

    /// <summary>
    /// Versão do modelo inicial da E7, rodada B: as seções por ciclo, as configurações do
    /// acompanhamento e os modelos do RA e do RR. Só carrega com a migration PeAcompanhamento
    /// aplicada (ela lê pe_secao.por_ciclo); é o que liga os ciclos e os relatórios na API
    /// (<see cref="PeAcompanhamentoAtivo"/>).
    /// </summary>
    public const int VersaoDoAcompanhamento = 6;

    /// <summary>
    /// Versão do modelo inicial da F1 (rodada de correções da revisão final): acrescenta aos campos
    /// que já existem as chaves novas do config (<see cref="ChavesNovasDoConfig"/>) e troca os
    /// textos do sistema que ainda são os que o carregador gravou (os "antes" do JSON).
    /// </summary>
    public const int VersaoDaRevisaoFinal = 7;

    /// <summary>
    /// As chaves do config que a versão 7 trouxe (a data que não pode ser futura e o formato do
    /// texto curto). Só elas entram num campo que já existia, e só na carga que vem de uma versão
    /// anterior à 7: o que o administrador mudou depois (inclusive tirar a chave) fica.
    /// </summary>
    public static readonly string[] ChavesNovasDoConfig = { "naoFutura", "formato" };

    /// <summary>
    /// Versão do modelo inicial da F3 (o retorno do dono do produto): grava a configuração
    /// modo_niveis = "livre" quando ela não existe (vale para os bancos que já têm o módulo). Só
    /// carrega com a migration PeModoLivreValidacao aplicada (lê a tabela pe_orgao_passo_detalhe e
    /// as colunas novas de pe_pdtic_passo); é a marca que liga o modo livre, a forma de cada passo e
    /// a validação da equipe na API (<see cref="PeModoNiveis"/>).
    /// </summary>
    public const int VersaoDoModoLivre = 8;

    /// <summary>
    /// Versão do modelo inicial da revisão dos textos e dos passos (2026-10-20): a trilha do órgão
    /// com os passos juntados (50 para 30 no Avançado) e a aprovação como a última seção do passo que
    /// ela aprova. Num banco com uma versão anterior, a carga reestrutura a trilha
    /// (<see cref="ReestruturarAsync"/>): leva cada seção para o passo dela no JSON, regrava o passo
    /// do sistema (tipo, trava, guia, situação nos níveis e ordem) e a situação das seções, apaga
    /// (exclusão lógica) os passos do sistema que sumiram e leva para o passo novo o que é do órgão
    /// (comentários e o passo dos capítulos do documento) ou descarta (o "não se aplica", a validação,
    /// a forma escolhida e os ajustes do passo que sumiu). Desde ela, toda carga que acha o JSON
    /// mudado (versão nova, ou a mesma versão com outro conteúdo: o resumo fica em
    /// pe_configuracao.seed_modelo_hash) regrava os textos de todos os itens do sistema a partir do
    /// JSON (<see cref="SincronizarTextosAsync"/>).
    /// </summary>
    public const int VersaoDaReestruturacao = 9;

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
                // Desde a versão 9, o "o que fazer" pode ficar vazio (quando o título já diz tudo)
                if (string.IsNullOrWhiteSpace(passo.Titulo)) Falha($"passo \"{passo.Chave}\" sem título.");
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
                    if (secao.PorCiclo != null && !PeDominios.TipoCiclo.Todos.Contains(secao.PorCiclo))
                        Falha($"porCiclo da seção \"{secao.Chave}\" (monitoramento ou avaliacao).");

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
            if (secao.Travada || secao.Inciso != null || secao.PorCiclo != null
                || secao.Niveis.ValueKind is not (JsonValueKind.Undefined or JsonValueKind.Null))
                Falha($"a seção \"{secao.Chave}\" é fora do PDTIC: sem trava, inciso, ciclo nem níveis.");
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
        var hash = Resumo(seed);
        if (versaoAnterior >= seed.Versao)
        {
            // Versão 9: a mesma versão com o JSON mudado (os textos reescritos sem subir a versão):
            // só os textos são regravados. Estrutura nova (item novo, situação, ordem) pede versão nova
            if (versaoAnterior != seed.Versao || seed.Versao < VersaoDaReestruturacao || ResumoGravado(configuracoes) == hash)
                return new PeCarregamentoResultado(versaoAnterior, versaoAnterior, false, 0, 0, 0, 0, 0, 0, 0);
            var mesmaVersao = DateTime.UtcNow;
            var nTextosMesmaVersao = await SincronizarTextosAsync(seed, mesmaVersao, ct);
            GravarResumo(configuracoes, hash, mesmaVersao);
            await _context.SaveChangesAsync(ct);
            if (transacao != null) await transacao.CommitAsync(ct);
            return new PeCarregamentoResultado(versaoAnterior, versaoAnterior, true, 0, 0, 0, 0, 0, 0, 0, Textos: nTextosMesmaVersao);
        }

        // Versão 8 (F3): lê a tabela nova e as colunas novas de pe_pdtic_passo. No intervalo do
        // deploy (o código novo sem a migration), a leitura falha antes de qualquer gravação e o
        // carregador tenta de novo: a marca da versão 8 (o modo livre, a forma de cada passo e a
        // validação da equipe) só entra com a migration aplicada
        if (seed.Versao >= VersaoDoModoLivre)
        {
            await _context.PeOrgaosPassoDetalhe.AsNoTracking().AnyAsync(ct);
            await _context.PePdticPassosValidacao.AsNoTracking().AnyAsync(ct);
        }

        var agora = DateTime.UtcNow;
        int nNiveis = 0, nEtapas = 0, nPassos = 0, nSecoes = 0, nCampos = 0, nOpcoes = 0, nConfig = 0, nCorrecoes = 0;
        var existentesAntes = new List<(PeCampo Campo, PeSeedCampo Seed)>();

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

        // Versão 9: o banco com a trilha de antes passa à trilha do JSON (os passos juntados)
        var nReestruturados = versaoAnterior > 0 && versaoAnterior < VersaoDaReestruturacao && seed.Versao >= VersaoDaReestruturacao
            ? await ReestruturarAsync(seed, nivelPorCodigo, agora, ct)
            : 0;

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
                        OQueFazer = sp.OQueFazer?.Trim() ?? string.Empty,
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
                else
                {
                    nCorrecoes += CorrigirPasso(passo, sp, agora);
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
                else
                {
                    nCorrecoes += CorrigirCampo(campo, sc, agora);
                    existentesAntes.Add((campo, sc));
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

        // Versão 7 (F1): as chaves novas do config nos campos que já existiam (só vindo de antes da 7)
        if (versaoAnterior < VersaoDaRevisaoFinal)
            foreach (var (campo, sc) in existentesAntes)
                nCorrecoes += AcrescentarConfig(campo, sc, agora);

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

        // Seções por ciclo (versão 6, E7 rodada B): a marca entra também na seção que já existia
        // (a coluna pe_secao.por_ciclo é nova, então não há o que o administrador tenha mudado);
        // marca que já existe não muda. Ler as marcas é o que falha no intervalo do deploy (a
        // coluna ainda não existe): nada é gravado e o carregador tenta de novo
        var nPorCiclo = 0;
        var comCiclo = seed.Etapas.SelectMany(e => e.Passos).SelectMany(p => p.Secoes).Where(s => s.PorCiclo != null).ToList();
        if (comCiclo.Count > 0)
        {
            var marcadas = await _context.PeSecoesCiclo.ToListAsync(ct);
            foreach (var ss in comCiclo)
            {
                var secao = secoes.First(s => s.Chave == ss.Chave);
                if (secao.Id != 0 && marcadas.Any(m => m.Id == secao.Id)) continue;
                var marca = new PeSecaoCiclo { Secao = secao, PorCiclo = ss.PorCiclo! };
                if (secao.Id != 0) marca.Id = secao.Id;
                _context.PeSecoesCiclo.Add(marca);
                marcadas.Add(marca);
                nPorCiclo++;
            }
        }

        // Modelo do documento do PDTIC (versão 3, E5): o que falta, achado pelo tipo do modelo e
        // pela chave do capítulo; os blocos entram com o capítulo novo (capítulo que já existe não
        // é tocado, nem os blocos dele)
        var (nDocumentos, nCapitulos, nBlocos, nBlocosCorrigidos) = seed.Versao >= VersaoDoDocumento
            ? await CarregarDocumentosAsync(documentos ?? PeDocSeed.Ler(), passos, secoes, campos, opcoes, agora, ct)
            : (0, 0, 0, 0);
        nCorrecoes += nBlocosCorrigidos;

        // Fluxos do guia (versão 4, E6): os que faltam, achados pela chave; o que já existe
        // (inclusive o que o administrador mudou) não é tocado
        var nFluxos = seed.Versao >= VersaoDosFluxos
            ? await CarregarFluxosAsync(fluxos ?? PeFluxoSeed.Ler(), agora, ct)
            : 0;

        // Versão 9: os textos de todos os itens do sistema, a partir do JSON (no banco que já tinha o
        // modelo; no banco novo, tudo acabou de entrar com os textos do JSON)
        var nTextos = versaoAnterior > 0 && seed.Versao >= VersaoDaReestruturacao
            ? await SincronizarTextosAsync(seed, agora, ct)
            : 0;
        if (seed.Versao >= VersaoDaReestruturacao) GravarResumo(configuracoes, hash, agora);

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
            nRegistros, nDocumentos, nCapitulos, nBlocos, nFluxos, nPorCiclo, nCorrecoes, nReestruturados, nTextos);
    }

    // ── Versão 9: o resumo do JSON, os textos e a reestruturação da trilha ────

    /// <summary>O resumo (SHA-256, em hexadecimal) do modelo inicial: muda com qualquer mudança no JSON.</summary>
    public static string Resumo(PeSeedModelo seed)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(seed, OpcoesDoResumo);
        return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)).ToLowerInvariant();
    }

    private static readonly JsonSerializerOptions OpcoesDoResumo = new() { Converters = { new ElementoDoResumo() } };

    // O "niveis" ausente (seção fora do PDTIC) é um JsonElement vazio, que o serializador não escreve sozinho
    private sealed class ElementoDoResumo : System.Text.Json.Serialization.JsonConverter<JsonElement>
    {
        public override JsonElement Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            JsonElement.ParseValue(ref reader);

        public override void Write(Utf8JsonWriter writer, JsonElement value, JsonSerializerOptions options)
        {
            if (value.ValueKind == JsonValueKind.Undefined) writer.WriteNullValue();
            else value.WriteTo(writer);
        }
    }

    private static string? ResumoGravado(IEnumerable<PeConfiguracao> configuracoes)
    {
        var valor = configuracoes.FirstOrDefault(c => c.Chave == PeConfiguracao.ChaveHashModelo)?.Valor;
        if (string.IsNullOrWhiteSpace(valor)) return null;
        try
        {
            using var documento = JsonDocument.Parse(valor);
            return documento.RootElement.ValueKind == JsonValueKind.String ? documento.RootElement.GetString() : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private void GravarResumo(List<PeConfiguracao> configuracoes, string hash, DateTime agora)
    {
        var valor = JsonSerializer.Serialize(hash);
        var registro = configuracoes.FirstOrDefault(c => c.Chave == PeConfiguracao.ChaveHashModelo);
        if (registro == null)
        {
            registro = new PeConfiguracao { Chave = PeConfiguracao.ChaveHashModelo, Valor = valor, CriadoEm = agora, CriadoPor = Autor };
            _context.PeConfiguracoes.Add(registro);
            configuracoes.Add(registro);
        }
        else if (registro.Valor != valor)
        {
            registro.Valor = valor;
            registro.AlteradoEm = agora;
            registro.AlteradoPor = Autor;
        }
    }

    // Troca o valor quando é diferente; devolve se trocou
    private static bool Trocar<T>(T atual, T novo, Action<T> gravar)
    {
        if (EqualityComparer<T>.Default.Equals(atual, novo)) return false;
        gravar(novo);
        return true;
    }

    private static string? SemVazio(string? texto) => string.IsNullOrWhiteSpace(texto) ? null : texto;

    /// <summary>
    /// Regrava, a partir do JSON, os textos de todos os itens do sistema que o JSON traz (achados pela
    /// chave, como na carga): o nome e a descrição dos níveis; o título, a descrição e a atividade do
    /// guia das etapas; o título, o "o que fazer", a base legal e a atividade do guia dos passos; o
    /// título e a ajuda das seções (as do PDTIC e as de fora dele); o rótulo e a ajuda dos campos; e o
    /// rótulo das opções. Só texto: a estrutura (situação nos níveis, ordem, tipo, config, opção
    /// ativa) não muda aqui. O texto que o administrador mudou também é regravado (pedido de
    /// 2026-10-20: o JSON é a fonte dos textos). Devolve quantos itens mudaram.
    /// </summary>
    public async Task<int> SincronizarTextosAsync(PeSeedModelo seed, DateTime agora, CancellationToken ct = default)
    {
        var niveis = await _context.PeNiveis.ToListAsync(ct);
        var etapas = await _context.PeEtapas.ToListAsync(ct);
        var passos = await _context.PePassos.ToListAsync(ct);
        var secoes = await _context.PeSecoes.ToListAsync(ct);
        var campos = await _context.PeCampos.ToListAsync(ct);
        var opcoes = await _context.PeOpcoes.ToListAsync(ct);
        var mudados = 0;

        void Marcar(IPeAuditavel item, bool mudou)
        {
            if (!mudou) return;
            item.AlteradoEm = agora;
            item.AlteradoPor = Autor;
            mudados++;
        }

        foreach (var sn in seed.Niveis)
            if (niveis.FirstOrDefault(n => n.Codigo == sn.Codigo) is { } nivel)
                Marcar(nivel, Trocar(nivel.Nome, sn.Nome, v => nivel.Nome = v)
                              | Trocar(nivel.Descricao, SemVazio(sn.Descricao), v => nivel.Descricao = v));

        void Secao(PeSeedSecao ss)
        {
            if (secoes.FirstOrDefault(s => s.Chave == ss.Chave) is not { } secao) return;
            Marcar(secao, Trocar(secao.Titulo, ss.Titulo, v => secao.Titulo = v) | Trocar(secao.Ajuda, SemVazio(ss.Ajuda), v => secao.Ajuda = v));
            foreach (var sc in ss.Campos)
            {
                if (campos.FirstOrDefault(c => c.SecaoId == secao.Id && c.Chave == sc.Chave) is not { } campo) continue;
                Marcar(campo, Trocar(campo.Rotulo, sc.Rotulo, v => campo.Rotulo = v) | Trocar(campo.Ajuda, SemVazio(sc.Ajuda), v => campo.Ajuda = v));
                foreach (var so in sc.Opcoes)
                    if (opcoes.FirstOrDefault(o => o.CampoId == campo.Id && o.Valor == so.Valor) is { } opcao)
                        Marcar(opcao, Trocar(opcao.Rotulo, so.Rotulo, v => opcao.Rotulo = v));
            }
        }

        foreach (var se in seed.Etapas)
        {
            if (etapas.FirstOrDefault(e => e.Chave == se.Chave) is { } etapa)
                Marcar(etapa, Trocar(etapa.Titulo, se.Titulo, v => etapa.Titulo = v)
                              | Trocar(etapa.Descricao, SemVazio(se.Descricao), v => etapa.Descricao = v)
                              | Trocar(etapa.ReferenciaGuia, SemVazio(se.ReferenciaGuia), v => etapa.ReferenciaGuia = v));
            foreach (var sp in se.Passos)
            {
                if (passos.FirstOrDefault(p => p.Chave == sp.Chave) is { } passo)
                    Marcar(passo, Trocar(passo.Titulo, sp.Titulo, v => passo.Titulo = v)
                                  | Trocar(passo.OQueFazer, sp.OQueFazer?.Trim() ?? string.Empty, v => passo.OQueFazer = v)
                                  | Trocar(passo.BaseLegal, SemVazio(sp.BaseLegal), v => passo.BaseLegal = v)
                                  | Trocar(passo.ReferenciaGuia, SemVazio(sp.ReferenciaGuia), v => passo.ReferenciaGuia = v));
                foreach (var ss in sp.Secoes) Secao(ss);
            }
        }
        foreach (var ss in seed.SecoesForaDoPdtic) Secao(ss);
        return mudados;
    }

    /// <summary>
    /// A reestruturação da trilha do órgão da versão 9, num banco com uma versão anterior. Deixa a
    /// trilha igual à do JSON: cada seção do PDTIC que o JSON traz vai para o passo dela (na ordem do
    /// JSON) com a situação de cada nível do JSON; cada passo do JSON é regravado (etapa, tipo, trava,
    /// inciso, "não se aplica", ordem e a situação de cada nível; os textos ficam com a
    /// <see cref="SincronizarTextosAsync"/>). Os passos do sistema que o JSON não traz mais saem como
    /// o módulo apaga item (exclusão lógica: ExcluidoEm), e o que é deles vai para o passo que recebeu
    /// as seções deles: as seções que o administrador criou nele, os comentários dos PDTICs e o passo
    /// dos capítulos do documento. O que valia só para o passo que sumiu é descartado: o "não se
    /// aplica" e a validação da equipe (pe_pdtic_passo), a forma escolhida (pe_orgao_passo_detalhe) e
    /// o ajuste do órgão sobre o passo; e sai a validação da equipe dos passos que receberam seções (o
    /// conteúdo deles mudou). Passo criado pelo administrador fica, depois dos do sistema na etapa.
    /// Idempotente: num banco que já está como o JSON, não muda nada. Devolve quantos itens mudaram.
    /// </summary>
    private async Task<int> ReestruturarAsync(PeSeedModelo seed, Dictionary<string, PeNivel> nivelPorCodigo, DateTime agora,
        CancellationToken ct)
    {
        var codigos = seed.Niveis.Select(n => n.Codigo).ToList();
        var etapas = await _context.PeEtapas.ToListAsync(ct);
        var passos = await _context.PePassos.Include(p => p.Niveis).ToListAsync(ct);
        var secoes = await _context.PeSecoes.Include(s => s.Niveis).Where(s => s.Escopo == PeDominios.Escopo.Pdtic).ToListAsync(ct);
        var passoAntes = secoes.ToDictionary(s => s.Id, s => s.PassoId);
        var mudados = new HashSet<IPeAuditavel>();

        void Marcar(IPeAuditavel item, bool mudou)
        {
            if (mudou) mudados.Add(item);
        }

        bool Niveis<TNivel>(List<TNivel> linhas, Dictionary<string, string> situacoes, Func<long, TNivel> nova) where TNivel : IPeSituacaoNivel
        {
            var mudou = false;
            foreach (var (codigo, situacao) in situacoes)
            {
                if (!nivelPorCodigo.TryGetValue(codigo, out var nivel) || nivel.Id == 0) continue;
                var linha = linhas.FirstOrDefault(l => l.NivelId == nivel.Id);
                if (linha == null)
                {
                    var criada = nova(nivel.Id);
                    criada.Situacao = situacao;
                    linhas.Add(criada);
                    mudou = true;
                }
                else if (linha.Situacao != situacao)
                {
                    linha.Situacao = situacao;
                    mudou = true;
                }
            }
            return mudou;
        }

        var doSeed = new HashSet<string>();
        foreach (var se in seed.Etapas)
        {
            var etapa = etapas.FirstOrDefault(e => e.Chave == se.Chave);
            if (etapa == null) continue;
            foreach (var (sp, ip) in se.Passos.Select((p, i) => (p, i)))
            {
                doSeed.Add(sp.Chave);
                var passo = passos.FirstOrDefault(p => p.Chave == sp.Chave);
                if (passo == null) continue;
                Marcar(passo, Trocar(passo.EtapaId, etapa.Id, v => passo.EtapaId = v)
                              | Trocar(passo.Tipo, sp.Tipo, v => passo.Tipo = v)
                              | Trocar(passo.Travado, sp.Travado, v => passo.Travado = v)
                              | Trocar(passo.IncisoDecreto, sp.Inciso, v => passo.IncisoDecreto = v)
                              | Trocar(passo.AceitaNaoSeAplica, sp.AceitaNaoSeAplica, v => passo.AceitaNaoSeAplica = v)
                              | Trocar(passo.Ordem, ip + 1, v => passo.Ordem = v)
                              | Trocar(passo.ExcluidoEm, null, v => passo.ExcluidoEm = v)
                              | Niveis(passo.Niveis, Situacoes(sp.Niveis, codigos, null, sp.Chave),
                                  nivelId => new PePassoNivel { PassoId = passo.Id, NivelId = nivelId }));
                foreach (var (ss, iss) in sp.Secoes.Select((s, i) => (s, i)))
                {
                    var secao = secoes.FirstOrDefault(s => s.Chave == ss.Chave);
                    if (secao == null) continue;
                    Marcar(secao, Trocar(secao.PassoId, passo.Id, v => secao.PassoId = v)
                                  | Trocar(secao.Ordem, iss + 1, v => secao.Ordem = v)
                                  | Niveis(secao.Niveis, Situacoes(ss.Niveis, codigos, null, ss.Chave),
                                      nivelId => new PeSecaoNivel { SecaoId = secao.Id, NivelId = nivelId }));
                }
            }
            // O passo criado pelo administrador fica depois dos do sistema, na ordem de antes
            var ordem = se.Passos.Count;
            foreach (var passo in passos.Where(p => p.EtapaId == etapa.Id && !p.Sistema).OrderBy(p => p.Ordem).ThenBy(p => p.Id))
                Marcar(passo, Trocar(passo.Ordem, ++ordem, v => passo.Ordem = v));
        }

        // Os passos do sistema que sumiram, e o passo que recebeu as seções de cada um
        var sumiram = passos.Where(p => p.Sistema && p.ExcluidoEm == null && !doSeed.Contains(p.Chave)).ToList();
        var destino = new Dictionary<long, PePasso>();
        foreach (var velho in sumiram)
        {
            var novoId = secoes.Where(s => passoAntes[s.Id] == velho.Id && s.PassoId != velho.Id).Select(s => s.PassoId).FirstOrDefault();
            if (novoId is long id && passos.FirstOrDefault(p => p.Id == id) is { } novo) destino[velho.Id] = novo;
            velho.ExcluidoEm = agora;
            mudados.Add(velho);
            // As seções que ficaram nele (criadas pelo administrador) vão junto, depois das do sistema
            if (!destino.TryGetValue(velho.Id, out var alvo)) continue;
            var ordemNoAlvo = secoes.Where(s => s.PassoId == alvo.Id).Select(s => s.Ordem).DefaultIfEmpty(0).Max();
            foreach (var resto in secoes.Where(s => s.PassoId == velho.Id).OrderBy(s => s.Ordem).ThenBy(s => s.Id).ToList())
            {
                resto.PassoId = alvo.Id;
                resto.Ordem = ++ordemNoAlvo;
                mudados.Add(resto);
            }
        }
        foreach (var item in mudados)
        {
            item.AlteradoEm = agora;
            item.AlteradoPor = Autor;
        }

        // Os passos que receberam seções de outro passo (o conteúdo deles mudou)
        var receberam = secoes.Where(s => s.PassoId != null && s.PassoId != passoAntes[s.Id]).Select(s => s.PassoId!.Value).ToHashSet();
        var velhos = sumiram.Select(p => p.Id).ToList();
        if (velhos.Count == 0 && receberam.Count == 0) return mudados.Count;

        // O que é do órgão: os comentários e o passo dos capítulos do documento vão para o passo novo
        foreach (var comentario in await _context.PeComentarios.Where(c => velhos.Contains(c.PassoId)).ToListAsync(ct))
            if (destino.TryGetValue(comentario.PassoId, out var alvo)) comentario.PassoId = alvo.Id;
        var chaves = sumiram.Where(p => destino.ContainsKey(p.Id)).ToDictionary(p => p.Chave, p => destino[p.Id].Chave);
        if (chaves.Count > 0)
        {
            var antigas = chaves.Keys.ToList();
            foreach (var capitulo in await _context.PeDocCapitulos.Where(c => c.PassoChave != null && antigas.Contains(c.PassoChave)).ToListAsync(ct))
            {
                capitulo.PassoChave = chaves[capitulo.PassoChave!];
                capitulo.AlteradoEm = agora;
                capitulo.AlteradoPor = Autor;
            }
        }

        // O que valia só para o passo que sumiu: o "não se aplica", a validação, a forma e o ajuste; e
        // a validação dos passos que receberam seções
        var semValidacao = velhos.Concat(receberam).ToList();
        _context.PePdticPassosValidacao.RemoveRange(await _context.PePdticPassosValidacao.Where(v => semValidacao.Contains(v.PassoId)).ToListAsync(ct));
        _context.PePdticPassos.RemoveRange(await _context.PePdticPassos.Where(m => velhos.Contains(m.PassoId)).ToListAsync(ct));
        _context.PeOrgaosPassoDetalhe.RemoveRange(await _context.PeOrgaosPassoDetalhe.Where(d => velhos.Contains(d.PassoId)).ToListAsync(ct));
        _context.PeOrgaosAjuste.RemoveRange(await _context.PeOrgaosAjuste
            .Where(a => a.AlvoTipo == PeDominios.AlvoAjuste.Passo && velhos.Contains(a.AlvoId)).ToListAsync(ct));
        return mudados.Count;
    }

    // ── Correções da versão 7 (F1) ──────────────────────────────────────────

    /// <summary>Troca o título e o "o que fazer" do passo que ainda são os que o carregador gravou.</summary>
    private static int CorrigirPasso(PePasso passo, PeSeedPasso sp, DateTime agora)
    {
        if (sp.Antes == null || passo.ExcluidoEm != null) return 0;
        var mudou = false;
        if (sp.Antes.Titulo?.Contains(passo.Titulo) == true && passo.Titulo != sp.Titulo)
        {
            passo.Titulo = sp.Titulo;
            mudou = true;
        }
        if (sp.Antes.OQueFazer?.Contains(passo.OQueFazer) == true && passo.OQueFazer != sp.OQueFazer)
        {
            passo.OQueFazer = sp.OQueFazer ?? string.Empty;
            mudou = true;
        }
        if (!mudou) return 0;
        passo.AlteradoEm = agora;
        passo.AlteradoPor = Autor;
        return 1;
    }

    /// <summary>
    /// Troca a ajuda, o config e a largura do campo que ainda são os que o carregador gravou (os
    /// "antes" do JSON); o que o administrador mudou fica.
    /// </summary>
    private static int CorrigirCampo(PeCampo campo, PeSeedCampo sc, DateTime agora)
    {
        if (sc.Antes == null || campo.ExcluidoEm != null) return 0;
        var mudou = false;
        if (sc.Antes.Ajuda != null && campo.Ajuda != null && sc.Antes.Ajuda.Contains(campo.Ajuda) && campo.Ajuda != sc.Ajuda)
        {
            campo.Ajuda = sc.Ajuda;
            mudou = true;
        }
        if (sc.Antes.Largura != null && sc.Antes.Largura.Contains(campo.Largura) && campo.Largura != sc.Largura)
        {
            campo.Largura = sc.Largura;
            mudou = true;
        }
        if (sc.Antes.Config != null && campo.Tipo == sc.Tipo
            && sc.Antes.Config.Any(antigo => Canonico(antigo.GetRawText()) == Canonico(campo.Config)))
        {
            string novo;
            try
            {
                novo = PeConfigCampo.Normalizar(campo.Tipo, sc.Config, new PeContextoConfig { ChaveDoCampo = campo.Chave });
            }
            catch (ApiException ex)
            {
                throw new InvalidOperationException($"Modelo inicial inválido: config do campo \"{campo.Chave}\": {ex.Error.Message}");
            }
            if (Canonico(novo) != Canonico(campo.Config))
            {
                campo.Config = novo;
                mudou = true;
            }
        }
        if (!mudou) return 0;
        campo.AlteradoEm = agora;
        campo.AlteradoPor = Autor;
        return 1;
    }

    /// <summary>O JSON com as chaves dos objetos em ordem (o jsonb do PostgreSQL reordena as chaves).</summary>
    private static string Canonico(string? json)
    {
        static System.Text.Json.Nodes.JsonNode? Ordenar(System.Text.Json.Nodes.JsonNode? no) => no switch
        {
            System.Text.Json.Nodes.JsonObject o => new System.Text.Json.Nodes.JsonObject(o
                .OrderBy(p => p.Key, StringComparer.Ordinal)
                .Select(p => KeyValuePair.Create(p.Key, Ordenar(p.Value)))),
            System.Text.Json.Nodes.JsonArray a => new System.Text.Json.Nodes.JsonArray(a.Select(Ordenar).ToArray()),
            null => null,
            _ => System.Text.Json.Nodes.JsonNode.Parse(no.ToJsonString())
        };
        var raiz = System.Text.Json.Nodes.JsonNode.Parse(string.IsNullOrWhiteSpace(json) ? PeConfigCampo.Vazio : json);
        return Ordenar(raiz)?.ToJsonString() ?? "null";
    }

    /// <summary>
    /// Acrescenta ao config de um campo que já existia as chaves novas da versão 7 que o JSON traz
    /// e que o campo ainda não tem (as outras chaves do config, que o administrador pode ter mudado,
    /// ficam como estão). O config resultante passa pela mesma conferência da API.
    /// </summary>
    private static int AcrescentarConfig(PeCampo campo, PeSeedCampo sc, DateTime agora)
    {
        if (campo.ExcluidoEm != null || campo.Tipo != sc.Tipo || sc.Config is not { ValueKind: JsonValueKind.Object } doSeed) return 0;
        var atual = System.Text.Json.Nodes.JsonNode.Parse(string.IsNullOrWhiteSpace(campo.Config) ? PeConfigCampo.Vazio : campo.Config)
            as System.Text.Json.Nodes.JsonObject ?? new System.Text.Json.Nodes.JsonObject();
        var mudou = false;
        foreach (var propriedade in doSeed.EnumerateObject())
        {
            if (!ChavesNovasDoConfig.Contains(propriedade.Name) || atual.ContainsKey(propriedade.Name)) continue;
            atual[propriedade.Name] = System.Text.Json.Nodes.JsonNode.Parse(propriedade.Value.GetRawText());
            mudou = true;
        }
        if (!mudou) return 0;
        try
        {
            campo.Config = PeConfigCampo.Normalizar(campo.Tipo, JsonSerializer.SerializeToElement(atual),
                new PeContextoConfig { ChaveDoCampo = campo.Chave });
        }
        catch (ApiException ex)
        {
            throw new InvalidOperationException($"Modelo inicial inválido: config do campo \"{campo.Chave}\": {ex.Error.Message}");
        }
        campo.AlteradoEm = agora;
        campo.AlteradoPor = Autor;
        return 1;
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
    private async Task<(int Documentos, int Capitulos, int Blocos, int Corrigidos)> CarregarDocumentosAsync(PeSeedDocumentos documentos,
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
        // Os blocos de texto com "antes" nos capítulos que já existem (versão 7): lidos só quando há
        var comAntes = documentos.Modelos.SelectMany(m => Todos(m.Capitulos)).Any(c => c.Blocos.Any(b => b.Antes is { Count: > 0 }));
        var blocosExistentes = comAntes ? await _context.PeDocBlocos.ToListAsync(ct) : new List<PeDocBloco>();
        var nCorrigidos = 0;

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
                else
                {
                    nCorrigidos += CorrigirBlocos(capitulo, sc, blocosExistentes, contexto, agora);
                }
                foreach (var (sub, i) in sc.Subcapitulos.Select((s, i) => (s, i)))
                    Carregar(sub, capitulo, i + 1, novo);
            }

            foreach (var (sc, i) in sm.Capitulos.Select((c, i) => (c, i)))
                Carregar(sc, null, i + 1, modeloNovo);
        }
        return (nDocumentos, nCapitulos, nBlocos, nCorrigidos);
    }

    private static IEnumerable<PeSeedDocCapitulo> Todos(IEnumerable<PeSeedDocCapitulo> capitulos) =>
        capitulos.SelectMany(c => new[] { c }.Concat(Todos(c.Subcapitulos)));

    /// <summary>
    /// Num capítulo que já existia (versão 7): o bloco de texto da mesma posição cujo texto ainda é
    /// um dos que o carregador gravou antes (os "antes" do JSON) passa ao texto de hoje. O texto que
    /// o administrador mudou fica, e a cópia do órgão (o texto que ele editou) não é tocada.
    /// </summary>
    private static int CorrigirBlocos(PeDocCapitulo capitulo, PeSeedDocCapitulo sc, List<PeDocBloco> blocos, PeDocConfig.Contexto contexto,
        DateTime agora)
    {
        if (capitulo.Id == 0) return 0;
        var corrigidos = 0;
        foreach (var (sb, ib) in sc.Blocos.Select((b, i) => (b, i)))
        {
            if (sb.Antes is not { Count: > 0 } || sb.Tipo != PeDominios.TipoBloco.Texto || sb.Texto == null) continue;
            var bloco = blocos.FirstOrDefault(b => b.CapituloId == capitulo.Id && b.Ordem == ib + 1 && b.ExcluidoEm == null
                                                   && b.Tipo == PeDominios.TipoBloco.Texto);
            if (bloco == null) continue;
            var atual = PeDocMarcadores.Hash(PeDocConfig.TextoDoBloco(PeDocConfig.Ler(bloco.Config)));
            string Normalizado(IEnumerable<string> linhas) =>
                PeDocMarcadores.Hash(PeDocConfig.TextoDoBloco(PeDocConfig.Ler(PeDocConfig.Normalizar(PeDominios.TipoBloco.Texto,
                    new PeSeedDocBloco { Tipo = PeDominios.TipoBloco.Texto, Texto = linhas.ToList() }.Config(), contexto).Json)));
            if (!sb.Antes.Any(antigas => Normalizado(antigas) == atual)) continue;
            bloco.Config = PeDocConfig.Normalizar(sb.Tipo, sb.Config(), contexto).Json;
            bloco.AlteradoEm = agora;
            bloco.AlteradoPor = Autor;
            corrigidos++;
        }
        return corrigidos;
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
