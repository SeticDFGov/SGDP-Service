using api.Planejamento;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Models.Planejamento;
using Xunit;

namespace test.planejamento;

/// <summary>
/// As rotas do caminho da aprovação (E7, rodada A) no PePdticController, com os status e os
/// corpos do contrato: a prévia do envio, o envio (400 com Pendencias), publicar (400 com
/// Campos), encerrar (corpo vazio aceito), a revisão (201), o registro externo (201 e 400 com
/// Campos pelo nome do campo do corpo), as versões e, na fila, a deliberação com o Documento.
/// </summary>
public class PeAprovacaoControllerTest : PeAprovacaoTestBase
{
    private static object? Propriedade(object? corpo, string nome) => corpo!.GetType().GetProperty(nome)!.GetValue(corpo);

    [Fact]
    public async Task Envio_Previa_EOEnvioCom400EAsPendencias()
    {
        var pdtic = await AbrirSesAsync();
        var orgao = ControladorPdtic(UserOrgaoSes);

        var (status, corpo) = Resultado(await orgao.Envio(pdtic.Id));
        Assert.Equal(StatusCodes.Status200OK, status);
        Assert.False(Assert.IsType<PeEnvioResponse>(corpo).PodeEnviar);

        (status, corpo) = Resultado(await orgao.Enviar(pdtic.Id));
        Assert.Equal(StatusCodes.Status400BadRequest, status);
        Assert.Equal(Codigo(ErrorCode.PePdticComPendencias), CodigoDe(corpo));
        var pendencias = Assert.IsAssignableFrom<IReadOnlyList<PePendenciaResponse>>(Propriedade(corpo, "Pendencias"));
        Assert.Equal(Passo("preparacao.abrangencia").Id, pendencias[0].PassoId);

        await PreencherElaboracaoAsync(pdtic.Id);
        (status, corpo) = Resultado(await orgao.Enviar(pdtic.Id));
        Assert.Equal(StatusCodes.Status200OK, status);
        Assert.Equal("em_aprovacao", Assert.IsType<PePdticResponse>(corpo).Situacao);

        // De novo: 409 com a mensagem da situação; a consulta recebe 403
        (status, corpo) = Resultado(await orgao.Enviar(pdtic.Id));
        Assert.Equal(StatusCodes.Status409Conflict, status);
        Assert.Equal(Codigo(ErrorCode.PePdticSituacaoInvalida), CodigoDe(corpo));
        (status, _) = Resultado(await ControladorPdtic(UserConsultaSes).Enviar(pdtic.Id));
        Assert.Equal(StatusCodes.Status403Forbidden, status);

        // Na fila, a deliberação com o PDF enviado
        var (_, fila) = Resultado(await ControladorDeliberacoes(UserPeCgtic).Listar(new PeDeliberacoesConsulta { ObjetoTipo = "pdtic" }));
        var item = Assert.Single(Assert.IsType<api.Common.PagedResponse<PeDeliberacaoResponse>>(fila).Items);
        Assert.Equal(2, item.Documento!.Numero);
    }

    [Fact]
    public async Task Publicar_400ComCampos_Encerrar_ERevisao201()
    {
        var pdtic = await ProntoParaEnviarAsync();
        await EnviarAsync(pdtic.Id);
        await AprovarNoCgticAsync(pdtic.Id);
        var orgao = ControladorPdtic(UserOrgaoSes);

        var (status, corpo) = Resultado(await orgao.Publicar(pdtic.Id));
        Assert.Equal(StatusCodes.Status400BadRequest, status);
        Assert.Equal(Codigo(ErrorCode.PePublicacaoIncompleta), CodigoDe(corpo));
        var campos = Assert.IsAssignableFrom<IReadOnlyDictionary<string, string>>(Propriedade(corpo, "Campos"));
        Assert.Contains("endereco", campos.Keys);

        await IncluirNoPdticAsync(pdtic.Id, "publicacao", new { data = "2026-09-15", endereco = Endereco });
        (status, corpo) = Resultado(await orgao.Publicar(pdtic.Id));
        Assert.Equal(StatusCodes.Status200OK, status);
        Assert.Equal("publicado", Assert.IsType<PePdticResponse>(corpo).Situacao);

        // Encerrar sem corpo: a equipe sem a aprovação da autoridade máxima recebe 409
        (status, corpo) = Resultado(await orgao.Encerrar(pdtic.Id, default));
        Assert.Equal(StatusCodes.Status409Conflict, status);
        Assert.Equal(Codigo(ErrorCode.PeEncerramentoRecusado), CodigoDe(corpo));

        // Revisão: 201 com o PDTIC novo; a segunda, 409
        (status, corpo) = Resultado(await orgao.Revisao(pdtic.Id, Json(new { Justificativa = "Mudou a estrutura." })));
        Assert.Equal(StatusCodes.Status201Created, status);
        Assert.Equal("1.1", Assert.IsType<PePdticResponse>(corpo).Versao);
        (status, corpo) = Resultado(await orgao.Revisao(pdtic.Id, default));
        Assert.Equal(StatusCodes.Status409Conflict, status);
        Assert.Equal(Codigo(ErrorCode.PeRevisaoEmAndamento), CodigoDe(corpo));

        // As versões do órgão, da mais nova para a mais antiga
        (status, corpo) = Resultado(await ControladorPdtic(UserConsultaSes).Versoes(null));
        Assert.Equal(StatusCodes.Status200OK, status);
        Assert.Equal(new[] { "1.1", "1.0" }, Assert.IsType<List<PePdticResponse>>(corpo).Select(p => p.Versao));
    }

    [Fact]
    public async Task RegistrarExterno_201_E400ComOsCampos()
    {
        var orgao = ControladorPdtic(UserOrgaoSes);
        var (status, corpo) = Resultado(await orgao.RegistrarExterno(Json(new { Versao = "1.0" })));
        Assert.Equal(StatusCodes.Status400BadRequest, status);
        Assert.Equal(Codigo(ErrorCode.PeRegistroExternoInvalido), CodigoDe(corpo));
        var campos = Assert.IsAssignableFrom<IReadOnlyDictionary<string, string>>(Propriedade(corpo, "Campos"));
        Assert.Contains("VigenciaInicio", campos.Keys);
        Assert.DoesNotContain("Versao", campos.Keys);

        // Corpo que não é objeto: 400 com mensagem
        (status, corpo) = Resultado(await orgao.RegistrarExterno(Json(new[] { 1 })));
        Assert.Equal(StatusCodes.Status400BadRequest, status);
        Assert.Equal(Codigo(ErrorCode.PeDadosInvalidos), CodigoDe(corpo));

        var arquivo = await EnviarArquivoAsync(UserOrgaoSes, "pdtic.pdf", Pdf());
        (status, corpo) = Resultado(await orgao.RegistrarExterno(Json(new
        {
            Versao = "1.0", VigenciaInicio = "2025-01-01", VigenciaFim = "2027-12-31", ArquivoId = arquivo.Id,
            AprovacaoInstancia = "cgtic", AprovacaoData = "2025-02-01", AprovacaoAtoTipo = "Resolução", AprovacaoAtoNumero = "2/2025",
            PublicacaoData = "2025-02-10", PublicacaoEndereco = Endereco
        })));
        Assert.Equal(StatusCodes.Status201Created, status);
        var pdtic = Assert.IsType<PePdticResponse>(corpo);
        Assert.True(pdtic.RegistradoExternamente);
        Assert.Equal(PeDominios.SituacaoPdtic.Publicado, pdtic.Situacao);
    }

    [Fact]
    public async Task Situacao_TrazOMotivoEOPodeEditar()
    {
        var pdtic = await AbrirSesAsync();
        var (_, corpo) = Resultado(await ControladorPdtic(UserOrgaoSes).Situacao(pdtic.Id));
        var situacao = Assert.IsType<PePdticSituacaoResponse>(corpo);
        var publicacao = situacao.Passos.Single(p => p.Chave == "planejamento.publicacao");
        Assert.Equal(("aguardando", "Disponível depois da aprovação do CGTIC", false), (publicacao.Situacao, publicacao.Motivo, publicacao.PodeEditar));
        Assert.True(situacao.Passos.Single(p => p.Chave == "preparacao.abrangencia").PodeEditar);
    }
}
