using api.Planejamento;
using app.Auth;
using app.Models;
using Controllers.Planejamento;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Models.Pgia;
using service.Planejamento;

namespace test.planejamento;

/// <summary>
/// Base dos testes da E4 (PDTIC dos órgãos): a base dos referenciais (modelo inicial versão
/// 2 carregado, SES sem nível escolhido, isto é, no Básico) com os serviços do PDTIC e dos
/// comentários, uma equipe de órgão na SEEC e atalhos para abrir o PDTIC, incluir
/// registros, ajustar passos, montar sistemas de IA do PGIA e textos ricos.
/// </summary>
public abstract class PePdticTestBase : PeReferenciaisTestBase
{
    protected readonly PePdticService Pdtics;
    protected readonly PeComentarioService Comentarios;
    // O documento (E5) e o caminho da aprovação (E7), que gera o PDF enviado ao CGTIC
    protected readonly PeDocumentoService Documentos;
    protected readonly PePdticAprovacaoService Aprovacao;
    // O acompanhamento (E7, rodada B): ciclos, grades e painel
    protected readonly PeAcompanhamentoService Acompanhamento;

    // pe_orgao na SEEC (o outro órgão da base)
    protected readonly User UserOrgaoSeec;

    protected PePdticTestBase()
    {
        Pdtics = new PePdticService(Context, Registros, Permissoes);
        Comentarios = new PeComentarioService(Context, Permissoes);
        Documentos = new PeDocumentoService(Context, Registros, Permissoes);
        Aprovacao = new PePdticAprovacaoService(Context, Registros, Permissoes, Pdtics, Documentos);
        Acompanhamento = new PeAcompanhamentoService(Context, Registros, Permissoes, Documentos);

        UserOrgaoSeec = NovoUser("olga@economia.df.gov.br", "Olga da Economia", Perfis.Basico, UnidadeSeec);
        DarPapel(UserOrgaoSeec, PapeisPlanejamento.Orgao);
    }

    protected Task<PeUserContext> Orgao() => ContextoDe(UserOrgaoSes);

    /// <summary>O PDTIC da SES, aberto pela equipe do órgão.</summary>
    protected async Task<PePdticResponse> AbrirSesAsync() => await Pdtics.AbrirAsync(new PePdticCriarDTO(), await Orgao());

    /// <summary>O PDTIC da SEEC, aberto pela equipe da SEEC.</summary>
    protected async Task<PePdticResponse> AbrirSeecAsync() => await Pdtics.AbrirAsync(new PePdticCriarDTO(), await ContextoDe(UserOrgaoSeec));

    /// <summary>Inclui um registro no PDTIC (na seção por ciclo, E7 rodada B, no ciclo dado).</summary>
    protected async Task<PeRegistroResponse> IncluirNoPdticAsync(long pdticId, string secao, object? dados = null, object? vinculos = null,
        User? user = null, long? cicloId = null) =>
        await Registros.CriarAsync(PeDono.DoPdtic(pdticId), secao, Salvar(dados, vinculos), await ContextoDe(user ?? UserOrgaoSes), cicloId);

    protected async Task<PePdticSituacaoResponse> SituacaoAsync(long pdticId, User? user = null) =>
        await Pdtics.SituacaoAsync(pdticId, await ContextoDe(user ?? UserOrgaoSes));

    protected async Task<PePassoSituacaoResponse> PassoAsync(long pdticId, string chave) =>
        (await SituacaoAsync(pdticId)).Passos.Single(p => p.Chave == chave);

    /// <summary>Ajustes de passo para o órgão (a lista inteira, como o PUT orgaos/{id}/ajustes).</summary>
    protected async Task AjustarPassosAsync(PgiaOrgao orgao, params (string Chave, string Situacao)[] passos) =>
        await Orgaos.DefinirAjustesAsync(orgao.Id,
            passos.Select(p => new PeOrgaoAjusteDTO { AlvoTipo = "passo", AlvoId = Passo(p.Chave).Id, Situacao = p.Situacao }).ToList(),
            EmailAdmin);

    protected PgiaSistemaIa SistemaPgia(PgiaOrgao orgao, string nome, string? classificacao = "Alto Risco",
        string situacao = "Implantado (em uso)", string? base_ = "art. 16, III")
    {
        var sistema = new PgiaSistemaIa
        {
            OrgaoId = orgao.Id,
            Denominacao = nome,
            Finalidade = "Finalidade de " + nome,
            OrigemRegistro = "Nova iniciativa",
            TipoSistema = "Desenvolvido internamente",
            Tecnologia = "Outra",
            StatusCicloVida = situacao,
            EscopoDados = "Somente dados públicos",
            ClassificacaoRiscoAtual = classificacao,
            EnquadramentoLegal = base_,
            SituacaoHomologacao = PgiaDominios.SituacaoHomologacao.AguardandoSgdi,
            CriadoEm = DateTime.UtcNow
        };
        Context.PgiaSistemasIa.Add(sistema);
        Context.SaveChanges();
        return sistema;
    }

    /// <summary>Um documento do TipTap com um parágrafo de texto.</summary>
    protected static object Rico(string texto) => new
    {
        type = "doc",
        content = new object[] { new { type = "paragraph", content = new object[] { new { type = "text", text = texto } } } }
    };

    /// <summary>Um documento do TipTap com os nós dados no topo.</summary>
    protected static object Doc(params object[] nos) => new { type = "doc", content = nos };

    protected PePdticController ControladorPdtic(User user) => new(Pdtics, Comentarios, Planilhas, Permissoes, Aprovacao)
    {
        ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = Principal(user) } }
    };
}
