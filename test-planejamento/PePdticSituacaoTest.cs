using api.Planejamento;
using Models.Planejamento;
using service;
using service.Planejamento;
using Xunit;

namespace test.planejamento;

/// <summary>
/// Situação dos passos do PDTIC (E4): feito, pendente, atenção (comentário aberto), não se
/// aplica e contínuo (tipos das próximas entregas); o próximo passo recomendado; o "não se
/// aplica" (só passo opcional no nível ou nos ajustes, que aceite e não seja travado, com
/// justificativa, e desfazer); a conferência dos temas (incisos V, VI e IX); e os avisos do
/// guia (fraqueza sem necessidade, ameaça sem risco, equipe só de TIC).
/// </summary>
public class PePdticSituacaoTest : PePdticTestBase
{
    private static readonly object Abrangencia = new
    {
        tipo_abrangencia = "todo_com_vinculadas",
        vigencia_inicio = "2027-01-01",
        vigencia_fim = "2030-12-31",
        periodicidade_revisao = "anual"
    };

    [Fact]
    public async Task NovoPdtic_PendenteNosDados_AguardandoDepoisDoEnvio_EOProximoE11()
    {
        var pdtic = await AbrirSesAsync();
        var situacao = await SituacaoAsync(pdtic.Id);

        // Básico: 23 passos, na ordem e com a numeração da trilha
        Assert.Equal(23, situacao.Passos.Count);
        Assert.Equal("1.1", situacao.Passos[0].Numero);
        Assert.Equal("preparacao.abrangencia", situacao.Passos[0].Chave);
        Assert.Equal("1.1", situacao.ProximoPasso);

        string De(string chave) => situacao.Passos.Single(p => p.Chave == chave).Situacao;
        Assert.Equal(PeDominios.SituacaoPasso.Pendente, De("preparacao.abrangencia"));
        Assert.Equal(PeDominios.SituacaoPasso.Pendente, De("planejamento.acoes-tematicas"));
        // Seção opcional sem registro não pende (a das aquisições de IA; o resumo da priorização)
        Assert.Equal(PeDominios.SituacaoPasso.Feito, De("diagnostico.sistemas-ia"));
        Assert.Equal(PeDominios.SituacaoPasso.Feito, De("planejamento.priorizacao"));
        // Desde a E7: o documento sem PDF, o envio e a deliberação pendem; a publicação e as
        // etapas 4 a 7 aguardam (com o motivo)
        foreach (var chave in new[] { "planejamento.documento", "planejamento.aprovacao-sgtic", "planejamento.deliberacao-cgtic" })
            Assert.Equal(PeDominios.SituacaoPasso.Pendente, De(chave));
        foreach (var chave in new[] { "planejamento.publicacao", "monitoramento.ciclo-monitoramento", "fechamento.aprovacao-autoridade" })
            Assert.Equal(PeDominios.SituacaoPasso.Aguardando, De(chave));
        Assert.All(situacao.Passos, p =>
        {
            Assert.Null(p.NaoSeAplica);
            Assert.Equal(0, p.ComentariosAbertos);
            Assert.Empty(p.Avisos);
        });

        // Consulta e papéis globais leem; a equipe de outro órgão, não
        Assert.Equal(23, (await SituacaoAsync(pdtic.Id, UserConsultaSes)).Passos.Count);
        Assert.Equal(23, (await SituacaoAsync(pdtic.Id, UserPeCgtic)).Passos.Count);
        Assert.Equal(Codigo(ErrorCode.PeSemPermissao), await ErroAsync(() => SituacaoAsync(pdtic.Id, UserOrgaoSeec)));
    }

    [Fact]
    public async Task Feito_ComOsObrigatoriosPreenchidos_EPendenteQuandoOModeloPedeMais()
    {
        var pdtic = await AbrirSesAsync();
        await IncluirNoPdticAsync(pdtic.Id, "abrangencia", Abrangencia);

        var situacao = await SituacaoAsync(pdtic.Id);
        Assert.Equal(PeDominios.SituacaoPasso.Feito, situacao.Passos[0].Situacao);
        Assert.Equal("1.2", situacao.ProximoPasso);

        // O administrador torna obrigatória a descrição do escopo para o órgão: volta a pender
        await Orgaos.DefinirAjustesAsync(OrgaoSes.Id, new List<PeOrgaoAjusteDTO>
        {
            new() { AlvoTipo = "campo", AlvoId = Campo("abrangencia", "descricao_escopo").Id, Situacao = "obrigatorio" }
        }, EmailAdmin);
        Assert.Equal(PeDominios.SituacaoPasso.Pendente, (await PassoAsync(pdtic.Id, "preparacao.abrangencia")).Situacao);

        // Tabela obrigatória: pelo menos um registro (e todos completos)
        Assert.Equal(PeDominios.SituacaoPasso.Pendente, (await PassoAsync(pdtic.Id, "diagnostico.ativos")).Situacao);
        await IncluirNoPdticAsync(pdtic.Id, "ativos", new { nome = "Rede", tipo = "rede", situacao = "em_operacao" });
        Assert.Equal(PeDominios.SituacaoPasso.Feito, (await PassoAsync(pdtic.Id, "diagnostico.ativos")).Situacao);
    }

    [Fact]
    public async Task Atencao_ComComentarioAberto_EVoltaAoResolver()
    {
        var pdtic = await AbrirSesAsync();
        await IncluirNoPdticAsync(pdtic.Id, "abrangencia", Abrangencia);
        var passo = Passo("preparacao.abrangencia");

        var comentario = await Comentarios.CriarAsync(pdtic.Id, new PeComentarioCriarDTO
        {
            PassoId = passo.Id,
            Texto = "Informe as unidades alcançadas."
        }, await ContextoDe(UserPeSgdi));
        var situacao = await SituacaoAsync(pdtic.Id);
        Assert.Equal(PeDominios.SituacaoPasso.Atencao, situacao.Passos[0].Situacao);
        Assert.Equal(1, situacao.Passos[0].ComentariosAbertos);
        Assert.Equal("1.1", situacao.ProximoPasso);

        // A resposta não conta como comentário aberto; resolver tira a atenção
        await Comentarios.CriarAsync(pdtic.Id, new PeComentarioCriarDTO { PaiId = comentario.Id, Texto = "Feito." }, await Orgao());
        Assert.Equal(1, (await PassoAsync(pdtic.Id, "preparacao.abrangencia")).ComentariosAbertos);
        await Comentarios.ResolverAsync(comentario.Id, await Orgao());
        var depois = await PassoAsync(pdtic.Id, "preparacao.abrangencia");
        Assert.Equal(PeDominios.SituacaoPasso.Feito, depois.Situacao);
        Assert.Equal(0, depois.ComentariosAbertos);
    }

    [Fact]
    public async Task NaoSeAplica_SoEmPassoOpcionalQueAceite_ComJustificativa_TravadoNunca()
    {
        var pdtic = await AbrirSesAsync();
        var equipe = Passo("preparacao.equipe").Id;
        var dto = new PeNaoSeAplicaDTO { Justificativa = "O órgão usa a equipe do SGTIC." };

        // No Básico o passo é obrigatório
        Assert.Equal(Codigo(ErrorCode.PeNaoSeAplicaRecusado),
            await ErroAsync(async () => await Pdtics.MarcarNaoSeAplicaAsync(pdtic.Id, equipe, dto, await Orgao())));

        // Opcional para o órgão (ajuste): pede justificativa, marca e desfaz
        await AjustarPassosAsync(OrgaoSes, ("preparacao.equipe", "opcional"), ("diagnostico.ativos", "opcional"),
            ("preparacao.abrangencia", "opcional"));
        Assert.Equal(Codigo(ErrorCode.PeJustificativaObrigatoria), await ErroAsync(async () =>
            await Pdtics.MarcarNaoSeAplicaAsync(pdtic.Id, equipe, new PeNaoSeAplicaDTO { Justificativa = "  " }, await Orgao())));

        var marcado = await Pdtics.MarcarNaoSeAplicaAsync(pdtic.Id, equipe, dto, await Orgao());
        Assert.Equal(PeDominios.SituacaoPasso.NaoSeAplica, marcado.Situacao);
        Assert.Equal("O órgão usa a equipe do SGTIC.", marcado.NaoSeAplica!.Justificativa);
        Assert.Equal(UserOrgaoSes.Email, marcado.NaoSeAplica.MarcadoPor);
        Assert.Equal(PeDominios.SituacaoPasso.NaoSeAplica, (await PassoAsync(pdtic.Id, "preparacao.equipe")).Situacao);

        var desfeito = await Pdtics.DesmarcarNaoSeAplicaAsync(pdtic.Id, equipe, await Orgao());
        // F3: opcional para o órgão e sem conteúdo, o passo não é cobrado (antes, pendente)
        Assert.Equal(PeDominios.SituacaoPasso.Opcional, desfeito.Situacao);
        Assert.Null(desfeito.NaoSeAplica);
        var linha = Context.PePdticPassos.Single(p => p.PdticId == pdtic.Id && p.PassoId == equipe);
        Assert.False(linha.NaoSeAplica);
        Assert.Null(linha.Justificativa);

        // Travado nunca (nem opcional por ajuste); passo que não aceita, também não
        Assert.Equal(Codigo(ErrorCode.PeNaoSeAplicaRecusado), await ErroAsync(async () =>
            await Pdtics.MarcarNaoSeAplicaAsync(pdtic.Id, Passo("diagnostico.ativos").Id, dto, await Orgao())));
        Assert.Equal(Codigo(ErrorCode.PeNaoSeAplicaRecusado), await ErroAsync(async () =>
            await Pdtics.MarcarNaoSeAplicaAsync(pdtic.Id, Passo("preparacao.abrangencia").Id, dto, await Orgao())));

        // Passo fora da trilha do órgão: 404; consulta e globais não marcam
        Assert.Equal(Codigo(ErrorCode.PePassoIndisponivel), await ErroAsync(async () =>
            await Pdtics.MarcarNaoSeAplicaAsync(pdtic.Id, Passo("diagnostico.swot").Id, dto, await Orgao())));
        foreach (var user in new[] { UserConsultaSes, UserPeSgdi, UserPeAdmin })
            Assert.Equal(Codigo(ErrorCode.PeSemPermissao), await ErroAsync(async () =>
                await Pdtics.MarcarNaoSeAplicaAsync(pdtic.Id, equipe, dto, await ContextoDe(user))));

        // A marca deixa de valer quando o passo volta a ser obrigatório (e volta com o ajuste)
        await Pdtics.MarcarNaoSeAplicaAsync(pdtic.Id, equipe, dto, await Orgao());
        await Orgaos.DefinirAjustesAsync(OrgaoSes.Id, new List<PeOrgaoAjusteDTO>(), EmailAdmin);
        var obrigatorio = await PassoAsync(pdtic.Id, "preparacao.equipe");
        Assert.Equal(PeDominios.SituacaoPasso.Pendente, obrigatorio.Situacao);
        Assert.Null(obrigatorio.NaoSeAplica);
        await AjustarPassosAsync(OrgaoSes, ("preparacao.equipe", "opcional"));
        Assert.Equal(PeDominios.SituacaoPasso.NaoSeAplica, (await PassoAsync(pdtic.Id, "preparacao.equipe")).Situacao);
    }

    [Fact]
    public async Task NaoSeAplica_SoComOPdticAberto()
    {
        var pdtic = await AbrirSesAsync();
        await AjustarPassosAsync(OrgaoSes, ("preparacao.equipe", "opcional"));
        var p = Context.PePdtics.Single(x => x.Id == pdtic.Id);
        p.Situacao = PeDominios.SituacaoPdtic.EmAprovacao;
        Context.SaveChanges();
        Context.ChangeTracker.Clear();

        Assert.Equal(Codigo(ErrorCode.PePdticFechado), await ErroAsync(async () => await Pdtics.MarcarNaoSeAplicaAsync(pdtic.Id,
            Passo("preparacao.equipe").Id, new PeNaoSeAplicaDTO { Justificativa = "Não se aplica." }, await Orgao())));
    }

    [Fact]
    public async Task ConferenciaDeTemas_AcoesPorTema_EJustificativaDoTemaSemAcao()
    {
        var pdtic = await AbrirSesAsync();
        var acao = await IncluirNoPdticAsync(pdtic.Id, "acoes", new
        {
            descricao = "Implantar a cópia de segurança em nuvem",
            tema = new[] { "infraestrutura", "seguranca" },
            responsavel = "Gerência de infraestrutura",
            conclusao = "2027-12-31",
            situacao = "em_andamento"
        });

        var temas = (await Pdtics.TemasAsync(pdtic.Id, await ContextoDe(UserConsultaSes))).Temas;
        Assert.Equal(new[] { "V", "VI", "IX" }, temas.Select(t => t.Inciso));
        Assert.Equal(new[] { "seguranca", "transformacao_digital", "governanca_dados" }, temas.Select(t => t.Valor));
        Assert.Equal("Segurança da informação e continuidade de serviços (inciso V)", temas[0].Rotulo);
        var daSeguranca = Assert.Single(temas[0].Acoes);
        Assert.Equal(acao.Id, daSeguranca.RegistroId);
        Assert.Equal("A01", daSeguranca.Codigo);
        Assert.Equal("Implantar a cópia de segurança em nuvem", daSeguranca.Descricao);
        Assert.Equal("Em andamento", daSeguranca.Situacao);
        Assert.Empty(temas[1].Acoes);
        Assert.Null(temas[1].Justificativa);
        Assert.Equal(PeDominios.SituacaoPasso.Pendente, (await PassoAsync(pdtic.Id, "planejamento.acoes-tematicas")).Situacao);

        // Os dois temas sem ação ganham justificativa na seção do passo 3.4
        await IncluirNoPdticAsync(pdtic.Id, "temas_sem_acao", new
        {
            justificativa_transformacao = "Os serviços já são digitais; o foco do ciclo é a infraestrutura.",
            justificativa_dados = "A governança de dados fica com a Secretaria de Economia neste ciclo."
        });
        temas = (await Pdtics.TemasAsync(pdtic.Id, await Orgao())).Temas;
        Assert.Null(temas[0].Justificativa);
        Assert.StartsWith("Os serviços já são digitais", temas[1].Justificativa);
        Assert.StartsWith("A governança de dados", temas[2].Justificativa);
        Assert.Equal(PeDominios.SituacaoPasso.Feito, (await PassoAsync(pdtic.Id, "planejamento.acoes-tematicas")).Situacao);

        Assert.Equal(Codigo(ErrorCode.PeSemPermissao), await ErroAsync(async () => await Pdtics.TemasAsync(pdtic.Id, await ContextoDe(UserOrgaoSeec))));
    }

    [Fact]
    public async Task Avisos_FraquezaSemNecessidade_AmeacaSemRisco_EquipeSoDeTic()
    {
        await DefinirNivelDoOrgaoAsync(OrgaoSes, "intermediario");
        var pdtic = await AbrirSesAsync();
        var d1 = await IncluirNoPdticAsync(pdtic.Id, "swot_fraquezas", new { descricao = "Rede antiga" });
        await IncluirNoPdticAsync(pdtic.Id, "swot_fraquezas", new { descricao = "Poucos servidores de TIC" });
        var am1 = await IncluirNoPdticAsync(pdtic.Id, "swot_ameacas", new { descricao = "Corte no orçamento" });
        await IncluirNoPdticAsync(pdtic.Id, "equipe_elaboracao", new { nome = "Ana", papel = "coordenacao", area = "Subsecretaria de TIC", tipo_area = "tic" });
        await IncluirNoPdticAsync(pdtic.Id, "equipe_elaboracao", new { nome = "Beto", papel = "membro", area = "Gerência de redes", tipo_area = "tic" });

        var swot = await PassoAsync(pdtic.Id, "diagnostico.swot");
        Assert.Equal(2, swot.Avisos.Count);
        Assert.Equal("As fraquezas D01 e D02 ainda não têm necessidade de TIC ligada. O guia recomenda que cada fraqueza vire pelo menos "
                     + "uma necessidade: faça a ligação no passo 2.7.", swot.Avisos[0]);
        Assert.Equal("A ameaça AM01 ainda não tem risco ligado. O guia recomenda tratar cada ameaça no plano de riscos: faça a ligação no passo 3.8.",
            swot.Avisos[1]);
        // O aviso não bloqueia: as seções da SWOT estão feitas, só faltam as forças e as oportunidades
        Assert.Equal(PeDominios.SituacaoPasso.Pendente, swot.Situacao);
        var equipe = await PassoAsync(pdtic.Id, "preparacao.equipe");
        Assert.Equal("A equipe de elaboração tem só pessoas da área de TIC. O guia recomenda incluir também pessoas das áreas finalísticas do órgão.",
            Assert.Single(equipe.Avisos));
        Assert.Equal(PeDominios.SituacaoPasso.Feito, equipe.Situacao);

        // Ligar a fraqueza D01 a uma necessidade, a ameaça a um risco e pôr alguém da área finalística
        await IncluirNoPdticAsync(pdtic.Id, "necessidades", new
        {
            descricao = "Trocar a rede", tipo = "infraestrutura", origem = "swot", areas = "Todas", gravidade = 5, urgencia = 5, tendencia = 5, priorizada = true
        }, new { fraqueza = new[] { d1.Id } });
        await IncluirNoPdticAsync(pdtic.Id, "riscos", new
        {
            descricao = "Faltar dinheiro para a rede", probabilidade = "media", impacto = "alto", estrategia = "mitigar",
            acao_preventiva = "Buscar emenda", resposta = "Adiar a segunda fase", responsavel = "Ana"
        }, new { ameaca = new[] { am1.Id } });
        await IncluirNoPdticAsync(pdtic.Id, "equipe_elaboracao", new { nome = "Caio", papel = "membro", area = "Atenção primária", tipo_area = "finalistica" });

        swot = await PassoAsync(pdtic.Id, "diagnostico.swot");
        Assert.Equal("A fraqueza D02 ainda não tem necessidade de TIC ligada. O guia recomenda que cada fraqueza vire pelo menos uma necessidade: "
                     + "faça a ligação no passo 2.7.", Assert.Single(swot.Avisos));
        Assert.Empty((await PassoAsync(pdtic.Id, "preparacao.equipe")).Avisos);

        // No Básico não há SWOT nem o tipo da área: sem avisos
        await DefinirNivelDoOrgaoAsync(OrgaoSes, "basico");
        Assert.All((await SituacaoAsync(pdtic.Id)).Passos, p => Assert.Empty(p.Avisos));
    }

    [Fact]
    public async Task ProximoPasso_OPrimeiroPendenteOuEmAtencao_NaOrdemDaTrilha()
    {
        var pdtic = await AbrirSesAsync();
        await IncluirNoPdticAsync(pdtic.Id, "abrangencia", Abrangencia);
        await IncluirNoPdticAsync(pdtic.Id, "nomes", new
        {
            comite = "SGTIC", equipe_elaboracao = "Equipe do PDTIC", autoridade_cargo = "Secretário", autoridade_nome = "Fulano", unidade_tic = "SUTIC"
        });
        Assert.Equal("1.3", (await SituacaoAsync(pdtic.Id)).ProximoPasso);

        // Um comentário no passo 1.1 (feito) passa na frente
        await Comentarios.CriarAsync(pdtic.Id, new PeComentarioCriarDTO { PassoId = Passo("preparacao.abrangencia").Id, Texto = "Confira a vigência." },
            await ContextoDe(UserPeAdmin));
        Assert.Equal("1.1", (await SituacaoAsync(pdtic.Id)).ProximoPasso);
    }
}
