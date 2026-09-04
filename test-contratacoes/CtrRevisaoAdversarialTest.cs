using api.Contratacoes;
using Microsoft.EntityFrameworkCore;
using Models.Contratacoes;
using service;
using service.Contratacoes;
using Xunit;

namespace test.contratacoes;

/// <summary>
/// Correções da revisão adversarial do backend: cada teste aqui reproduz um defeito
/// encontrado na revisão e trava o comportamento corrigido.
/// </summary>
public class CtrRevisaoAdversarialTest : CtrTestBase
{
    private const string Cabecalho =
        "Processo;Orgão;Sigla;Complemento / Área;Objeto;Categoria do Objeto;Chegada da analise - SGDI;"
        + "Chegada da analise- SUBGD;Chegada da analise- UGTIC;Data de Retorno ao Gab SGDI;"
        + "Data de Retorno ao Órgão Comunicante;Observação";

    private static string Planilha(params string[] linhas) =>
        "Analises de contratações;;;;;;;;;;;\r\n" + Cabecalho + "\r\n" + string.Join("\r\n", linhas) + "\r\n";

    // ── A1: aspas no meio do texto não abrem campo ────────────────────────────

    [Fact]
    public void Ler_AspasNoMeioDaCelula_NaoEngoleAsLinhasSeguintes()
    {
        // A polegada do rack ("24\"") não pode abrir modo "entre aspas" e sugar o
        // resto do arquivo para dentro de um campo só, sem erro nenhum
        var csv = Planilha(
            "04044-00002545/2024-62;Economia;SEEC;;Aquisição de rack 24\" padrão;"
            + "Infraestrutura de Rede;22/05/2026;;;;;",
            "00080-00224827/2024-02;Educação;SEEDF;;Switches de acesso;"
            + "Infraestrutura de Rede;23/05/2026;;;;;");

        var linhas = CtrCsv.Ler(Bytes1252(csv));

        Assert.Equal(2, linhas.Count);
        Assert.All(linhas, l => Assert.Null(l.Erro));
        Assert.Equal("Aquisição de rack 24\" padrão", linhas[0].Dados!.Objeto);
        Assert.Equal("00080-00224827/2024-02", linhas[1].Dados!.NumeroProcesso);
        Assert.Equal(new DateOnly(2026, 5, 23), linhas[1].Dados!.ChegadaSgdi);
    }

    [Fact]
    public void Ler_CampoQueCOMECAComAspas_ContinuaSendoCampoCitado()
    {
        // O caminho legítimo do RFC 4180 não pode ter regredido
        var csv = Planilha(
            "04044-00002545/2024-62;Economia;SEEC;;\"Switches; com \"\"garantia\"\" estendida\";"
            + "Infraestrutura de Rede;22/05/2026;;;;;");

        var linha = Assert.Single(CtrCsv.Ler(Bytes1252(csv)));

        Assert.Equal("Switches; com \"garantia\" estendida", linha.Dados!.Objeto);
    }

    // ── M1: PUT recusado não suja a entidade rastreada ────────────────────────

    [Fact]
    public async Task Atualizar_ManifestacaoRecusada_NaoDeixaRastroNaEntidade()
    {
        var ctx = await ContextoAnalistaAsync();
        var processo = SemearProcesso("04044-00002545/2024-62", p => p.ChegadaSgdi = DiasAtras(30));
        var service = NovoManifestacaoService();
        var criada = await service.CriarAsync(processo.Id, NovaManifestacaoIncisoI(), ctx);

        var invalida = NovaManifestacaoIncisoI();
        invalida.Criticidade = "Altíssima"; // fora do domínio
        invalida.OficioTcdf = "999/2026-GAB";

        await Assert.ThrowsAsync<ApiException>(() => service.AtualizarAsync(criada.Id, invalida, ctx));

        // Releitura no MESMO contexto: nada da tentativa recusada ficou na entidade
        var atual = await service.GetAsync(criada.Id);
        Assert.Equal(CtrDominios.Criticidade.Alta, atual.Criticidade);
        Assert.Equal("123/2026-GAB", atual.OficioTcdf);
        Assert.Null(atual.AlteradoEm);
    }

    // ── M2: restituição negada e data da mesma frase ──────────────────────────

    [Fact]
    public void Ler_ObservacaoComRestituicaoNEGADA_NaoMarcaRestituido()
    {
        // A observação vai entre aspas porque ela mesma tem ";" (o separador)
        var csv = Planilha(
            "04044-00002545/2024-62;Economia;SEEC;;Switches;Infraestrutura de Rede;22/05/2026;;;;;"
            + "\"Processo NÃO restituído; segue em análise na SUBGD desde 10/03/2026\"");

        var linha = Assert.Single(CtrCsv.Ler(Bytes1252(csv)));

        Assert.False(linha.Dados!.Restituido);
        Assert.Null(linha.Dados.RestituidoEm);
        Assert.Contains("NÃO restituído", linha.Dados.Observacao);
    }

    [Fact]
    public void Ler_RestituicaoUsaADataDaMESMAFrase()
    {
        var csv = Planilha(
            "04044-00002545/2024-62;Economia;SEEC;;Switches;Infraestrutura de Rede;01/02/2026;;;;;"
            + "Recebido do órgão em 01/02/2026. Processo restituído em 05/05/2026 por falta do formulário.");

        var linha = Assert.Single(CtrCsv.Ler(Bytes1252(csv)));

        Assert.True(linha.Dados!.Restituido);
        Assert.Equal(new DateOnly(2026, 5, 5), linha.Dados.RestituidoEm);
    }

    // ── M3: número liberado quando a primeira ocorrência é rejeitada ──────────

    [Fact]
    public void Ler_PrimeiraOcorrenciaRejeitada_LiberaONumeroParaASegunda()
    {
        var csv = Planilha(
            "04044-00002545/2024-62;Economia;SEEC;;Switches;Categoria que não existe;22/05/2026;;;;;",
            "04044-00002545/2024-62;Economia;SEEC;;Switches;Infraestrutura de Rede;22/05/2026;;;;;");

        var linhas = CtrCsv.Ler(Bytes1252(csv));

        Assert.Contains("categoria desconhecida", linhas[0].Erro);
        // A segunda NÃO pode ser descartada como "duplicado" — senão as duas se perdem
        Assert.Null(linhas[1].Erro);
        Assert.Equal("04044-00002545/2024-62", linhas[1].Dados!.NumeroProcesso);
    }

    [Fact]
    public void Ler_DuplicidadeEntreLinhasVALIDAS_ContinuaSendoRejeitada()
    {
        var csv = Planilha(
            "04044-00002545/2024-62;Economia;SEEC;;Switches;Infraestrutura de Rede;22/05/2026;;;;;",
            "04044-00002545/2024-62;Economia;SEEC;;Switches (de novo);Infraestrutura de Rede;23/05/2026;;;;;");

        var linhas = CtrCsv.Ler(Bytes1252(csv));

        Assert.Null(linhas[0].Erro);
        Assert.Contains("duplicado no arquivo, linha 3", linhas[1].Erro);
    }

    // ── B1: injeção de fórmula no export ──────────────────────────────────────

    [Fact]
    public void Escrever_TextoLivreQueViraFormula_RecebeApostrofo_EVoltaIntactoNaLeitura()
    {
        var processo = new CtrProcessoResponse
        {
            NumeroProcesso = "04044-00002545/2024-62",
            OrgaoNome = "Economia",
            OrgaoSigla = "SEEC",
            Objeto = "=1+1",
            ComplementoArea = "@area",
            Observacao = "-desconto aplicado",
            CategoriaObjeto = CtrDominios.CategoriaObjeto.InfraestruturaRede,
            ChegadaSgdi = new DateOnly(2026, 5, 22),
            UgticNaoSeAplica = true
        };

        var bytes = CtrCsv.Escrever(new List<CtrProcessoResponse> { processo });
        var texto = System.Text.Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);

        Assert.Contains(";'=1+1;", texto);
        Assert.Contains(";'@area;", texto);
        Assert.Contains(";'-desconto aplicado;", texto);
        // Datas, o "-" da UGTIC e o Sim/Não NÃO são neutralizados
        Assert.Contains(";-;", texto);
        Assert.Contains(";22/05/2026;", texto);
        Assert.Contains(";Não;", texto);

        // Round-trip fiel: a leitura desfaz o apóstrofo
        var linha = Assert.Single(CtrCsv.Ler(bytes));
        Assert.Equal("=1+1", linha.Dados!.Objeto);
        Assert.Equal("@area", linha.Dados.ComplementoArea);
        Assert.Equal("-desconto aplicado", linha.Dados.Observacao);
        Assert.True(linha.Dados.UgticNaoSeAplica);
    }

    [Fact]
    public void Ler_ApostrofoLegitimoNoInicioDoTexto_EhPreservado()
    {
        var csv = Planilha(
            "04044-00002545/2024-62;Economia;SEEC;;'Termo de referência' revisado;"
            + "Infraestrutura de Rede;22/05/2026;;;;;");

        var linha = Assert.Single(CtrCsv.Ler(Bytes1252(csv)));

        Assert.Equal("'Termo de referência' revisado", linha.Dados!.Objeto);
    }

    // ── B2: dd/MM completado que cairia no futuro ─────────────────────────────

    [Fact]
    public void Ler_RestituicaoSemAnoQueCairiaNoFuturo_UsaOAnoAnterior()
    {
        // Chegada em fevereiro e "restituído em 04/12": dezembro do ano anterior,
        // não o de dezembro que ainda não chegou (que faria a linha ser rejeitada)
        var csv = Planilha(
            "04044-00002545/2024-62;Economia;SEEC;;Switches;Infraestrutura de Rede;01/02/2026;;;;;"
            + "Processo restituído ao órgão em 04/12 por ausência do formulário");

        var linha = Assert.Single(CtrCsv.Ler(Bytes1252(csv)));

        Assert.True(linha.Dados!.Restituido);
        Assert.Equal(new DateOnly(2025, 12, 4), linha.Dados.RestituidoEm);
        Assert.True(linha.Dados.RestituidoEm <= Hoje);
    }

    // ── B3: coerência das datas da manifestação ───────────────────────────────

    [Fact]
    public async Task Criar_ComunicadaDesdeFutura_EhRecusada()
    {
        var ctx = await ContextoAnalistaAsync();
        var processo = SemearProcesso("04044-00002545/2024-62");
        var dto = NovaManifestacaoIncisoI();
        dto.ComunicadaDesde = Hoje.AddDays(1);

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            NovoManifestacaoService().CriarAsync(processo.Id, dto, ctx));

        Assert.Equal((int)ErrorCode.CtrManifestacaoInvalida, ex.Error.Code);
        Assert.Contains("futura", ex.Error.Message);
    }

    [Fact]
    public async Task Criar_ComunicadaDesdePosteriorAoOficio_EhRecusada()
    {
        var ctx = await ContextoAnalistaAsync();
        var processo = SemearProcesso("04044-00002545/2024-62");
        var dto = NovaManifestacaoIncisoI();
        dto.DataOficio = DiasAtras(10);
        dto.ComunicadaDesde = DiasAtras(2); // depois do ofício

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            NovoManifestacaoService().CriarAsync(processo.Id, dto, ctx));

        Assert.Equal((int)ErrorCode.CtrManifestacaoInvalida, ex.Error.Code);
        Assert.Contains("posterior à data do ofício", ex.Error.Message);
    }

    // ── B4: UTF-8 sem BOM ─────────────────────────────────────────────────────

    [Fact]
    public void Ler_Utf8SemBom_NaoDaMojibake()
    {
        // É o que o Google Sheets e o LibreOffice exportam
        var csv = Planilha(
            "00193-00000779/2026-54;Fundação de Apoio à Pesquisa do Distrito Federal;FAPDF;"
            + "Gerência de Informática;Solução de Voz sobre IP (VoIP);Telefonia / VoIP;09/06/2026;;;;;");

        var linha = Assert.Single(CtrCsv.Ler(System.Text.Encoding.UTF8.GetBytes(csv)));

        Assert.Equal("Fundação de Apoio à Pesquisa do Distrito Federal", linha.Dados!.OrgaoNome);
        Assert.Equal("Gerência de Informática", linha.Dados.ComplementoArea);
        Assert.Equal("Solução de Voz sobre IP (VoIP)", linha.Dados.Objeto);
    }

    [Fact]
    public void Ler_PlanilhaReal1252_ContinuaSendoLidaComoWindows1252()
    {
        // A tentativa de UTF-8 estrito não pode "sequestrar" o arquivo real da equipe
        var linhas = CtrCsv.Ler(PlanilhaReal());

        Assert.Equal(37, linhas.Count);
        Assert.All(linhas, l => Assert.Null(l.Erro));
        Assert.Contains(linhas, l => l.Dados!.OrgaoNome == "Serviço de Limpeza Urbana do Distrito Federal");
    }

    // ── B5: domínio inválido devolve vazio ────────────────────────────────────

    [Fact]
    public async Task Listar_EstagioForaDoDominio_NaoDevolveNada()
    {
        var ctx = await ContextoAnalistaAsync();
        var processo = SemearProcesso("04044-00002545/2024-62");
        var service = NovoManifestacaoService();
        await service.CriarAsync(processo.Id, NovaManifestacaoIncisoI(), ctx);

        var todas = await service.ListarAsync(new CtrManifestacaoFiltro { PageSize = 50 });
        Assert.Single(todas.Items);

        var invalida = await service.ListarAsync(new CtrManifestacaoFiltro
        { Estagio = "Em diligência no TCDF", PageSize = 50 });

        Assert.Empty(invalida.Items);
        Assert.Equal(0, invalida.TotalItems);
    }

    // ── B6: manifestações de processo excluído somem da lista do processo ─────

    [Fact]
    public async Task ListarDoProcesso_DeProcessoExcluido_VemVazio()
    {
        var ctx = await ContextoAnalistaAsync();
        var processo = SemearProcesso("04044-00002545/2024-62");
        var service = NovoManifestacaoService();
        await service.CriarAsync(processo.Id, NovaManifestacaoIncisoI(), ctx);

        Assert.Single(await service.ListarDoProcessoAsync(processo.Id));

        await NovoProcessoService().ExcluirAsync(processo.Id, ctx);

        Assert.Empty(await service.ListarDoProcessoAsync(processo.Id));
    }

    // ── B7: informar a data da UGTIC reativa a etapa ──────────────────────────

    [Fact]
    public async Task Checkpoint_DataNaUgtic_DesmarcaONaoSeAplica()
    {
        var ctx = await ContextoAnalistaAsync();
        var service = NovoProcessoService();
        var dto = NovoProcessoDto();
        dto.ChegadaSgdi = DiasAtras(20);
        dto.ChegadaSubgd = DiasAtras(18);
        dto.UgticNaoSeAplica = true;
        var criado = await service.CriarAsync(dto, ctx);
        Assert.True(criado.UgticNaoSeAplica);

        // Informar a data É o gesto de reativar a etapa (antes dava erro de coerência)
        var atualizado = await service.RegistrarCheckpointAsync(criado.Id,
            new CtrCheckpointDTO { Etapa = CtrDominios.Etapa.ChegadaUgtic, Data = DiasAtras(10) }, ctx);

        Assert.False(atualizado.UgticNaoSeAplica);
        Assert.Equal(DiasAtras(10), atualizado.ChegadaUgtic);
        Assert.Equal(CtrDominios.Situacao.EmAnaliseUgtic, atualizado.Situacao);

        var persistido = await Context.CtrProcessos.AsNoTracking().FirstAsync(p => p.Id == criado.Id);
        Assert.False(persistido.UgticNaoSeAplica);
    }

    // ── B10: ordem determinística das contagens do painel ─────────────────────

    [Fact]
    public async Task Painel_ContagensEmpatadas_SeguemAOrdemDoDominio()
    {
        SemearProcesso("04044-00000001/2026-11", p => p.ChegadaSgdi = DiasAtras(10));   // Em análise na SGDI
        SemearProcesso("04044-00000002/2026-12", p =>                                    // Restituído
        {
            p.ChegadaSgdi = DiasAtras(12);
            p.Restituido = true;
            p.RestituidoEm = DiasAtras(5);
            p.RestituidoMotivo = "devolvido";
        });

        var painel = await NovoProcessoService().MontarPainelAsync(15);
        var comQuantidade = painel.PorSituacao.Where(c => c.Quantidade > 0).Select(c => c.Chave).ToList();

        // Empate em 1: vence a ordem do domínio (Restituído vem antes de Em análise na SGDI)
        Assert.Equal(new[] { CtrDominios.Situacao.Restituido, CtrDominios.Situacao.EmAnaliseSgdi }, comQuantidade);

        var categorias = painel.PorCategoria.Select(c => c.Chave).ToList();
        Assert.Equal(categorias.OrderBy(c => c), categorias); // empate por Chave
    }
}
