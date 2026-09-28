using System.Text.Json;
using api.Planejamento;
using Microsoft.EntityFrameworkCore;
using Models.Planejamento;
using service;
using service.Planejamento;
using Xunit;

namespace test.planejamento;

/// <summary>
/// Motor de registros (E3): leitura com rótulos, permissões, código pela sequência do dono
/// (sem reaproveitar), validação de cada tipo, obrigatório, guarda e esconde, formulário,
/// ligações (mesmo dono, catálogo, uma ou várias), 409 ao apagar o que está ligado,
/// calculados e o campo de arquivo.
/// </summary>
public class PeRegistroMotorTest : PeReferenciaisTestBase
{
    // ── Leitura e permissões ──────────────────────────────────────────────────

    [Fact]
    public async Task Principios_OsOnzeDoSistema_ComRotulos_EPodeEditarSoQuemAdministra()
    {
        var admin = await Registros.ListarAsync(PeDono.Df, "principio", await Admin());

        Assert.Equal("principio", admin.Secao.Chave);
        Assert.Equal("tabela", admin.Secao.Tipo);
        Assert.Equal("PR", admin.Secao.PrefixoCodigo);
        Assert.Equal(new[] { "texto", "fundamento", "criterio_priorizacao" }, admin.Secao.Campos.Select(c => c.Chave));
        Assert.True(admin.Secao.Campos[0].Obrigatorio);
        Assert.True(admin.Secao.Campos[0].Principal);
        Assert.Equal("larga", admin.Secao.Campos[0].Largura);
        Assert.False(admin.Secao.Campos[2].Obrigatorio);
        Assert.Equal(4000, admin.Secao.Campos[0].Config.GetProperty("max").GetInt32());

        Assert.Equal(11, admin.Registros.Count);
        var pr01 = admin.Registros[0];
        Assert.True(pr01.Sistema);
        Assert.Equal("PR01", pr01.Codigo);
        Assert.Equal(PeReferenciaisCargaTest.Art4[0], pr01.Dados["texto"].GetString());
        Assert.True(pr01.Dados["criterio_priorizacao"].GetBoolean());
        Assert.Equal("Sim", pr01.Rotulos["criterio_priorizacao"]);
        Assert.Equal("art. 4º, I, do Decreto nº 48.900/2026", pr01.Rotulos["fundamento"]);
        Assert.Empty(pr01.Vinculos);
        Assert.True(admin.PodeEditar);

        foreach (var user in new[] { UserPeSgdi, UserPeCgtic, UserOrgaoSes, UserConsultaSes })
        {
            var leitura = await Registros.ListarAsync(PeDono.Df, "principio", await ContextoDe(user));
            Assert.Equal(11, leitura.Registros.Count);
            Assert.False(leitura.PodeEditar);
        }
        Assert.True((await Registros.ListarAsync(PeDono.Df, "principio", await ContextoDe(UserAdminGeral))).PodeEditar);
    }

    [Fact]
    public async Task SemPapel_NaoLe_EQuemNaoAdministra_NaoGrava()
    {
        Assert.Equal(Codigo(ErrorCode.PeSemPermissao),
            await ErroAsync(async () => await Registros.ListarAsync(PeDono.Df, "principio", await ContextoDe(UserSemPapel))));

        foreach (var user in new[] { UserPeSgdi, UserPeCgtic, UserOrgaoSes, UserConsultaSes })
            Assert.Equal(Codigo(ErrorCode.PeSemPermissao), await ErroAsync(async () => await Registros.CriarAsync(PeDono.Df, "principio",
                Salvar(new { texto = "Outro", fundamento = "Resolução" }), await ContextoDe(user))));
        Assert.Equal(11, Context.PeRegistros.Count(r => r.PeticId == null));
    }

    [Fact]
    public async Task Secao_DeOutroEscopo_Inexistente_OuDesligada_404()
    {
        var ctx = await Admin();
        Assert.Equal(Codigo(ErrorCode.PeSecaoIndisponivel), await ErroAsync(() => Registros.ListarAsync(PeDono.Df, "necessidades", ctx)));
        Assert.Equal(Codigo(ErrorCode.PeSecaoIndisponivel), await ErroAsync(() => Registros.ListarAsync(PeDono.Df, "petic_objetivo", ctx)));
        Assert.Equal(Codigo(ErrorCode.PeSecaoIndisponivel), await ErroAsync(() => Registros.ListarAsync(PeDono.Df, "nao_existe", ctx)));

        await Modelo.DefinirSituacaoSecaoAsync(Secao("diretriz_ciclo").Id, new PeSituacoesDTO { SituacaoGeral = "desligado" }, EmailAdmin);
        Assert.Equal(Codigo(ErrorCode.PeSecaoIndisponivel), await ErroAsync(() => Registros.ListarAsync(PeDono.Df, "diretriz_ciclo", ctx)));

        Assert.Equal(Codigo(ErrorCode.PePeticNaoEncontrado),
            await ErroAsync(() => Registros.ListarAsync(PeDono.DoPetic(999), "petic_objetivo", ctx)));

        // Antes de o carregador trazer os referenciais (versão 1 gravada), é a atualização: 409
        var versao = Context.PeConfiguracoes.Single(c => c.Chave == PeConfiguracao.ChaveVersaoModelo);
        versao.Valor = "1";
        await Context.SaveChangesAsync();
        Assert.Equal(Codigo(ErrorCode.PeModeloIndisponivel), await ErroAsync(() => Registros.ListarAsync(PeDono.Df, "nao_existe", ctx)));
    }

    // ── Código e ordem ────────────────────────────────────────────────────────

    [Fact]
    public async Task Codigo_PelaSequenciaDoDono_NuncaReaproveitaOApagado()
    {
        var ctx = await Admin();
        var pr12 = await Registros.CriarAsync(PeDono.Df, "principio", Salvar(new { texto = "A", fundamento = "Resolução 1" }), ctx);
        var pr13 = await Registros.CriarAsync(PeDono.Df, "principio", Salvar(new { texto = "B", fundamento = "Resolução 2" }), ctx);
        Assert.Equal("PR12", pr12.Codigo);
        Assert.Equal("PR13", pr13.Codigo);
        Assert.Equal(12, pr12.Ordem);
        Assert.False(pr12.Sistema);
        Assert.Equal(UserPeAdmin.Email, pr12.CriadoPor);

        await Registros.ExcluirAsync(PeDono.Df, "principio", pr13.Id, ctx);
        var pr14 = await Registros.CriarAsync(PeDono.Df, "principio", Salvar(new { texto = "C", fundamento = "Resolução 3" }), ctx);
        Assert.Equal("PR14", pr14.Codigo);

        // Cada dono tem a sua sequência
        var rascunho = await RascunhoAsync();
        Assert.Equal("D01", (await IncluirAsync(rascunho.Id, "petic_diretriz", new { texto = "Diretriz" })).Codigo);
        Assert.Equal("DC01", (await Registros.CriarAsync(PeDono.Df, "diretriz_ciclo",
            Salvar(new { ano = 2027, texto = "Priorizar a nuvem", situacao_deliberacao = "proposta" }), ctx)).Codigo);
    }

    [Fact]
    public async Task RegistroDoSistema_NaoMuda_NemEApagado()
    {
        var ctx = await Admin();
        var pr01 = Context.PeRegistros.AsNoTracking().Single(r => r.Codigo == "PR01" && r.PeticId == null);

        Assert.Equal(Codigo(ErrorCode.PeRegistroDoSistema), await ErroAsync(() => Registros.AtualizarAsync(PeDono.Df, "principio", pr01.Id,
            Salvar(new { texto = "Mudado", fundamento = "x" }), ctx)));
        Assert.Equal(Codigo(ErrorCode.PeRegistroDoSistema), await ErroAsync(() => Registros.ExcluirAsync(PeDono.Df, "principio", pr01.Id, ctx)));
        Assert.Equal(pr01.Dados, RegistroNoBanco(pr01.Id).Dados);
    }

    [Fact]
    public async Task Ordem_TodosOsRegistros_NaNovaOrdem()
    {
        var ctx = await Admin();
        var ids = (await Registros.ListarAsync(PeDono.Df, "principio", ctx)).Registros.Select(r => r.Id).Reverse().ToList();

        var nova = await Registros.OrdenarAsync(PeDono.Df, "principio", new PeOrdemDTO { Ids = ids }, ctx);

        Assert.Equal("PR11", nova.Registros[0].Codigo);
        Assert.Equal(Enumerable.Range(1, 11), nova.Registros.Select(r => r.Ordem));
        Assert.Equal(Codigo(ErrorCode.PeOrdemInvalida),
            await ErroAsync(() => Registros.OrdenarAsync(PeDono.Df, "principio", new PeOrdemDTO { Ids = ids.Skip(1).ToList() }, ctx)));
        Assert.Equal(Codigo(ErrorCode.PeOrdemInvalida), await ErroAsync(() => Registros.OrdenarAsync(PeDono.Df, "principio",
            new PeOrdemDTO { Ids = ids.Take(10).Append(ids[0]).ToList() }, ctx)));
    }

    // ── Validação ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Obrigatorio_CampoDesconhecido_ETamanho_VoltamEmCamposPelaChave()
    {
        var ctx = await Admin();

        var erros = await CamposComErroAsync(() => Registros.CriarAsync(PeDono.Df, "principio", Salvar(new { texto = "   ", inventado = 1 }), ctx));
        Assert.Equal("Preencha este campo.", erros["texto"]);
        Assert.Equal("Preencha este campo.", erros["fundamento"]);
        Assert.Contains("não existe", erros["inventado"]);
        Assert.DoesNotContain("criterio_priorizacao", erros.Keys);

        erros = await CamposComErroAsync(() => Registros.CriarAsync(PeDono.Df, "principio",
            Salvar(new { texto = "Ok", fundamento = new string('x', 301) }), ctx));
        Assert.Contains("300", erros["fundamento"]);
        Assert.Single(erros);

        Assert.Equal(11, Context.PeRegistros.Count(r => r.PeticId == null));
    }

    [Fact]
    public async Task Tipos_CadaUmComASuaRegra_EOsRotulosProntos()
    {
        var secao = await SecaoDfAsync("Tipos");
        await CampoAsync(secao.Id, "nome", "texto_curto", new { max = 10 }, "obrigatorio");
        await CampoAsync(secao.Id, "quantidade", "numero", new { min = 1, max = 5, casas = 0, unidade = "salas" });
        await CampoAsync(secao.Id, "valor", "moeda");
        await CampoAsync(secao.Id, "pct", "percentual");
        await CampoAsync(secao.Id, "quando", "data");
        await CampoAsync(secao.Id, "ativo", "sim_nao");
        var cor = await CampoAsync(secao.Id, "cor", "lista");
        await OpcoesAsync(cor.Id, "azul", "verde");
        var temas = await CampoAsync(secao.Id, "temas", "lista_multipla");
        await OpcoesAsync(temas.Id, "a", "b", "c");
        var ctx = await Admin();

        var erros = await CamposComErroAsync(() => Registros.CriarAsync(PeDono.Df, secao.Chave, Salvar(new
        {
            nome = "Nome longo demais",
            quantidade = 2.5,
            valor = -3,
            pct = 101,
            quando = "31/12/2026",
            ativo = "sim",
            cor = "vermelho",
            temas = new[] { "a", "z" }
        }), ctx));
        Assert.Equal(8, erros.Count);
        Assert.Contains("10", erros["nome"]);
        Assert.Contains("inteiro", erros["quantidade"]);
        Assert.Contains("zero", erros["valor"]);
        Assert.Contains("0 a 100", erros["pct"]);
        Assert.Contains("aaaa-mm-dd", erros["quando"]);
        Assert.Equal("Responda sim ou não.", erros["ativo"]);
        Assert.Contains("opções", erros["cor"]);
        Assert.Contains("opções", erros["temas"]);

        erros = await CamposComErroAsync(() => Registros.CriarAsync(PeDono.Df, secao.Chave,
            Salvar(new { nome = "Ok", quantidade = 9, valor = 10.123, quando = "2026-02-30" }), ctx));
        Assert.Contains("5", erros["quantidade"]);
        Assert.Contains("duas casas", erros["valor"]);
        Assert.Contains("válida", erros["quando"]);

        var ok = await Registros.CriarAsync(PeDono.Df, secao.Chave, Salvar(new
        {
            nome = "  Nome  ",
            quantidade = 3,
            valor = 1234.5,
            pct = 12.5,
            quando = "2026-12-31",
            ativo = true,
            cor = "verde",
            temas = new[] { "c", "a", "c" }
        }), ctx);
        Assert.Equal("T01", ok.Codigo);
        Assert.Equal("Nome", ok.Dados["nome"].GetString());
        Assert.Equal(3, ok.Dados["quantidade"].GetDecimal());
        Assert.Equal("2026-12-31", ok.Dados["quando"].GetString());
        Assert.Equal(new[] { "a", "c" }, ok.Dados["temas"].EnumerateArray().Select(e => e.GetString()));
        Assert.Equal("3 salas", ok.Rotulos["quantidade"]);
        Assert.Equal("R$ 1.234,50", ok.Rotulos["valor"]);
        Assert.Equal("12,5%", ok.Rotulos["pct"]);
        Assert.Equal("31/12/2026", ok.Rotulos["quando"]);
        Assert.Equal("Sim", ok.Rotulos["ativo"]);
        Assert.Equal("Opção verde", ok.Rotulos["cor"]);
        Assert.Equal("Opção a, Opção c", ok.Rotulos["temas"]);

        // Vazio (nulo, texto em branco, lista vazia) não é guardado
        var vazio = await Registros.AtualizarAsync(PeDono.Df, secao.Chave, ok.Id,
            Salvar(new { nome = "Nome", quantidade = (int?)null, temas = Array.Empty<string>(), cor = "" }), ctx);
        Assert.Equal(new[] { "nome" }, vazio.Dados.Keys);
        Assert.Equal("{\"nome\":\"Nome\"}", RegistroNoBanco(ok.Id).Dados);
    }

    [Fact]
    public async Task OpcaoDesativada_SoValeQuandoJaEraAGuardada()
    {
        var secao = await SecaoDfAsync("Cores");
        await CampoAsync(secao.Id, "nome", "texto_curto", null, "obrigatorio");
        var cor = await CampoAsync(secao.Id, "cor", "lista");
        await OpcoesAsync(cor.Id, "azul", "verde");
        var ctx = await Admin();
        var registro = await Registros.CriarAsync(PeDono.Df, secao.Chave, Salvar(new { nome = "Um", cor = "verde" }), ctx);

        // Em uso por um registro: apagar só desativa (E3)
        var desativada = await Modelo.ExcluirOpcaoAsync(Opcao(secao.Chave, "cor", "verde").Id, EmailAdmin);
        Assert.NotNull(desativada);
        Assert.False(desativada!.Ativa);

        var mantido = await Registros.AtualizarAsync(PeDono.Df, secao.Chave, registro.Id, Salvar(new { nome = "Um", cor = "verde" }), ctx);
        Assert.Equal("Opção verde", mantido.Rotulos["cor"]);
        var erros = await CamposComErroAsync(() => Registros.CriarAsync(PeDono.Df, secao.Chave, Salvar(new { nome = "Dois", cor = "verde" }), ctx));
        Assert.Contains("opções", erros["cor"]);
        // Na tela, só as opções ativas
        Assert.Equal(new[] { "azul" },
            (await Registros.ListarAsync(PeDono.Df, secao.Chave, ctx)).Secao.Campos.Single(c => c.Chave == "cor").Opcoes.Select(o => o.Valor));

        // Opção que ninguém usa é apagada de verdade
        Assert.Null(await Modelo.ExcluirOpcaoAsync(Opcao(secao.Chave, "cor", "azul").Id, EmailAdmin));
    }

    [Fact]
    public async Task GuardaEEsconde_CampoDesligadoMantemOValor_ENaoEExigido()
    {
        var secao = await SecaoDfAsync("Notas");
        await CampoAsync(secao.Id, "nome", "texto_curto", null, "obrigatorio");
        var nota = await CampoAsync(secao.Id, "nota", "texto_longo", null, "obrigatorio");
        var ctx = await Admin();
        var registro = await Registros.CriarAsync(PeDono.Df, secao.Chave, Salvar(new { nome = "Um", nota = "Guardada" }), ctx);

        await Modelo.DefinirSituacaoCampoAsync(nota.Id, new PeSituacoesDTO { SituacaoGeral = "desligado" }, EmailAdmin);

        var lista = await Registros.ListarAsync(PeDono.Df, secao.Chave, ctx);
        Assert.DoesNotContain(lista.Secao.Campos, c => c.Chave == "nota");
        Assert.False(lista.Registros[0].Dados.ContainsKey("nota"));

        // Salvar sem o campo (e até mandando outro valor para ele) não apaga nem troca o guardado
        await Registros.AtualizarAsync(PeDono.Df, secao.Chave, registro.Id, Salvar(new { nome = "Um mudado", nota = "Outro" }), ctx);
        Assert.Equal("Guardada", PeRegistroDados.Texto(PeRegistroDados.Ler(RegistroNoBanco(registro.Id).Dados)["nota"]));
        var novo = await Registros.CriarAsync(PeDono.Df, secao.Chave, Salvar(new { nome = "Dois" }), ctx);
        Assert.Equal("T02", novo.Codigo);

        // Religado, o valor volta
        await Modelo.DefinirSituacaoCampoAsync(nota.Id, new PeSituacoesDTO { SituacaoGeral = "opcional" }, EmailAdmin);
        lista = await Registros.ListarAsync(PeDono.Df, secao.Chave, ctx);
        Assert.Equal("Guardada", lista.Registros.Single(r => r.Id == registro.Id).Dados["nota"].GetString());
        Assert.Equal("Um mudado", lista.Registros.Single(r => r.Id == registro.Id).Dados["nome"].GetString());
    }

    [Fact]
    public async Task Put_SemDadosMantemOsValores_ESemVinculosMantemAsLigacoes()
    {
        var rascunho = await RascunhoAsync();
        var oe = await IncluirAsync(rascunho.Id, "petic_objetivo", new { texto = "Objetivo" });
        var ie = await IncluirAsync(rascunho.Id, "petic_indicador", new { nome = "Indicador", fonte = "SGDI" }, new { objetivo = new[] { oe.Id } });
        var dono = PeDono.DoPetic(rascunho.Id);
        var ctx = await Admin();

        var soDados = await Registros.AtualizarAsync(dono, "petic_indicador", ie.Id, Salvar(new { nome = "Novo nome" }), ctx);
        Assert.Equal(oe.Id, soDados.Vinculos["objetivo"].Single().RegistroId);
        Assert.False(soDados.Dados.ContainsKey("fonte"));

        var oe2 = await IncluirAsync(rascunho.Id, "petic_objetivo", new { texto = "Outro objetivo" });
        var soLigacao = await Registros.AtualizarAsync(dono, "petic_indicador", ie.Id, Salvar(vinculos: new { objetivo = new[] { oe2.Id } }), ctx);
        Assert.Equal("Novo nome", soLigacao.Dados["nome"].GetString());
        Assert.Equal("OE02", soLigacao.Vinculos["objetivo"].Single().Codigo);
        Assert.Single(Context.PeVinculos.Where(v => v.RegistroOrigemId == ie.Id));
    }

    [Fact]
    public async Task Formulario_UmRegistroSo_SemCodigo()
    {
        var rascunho = await RascunhoAsync();
        var ctx = await Admin();
        var dono = PeDono.DoPetic(rascunho.Id);

        var erros = await CamposComErroAsync(() => IncluirAsync(rascunho.Id, "petic_identidade", new { missao = "Missão" }));
        Assert.Equal("Preencha este campo.", erros["visao"]);

        var identidade = await IncluirAsync(rascunho.Id, "petic_identidade", new { missao = "Missão", visao = "Visão" });
        Assert.Null(identidade.Codigo);
        Assert.Equal(Codigo(ErrorCode.PeFormularioJaPreenchido),
            await ErroAsync(() => IncluirAsync(rascunho.Id, "petic_identidade", new { missao = "Outra", visao = "Outra" })));

        var atualizado = await Registros.AtualizarAsync(dono, "petic_identidade", identidade.Id,
            Salvar(new { missao = "Nova missão", visao = "Visão", valores = "Ética" }), ctx);
        Assert.Equal("Nova missão", atualizado.Dados["missao"].GetString());
        Assert.Single((await Registros.ListarAsync(dono, "petic_identidade", ctx)).Registros);
    }

    // ── Ligações ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Ligacao_SoComRegistroDoMesmoDono_EUmaSoQuandoNaoMultipla()
    {
        var (_, objetivoDaVigente) = await VigenteAsync();
        var rascunho = await RascunhoAsync(copiar: false);
        var oe = await IncluirAsync(rascunho.Id, "petic_objetivo", new { texto = "Objetivo do rascunho" });
        var oe2 = await IncluirAsync(rascunho.Id, "petic_objetivo", new { texto = "Segundo objetivo" });

        var erros = await CamposComErroAsync(() => IncluirAsync(rascunho.Id, "petic_indicador", new { nome = "I" },
            new { objetivo = new[] { objetivoDaVigente.Id } }));
        Assert.Contains("não pode ser ligado", erros["objetivo"]);
        erros = await CamposComErroAsync(() => IncluirAsync(rascunho.Id, "petic_indicador", new { nome = "I" },
            new { objetivo = new[] { oe.Id, oe2.Id } }));
        Assert.Equal("Escolha só um item.", erros["objetivo"]);
        erros = await CamposComErroAsync(() => IncluirAsync(rascunho.Id, "petic_indicador", new { nome = "I" }));
        Assert.Equal("Escolha um item.", erros["objetivo"]);
        erros = await CamposComErroAsync(() => IncluirAsync(rascunho.Id, "petic_indicador", new { nome = "I", objetivo = oe.Id }));
        Assert.Contains("Vinculos", erros["objetivo"]);
        erros = await CamposComErroAsync(() => IncluirAsync(rascunho.Id, "petic_indicador", new { nome = "I" },
            new { objetivo = new[] { oe.Id }, nome = new[] { 1 } }));
        Assert.Contains("ligação", erros["nome"]);

        var ie = await IncluirAsync(rascunho.Id, "petic_indicador", new { nome = "I" }, new { objetivo = new[] { oe.Id } });
        var ligado = Assert.Single(ie.Vinculos["objetivo"]);
        Assert.Equal(oe.Id, ligado.RegistroId);
        Assert.Equal("OE01", ligado.Codigo);
        Assert.Equal("Objetivo do rascunho", ligado.Resumo);
        Assert.Equal("OE01", ie.Rotulos["objetivo"]);

        var prioridade = await IncluirAsync(rascunho.Id, "petic_prioridade", new { texto = "P" }, new { objetivos = new[] { oe2.Id, oe.Id } });
        Assert.Equal(new[] { "OE01", "OE02" }, prioridade.Vinculos["objetivos"].Select(v => v.Codigo));
        Assert.Equal("OE01, OE02", prioridade.Rotulos["objetivos"]);
    }

    [Fact]
    public async Task ApagarRegistroLigado_409DizendoQuemLiga()
    {
        var rascunho = await RascunhoAsync();
        var dono = PeDono.DoPetic(rascunho.Id);
        var ctx = await Admin();
        var oe = await IncluirAsync(rascunho.Id, "petic_objetivo", new { texto = "Objetivo" });
        var ie = await IncluirAsync(rascunho.Id, "petic_indicador", new { nome = "Indicador" }, new { objetivo = new[] { oe.Id } });

        var ex = await Assert.ThrowsAsync<ApiException>(() => Registros.ExcluirAsync(dono, "petic_objetivo", oe.Id, ctx));
        Assert.Equal((int)ErrorCode.PeRegistroLigado, ex.Error.Code);
        Assert.Contains("IE01 (Indicadores estratégicos, PETIC-DF 1.0)", ex.Error.Message);

        // Apagar quem liga leva a ligação junto; aí o objetivo sai
        await Registros.ExcluirAsync(dono, "petic_indicador", ie.Id, ctx);
        Assert.Empty(Context.PeVinculos.Where(v => v.RegistroDestinoId == oe.Id));
        await Registros.ExcluirAsync(dono, "petic_objetivo", oe.Id, ctx);
        Assert.Empty(Context.PeRegistros.Where(r => r.Id == oe.Id));
    }

    [Fact]
    public async Task LigacaoComCatalogo_Principios_EPeticOpcionalSemVigente()
    {
        var secao = await SecaoDfAsync("Critérios");
        await CampoAsync(secao.Id, "nome", "texto_curto", null, "obrigatorio");
        await CampoAsync(secao.Id, "principios", "ligacao_catalogo", new { catalogo = "principio", multipla = true }, "obrigatorio");
        await CampoAsync(secao.Id, "objetivo_petic", "ligacao_catalogo", new { catalogo = "petic_objetivo", multipla = false }, "obrigatorio");
        var ctx = await Admin();
        var principios = await Registros.CatalogoAsync("principio", ctx);

        // Sem PETIC-DF vigente: a ligação com os objetivos fica opcional e o catálogo vem vazio
        var lista = await Registros.ListarAsync(PeDono.Df, secao.Chave, ctx);
        Assert.False(lista.Secao.Campos.Single(c => c.Chave == "objetivo_petic").Obrigatorio);
        Assert.True(lista.Secao.Campos.Single(c => c.Chave == "principios").Obrigatorio);
        Assert.Empty(await Registros.CatalogoAsync("petic_objetivo", ctx));
        var registro = await Registros.CriarAsync(PeDono.Df, secao.Chave,
            Salvar(new { nome = "C" }, new { principios = new[] { principios[4].Id, principios[0].Id } }), ctx);
        Assert.Equal("PR01, PR05", registro.Rotulos["principios"]);
        Assert.Empty(registro.Vinculos["objetivo_petic"]);

        // Com vigente: obrigatória, e só com os objetivos da vigente
        var (_, objetivo) = await VigenteAsync();
        lista = await Registros.ListarAsync(PeDono.Df, secao.Chave, ctx);
        Assert.True(lista.Secao.Campos.Single(c => c.Chave == "objetivo_petic").Obrigatorio);
        var erros = await CamposComErroAsync(() => Registros.AtualizarAsync(PeDono.Df, secao.Chave, registro.Id,
            Salvar(new { nome = "C" }, new { principios = new[] { principios[0].Id } }), ctx));
        Assert.Equal("Escolha um item.", erros["objetivo_petic"]);
        erros = await CamposComErroAsync(() => Registros.AtualizarAsync(PeDono.Df, secao.Chave, registro.Id,
            Salvar(new { nome = "C" }, new { principios = new[] { principios[0].Id }, objetivo_petic = new[] { principios[1].Id } }), ctx));
        Assert.Contains("não pode ser ligado", erros["objetivo_petic"]);

        var catalogo = Assert.Single(await Registros.CatalogoAsync("petic_objetivo", ctx));
        Assert.Equal(objetivo.Id, catalogo.Id);
        var ok = await Registros.AtualizarAsync(PeDono.Df, secao.Chave, registro.Id,
            Salvar(new { nome = "C" }, new { principios = new[] { principios[0].Id }, objetivo_petic = new[] { catalogo.Id } }), ctx);
        Assert.Equal("OE01", ok.Rotulos["objetivo_petic"]);
        Assert.Equal("Ampliar os serviços digitais.", ok.Vinculos["objetivo_petic"][0].Resumo);

        // O princípio ligado não sai (e o objetivo da vigente também não, que a versão não muda)
        var pr12 = await Registros.CriarAsync(PeDono.Df, "principio", Salvar(new { texto = "Novo", fundamento = "Resolução" }), ctx);
        await Registros.AtualizarAsync(PeDono.Df, secao.Chave, registro.Id,
            Salvar(vinculos: new { principios = new[] { pr12.Id }, objetivo_petic = new[] { catalogo.Id } }), ctx);
        var ex = await Assert.ThrowsAsync<ApiException>(() => Registros.ExcluirAsync(PeDono.Df, "principio", pr12.Id, ctx));
        Assert.Equal((int)ErrorCode.PeRegistroLigado, ex.Error.Code);
        Assert.Contains("T01 (Critérios)", ex.Error.Message);
    }

    [Fact]
    public async Task Catalogos_IdCodigoERotulo_EForaDaLista404()
    {
        var ctx = await ContextoDe(UserOrgaoSes);

        var principios = await Registros.CatalogoAsync("principio", ctx);
        Assert.Equal(11, principios.Count);
        Assert.Equal("PR01", principios[0].Codigo);
        Assert.Equal(PeReferenciaisCargaTest.Art4[0], principios[0].Rotulo);
        Assert.Empty(await Registros.CatalogoAsync("petic_eixo", ctx));
        // Desde a E4 o catálogo do PGIA existe, mas pede o PDTIC (os sistemas são do órgão dele)
        Assert.Equal(Codigo(ErrorCode.PeDadosInvalidos), await ErroAsync(() => Registros.CatalogoAsync("pgia_sistema", ctx)));
        Assert.Equal(Codigo(ErrorCode.PeCatalogoNaoEncontrado), await ErroAsync(() => Registros.CatalogoAsync("outro", ctx)));
        Assert.Equal(Codigo(ErrorCode.PeSemPermissao),
            await ErroAsync(async () => await Registros.CatalogoAsync("principio", await ContextoDe(UserSemPapel))));
    }

    // ── Calculados ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Calculados_ProdutoSomaPonderadaSubtracaoENivelDeRisco()
    {
        var secao = await SecaoDfAsync("Cálculos");
        await CampoAsync(secao.Id, "nome", "texto_curto", null, "obrigatorio");
        await CampoAsync(secao.Id, "a", "numero");
        await CampoAsync(secao.Id, "b", "numero");
        var c = await CampoAsync(secao.Id, "c", "lista");
        await Modelo.CriarOpcaoAsync(c.Id, new PeOpcaoCriarDTO { Valor = "2", Rotulo = "Dois" }, EmailAdmin);
        await Modelo.CriarOpcaoAsync(c.Id, new PeOpcaoCriarDTO { Valor = "3", Rotulo = "Três" }, EmailAdmin);
        await CampoAsync(secao.Id, "produto", "calculado", new { calculo = "produto", campos = new[] { "a", "b", "c" } });
        await CampoAsync(secao.Id, "soma", "calculado", new { calculo = "soma_ponderada", campos = new[] { "a", "b" }, pesos = new { a = 2, b = 0.5 } });
        await CampoAsync(secao.Id, "diferenca", "calculado", new { calculo = "subtracao", campos = new[] { "a", "b" } });
        var prob = await CampoAsync(secao.Id, "prob", "lista");
        await OpcoesAsync(prob.Id, "baixa", "alta");
        var imp = await CampoAsync(secao.Id, "imp", "lista");
        await OpcoesAsync(imp.Id, "baixo", "alto");
        var nivel = await CampoAsync(secao.Id, "nivel", "calculado", new
        {
            calculo = "nivel_risco",
            campos = new[] { "prob", "imp" },
            matriz = new { baixa = new { baixo = "b", alto = "m" }, alta = new { baixo = "m", alto = "a" } }
        });
        await OpcoesAsync(nivel.Id, "b", "m", "a");
        var ctx = await Admin();

        // O valor mandado para um calculado é ignorado: quem calcula é o servidor
        var registro = await Registros.CriarAsync(PeDono.Df, secao.Chave,
            Salvar(new { nome = "X", a = 4, b = 2.5, c = "3", prob = "alta", imp = "baixo", produto = 999 }), ctx);
        Assert.Equal(30m, registro.Dados["produto"].GetDecimal());
        Assert.Equal(9.25m, registro.Dados["soma"].GetDecimal());
        Assert.Equal(1.5m, registro.Dados["diferenca"].GetDecimal());
        Assert.Equal("m", registro.Dados["nivel"].GetString());
        Assert.Equal("Opção m", registro.Rotulos["nivel"]);
        Assert.Equal("30", registro.Rotulos["produto"]);
        Assert.Equal("9,25", registro.Rotulos["soma"]);
        Assert.Equal(30m, PeRegistroDados.Numero(PeRegistroDados.Ler(RegistroNoBanco(registro.Id).Dados)["produto"]));

        // Falta um valor: o calculado fica vazio
        var semB = await Registros.AtualizarAsync(PeDono.Df, secao.Chave, registro.Id,
            Salvar(new { nome = "X", a = 4, c = "3", prob = "alta" }), ctx);
        Assert.False(semB.Dados.ContainsKey("produto"));
        Assert.False(semB.Dados.ContainsKey("diferenca"));
        Assert.False(semB.Dados.ContainsKey("nivel"));

        // Campo escondido fica fora do produto e da soma; a subtração, que precisa dos dois, fica vazia
        await Modelo.DefinirSituacaoCampoAsync(Campo(secao.Chave, "b").Id, new PeSituacoesDTO { SituacaoGeral = "desligado" }, EmailAdmin);
        var escondido = await Registros.AtualizarAsync(PeDono.Df, secao.Chave, registro.Id,
            Salvar(new { nome = "X", a = 4, c = "2", prob = "baixa", imp = "baixo" }), ctx);
        Assert.Equal(8m, escondido.Dados["produto"].GetDecimal());
        Assert.Equal(8m, escondido.Dados["soma"].GetDecimal());
        Assert.False(escondido.Dados.ContainsKey("diferenca"));
        Assert.Equal("b", escondido.Dados["nivel"].GetString());
    }

    // ── Arquivo ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task CampoDeArquivo_UsaOArquivoDeQuemGrava_DoTipoETamanhoDoCampo_EDaDono()
    {
        var secao = await SecaoDfAsync("Atos");
        await CampoAsync(secao.Id, "nome", "texto_curto", null, "obrigatorio");
        await CampoAsync(secao.Id, "ato", "arquivo", new { tipos = new[] { "pdf" }, maxMb = 1 }, "obrigatorio");
        var pdf = await EnviarArquivoAsync(UserPeAdmin, "ato.pdf", Pdf());
        var png = await EnviarArquivoAsync(UserPeAdmin, "foto.png", Png());
        var grande = await EnviarArquivoAsync(UserPeAdmin, "grande.pdf", Pdf(2 * 1024 * 1024));
        var deOutraPessoa = await EnviarArquivoAsync(UserAdminGeral, "outro.pdf", Pdf());
        var ctx = await Admin();

        async Task<string> Erro(long id) =>
            (await CamposComErroAsync(() => Registros.CriarAsync(PeDono.Df, secao.Chave,
                Salvar(new { nome = "Ato", ato = new { ArquivoId = id, Nome = "qualquer.pdf" } }), ctx)))["ato"];
        Assert.Contains("PDF", await Erro(png.Id));
        Assert.Contains("1 MB", await Erro(grande.Id));
        Assert.Contains("não pode ser usado", await Erro(deOutraPessoa.Id));
        Assert.Contains("não foi encontrado", await Erro(999999));
        Assert.Equal("Envie o arquivo.", (await CamposComErroAsync(() =>
            Registros.CriarAsync(PeDono.Df, secao.Chave, Salvar(new { nome = "Ato" }), ctx)))["ato"]);

        var registro = await Registros.CriarAsync(PeDono.Df, secao.Chave,
            Salvar(new { nome = "Ato", ato = new { ArquivoId = pdf.Id, Nome = "nome-do-navegador.pdf" } }), ctx);
        Assert.Equal(pdf.Id, registro.Dados["ato"].GetProperty("ArquivoId").GetInt64());
        Assert.Equal("ato.pdf", registro.Dados["ato"].GetProperty("Nome").GetString());
        Assert.Equal("ato.pdf", registro.Rotulos["ato"]);
        var noBanco = Context.PeArquivos.AsNoTracking().Single(a => a.Id == pdf.Id);
        Assert.Equal("registro", noBanco.DonoTipo);
        Assert.Equal(registro.Id, noBanco.DonoId);

        // O mesmo arquivo continua valendo no registro dele; em outro, não
        await Registros.AtualizarAsync(PeDono.Df, secao.Chave, registro.Id, Salvar(new { nome = "Ato 2", ato = new { ArquivoId = pdf.Id } }), ctx);
        Assert.Contains("não pode ser usado", await Erro(pdf.Id));
    }
}
