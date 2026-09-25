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

    // Módulo Governança Estratégica (planejamento): faixa 1000 a 1159 (a 1000 a 1099 acabou na
    // E6; a E7 usa 1100 a 1139), respondida como { Code, Message } com 400, 403, 404 ou 409
    // (padrão do AcessoController)
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
    // Documento do PDTIC (E5): faixa 1070 a 1089
    PeDocCapituloNaoEncontrado = 1070,  // 404: capítulo inexistente, apagado ou fora do documento do órgão
    PeDocBlocoNaoEncontrado = 1071,     // 404: bloco inexistente, apagado ou fora do documento do órgão
    PeDocCapituloObrigatorio = 1072,    // 409: capítulo travado (art. 12, § 2º) ou obrigatório não se esconde
    PeDocBlocoNaoEditavel = 1073,       // 400: só o bloco de texto recebe o texto do órgão
    PeDocTextoInvalido = 1074,          // 400: texto rico fora da lista, grande demais ou com imagem que não serve
    PeDocConfigInvalida = 1075,         // 400: config do bloco não serve para o tipo
    PeDocVersaoNaoEncontrada = 1076,    // 404: versão gerada inexistente
    PeDocGeracaoFalhou = 1077,          // 409: o PDF não pôde ser montado (conteúdo que não cabe na página)
    // Fluxos (E6): faixa 1090 a 1099
    PeFluxoNaoEncontrado = 1090,        // 404: chave de fluxo que não existe
    PeFluxoInvalido = 1091,             // 400: a definição não passa na validação; o corpo traz Erros [texto]
    PeCronogramaPreenchido = 1092,      // 409: a seção do cronograma já tem linhas (a sugestão só entra nela vazia)
    PeCronogramaSemTarefas = 1093,      // 409: os fluxos da elaboração não têm tarefa para sugerir
    // Aprovação, publicação e acompanhamento (E7): faixa 1100 a 1139 (rodada A: 1100 a 1119; rodada B: 1120 a 1139)
    PePdticComPendencias = 1100,        // 400: envio ao CGTIC com pendências; o corpo traz Pendencias [{ PassoId, PassoNumero, PassoTitulo, Motivo }]
    PePdticSituacaoInvalida = 1101,     // 409: a transição não vale na situação do PDTIC (enviar fora da elaboração, publicar fora de aprovado, revisar fora da vigente)
    PePublicacaoIncompleta = 1102,      // 400: publicar sem a data ou o endereço da seção publicacao; o corpo traz Campos
    PeEncerramentoRecusado = 1103,      // 409: a equipe sem a aprovação da autoridade máxima (7.4) ou o administrador com a vigência ainda correndo
    PeRevisaoEmAndamento = 1104,        // 409: o órgão já tem uma versão em elaboração (a revisão espera ela terminar)
    PeRevisaoRecusada = 1105,           // 409: o passo 6.3 está ligado e a avaliação do comitê não decidiu "revisar"
    PeRegistroExternoInvalido = 1106,   // 400: dados do PDTIC aprovado fora do sistema; o corpo traz Campos (pelo nome do campo do corpo)
    PeVersaoPdticDuplicada = 1107,      // 409: o órgão já tem um PDTIC com esta versão
    // Acompanhamento (E7, rodada B): ciclos de monitoramento e de avaliação, grades, painel e relatórios RA e RR
    PeCicloNaoEncontrado = 1120,        // 404: ciclo inexistente ou de outro PDTIC
    PeCicloObrigatorio = 1121,          // 400: seção por ciclo sem o ?cicloId=
    PeCicloInvalido = 1122,             // 400: ciclo do tipo errado para a seção, a grade ou o pedido (criar monitoramento, avaliação num ciclo de monitoramento)
    PeCicloFechado = 1123,              // 409: o ciclo não aceita a operação agora (gravar num ciclo fechado ou que ainda não começou, fechar o fechado, reabrir o aberto)
    PeCicloComPendencias = 1124,        // 400: fechar o ciclo com pendências; o corpo traz Pendencias [{ PassoId, PassoNumero, PassoTitulo, Motivo }]
    PeAvaliacaoAberta = 1125,           // 409: já há uma avaliação intermediária aberta (uma por vez)
    // Painéis da SGDI, conformidade e inadimplência (E8): faixa 1140 a 1159
    PeInadimplenciaNaoEncontrada = 1140,    // 404: registro de inadimplência inexistente
    PeInadimplenciaInvalida = 1141,         // 400: notificar, justificar, registrar ou sanear sem os dados; o corpo traz Campos (pelo nome do campo do corpo)
    PeInadimplenciaSituacaoInvalida = 1142, // 409: a operação não vale na situação (justificar ou registrar fora de notificado, sanear o justificado ou o saneado)
    PeInadimplenciaPrazoAberto = 1143,      // 409: registrar a inadimplência antes de vencer o prazo de 5 dias úteis (art. 11, II)
    PePassoSemPlanilha = 1144,              // 404: a planilha do passo sem seção visível e marcada "na planilha" (documento, envio sem seção, deliberação)
}
