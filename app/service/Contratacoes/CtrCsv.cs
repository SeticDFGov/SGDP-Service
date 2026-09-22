using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using api.Contratacoes;
using Models.Contratacoes;

namespace service.Contratacoes;

/// <summary>
/// Quais das colunas OPCIONAIS (as 4 acrescentadas na rodada da chefia) o arquivo
/// trazia no CABEÇALHO.
///
/// Coluna AUSENTE não é o mesmo que coluna presente e VAZIA: a planilha real da
/// equipe tem 12 colunas e não sabe nada de criticidade, etapa, assinatura ou
/// origem — reimportá-la não pode apagar o que foi digitado na tela (e nem a
/// criticidade que o backfill da migration gravou). Vazia, sim, limpa: é escolha
/// explícita de quem exportou, e é o que mantém o round-trip fiel.
/// </summary>
public class CtrCsvColunasOpcionais
{
    public bool EtapaPlanejamento { get; init; }

    public bool DataAssinaturaContrato { get; init; }

    public bool Criticidade { get; init; }

    public bool Origem { get; init; }

    /// <summary>
    /// As TRÊS colunas de esclarecimento, tratadas como bloco: um CHECK amarra
    /// pedido/descrição/resposta, e herdar só parte delas produziria estado
    /// incoerente. Presente só quando as três estão no cabeçalho.
    /// </summary>
    public bool Esclarecimento { get; init; }

    /// <summary>
    /// As SETE colunas dos critérios de criticidade (art. 11, § 3º, da IN), também como
    /// bloco: presente só quando as sete estão no cabeçalho.
    /// </summary>
    public bool CriteriosCriticidade { get; init; }

    /// <summary>Nenhuma delas (planilha legada de 12 colunas) — o default conservador.</summary>
    public static readonly CtrCsvColunasOpcionais Nenhuma = new();

    /// <summary>Todas (o que o NOSSO export escreve).</summary>
    public static readonly CtrCsvColunasOpcionais Todas = new()
    {
        EtapaPlanejamento = true,
        DataAssinaturaContrato = true,
        Criticidade = true,
        Origem = true,
        Esclarecimento = true,
        CriteriosCriticidade = true
    };
}

/// <summary>
/// Uma linha da planilha, como o parser a leu. Erro != null = linha rejeitada
/// (o service transforma isso em CtrImportacaoLinha com Ação "Rejeitar").
/// </summary>
public class CtrCsvLinha
{
    /// <summary>
    /// Colunas opcionais que o cabeçalho do arquivo trazia. O serviço de importação
    /// usa isto para NÃO tocar, no upsert de processo existente, nos campos cuja
    /// coluna o arquivo nem tem.
    /// </summary>
    public CtrCsvColunasOpcionais ColunasOpcionais { get; set; } = CtrCsvColunasOpcionais.Nenhuma;

    /// <summary>
    /// A célula "Data de Retorno ao Órgão Comunicante" veio VAZIA (nem data nem "-").
    /// Essa coluna EXISTE na planilha legada de 12 colunas, então a proteção por
    /// coluna ausente não a cobre: sem isto, reimportar a planilha da equipe zerava
    /// o "retorno dispensado" e o processo saía de Concluído. Só data explícita
    /// desmarca o flag e só "-" o marca; vazio preserva o que está gravado.
    /// </summary>
    public bool RetornoOrgaoNaoInformado { get; set; }

    /// <summary>Número físico da linha no arquivo (o que o usuário vê no Excel).</summary>
    public int Linha { get; set; }

    public string NumeroProcesso { get; set; } = string.Empty;

    public string OrgaoSigla { get; set; } = string.Empty;

    public string CategoriaObjeto { get; set; } = string.Empty;

    public string? Erro { get; set; }

    /// <summary>Dados interpretados; null quando a linha foi rejeitada.</summary>
    public CtrProcessoCreateDTO? Dados { get; set; }
}

/// <summary>
/// Leitura e escrita do CSV da planilha "Análises de contratações" — puro e testável,
/// sem dependência de banco. Só BCL: nenhum pacote novo.
///
/// Leitura: Windows-1252 (ou UTF-8 quando há BOM), separador ";", aspas com "" como
/// escape, título antes do cabeçalho, colunas por posição.
/// Escrita: UTF-8 COM BOM (o Excel abre sem mojibake), ";", CRLF, sem linha de título.
/// </summary>
public static class CtrCsv
{
    /// <summary>
    /// Cabeçalho do export: os 12 nomes originais da planilha, os 3 da restituição e,
    /// AO FINAL (a ordem já existente é preservada), os 4 da rodada de planejamento e
    /// status no TCDF, os 3 do esclarecimento e os 7 dos critérios de criticidade. Na
    /// importação, da 16ª em diante são opcionais.
    /// </summary>
    public static readonly string[] Cabecalho =
    {
        "Processo", "Orgão", "Sigla", "Complemento / Área", "Objeto", "Categoria do Objeto",
        "Chegada da analise - SGDI", "Chegada da analise- SUBGD", "Chegada da analise- UGTIC",
        "Data de Retorno ao Gab SGDI", "Data de Retorno ao Órgão Comunicante", "Observação",
        "Restituído", "Data da restituição", "Motivo da restituição",
        "Etapa do planejamento", "Assinatura do contrato", "Criticidade", "Origem",
        "Pedido de esclarecimento em", "Esclarecimento solicitado", "Esclarecimento respondido em",
        "Critério I - Alinhamento à EGD/DF", "Critério II - Impacto nos serviços públicos digitais",
        "Critério III - Compartilhamento ou uso corporativo",
        "Critério IV - Impacto na arquitetura, interoperabilidade ou dados",
        "Critério V - Tecnologias emergentes, nuvem ou IA",
        "Critério VI - Riscos de segurança, dados pessoais ou continuidade",
        "Critério VII - Valor estimado no limite do inciso VII"
    };

    /// <summary>As sete colunas dos critérios, na ordem de CtrDominios.CriterioCriticidade.Todos.</summary>
    public static readonly string[] ColunasCriterios = Cabecalho[ColunaCriterioInicial..];

    // Posição das 4 colunas opcionais (leitura é por posição, como o resto)
    private const int ColunaEtapaPlanejamento = 15;
    private const int ColunaDataAssinatura = 16;
    private const int ColunaCriticidade = 17;
    private const int ColunaOrigem = 18;
    private const int ColunaEsclarecimentoSolicitadoEm = 19;
    private const int ColunaEsclarecimentoDescricao = 20;
    private const int ColunaEsclarecimentoRespondidoEm = 21;

    // As sete colunas dos critérios de criticidade, na ordem dos incisos (bloco: as sete ou nenhuma)
    private const int ColunaCriterioInicial = 22;

    private static readonly string[] FormatosData = { "dd/MM/yyyy", "d/M/yyyy", "dd/M/yyyy", "d/MM/yyyy" };

    private static readonly Regex FormatoSei = new(@"^\d{5}-\d{8}/\d{4}-\d{2}$", RegexOptions.Compiled);

    // A planilha real traz "restiuído" (sem o segundo t) numa das duas observações
    // de restituição: o "t" opcional cobre o erro de digitação sem deixar de casar
    // a grafia correta ("restituído"/"restituída"/"restituido").
    private static readonly Regex MencionaRestituicao =
        new(@"restit?u[ií]d[oa]", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Data citada no texto: dd/MM/yyyy ou dd/MM (ano completado depois)
    private static readonly Regex DataNaObservacao =
        new(@"(\d{1,2})/(\d{1,2})(?:/(\d{4}))?", RegexOptions.Compiled);

    // "não restituído" / "não foi restituída": a menção NEGADA não vira restituição
    private static readonly Regex NegacaoAntes =
        new(@"n[ãa]o\s+(foi\s+)?$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Fim da frase, para procurar a data no MESMO período em que a restituição é dita
    private static readonly char[] FimDeFrase = { '.', ';', '\n', '\r' };

    // Caracteres que fazem uma célula virar fórmula no Excel/Sheets (CSV injection)
    private static readonly char[] IniciamFormula = { '=', '+', '-', '@', '\t', '\r' };

    static CtrCsv()
    {
        // Windows-1252 não vem registrado por padrão no .NET 8 (faz parte do runtime,
        // sem pacote novo — só precisa do provider de code pages).
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    // ── Leitura ───────────────────────────────────────────────────────────────

    public static List<CtrCsvLinha> Ler(byte[] conteudo)
    {
        var texto = Decodificar(conteudo);
        var registros = Tokenizar(texto);

        var linhas = new List<CtrCsvLinha>();
        var numerosVistos = new Dictionary<string, int>();
        var cabecalhoEncontrado = false;
        var colunas = CtrCsvColunasOpcionais.Nenhuma;

        foreach (var (numeroLinha, campos) in registros)
        {
            if (campos.All(string.IsNullOrWhiteSpace)) continue;

            if (!cabecalhoEncontrado)
            {
                // Cabeçalho = primeira linha cuja primeira célula é "processo"
                if (Normalizar(campos[0]) == "processo")
                {
                    cabecalhoEncontrado = true;
                    // É o CABEÇALHO que diz quais colunas o arquivo tem: uma linha de
                    // dados com lixo depois da última coluna não inventa coluna nova
                    colunas = new CtrCsvColunasOpcionais
                    {
                        EtapaPlanejamento = TemColuna(campos, ColunaEtapaPlanejamento),
                        DataAssinaturaContrato = TemColuna(campos, ColunaDataAssinatura),
                        Criticidade = TemColuna(campos, ColunaCriticidade),
                        Origem = TemColuna(campos, ColunaOrigem),
                        // Bloco: as três juntas ou nenhuma (o CHECK amarra os campos)
                        Esclarecimento = TemColuna(campos, ColunaEsclarecimentoSolicitadoEm)
                            && TemColuna(campos, ColunaEsclarecimentoDescricao)
                            && TemColuna(campos, ColunaEsclarecimentoRespondidoEm),
                        // Também bloco: uma resposta solta não classifica nada
                        CriteriosCriticidade = CtrDominios.CriterioCriticidade.Todos
                            .Select((_, i) => TemColuna(campos, ColunaCriterioInicial + i))
                            .All(tem => tem)
                    };
                }
                continue; // o título (linha 1) e o próprio cabeçalho não viram dados
            }

            linhas.Add(LerLinha(numeroLinha, campos, numerosVistos, colunas));
        }

        return linhas;
    }

    /// <summary>Coluna existe no cabeçalho quando há célula na posição e ela não é vazia.</summary>
    private static bool TemColuna(List<string> cabecalho, int indice) =>
        indice < cabecalho.Count && !string.IsNullOrWhiteSpace(cabecalho[indice]);

    private static CtrCsvLinha LerLinha(int numeroLinha, List<string> campos,
        Dictionary<string, int> numerosVistos, CtrCsvColunasOpcionais colunas)
    {
        string Campo(int indice) => indice < campos.Count ? campos[indice].Trim() : string.Empty;
        // Colunas de TEXTO LIVRE: desfaz o apóstrofo que o nosso export põe na frente
        // de célula que começaria com fórmula (round-trip fiel)
        string Texto(int indice) => RemoverApostrofoDeFormula(Campo(indice));

        var numero = Campo(0);
        var sigla = Texto(2).ToUpperInvariant();
        var categoriaBruta = Campo(5);

        var linha = new CtrCsvLinha
        {
            Linha = numeroLinha,
            NumeroProcesso = numero,
            OrgaoSigla = sigla,
            CategoriaObjeto = categoriaBruta,
            ColunasOpcionais = colunas
        };

        if (string.IsNullOrWhiteSpace(numero))
            return Rejeitar(linha, "número do processo ausente");

        if (!FormatoSei.IsMatch(numero))
            return Rejeitar(linha, $"número de processo inválido: {numero}");

        if (numerosVistos.TryGetValue(numero, out var linhaAnterior))
            return Rejeitar(linha, $"duplicado no arquivo, linha {linhaAnterior}");

        var categoria = ResolverCategoria(categoriaBruta);
        if (categoria == null)
            return Rejeitar(linha, $"categoria desconhecida: {categoriaBruta}");
        linha.CategoriaObjeto = categoria;

        // Datas: célula vazia ou "-" viram nulo; "-" na coluna da UGTIC marca "não se aplica"
        if (!TentarData(Campo(6), out var chegadaSgdi, out _))
            return Rejeitar(linha, $"data inválida em Chegada SGDI: {Campo(6)}");
        if (!TentarData(Campo(7), out var chegadaSubgd, out _))
            return Rejeitar(linha, $"data inválida em Chegada SUBGD: {Campo(7)}");
        if (!TentarData(Campo(8), out var chegadaUgtic, out var ugticNaoSeAplica))
            return Rejeitar(linha, $"data inválida em Chegada UGTIC: {Campo(8)}");
        if (!TentarData(Campo(9), out var retornoGab, out _))
            return Rejeitar(linha, $"data inválida em Retorno ao Gab SGDI: {Campo(9)}");
        // "-" aqui passou a significar "não se aplica", como já valia na coluna da UGTIC.
        // Célula VAZIA é um terceiro estado ("não informado"): ver RetornoOrgaoNaoInformado.
        if (!TentarData(Campo(10), out var retornoOrgao, out var retornoOrgaoNaoSeAplica))
            return Rejeitar(linha, $"data inválida em Retorno ao Órgão Comunicante: {Campo(10)}");
        linha.RetornoOrgaoNaoInformado = retornoOrgao == null && !retornoOrgaoNaoSeAplica;

        var observacao = Texto(11);

        // ── Colunas 16-19 (opcionais) ─────────────────────────────────────────
        // Coluna AUSENTE do cabeçalho nem é lida: o valor fica no default e quem
        // decide o que fazer com ele é o serviço de importação (processo existente
        // PRESERVA o que está no banco; processo novo nasce com o default).
        // Coluna presente e vazia = limpar; presente e fora do domínio = rejeitar.
        var etapaBruta = colunas.EtapaPlanejamento ? Campo(ColunaEtapaPlanejamento) : string.Empty;
        string? etapaPlanejamento = null;
        if (!string.IsNullOrWhiteSpace(etapaBruta))
        {
            etapaPlanejamento = CtrDominios.EtapaPlanejamento.Todos
                .FirstOrDefault(e => Normalizar(e) == Normalizar(etapaBruta));
            if (etapaPlanejamento == null)
                return Rejeitar(linha, $"etapa do planejamento desconhecida: {etapaBruta}");
        }

        var assinaturaBruta = colunas.DataAssinaturaContrato ? Campo(ColunaDataAssinatura) : string.Empty;
        // Não existe "não se aplica" para assinatura: o "-" aqui é erro de preenchimento
        if (!TentarData(assinaturaBruta, out var dataAssinatura, out var assinaturaNaoSeAplica)
            || assinaturaNaoSeAplica)
            return Rejeitar(linha, $"data inválida em Assinatura do contrato: {assinaturaBruta}");

        var criticidadeBruta = colunas.Criticidade ? Campo(ColunaCriticidade) : string.Empty;
        string? criticidade = null;
        if (!string.IsNullOrWhiteSpace(criticidadeBruta))
        {
            criticidade = CtrDominios.Criticidade.Todos
                .FirstOrDefault(c => Normalizar(c) == Normalizar(criticidadeBruta));
            if (criticidade == null)
                return Rejeitar(linha, $"criticidade desconhecida: {criticidadeBruta}");
        }

        var origemBruta = colunas.Origem ? Campo(ColunaOrigem) : string.Empty;
        var origem = CtrDominios.Origem.OrgaoComunicante;
        if (!string.IsNullOrWhiteSpace(origemBruta))
        {
            var achada = CtrDominios.Origem.Todos.FirstOrDefault(o => Normalizar(o) == Normalizar(origemBruta));
            if (achada == null)
                return Rejeitar(linha, $"origem desconhecida: {origemBruta}");
            origem = achada;
        }

        // ── Colunas 20-22 (bloco do esclarecimento) ───────────────────────────
        DateOnly? esclarecimentoSolicitadoEm = null;
        string? esclarecimentoDescricao = null;
        DateOnly? esclarecimentoRespondidoEm = null;
        if (colunas.Esclarecimento)
        {
            if (!TentarData(Campo(ColunaEsclarecimentoSolicitadoEm), out esclarecimentoSolicitadoEm, out var naoAplicaPedido)
                || naoAplicaPedido)
                return Rejeitar(linha,
                    $"data inválida em Pedido de esclarecimento em: {Campo(ColunaEsclarecimentoSolicitadoEm)}");

            var descricao = Texto(ColunaEsclarecimentoDescricao);
            esclarecimentoDescricao = string.IsNullOrWhiteSpace(descricao) ? null : descricao;

            if (!TentarData(Campo(ColunaEsclarecimentoRespondidoEm), out esclarecimentoRespondidoEm, out var naoAplicaResposta)
                || naoAplicaResposta)
                return Rejeitar(linha,
                    $"data inválida em Esclarecimento respondido em: {Campo(ColunaEsclarecimentoRespondidoEm)}");
        }

        // ── Colunas 23-29 (bloco dos critérios de criticidade) ────────────────
        // As sete células vazias = critérios não avaliados (a coluna Criticidade decide,
        // como antes). Qualquer resposta preenchida avalia o bloco: as ausentes recebem a
        // resposta padrão e a criticidade passa a ser DERIVADA das respostas.
        Dictionary<string, string>? criterios = null;
        if (colunas.CriteriosCriticidade)
        {
            var respostas = new Dictionary<string, string>();
            for (var i = 0; i < CtrDominios.CriterioCriticidade.Todos.Length; i++)
            {
                var codigo = CtrDominios.CriterioCriticidade.Todos[i];
                var bruta = Campo(ColunaCriterioInicial + i);
                if (string.IsNullOrWhiteSpace(bruta)) continue;

                var resposta = CtrDominios.CriterioCriticidade.RespostasDe(codigo)
                    .FirstOrDefault(r => Normalizar(r) == Normalizar(bruta));
                if (resposta == null)
                    return Rejeitar(linha, $"resposta inválida em {Cabecalho[ColunaCriterioInicial + i]}: {bruta}");
                respostas[codigo] = resposta;
            }

            if (respostas.Count > 0) criterios = CtrCriticidade.Normalizar(respostas);
        }

        // Criticidade digitada que não confere com os critérios é RECUSADA, não corrigida em
        // silêncio: com respostas a coluna é derivada, e a divergência só nasce de edição à
        // mão da planilha
        if (criterios != null && criticidade != null)
        {
            var calculada = CtrCriticidade.Calcular(criterios);
            if (calculada != criticidade)
                return Rejeitar(linha, $"criticidade {criticidade} não confere com os critérios (pelas respostas seria "
                    + $"{calculada}); ajuste as colunas dos critérios ou deixe a Criticidade vazia");
        }

        var dados = new CtrProcessoCreateDTO
        {
            NumeroProcesso = numero,
            OrgaoNome = Texto(1),
            OrgaoSigla = sigla,
            ComplementoArea = string.IsNullOrWhiteSpace(Texto(3)) ? null : Texto(3),
            Objeto = Texto(4),
            CategoriaObjeto = categoria,
            ChegadaSgdi = chegadaSgdi,
            ChegadaSubgd = chegadaSubgd,
            ChegadaUgtic = chegadaUgtic,
            UgticNaoSeAplica = ugticNaoSeAplica,
            RetornoGabSgdi = retornoGab,
            RetornoOrgao = retornoOrgao,
            RetornoOrgaoNaoSeAplica = retornoOrgaoNaoSeAplica,
            Observacao = string.IsNullOrWhiteSpace(observacao) ? null : observacao,
            EtapaPlanejamento = etapaPlanejamento,
            DataAssinaturaContrato = dataAssinatura,
            Criticidade = criticidade,
            CriteriosCriticidade = criterios,
            Origem = origem,
            EsclarecimentoSolicitadoEm = esclarecimentoSolicitadoEm,
            EsclarecimentoDescricao = esclarecimentoDescricao,
            EsclarecimentoRespondidoEm = esclarecimentoRespondidoEm
        };

        var restituido = Campo(12);
        if (!string.IsNullOrWhiteSpace(restituido))
        {
            // Colunas 13-15 (o que o NOSSO export escreve): a informação é explícita
            var marcado = Normalizar(restituido);
            if (marcado is not ("sim" or "nao"))
                return Rejeitar(linha, $"valor inválido em Restituído: {restituido}");

            if (marcado == "sim")
            {
                if (!TentarData(Campo(13), out var restituidoEm, out _) || restituidoEm == null)
                    return Rejeitar(linha, "restituição sem data");

                dados.Restituido = true;
                dados.RestituidoEm = restituidoEm;
                dados.RestituidoMotivo = string.IsNullOrWhiteSpace(Texto(14)) ? observacao : Texto(14);

                if (string.IsNullOrWhiteSpace(dados.RestituidoMotivo))
                    return Rejeitar(linha, "restituição sem motivo");
            }
        }
        else if (!string.IsNullOrWhiteSpace(observacao))
        {
            // Planilha legada: a restituição vive espremida na Observação
            var mencao = MencaoDeRestituicao(observacao);
            if (mencao != null)
            {
                // A data preferida é a da MESMA frase em que a restituição é dita;
                // sem ela, cai para a chegada à SGDI ou a última data preenchida
                var data = DataDaFrase(observacao, mencao.Index, chegadaSgdi)
                    ?? chegadaSgdi
                    ?? CtrProcessoService.CalcularUltimaMovimentacao(
                        chegadaSgdi, chegadaSubgd, chegadaUgtic, retornoGab, retornoOrgao);

                if (data == null)
                    return Rejeitar(linha, "restituição sem data");

                dados.Restituido = true;
                dados.RestituidoEm = data;
                dados.RestituidoMotivo = observacao; // a observação inteira vira o motivo
            }
        }

        // Só uma linha que chegou íntegra "ocupa" o número: se a primeira ocorrência
        // foi rejeitada, a segunda ainda pode entrar (senão as duas se perderiam)
        numerosVistos[numero] = numeroLinha;

        linha.Dados = dados;
        return linha;
    }

    private static CtrCsvLinha Rejeitar(CtrCsvLinha linha, string motivo)
    {
        linha.Erro = motivo;
        linha.Dados = null;
        return linha;
    }

    /// <summary>
    /// BOM UTF-8 é atalho; sem ele, tenta UTF-8 ESTRITO (lança em byte inválido) e
    /// só então cai para Windows-1252. Texto 1252 com acento praticamente nunca é
    /// UTF-8 válido, então a tentativa é segura — e faz o CSV do Google Sheets /
    /// LibreOffice (UTF-8 sem BOM) entrar sem mojibake.
    /// </summary>
    private static string Decodificar(byte[] conteudo)
    {
        if (conteudo.Length >= 3 && conteudo[0] == 0xEF && conteudo[1] == 0xBB && conteudo[2] == 0xBF)
            return Encoding.UTF8.GetString(conteudo, 3, conteudo.Length - 3);

        try
        {
            return new UTF8Encoding(false, true).GetString(conteudo);
        }
        catch (ArgumentException)
        {
            // DecoderFallbackException: não é UTF-8 — é a planilha em Windows-1252
            return Encoding.GetEncoding(1252).GetString(conteudo);
        }
    }

    /// <summary>
    /// Quebra o texto em registros de campos, respeitando aspas (com "" como escape),
    /// ";" e quebras de linha dentro das aspas. Devolve a linha física em que cada
    /// registro começa.
    /// </summary>
    private static List<(int Linha, List<string> Campos)> Tokenizar(string texto)
    {
        var registros = new List<(int, List<string>)>();
        var campos = new List<string>();
        var campo = new StringBuilder();
        var emAspas = false;
        var linhaAtual = 1;
        var linhaRegistro = 1;

        for (var i = 0; i < texto.Length; i++)
        {
            var c = texto[i];

            if (emAspas)
            {
                if (c == '"')
                {
                    if (i + 1 < texto.Length && texto[i + 1] == '"')
                    {
                        campo.Append('"');
                        i++;
                    }
                    else
                    {
                        emAspas = false;
                    }
                }
                else
                {
                    // Quebra de linha DENTRO das aspas faz parte do campo
                    if (c == '\n') linhaAtual++;
                    campo.Append(c);
                }
                continue;
            }

            switch (c)
            {
                case '"':
                    // RFC 4180: aspas só ABREM o campo quando ele ainda está vazio.
                    // No meio do texto são literais — senão uma polegada (Rack 24")
                    // engoliria todas as linhas seguintes num campo só, sem erro.
                    if (campo.Length == 0) emAspas = true;
                    else campo.Append('"');
                    break;
                case ';':
                    campos.Add(campo.ToString());
                    campo.Clear();
                    break;
                case '\r':
                    break; // tratado junto do \n
                case '\n':
                    campos.Add(campo.ToString());
                    campo.Clear();
                    registros.Add((linhaRegistro, campos));
                    campos = new List<string>();
                    linhaAtual++;
                    linhaRegistro = linhaAtual;
                    break;
                default:
                    campo.Append(c);
                    break;
            }
        }

        if (campo.Length > 0 || campos.Count > 0)
        {
            campos.Add(campo.ToString());
            registros.Add((linhaRegistro, campos));
        }

        return registros;
    }

    /// <summary>
    /// Data da célula: vazia -> null; "-" ou "–" -> null com naoSeAplica; formato
    /// dd/MM/yyyy (ou d/M/yyyy). Retorna false quando a célula não é data nenhuma.
    /// </summary>
    private static bool TentarData(string valor, out DateOnly? data, out bool naoSeAplica)
    {
        data = null;
        naoSeAplica = false;

        var texto = valor.Trim();
        if (texto.Length == 0) return true;

        if (texto is "-" or "–" or "—")
        {
            naoSeAplica = true;
            return true;
        }

        if (DateOnly.TryParseExact(texto, FormatosData, CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var lida))
        {
            data = lida;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Primeira menção de restituição que NÃO esteja negada ("não restituído",
    /// "não foi restituída"); null quando só há menções negadas ou nenhuma.
    /// </summary>
    private static Match? MencaoDeRestituicao(string observacao)
    {
        foreach (Match mencao in MencionaRestituicao.Matches(observacao))
        {
            if (NegacaoAntes.IsMatch(observacao[..mencao.Index])) continue;
            return mencao;
        }

        return null;
    }

    /// <summary>
    /// Data citada na MESMA frase da menção (até o ponto/ponto e vírgula seguinte):
    /// é ela que fala da restituição, não uma data solta de outra frase. Sem ano,
    /// completa com o ano da chegada à SGDI (ou o corrente) e, se isso cair no
    /// futuro, usa o ano anterior — "restituído em 04/12" num processo de fevereiro
    /// é dezembro do ano passado, não do que vem.
    /// </summary>
    private static DateOnly? DataDaFrase(string observacao, int indiceMencao, DateOnly? chegadaSgdi)
    {
        var inicio = observacao.LastIndexOfAny(FimDeFrase, Math.Max(0, indiceMencao - 1)) + 1;
        var fim = observacao.IndexOfAny(FimDeFrase, indiceMencao);
        if (fim < 0) fim = observacao.Length;
        if (inicio >= fim) return null;

        var match = DataNaObservacao.Match(observacao[inicio..fim]);
        if (!match.Success) return null;

        var dia = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
        var mes = int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
        var temAno = match.Groups[3].Success;
        var ano = temAno
            ? int.Parse(match.Groups[3].Value, CultureInfo.InvariantCulture)
            : chegadaSgdi?.Year ?? CtrProcessoService.HojeBrasilia().Year;

        if (mes is < 1 or > 12 || dia < 1 || dia > DateTime.DaysInMonth(ano, mes)) return null;

        var data = new DateOnly(ano, mes, dia);
        if (!temAno && data > CtrProcessoService.HojeBrasilia())
        {
            var anterior = ano - 1;
            if (dia > DateTime.DaysInMonth(anterior, mes)) return null;
            data = new DateOnly(anterior, mes, dia);
        }

        return data;
    }

    /// <summary>
    /// Desfaz a proteção contra fórmula: tira UM apóstrofo inicial quando ele está
    /// justamente na frente de um caractere que iniciaria fórmula. Texto que começa
    /// legitimamente com apóstrofo fica intacto.
    /// </summary>
    private static string RemoverApostrofoDeFormula(string valor)
    {
        if (valor.Length >= 2 && valor[0] == '\'' && IniciamFormula.Contains(valor[1]))
            return valor[1..];

        return valor;
    }

    /// <summary>
    /// Categoria por normalização (trim, caixa, acentos, espaços múltiplos).
    /// Vazia vira "Sem objeto / Indefinido"; fora do domínio devolve null (rejeita).
    /// </summary>
    public static string? ResolverCategoria(string valor)
    {
        if (string.IsNullOrWhiteSpace(valor)) return CtrDominios.CategoriaObjeto.SemObjeto;

        var alvo = Normalizar(valor);
        return CtrDominios.CategoriaObjeto.Todos.FirstOrDefault(c => Normalizar(c) == alvo);
    }

    /// <summary>Trim + minúsculas + sem acentos + espaços colapsados.</summary>
    public static string Normalizar(string valor)
    {
        var semAcento = new StringBuilder();
        foreach (var c in valor.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                semAcento.Append(c);
        }

        return Regex.Replace(semAcento.ToString().Normalize(NormalizationForm.FormC), @"\s+", " ");
    }

    // ── Escrita ───────────────────────────────────────────────────────────────

    /// <summary>
    /// CSV da listagem: UTF-8 com BOM, ";", CRLF, sem linha de título. As colunas
    /// são as da planilha original + as três da restituição, para o arquivo poder
    /// ser reimportado sem perda (round-trip).
    /// </summary>
    public static byte[] Escrever(IEnumerable<CtrProcessoResponse> processos)
    {
        var sb = new StringBuilder();
        sb.Append(string.Join(";", Cabecalho)).Append("\r\n");

        foreach (var p in processos)
        {
            // true = coluna de TEXTO LIVRE, que recebe a proteção contra fórmula.
            // Datas, o "-" da UGTIC e o Sim/Não NÃO são neutralizados: o apóstrofo
            // atrapalharia a releitura e essas colunas não vêm do teclado do usuário.
            var celulas = new (string Valor, bool TextoLivre)[]
            {
                (p.NumeroProcesso, false),
                (p.OrgaoNome, true),
                (p.OrgaoSigla, true),
                (p.ComplementoArea ?? string.Empty, true),
                (p.Objeto, true),
                (p.CategoriaObjeto, false),
                (Data(p.ChegadaSgdi), false),
                (Data(p.ChegadaSubgd), false),
                (p.UgticNaoSeAplica ? "-" : Data(p.ChegadaUgtic), false),
                (Data(p.RetornoGabSgdi), false),
                // "-" = não se aplica, a mesma semântica já usada na coluna da UGTIC
                (p.RetornoOrgaoNaoSeAplica ? "-" : Data(p.RetornoOrgao), false),
                (p.Observacao ?? string.Empty, true),
                (p.Restituido ? "Sim" : "Não", false),
                (Data(p.RestituidoEm), false),
                (p.RestituidoMotivo ?? string.Empty, true),
                (p.EtapaPlanejamento ?? string.Empty, false),
                (Data(p.DataAssinaturaContrato), false),
                (p.Criticidade ?? string.Empty, false),
                (p.Origem, false),
                (Data(p.EsclarecimentoSolicitadoEm), false),
                (p.EsclarecimentoDescricao ?? string.Empty, true),
                (Data(p.EsclarecimentoRespondidoEm), false),
                // Os sete critérios: vazios quando o processo ainda não os tem avaliados
                (Criterio(p, CtrDominios.CriterioCriticidade.AlinhamentoEgd), false),
                (Criterio(p, CtrDominios.CriterioCriticidade.ImpactoServicos), false),
                (Criterio(p, CtrDominios.CriterioCriticidade.Compartilhamento), false),
                (Criterio(p, CtrDominios.CriterioCriticidade.ImpactoArquitetura), false),
                (Criterio(p, CtrDominios.CriterioCriticidade.TecnologiasEmergentes), false),
                (Criterio(p, CtrDominios.CriterioCriticidade.RiscosSeguranca), false),
                (Criterio(p, CtrDominios.CriterioCriticidade.ValorEstimado), false)
            };

            sb.Append(string.Join(";", celulas.Select(c => Escapar(c.Valor, c.TextoLivre)))).Append("\r\n");
        }

        // BOM explícito: é ele que faz o Excel abrir o arquivo em UTF-8
        return Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
    }

    private static string Data(DateOnly? data) => data?.ToString("dd/MM/yyyy") ?? string.Empty;

    private static string Criterio(CtrProcessoResponse p, string codigo) =>
        p.CriteriosCriticidade != null && p.CriteriosCriticidade.TryGetValue(codigo, out var resposta)
            ? resposta
            : string.Empty;

    private static string Escapar(string valor, bool textoLivre = false)
    {
        // CSV injection: célula de texto livre começando com =, +, -, @ (ou TAB/CR)
        // vira fórmula ao abrir no Excel/Sheets. O apóstrofo à frente a mantém texto
        // e a importação o remove de volta, para o round-trip ficar fiel.
        if (textoLivre && valor.Length > 0 && IniciamFormula.Contains(valor[0]))
            valor = "'" + valor;

        if (valor.Contains(';') || valor.Contains('"') || valor.Contains('\n') || valor.Contains('\r'))
            return "\"" + valor.Replace("\"", "\"\"") + "\"";

        return valor;
    }
}
