using System.Globalization;
using System.Text.Json;
using api.Planejamento;
using demanda_service.Helpers;
using Microsoft.EntityFrameworkCore;
using Models;
using Models.Pgia;
using Models.Planejamento;
using service.Interface;

namespace service.Planejamento;

/// <summary>
/// Planilhas do PETIC-DF, do catálogo do DF e, desde a E4, do PDTIC de cada órgão e do
/// consolidado de todos os órgãos (plano, seção 11). Colunas: o código (tabela com prefixo),
/// depois os campos visíveis e marcados "na planilha", na ordem do modelo, com os rótulos
/// configurados; ligações mostram os códigos dos registros ligados. No PDTIC, os campos
/// visíveis são os do nível do órgão e dos ajustes; no consolidado, a união dos campos
/// visíveis em qualquer nível ou para qualquer órgão, depois das colunas do órgão.
/// <list type="bullet">
/// <item>CSV: pelo escritor comum (UTF-8 com BOM, ";", CRLF, proteção contra fórmula no
/// texto digitado); números com vírgula e sem milhar, datas em dd/mm/aaaa, listas pelo rótulo;</item>
/// <item>XLSX (Open XML SDK): uma aba por seção, cabeçalho destacado e fixo, filtro, datas e
/// valores como números com formato, listas e sim ou não com validação, e a aba Leia-me
/// (dono, versão, data da extração e a ajuda de cada coluna).</item>
/// </list>
/// Nome do arquivo: PETIC-DF_versao_secao_aaaa-mm-dd (a completa leva "completa" no lugar da
/// seção), DF_secao_aaaa-mm-dd, PDTIC_SIGLA_secao_aaaa-mm-dd e PDTIC_consolidado_secao_aaaa-mm-dd.
/// </summary>
public class PePlanilhaService : IPePlanilhaService
{
    public const string MimeCsv = "text/csv; charset=utf-8";
    public const string MimeXlsx = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    private const string Csv = "csv";
    private const string Xlsx = "xlsx";
    private const string Completa = "completa";

    private readonly AppDbContext _context;
    private readonly IPeRegistroService _registros;
    private readonly IPePermissionService _permissoes;

    public PePlanilhaService(AppDbContext context, IPeRegistroService registros, IPePermissionService permissoes)
    {
        _context = context;
        _registros = registros;
        _permissoes = permissoes;
    }

    public async Task<PePlanilhaArquivo> PeticSecaoAsync(long peticId, string secaoChave, string? formato, PeUserContext ctx)
    {
        var tipo = Formato(formato, completa: false);
        var secao = await _registros.ExportarSecaoAsync(PeDono.DoPetic(peticId), secaoChave, ctx);
        var petic = await _context.PePetics.AsNoTracking().FirstAsync(p => p.Id == peticId);
        var nome = $"PETIC-DF_{petic.Versao}_{secao.Modelo.Secao.Chave}_{Hoje()}.{tipo}";
        return tipo == Csv
            ? new PePlanilhaArquivo(GerarCsv(secao), MimeCsv, nome)
            : new PePlanilhaArquivo(PeXlsx.Gerar(new[] { Aba(secao) }, LeiaMeDoPetic(petic)), MimeXlsx, nome);
    }

    public async Task<PePlanilhaArquivo> PeticCompletaAsync(long peticId, string? formato, PeUserContext ctx)
    {
        Formato(formato, completa: true);
        var secoes = await _registros.ExportarSecoesAsync(PeDono.DoPetic(peticId), ctx);
        var petic = await _context.PePetics.AsNoTracking().FirstAsync(p => p.Id == peticId);
        var nome = $"PETIC-DF_{petic.Versao}_{Completa}_{Hoje()}.{Xlsx}";
        return new PePlanilhaArquivo(PeXlsx.Gerar(secoes.Select(Aba).ToList(), LeiaMeDoPetic(petic)), MimeXlsx, nome);
    }

    // ── PDTIC de um órgão (E4) ──────────────────────────────────────────────

    public async Task<PePlanilhaArquivo> PdticSecaoAsync(long pdticId, string secaoChave, string? formato, PeUserContext ctx)
    {
        var tipo = Formato(formato, completa: false);
        // O motor confere quem chama e a seção no nível do órgão
        var secao = await _registros.ExportarSecaoAsync(PeDono.DoPdtic(pdticId), secaoChave, ctx);
        var (pdtic, orgao, nivel) = await CabecalhoDoPdticAsync(pdticId);
        var nome = $"PDTIC_{NomeSeguro(orgao.Sigla)}_{secao.Modelo.Secao.Chave}_{Hoje()}.{tipo}";
        return tipo == Csv
            ? new PePlanilhaArquivo(GerarCsv(secao), MimeCsv, nome)
            : new PePlanilhaArquivo(PeXlsx.Gerar(new[] { Aba(secao) }, LeiaMeDoPdtic(pdtic, orgao, nivel)), MimeXlsx, nome);
    }

    public async Task<PePlanilhaArquivo> PdticCompletaAsync(long pdticId, string? formato, PeUserContext ctx)
    {
        Formato(formato, completa: true);
        var secoes = await _registros.ExportarSecoesAsync(PeDono.DoPdtic(pdticId), ctx);
        var (pdtic, orgao, nivel) = await CabecalhoDoPdticAsync(pdticId);
        var nome = $"PDTIC_{NomeSeguro(orgao.Sigla)}_{Completa}_{Hoje()}.{Xlsx}";
        return new PePlanilhaArquivo(PeXlsx.Gerar(secoes.Select(Aba).ToList(), LeiaMeDoPdtic(pdtic, orgao, nivel)), MimeXlsx, nome);
    }

    private async Task<(PePdtic Pdtic, PgiaOrgao Orgao, PeNivel? Nivel)> CabecalhoDoPdticAsync(long pdticId)
    {
        var pdtic = await _context.PePdtics.AsNoTracking().FirstAsync(p => p.Id == pdticId);
        var orgao = await _context.PgiaOrgaos.AsNoTracking().FirstAsync(o => o.Id == pdtic.OrgaoId);
        var niveis = await PeTrilhaOrgao.NiveisDosOrgaosAsync(_context, new[] { orgao.Id });
        return (pdtic, orgao, niveis.GetValueOrDefault(orgao.Id));
    }

    private static IReadOnlyList<(string, string)> LeiaMeDoPdtic(PePdtic pdtic, PgiaOrgao orgao, PeNivel? nivel)
    {
        var itens = new List<(string, string)>
        {
            ("Órgão", $"{orgao.Sigla} · {orgao.Nome}"),
            ("PDTIC", $"Plano Diretor de Tecnologia da Informação e Comunicação, versão {pdtic.Versao} ({PeDominios.SituacaoPdtic.Rotulo(pdtic.Situacao).ToLowerInvariant()})"),
            ("Nível de maturidade", nivel?.Nome ?? "-"),
            ("Vigência", pdtic.VigenciaInicio != null && pdtic.VigenciaFim != null
                ? $"{PeFormato.Data(pdtic.VigenciaInicio.Value)} a {PeFormato.Data(pdtic.VigenciaFim.Value)}"
                : "-"),
            ("Extraído em", Agora()),
            ("Como ler", "Cada aba é uma seção do PDTIC, com as colunas do nível do órgão. A coluna Código identifica cada registro "
                         + "(por exemplo, N01), e as colunas de ligação mostram os códigos dos registros ligados, para cruzar as abas. "
                         + "Datas e valores estão como números.")
        };
        return itens;
    }

    // ── Consolidado de todos os órgãos (E4) ─────────────────────────────────

    /// <summary>Os PDTICs atuais com o órgão, o nível e a trilha de cada um, e a trilha de cada nível.</summary>
    private sealed record PeBaseConsolidada(
        PeModeloDados Dados,
        List<(PePdtic Pdtic, PeTrilhaOrgao Trilha)> Pdtics,
        List<List<PeTrilhaEtapa>> TrilhasDosNiveis);

    /// <summary>Uma seção consolidada: as colunas de campo (a união) e as linhas de cada órgão.</summary>
    private sealed record PeSecaoConsolidada(
        PeSecaoDoDono Modelo,
        List<PeCampo> Colunas,
        List<(PePdtic Pdtic, PeTrilhaOrgao Trilha, HashSet<long> Visiveis, PeRegistroResponse Registro)> Linhas,
        // Seção por ciclo (E7, rodada B): o rótulo do ciclo de cada registro; nulo nas outras
        Dictionary<long, string>? Ciclos = null);

    public async Task<PePlanilhaArquivo> ConsolidadoSecaoAsync(string secaoChave, string? formato, PeUserContext ctx)
    {
        ConferirConsolidado(ctx);
        var tipo = Formato(formato, completa: false);
        var baseConsolidada = await BaseConsolidadaAsync();

        var secao = baseConsolidada.Dados.SecaoPorChave(secaoChave?.Trim() ?? string.Empty);
        if (secao == null || secao.Escopo != PeDominios.Escopo.Pdtic || secao.ExcluidoEm != null
            || baseConsolidada.Dados.Passos.FirstOrDefault(p => p.Id == secao.PassoId)?.ExcluidoEm != null)
            throw new ApiException(ErrorCode.PeSecaoIndisponivel, "Esta seção não existe no PDTIC. Atualize a tela.");
        if (!secao.NaPlanilha)
            throw new ApiException(ErrorCode.PeSecaoIndisponivel, "Esta seção não vai para a planilha: o administrador a deixou de fora.");
        var consolidada = await ConsolidarAsync(baseConsolidada, secao)
            ?? throw new ApiException(ErrorCode.PeSecaoIndisponivel, "Esta seção não aparece em nenhum nível nem para nenhum órgão.");

        var nome = $"PDTIC_consolidado_{secao.Chave}_{Hoje()}.{tipo}";
        return tipo == Csv
            ? new PePlanilhaArquivo(GerarCsvConsolidado(consolidada), MimeCsv, nome)
            : new PePlanilhaArquivo(PeXlsx.Gerar(new[] { AbaConsolidada(consolidada) }, LeiaMeDoConsolidado(baseConsolidada)), MimeXlsx, nome);
    }

    public async Task<PePlanilhaArquivo> ConsolidadoCompletoAsync(string? formato, PeUserContext ctx)
    {
        ConferirConsolidado(ctx);
        Formato(formato, completa: true);
        var baseConsolidada = await BaseConsolidadaAsync();
        var dados = baseConsolidada.Dados;

        var abas = new List<PeXlsxAba>();
        foreach (var etapa in dados.Etapas)
            foreach (var passo in dados.PassosDaEtapa(etapa.Id, incluirExcluidos: false))
                foreach (var secao in dados.SecoesDoPasso(passo.Id, incluirExcluidos: false).Where(s => s.NaPlanilha))
                    if (await ConsolidarAsync(baseConsolidada, secao) is PeSecaoConsolidada consolidada)
                        abas.Add(AbaConsolidada(consolidada));

        var nome = $"PDTIC_consolidado_{Completa}_{Hoje()}.{Xlsx}";
        return new PePlanilhaArquivo(PeXlsx.Gerar(abas, LeiaMeDoConsolidado(baseConsolidada)), MimeXlsx, nome);
    }

    private void ConferirConsolidado(PeUserContext ctx)
    {
        if (!_permissoes.PodeVerConsolidado(ctx))
            throw new ApiException(ErrorCode.PeSemPermissao,
                "As planilhas consolidadas são da SGDI, da Secretaria do CGTIC e do administrador do módulo.");
    }

    private async Task<PeBaseConsolidada> BaseConsolidadaAsync()
    {
        var dados = await PeModeloDados.CarregarAsync(_context);
        var semVigente = await PeTrilhaOrgao.SemPeticVigenteAsync(_context);
        var atuais = await (from p in _context.PePdtics.AsNoTracking()
                            join o in _context.PgiaOrgaos.AsNoTracking() on p.OrgaoId equals o.Id
                            where !PeDominios.SituacaoPdtic.Encerradas.Contains(p.Situacao)
                            orderby o.Sigla, o.Nome, p.Id
                            select new { Pdtic = p, Orgao = o })
            .ToListAsync();

        var ids = atuais.Select(a => a.Orgao.Id).Distinct().ToList();
        var escolhidos = await _context.PeOrgaosConfig.AsNoTracking()
            .Where(c => ids.Contains(c.OrgaoId))
            .ToDictionaryAsync(c => c.OrgaoId, c => c.NivelId);
        var ajustes = (await _context.PeOrgaosAjuste.AsNoTracking().Where(a => ids.Contains(a.OrgaoId)).ToListAsync())
            .ToLookup(a => a.OrgaoId);

        var pdtics = atuais.Select(a => (a.Pdtic, PeTrilhaOrgao.Resolver(dados, a.Orgao,
                escolhidos.TryGetValue(a.Orgao.Id, out var nivel) ? nivel : null,
                ajustes[a.Orgao.Id].ToDictionary(x => (x.AlvoTipo, x.AlvoId), x => x.Situacao),
                semVigente)))
            .ToList();
        var niveis = dados.Niveis.Select(n => PeTrilhaOrgao.DoNivel(dados, n.Id)).ToList();
        return new PeBaseConsolidada(dados, pdtics, niveis);
    }

    /// <summary>
    /// Colunas: os campos da seção (não apagados e na planilha) que aparecem em algum nível ou
    /// para algum órgão, na ordem do modelo. Linhas: os registros de cada PDTIC atual cujo
    /// órgão vê a seção. Nulo quando a seção não aparece em nenhum nível nem órgão.
    /// </summary>
    private async Task<PeSecaoConsolidada?> ConsolidarAsync(PeBaseConsolidada baseConsolidada, PeSecao secao)
    {
        var dados = baseConsolidada.Dados;
        var visiveisNosNiveis = baseConsolidada.TrilhasDosNiveis
            .SelectMany(etapas => etapas.SelectMany(e => e.Passos).SelectMany(p => p.Secoes))
            .Where(s => s.Id == secao.Id)
            .ToList();
        var visiveisNosOrgaos = baseConsolidada.Pdtics
            .Select(p => p.Trilha.Secao(secao.Id))
            .Where(s => s != null)
            .Select(s => s!.Value.Secao)
            .ToList();
        if (visiveisNosNiveis.Count == 0 && visiveisNosOrgaos.Count == 0) return null;

        var idsVisiveis = visiveisNosNiveis.Concat(visiveisNosOrgaos).SelectMany(s => s.Campos).Select(c => c.Id).ToHashSet();
        var campos = dados.CamposDaSecao(secao.Id, incluirExcluidos: true).ToList();
        var idsCampos = campos.Select(c => c.Id).ToHashSet();
        var modelo = new PeSecaoDoDono
        {
            Secao = secao,
            Campos = campos,
            Visiveis = campos.Where(c => c.ExcluidoEm == null && idsVisiveis.Contains(c.Id)).Select(c => new PeCampoVisivel(c, false)).ToList(),
            Opcoes = dados.Opcoes.Where(o => idsCampos.Contains(o.CampoId)).GroupBy(o => o.CampoId).ToDictionary(g => g.Key, g => g.ToList())
        };

        // Cada PDTIC com a seção como o órgão dele a vê; os registros de todos de uma vez
        var dosOrgaos = baseConsolidada.Pdtics
            .Select(p => (p.Pdtic, p.Trilha, Visivel: p.Trilha.Secao(secao.Id)))
            .Where(p => p.Visivel != null)
            .Select(p => (p.Pdtic, p.Trilha, Secao: p.Trilha.Montar(p.Visivel!.Value.Secao)))
            .ToList();
        var registros = await _registros.ExportarDosPdticsAsync(secao.Id, dosOrgaos.Select(p => (p.Pdtic.Id, p.Secao)).ToList());

        // Seção por ciclo: só os registros com ciclo, com o rótulo dele
        Dictionary<long, string>? ciclos = null;
        if (dados.PorCicloDe(secao.Id) != null)
            ciclos = (await _registros.CiclosDosRegistrosAsync(registros.Values.SelectMany(r => r).Select(r => r.Id).ToList()))
                .ToDictionary(c => c.Key, c => c.Value.Rotulo);

        var linhas = new List<(PePdtic, PeTrilhaOrgao, HashSet<long>, PeRegistroResponse)>();
        foreach (var (pdtic, trilha, doOrgao) in dosOrgaos)
        {
            var visiveis = doOrgao.Visiveis.Select(v => v.Campo.Id).ToHashSet();
            linhas.AddRange(registros.GetValueOrDefault(pdtic.Id, new List<PeRegistroResponse>())
                .Where(r => ciclos == null || ciclos.ContainsKey(r.Id))
                .Select(r => (pdtic, trilha, visiveis, r)));
        }

        return new PeSecaoConsolidada(modelo, modelo.Visiveis.Select(v => v.Campo).Where(c => c.NaPlanilha).ToList(), linhas, ciclos);
    }

    /// <summary>As colunas do órgão antes dos campos (nome, sigla, nível, versão e situação do PDTIC).</summary>
    private static readonly (string Titulo, double Largura, string Ajuda)[] ColunasDoOrgao =
    {
        ("Órgão", 40, "Nome do órgão ou entidade."),
        ("Sigla", 10, "Sigla do órgão."),
        ("Nível", 16, "Nível de maturidade do órgão hoje: diz que campos aparecem para ele."),
        ("Versão do PDTIC", 12, "Versão do PDTIC atual do órgão."),
        ("Situação do PDTIC", 20, "Situação do PDTIC atual do órgão.")
    };

    private static string[] CelulasDoOrgao(PePdtic pdtic, PeTrilhaOrgao trilha) => new[]
    {
        trilha.Orgao.Nome,
        trilha.Orgao.Sigla,
        trilha.Nivel.Nome,
        pdtic.Versao,
        PeDominios.SituacaoPdtic.Rotulo(pdtic.Situacao)
    };

    private static byte[] GerarCsvConsolidado(PeSecaoConsolidada secao)
    {
        var csv = new CsvEscritor();
        var comCodigo = TemCodigo(secao.Modelo);

        var cabecalho = ColunasDoOrgao.Select(c => (c.Titulo, false)).ToList();
        if (secao.Ciclos != null) cabecalho.Add(("Ciclo", false));
        if (comCodigo) cabecalho.Add(("Código", false));
        cabecalho.AddRange(secao.Colunas.Select(c => (c.Rotulo, true)));
        csv.Linha(cabecalho);

        foreach (var (pdtic, trilha, visiveis, registro) in secao.Linhas)
        {
            var orgao = CelulasDoOrgao(pdtic, trilha);
            // Nome, sigla e nível foram digitados por alguém: protegidos como texto livre
            var linha = new List<(string, bool)> { (orgao[0], true), (orgao[1], true), (orgao[2], true), (orgao[3], false), (orgao[4], false) };
            if (secao.Ciclos != null) linha.Add((secao.Ciclos.GetValueOrDefault(registro.Id) ?? string.Empty, true));
            if (comCodigo) linha.Add((registro.Codigo ?? string.Empty, false));
            foreach (var campo in secao.Colunas)
                linha.Add(visiveis.Contains(campo.Id) ? TextoCsv(campo, registro) : (string.Empty, false));
            csv.Linha(linha);
        }
        return csv.ParaBytes();
    }

    private static PeXlsxAba AbaConsolidada(PeSecaoConsolidada secao)
    {
        var colunas = ColunasDoOrgao
            .Select(c => new PeXlsxColuna { Titulo = c.Titulo, Tipo = PeXlsxTipo.Texto, Largura = c.Largura, Ajuda = c.Ajuda })
            .ToList();
        var comCodigo = TemCodigo(secao.Modelo);
        if (secao.Ciclos != null) colunas.Add(ColunaDoCiclo());
        if (comCodigo) colunas.Add(ColunaDoCodigo());
        colunas.AddRange(secao.Colunas.Select(c => Coluna(secao.Modelo, c)));

        var linhas = secao.Linhas.Select(l =>
        {
            var celulas = new List<object?>(CelulasDoOrgao(l.Pdtic, l.Trilha));
            if (secao.Ciclos != null) celulas.Add(secao.Ciclos.GetValueOrDefault(l.Registro.Id));
            if (comCodigo) celulas.Add(l.Registro.Codigo);
            foreach (var campo in secao.Colunas)
                celulas.Add(l.Visiveis.Contains(campo.Id) ? Celula(campo, l.Registro) : null);
            return celulas.ToArray();
        }).ToList();

        return new PeXlsxAba { Nome = secao.Modelo.Secao.Titulo, Colunas = colunas, Linhas = linhas };
    }

    private static IReadOnlyList<(string, string)> LeiaMeDoConsolidado(PeBaseConsolidada baseConsolidada)
    {
        var orgaos = baseConsolidada.Pdtics.Select(p => p.Pdtic.OrgaoId).Distinct().Count();
        return new List<(string, string)>
        {
            ("Conteúdo", "Consolidado dos PDTICs atuais de todos os órgãos (em elaboração, em aprovação, devolvidos, aprovados, publicados ou em acompanhamento)"),
            ("Órgãos", orgaos == 1 ? "1 órgão com PDTIC atual" : $"{orgaos} órgãos com PDTIC atual"),
            ("Extraído em", Agora()),
            ("Como ler", "Cada linha é um registro do PDTIC de um órgão. As primeiras colunas dizem o órgão, o nível de maturidade dele, "
                         + "a versão e a situação do PDTIC. As colunas dos campos juntam o que aparece em qualquer nível: a célula fica vazia "
                         + "quando o campo não aparece para o órgão. Os códigos (por exemplo, N01) valem dentro do PDTIC de cada órgão.")
        };
    }

    /// <summary>A sigla no nome do arquivo: só letras, números, hífen e sublinhado.</summary>
    private static string NomeSeguro(string texto)
    {
        var limpo = new string((texto ?? string.Empty).Trim().Select(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' ? c : '_').ToArray());
        return string.IsNullOrEmpty(limpo) ? "orgao" : limpo;
    }

    public async Task<PePlanilhaArquivo> DfSecaoAsync(string secaoChave, string? formato, PeUserContext ctx)
    {
        var tipo = Formato(formato, completa: false);
        var secao = await _registros.ExportarSecaoAsync(PeDono.Df, secaoChave, ctx);
        var nome = $"DF_{secao.Modelo.Secao.Chave}_{Hoje()}.{tipo}";
        var leiaMe = new List<(string, string)>
        {
            ("Dono", "Catálogo do DF (princípios e diretrizes do ciclo)"),
            ("Seção", secao.Modelo.Secao.Titulo),
            ("Extraído em", Agora())
        };
        leiaMe.AddRange(ComoLer());
        return tipo == Csv
            ? new PePlanilhaArquivo(GerarCsv(secao), MimeCsv, nome)
            : new PePlanilhaArquivo(PeXlsx.Gerar(new[] { Aba(secao) }, leiaMe), MimeXlsx, nome);
    }

    // ── CSV ─────────────────────────────────────────────────────────────────

    /// <summary>O CSV de uma seção: cabeçalho com os rótulos e uma linha por registro.</summary>
    public static byte[] GerarCsv(PeSecaoExportada secao)
    {
        var csv = new CsvEscritor();
        var comCodigo = TemCodigo(secao.Modelo);
        var comCiclo = secao.Modelo.PorCiclo != null;

        var cabecalho = new List<(string, bool)>();
        if (comCiclo) cabecalho.Add(("Ciclo", false));
        if (comCodigo) cabecalho.Add(("Código", false));
        cabecalho.AddRange(secao.Colunas.Select(c => (c.Campo.Rotulo, true)));
        csv.Linha(cabecalho);

        foreach (var registro in RegistrosNaOrdem(secao))
        {
            var linha = new List<(string, bool)>();
            // O nome da avaliação intermediária foi digitado pela equipe: protegido como texto livre
            if (comCiclo) linha.Add((secao.CicloDe(registro.Id)?.Rotulo ?? string.Empty, true));
            if (comCodigo) linha.Add((registro.Codigo ?? string.Empty, false));
            foreach (var coluna in secao.Colunas)
            {
                var (valor, textoLivre) = TextoCsv(coluna.Campo, registro);
                linha.Add((valor, textoLivre));
            }
            csv.Linha(linha);
        }
        return csv.ParaBytes();
    }

    private static (string Valor, bool TextoLivre) TextoCsv(PeCampo campo, PeRegistroResponse registro)
    {
        var rotulo = registro.Rotulos.GetValueOrDefault(campo.Chave) ?? string.Empty;
        switch (TipoDaColuna(campo))
        {
            case PeXlsxTipo.Numero:
            case PeXlsxTipo.Moeda:
            case PeXlsxTipo.Percentual:
                var numero = Numero(registro, campo.Chave);
                if (numero == null) return (string.Empty, false);
                var casas = campo.Tipo == PeDominios.TipoCampo.Numero ? PeValores.Inteiro(campo.Config, "casas") : null;
                var formato = campo.Tipo == PeDominios.TipoCampo.Moeda ? "0.00"
                    : casas is > 0 ? "0." + new string('0', casas.Value)
                    : casas == 0 ? "0"
                    : "0.######";
                return (numero.Value.ToString(formato, PeFormato.Csv), false);
            case PeXlsxTipo.Data:
                return (rotulo, false);
            default:
                // Ligações mostram códigos (dados pelo sistema); o resto veio do teclado de alguém
                return (rotulo, !PeRegistroDados.EhLigacao(campo));
        }
    }

    // ── XLSX ────────────────────────────────────────────────────────────────

    private static PeXlsxAba Aba(PeSecaoExportada secao)
    {
        var colunas = new List<PeXlsxColuna>();
        var comCodigo = TemCodigo(secao.Modelo);
        var comCiclo = secao.Modelo.PorCiclo != null;
        if (comCiclo) colunas.Add(ColunaDoCiclo());
        if (comCodigo) colunas.Add(ColunaDoCodigo());
        colunas.AddRange(secao.Colunas.Select(c => Coluna(secao.Modelo, c.Campo)));

        var linhas = RegistrosNaOrdem(secao).Select(registro =>
        {
            var celulas = new List<object?>();
            if (comCiclo) celulas.Add(secao.CicloDe(registro.Id)?.Rotulo);
            if (comCodigo) celulas.Add(registro.Codigo);
            foreach (var coluna in secao.Colunas) celulas.Add(Celula(coluna.Campo, registro));
            return celulas.ToArray();
        }).ToList();

        return new PeXlsxAba { Nome = secao.Modelo.Secao.Titulo, Colunas = colunas, Linhas = linhas };
    }

    /// <summary>
    /// Os registros na ordem da planilha: na seção por ciclo (E7, rodada B), pelo ciclo (os de
    /// monitoramento pelo início, depois as avaliações) e, dentro dele, pela ordem da seção.
    /// </summary>
    private static IEnumerable<PeRegistroResponse> RegistrosNaOrdem(PeSecaoExportada secao) =>
        secao.Modelo.PorCiclo == null
            ? secao.Registros
            : secao.Registros
                .Select((registro, posicao) => (Registro: registro, Posicao: posicao, Ciclo: secao.CicloDe(registro.Id)))
                .OrderBy(x => x.Ciclo?.Tipo == PeDominios.TipoCiclo.Avaliacao ? 1 : 0)
                .ThenBy(x => x.Ciclo?.Inicio ?? DateOnly.MinValue)
                .ThenBy(x => x.Ciclo?.Numero ?? 0)
                .ThenBy(x => x.Posicao)
                .Select(x => x.Registro);

    private static PeXlsxColuna ColunaDoCiclo() => new()
    {
        Titulo = "Ciclo",
        Tipo = PeXlsxTipo.Texto,
        Largura = 24,
        Ajuda = "O ciclo do acompanhamento do registro: o ciclo de monitoramento ou a avaliação intermediária."
    };

    private static PeXlsxColuna ColunaDoCodigo() => new()
    {
        Titulo = "Código",
        Tipo = PeXlsxTipo.Codigo,
        Largura = 10,
        Ajuda = "Código do registro, dado pelo sistema. Não se repete, nem depois de apagado."
    };

    private static PeXlsxColuna Coluna(PeSecaoDoDono modelo, PeCampo campo)
    {
        var tipo = TipoDaColuna(campo);
        return new PeXlsxColuna
        {
            Titulo = campo.Rotulo,
            Tipo = tipo,
            Casas = campo.Tipo == PeDominios.TipoCampo.Numero ? PeValores.Inteiro(campo.Config, "casas") : null,
            Largura = Largura(campo, tipo),
            Lista = campo.Tipo switch
            {
                PeDominios.TipoCampo.Lista => modelo.OpcoesDe(campo).Where(o => o.Ativa).Select(o => o.Rotulo).ToList(),
                PeDominios.TipoCampo.SimNao => new[] { "Sim", "Não" },
                _ => null
            },
            Ajuda = AjudaDaColuna(campo)
        };
    }

    /// <summary>A célula de um campo: número, moeda, percentual e data como número; o resto pelo rótulo.</summary>
    private static object? Celula(PeCampo campo, PeRegistroResponse registro) => TipoDaColuna(campo) switch
    {
        PeXlsxTipo.Numero or PeXlsxTipo.Moeda or PeXlsxTipo.Percentual => Numero(registro, campo.Chave),
        PeXlsxTipo.Data => Data(registro, campo.Chave),
        _ => registro.Rotulos.GetValueOrDefault(campo.Chave)
    };

    private static PeXlsxTipo TipoDaColuna(PeCampo campo) => campo.Tipo switch
    {
        PeDominios.TipoCampo.Numero => PeXlsxTipo.Numero,
        PeDominios.TipoCampo.Moeda => PeXlsxTipo.Moeda,
        PeDominios.TipoCampo.Percentual => PeXlsxTipo.Percentual,
        PeDominios.TipoCampo.Data => PeXlsxTipo.Data,
        PeDominios.TipoCampo.TextoLongo or PeDominios.TipoCampo.TextoRico => PeXlsxTipo.TextoLongo,
        PeDominios.TipoCampo.Calculado when PeConfigCampo.TipoDoCalculo(campo.Config) != PeDominios.Calculo.NivelRisco => PeXlsxTipo.Numero,
        _ => PeXlsxTipo.Texto
    };

    private static double Largura(PeCampo campo, PeXlsxTipo tipo) => campo.Largura switch
    {
        PeDominios.Largura.Estreita => 12,
        PeDominios.Largura.Media => 24,
        PeDominios.Largura.Larga => 50,
        _ => tipo switch
        {
            PeXlsxTipo.TextoLongo => 50,
            PeXlsxTipo.Data => 12,
            PeXlsxTipo.Numero or PeXlsxTipo.Percentual => 14,
            PeXlsxTipo.Moeda => 18,
            _ => 24
        }
    };

    private static string? AjudaDaColuna(PeCampo campo)
    {
        var ajuda = campo.Ajuda?.Trim();
        return PeRegistroDados.EhLigacao(campo)
            ? string.IsNullOrEmpty(ajuda) ? "Os códigos dos registros ligados." : $"{ajuda} A coluna mostra os códigos dos registros ligados."
            : ajuda;
    }

    private static IReadOnlyList<(string, string)> LeiaMeDoPetic(PePetic petic)
    {
        var itens = new List<(string, string)>
        {
            ("Dono", "PETIC-DF (Plano Estratégico de Tecnologia da Informação e Comunicação do Distrito Federal)"),
            ("Versão", $"{petic.Versao} ({Situacao(petic.Situacao)})"),
            ("Título", petic.Titulo),
            ("Vigência", petic.VigenciaInicio != null && petic.VigenciaFim != null
                ? $"{PeFormato.Data(petic.VigenciaInicio.Value)} a {PeFormato.Data(petic.VigenciaFim.Value)}"
                : "-"),
            ("Extraído em", Agora())
        };
        itens.AddRange(ComoLer());
        return itens;
    }

    private static IEnumerable<(string, string)> ComoLer() => new[]
    {
        ("Como ler", "Cada aba é uma seção. A coluna Código identifica cada registro (por exemplo, OE01), e as colunas de "
                     + "ligação mostram os códigos dos registros ligados, para cruzar as abas. Datas e valores estão como números.")
    };

    private static string Situacao(string situacao) => situacao switch
    {
        PeDominios.SituacaoPetic.Rascunho => "rascunho",
        PeDominios.SituacaoPetic.EmDeliberacao => "em deliberação no CGTIC",
        PeDominios.SituacaoPetic.Aprovado => "aprovada, vigente",
        PeDominios.SituacaoPetic.Substituido => "substituída",
        _ => situacao
    };

    // ── Apoio ───────────────────────────────────────────────────────────────

    private static bool TemCodigo(PeSecaoDoDono modelo) =>
        !modelo.EhFormulario && !string.IsNullOrEmpty(modelo.Secao.PrefixoCodigo);

    private static decimal? Numero(PeRegistroResponse registro, string chave) =>
        registro.Dados.TryGetValue(chave, out var valor) && valor.ValueKind == JsonValueKind.Number && valor.TryGetDecimal(out var numero)
            ? numero
            : null;

    private static DateOnly? Data(PeRegistroResponse registro, string chave) =>
        registro.Dados.TryGetValue(chave, out var valor) && valor.ValueKind == JsonValueKind.String
        && DateOnly.TryParseExact(valor.GetString(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var data)
            ? data
            : null;

    /// <summary>csv ou xlsx (padrão: xlsx); a completa só sai em xlsx.</summary>
    private static string Formato(string? formato, bool completa)
    {
        var texto = string.IsNullOrWhiteSpace(formato) ? Xlsx : formato.Trim().ToLowerInvariant();
        if (texto is not (Csv or Xlsx))
            throw new ApiException(ErrorCode.PePlanilhaInvalida, "Escolha o formato da planilha: csv ou xlsx.");
        if (completa && texto == Csv)
            throw new ApiException(ErrorCode.PePlanilhaInvalida, "A planilha completa, com uma aba por seção, só sai em xlsx.");
        return texto;
    }

    private static string Hoje() => DateTimeHelper.TodayBrasilia().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static string Agora() =>
        DateTimeHelper.NowBrasilia().ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture) + " (horário de Brasília)";
}
