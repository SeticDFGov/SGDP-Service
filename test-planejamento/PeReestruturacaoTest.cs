using System.Text.Json;
using api.Planejamento;
using Microsoft.EntityFrameworkCore;
using Models;
using Models.Planejamento;
using service.Planejamento;
using Xunit;

namespace test.planejamento;

/// <summary>
/// Versão 9 do modelo inicial (revisão dos textos e dos passos, 2026-10-20): a trilha do órgão com
/// os passos juntados (50 para 31 no Avançado) e a aprovação como a última seção do passo que ela
/// aprova. Um banco com o modelo da versão 8 (o JSON de antes, em Fixtures/modelo-inicial-v8.json)
/// passa sozinho, na carga, à trilha nova: fica igual a um banco novo (estrutura e textos), os passos
/// que sumiram saem por exclusão lógica, o que é do órgão vai para o passo novo (comentários,
/// registros, capítulos do documento) e o que valia só para o passo que sumiu é descartado. Desde a
/// versão 9, o JSON mudado na mesma versão (o resumo gravado) regrava os textos.
/// </summary>
public class PeReestruturacaoTest : PeModeloTestBase
{
    public PeReestruturacaoTest() : base(carregar: false)
    {
    }

    /// <summary>Os passos de antes que viraram seções de outro passo (chave antiga, chave nova).</summary>
    public static readonly (string Antigo, string Novo)[] PassosJuntados =
    {
        ("preparacao.nomes", "preparacao.abrangencia"),
        ("preparacao.sgtic", "preparacao.equipe"),
        ("preparacao.plano-trabalho", "preparacao.metodologia"),
        ("preparacao.aprovacao-plano-trabalho", "preparacao.metodologia"),
        ("preparacao.documentos-referencia", "preparacao.estrategias"),
        ("diagnostico.capacidade", "diagnostico.ambiente-tecnologico"),
        ("diagnostico.plano-levantamento", "diagnostico.necessidades-tic"),
        ("diagnostico.necessidades-informacao", "diagnostico.necessidades-tic"),
        ("diagnostico.inventario", "diagnostico.necessidades-tic"),
        ("diagnostico.aprovacao-inventario", "diagnostico.necessidades-tic"),
        ("planejamento.orcamento", "planejamento.pessoas"),
        ("plano-acompanhamento.plano-execucao", "plano-acompanhamento.quem-acompanha"),
        ("plano-acompanhamento.plano-avaliacao", "plano-acompanhamento.plano-monitoramento"),
        ("plano-acompanhamento.plano-acompanhamento", "plano-acompanhamento.plano-monitoramento"),
        ("plano-acompanhamento.aprovacao-acompanhamento", "plano-acompanhamento.plano-monitoramento"),
        ("avaliacao-intermediaria.resultados-intermediarios", "avaliacao-intermediaria.avaliacao-comite"),
        ("avaliacao-intermediaria.comparacao-metas", "avaliacao-intermediaria.avaliacao-comite"),
        ("fechamento.indicadores-finais", "fechamento.licoes-aprendidas"),
        ("fechamento.aprovacao-comite", "fechamento.aprovacao-autoridade")
    };

    /// <summary>O modelo inicial da versão 8 (o JSON de antes da revisão), guardado nos testes.</summary>
    private static PeSeedModelo SeedV8() =>
        PeCarregadorModelo.LerSeed(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "modelo-inicial-v8.json")));

    /// <summary>O modelo do documento com o passo de cada capítulo como era na versão 8.</summary>
    private static PeSeedDocumentos DocumentosV8()
    {
        var antes = new Dictionary<(string Tipo, string Capitulo), string>
        {
            [("pdtic", "documentos_referencia")] = "preparacao.documentos-referencia",
            [("pdtic", "diagnostico_capacidade")] = "diagnostico.capacidade",
            [("pdtic", "necessidades_levantamento")] = "diagnostico.necessidades-informacao",
            [("pdtic", "orcamento")] = "planejamento.orcamento",
            [("pdtic", "anexo_plano_trabalho")] = "preparacao.plano-trabalho",
            [("ra", "avaliacao_metas")] = "avaliacao-intermediaria.resultados-intermediarios",
            [("ra", "execucao_orcamentaria")] = "avaliacao-intermediaria.comparacao-metas",
            [("ra", "pessoas")] = "avaliacao-intermediaria.comparacao-metas"
        };
        var documentos = PeDocSeed.Ler();
        void Trocar(string tipo, IEnumerable<PeSeedDocCapitulo> capitulos)
        {
            foreach (var capitulo in capitulos)
            {
                if (antes.TryGetValue((tipo, capitulo.Chave), out var passo)) capitulo.Passo = passo;
                Trocar(tipo, capitulo.Subcapitulos);
            }
        }
        foreach (var modelo in documentos.Modelos) Trocar(modelo.Tipo, modelo.Capitulos);
        return documentos;
    }

    /// <summary>
    /// O retrato do modelo (só os itens do sistema, sem ids): etapas, passos (não apagados), seções,
    /// campos e opções com os textos, a situação em cada nível, a ordem e o tipo, e o passo de cada
    /// capítulo do documento.
    /// </summary>
    private static List<string> Retrato(AppDbContext context)
    {
        var niveis = context.PeNiveis.AsNoTracking().ToDictionary(n => n.Id, n => n.Codigo);
        string Sit(IEnumerable<IPeSituacaoNivel> linhas) =>
            string.Join(",", linhas.OrderBy(l => niveis[l.NivelId]).Select(l => $"{niveis[l.NivelId]}={l.Situacao}"));
        var saida = new List<string>();
        saida.AddRange(context.PeNiveis.AsNoTracking().OrderBy(n => n.Ordem).Select(n => $"nivel {n.Codigo} {n.Nome} {n.Descricao}"));
        var etapas = context.PeEtapas.AsNoTracking().ToDictionary(e => e.Id);
        saida.AddRange(etapas.Values.OrderBy(e => e.Ordem).Select(e => $"etapa {e.Chave} {e.Ordem} {e.Titulo} {e.Descricao} {e.ReferenciaGuia}"));
        var passos = context.PePassos.AsNoTracking().Include(p => p.Niveis).Where(p => p.Sistema && p.ExcluidoEm == null).ToList();
        var chaveDoPasso = passos.ToDictionary(p => p.Id, p => p.Chave);
        saida.AddRange(passos.OrderBy(p => etapas[p.EtapaId].Ordem).ThenBy(p => p.Ordem).Select(p =>
            $"passo {etapas[p.EtapaId].Chave} {p.Ordem} {p.Chave} {p.Tipo} {p.Travado} {p.IncisoDecreto} {p.AceitaNaoSeAplica} [{Sit(p.Niveis)}] "
            + $"{p.Titulo} | {p.OQueFazer} | {p.BaseLegal} | {p.ReferenciaGuia}"));
        var secoes = context.PeSecoes.AsNoTracking().Include(s => s.Niveis).Where(s => s.Sistema).ToList();
        saida.AddRange(secoes.OrderBy(s => s.Chave).Select(s =>
            $"secao {s.Chave} {(s.PassoId is long id ? chaveDoPasso.GetValueOrDefault(id, "?") : "-")} {s.Ordem} {s.Tipo} [{Sit(s.Niveis)}] {s.Titulo} | {s.Ajuda}"));
        var chaveDaSecao = secoes.ToDictionary(s => s.Id, s => s.Chave);
        var campos = context.PeCampos.AsNoTracking().Include(c => c.Niveis).Where(c => c.Sistema).ToList();
        saida.AddRange(campos.OrderBy(c => chaveDaSecao[c.SecaoId]).ThenBy(c => c.Ordem).Select(c =>
            $"campo {chaveDaSecao[c.SecaoId]}.{c.Chave} {c.Ordem} {c.Tipo} [{Sit(c.Niveis)}] {c.Rotulo} | {c.Ajuda} | {c.Config} | {c.Largura}"));
        var chaveDoCampo = campos.ToDictionary(c => c.Id, c => $"{chaveDaSecao[c.SecaoId]}.{c.Chave}");
        saida.AddRange(context.PeOpcoes.AsNoTracking().Where(o => o.Sistema).ToList()
            .OrderBy(o => chaveDoCampo[o.CampoId]).ThenBy(o => o.Ordem).Select(o => $"opcao {chaveDoCampo[o.CampoId]}.{o.Valor} {o.Rotulo} {o.Ativa}"));
        var modelos = context.PeDocModelos.AsNoTracking().ToDictionary(m => m.Id, m => m.Tipo);
        saida.AddRange(context.PeDocCapitulos.AsNoTracking().ToList().OrderBy(c => modelos[c.ModeloId]).ThenBy(c => c.Chave)
            .Select(c => $"capitulo {modelos[c.ModeloId]}.{c.Chave} {c.PassoChave}"));
        return saida;
    }

    private async Task CarregarV8Async() => await new PeCarregadorModelo(Context).CarregarAsync(SeedV8(), DocumentosV8());

    private long PdticDaSes()
    {
        var pdtic = new PePdtic { OrgaoId = OrgaoSes.Id, Versao = "1.0", CriadoEm = DateTime.UtcNow, CriadoPor = AutorSemente };
        Context.PePdtics.Add(pdtic);
        Context.SaveChanges();
        return pdtic.Id;
    }

    [Fact]
    public async Task Fixture_EhAVersao8_Com50Passos()
    {
        var v8 = SeedV8();
        Assert.Equal(PeCarregadorModelo.VersaoDoModoLivre, v8.Versao);
        Assert.Equal(50, v8.Etapas.Sum(e => e.Passos.Count));
        var hoje = PeCarregadorModelo.LerSeed();
        Assert.Equal(PeCarregadorModelo.VersaoDaReestruturacao, hoje.Versao);
        Assert.Equal(31, hoje.Etapas.Sum(e => e.Passos.Count));
        // Toda chave de passo da versão 8 continua no JSON ou está na lista dos juntados
        var novas = hoje.Etapas.SelectMany(e => e.Passos).Select(p => p.Chave).ToHashSet();
        var antigas = v8.Etapas.SelectMany(e => e.Passos).Select(p => p.Chave).ToList();
        Assert.All(antigas.Where(c => !novas.Contains(c)), c => Assert.Contains(PassosJuntados, j => j.Antigo == c));
        Assert.All(PassosJuntados, j => Assert.Contains(j.Novo, novas));
        // As seções não mudam de chave: as mesmas nas duas versões
        Assert.Equal(v8.Etapas.SelectMany(e => e.Passos).SelectMany(p => p.Secoes).Select(s => s.Chave).OrderBy(c => c),
            hoje.Etapas.SelectMany(e => e.Passos).SelectMany(p => p.Secoes).Select(s => s.Chave).OrderBy(c => c));
        await Task.CompletedTask;
    }

    [Fact]
    public async Task BancoComAVersao8_FicaIgualAoBancoNovo()
    {
        await CarregarV8Async();
        Assert.Equal(50, Context.PePassos.Count());
        Context.ChangeTracker.Clear();

        var resultado = await new PeCarregadorModelo(Context).CarregarAsync();
        Assert.True(resultado.Executou);
        Assert.Equal((PeCarregadorModelo.VersaoDoModoLivre, PeCarregadorModelo.VersaoDaReestruturacao), (resultado.VersaoAnterior, resultado.Versao));
        Assert.Equal(0, resultado.Passos + resultado.Secoes + resultado.Campos + resultado.Opcoes + resultado.Etapas + resultado.Niveis);
        Assert.True(resultado.Reestruturados > 0);
        Assert.True(resultado.Textos > 0);
        Context.ChangeTracker.Clear();

        using var novo = new PeBancoVazio();
        await new PeCarregadorModelo(novo.Context).CarregarAsync();
        Assert.Equal(Retrato(novo.Context), Retrato(Context));

        // Os passos que sumiram: apagados (exclusão lógica), com a chave guardada
        foreach (var (antigo, _) in PassosJuntados)
            Assert.NotNull(Passo(antigo).ExcluidoEm);
        Assert.Equal(31, Context.PePassos.Count(p => p.ExcluidoEm == null));
        Assert.Equal(50, Context.PePassos.Count());
        Assert.Equal(PeCarregadorModelo.LerSeed().Versao.ToString(),
            Context.PeConfiguracoes.AsNoTracking().Single(c => c.Chave == PeConfiguracao.ChaveVersaoModelo).Valor);

        // A carga seguinte não faz nada
        Context.ChangeTracker.Clear();
        Assert.False((await new PeCarregadorModelo(Context).CarregarAsync()).Executou);
    }

    [Fact]
    public async Task BancoComAVersao8_OQueEhDoOrgaoVaiParaOPassoNovo_EOQueEraSoDoPassoQueSumiuSai()
    {
        await CarregarV8Async();
        var id = PdticDaSes();
        var agora = DateTime.UtcNow;
        long P(string chave) => Passo(chave).Id;

        // Um registro numa seção que muda de passo (o dicionário de nomes)
        var registro = new PeRegistro
        {
            SecaoId = Secao("nomes").Id, PdticId = id, Ordem = 1, Dados = "{\"comite\":\"SGTIC\"}", CriadoEm = agora, CriadoPor = AutorSemente
        };
        Context.PeRegistros.Add(registro);
        // Um comentário num passo que some
        var comentario = new PeComentario
        {
            PdticId = id, PassoId = P("preparacao.nomes"), Texto = "Confira os nomes.", AutorEmail = UserPeSgdi.Email, AutorNome = "Sérgio",
            CriadoEm = agora
        };
        Context.PeComentarios.Add(comentario);
        // "Não se aplica" num passo que some; validações num passo que some, num que recebe seções e num que não muda
        Context.PePdticPassos.Add(new PePdticPasso
        {
            PdticId = id, PassoId = P("preparacao.documentos-referencia"), NaoSeAplica = true, Justificativa = "Sem documentos.",
            MarcadoEm = agora, MarcadoPor = UserOrgaoSes.Email
        });
        foreach (var chave in new[] { "preparacao.sgtic", "preparacao.equipe", "diagnostico.ativos" })
        {
            Context.PePdticPassos.Add(new PePdticPasso { PdticId = id, PassoId = P(chave), MarcadoEm = agora, MarcadoPor = UserOrgaoSes.Email });
            Context.PePdticPassosValidacao.Add(new PePdticPassoValidacao { PdticId = id, PassoId = P(chave), ValidadoEm = agora, ValidadoPor = UserOrgaoSes.Email });
        }
        // A forma escolhida e os ajustes do órgão
        Context.PeOrgaosPassoDetalhe.Add(new PeOrgaoPassoDetalhe
        {
            OrgaoId = OrgaoSes.Id, PassoId = P("diagnostico.necessidades-informacao"), NivelId = NivelId("intermediario"), AlteradoEm = agora,
            AlteradoPor = EmailAdmin
        });
        Context.PeOrgaosPassoDetalhe.Add(new PeOrgaoPassoDetalhe
        {
            OrgaoId = OrgaoSes.Id, PassoId = P("preparacao.metodologia"), NivelId = NivelId("avancado"), AlteradoEm = agora, AlteradoPor = EmailAdmin
        });
        Context.PeOrgaosAjuste.Add(new PeOrgaoAjuste
        {
            OrgaoId = OrgaoSes.Id, AlvoTipo = PeDominios.AlvoAjuste.Passo, AlvoId = P("planejamento.orcamento"), Situacao = "opcional",
            CriadoEm = agora, CriadoPor = EmailAdmin
        });
        Context.PeOrgaosAjuste.Add(new PeOrgaoAjuste
        {
            OrgaoId = OrgaoSes.Id, AlvoTipo = PeDominios.AlvoAjuste.Secao, AlvoId = Secao("orcamento_loa").Id, Situacao = "opcional",
            CriadoEm = agora, CriadoPor = EmailAdmin
        });
        await Context.SaveChangesAsync();
        // O que o administrador criou: um passo na etapa 1 e uma seção num passo que some
        var criado = await Modelo.CriarPassoAsync(new PePassoCriarDTO { EtapaId = Passo("preparacao.nomes").EtapaId, Titulo = "Registre os parceiros" },
            EmailAdmin);
        var secaoCriada = await Modelo.CriarSecaoAsync(new PeSecaoCriarDTO { PassoId = P("preparacao.nomes"), Titulo = "Contatos", Tipo = "tabela" },
            EmailAdmin);
        Context.ChangeTracker.Clear();

        await new PeCarregadorModelo(Context).CarregarAsync();
        Context.ChangeTracker.Clear();

        // O registro fica (a seção não muda de chave) e aparece no passo novo
        var nomes = Secao("nomes");
        Assert.Equal(P("preparacao.abrangencia"), nomes.PassoId);
        Assert.Equal(nomes.Id, Context.PeRegistros.AsNoTracking().Single(r => r.Id == registro.Id).SecaoId);
        var trilha = await TrilhaAsync(OrgaoSes);
        Assert.Equal(new[] { "abrangencia", "nomes" }, NaTrilha(trilha, "preparacao.abrangencia")!.Secoes.Select(s => s.Chave).Take(2));
        // O comentário vai para o passo novo
        Assert.Equal(P("preparacao.abrangencia"), Context.PeComentarios.AsNoTracking().Single(c => c.Id == comentario.Id).PassoId);
        // O "não se aplica" e a validação do passo que sumiu saem; a validação do passo que recebeu seções sai (a linha
        // fica); a do passo que não mudou fica
        var linhas = Context.PePdticPassos.AsNoTracking().Select(p => p.PassoId).ToList();
        Assert.DoesNotContain(P("preparacao.documentos-referencia"), linhas);
        Assert.DoesNotContain(P("preparacao.sgtic"), linhas);
        Assert.Contains(P("preparacao.equipe"), linhas);
        Assert.Equal(new[] { P("diagnostico.ativos") }, Context.PePdticPassosValidacao.AsNoTracking().Select(v => v.PassoId).ToList());
        // A forma do passo que sumiu sai; a do passo que ficou, fica
        Assert.Equal(new[] { P("preparacao.metodologia") }, Context.PeOrgaosPassoDetalhe.AsNoTracking().Select(d => d.PassoId).ToList());
        // O ajuste do passo que sumiu sai; o da seção fica
        var ajustes = Context.PeOrgaosAjuste.AsNoTracking().ToList();
        Assert.Equal((PeDominios.AlvoAjuste.Secao, Secao("orcamento_loa").Id), (Assert.Single(ajustes).AlvoTipo, ajustes[0].AlvoId));
        // O passo do administrador fica, depois dos do sistema; a seção dele vai para o passo que recebeu as do passo que sumiu
        var doAdmin = Passo(criado.Chave);
        Assert.Null(doAdmin.ExcluidoEm);
        Assert.Equal(6, doAdmin.Ordem);
        var contatos = Secao(secaoCriada.Chave);
        Assert.Equal(P("preparacao.abrangencia"), contatos.PassoId);
        Assert.Equal(3, contatos.Ordem);
        // Os capítulos do documento apontam para os passos novos
        Assert.Equal("preparacao.estrategias", Context.PeDocCapitulos.AsNoTracking().Single(c => c.Chave == "documentos_referencia").PassoChave);
        Assert.Equal("planejamento.pessoas", Context.PeDocCapitulos.AsNoTracking().Single(c => c.Chave == "orcamento").PassoChave);
        Assert.DoesNotContain(Context.PeDocCapitulos.AsNoTracking().Select(c => c.PassoChave).ToList(),
            c => c != null && PassosJuntados.Any(j => j.Antigo == c));
    }

    [Fact]
    public async Task MesmaVersao_ComOJsonMudado_RegravaSoOsTextos_EDepoisNaoFazNada()
    {
        await new PeCarregadorModelo(Context).CarregarAsync();
        var seed = PeCarregadorModelo.LerSeed();
        var passo = seed.Etapas[0].Passos[0];
        passo.Titulo = "Defina o alcance do PDTIC";
        passo.OQueFazer = string.Empty;
        var secao = passo.Secoes[0];
        secao.Ajuda = "Uma ajuda nova.";
        var campo = secao.Campos[0];
        campo.Rotulo = "Alcance";
        campo.Opcoes[0].Rotulo = "O órgão inteiro";
        seed.Niveis[0].Nome = "Inicial";
        seed.Etapas[0].Descricao = "Uma descrição nova.";
        // Um campo novo sem subir a versão: estrutura nova pede versão nova (não entra)
        secao.Campos.Add(new PeSeedCampo { Chave = "observacao_nova", Rotulo = "Observação", Tipo = PeDominios.TipoCampo.TextoCurto });
        Context.ChangeTracker.Clear();

        var resultado = await new PeCarregadorModelo(Context).CarregarAsync(seed);
        Assert.True(resultado.Executou);
        Assert.Equal((PeCarregadorModelo.VersaoDaReestruturacao, PeCarregadorModelo.VersaoDaReestruturacao), (resultado.VersaoAnterior, resultado.Versao));
        Assert.Equal(6, resultado.Textos);
        Assert.Equal(0, resultado.Campos);
        Context.ChangeTracker.Clear();

        var gravado = Passo("preparacao.abrangencia");
        Assert.Equal(("Defina o alcance do PDTIC", string.Empty, PeCarregadorModelo.Autor), (gravado.Titulo, gravado.OQueFazer, gravado.AlteradoPor));
        Assert.Equal("Uma ajuda nova.", Secao("abrangencia").Ajuda);
        Assert.Equal("Alcance", Campo("abrangencia", "tipo_abrangencia").Rotulo);
        Assert.Equal("O órgão inteiro", Opcao("abrangencia", "tipo_abrangencia", campo.Opcoes[0].Valor).Rotulo);
        Assert.Equal("Inicial", Nivel("basico").Nome);
        Assert.Equal("Uma descrição nova.", Context.PeEtapas.AsNoTracking().Single(e => e.Chave == "preparacao").Descricao);
        Assert.False(Context.PeCampos.Any(c => c.Chave == "observacao_nova"));
        Assert.Equal(JsonSerializer.Serialize(PeCarregadorModelo.Resumo(seed)),
            Context.PeConfiguracoes.AsNoTracking().Single(c => c.Chave == PeConfiguracao.ChaveHashModelo).Valor);

        // O mesmo JSON de novo: nada; o JSON da aplicação de novo: os textos voltam
        Assert.False((await new PeCarregadorModelo(Context).CarregarAsync(seed)).Executou);
        Context.ChangeTracker.Clear();
        var deVolta = await new PeCarregadorModelo(Context).CarregarAsync();
        Assert.Equal(6, deVolta.Textos);
        Assert.Equal(PeCarregadorModelo.LerSeed().Etapas[0].Passos[0].Titulo, Passo("preparacao.abrangencia").Titulo);
    }

    [Fact]
    public async Task TextoDoAdministrador_ERegravadoQuandoOJsonMuda_ENaoAntes()
    {
        await new PeCarregadorModelo(Context).CarregarAsync();
        await Modelo.AtualizarPassoAsync(Passo("preparacao.equipe").Id,
            new PePassoAtualizarDTO { Titulo = "Monte o SGTIC e a equipe", Informados = new HashSet<string> { "Titulo" } }, EmailAdmin);
        Context.ChangeTracker.Clear();

        // A carga com o mesmo JSON não mexe no que o administrador mudou
        Assert.False((await new PeCarregadorModelo(Context).CarregarAsync()).Executou);
        Assert.Equal("Monte o SGTIC e a equipe", Passo("preparacao.equipe").Titulo);

        // O JSON mudou (outro texto, em outro item): todos os textos voltam aos do JSON
        var seed = PeCarregadorModelo.LerSeed();
        seed.Etapas[1].Passos[0].OQueFazer = "Outro texto.";
        Context.ChangeTracker.Clear();
        var resultado = await new PeCarregadorModelo(Context).CarregarAsync(seed);
        Assert.Equal(2, resultado.Textos);
        Assert.Equal(seed.Etapas[0].Passos[1].Titulo, Passo("preparacao.equipe").Titulo);
    }

    [Fact]
    public void Json_OQueFazerVazio_EhAceito_ETituloVazioNao()
    {
        var seed = PeCarregadorModelo.LerSeed();
        seed.Etapas[0].Passos[0].OQueFazer = string.Empty;
        PeCarregadorModelo.ValidarSeed(seed);
        seed.Etapas[0].Passos[0].OQueFazer = null;
        PeCarregadorModelo.ValidarSeed(seed);

        seed.Etapas[0].Passos[0].Titulo = " ";
        Assert.Throws<InvalidOperationException>(() => PeCarregadorModelo.ValidarSeed(seed));
    }

    [Fact]
    public async Task PassoNovo_ComOQueFazerVazio_GravaTextoVazio()
    {
        var seed = PeCarregadorModelo.LerSeed();
        seed.Etapas[0].Passos[0].OQueFazer = null;
        await new PeCarregadorModelo(Context).CarregarAsync(seed);
        Assert.Equal(string.Empty, Passo("preparacao.abrangencia").OQueFazer);
        Assert.Equal(string.Empty, NaTrilha(await TrilhaAsync(OrgaoSes), "preparacao.abrangencia")!.OQueFazer);
    }
}
