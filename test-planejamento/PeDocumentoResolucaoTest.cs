using System.Text.Json;
using api.Planejamento;
using Microsoft.EntityFrameworkCore;
using Models.Planejamento;
using service;
using service.Planejamento;
using Xunit;

namespace test.planejamento;

/// <summary>
/// Resolução do documento do órgão (GET pdtic/{id}/documento): os capítulos do nível do órgão
/// numerados pela posição, os ocultos e os travados, os marcadores com e sem valor, o texto do
/// órgão e o do modelo com o aviso "o modelo mudou", e os dados das tabelas, das listas de
/// tema, da SWOT, do PGIA e do fluxo.
/// </summary>
public class PeDocumentoResolucaoTest : PeDocumentoTestBase
{
    private static string?[] Numeros(PeDocumentoResponse documento, params string[] chaves) =>
        chaves.Select(c => Cap(documento, c).Numero).ToArray();

    // ── Capítulos pelo nível ──────────────────────────────────────────────────

    [Fact]
    public async Task Basico_SoOsCapitulosDoNivel_NumeradosPelaPosicao()
    {
        var pdtic = await AbrirSesAsync();
        var documento = await DocumentoAsync(pdtic.Id);

        Assert.Equal(pdtic.Id, documento.PdticId);
        Assert.Equal("1.0", documento.Versao);
        Assert.Equal("SES", documento.OrgaoSigla);
        Assert.Equal("Plano Diretor de Tecnologia da Informação e Comunicação", documento.Titulo);
        Assert.True(documento.PodeEditar);

        // Passo desligado no Básico: o capítulo (e o subcapítulo) não vem
        foreach (var fora in new[] { "documentos_referencia", "diagnostico_pdtic_anterior", "diagnostico_referencial", "diagnostico_swot",
                     "diagnostico_capacidade", "necessidades_levantamento", "necessidades_criterios", "pessoas", "orcamento", "riscos",
                     "fatores_criticos", "anexo_nao_priorizadas", "anexo_plano_trabalho" })
            Assert.Null(CapOuNulo(documento, fora));

        Assert.All(Numeros(documento, "capa", "folha_rosto", "historico_versoes", "sumario", "apresentacao"), Assert.Null);
        Assert.Equal(new[] { "1", "2", "3", "4", "5", "5.1", "6", "7", "8", "8.1", "9", "10", "11", "12", "13", "14", "15", "16" },
            Numeros(documento, "introducao", "termos", "metodologia", "principios", "diagnostico", "diagnostico_organizacao", "ativos",
                "alinhamento", "necessidades", "necessidades_priorizadas", "contratacoes", "seguranca", "transformacao_digital",
                "metas_indicadores", "sistemas_ia", "governanca_dados", "revisao_acompanhamento", "conclusao"));
        Assert.Null(Cap(documento, "anexos").Numero);
        Assert.Equal(2, Cap(documento, "diagnostico_organizacao").Nivel);

        // Os nove conteúdos vêm travados, com o inciso e o número do passo na trilha do órgão
        var travados = documento.Capitulos.Where(c => c.Travado).ToList();
        Assert.Equal(new[] { "I", "II", "III", "IV", "V", "VI", "VII", "VIII", "IX" }, travados.Select(c => c.IncisoDecreto));
        Assert.All(travados, c => Assert.True(c.Obrigatorio));
        var trilha = await TrilhaAsync(OrgaoSes);
        Assert.Equal(NaTrilha(trilha, "diagnostico.ativos")!.Numero, Cap(documento, "ativos").PassoNumero);
        Assert.Equal("diagnostico.ativos", Cap(documento, "ativos").PassoChave);
    }

    [Fact]
    public async Task Avancado_OsVinteEUmCapitulos_ComANumeracaoDoPlano()
    {
        await DefinirNivelDoOrgaoAsync(OrgaoSes, "avancado");
        var pdtic = await AbrirSesAsync();
        var documento = await DocumentoAsync(pdtic.Id);

        Assert.Equal(Enumerable.Range(1, 21).Select(n => n.ToString()),
            documento.Capitulos.Where(c => c.Nivel == 1 && c.Numero != null).Select(c => c.Numero));
        Assert.Equal(new[] { "6", "6.1", "6.2", "6.3", "6.4", "6.5", "9", "9.1", "9.2", "9.3", "14", "21" },
            Numeros(documento, "diagnostico", "diagnostico_organizacao", "diagnostico_pdtic_anterior", "diagnostico_referencial",
                "diagnostico_swot", "diagnostico_capacidade", "necessidades", "necessidades_levantamento", "necessidades_criterios",
                "necessidades_priorizadas", "sistemas_ia", "conclusao"));
        Assert.Equal("2.4", Cap(documento, "ativos").PassoNumero);
        Assert.NotNull(CapOuNulo(documento, "anexo_plano_trabalho"));
        Assert.Null(Cap(documento, "anexo_plano_trabalho").Numero);
    }

    // ── Oculto, obrigatório e título próprio ──────────────────────────────────

    [Fact]
    public async Task OpcionalSeEsconde_ENumeracaoAnda_TravadoEObrigatorio409()
    {
        var pdtic = await AbrirSesAsync();
        var ctx = await Orgao();
        var termos = CapituloDoModelo("termos");

        var oculto = await Documentos.AtualizarCapituloAsync(pdtic.Id, termos.Id, new PeDocCapituloOrgaoDTO { Oculto = true, Informados = new HashSet<string> { "Oculto" } }, ctx);
        Assert.True(oculto.Oculto);
        Assert.Null(oculto.Numero);
        Assert.Empty(oculto.Blocos);

        var documento = await DocumentoAsync(pdtic.Id);
        Assert.True(Cap(documento, "termos").Oculto);
        Assert.Equal(new[] { "1", "2" }, Numeros(documento, "introducao", "metodologia"));

        // Pai oculto: os subcapítulos não vêm
        await Documentos.AtualizarCapituloAsync(pdtic.Id, CapituloDoModelo("anexos").Id, new PeDocCapituloOrgaoDTO { Oculto = true, Informados = new HashSet<string> { "Oculto" } }, ctx);
        documento = await DocumentoAsync(pdtic.Id);
        Assert.Null(CapOuNulo(documento, "anexo_outros"));

        // Mostrar de novo
        var mostrado = await Documentos.AtualizarCapituloAsync(pdtic.Id, termos.Id, new PeDocCapituloOrgaoDTO { Oculto = false, Informados = new HashSet<string> { "Oculto" } }, ctx);
        Assert.False(mostrado.Oculto);
        Assert.Equal("2", mostrado.Numero);
        Assert.NotEmpty(mostrado.Blocos);

        // Travado (os nove) e obrigatório não se escondem
        var travado = await Assert.ThrowsAsync<ApiException>(() => Documentos.AtualizarCapituloAsync(pdtic.Id, CapituloDoModelo("ativos").Id,
            new PeDocCapituloOrgaoDTO { Oculto = true, Informados = new HashSet<string> { "Oculto" } }, ctx));
        Assert.Equal((int)ErrorCode.PeDocCapituloObrigatorio, travado.Error.Code);
        Assert.Contains("art. 12, § 2º", travado.Error.Message);
        var obrigatorio = await Assert.ThrowsAsync<ApiException>(() => Documentos.AtualizarCapituloAsync(pdtic.Id, CapituloDoModelo("introducao").Id,
            new PeDocCapituloOrgaoDTO { Oculto = true, Informados = new HashSet<string> { "Oculto" } }, ctx));
        Assert.Equal((int)ErrorCode.PeDocCapituloObrigatorio, obrigatorio.Error.Code);

        // Capítulo fora do documento do órgão (passo desligado no Básico): 404
        var fora = await Assert.ThrowsAsync<ApiException>(() => Documentos.AtualizarCapituloAsync(pdtic.Id, CapituloDoModelo("riscos").Id,
            new PeDocCapituloOrgaoDTO { Oculto = true, Informados = new HashSet<string> { "Oculto" } }, ctx));
        Assert.Equal((int)ErrorCode.PeDocCapituloNaoEncontrado, fora.Error.Code);
    }

    [Fact]
    public async Task TituloProprio_Renomeia_EVazioVoltaAoDoModelo()
    {
        var pdtic = await AbrirSesAsync();
        var ctx = await Orgao();
        var ativos = CapituloDoModelo("ativos");

        var renomeado = await Documentos.AtualizarCapituloAsync(pdtic.Id, ativos.Id,
            new PeDocCapituloOrgaoDTO { TituloProprio = "  Inventário de TIC  ", Informados = new HashSet<string> { "TituloProprio" } }, ctx);
        Assert.Equal("Inventário de TIC", renomeado.Titulo);
        Assert.Equal("Inventário de TIC", renomeado.TituloProprio);
        Assert.Equal("Soluções e ativos de TIC", renomeado.TituloModelo);
        Assert.True(renomeado.Travado);

        var devolvido = await Documentos.AtualizarCapituloAsync(pdtic.Id, ativos.Id,
            new PeDocCapituloOrgaoDTO { TituloProprio = "", Informados = new HashSet<string> { "TituloProprio" } }, ctx);
        Assert.Null(devolvido.TituloProprio);
        Assert.Equal("Soluções e ativos de TIC", devolvido.Titulo);

        var longo = await Assert.ThrowsAsync<ApiException>(() => Documentos.AtualizarCapituloAsync(pdtic.Id, ativos.Id,
            new PeDocCapituloOrgaoDTO { TituloProprio = new string('x', 201), Informados = new HashSet<string> { "TituloProprio" } }, ctx));
        Assert.Equal((int)ErrorCode.PeDadosInvalidos, longo.Error.Code);
    }

    // ── Marcadores ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Marcadores_SemValorDestacados_EComValorResolvidos()
    {
        var pdtic = await AbrirSesAsync();

        var antes = await DocumentoAsync(pdtic.Id);
        var folha = Texto(antes, "folha_rosto");
        Assert.Equal(new[] { "nomes.autoridade", "nomes.autoridade_cargo", "nomes.comite", "nomes.equipe", "nomes.unidade_tic" },
            folha.MarcadoresSemValor.OrderBy(m => m));
        // Na prévia, o marcador sem valor continua escrito (o front destaca)
        Assert.Contains("{nomes.comite}", TextoDe(folha.TextoResolvido));
        Assert.Contains("{nomes.comite}", TextoDe(folha.TextoBruto));
        Assert.Contains("Secretaria de Estado de Saúde", TextoDe(folha.TextoResolvido));
        Assert.Equal(new[] { "vigencia.fim", "vigencia.inicio" }, Texto(antes, "capa").MarcadoresSemValor.OrderBy(m => m));

        await PreencherNomesAsync(pdtic.Id);
        await PreencherAbrangenciaAsync(pdtic.Id);
        var depois = await DocumentoAsync(pdtic.Id);

        folha = Texto(depois, "folha_rosto");
        Assert.Empty(folha.MarcadoresSemValor);
        Assert.Contains("Secretária de Estado de Saúde: Maria da Silva", TextoDe(folha.TextoResolvido));
        Assert.Contains("Subcomitê Gestor de TIC da Saúde", TextoDe(folha.TextoResolvido));
        Assert.Contains("{nomes.comite}", TextoDe(folha.TextoBruto));
        Assert.Contains("Vigência: de 01/01/2026 a 31/12/2029", TextoDe(Texto(depois, "capa").TextoResolvido));
        Assert.Contains("Versão 1.0", TextoDe(Texto(depois, "capa").TextoResolvido));
    }

    [Fact]
    public async Task Marcadores_SiglaDoDicionario_ENomeCriadoPeloAdministrador()
    {
        var pdtic = await AbrirSesAsync();
        var campo = await Modelo.CriarCampoAsync(new PeCampoCriarDTO { SecaoId = Secao("nomes").Id, Chave = "ouvidoria", Rotulo = "Nome da ouvidoria", Tipo = "texto_curto" }, EmailAdmin);
        await Modelo.DefinirSituacaoCampoAsync(campo.Id, Todos("opcional"), EmailAdmin);
        await PreencherNomesAsync(pdtic.Id, new { sigla_orgao = "SES-DF", ouvidoria = "Ouvidoria da Saúde" });

        var texto = Doc(Paragrafo("{orgao.sigla} · {nomes.ouvidoria} · {pdtic.versao} · {inventado.aqui}"));
        var bloco = await Documentos.SalvarTextoAsync(pdtic.Id, BlocoDoModelo("introducao").Id, Json(texto), await Orgao());

        Assert.Equal("SES-DF · Ouvidoria da Saúde · 1.0 · {inventado.aqui}", TextoDe(bloco.TextoResolvido));
        Assert.Empty(bloco.MarcadoresSemValor);
    }

    // ── Texto do órgão e do modelo ────────────────────────────────────────────

    [Fact]
    public async Task TextoDoOrgao_Fica_ModeloMudou_EVoltarAoModelo()
    {
        var pdtic = await AbrirSesAsync();
        var seec = await AbrirSeecAsync();
        var blocoId = BlocoDoModelo("introducao").Id;

        var editado = await Documentos.SalvarTextoAsync(pdtic.Id, blocoId, Json(Doc(Paragrafo("Texto do órgão sobre o {orgao.sigla}."))), await Orgao());
        Assert.True(editado.EditadoPeloOrgao);
        Assert.False(editado.ModeloMudou);
        Assert.Null(editado.TextoModeloAtual);
        Assert.Equal(UserOrgaoSes.Email, editado.EditadoPor);
        Assert.NotNull(editado.EditadoEm);
        Assert.Equal("Texto do órgão sobre o SES.", TextoDe(editado.TextoResolvido));

        // O administrador muda o texto padrão: vale na hora para quem não editou
        await ModeloDoc.AtualizarBlocoAsync(blocoId, new PeDocBlocoAtualizarDTO { Config = Json(new { Texto = Doc(Paragrafo("Novo texto padrão do {orgao.nome}.")) }) }, await Admin());

        var daSeec = Texto(await DocumentoAsync(seec.Id, UserOrgaoSeec), "introducao");
        Assert.False(daSeec.EditadoPeloOrgao);
        Assert.Equal("Novo texto padrão do Secretaria de Estado de Economia.", TextoDe(daSeec.TextoResolvido));

        // Quem editou fica com o próprio texto e o aviso, com o texto novo ao lado
        var daSes = Texto(await DocumentoAsync(pdtic.Id), "introducao");
        Assert.True(daSes.EditadoPeloOrgao);
        Assert.True(daSes.ModeloMudou);
        Assert.Equal("Texto do órgão sobre o SES.", TextoDe(daSes.TextoResolvido));
        Assert.Equal("Novo texto padrão do Secretaria de Estado de Saúde.", TextoDe(daSes.TextoModeloAtual));

        // "Manter o meu texto": o mesmo texto de novo passa a valer sobre o modelo atual e o aviso some
        var mantido = await Documentos.SalvarTextoAsync(pdtic.Id, blocoId, daSes.TextoBruto!.Value, await Orgao());
        Assert.True(mantido.EditadoPeloOrgao);
        Assert.False(mantido.ModeloMudou);
        Assert.Null(mantido.TextoModeloAtual);
        Assert.Equal("Texto do órgão sobre o SES.", TextoDe(mantido.TextoResolvido));
        Assert.False(Texto(await DocumentoAsync(pdtic.Id), "introducao").ModeloMudou);

        // "Usar o texto do modelo"
        var restaurado = await Documentos.RestaurarTextoAsync(pdtic.Id, blocoId, await Orgao());
        Assert.False(restaurado.EditadoPeloOrgao);
        Assert.False(restaurado.ModeloMudou);
        Assert.Null(restaurado.EditadoPor);
        Assert.Equal("Novo texto padrão do Secretaria de Estado de Saúde.", TextoDe(restaurado.TextoResolvido));
        Assert.Empty(Context.PeDocOrgaoBlocos.AsNoTracking().Where(o => o.PdticId == pdtic.Id));
        // De novo: nada a desfazer
        Assert.False((await Documentos.RestaurarTextoAsync(pdtic.Id, blocoId, await Orgao())).EditadoPeloOrgao);
    }

    [Fact]
    public async Task TextoIgualAoDoModelo_NaoEhEdicao_EVazioDeixaOBlocoEmBranco()
    {
        var pdtic = await AbrirSesAsync();
        var bloco = BlocoDoModelo("conclusao");
        var doModelo = PeDocConfig.TextoDoBloco(PeDocConfig.Ler(bloco.Config))!;

        var igual = await Documentos.SalvarTextoAsync(pdtic.Id, bloco.Id, JsonSerializer.SerializeToElement(doModelo), await Orgao());
        Assert.False(igual.EditadoPeloOrgao);
        Assert.Empty(Context.PeDocOrgaoBlocos.AsNoTracking());

        var vazio = await Documentos.SalvarTextoAsync(pdtic.Id, bloco.Id, Json(Doc(new { type = "paragraph" })), await Orgao());
        Assert.True(vazio.EditadoPeloOrgao);
        Assert.Equal(string.Empty, TextoDe(vazio.TextoResolvido));
    }

    // ── Dados: tabelas, listas, SWOT, PGIA e fluxo ────────────────────────────

    [Fact]
    public async Task Tabela_ColunasDoNivel_Filtro_ECelulaVaziaComTraco()
    {
        await DefinirNivelDoOrgaoAsync(OrgaoSes, "intermediario");
        var pdtic = await AbrirSesAsync();
        object Necessidade(string descricao, bool priorizada) => new
        {
            descricao, tipo = "servico", origem = "swot", areas = "Regulação", gravidade = 5, urgencia = 5, tendencia = 4, priorizada
        };
        await IncluirNoPdticAsync(pdtic.Id, "necessidades", Necessidade("Novo sistema de regulação.", true));
        await IncluirNoPdticAsync(pdtic.Id, "necessidades", Necessidade("Monitores novos.", false));

        var documento = await DocumentoAsync(pdtic.Id);
        var priorizadas = TabelaDe(documento, "necessidades_priorizadas", "necessidades");

        Assert.True(priorizadas.PaginaDeitada);
        var tabela = priorizadas.Tabela!;
        Assert.Equal("Necessidades de TIC", tabela.SecaoTitulo);
        Assert.Equal("tabela", tabela.SecaoTipo);
        // A prioridade simples é só do Básico: some; as outras colunas seguem a ordem do bloco
        Assert.Equal(new[] { "descricao", "tipo", "areas", "objetivo_petic", "prioridade", "valor_estimado" }, tabela.Colunas.Select(c => c.Chave));
        Assert.Equal("Necessidade", tabela.Colunas[0].Rotulo);
        var linha = Assert.Single(tabela.Linhas);
        Assert.Equal("N01", linha.Codigo);
        Assert.Equal("Serviço de TIC", linha.Celulas["tipo"]);
        Assert.Equal("100", linha.Celulas["prioridade"]);
        Assert.Equal("-", linha.Celulas["valor_estimado"]);
        Assert.Equal("-", linha.Celulas["objetivo_petic"]);
        Assert.False(tabela.Vazia);
        Assert.Equal(NaTrilha(await TrilhaAsync(OrgaoSes), "diagnostico.necessidades-tic")!.Numero, tabela.PassoNumero);

        // No anexo, só a não priorizada
        var anexo = TabelaDe(documento, "anexo_nao_priorizadas", "necessidades").Tabela!;
        Assert.Equal("N02", Assert.Single(anexo.Linhas).Codigo);

        // No Básico não há "priorizada": o capítulo mostra todas e o anexo não vem
        await DefinirNivelDoOrgaoAsync(OrgaoSes, "basico");
        documento = await DocumentoAsync(pdtic.Id);
        tabela = TabelaDe(documento, "necessidades_priorizadas", "necessidades").Tabela!;
        Assert.Equal(new[] { "N01", "N02" }, tabela.Linhas.Select(l => l.Codigo));
        Assert.Equal(new[] { "descricao", "tipo", "objetivo_petic", "prioridade_simples" }, tabela.Colunas.Select(c => c.Chave));
        Assert.Null(CapOuNulo(documento, "anexo_nao_priorizadas"));
    }

    [Fact]
    public async Task Formulario_ComTextoRico_VemEmRicos_ESecaoVaziaVemVazia()
    {
        var pdtic = await AbrirSesAsync();
        var documento = await DocumentoAsync(pdtic.Id);
        var vazia = TabelaDe(documento, "diagnostico_organizacao", "diagnostico_ambiente").Tabela!;
        Assert.True(vazia.Vazia);
        Assert.Equal("formulario", vazia.SecaoTipo);

        await IncluirNoPdticAsync(pdtic.Id, "diagnostico_ambiente", new Dictionary<string, object> { ["diagnostico"] = Rico("A TIC tem 40 pessoas.") });
        documento = await DocumentoAsync(pdtic.Id);
        var tabela = TabelaDe(documento, "diagnostico_organizacao", "diagnostico_ambiente").Tabela!;
        var linha = Assert.Single(tabela.Linhas);
        Assert.Null(linha.Codigo);
        Assert.Equal("A TIC tem 40 pessoas.", linha.Celulas["diagnostico"]);
        Assert.Equal("A TIC tem 40 pessoas.", TextoDe(linha.Ricos!["diagnostico"]));
    }

    [Fact]
    public async Task SecaoForaDoDocumento_OBlocoNaoVem()
    {
        var pdtic = await AbrirSesAsync();
        Assert.Contains(Cap(await DocumentoAsync(pdtic.Id), "apresentacao").Blocos, b => b.Tabela?.SecaoChave == "abrangencia");

        await Modelo.AtualizarSecaoAsync(Secao("abrangencia").Id, new PeSecaoAtualizarDTO { NoDocumento = false, Informados = new HashSet<string> { "NoDocumento" } }, EmailAdmin);

        Assert.DoesNotContain(Cap(await DocumentoAsync(pdtic.Id), "apresentacao").Blocos, b => b.Tipo == "tabela_secao");
    }

    [Fact]
    public async Task ListaDoTema_AcoesComSituacao_ETemaSemAcaoComJustificativa()
    {
        var pdtic = await AbrirSesAsync();
        await IncluirNoPdticAsync(pdtic.Id, "acoes", new
        {
            descricao = "Implantar o backup diário.", tema = new[] { "seguranca", "infraestrutura" }, responsavel = "Infraestrutura",
            conclusao = "2027-06-30", situacao = "em_andamento"
        });

        var documento = await DocumentoAsync(pdtic.Id);
        var seguranca = Cap(documento, "seguranca").Blocos.Single(b => b.Tipo == "lista_tema").Lista!;
        Assert.Equal("Segurança da informação e continuidade de serviços (inciso V)", seguranca.Tema);
        var item = Assert.Single(seguranca.Itens);
        Assert.Equal(("A01", "Implantar o backup diário.", "Em andamento"), (item.Codigo, item.Texto, item.Situacao));
        Assert.Null(seguranca.Justificativa);

        var dados = Cap(documento, "governanca_dados").Blocos.Single(b => b.Tipo == "lista_tema").Lista!;
        Assert.Empty(dados.Itens);
        Assert.Null(dados.Justificativa);

        await IncluirNoPdticAsync(pdtic.Id, "temas_sem_acao", new { justificativa_dados = "A PGD/DF ainda será regulamentada." });
        dados = Cap(await DocumentoAsync(pdtic.Id), "governanca_dados").Blocos.Single(b => b.Tipo == "lista_tema").Lista!;
        Assert.Equal("A PGD/DF ainda será regulamentada.", dados.Justificativa);
    }

    [Fact]
    public async Task Swot_QuatroQuadrantes_ComOsCodigos()
    {
        await DefinirNivelDoOrgaoAsync(OrgaoSes, "intermediario");
        var pdtic = await AbrirSesAsync();
        await IncluirNoPdticAsync(pdtic.Id, "swot_forcas", new { descricao = "Equipe experiente." });
        await IncluirNoPdticAsync(pdtic.Id, "swot_fraquezas", new { descricao = "Sistemas legados." });
        await IncluirNoPdticAsync(pdtic.Id, "swot_ameacas", new { descricao = "Ransomware." });

        var swot = Cap(await DocumentoAsync(pdtic.Id), "diagnostico_swot").Blocos.Single(b => b.Tipo == "matriz_swot").Swot!;

        Assert.Equal(new[] { "F01 · Equipe experiente." }, swot.Forcas);
        Assert.Equal(new[] { "D01 · Sistemas legados." }, swot.Fraquezas);
        Assert.Empty(swot.Oportunidades);
        Assert.Equal(new[] { "AM01 · Ransomware." }, swot.Ameacas);
    }

    [Fact]
    public async Task SistemasDeIa_DoPgia_EFluxo_SemDesenhoAteAE6()
    {
        var pdtic = await AbrirSesAsync();
        SistemaPgia(OrgaoSes, "Triagem de exames");

        var documento = await DocumentoAsync(pdtic.Id);
        var pgia = TabelaDe(documento, "sistemas_ia", PeDocConfig.SecaoPgia).Tabela!;
        Assert.Equal(new[] { "nome", "finalidade", "classificacao", "base", "situacao" }, pgia.Colunas.Select(c => c.Chave));
        var linha = Assert.Single(pgia.Linhas);
        Assert.Equal("Triagem de exames", linha.Celulas["nome"]);
        Assert.Equal("Alto Risco", linha.Celulas["classificacao"]);
        Assert.Equal(NaTrilha(await TrilhaAsync(OrgaoSes), "diagnostico.sistemas-ia")!.Numero, pgia.PassoNumero);

        var fluxo = Cap(documento, "metodologia").Blocos.Single(b => b.Tipo == "fluxo").Fluxo!;
        Assert.Equal("elaboracao", fluxo.Chave);
        Assert.Equal("Processo de elaboração do PDTIC (figura 5 do guia)", fluxo.Nome);
        // Desde a E6, o desenho do fluxo do guia (o modelo, sem cópia do órgão)
        Assert.StartsWith("<svg", fluxo.Svg);
        Assert.False(fluxo.Personalizado);
    }

    [Fact]
    public async Task Logotipo_DoDicionarioDeNomes()
    {
        var pdtic = await AbrirSesAsync();
        Assert.Null((await DocumentoAsync(pdtic.Id)).Logotipo);

        var logo = await EnviarArquivoAsync(UserOrgaoSes, "logo.jpg", JpegDeVerdade());
        await PreencherNomesAsync(pdtic.Id, new { logotipo = new { ArquivoId = logo.Id } });

        Assert.Equal($"api/planejamento/arquivos/{logo.Id}", (await DocumentoAsync(pdtic.Id)).Logotipo);
    }

    // ── Quem lê e o modelo que falta ──────────────────────────────────────────

    [Fact]
    public async Task Leitura_QuemVeOOrgao_SoAEquipePodeEditar()
    {
        var pdtic = await AbrirSesAsync();

        Assert.True((await DocumentoAsync(pdtic.Id)).PodeEditar);
        Assert.False((await DocumentoAsync(pdtic.Id, UserConsultaSes)).PodeEditar);
        Assert.False((await DocumentoAsync(pdtic.Id, UserPeSgdi)).PodeEditar);
        Assert.False((await DocumentoAsync(pdtic.Id, UserPeCgtic)).PodeEditar);
        Assert.True((await DocumentoAsync(pdtic.Id, UserAdminGeral)).PodeEditar);

        var outro = await Assert.ThrowsAsync<ApiException>(() => DocumentoAsync(pdtic.Id, UserOrgaoSeec));
        Assert.Equal((int)ErrorCode.PeSemPermissao, outro.Error.Code);
        var inexistente = await Assert.ThrowsAsync<ApiException>(() => DocumentoAsync(999_999));
        Assert.Equal((int)ErrorCode.PePdticNaoEncontrado, inexistente.Error.Code);
    }

    [Fact]
    public async Task SemModeloAtivo_409()
    {
        var pdtic = await AbrirSesAsync();
        var modelo = Context.PeDocModelos.Single();
        modelo.Ativo = false;
        await Context.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<ApiException>(() => DocumentoAsync(pdtic.Id));
        Assert.Equal((int)ErrorCode.PeModeloIndisponivel, ex.Error.Code);
    }
}
