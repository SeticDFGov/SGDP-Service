using System.Text.Json;
using System.Text.Json.Nodes;
using api.Planejamento;
using Microsoft.EntityFrameworkCore;
using Models.Planejamento;
using service;
using service.Planejamento;
using Xunit;

namespace test.planejamento;

/// <summary>
/// Motor de registros com o dono PDTIC (E4): seção e campos pelo nível do órgão e pelos
/// ajustes (desligado some no GET e é ignorado no POST e no PUT, com o valor guardado), código
/// por PDTIC, ligação só no mesmo PDTIC, vigência copiada do passo 1.1, ligação com o PETIC-DF
/// obrigatória só com versão vigente (no motor e na trilha), o catálogo pgia_sistema e o
/// texto rico com imagens do próprio módulo.
/// </summary>
public class PePdticRegistrosTest : PePdticTestBase
{
    private static readonly object Abrangencia = new
    {
        tipo_abrangencia = "todo_com_vinculadas",
        vigencia_inicio = "2027-01-01",
        vigencia_fim = "2030-12-31",
        periodicidade_revisao = "anual"
    };

    [Fact]
    public async Task Secao_PeloNivelDoOrgao_CamposEObrigatorios()
    {
        var pdtic = await AbrirSesAsync();
        var dono = PeDono.DoPdtic(pdtic.Id);

        var basico = await Registros.ListarAsync(dono, "necessidades", await Orgao());
        Assert.Equal("N", basico.Secao.PrefixoCodigo);
        Assert.Equal(new[] { "descricao", "tipo", "objetivo_petic", "prioridade_simples" }, basico.Secao.Campos.Select(c => c.Chave));
        Assert.True(basico.Secao.Campos.Single(c => c.Chave == "prioridade_simples").Obrigatorio);

        await DefinirNivelDoOrgaoAsync(OrgaoSes, "intermediario");
        var intermediario = await Registros.ListarAsync(dono, "necessidades", await Orgao());
        Assert.Contains(intermediario.Secao.Campos, c => c.Chave == "gravidade" && c.Obrigatorio);
        Assert.Contains(intermediario.Secao.Campos, c => c.Chave == "fraqueza" && !c.Obrigatorio);
        Assert.DoesNotContain(intermediario.Secao.Campos, c => c.Chave == "prioridade_simples");

        // Seção desligada no nível, de outro escopo ou inexistente: 404
        await DefinirNivelDoOrgaoAsync(OrgaoSes, "basico");
        foreach (var chave in new[] { "swot_fraquezas", "petic_objetivo", "principio", "nao_existe" })
            Assert.Equal(Codigo(ErrorCode.PeSecaoIndisponivel), await ErroAsync(async () => await Registros.ListarAsync(dono, chave, await Orgao())));

        // Ajuste do órgão liga a seção (e o passo) no Básico
        await AjustarPassosAsync(OrgaoSes, ("diagnostico.swot", "opcional"));
        Assert.Empty((await Registros.ListarAsync(dono, "swot_fraquezas", await Orgao())).Registros);
    }

    [Fact]
    public async Task CampoDesligado_NaoApareceEEIgnorado_ComOValorGuardado()
    {
        await DefinirNivelDoOrgaoAsync(OrgaoSes, "avancado");
        var pdtic = await AbrirSesAsync();
        var dono = PeDono.DoPdtic(pdtic.Id);
        var ativo = await IncluirNoPdticAsync(pdtic.Id, "ativos", new
        {
            nome = "Sistema de regulação", tipo = "sistema", situacao = "em_operacao", hospedagem = "cetic", usa_gdfnet = true, criticidade = "alta"
        });
        Assert.Equal("AT01", ativo.Codigo);
        Assert.Equal("Alta", ativo.Rotulos["criticidade"]);

        // No Básico, a criticidade some e é ignorada (mandar um valor não é erro)
        await DefinirNivelDoOrgaoAsync(OrgaoSes, "basico");
        var lido = Assert.Single((await Registros.ListarAsync(dono, "ativos", await Orgao())).Registros);
        Assert.False(lido.Dados.ContainsKey("criticidade"));
        var atualizado = await Registros.AtualizarAsync(dono, "ativos", ativo.Id,
            Salvar(new { nome = "Sistema de regulação", tipo = "sistema", situacao = "em_desativacao", criticidade = "baixa" }), await Orgao());
        Assert.Equal("em_desativacao", Valor(atualizado, "situacao"));
        Assert.Equal("alta", JsonNode.Parse(RegistroNoBanco(ativo.Id).Dados)!["criticidade"]!.GetValue<string>());

        // De volta ao Avançado, o valor guardado reaparece
        await DefinirNivelDoOrgaoAsync(OrgaoSes, "avancado");
        Assert.Equal("Alta", Assert.Single((await Registros.ListarAsync(dono, "ativos", await Orgao())).Registros).Rotulos["criticidade"]);
    }

    [Fact]
    public async Task Codigos_PorPdtic_ELigacaoSoNoMesmoPdtic()
    {
        var ses = await AbrirSesAsync();
        var seec = await AbrirSeecAsync();
        var n1 = await IncluirNoPdticAsync(ses.Id, "necessidades", new { descricao = "Ampliar a rede", tipo = "infraestrutura", prioridade_simples = "alta" });
        var n2 = await IncluirNoPdticAsync(ses.Id, "necessidades", new { descricao = "Novo prontuário", tipo = "servico", prioridade_simples = "media" });
        var daSeec = await IncluirNoPdticAsync(seec.Id, "necessidades",
            new { descricao = "Painel fiscal", tipo = "servico", prioridade_simples = "baixa" }, user: UserOrgaoSeec);
        Assert.Equal(new[] { "N01", "N02", "N01" }, new[] { n1.Codigo, n2.Codigo, daSeec.Codigo });

        var meta = new { descricao = "Rede ampliada", indicador = "Unidades ligadas", valor = "100%", prazo = "2028-12-31" };
        var campos = await CamposComErroAsync(() => IncluirNoPdticAsync(ses.Id, "metas", meta, new { necessidades = new[] { daSeec.Id } }));
        Assert.Contains("necessidades", campos.Keys);

        var ligada = await IncluirNoPdticAsync(ses.Id, "metas", meta, new { necessidades = new[] { n1.Id, n2.Id } });
        Assert.Equal("M01", ligada.Codigo);
        Assert.Equal(new[] { "N01", "N02" }, ligada.Vinculos["necessidades"].Select(v => v.Codigo));

        // Apagar a necessidade ligada: 409 dizendo quem liga
        var ex = await Assert.ThrowsAsync<ApiException>(async () =>
            await Registros.ExcluirAsync(PeDono.DoPdtic(ses.Id), "necessidades", n1.Id, await Orgao()));
        Assert.Equal((int)ErrorCode.PeRegistroLigado, ex.Error.Code);
        Assert.Contains("M01 (Metas)", ex.Error.Message);
    }

    [Fact]
    public async Task Vigencia_CopiadaDoPasso11_ComOFimDepoisDoInicio()
    {
        var pdtic = await AbrirSesAsync();
        var dono = PeDono.DoPdtic(pdtic.Id);

        var invertida = await CamposComErroAsync(() => IncluirNoPdticAsync(pdtic.Id, "abrangencia", new
        {
            tipo_abrangencia = "outro", vigencia_inicio = "2030-01-01", vigencia_fim = "2027-12-31", periodicidade_revisao = "anual"
        }));
        Assert.Equal("O fim da vigência não pode ser antes do início.", invertida["vigencia_fim"]);

        var registro = await IncluirNoPdticAsync(pdtic.Id, "abrangencia", Abrangencia);
        var lido = await Pdtics.ObterAsync(pdtic.Id, await Orgao());
        Assert.Equal(new DateOnly(2027, 1, 1), lido.VigenciaInicio);
        Assert.Equal(new DateOnly(2030, 12, 31), lido.VigenciaFim);

        await Registros.AtualizarAsync(dono, "abrangencia", registro.Id, Salvar(new
        {
            tipo_abrangencia = "outro", vigencia_inicio = "2027-03-01", vigencia_fim = "2029-02-28", periodicidade_revisao = "semestral"
        }), await Orgao());
        Assert.Equal(new DateOnly(2029, 2, 28), (await Pdtics.ObterAsync(pdtic.Id, await Orgao())).VigenciaFim);

        await Registros.ExcluirAsync(dono, "abrangencia", registro.Id, await Orgao());
        var semVigencia = await Pdtics.ObterAsync(pdtic.Id, await Orgao());
        Assert.Null(semVigencia.VigenciaInicio);
        Assert.Null(semVigencia.VigenciaFim);
    }

    [Fact]
    public async Task LigacaoComOPetic_ObrigatoriaNoIntermediario_SoComVersaoVigente()
    {
        await DefinirNivelDoOrgaoAsync(OrgaoSes, "intermediario");
        var pdtic = await AbrirSesAsync();
        var dono = PeDono.DoPdtic(pdtic.Id);
        var necessidade = new
        {
            descricao = "Ampliar a rede", tipo = "infraestrutura", origem = "infraestrutura", areas = "Todas",
            gravidade = 5, urgencia = 4, tendencia = 3, priorizada = true
        };

        // Sem vigente: opcional no motor, no GET e na trilha
        var semLigacao = await IncluirNoPdticAsync(pdtic.Id, "necessidades", necessidade);
        Assert.Equal("60", Valor(semLigacao, "prioridade"));
        Assert.False((await Registros.ListarAsync(dono, "necessidades", await Orgao())).Secao.Campos.Single(c => c.Chave == "objetivo_petic").Obrigatorio);
        Assert.False(CampoNaTrilha(await TrilhaAsync(OrgaoSes), "necessidades", "objetivo_petic").Obrigatorio);
        Assert.Equal(PeDominios.SituacaoPasso.Feito, (await PassoAsync(pdtic.Id, "diagnostico.necessidades-tic")).Situacao);

        // Com vigente: obrigatória de novo, e o registro antigo passa a ter pendência
        var (_, objetivo) = await VigenteAsync();
        Assert.True(CampoNaTrilha(await TrilhaAsync(OrgaoSes), "necessidades", "objetivo_petic").Obrigatorio);
        Assert.True((await Registros.ListarAsync(dono, "necessidades", await Orgao())).Secao.Campos.Single(c => c.Chave == "objetivo_petic").Obrigatorio);
        var campos = await CamposComErroAsync(() => IncluirNoPdticAsync(pdtic.Id, "necessidades", necessidade));
        Assert.Equal("Escolha um item.", campos["objetivo_petic"]);
        Assert.Equal(PeDominios.SituacaoPasso.Pendente, (await PassoAsync(pdtic.Id, "diagnostico.necessidades-tic")).Situacao);

        var ligada = await IncluirNoPdticAsync(pdtic.Id, "necessidades", necessidade, new { objetivo_petic = new[] { objetivo.Id } });
        Assert.Equal("OE01", Assert.Single(ligada.Vinculos["objetivo_petic"]).Codigo);
        await Registros.AtualizarAsync(dono, "necessidades", semLigacao.Id, Salvar(vinculos: new { objetivo_petic = new[] { objetivo.Id } }), await Orgao());
        Assert.Equal(PeDominios.SituacaoPasso.Feito, (await PassoAsync(pdtic.Id, "diagnostico.necessidades-tic")).Situacao);
    }

    [Fact]
    public async Task CatalogoPgia_SistemasDoOrgaoDoPdtic_ELigacaoGuardadaNoRegistro()
    {
        var pdtic = await AbrirSesAsync();
        var dono = PeDono.DoPdtic(pdtic.Id);
        var triagem = SistemaPgia(OrgaoSes, "Triagem inteligente");
        var agenda = SistemaPgia(OrgaoSes, "Agenda assistida", "Baixo Risco", "Em aquisição", null);
        var fiscal = SistemaPgia(OrgaoSeec, "Malha fiscal");

        // O catálogo: só os do órgão do PDTIC, por nome, e pede o PDTIC
        var catalogo = await Registros.CatalogoAsync("pgia_sistema", await Orgao(), pdtic.Id);
        Assert.Equal(new[] { "Agenda assistida", "Triagem inteligente" }, catalogo.Select(c => c.Rotulo));
        Assert.All(catalogo, c => Assert.Null(c.Codigo));
        Assert.Equal(Codigo(ErrorCode.PeSemPermissao),
            await ErroAsync(async () => await Registros.CatalogoAsync("pgia_sistema", await ContextoDe(UserOrgaoSeec), pdtic.Id)));

        // Um campo de ligação com o PGIA criado pelo administrador nas aquisições de IA
        var campo = await Modelo.CriarCampoAsync(new PeCampoCriarDTO
        {
            SecaoId = Secao("aquisicoes_ia").Id,
            Chave = "sistemas_pgia",
            Rotulo = "Sistemas do PGIA relacionados",
            Tipo = "ligacao_catalogo",
            Config = JsonSerializer.SerializeToElement(new { catalogo = "pgia_sistema", multipla = true })
        }, EmailAdmin);
        await Modelo.DefinirSituacaoCampoAsync(campo.Id, Todos("opcional"), EmailAdmin);

        var aquisicao = new { objeto = "Assistente de atendimento", classificacao_prevista = "moderado" };
        var campos = await CamposComErroAsync(() => IncluirNoPdticAsync(pdtic.Id, "aquisicoes_ia", aquisicao,
            new { sistemas_pgia = new[] { fiscal.Id } }));
        Assert.Contains("inventário do PGIA", campos["sistemas_pgia"]);
        Assert.Contains("sistemas_pgia", (await CamposComErroAsync(() => IncluirNoPdticAsync(pdtic.Id, "aquisicoes_ia",
            new { objeto = "X", classificacao_prevista = "moderado", sistemas_pgia = new[] { triagem.Id } }))).Keys);

        var registro = await IncluirNoPdticAsync(pdtic.Id, "aquisicoes_ia", aquisicao, new { sistemas_pgia = new[] { triagem.Id, agenda.Id } });
        Assert.Equal(new[] { "Triagem inteligente", "Agenda assistida" }, registro.Vinculos["sistemas_pgia"].Select(v => v.Resumo));
        Assert.Equal(new[] { triagem.Id, agenda.Id }, registro.Vinculos["sistemas_pgia"].Select(v => v.RegistroId));
        Assert.Equal("Triagem inteligente, Agenda assistida", registro.Rotulos["sistemas_pgia"]);
        Assert.False(registro.Dados.ContainsKey("sistemas_pgia"));
        Assert.Equal("[" + triagem.Id + "," + agenda.Id + "]", JsonNode.Parse(RegistroNoBanco(registro.Id).Dados)!["sistemas_pgia"]!.ToJsonString());
        Assert.Empty(Context.PeVinculos.Where(v => v.RegistroOrigemId == registro.Id));

        // PUT sem Vinculos mantém; com a lista, troca
        var mantido = await Registros.AtualizarAsync(dono, "aquisicoes_ia", registro.Id, Salvar(aquisicao), await Orgao());
        Assert.Equal(2, mantido.Vinculos["sistemas_pgia"].Count);
        var trocado = await Registros.AtualizarAsync(dono, "aquisicoes_ia", registro.Id, Salvar(vinculos: new { sistemas_pgia = new[] { agenda.Id } }), await Orgao());
        Assert.Equal("Agenda assistida", Assert.Single(trocado.Vinculos["sistemas_pgia"]).Resumo);

        // Com dados gravados, o campo não troca de catálogo
        Assert.Equal(Codigo(ErrorCode.PeItemEmUso), await ErroAsync(() => Modelo.AtualizarCampoAsync(campo.Id, new PeCampoAtualizarDTO
        {
            Config = JsonSerializer.SerializeToElement(new { catalogo = "principio", multipla = true }),
            Informados = new HashSet<string> { "Config" }
        }, EmailAdmin)));

        // Fora do PDTIC, o catálogo do PGIA não liga
        var secaoDf = await SecaoDfAsync("Sistemas no DF");
        await CampoAsync(secaoDf.Id, "nome", "texto_curto", null, "obrigatorio");
        await CampoAsync(secaoDf.Id, "sistemas", "ligacao_catalogo", new { catalogo = "pgia_sistema", multipla = false });
        var noDf = await CamposComErroAsync(async () => await Registros.CriarAsync(PeDono.Df, secaoDf.Chave,
            Salvar(new { nome = "A" }, new { sistemas = new[] { triagem.Id } }), await Admin()));
        Assert.Contains("PDTIC", noDf["sistemas"]);
    }

    [Fact]
    public async Task TextoRico_GuardadoLimpo_ERecusadoForaDaLista()
    {
        var pdtic = await AbrirSesAsync();
        var dono = PeDono.DoPdtic(pdtic.Id);

        // Vazio conta como vazio (o diagnóstico é obrigatório)
        var vazio = await CamposComErroAsync(() => IncluirNoPdticAsync(pdtic.Id, "diagnostico_ambiente",
            new { diagnostico = Doc(new { type = "paragraph" }) }));
        Assert.Equal("Preencha este campo.", vazio["diagnostico"]);

        // Fora da lista: citação, título 1, link javascript, imagem de fora
        async Task<string> Erro(object doc) =>
            (await CamposComErroAsync(() => IncluirNoPdticAsync(pdtic.Id, "diagnostico_ambiente", new { diagnostico = doc })))["diagnostico"];
        Assert.Contains("citação", await Erro(Doc(new { type = "blockquote", content = new object[] { new { type = "paragraph" } } })));
        Assert.Equal("Use só títulos de nível 3 ou 4.",
            await Erro(Doc(new { type = "heading", attrs = new { level = 1 }, content = new object[] { new { type = "text", text = "T" } } })));
        Assert.Contains("http", await Erro(Doc(new
        {
            type = "paragraph",
            content = new object[] { new { type = "text", text = "x", marks = new object[] { new { type = "link", attrs = new { href = "javascript:alert(1)" } } } } }
        })));
        Assert.Contains("tachado", await Erro(Doc(new
        {
            type = "paragraph",
            content = new object[] { new { type = "text", text = "x", marks = new object[] { new { type = "strike" } } } }
        })));
        Assert.Contains("botão de imagem", await Erro(Doc(new { type = "image", attrs = new { src = "https://exemplo.com/a.png" } })));

        // Dentro da lista: guardado limpo (atributos que o módulo não usa saem)
        var doc = Doc(
            new { type = "heading", attrs = new { level = 3, textAlign = "center" }, content = new object[] { new { type = "text", text = "Organização da TIC" } } },
            new
            {
                type = "paragraph",
                content = new object[]
                {
                    new { type = "text", text = "Veja o ", marks = new object[] { new { type = "bold" } } },
                    new { type = "text", text = "portal", marks = new object[] { new { type = "link", attrs = new { href = "https://www.df.gov.br", target = "_blank", @class = "x" } } } }
                }
            });
        var registro = await IncluirNoPdticAsync(pdtic.Id, "diagnostico_ambiente", new { diagnostico = doc });
        var guardado = JsonNode.Parse(RegistroNoBanco(registro.Id).Dados)!["diagnostico"]!;
        Assert.Equal("{\"level\":3}", guardado["content"]![0]!["attrs"]!.ToJsonString());
        Assert.Equal("https://www.df.gov.br", guardado["content"]![1]!["content"]![1]!["marks"]![0]!["attrs"]!["href"]!.GetValue<string>());
        Assert.Null(guardado["content"]![1]!["content"]![1]!["marks"]![0]!["attrs"]!["class"]);
        Assert.Equal("Organização da TIC Veja o portal", registro.Rotulos["diagnostico"]);
        Assert.Equal("doc", registro.Dados["diagnostico"].GetProperty("type").GetString());
    }

    [Fact]
    public async Task TextoRico_ImagemDoProprioModulo_GanhaODonoEAbreParaQuemVeOOrgao()
    {
        var pdtic = await AbrirSesAsync();
        var dono = PeDono.DoPdtic(pdtic.Id);
        var png = await EnviarArquivoAsync(UserOrgaoSes, "organograma.png", Png());
        var pdf = await EnviarArquivoAsync(UserOrgaoSes, "ato.pdf", Pdf());
        var deOutro = await EnviarArquivoAsync(UserPeAdmin, "outro.png", Png());

        object ComImagem(long id, string prefixo = "api/planejamento/arquivos/") =>
            Doc(new { type = "paragraph", content = new object[] { new { type = "text", text = "Organograma:" } } },
                new { type = "image", attrs = new { src = prefixo + id, alt = "Organograma" } });

        Assert.Contains("PNG ou JPEG", (await CamposComErroAsync(() =>
            IncluirNoPdticAsync(pdtic.Id, "diagnostico_ambiente", new { diagnostico = ComImagem(pdf.Id) })))["diagnostico"]);
        Assert.Contains("não pode ser usada", (await CamposComErroAsync(() =>
            IncluirNoPdticAsync(pdtic.Id, "diagnostico_ambiente", new { diagnostico = ComImagem(deOutro.Id) })))["diagnostico"]);
        Assert.Contains("não foi encontrada", (await CamposComErroAsync(() =>
            IncluirNoPdticAsync(pdtic.Id, "diagnostico_ambiente", new { diagnostico = ComImagem(999_999) })))["diagnostico"]);

        var registro = await IncluirNoPdticAsync(pdtic.Id, "diagnostico_ambiente", new { diagnostico = ComImagem(png.Id, "/api/planejamento/arquivos/") });
        var arquivo = Context.PeArquivos.AsNoTracking().Single(a => a.Id == png.Id);
        Assert.Equal(PeDominios.DonoArquivo.Registro, arquivo.DonoTipo);
        Assert.Equal(registro.Id, arquivo.DonoId);
        // Guardado sem a barra, do jeito do contrato
        Assert.Equal("api/planejamento/arquivos/" + png.Id,
            JsonNode.Parse(RegistroNoBanco(registro.Id).Dados)!["diagnostico"]!["content"]![1]!["attrs"]!["src"]!.GetValue<string>());

        // Salvar de novo com a mesma imagem continua valendo
        await Registros.AtualizarAsync(dono, "diagnostico_ambiente", registro.Id, Salvar(new { diagnostico = ComImagem(png.Id) }), await Orgao());

        // Quem vê o órgão baixa; a equipe de outro órgão, não
        foreach (var user in new[] { UserConsultaSes, UserPeSgdi, UserAdminGeral })
            Assert.Equal("image/png", (await Arquivos.BaixarAsync(png.Id, await ContextoDe(user))).TipoMime);
        Assert.Equal(Codigo(ErrorCode.PeSemPermissao), await ErroAsync(async () => await Arquivos.BaixarAsync(png.Id, await ContextoDe(UserOrgaoSeec))));
    }

    private static PeTrilhaCampo CampoNaTrilha(PeTrilhaResponse trilha, string secao, string campo) =>
        trilha.Etapas.SelectMany(e => e.Passos).SelectMany(p => p.Secoes).Single(s => s.Chave == secao).Campos.Single(c => c.Chave == campo);
}
