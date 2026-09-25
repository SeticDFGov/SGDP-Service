using System.Globalization;
using api.Planejamento;
using demanda_service.Helpers;
using Microsoft.EntityFrameworkCore;
using Models;
using Models.Pgia;
using Models.Planejamento;

namespace service.Planejamento;

/// <summary>
/// Um órgão como o painel, a conformidade e a página do órgão o veem (E8): o nível de hoje, todas
/// as versões do PDTIC, a versão avaliada, a trilha do órgão ajustada a ela, a situação dos passos
/// (com os ciclos) e as inadimplências vigentes.
/// </summary>
public sealed class PeRetratoDoOrgao
{
    public required PgiaOrgao Orgao { get; init; }

    // O nível de hoje: o escolhido ou o padrão (o primeiro ativo pela ordem); nulo sem modelo
    public PeNivel? Nivel { get; init; }

    public bool NivelPadrao { get; init; }

    // Todas as versões do PDTIC do órgão, da mais nova para a mais antiga
    public required List<PePdtic> Pdtics { get; init; }

    // O PDTIC em vigor ou em andamento: a versão vigente; sem ela, a da elaboração (a conformidade
    // avalia este); nulo quando o órgão não tem nenhum dos dois
    public PePdtic? EmVigor { get; init; }

    // O PDTIC de referência (a situação do órgão no painel e o PDTIC da página do órgão): o em
    // vigor; sem ele, a versão mais recente (encerrada); nulo sem PDTIC
    public PePdtic? Referencia { get; init; }

    // O PDTIC cuja situação dos passos foi calculada (o em vigor; na página do órgão, a referência)
    public PePdtic? Avaliado { get; init; }

    public PeTrilhaOrgao? Trilha { get; set; }

    public PeSituacaoDetalhada? Situacao { get; set; }

    // Só as vigentes (notificadas ou inadimplentes)
    public List<PeInadimplencia> Inadimplencias { get; set; } = new();

    public PeAvaliacaoDeConformidade? Conformidade { get; set; }

    /// <summary>A situação do órgão no painel: a do PDTIC de referência, ou sem PDTIC (a substituída conta como encerrada).</summary>
    public string SituacaoNoPainel => Referencia == null
        ? PeDominios.SituacaoPainel.SemPdtic
        : Referencia.Situacao == PeDominios.SituacaoPdtic.Substituido ? PeDominios.SituacaoPdtic.Encerrado : Referencia.Situacao;
}

/// <summary>A linha da conformidade de um órgão e os alertas que saem dela.</summary>
public sealed record PeAvaliacaoDeConformidade(PeConformidadeOrgaoResponse Linha, bool VigenciaVencida, bool RevisaoVencida, bool CicloAtrasado);

/// <summary>
/// A base dos painéis da SGDI (E8), lida de uma vez para todos os órgãos pedidos: o modelo (uma
/// vez), as versões do PDTIC, o nível e os ajustes de cada órgão, a trilha resolvida em memória e,
/// quando pedida, a situação dos passos de todos (pela <see cref="PeLeituraDaSituacao"/>) e as
/// inadimplências vigentes. O número de consultas não cresce com o número de órgãos, e nada é
/// gravado (os ciclos de monitoramento que faltam entram só no cálculo).
/// </summary>
public sealed class PeBaseDosPaineis
{
    public required PeModeloDados Dados { get; init; }

    public required List<PeRetratoDoOrgao> Orgaos { get; init; }

    // A leitura da situação (os registros, as ligações, os ciclos, as deliberações); nula sem a situação
    public PeLeituraDaSituacao? Leitura { get; init; }

    public DateOnly Hoje { get; init; }

    /// <summary>
    /// A base dos órgãos dados. comSituacao: calcula a situação dos passos do PDTIC em vigor (com
    /// daReferencia, a do PDTIC de referência, também o encerrado: a página do órgão) e a linha da
    /// conformidade; comInadimplencias: lê as inadimplências vigentes (a tabela da E8).
    /// </summary>
    public static async Task<PeBaseDosPaineis> CarregarAsync(AppDbContext context, IReadOnlyList<PgiaOrgao> orgaos, bool comSituacao,
        bool daReferencia = false, bool comInadimplencias = true)
    {
        var hoje = PeCiclos.Hoje();
        var ids = orgaos.Select(o => o.Id).ToList();

        // As inadimplências primeiro: no intervalo do deploy (a tabela da E8 ainda não existe), a
        // consulta falha antes de qualquer outra coisa e o controller responde 409 com corpo
        var inadimplencias = comInadimplencias && ids.Count > 0
            ? (await context.PeInadimplencias.AsNoTracking()
                .Where(i => ids.Contains(i.OrgaoId) && PeDominios.SituacaoInadimplencia.Vigentes.Contains(i.Situacao))
                .ToListAsync())
            .ToLookup(i => i.OrgaoId)
            : Array.Empty<PeInadimplencia>().ToLookup(i => 0L);

        var dados = await PeModeloDados.CarregarAsync(context);
        var semVigente = await PeTrilhaOrgao.SemPeticVigenteAsync(context);
        var pdtics = ids.Count == 0
            ? new List<PePdtic>()
            : await context.PePdtics.AsNoTracking().Where(p => ids.Contains(p.OrgaoId)).OrderByDescending(p => p.Id).ToListAsync();
        var porOrgao = pdtics.ToLookup(p => p.OrgaoId);
        var escolhidos = ids.Count == 0
            ? new Dictionary<long, long>()
            : await context.PeOrgaosConfig.AsNoTracking().Where(c => ids.Contains(c.OrgaoId)).ToDictionaryAsync(c => c.OrgaoId, c => c.NivelId);
        var ajustes = ids.Count == 0
            ? Array.Empty<PeOrgaoAjuste>().ToLookup(a => 0L)
            : (await context.PeOrgaosAjuste.AsNoTracking().Where(a => ids.Contains(a.OrgaoId)).ToListAsync()).ToLookup(a => a.OrgaoId);

        var retratos = new List<PeRetratoDoOrgao>();
        foreach (var orgao in orgaos)
        {
            var versoes = porOrgao[orgao.Id].ToList();
            var emVigor = versoes.FirstOrDefault(p => PeDominios.SituacaoPdtic.Vigentes.Contains(p.Situacao))
                          ?? versoes.FirstOrDefault(p => PeDominios.SituacaoPdtic.DaElaboracao.Contains(p.Situacao));
            var referencia = emVigor ?? versoes.FirstOrDefault();
            var avaliado = daReferencia ? referencia : emVigor;
            long? escolhido = escolhidos.TryGetValue(orgao.Id, out var nivelId) ? nivelId : null;
            var nivel = (escolhido != null ? dados.Niveis.FirstOrDefault(n => n.Id == escolhido) : null) ?? dados.NivelPadrao();

            var retrato = new PeRetratoDoOrgao
            {
                Orgao = orgao,
                Nivel = nivel,
                NivelPadrao = nivel == null || escolhido != nivel.Id,
                Pdtics = versoes,
                EmVigor = emVigor,
                Referencia = referencia,
                Avaliado = avaliado,
                Inadimplencias = inadimplencias[orgao.Id].ToList()
            };
            if (avaliado != null && nivel != null)
            {
                retrato.Trilha = PeTrilhaOrgao.Resolver(dados, orgao, escolhido,
                    ajustes[orgao.Id].ToDictionary(a => (a.AlvoTipo, a.AlvoId), a => a.Situacao), semVigente);
                retrato.Trilha.AjustarAoPdtic(avaliado);
            }
            retratos.Add(retrato);
        }

        PeLeituraDaSituacao? leitura = null;
        if (comSituacao)
        {
            var avaliados = retratos.Where(r => r.Trilha != null).Select(r => r.Avaliado!.Id).ToList();
            leitura = await PeLeituraDaSituacao.CarregarAsync(context, avaliados, dados.Acompanhamento.Ativo);
            foreach (var retrato in retratos)
            {
                if (retrato.Trilha != null)
                    retrato.Situacao = PePdticService.Detalhar(retrato.Avaliado!, retrato.Trilha, leitura, papelEdita: false);
                retrato.Conformidade = PeConformidadeRegras.Avaliar(retrato, leitura, dados.Acompanhamento.Ativo, hoje);
            }
        }

        return new PeBaseDosPaineis { Dados = dados, Orgaos = retratos, Leitura = leitura, Hoje = hoje };
    }

    /// <summary>Os órgãos ativos (os do painel e da conformidade), pela sigla.</summary>
    public static async Task<List<PgiaOrgao>> OrgaosAtivosAsync(AppDbContext context) =>
        await context.PgiaOrgaos.AsNoTracking().Where(o => o.Ativo).OrderBy(o => o.Sigla).ThenBy(o => o.Nome).ThenBy(o => o.Id).ToListAsync();
}

/// <summary>
/// Os itens de conformidade de um órgão (E8; só itens de TIC, decisão 4 do plano), sobre o PDTIC
/// em vigor (o vigente; sem ele, o da elaboração). Regras puras, com a situação dos passos já
/// calculada:
/// <list type="bullet">
/// <item>aprovado pelo CGTIC: há deliberação "aprovado" desta versão (art. 5º do Decreto nº 48.900/2026);</item>
/// <item>comunicado à SGDI: a versão foi enviada pelo sistema, ou registrada fora dele com a
/// aprovação (art. 7º, V, do Decreto nº 48.899/2026);</item>
/// <item>vigente: publicado ou em acompanhamento, com hoje dentro da vigência (art. 12);</item>
/// <item>os nove conteúdos: todos os passos travados feitos (o feito fora do sistema conta; art. 12, § 2º);</item>
/// <item>revisão em dia: a última aprovação do CGTIC (no registrado fora, a publicação) somada à
/// periodicidade de revisão do passo da abrangência (anual por padrão) não venceu;</item>
/// <item>acompanhamento em dia: nenhum ciclo de monitoramento atrasado; antes da publicação, não se
/// aplica e sai da conta.</item>
/// </list>
/// Percentual = atendidos / aplicáveis, arredondado; o grupo sai do percentual
/// (<see cref="PeDominios.GrupoConformidade"/>). Órgão sem PDTIC em vigor: nada atendido, 0% e
/// grupo baixa. Desde a F2, a versão e a situação da linha são as do PDTIC de referência (as do
/// painel: o encerrado aparece como encerrado) e a linha traz as marcas dos alertas que o painel
/// conta (vigência vencida, revisão vencida e ciclo atrasado).
/// </summary>
public static class PeConformidadeRegras
{
    public static PeAvaliacaoDeConformidade Avaliar(PeRetratoDoOrgao retrato, PeLeituraDaSituacao? leitura, bool acompanhamentoAtivo, DateOnly hoje)
    {
        var pdtic = retrato.EmVigor;
        var referencia = retrato.Referencia;
        var linha = new PeConformidadeOrgaoResponse
        {
            OrgaoId = retrato.Orgao.Id,
            Sigla = retrato.Orgao.Sigla,
            Nome = retrato.Orgao.Nome,
            NivelNome = retrato.Nivel?.Nome,
            // A versão e a situação de referência, as mesmas do painel (F2): o órgão com o PDTIC
            // encerrado aparece como encerrado, e "sem PDTIC" é só quem nunca abriu um. Os itens
            // continuam avaliando só a versão vigente ou a da elaboração
            PdticId = referencia?.Id,
            PdticVersao = referencia?.Versao,
            PdticSituacao = referencia == null ? null : retrato.SituacaoNoPainel
        };
        var vigenciaVencida = false;
        var revisaoVencida = false;
        var cicloAtrasado = false;

        // A situação dos passos é a do PDTIC em vigor (na página do órgão, o avaliado pode ser o encerrado)
        var situacao = pdtic != null && retrato.Avaliado?.Id == pdtic.Id ? retrato.Situacao : null;
        if (pdtic == null || situacao == null || leitura == null)
        {
            var semPdtic = SemPdtic(retrato);
            foreach (var item in PeDominios.ItemConformidade.Todos)
                linha.Itens[item.Chave] = item.Chave == PeDominios.ItemConformidade.AcompanhamentoEmDia
                    ? Item(null, "O acompanhamento começa depois da publicação do PDTIC")
                    : Item(false, semPdtic);
        }
        else
        {
            var aprovacao = leitura.Deliberacoes(pdtic.Id).FirstOrDefault(d => d.Situacao == PeDominios.SituacaoDeliberacao.Aprovado);
            linha.Itens[PeDominios.ItemConformidade.AprovadoCgtic] = AprovadoPeloCgtic(pdtic, aprovacao, leitura);
            linha.Itens[PeDominios.ItemConformidade.ComunicadoSgdi] = ComunicadoASgdi(pdtic);
            linha.Itens[PeDominios.ItemConformidade.Vigente] = Vigente(pdtic, hoje, out vigenciaVencida);
            linha.Itens[PeDominios.ItemConformidade.NoveConteudos] = NoveConteudos(situacao);
            linha.Itens[PeDominios.ItemConformidade.RevisaoEmDia] = RevisaoEmDia(retrato, pdtic, aprovacao, situacao, hoje, out revisaoVencida);
            linha.Itens[PeDominios.ItemConformidade.AcompanhamentoEmDia] =
                AcompanhamentoEmDia(pdtic, situacao, acompanhamentoAtivo, hoje, out cicloAtrasado);
        }

        linha.Atendidos = linha.Itens.Values.Count(i => i.Atende == true);
        linha.Aplicaveis = linha.Itens.Values.Count(i => i.Atende != null);
        linha.Percentual = Percentual(linha.Atendidos, linha.Aplicaveis);
        linha.Grupo = PeDominios.GrupoConformidade.De(linha.Percentual);
        // As marcas que o painel conta (F2): a tela filtra a lista por elas, não pelo "Não" do item
        // (o item também é "Não" antes da primeira aprovação, e a vigência que ainda não começou)
        linha.Alertas = new PeConformidadeAlertasResponse
        {
            VigenciaVencida = vigenciaVencida,
            RevisaoVencida = revisaoVencida,
            CicloAtrasado = cicloAtrasado
        };
        var vigente = retrato.Inadimplencias
            .OrderBy(i => i.Situacao == PeDominios.SituacaoInadimplencia.Inadimplente ? 0 : 1)
            .ThenByDescending(i => i.Id)
            .FirstOrDefault();
        linha.Inadimplencia = vigente == null
            ? null
            : new PeConformidadeInadimplenciaResponse { Id = vigente.Id, Situacao = vigente.Situacao, Prazo = vigente.Prazo };
        return new PeAvaliacaoDeConformidade(linha, vigenciaVencida, revisaoVencida, cicloAtrasado);
    }

    /// <summary>Atendidos sobre aplicáveis, de 0 a 100, arredondado (0 sem nenhum aplicável).</summary>
    public static int Percentual(int atendidos, int aplicaveis) =>
        aplicaveis <= 0 ? 0 : (int)Math.Round(atendidos * 100m / aplicaveis, 0, MidpointRounding.AwayFromZero);

    private static PeConformidadeAtendeResponse Item(bool? atende, string detalhe) => new() { Atende = atende, Detalhe = detalhe };

    private static string SemPdtic(PeRetratoDoOrgao retrato)
    {
        var ultimo = retrato.Pdtics.FirstOrDefault();
        if (ultimo == null) return "O órgão ainda não abriu o PDTIC";
        var quando = ultimo.EncerradoEm is DateTime encerrado ? $" em {PePdticService.DataBrasilia(encerrado)}" : string.Empty;
        return $"O PDTIC {ultimo.Versao} foi {PeDominios.SituacaoPdtic.RotuloMinusculo(ultimo.Situacao)}{quando} e o próximo ainda não foi aberto";
    }

    /// <summary>A data do ato de aprovação (ou do dia da decisão, em Brasília).</summary>
    private static DateOnly DataDaAprovacao(PeDeliberacao aprovacao) =>
        aprovacao.AtoData ?? DateOnly.FromDateTime(DateTimeHelper.ToBrasilia(aprovacao.DecididoEm ?? aprovacao.EnviadoEm));

    private static DateOnly DiaEmBrasilia(DateTime utc) => DateOnly.FromDateTime(DateTimeHelper.ToBrasilia(utc));

    private static PeConformidadeAtendeResponse AprovadoPeloCgtic(PePdtic pdtic, PeDeliberacao? aprovacao, PeLeituraDaSituacao leitura)
    {
        if (aprovacao != null)
        {
            var ato = PeDocumentoService.Ato(aprovacao.AtoTipo, aprovacao.AtoNumero);
            return Item(true, $"Aprovado em {PeCiclos.Data(DataDaAprovacao(aprovacao))}"
                              + (ato == null ? string.Empty : $" ({ato})")
                              + (pdtic.RegistradoExternamente ? ", registrado fora do sistema" : string.Empty));
        }
        switch (pdtic.Situacao)
        {
            case PeDominios.SituacaoPdtic.EmAprovacao:
                return Item(false, "Aguardando a deliberação do CGTIC desde " + PePdticService.DataBrasilia(pdtic.EnviadoEm));
            case PeDominios.SituacaoPdtic.Devolvido:
                var devolucao = leitura.Deliberacoes(pdtic.Id).FirstOrDefault(d => d.Situacao == PeDominios.SituacaoDeliberacao.Devolvido);
                return Item(false, devolucao?.DecididoEm is DateTime decidido
                    ? $"Devolvido pelo CGTIC em {PePdticService.DataBrasilia(decidido)} para ajuste"
                    : "Devolvido pelo CGTIC para ajuste");
            case PeDominios.SituacaoPdtic.EmElaboracao:
                return Item(false, "Ainda não enviado ao CGTIC");
        }
        return Item(false, pdtic.RegistradoExternamente
            ? "Aprovado fora do sistema por outra instância, sem deliberação do CGTIC"
            : "Sem deliberação do CGTIC que aprove esta versão");
    }

    private static PeConformidadeAtendeResponse ComunicadoASgdi(PePdtic pdtic)
    {
        if (pdtic.RegistradoExternamente)
            return Item(true, $"Registrado fora do sistema em {PeCiclos.Data(DiaEmBrasilia(pdtic.CriadoEm))}"
                              + (pdtic.AprovadoEm is DateTime aprovado ? $", com a aprovação de {PeCiclos.Data(DiaEmBrasilia(aprovado))}" : string.Empty));
        if (pdtic.EnviadoEm is DateTime enviado)
            return Item(true, $"Enviado ao CGTIC pelo sistema em {PePdticService.DataBrasilia(enviado)} (vale como a comunicação à SGDI)");
        return Item(false, "Ainda não enviado ao CGTIC pelo sistema");
    }

    private static PeConformidadeAtendeResponse Vigente(PePdtic pdtic, DateOnly hoje, out bool vencida)
    {
        vencida = false;
        if (PeDominios.SituacaoPdtic.Vigentes.Contains(pdtic.Situacao))
        {
            if (pdtic.VigenciaInicio is not DateOnly inicio || pdtic.VigenciaFim is not DateOnly fim)
                return Item(false, "Publicado sem a vigência no passo da abrangência");
            if (hoje < inicio) return Item(false, $"A vigência começa em {PeCiclos.Data(inicio)}");
            if (hoje > fim)
            {
                vencida = true;
                return Item(false, $"A vigência terminou em {PeCiclos.Data(fim)}");
            }
            return Item(true, $"Vigente de {PeCiclos.Data(inicio)} a {PeCiclos.Data(fim)}");
        }
        return Item(false, pdtic.Situacao == PeDominios.SituacaoPdtic.Aprovado
            ? "Aprovado pelo CGTIC e ainda não publicado"
            : $"Ainda não publicado ({PeDominios.SituacaoPdtic.RotuloMinusculo(pdtic.Situacao)})");
    }

    private static PeConformidadeAtendeResponse NoveConteudos(PeSituacaoDetalhada situacao)
    {
        var travados = situacao.Trilha.Passos.Where(p => p.Travado).Select(p => p.Id).ToHashSet();
        var passos = situacao.Resposta.Passos.Where(p => travados.Contains(p.PassoId)).ToList();
        var faltam = passos
            .Where(p => p.Situacao is not (PeDominios.SituacaoPasso.Feito or PeDominios.SituacaoPasso.Externo))
            .Select(p =>
            {
                var inciso = situacao.Trilha.Passo(p.PassoId)?.IncisoDecreto;
                var notas = new List<string>();
                if (!string.IsNullOrWhiteSpace(inciso))
                    notas.Add((inciso.Contains(',') ? "incisos " : "inciso ") + PePdticService.Lista(inciso.Split(',').Select(i => i.Trim()).ToList()));
                if (p.Situacao == PeDominios.SituacaoPasso.Atencao) notas.Add("com comentário aberto");
                return notas.Count == 0 ? p.Numero : $"{p.Numero} ({string.Join(", ", notas)})";
            })
            .ToList();
        if (faltam.Count > 0)
            return Item(false, faltam.Count == 1 ? $"Falta concluir o passo {faltam[0]}" : $"Falta concluir os passos {PePdticService.Lista(faltam)}");
        var externos = passos.Count(p => p.Situacao == PeDominios.SituacaoPasso.Externo);
        return Item(true, externos == 0
            ? "Os passos dos nove conteúdos estão feitos"
            : $"Os passos dos nove conteúdos estão feitos ({externos} fora do sistema, no PDTIC registrado)");
    }

    /// <summary>Os meses da revisão pela periodicidade do passo da abrangência: semestral 6, anual (e o padrão) 12.</summary>
    public static (int Meses, string Rotulo) PeriodicidadeDaRevisao(string? valor) => valor switch
    {
        "semestral" => (6, "revisão semestral"),
        "anual" => (12, "revisão anual"),
        _ => (12, "revisão anual, o padrão")
    };

    private static PeConformidadeAtendeResponse RevisaoEmDia(PeRetratoDoOrgao retrato, PePdtic pdtic, PeDeliberacao? aprovacao,
        PeSituacaoDetalhada situacao, DateOnly hoje, out bool vencida)
    {
        vencida = false;
        DateOnly? base_ = pdtic.RegistradoExternamente
            ? pdtic.PublicadoEm is DateTime publicado ? DiaEmBrasilia(publicado) : null
            : aprovacao != null ? DataDaAprovacao(aprovacao) : pdtic.AprovadoEm is DateTime aprovado ? DiaEmBrasilia(aprovado) : null;
        if (base_ is not DateOnly desde)
            return Item(false, "Ainda sem aprovação do CGTIC");

        var abrangencia = situacao.Analise.Secao(PeDominios.ChavePdtic.SecaoAbrangencia);
        var valor = abrangencia != null && abrangencia.Secao.Visiveis.Any(v => v.Campo.Chave == CampoPeriodicidadeRevisao)
            ? PeRegistroDados.Texto(PeRegistroDados.Ler(abrangencia.Registros.FirstOrDefault()?.Dados)[CampoPeriodicidadeRevisao])
            : null;
        var (meses, rotulo) = PeriodicidadeDaRevisao(valor);
        var limite = desde.AddMonths(meses);
        var oQue = pdtic.RegistradoExternamente ? "publicação" : "aprovação";
        if (hoje <= limite)
            return Item(true, $"{char.ToUpperInvariant(oQue[0])}{oQue[1..]} em {PeCiclos.Data(desde)}; a próxima revisão vence em {PeCiclos.Data(limite)} ({rotulo})");

        vencida = true;
        var revisao = retrato.Pdtics.FirstOrDefault(p => p.Id != pdtic.Id && PeDominios.SituacaoPdtic.DaElaboracao.Contains(p.Situacao));
        return Item(false, $"A revisão venceu em {PeCiclos.Data(limite)}: a última {oQue} foi em {PeCiclos.Data(desde)} ({rotulo})"
                           + (revisao == null
                               ? string.Empty
                               : $"; a revisão {revisao.Versao} está {PeDominios.SituacaoPdtic.RotuloMinusculo(revisao.Situacao)}"));
    }

    // O campo da periodicidade da revisão na seção da abrangência (passo 1.1)
    public const string CampoPeriodicidadeRevisao = "periodicidade_revisao";

    private static PeConformidadeAtendeResponse AcompanhamentoEmDia(PePdtic pdtic, PeSituacaoDetalhada situacao, bool acompanhamentoAtivo,
        DateOnly hoje, out bool atrasado)
    {
        atrasado = false;
        if (!PeDominios.SituacaoPdtic.Vigentes.Contains(pdtic.Situacao))
            return Item(null, "O acompanhamento começa depois da publicação do PDTIC");
        if (!acompanhamentoAtivo)
            return Item(null, "O acompanhamento do PDTIC está sendo preparado nesta atualização");

        var monitoramento = situacao.Ciclos.Where(c => c.Tipo == PeDominios.TipoCiclo.Monitoramento).OrderBy(c => c.Inicio).ToList();
        var atrasados = monitoramento.Where(c => PeCiclos.Exibida(c, hoje) == PeDominios.SituacaoCicloExibida.Atrasado).ToList();
        if (atrasados.Count > 0)
        {
            atrasado = true;
            return Item(false, atrasados.Count == 1
                ? $"O ciclo {atrasados[0].Rotulo} passou do prazo de fechamento ({PeCiclos.Data(atrasados[0].Prazo!.Value)})"
                : $"Os ciclos {PePdticService.Lista(atrasados.Select(c => c.Rotulo).ToList())} passaram do prazo de fechamento");
        }
        var fechado = monitoramento.LastOrDefault(c => c.Situacao == PeDominios.SituacaoCiclo.Fechado);
        var comecou = monitoramento.Any(c => c.Inicio <= hoje);
        return Item(true, fechado != null
            ? $"Nenhum ciclo de monitoramento atrasado; o último fechado foi {fechado.Rotulo}"
            : comecou ? "Nenhum ciclo de monitoramento atrasado" : "O primeiro ciclo de monitoramento ainda não começou");
    }

    /// <summary>"Sim", "Não" ou "Não se aplica" (a planilha).</summary>
    public static string Texto(bool? atende) => atende switch { true => "Sim", false => "Não", _ => "Não se aplica" };

    internal static string Hoje(DateOnly hoje) => hoje.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
