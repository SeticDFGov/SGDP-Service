namespace api.Planejamento;

// ── Painel da SGDI (E8) ──────────────────────────────────────────────────────

/// <summary>
/// GET painel?NivelId=&amp;Situacao=: os órgãos pela situação do PDTIC de referência (a vigente;
/// sem ela, a da elaboração; sem as duas, a mais recente) e, em elaboração, pela etapa do próximo
/// passo; os órgãos por nível; as necessidades e as metas por objetivo do PETIC-DF vigente; as
/// ações por tema; os riscos por nível; a execução das ações no último ciclo com dado; os alertas;
/// e as opções dos filtros. Os filtros valem para tudo (fora do domínio: nenhum órgão).
/// </summary>
public class PePainelGeralResponse
{
    public DateTime GeradoEm { get; set; }

    public int TotalOrgaos { get; set; }

    public List<PePainelSituacaoResponse> PorSituacao { get; set; } = new();

    public List<PePainelEtapaResponse> EmElaboracaoPorEtapa { get; set; } = new();

    public List<PePainelNivelResponse> PorNivel { get; set; } = new();

    public List<PePainelObjetivoResponse> PorObjetivoPetic { get; set; } = new();

    public List<PePainelValorResponse> AcoesPorTema { get; set; } = new();

    public List<PePainelRiscoResponse> RiscosPorNivel { get; set; } = new();

    public List<PePainelExecucaoResponse> ExecucaoUltimoCiclo { get; set; } = new();

    public PePainelAlertasResponse Alertas { get; set; } = new();

    public PePainelFiltrosResponse Filtros { get; set; } = new();
}

public class PePainelSituacaoResponse
{
    // sem_pdtic, em_elaboracao, em_aprovacao, devolvido, aprovado, publicado, em_acompanhamento ou encerrado
    public string Chave { get; set; } = string.Empty;

    public string Rotulo { get; set; } = string.Empty;

    public int Quantidade { get; set; }
}

public class PePainelEtapaResponse
{
    // A posição da etapa no modelo (1 a 7 no modelo inicial, a numeração do Avançado)
    public int Etapa { get; set; }

    public string Titulo { get; set; } = string.Empty;

    public int Quantidade { get; set; }
}

public class PePainelNivelResponse
{
    public long NivelId { get; set; }

    public string Nome { get; set; } = string.Empty;

    public int Quantidade { get; set; }
}

public class PePainelObjetivoResponse
{
    // O registro do objetivo no PETIC-DF vigente
    public long RegistroId { get; set; }

    public string? Codigo { get; set; }

    public string Texto { get; set; } = string.Empty;

    public int Necessidades { get; set; }

    public int Metas { get; set; }
}

public class PePainelValorResponse
{
    // O valor da opção (seguranca, transformacao_digital, governanca_dados...)
    public string Valor { get; set; } = string.Empty;

    public string Rotulo { get; set; } = string.Empty;

    public int Quantidade { get; set; }
}

public class PePainelRiscoResponse
{
    // alto, medio, baixo ou sem_nivel
    public string Nivel { get; set; } = string.Empty;

    public string Rotulo { get; set; } = string.Empty;

    public int Quantidade { get; set; }
}

public class PePainelExecucaoResponse
{
    // nao_iniciada, em_andamento, concluida, cancelada ou sem_registro
    public string Situacao { get; set; } = string.Empty;

    public string Rotulo { get; set; } = string.Empty;

    public int Quantidade { get; set; }
}

/// <summary>
/// Os alertas do painel, em número de órgãos (as deliberações, em número de deliberações). Os três
/// primeiros contam as linhas da conformidade com a marca de mesmo nome (Alertas da linha; F2), com
/// os mesmos filtros: GET conformidade?Alerta=vigencia, revisao ou ciclo devolve exatamente esses órgãos.
/// </summary>
public class PePainelAlertasResponse
{
    // O PDTIC vigente passou do fim da vigência e não foi encerrado
    public int VigenciaVencida { get; set; }

    // A última aprovação passou da periodicidade de revisão
    public int RevisaoVencida { get; set; }

    // Algum ciclo de monitoramento passou do prazo de fechamento
    public int CicloAtrasado { get; set; }

    // Órgãos com inadimplência registrada (situação inadimplente)
    public int Inadimplentes { get; set; }

    // Deliberações aguardando a decisão do CGTIC. Sem filtro de nível nem de situação, todas as da
    // fila (a fila da Secretaria: os PDTICs de qualquer órgão e o PETIC-DF; F2); com filtro, só as
    // dos PDTICs dos órgãos filtrados
    public int DeliberacoesAguardando { get; set; }
}

public class PePainelFiltrosResponse
{
    public List<PePainelFiltroNivelResponse> Niveis { get; set; } = new();

    public List<PePainelFiltroSituacaoResponse> Situacoes { get; set; } = new();
}

public class PePainelFiltroNivelResponse
{
    public long Id { get; set; }

    public string Nome { get; set; } = string.Empty;
}

public class PePainelFiltroSituacaoResponse
{
    public string Chave { get; set; } = string.Empty;

    public string Rotulo { get; set; } = string.Empty;
}

/// <summary>GET painel: os filtros (os dois opcionais).</summary>
public class PePainelConsulta
{
    public long? NivelId { get; set; }

    public string? Situacao { get; set; }
}

// ── Conformidade (E8) ────────────────────────────────────────────────────────

/// <summary>
/// GET conformidade?Grupo=&amp;NivelId=&amp;Filtro=&amp;Situacao=&amp;Alerta=: os itens (só de TIC), o resumo
/// dos grupos (com os outros filtros, sem o grupo) e uma linha por órgão, pela sigla.
/// </summary>
public class PeConformidadeResponse
{
    public List<PeConformidadeItemResponse> Itens { get; set; } = new();

    public PeConformidadeResumoResponse Resumo { get; set; } = new();

    public List<PeConformidadeOrgaoResponse> Orgaos { get; set; } = new();
}

public class PeConformidadeItemResponse
{
    public string Chave { get; set; } = string.Empty;

    public string Rotulo { get; set; } = string.Empty;

    // A base legal ("art. 5º do Decreto nº 48.900/2026")
    public string Base { get; set; } = string.Empty;

    // A mais que o contrato: quando o item é atendido, em texto
    public string AtendeQuando { get; set; } = string.Empty;
}

public class PeConformidadeResumoResponse
{
    public int Alta { get; set; }

    public int Media { get; set; }

    public int Baixa { get; set; }
}

/// <summary>
/// A linha de um órgão: o PDTIC de referência (a versão e a situação, as mesmas do painel), cada
/// item pela chave (Atende nulo = não se aplica e sai da conta; os itens avaliam só a versão
/// vigente ou a da elaboração), o percentual e o grupo, as marcas dos alertas do painel e a
/// inadimplência vigente (notificada ou inadimplente).
/// </summary>
public class PeConformidadeOrgaoResponse
{
    public long OrgaoId { get; set; }

    public string Sigla { get; set; } = string.Empty;

    public string Nome { get; set; } = string.Empty;

    public string? NivelNome { get; set; }

    // O PDTIC de referência do órgão (F2, a regra do painel): a versão vigente; sem ela, a da
    // elaboração; sem as duas, a mais recente (encerrada). Nulo só quando o órgão nunca abriu um PDTIC
    public long? PdticId { get; set; }

    public string? PdticVersao { get; set; }

    // A chave do painel (PorSituacao): a situação do PDTIC de referência, com a substituída como
    // "encerrado"; nulo = sem PDTIC ("sem_pdtic" no painel)
    public string? PdticSituacao { get; set; }

    public Dictionary<string, PeConformidadeAtendeResponse> Itens { get; set; } = new();

    // F2: as marcas dos alertas do painel; o painel conta exatamente as linhas com cada marca
    public PeConformidadeAlertasResponse Alertas { get; set; } = new();

    public int Atendidos { get; set; }

    public int Aplicaveis { get; set; }

    // 0 a 100, arredondado
    public int Percentual { get; set; }

    // alta, media ou baixa
    public string Grupo { get; set; } = string.Empty;

    public PeConformidadeInadimplenciaResponse? Inadimplencia { get; set; }
}

/// <summary>
/// As marcas dos alertas do painel numa linha da conformidade (F2). O painel conta exatamente as
/// linhas marcadas (Alertas.VigenciaVencida, RevisaoVencida e CicloAtrasado do painel), e
/// GET conformidade?Alerta= filtra por elas: é a mesma regra nos dois lados.
/// </summary>
public class PeConformidadeAlertasResponse
{
    // O PDTIC está publicado (ou em acompanhamento) e a vigência já terminou (?Alerta=vigencia)
    public bool VigenciaVencida { get; set; }

    // A última aprovação do CGTIC (no PDTIC registrado fora do sistema, a publicação) mais a
    // periodicidade de revisão já passou (?Alerta=revisao). Sem aprovação ainda, não há revisão vencida
    public bool RevisaoVencida { get; set; }

    // Um ciclo de monitoramento começado passou do prazo de fechamento sem ser fechado (?Alerta=ciclo)
    public bool CicloAtrasado { get; set; }
}

public class PeConformidadeAtendeResponse
{
    // Verdadeiro, falso ou nulo (não se aplica)
    public bool? Atende { get; set; }

    // Em texto pronto: "Aprovado em 12/03/2027 (Resolução nº 3/2027)"
    public string Detalhe { get; set; } = string.Empty;
}

public class PeConformidadeInadimplenciaResponse
{
    public long Id { get; set; }

    // notificado ou inadimplente
    public string Situacao { get; set; } = string.Empty;

    public DateOnly Prazo { get; set; }
}

/// <summary>
/// GET conformidade e GET conformidade/planilha: os filtros da tela, todos opcionais, aplicados do
/// mesmo jeito nas duas rotas (a planilha sai com a lista que a tela mostra). Valor fora do
/// domínio: lista vazia, nunca "todos" em silêncio.
/// </summary>
public class PeConformidadeConsulta
{
    // alta, media ou baixa (o resumo dos grupos não sofre este filtro)
    public string? Grupo { get; set; }

    public long? NivelId { get; set; }

    // Sigla ou nome do órgão, sem diferenciar maiúsculas nem acentos ("saude" acha "Saúde"), como a tela
    public string? Filtro { get; set; }

    // F2: as chaves do painel separadas por vírgula (sem_pdtic, em_elaboracao, em_aprovacao,
    // devolvido, aprovado, publicado, em_acompanhamento, encerrado), a situação da linha (PdticSituacao)
    public string? Situacao { get; set; }

    // F2: vigencia, revisao ou ciclo (as marcas de Alertas da linha, as mesmas que o painel conta) ou
    // inadimplencia (notificação ou inadimplência em aberto: a linha tem Inadimplencia)
    public string? Alerta { get; set; }
}

// ── Inadimplência (E8; art. 7º, § 3º, e art. 11 do Decreto nº 48.899/2026) ────

/// <summary>
/// Um registro de inadimplência: a notificação, o prazo de 5 dias úteis (e quantos faltam), a
/// situação (notificado, justificado, inadimplente, saneado), a justificativa aceita, o registro
/// da inadimplência (motivo, nota de motivação, comunicação ao controle interno) e o saneamento.
/// </summary>
public class PeInadimplenciaResponse
{
    public long Id { get; set; }

    public long OrgaoId { get; set; }

    public string OrgaoSigla { get; set; } = string.Empty;

    public string OrgaoNome { get; set; } = string.Empty;

    public string Obrigacao { get; set; } = string.Empty;

    public string PrazoDescumprido { get; set; } = string.Empty;

    public DateOnly NotificadoEm { get; set; }

    public string Documento { get; set; } = string.Empty;

    public string? Sei { get; set; }

    // O último dia para regularizar ou justificar
    public DateOnly Prazo { get; set; }

    // Dias úteis depois de hoje até o prazo, inclusive (só na notificada; nas outras, 0)
    public int DiasUteisRestantes { get; set; }

    // Hoje já passou do prazo (a inadimplência pode ser registrada a partir do dia seguinte ao prazo)
    public bool Vencido { get; set; }

    public string Situacao { get; set; } = string.Empty;

    public string SituacaoRotulo { get; set; } = string.Empty;

    public string? Justificativa { get; set; }

    public string? Motivo { get; set; }

    public string? MotivoRotulo { get; set; }

    public string? NotaMotivacao { get; set; }

    public DateOnly? ComunicadoControleEm { get; set; }

    public DateTime? RegistradoEm { get; set; }

    public string? RegistradoPor { get; set; }
    // F1 (C19): o nome da pessoa (o do cadastro do usuário; sem nome, o e-mail); nulo com o RegistradoPor
    public string? RegistradoPorNome { get; set; }

    public DateOnly? SaneadoEm { get; set; }

    public string? Observacao { get; set; }

    public DateTime CriadoEm { get; set; }

    public string CriadoPor { get; set; } = string.Empty;
    // F1 (C19): o nome da pessoa (o do cadastro do usuário; sem nome, o e-mail)
    public string CriadoPorNome { get; set; } = string.Empty;
}

/// <summary>GET inadimplencias: filtros e página (vigentes primeiro).</summary>
public class PeInadimplenciasConsulta
{
    public string? Situacao { get; set; }

    public long? OrgaoId { get; set; }

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 20;
}

/// <summary>POST orgaos/{id}/inadimplencias: a notificação (datas em aaaa-mm-dd).</summary>
public class PeInadimplenciaNotificarDTO
{
    public string? Obrigacao { get; set; }

    public string? PrazoDescumprido { get; set; }

    public string? NotificadoEm { get; set; }

    public string? Documento { get; set; }

    public string? Sei { get; set; }
}

/// <summary>POST inadimplencias/{id}/justificar: a justificativa aceita.</summary>
public class PeInadimplenciaJustificarDTO
{
    public string? Justificativa { get; set; }
}

/// <summary>POST inadimplencias/{id}/registrar: o motivo, a nota de motivação e a comunicação ao controle interno (aaaa-mm-dd ou nulo).</summary>
public class PeInadimplenciaRegistrarDTO
{
    public string? Motivo { get; set; }

    public string? NotaMotivacao { get; set; }

    public string? ComunicadoControleEm { get; set; }
}

/// <summary>POST inadimplencias/{id}/sanear: a data do saneamento (aaaa-mm-dd) e a observação.</summary>
public class PeInadimplenciaSanearDTO
{
    public string? SaneadoEm { get; set; }

    public string? Observacao { get; set; }
}

// ── Árvore do PETIC-DF (E8) ──────────────────────────────────────────────────

/// <summary>
/// GET petic/arvore?orgaoId=: os objetivos do PETIC-DF vigente, cada um com os indicadores do
/// próprio PETIC-DF e, por órgão, as necessidades e as metas do PDTIC ligadas a ele. Sem vigente,
/// Petic nulo e nenhum objetivo.
/// </summary>
public class PeArvorePeticResponse
{
    public PeArvorePeticVersaoResponse? Petic { get; set; }

    public List<PeArvoreObjetivoResponse> Objetivos { get; set; } = new();
}

public class PeArvorePeticVersaoResponse
{
    public long Id { get; set; }

    public string Versao { get; set; } = string.Empty;

    public string Titulo { get; set; } = string.Empty;
}

public class PeArvoreObjetivoResponse
{
    public long RegistroId { get; set; }

    public string? Codigo { get; set; }

    public string Texto { get; set; } = string.Empty;

    public List<PeArvoreIndicadorResponse> Indicadores { get; set; } = new();

    public int TotalNecessidades { get; set; }

    public int TotalMetas { get; set; }

    // Só os órgãos com alguma necessidade ou meta ligada ao objetivo, pela sigla
    public List<PeArvoreOrgaoResponse> Orgaos { get; set; } = new();
}

public class PeArvoreIndicadorResponse
{
    public string? Codigo { get; set; }

    public string Nome { get; set; } = string.Empty;

    // A meta em texto pronto ("80 % até 31/12/2027"), ou nulo
    public string? Meta { get; set; }
}

public class PeArvoreOrgaoResponse
{
    public long OrgaoId { get; set; }

    public string Sigla { get; set; } = string.Empty;

    public long PdticId { get; set; }

    public List<PeArvoreNecessidadeResponse> Necessidades { get; set; } = new();

    public List<PeArvoreMetaResponse> Metas { get; set; } = new();
}

public class PeArvoreNecessidadeResponse
{
    public string? Codigo { get; set; }

    public string Descricao { get; set; } = string.Empty;

    // Nulo quando o nível do órgão não pergunta (Básico) ou não foi respondido
    public bool? Priorizada { get; set; }
}

public class PeArvoreMetaResponse
{
    public string? Codigo { get; set; }

    public string Descricao { get; set; } = string.Empty;

    public string? Indicador { get; set; }

    public string? Valor { get; set; }

    public DateOnly? Prazo { get; set; }

    // O rótulo da situação da meta ("Em andamento"), ou nulo
    public string? Situacao { get; set; }
}

// ── Página do órgão (E8) ─────────────────────────────────────────────────────

/// <summary>
/// GET orgaos/{id}/resumo: tudo do órgão numa resposta. O Pdtic é o de referência (a versão
/// vigente; sem ela, a da elaboração; sem as duas, a mais recente, encerrada); o andamento, o
/// próximo passo, o "não se aplica", os comentários abertos e as aprovações são dele; a
/// conformidade é a linha do órgão; os ciclos são os do PDTIC vigente (a lista da E7); as
/// deliberações, os documentos e as inadimplências são de todas as versões do órgão.
/// </summary>
public class PeOrgaoResumoResponse
{
    public PeOrgaoResumoOrgaoResponse Orgao { get; set; } = new();

    public PeOrgaoResumoNivelResponse Nivel { get; set; } = new();

    public PePdticResponse? Pdtic { get; set; }

    public List<PePdticResponse> Versoes { get; set; } = new();

    public List<PeOrgaoAndamentoResponse> Andamento { get; set; } = new();

    public string? ProximoPasso { get; set; }

    public PeConformidadeOrgaoResponse Conformidade { get; set; } = new();

    public List<PeOrgaoNaoSeAplicaResponse> NaoSeAplica { get; set; } = new();

    public List<PeOrgaoComentarioAbertoResponse> ComentariosAbertos { get; set; } = new();

    public List<PeOrgaoAprovacaoResponse> Aprovacoes { get; set; } = new();

    public List<PeDeliberacaoResponse> Deliberacoes { get; set; } = new();

    public List<PeOrgaoDocumentoResponse> Documentos { get; set; } = new();

    public List<PeOrgaoCicloResponse> Ciclos { get; set; } = new();

    public List<PeInadimplenciaResponse> Inadimplencias { get; set; } = new();
}

public class PeOrgaoResumoOrgaoResponse
{
    public long Id { get; set; }

    public string Sigla { get; set; } = string.Empty;

    public string Nome { get; set; } = string.Empty;
}

public class PeOrgaoResumoNivelResponse
{
    // O nível de hoje (o escolhido ou o padrão); nulo sem modelo
    public long? Id { get; set; }

    public string? Nome { get; set; }

    // A mais que o contrato: o órgão não escolheu nível (vale o padrão)
    public bool Padrao { get; set; }

    // As trocas de nível, da mais nova para a mais antiga
    public List<PeOrgaoNivelTrocaResponse> Historico { get; set; } = new();
}

public class PeOrgaoNivelTrocaResponse
{
    public string? NivelNome { get; set; }

    // F1 (A21): o nível desta troca é o padrão (o órgão voltou ao padrão) e o de antes dela
    public bool Padrao { get; set; }

    public string? NivelAnterior { get; set; }

    public bool NivelAnteriorPadrao { get; set; }

    public string? Justificativa { get; set; }

    public DateTime AlteradoEm { get; set; }

    public string AlteradoPor { get; set; } = string.Empty;
    // F1 (C19): o nome da pessoa (o do cadastro do usuário; sem nome, o e-mail)
    public string AlteradoPorNome { get; set; } = string.Empty;
}

public class PeOrgaoAndamentoResponse
{
    // O número da etapa na trilha do órgão
    public int Etapa { get; set; }

    public string Titulo { get; set; } = string.Empty;

    // Feitos, feitos fora do sistema e "não se aplica"
    public int Feitos { get; set; }

    public int Total { get; set; }

    public int Atrasados { get; set; }

    public int Aguardando { get; set; }
}

public class PeOrgaoNaoSeAplicaResponse
{
    public string PassoNumero { get; set; } = string.Empty;

    public string PassoTitulo { get; set; } = string.Empty;

    public string Justificativa { get; set; } = string.Empty;

    public DateTime MarcadoEm { get; set; }

    public string MarcadoPor { get; set; } = string.Empty;
    // F1 (C19): o nome da pessoa (o do cadastro do usuário; sem nome, o e-mail)
    public string MarcadoPorNome { get; set; } = string.Empty;
}

public class PeOrgaoComentarioAbertoResponse
{
    public long Id { get; set; }

    public long PassoId { get; set; }

    // Nulo quando o passo saiu da trilha do órgão
    public string? PassoNumero { get; set; }

    public string Texto { get; set; } = string.Empty;

    public string AutorNome { get; set; } = string.Empty;

    public DateTime CriadoEm { get; set; }
}

/// <summary>Uma decisão registrada num passo de aprovação (ou na aprovação do SGTIC, no passo do envio).</summary>
public class PeOrgaoAprovacaoResponse
{
    public string PassoNumero { get; set; } = string.Empty;

    // O título da seção; na seção por ciclo, com o rótulo do ciclo ("Avaliação do comitê · Avaliação intermediária 1")
    public string Rotulo { get; set; } = string.Empty;

    // O rótulo da decisão, como o modelo diz ("Aprovado", "Devolvido", "Revisar o PDTIC")
    public string Decisao { get; set; } = string.Empty;

    // A mais que o contrato: o valor da decisão (aprovado, devolvido, seguir, revisar)
    public string DecisaoValor { get; set; } = string.Empty;

    public DateOnly? Data { get; set; }

    // "Ata nº 3/2026"
    public string? Ato { get; set; }

    public string? Sei { get; set; }
}

public class PeOrgaoDocumentoResponse
{
    // pdtic, ra ou rr
    public string Tipo { get; set; } = string.Empty;

    // "PDTIC 1.0", "Relatório de acompanhamento · 2027 · 1º trimestre (PDTIC 1.0)"
    public string Rotulo { get; set; } = string.Empty;

    public int Numero { get; set; }

    // minuta, enviada, aprovada ou publicada
    public string Situacao { get; set; } = string.Empty;

    public DateTime GeradoEm { get; set; }

    public int Paginas { get; set; }

    // O caminho do download ("api/planejamento/pdtic/12/documento/versoes/3/arquivo")
    public string Url { get; set; } = string.Empty;

    // A mais que o contrato: o PDTIC e, no RA, o ciclo
    public long PdticId { get; set; }

    public long? CicloId { get; set; }
}

/// <summary>Um ciclo do acompanhamento na página do órgão: a forma da E7 (GET pdtic/{id}/ciclos), sem o resumo.</summary>
public class PeOrgaoCicloResponse
{
    public long Id { get; set; }

    public string Tipo { get; set; } = string.Empty;

    public int Numero { get; set; }

    public string Rotulo { get; set; } = string.Empty;

    public DateOnly Inicio { get; set; }

    public DateOnly? Fim { get; set; }

    public DateOnly? Prazo { get; set; }

    public string Situacao { get; set; } = string.Empty;

    public DateTime? FechadoEm { get; set; }

    public string? FechadoPor { get; set; }
    // F1 (C19): o nome da pessoa (o do cadastro do usuário; sem nome, o e-mail); nulo com o FechadoPor
    public string? FechadoPorNome { get; set; }

    public PeCicloRelatorioResponse? Relatorio { get; set; }

    public DateTime? ReabertoEm { get; set; }

    public string? ReabertoPor { get; set; }
    // F1 (C19): o nome da pessoa (o do cadastro do usuário; sem nome, o e-mail); nulo com o ReabertoPor
    public string? ReabertoPorNome { get; set; }

    public bool PodeEditar { get; set; }

    // A mais que o contrato: o PDTIC do ciclo (as rotas do ciclo e do RA partem dele)
    public long PdticId { get; set; }
}
