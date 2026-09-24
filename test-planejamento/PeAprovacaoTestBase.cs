using api.Planejamento;
using app.Models;
using Microsoft.EntityFrameworkCore;
using Models.Planejamento;
using service.Planejamento;
using Xunit;

namespace test.planejamento;

/// <summary>
/// Base dos testes da E7, rodada A (o caminho da aprovação do PDTIC): a base do documento
/// (E5), com atalhos para preencher o mínimo que o nível Básico pede nas etapas 1 a 3 (o que o
/// envio ao CGTIC exige), gerar a minuta, enviar, decidir pela Secretaria do CGTIC, publicar e
/// pôr o PDTIC numa situação à mão (para as regras que vêm depois da publicação).
/// </summary>
public abstract class PeAprovacaoTestBase : PeDocumentoTestBase
{
    protected const string Endereco = "https://www.saude.df.gov.br/pdtic";

    /// <summary>
    /// Preenche o mínimo do Básico nas etapas 1 a 3 (os passos obrigatórios antes do envio
    /// ficam feitos): abrangência, nomes, SGTIC, equipe, metodologia, estratégias, princípios,
    /// ambiente, ativos, necessidades, metas e ações (com os três temas do decreto) e contratações.
    /// Com minuta, gera o PDF (o passo do documento fica feito); com sgtic, registra a aprovação
    /// do SGTIC ("aprovado" e a data).
    /// </summary>
    protected async Task PreencherElaboracaoAsync(long id, bool minuta = true, bool sgtic = true)
    {
        await PreencherAbrangenciaAsync(id);
        await PreencherNomesAsync(id);
        await IncluirNoPdticAsync(id, "sgtic", new { forma = "subcomite", ato_tipo = "portaria", ato_numero = "12/2026", ato_data = "2026-01-10" });
        await IncluirNoPdticAsync(id, "sgtic_membros", new { nome = "Maria da Silva", papel = "presidente" });
        await IncluirNoPdticAsync(id, "equipe_elaboracao", new { nome = "Ana Souza" });
        await IncluirNoPdticAsync(id, "metodologia", new { metodologia_adotada = "guia_sisp" });
        await IncluirNoPdticAsync(id, "alinhamento_petic", new { });
        await IncluirNoPdticAsync(id, "principios_diretrizes", new { principio = "Priorizar soluções corporativas.", origem = "art4" });
        await IncluirNoPdticAsync(id, "diagnostico_ambiente", new Dictionary<string, object> { ["diagnostico"] = Rico("A TIC atende todas as unidades.") });
        await IncluirNoPdticAsync(id, "ativos", new { nome = "Prontuário Eletrônico", tipo = "sistema", situacao = "em_operacao" });
        var necessidade = await IncluirNoPdticAsync(id, "necessidades",
            new { descricao = "Substituir o sistema de regulação.", tipo = "servico", prioridade_simples = "alta" });
        var meta = await IncluirNoPdticAsync(id, "metas",
            new { descricao = "Implantar o novo sistema de regulação.", indicador = "Unidades com o sistema", valor = "100%", prazo = "2027-12-31" },
            new { necessidades = new[] { necessidade.Id } });
        await IncluirNoPdticAsync(id, "acoes", new
        {
            descricao = "Contratar e implantar a solução de regulação.",
            tema = new[] { "seguranca", "transformacao_digital", "governanca_dados" },
            responsavel = "Coordenação de Sistemas",
            conclusao = "2027-06-30",
            situacao = "nao_iniciada"
        });
        await IncluirNoPdticAsync(id, "contratacoes", new { objeto = "Solução de regulação em nuvem.", tipo = "solucao", valor_estimado = 1500000m, ano_previsto = 2026, no_pca = true });
        if (minuta) await Documentos.GerarPdfAsync(id, await Orgao());
        if (sgtic) await AprovacaoSgticAsync(id);
        Assert.NotNull(meta);
    }

    /// <summary>A aprovação do SGTIC, no passo do envio (grava ou regrava o formulário).</summary>
    protected async Task AprovacaoSgticAsync(long id, string decisao = "aprovado", string data = "2026-09-01")
    {
        var dados = new { decisao, data, instancia = "sgtic", ato_tipo = "ata", ato_numero = "3/2026", observacao = decisao == "devolvido" ? "Ajustar as metas." : null };
        var dono = PeDono.DoPdtic(id);
        var atual = (await Registros.ListarAsync(dono, "aprovacao_sgtic", await Orgao())).Registros.FirstOrDefault();
        if (atual == null) await IncluirNoPdticAsync(id, "aprovacao_sgtic", dados);
        else await Registros.AtualizarAsync(dono, "aprovacao_sgtic", atual.Id, Salvar(dados), await Orgao());
    }

    /// <summary>Um PDTIC da SES pronto para enviar (o mínimo do Básico, a minuta e a aprovação do SGTIC).</summary>
    protected async Task<PePdticResponse> ProntoParaEnviarAsync()
    {
        var pdtic = await AbrirSesAsync();
        await PreencherElaboracaoAsync(pdtic.Id);
        return pdtic;
    }

    protected async Task<PePdticResponse> EnviarAsync(long id, User? user = null) =>
        await Aprovacao.EnviarAsync(id, await ContextoDe(user ?? UserOrgaoSes));

    /// <summary>A deliberação aguardando do PDTIC.</summary>
    protected PeDeliberacao DeliberacaoAguardando(long pdticId) =>
        Context.PeDeliberacoes.AsNoTracking().Single(d => d.ObjetoTipo == "pdtic" && d.ObjetoId == pdticId && d.Situacao == "aguardando");

    protected async Task<PeDeliberacaoResponse> AprovarNoCgticAsync(long pdticId) =>
        await Deliberacoes.DecidirAsync(DeliberacaoAguardando(pdticId).Id, new PeDecidirDTO
        {
            Decisao = "aprovado",
            AtoTipo = "Resolução",
            AtoNumero = "12/2026",
            AtoData = "2026-09-10",
            Sei = "00040-00012345/2026-11"
        }, await Cgtic());

    protected async Task<PeDeliberacaoResponse> DevolverNoCgticAsync(long pdticId, string observacao = "Detalhe as metas de segurança.") =>
        await Deliberacoes.DecidirAsync(DeliberacaoAguardando(pdticId).Id,
            new PeDecidirDTO { Decisao = "devolvido", Observacao = observacao }, await Cgtic());

    /// <summary>A seção da publicação (data e endereço) e o "publicar".</summary>
    protected async Task<PePdticResponse> PublicarAsync(long id)
    {
        await IncluirNoPdticAsync(id, "publicacao", new { data = "2026-09-15", endereco = Endereco });
        return await Aprovacao.PublicarAsync(id, await Orgao());
    }

    /// <summary>O caminho inteiro até a publicação (Básico): preencher, enviar, aprovar e publicar.</summary>
    protected async Task<PePdticResponse> PublicadoAsync()
    {
        var pdtic = await ProntoParaEnviarAsync();
        await EnviarAsync(pdtic.Id);
        await AprovarNoCgticAsync(pdtic.Id);
        return await PublicarAsync(pdtic.Id);
    }

    /// <summary>Põe o PDTIC numa situação à mão, com as datas que a situação pede (o InMemory não confere os CHECKs).</summary>
    protected void Situacao(long pdticId, string situacao, DateOnly? fimDaVigencia = null)
    {
        var pdtic = Context.PePdtics.Single(p => p.Id == pdticId);
        var agora = DateTime.UtcNow;
        pdtic.Situacao = situacao;
        if (situacao == PeDominios.SituacaoPdtic.EmAprovacao) pdtic.EnviadoEm ??= agora;
        if (situacao is "aprovado" or "publicado" or "em_acompanhamento") pdtic.AprovadoEm ??= agora;
        if (situacao is "publicado" or "em_acompanhamento") pdtic.PublicadoEm ??= agora;
        if (situacao == PeDominios.SituacaoPdtic.Encerrado) pdtic.EncerradoEm ??= agora;
        if (fimDaVigencia != null) pdtic.VigenciaFim = fimDaVigencia;
        Context.SaveChanges();
        Context.ChangeTracker.Clear();
    }

    protected PePdtic PdticNoBanco(long id) => Context.PePdtics.AsNoTracking().Single(p => p.Id == id);

    protected async Task<PePassoSituacaoResponse> PassoDaSituacaoAsync(long pdticId, string chave, User? user = null) =>
        (await SituacaoAsync(pdticId, user)).Passos.Single(p => p.Chave == chave);

    /// <summary>O erro de uma validação com Campos (publicação, registro externo): o código e as mensagens por campo.</summary>
    protected static async Task<(int Codigo, IReadOnlyDictionary<string, string> Campos)> ValidacaoAsync(Func<Task> acao)
    {
        var ex = await Assert.ThrowsAsync<PeValidacaoException>(acao);
        return (ex.Error.Code, ex.Campos);
    }

    /// <summary>A data de hoje (Brasília) no formato dd/mm/aaaa.</summary>
    protected static string Hoje() => DateOnly.FromDateTime(demanda_service.Helpers.DateTimeHelper.TodayBrasilia()).ToString("dd/MM/yyyy");
}
