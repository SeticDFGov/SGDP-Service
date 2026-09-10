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

    // Módulo Análises de Contratações (faixa 8xx)
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
}
