using System.Text.Json;
using api.Planejamento;
using Microsoft.EntityFrameworkCore;
using Models.Planejamento;
using service;
using service.Planejamento;
using Xunit;

namespace test.planejamento;

/// <summary>
/// Modelo do documento pelo administrador do módulo: ler com os marcadores, criar, alterar,
/// apagar e ordenar capítulos e blocos, com as travas dos nove conteúdos, o histórico e a
/// mudança valendo na hora para os órgãos.
/// </summary>
public class PeDocModeloTest : PeDocumentoTestBase
{
    private static int Codigo(ApiException ex) => ex.Error.Code;

    [Fact]
    public async Task Obter_ArvoreNaOrdem_ComOsBlocosEOsMarcadores()
    {
        var campo = await Modelo.CriarCampoAsync(new PeCampoCriarDTO { SecaoId = Secao("nomes").Id, Chave = "ouvidoria", Rotulo = "Nome da ouvidoria", Tipo = "texto_curto" }, EmailAdmin);

        var modelo = await ModeloDoc.ObterAsync(null);

        Assert.Equal("pdtic", modelo.Tipo);
        Assert.Equal("Modelo da SGDI para o PDTIC", modelo.Nome);
        var chaves = modelo.Capitulos.Select(c => c.Chave).ToList();
        // O subcapítulo logo depois do capítulo dele
        Assert.Equal(chaves.IndexOf("diagnostico") + 1, chaves.IndexOf("diagnostico_organizacao"));
        Assert.Equal(modelo.Capitulos.Single(c => c.Chave == "diagnostico").Id, modelo.Capitulos.Single(c => c.Chave == "diagnostico_organizacao").PaiId);
        var ativos = modelo.Capitulos.Single(c => c.Chave == "ativos");
        Assert.True(ativos.Travado);
        Assert.Equal("II", ativos.IncisoDecreto);
        Assert.Equal(new[] { "texto", "tabela_secao" }, ativos.Blocos.Select(b => b.Tipo));
        Assert.Equal("ativos", ativos.Blocos[1].Config.GetProperty("Secao").GetString());
        Assert.True(ativos.Blocos[1].Config.GetProperty("PaginaDeitada").GetBoolean());

        // Os marcadores do ciclo são só do RA (F1, B07): o modelo do PDTIC não os oferece
        Assert.Equal(PeDocMarcadores.Fixos.Select(m => m.Chave).Where(c => !PeDocMarcadores.DoCiclo.Contains(c)).Append("nomes.ouvidoria"),
            modelo.Marcadores.Select(m => m.Chave));
        Assert.All(modelo.Marcadores, m => Assert.False(string.IsNullOrWhiteSpace(m.Descricao)));

        var invalido = await Assert.ThrowsAsync<ApiException>(() => ModeloDoc.ObterAsync("xyz"));
        Assert.Equal((int)ErrorCode.PeDadosInvalidos, Codigo(invalido));
    }

    [Fact]
    public async Task CriarCapitulo_ESubcapitulo_ComChaveGerada_EHistorico()
    {
        var admin = await Admin();
        var criado = await ModeloDoc.CriarCapituloAsync(new PeDocCapituloCriarDTO { Titulo = "Glossário da saúde digital", PassoChave = "diagnostico.ativos" }, admin);

        Assert.Equal("glossario_da_saude_digital", criado.Chave);
        Assert.False(criado.Sistema);
        Assert.False(criado.Travado);
        Assert.False(criado.Obrigatorio);
        Assert.True(criado.Numerado);
        Assert.Equal(Context.PeDocCapitulos.Where(c => c.PaiId == null).Max(c => c.Ordem), criado.Ordem);
        Assert.Single(HistoricoDe(PeDominios.EntidadeHistorico.DocCapitulo, criado.Id), h => h.Acao == PeDominios.AcaoHistorico.Criacao);

        var sub = await ModeloDoc.CriarCapituloAsync(new PeDocCapituloCriarDTO { Titulo = "Termos de saúde", PaiId = criado.Id, Numerado = false }, admin);
        Assert.Equal(criado.Id, sub.PaiId);
        Assert.Equal(1, sub.Ordem);

        // A chave informada vale do jeito que vier (sem acento, minúsculas, sublinhado); sem letra no começo, 400
        var comChave = await ModeloDoc.CriarCapituloAsync(new PeDocCapituloCriarDTO { Tipo = "pdtic", Titulo = "Anexo técnico", Chave = "Anexo-Técnico" }, admin);
        Assert.Equal("anexo_tecnico", comChave.Chave);
        Assert.Equal((int)ErrorCode.PeDadosInvalidos, Codigo(await Assert.ThrowsAsync<ApiException>(() =>
            ModeloDoc.CriarCapituloAsync(new PeDocCapituloCriarDTO { Titulo = "Outro", Chave = "2026" }, admin))));

        // Subcapítulo não tem subcapítulo; chave repetida; passo que não existe
        Assert.Equal((int)ErrorCode.PeDadosInvalidos, Codigo(await Assert.ThrowsAsync<ApiException>(() =>
            ModeloDoc.CriarCapituloAsync(new PeDocCapituloCriarDTO { Titulo = "Neto", PaiId = sub.Id }, admin))));
        Assert.Equal((int)ErrorCode.PeChaveDuplicada, Codigo(await Assert.ThrowsAsync<ApiException>(() =>
            ModeloDoc.CriarCapituloAsync(new PeDocCapituloCriarDTO { Titulo = "Outro", Chave = "introducao" }, admin))));
        Assert.Equal((int)ErrorCode.PeDadosInvalidos, Codigo(await Assert.ThrowsAsync<ApiException>(() =>
            ModeloDoc.CriarCapituloAsync(new PeDocCapituloCriarDTO { Titulo = "Outro", PassoChave = "nao.existe" }, admin))));
        Assert.Equal((int)ErrorCode.PeDadosInvalidos, Codigo(await Assert.ThrowsAsync<ApiException>(() =>
            ModeloDoc.CriarCapituloAsync(new PeDocCapituloCriarDTO { Titulo = " " }, admin))));

        // O capítulo novo aparece no documento do órgão (o passo dos ativos está em todos os níveis)
        var pdtic = await AbrirSesAsync();
        var documento = await DocumentoAsync(pdtic.Id);
        Assert.Equal("17", Cap(documento, criado.Chave).Numero);
        Assert.Null(Cap(documento, sub.Chave).Numero);
    }

    [Fact]
    public async Task AtualizarCapitulo_TravadoContinuaObrigatorio_ENaoMudaDePasso()
    {
        var admin = await Admin();
        var ativos = CapituloDoModelo("ativos");

        var ex = await Assert.ThrowsAsync<ApiException>(() => ModeloDoc.AtualizarCapituloAsync(ativos.Id,
            new PeDocCapituloAtualizarDTO { Obrigatorio = false, Informados = new HashSet<string> { "Obrigatorio" } }, admin));
        Assert.Equal((int)ErrorCode.PeItemTravado, Codigo(ex));
        ex = await Assert.ThrowsAsync<ApiException>(() => ModeloDoc.AtualizarCapituloAsync(ativos.Id,
            new PeDocCapituloAtualizarDTO { PassoChave = "diagnostico.swot", Informados = new HashSet<string> { "PassoChave" } }, admin));
        Assert.Equal((int)ErrorCode.PeItemTravado, Codigo(ex));

        var renomeado = await ModeloDoc.AtualizarCapituloAsync(ativos.Id,
            new PeDocCapituloAtualizarDTO { Titulo = "Inventário de soluções e ativos de TIC", Informados = new HashSet<string> { "Titulo" } }, admin);
        Assert.Equal("Inventário de soluções e ativos de TIC", renomeado.Titulo);
        var historico = HistoricoDe(PeDominios.EntidadeHistorico.DocCapitulo, ativos.Id);
        Assert.Contains(historico, h => h.Acao == PeDominios.AcaoHistorico.Alteracao && h.Depois!.Contains("Inventário"));

        // Capítulo do sistema, não travado, fica opcional (o órgão passa a poder esconder)
        var introducao = CapituloDoModelo("introducao");
        var opcional = await ModeloDoc.AtualizarCapituloAsync(introducao.Id,
            new PeDocCapituloAtualizarDTO { Obrigatorio = false, Informados = new HashSet<string> { "Obrigatorio" } }, admin);
        Assert.False(opcional.Obrigatorio);
        var pdtic = await AbrirSesAsync();
        var oculto = await Documentos.AtualizarCapituloAsync(pdtic.Id, introducao.Id,
            new PeDocCapituloOrgaoDTO { Oculto = true, Informados = new HashSet<string> { "Oculto" } }, await Orgao());
        Assert.True(oculto.Oculto);

        // Sem mudança de fato, sem histórico novo
        var antes = Context.PeModeloHistorico.Count();
        await ModeloDoc.AtualizarCapituloAsync(introducao.Id, new PeDocCapituloAtualizarDTO { Obrigatorio = false, Informados = new HashSet<string> { "Obrigatorio" } }, admin);
        Assert.Equal(antes, Context.PeModeloHistorico.Count());
    }

    [Fact]
    public async Task ExcluirCapitulo_TravadoEDoSistemaNao_OCriadoSaiComOsSubcapitulos()
    {
        var admin = await Admin();
        Assert.Equal((int)ErrorCode.PeItemTravado, Codigo(await Assert.ThrowsAsync<ApiException>(() => ModeloDoc.ExcluirCapituloAsync(CapituloDoModelo("ativos").Id, admin))));
        Assert.Equal((int)ErrorCode.PeItemDoSistema, Codigo(await Assert.ThrowsAsync<ApiException>(() => ModeloDoc.ExcluirCapituloAsync(CapituloDoModelo("termos").Id, admin))));
        Assert.Equal((int)ErrorCode.PeDocCapituloNaoEncontrado, Codigo(await Assert.ThrowsAsync<ApiException>(() => ModeloDoc.ExcluirCapituloAsync(999_999, admin))));

        var criado = await ModeloDoc.CriarCapituloAsync(new PeDocCapituloCriarDTO { Titulo = "Capítulo extra" }, admin);
        var sub = await ModeloDoc.CriarCapituloAsync(new PeDocCapituloCriarDTO { Titulo = "Sub extra", PaiId = criado.Id }, admin);

        await ModeloDoc.ExcluirCapituloAsync(criado.Id, admin);
        await ModeloDoc.ExcluirCapituloAsync(criado.Id, admin);

        Assert.NotNull(Context.PeDocCapitulos.AsNoTracking().Single(c => c.Id == criado.Id).ExcluidoEm);
        Assert.NotNull(Context.PeDocCapitulos.AsNoTracking().Single(c => c.Id == sub.Id).ExcluidoEm);
        Assert.DoesNotContain((await ModeloDoc.ObterAsync("pdtic")).Capitulos, c => c.Id == criado.Id || c.Id == sub.Id);
        Assert.Single(HistoricoDe(PeDominios.EntidadeHistorico.DocCapitulo, criado.Id), h => h.Acao == PeDominios.AcaoHistorico.Exclusao);
        var editar = await Assert.ThrowsAsync<ApiException>(() => ModeloDoc.AtualizarCapituloAsync(criado.Id,
            new PeDocCapituloAtualizarDTO { Titulo = "x", Informados = new HashSet<string> { "Titulo" } }, admin));
        Assert.Equal((int)ErrorCode.PeItemExcluido, Codigo(editar));
    }

    [Fact]
    public async Task OrdenarCapitulos_DoMesmoNivel_ComHistorico()
    {
        var admin = await Admin();
        var diagnostico = CapituloDoModelo("diagnostico");
        var subs = Context.PeDocCapitulos.AsNoTracking().Where(c => c.PaiId == diagnostico.Id).OrderBy(c => c.Ordem).Select(c => c.Id).ToList();
        var nova = subs.AsEnumerable().Reverse().ToList();

        var modelo = await ModeloDoc.OrdenarCapitulosAsync(new PeOrdemDTO { Ids = nova }, admin);

        Assert.Equal(nova, modelo.Capitulos.Where(c => c.PaiId == diagnostico.Id).Select(c => c.Id));
        Assert.Contains(HistoricoDe(PeDominios.EntidadeHistorico.DocCapitulo, nova[0]), h => h.Acao == PeDominios.AcaoHistorico.Ordem);
        var faltando = await Assert.ThrowsAsync<ApiException>(() => ModeloDoc.OrdenarCapitulosAsync(new PeOrdemDTO { Ids = nova.Skip(1).ToList() }, admin));
        Assert.Equal((int)ErrorCode.PeOrdemInvalida, Codigo(faltando));

        // A numeração do órgão acompanha a nova ordem
        await DefinirNivelDoOrgaoAsync(OrgaoSes, "avancado");
        var documento = await DocumentoAsync((await AbrirSesAsync()).Id);
        Assert.Equal("6.1", Cap(documento, "diagnostico_capacidade").Numero);
    }

    [Fact]
    public async Task Blocos_CriarAtualizarOrdenarExcluir_ComConfigConferido()
    {
        var admin = await Admin();
        var termos = CapituloDoModelo("termos");

        var criado = await ModeloDoc.CriarBlocoAsync(new PeDocBlocoCriarDTO
        {
            CapituloId = termos.Id, Tipo = "tabela_secao", Config = Json(new { secao = "ativos", colunas = new[] { "nome", "situacao" }, paginaDeitada = true })
        }, admin);
        Assert.Equal("ativos", criado.Config.GetProperty("Secao").GetString());
        Assert.Equal(new[] { "nome", "situacao" }, criado.Config.GetProperty("Colunas").EnumerateArray().Select(c => c.GetString()));
        Assert.Single(HistoricoDe(PeDominios.EntidadeHistorico.DocBloco, criado.Id), h => h.Acao == PeDominios.AcaoHistorico.Criacao);

        var atualizado = await ModeloDoc.AtualizarBlocoAsync(criado.Id, new PeDocBlocoAtualizarDTO { Config = Json(new { Secao = "ativos" }) }, admin);
        Assert.False(atualizado.Config.TryGetProperty("Colunas", out _));
        Assert.False(atualizado.Config.TryGetProperty("PaginaDeitada", out _));

        foreach (var ruim in new object[]
                 {
                     new { Secao = "nao_existe" }, new { Secao = "ativos", Colunas = new[] { "inventada" } }, new { Secao = "ativos", Outra = 1 },
                     new { Secao = "ativos", PaginaDeitada = "sim" }
                 })
        {
            var ex = await Assert.ThrowsAsync<ApiException>(() => ModeloDoc.AtualizarBlocoAsync(criado.Id, new PeDocBlocoAtualizarDTO { Config = Json(ruim) }, admin));
            Assert.Equal((int)ErrorCode.PeDocConfigInvalida, Codigo(ex));
        }
        Assert.Equal((int)ErrorCode.PeDocConfigInvalida, Codigo(await Assert.ThrowsAsync<ApiException>(() =>
            ModeloDoc.CriarBlocoAsync(new PeDocBlocoCriarDTO { CapituloId = termos.Id, Tipo = "grafico" }, admin))));

        var blocos = Context.PeDocBlocos.AsNoTracking().Where(b => b.CapituloId == termos.Id && b.ExcluidoEm == null).OrderBy(b => b.Ordem).Select(b => b.Id).ToList();
        var ordem = blocos.AsEnumerable().Reverse().ToList();
        var modelo = await ModeloDoc.OrdenarBlocosAsync(new PeOrdemDTO { Ids = ordem }, admin);
        Assert.Equal(ordem, modelo.Capitulos.Single(c => c.Id == termos.Id).Blocos.Select(b => b.Id));

        await ModeloDoc.ExcluirBlocoAsync(criado.Id, admin);
        Assert.DoesNotContain((await ModeloDoc.ObterAsync(null)).Capitulos.Single(c => c.Id == termos.Id).Blocos, b => b.Id == criado.Id);
        Assert.Equal((int)ErrorCode.PeItemExcluido, Codigo(await Assert.ThrowsAsync<ApiException>(() =>
            ModeloDoc.AtualizarBlocoAsync(criado.Id, new PeDocBlocoAtualizarDTO { Config = Json(new { Secao = "ativos" }) }, admin))));
    }

    [Fact]
    public async Task BlocoDosNoveConteudos_NaoMudaDeSecao_NemSeApaga()
    {
        var admin = await Admin();
        var bloco = BlocoDoModelo("ativos", "tabela_secao");

        var trocar = await Assert.ThrowsAsync<ApiException>(() =>
            ModeloDoc.AtualizarBlocoAsync(bloco.Id, new PeDocBlocoAtualizarDTO { Config = Json(new { Secao = "necessidades" }) }, admin));
        Assert.Equal((int)ErrorCode.PeItemTravado, Codigo(trocar));
        Assert.Equal((int)ErrorCode.PeItemTravado, Codigo(await Assert.ThrowsAsync<ApiException>(() => ModeloDoc.ExcluirBlocoAsync(bloco.Id, admin))));
        Assert.Equal((int)ErrorCode.PeItemTravado, Codigo(await Assert.ThrowsAsync<ApiException>(() =>
            ModeloDoc.AtualizarBlocoAsync(BlocoDoModelo("seguranca", "lista_tema").Id, new PeDocBlocoAtualizarDTO { Config = Json(new { Tema = "sistemas" }) }, admin))));

        // As colunas e a página deitada mudam
        var colunas = await ModeloDoc.AtualizarBlocoAsync(bloco.Id, new PeDocBlocoAtualizarDTO { Config = Json(new { Secao = "ativos", Colunas = new[] { "nome", "tipo" } }) }, admin);
        Assert.Equal(2, colunas.Config.GetProperty("Colunas").GetArrayLength());
    }

    [Fact]
    public async Task ImagemNoTextoPadrao_PassaASerDoModelo_EQualquerPapelBaixa()
    {
        var admin = await Admin();
        var imagem = await EnviarArquivoAsync(UserPeAdmin, "selo.png", PngDeVerdade());
        var bloco = BlocoDoModelo("conclusao");

        await ModeloDoc.AtualizarBlocoAsync(bloco.Id, new PeDocBlocoAtualizarDTO
        {
            Config = Json(new { Texto = Doc(Paragrafo("Conclusão com o selo."), new { type = "image", attrs = new { src = $"api/planejamento/arquivos/{imagem.Id}" } }) })
        }, admin);

        var arquivo = Context.PeArquivos.AsNoTracking().Single(a => a.Id == imagem.Id);
        Assert.Equal(PeDominios.DonoArquivo.DocModelo, arquivo.DonoTipo);
        Assert.NotEmpty((await Arquivos.BaixarAsync(imagem.Id, await ContextoDe(UserConsultaSes))).Conteudo);

        // O órgão que usa o texto do modelo pode gravar um texto com a mesma imagem
        var pdtic = await AbrirSesAsync();
        var texto = JsonSerializer.SerializeToElement(Doc(Paragrafo("Nossa conclusão."), new { type = "image", attrs = new { src = $"api/planejamento/arquivos/{imagem.Id}" } }));
        Assert.True((await Documentos.SalvarTextoAsync(pdtic.Id, bloco.Id, texto, await Orgao())).EditadoPeloOrgao);
        Assert.Equal(PeDominios.DonoArquivo.DocModelo, Context.PeArquivos.AsNoTracking().Single(a => a.Id == imagem.Id).DonoTipo);
    }
}
