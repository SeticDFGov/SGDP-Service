public enum ErrorCode
{
    ProjetoNaoEncontrado = 404,
    EtapaNaoEncontrada = 405,
    DemandaNaoEncontrada = 406,
    AreaExecutoraNaoEncontrada = 407,
    AreasDemandantesNaoEncontradas = 408,
    ErroAoCriarEtapa = 500,
    ErroAoEditarEtapa = 501,
    ErroAoDeletarEtapa = 502,
    ErroAoBuscarAreasDemandantes = 503,
    ErroAoCriarAreaDemandante = 504,

    // Módulo PGIA (faixa 6xx)
    PgiaOrgaoNaoEncontrado = 600,
    PgiaOrgaoJaExiste = 601,
    PgiaUsuarioNaoEncontrado = 602,
    PgiaPapelInvalido = 603,
    PgiaDominioInvalido = 604,
    PgiaDesignacaoNaoEncontrada = 605,
    PgiaAgenteDeOutroOrgao = 606,
    PgiaPrazoNaoEncontrado = 607,
    PgiaUnidadeNaoEncontrada = 608,
    PgiaOrgaoSemUnidadeVinculada = 609,
    PgiaDesignacaoVigenteImpedeTroca = 610,

    // Módulo PGIA — fase 1 (inventário e classificação de risco)
    PgiaSistemaNaoEncontrado = 620,
    PgiaSistemaJaExiste = 621,
    PgiaChecklistInvalido = 622,
    PgiaClassificacaoInvalida = 623,
    PgiaRiscoExcessivoBloqueado = 624,
    PgiaResponsavelNaoDesignado = 625,
    PgiaDocumentoNaoEncontrado = 626,
    PgiaArquivoInvalido = 627,

    // Módulo PGIA — fase 2 (governança central e homologação)
    PgiaAiaNaoEncontrada = 640,
    PgiaDeliberacaoNaoEncontrada = 641,
    PgiaPlataformaNaoEncontrada = 642,
    PgiaPlataformaJaExiste = 643,
    PgiaNormaNaoEncontrada = 644,
    PgiaAutorizacaoNaoEncontrada = 645,
    PgiaHomologacaoIndevida = 646,
    PgiaImplantacaoBloqueada = 647,

    // Módulo PGIA — fase 3 (operação contínua)
    PgiaIncidenteNaoEncontrado = 660,
    PgiaIncidenteNaoComunicado = 661,
    PgiaNaoConformidadeNaoEncontrada = 662,
    PgiaCapacitacaoNaoEncontrada = 663,
    PgiaCapacitacaoJaExiste = 664,
    PgiaRegistroUsoInvalido = 665,
    PgiaPlataformaNaoHomologada = 666,
    PgiaRevisaoHumanaObrigatoria = 667,
    PgiaSemOrgaoResolvido = 668,

    // Módulo PGIA — fase 4 (contratações, relatórios e auditorias)
    PgiaContratoNaoEncontrado = 680,
    PgiaContratoInvalido = 681,
    PgiaLegadoNaoEncontrado = 682,
    PgiaIndicadorNaoEncontrado = 683,
    PgiaPeriodoInvalido = 684,
    PgiaRelatorioNaoEncontrado = 685,
    PgiaRelatorioJaExiste = 686,
    PgiaAuditoriaNaoEncontrada = 687,
    PgiaAuditoriaNaoDesignada = 688,

    // Módulo PGIA — fase 5 (transparência pública)
    PgiaSolicitacaoNaoEncontrada = 700,
    PgiaSolicitacaoInvalida = 701,
    PgiaSistemaNaoPublicado = 702,

    // Módulo Supervisão Contínua das Contratações (faixa 8xx)
    CtrProcessoNaoEncontrado = 800,
    CtrProcessoInvalido = 801,
    CtrProcessoDuplicado = 802,
    CtrDatasIncoerentes = 803,
    CtrManifestacaoNaoEncontrada = 804,
    CtrManifestacaoInvalida = 805,
    CtrImportacaoInvalida = 806,
    CtrPapelInvalido = 807,
    CtrUsuarioNaoEncontrado = 808,
    CtrDominioInvalido = 809,
    // Classificação de riscos do processo (completude, riscos declarados, ciclo de vida)
    CtrClassificacaoRiscoInvalida = 810,

    // Gestão de acessos por módulo (isolamento entre módulos) — faixa 9xx
    AcessoUsuarioNaoEncontrado = 900,
    AcessoInvalido = 901,
    // Pedidos de acesso (card da tela inicial e fila de quem decide)
    PedidoAcessoNaoEncontrado = 902,
    PedidoAcessoInvalido = 903,
    PedidoAcessoJaDecidido = 904,
    PedidoAcessoForaDoEscopo = 905,

    // Módulo Governança Estratégica (planejamento): faixa 1000 a 1099, respondida como
    // { Code, Message } com 400, 403, 404 ou 409 (padrão do AcessoController)
    PeUsuarioNaoEncontrado = 1000,  // 404
    PePapelInvalido = 1001,         // 400
    PeSemPermissao = 1002,          // 403
    PeAutoRebaixamento = 1003,      // 409: pe_admin tirando ou trocando o próprio papel
    // Modelo configurável e níveis de maturidade (E2): faixa 1010 a 1029
    PeItemNaoEncontrado = 1010,         // 404: nível, etapa, passo, seção, campo ou opção
    PeOrgaoNaoEncontrado = 1011,        // 404: órgão inexistente ou inativo; papel de órgão sem órgão
    PeDadosInvalidos = 1012,            // 400: texto vazio ou longo demais, valor fora do domínio
    PeItemTravado = 1013,               // 409: item travado (art. 12, § 2º) não desliga nem desativa
    PeItemDoSistema = 1014,             // 409: item do sistema não muda de tipo nem de chave e não é apagado
    PeChaveDuplicada = 1015,            // 409: chave, código ou valor já usado
    PeConfigInvalida = 1016,            // 400: config do campo não serve para o tipo
    PeOrdemInvalida = 1017,             // 400: a lista de ids não é a dos itens do grupo
    PeNivelInativo = 1018,              // 409: nível desativado não pode ser escolhido
    PeUltimoNivelAtivo = 1019,          // 409: o último nível ativo não pode ser desativado
    PeJustificativaObrigatoria = 1020,  // 400: troca de nível sem justificativa
    PeOrgaoObrigatorio = 1021,          // 400: papel global pedindo a trilha sem dizer o órgão
    PeItemExcluido = 1022,              // 409: item apagado não é editado
    PeItemEmUso = 1023,                 // 409: item usado por um cálculo ou por uma ligação
    PeModeloIndisponivel = 1024,        // 409: modelo ainda não carregado (migration ou carregador pendente)
    PeConflitoGravacao = 1025,          // 409: outra pessoa gravou ao mesmo tempo
    // Referenciais, registros, deliberações e planilhas (E3): faixa 1030 a 1049
    PeRegistroNaoEncontrado = 1030,     // 404: registro inexistente (ou de outra seção ou dono)
    PeRegistroInvalido = 1031,          // 400: validação dos campos; o corpo traz Campos { chave: mensagem }
    PeRegistroDoSistema = 1032,         // 409: registro do sistema (princípios do art. 4º) não se edita nem se apaga
    PeRegistroLigado = 1033,            // 409: registro ligado por outro não é apagado (a mensagem diz quem liga)
    PeFormularioJaPreenchido = 1034,    // 409: POST num formulário que já tem registro (use PUT)
    PeSecaoIndisponivel = 1035,         // 404: seção inexistente, de outro escopo, apagada ou desligada
    PePeticNaoEncontrado = 1036,        // 404: versão do PETIC-DF inexistente
    PeVersaoFechada = 1037,             // 409: versão fora do rascunho não é editada
    PeVersaoEmAndamento = 1038,         // 409: já há versão em rascunho ou em deliberação
    PePeticIncompleto = 1039,           // 400: envio ao CGTIC com pendências (a mensagem lista)
    PeDeliberacaoNaoEncontrada = 1040,  // 404
    PeDeliberacaoJaDecidida = 1041,     // 409: deliberação decidida não muda
    PeDecisaoInvalida = 1042,           // 400: aprovado sem ato e data, devolvido sem observação, decisão fora do domínio
    PeArquivoInvalido = 1043,           // 400: arquivo vazio, grande demais, extensão fora da lista ou conteúdo que não bate
    PeArquivoNaoEncontrado = 1044,      // 404
    PeCatalogoNaoEncontrado = 1045,     // 404: catálogo fora da lista
    PePlanilhaInvalida = 1046,          // 400: formato fora de csv e xlsx (a completa só sai em xlsx)
    PeVersaoJaEnviada = 1047,           // 409: versão que já foi ao CGTIC não é apagada
    // PDTIC dos órgãos, situação dos passos, comentários e planilhas (E4): faixa 1050 a 1069
    PePdticNaoEncontrado = 1050,        // 404: PDTIC inexistente
    PePdticJaExiste = 1051,             // 409: o órgão já tem um PDTIC atual (nem encerrado nem substituído)
    PePdticFechado = 1052,              // 409: PDTIC fora de em_elaboracao e devolvido não é editado
    PePassoIndisponivel = 1053,         // 404: passo que não está na trilha do órgão (apagado ou desligado)
    PeNaoSeAplicaRecusado = 1054,       // 409: passo obrigatório, travado ou que não aceita "não se aplica"
    PeComentarioNaoEncontrado = 1055,   // 404
    PeComentarioInvalido = 1056,        // 400: texto vazio ou longo, resposta a uma resposta, passo diferente do comentário
    PeComentarioResolvido = 1057,       // 409: comentário resolvido não recebe resposta
}
