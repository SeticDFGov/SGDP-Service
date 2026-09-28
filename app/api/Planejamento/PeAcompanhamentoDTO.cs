namespace api.Planejamento;

// ── Ciclos (E7, rodada B) ─────────────────────────────────────────────────────

/// <summary>
/// Um ciclo do acompanhamento (GET pdtic/{id}/ciclos): o ciclo de monitoramento (criado sozinho
/// pela periodicidade) ou a avaliação intermediária (aberta pela equipe), com a situação exibida
/// (futuro, aberto, atrasado ou fechado), o resumo do que foi registrado nele e a última versão
/// do relatório de acompanhamento (RA) do ciclo.
/// </summary>
public class PeCicloResponse
{
    public long Id { get; set; }

    // monitoramento ou avaliacao
    public string Tipo { get; set; } = string.Empty;

    public int Numero { get; set; }

    // "2027 · 1º trimestre"; na avaliação, o que a equipe escreveu
    public string Rotulo { get; set; } = string.Empty;

    public DateOnly Inicio { get; set; }

    // Na avaliação, nulo enquanto aberta (o dia em que foi fechada)
    public DateOnly? Fim { get; set; }

    // O prazo para fechar (o fim mais os dias da configuração); nulo na avaliação
    public DateOnly? Prazo { get; set; }

    // futuro, aberto, atrasado ou fechado
    public string Situacao { get; set; } = string.Empty;

    public DateTime? FechadoEm { get; set; }

    public string? FechadoPor { get; set; }
    // F1 (C19): o nome da pessoa (o do cadastro do usuário; sem nome, o e-mail); nulo com o FechadoPor
    public string? FechadoPorNome { get; set; }

    public PeCicloResumoResponse Resumo { get; set; } = new();

    // A última versão do RA do ciclo, ou nulo
    public PeCicloRelatorioResponse? Relatorio { get; set; }

    // A mais que o contrato: a última reabertura
    public DateTime? ReabertoEm { get; set; }

    public string? ReabertoPor { get; set; }
    // F1 (C19): o nome da pessoa (o do cadastro do usuário; sem nome, o e-mail); nulo com o ReabertoPor
    public string? ReabertoPorNome { get; set; }

    // A mais que o contrato: quem chama grava dados neste ciclo agora (a equipe do órgão, o PDTIC
    // vigente e o ciclo começado e aberto)
    public bool PodeEditar { get; set; }
}

public class PeCicloResumoResponse
{
    // Ações do PDTIC com a situação registrada no ciclo
    public int AcoesComSituacao { get; set; }

    public int TotalAcoes { get; set; }

    public int Medicoes { get; set; }

    public int RiscosOcorridos { get; set; }

    // A mais que o contrato (F1, I04 e C23), só na avaliação intermediária (zero e nulos no
    // monitoramento): os resultados das metas registrados, se a análise foi registrada e a
    // decisão do comitê (o valor, seguir ou revisar, e o rótulo)
    public int ResultadosMetas { get; set; }

    public bool AnaliseRegistrada { get; set; }

    public string? DecisaoComite { get; set; }

    public string? DecisaoComiteRotulo { get; set; }
}

public class PeCicloRelatorioResponse
{
    public int Numero { get; set; }

    // minuta (os relatórios do acompanhamento só geram minutas)
    public string Situacao { get; set; } = string.Empty;

    public DateTime GeradoEm { get; set; }
}

/// <summary>POST pdtic/{id}/ciclos: { Tipo: "avaliacao", Rotulo } (o monitoramento é criado sozinho).</summary>
public class PeCicloCriarDTO
{
    public string? Tipo { get; set; }

    // Opcional: sem ele, "Avaliação intermediária N"
    public string? Rotulo { get; set; }
}

// ── Grade das ações (situação das ações no ciclo) ─────────────────────────────

/// <summary>
/// Uma ação do PDTIC na grade do ciclo (GET ciclos/{cicloId}/acoes): o que o plano diz (código,
/// descrição, metas, datas previstas), o que foi registrado no ciclo (o registro da seção
/// monitoramento_acoes, pelo motor) e o último registro de um ciclo anterior (Anterior).
/// </summary>
public class PeCicloAcaoResponse
{
    // O registro da ação (seção acoes)
    public long AcaoId { get; set; }

    public string? Codigo { get; set; }

    public string Descricao { get; set; } = string.Empty;

    // Os códigos das metas ligadas à ação ("M01"); vazio quando o nível não liga
    public List<string> Metas { get; set; } = new();

    public DateOnly? InicioPrevisto { get; set; }

    public DateOnly? ConclusaoPrevista { get; set; }

    // O registro da ação neste ciclo, ou nulo quando ainda não há
    public long? RegistroId { get; set; }

    // O valor da opção (nao_iniciada, em_andamento, concluida, cancelada), ou nulo
    public string? Situacao { get; set; }

    public decimal? ExecucaoFisica { get; set; }

    public decimal? ExecucaoOrcamentaria { get; set; }

    public string? Observacao { get; set; }

    // O último registro da ação num ciclo de monitoramento anterior, ou nulo
    public PeCicloAcaoAnteriorResponse? Anterior { get; set; }
}

public class PeCicloAcaoAnteriorResponse
{
    public string? Situacao { get; set; }

    public decimal? ExecucaoFisica { get; set; }

    public decimal? ExecucaoOrcamentaria { get; set; }

    // A mais que o contrato: o rótulo do ciclo de onde vêm os valores
    public string? Ciclo { get; set; }
}

/// <summary>PUT ciclos/{cicloId}/acoes: { Itens: [{ AcaoId, Situacao, ExecucaoFisica, ExecucaoOrcamentaria, Observacao }] }.</summary>
public class PeCicloAcoesDTO
{
    public List<PeCicloAcaoItemDTO>? Itens { get; set; }
}

public class PeCicloAcaoItemDTO
{
    public long? AcaoId { get; set; }

    public string? Situacao { get; set; }

    public decimal? ExecucaoFisica { get; set; }

    public decimal? ExecucaoOrcamentaria { get; set; }

    public string? Observacao { get; set; }
}

// ── Grade das medições ────────────────────────────────────────────────────────

/// <summary>
/// Um indicador de monitoramento (passo 4.3) na grade das medições do ciclo (GET
/// ciclos/{cicloId}/medicoes), com a medição registrada no ciclo e a do ciclo anterior.
/// </summary>
public class PeCicloMedicaoResponse
{
    // O registro do indicador (seção indicadores_monitoramento)
    public long IndicadorId { get; set; }

    public string? Codigo { get; set; }

    public string Indicador { get; set; } = string.Empty;

    // O texto do valor de referência de cada período, como o órgão escreveu
    public string? ValoresReferencia { get; set; }

    public long? RegistroId { get; set; }

    public decimal? ValorApurado { get; set; }

    public DateOnly? Data { get; set; }

    public string? Observacao { get; set; }

    public PeCicloMedicaoAnteriorResponse? Anterior { get; set; }
}

public class PeCicloMedicaoAnteriorResponse
{
    public decimal? ValorApurado { get; set; }

    public DateOnly? Data { get; set; }

    // A mais que o contrato: o rótulo do ciclo de onde vem a medição
    public string? Ciclo { get; set; }
}

/// <summary>PUT ciclos/{cicloId}/medicoes: { Itens: [{ IndicadorId, ValorApurado, Data, Observacao }] }.</summary>
public class PeCicloMedicoesDTO
{
    public List<PeCicloMedicaoItemDTO>? Itens { get; set; }
}

public class PeCicloMedicaoItemDTO
{
    public long? IndicadorId { get; set; }

    public decimal? ValorApurado { get; set; }

    // aaaa-mm-dd
    public string? Data { get; set; }

    public string? Observacao { get; set; }
}

// ── Painel do PDTIC (AC-PDTIC, Anexo XIII) ────────────────────────────────────

/// <summary>
/// GET pdtic/{id}/painel-acompanhamento?cicloId=: as metas com as ações ligadas, os percentuais
/// de execução e a situação física e orçamentária de cada ação no ciclo de referência (o dado ou
/// o último com dado), e os quadros dos riscos.
/// </summary>
public class PePainelResponse
{
    // O ciclo de referência, ou nulo quando nenhum ciclo tem dado
    public PeCicloReferenciaResponse? Ciclo { get; set; }

    public List<PePainelMetaResponse> Metas { get; set; } = new();

    // A mais que o contrato: as ações sem meta ligada (no nível Básico, a ligação com a meta não aparece)
    public List<PePainelAcaoResponse> AcoesSemMeta { get; set; } = new();

    public PePainelRiscosResponse Riscos { get; set; } = new();
}

public class PeCicloReferenciaResponse
{
    public long Id { get; set; }

    public string Rotulo { get; set; } = string.Empty;
}

public class PePainelMetaResponse
{
    public long MetaId { get; set; }

    public string? Codigo { get; set; }

    public string Descricao { get; set; } = string.Empty;

    public string? Indicador { get; set; }

    // O valor da meta como o órgão escreveu ("100%", "Sim")
    public string? Valor { get; set; }

    public DateOnly? Prazo { get; set; }

    // Os códigos das necessidades ligadas ("N01")
    public List<string> Necessidades { get; set; } = new();

    // A média das ações ligadas (ponderada pelo peso da ação na meta, do plano de execução, quando há); nulo sem dado
    public decimal? PercentualExecucao { get; set; }

    public List<PePainelAcaoResponse> Acoes { get; set; } = new();
}

public class PePainelAcaoResponse
{
    public long AcaoId { get; set; }

    public string? Codigo { get; set; }

    public string Descricao { get; set; } = string.Empty;

    // A execução física no último ciclo com dado da ação (a concluída sem o percentual conta 100)
    public decimal? PercentualExecucao { get; set; }

    // em_dia, atrasada, concluida, cancelada ou sem_registro
    public string SituacaoFisica { get; set; } = string.Empty;

    // O percentual da execução orçamentária, ou nulo (sem dado, ou quando não se aplica)
    public decimal? ExecucaoOrcamentaria { get; set; }

    // A ação não tem investimento nem custeio
    public bool OrcamentoNaoSeAplica { get; set; }
}

public class PePainelRiscosResponse
{
    // aberto, fechado, excluido (a última ocorrência de cada risco) e sem_ocorrencia
    public Dictionary<string, int> PorSituacao { get; set; } = new();

    // alto, medio e baixo (o nível do risco no plano)
    public Dictionary<string, int> PorNivel { get; set; } = new();

    // As doze combinações de situação e nível, na ordem
    public List<PePainelMatrizResponse> Matriz { get; set; } = new();
}

public class PePainelMatrizResponse
{
    public string Situacao { get; set; } = string.Empty;

    public string Nivel { get; set; } = string.Empty;

    public int Quantidade { get; set; }
}
