namespace api.Planejamento;

// ── PDTIC dos órgãos (E4) ────────────────────────────────────────────────────

/// <summary>
/// O PDTIC de um órgão numa versão, com o nível de maturidade de hoje do órgão e se quem
/// chama pode editar a elaboração (equipe do órgão, ou admin geral, com o PDTIC em elaboração
/// ou devolvido: as etapas 1 a 3, o documento e os fluxos). Para os outros passos (a
/// publicação e as etapas 4 a 7), vale o PodeEditar de cada passo em GET pdtic/{id}/situacao.
/// Desde a E7, as datas do caminho da aprovação, a deliberação mais recente do CGTIC (com o
/// PDF enviado) e, na revisão, a versão revista.
/// </summary>
public class PePdticResponse
{
    public long Id { get; set; }

    public long OrgaoId { get; set; }

    public string OrgaoSigla { get; set; } = string.Empty;

    public string OrgaoNome { get; set; } = string.Empty;

    public string Versao { get; set; } = string.Empty;

    // em_elaboracao, em_aprovacao, devolvido, aprovado, publicado, em_acompanhamento, encerrado ou substituido
    public string Situacao { get; set; } = string.Empty;

    // Copiadas do passo 1.1 (seção abrangencia) a cada gravação
    public DateOnly? VigenciaInicio { get; set; }

    public DateOnly? VigenciaFim { get; set; }

    public bool RegistradoExternamente { get; set; }

    public long? AnteriorId { get; set; }

    // Nível de hoje do órgão (o escolhido ou o padrão)
    public long? NivelId { get; set; }

    public string? NivelNome { get; set; }

    // A elaboração (etapas 1 a 3, documento e fluxos) está aberta para quem chama
    public bool PodeEditar { get; set; }

    public DateTime CriadoEm { get; set; }

    public string CriadoPor { get; set; } = string.Empty;
    // F1 (C19): o nome da pessoa (o do cadastro do usuário; sem nome, o e-mail)
    public string CriadoPorNome { get; set; } = string.Empty;

    // ── Caminho da aprovação (E7) ──

    // O último envio ao CGTIC
    public DateTime? EnviadoEm { get; set; }

    public DateTime? AprovadoEm { get; set; }

    public DateTime? PublicadoEm { get; set; }

    public DateTime? EncerradoEm { get; set; }

    public string? EncerramentoMotivo { get; set; }

    // A deliberação mais recente do CGTIC sobre esta versão (a forma da fila, com o Documento), ou nulo
    public PeDeliberacaoResponse? Deliberacao { get; set; }

    // Na revisão (versão 1.1 aberta a partir da vigente 1.0): a versão revista; nulo fora dela
    public PePdticRevisaoResponse? Revisao { get; set; }
}

/// <summary>A versão que a revisão reviu (a vigente quando a revisão foi aberta).</summary>
public class PePdticRevisaoResponse
{
    public long AnteriorId { get; set; }

    public string AnteriorVersao { get; set; } = string.Empty;

    // A mais que o contrato: a justificativa de quem abriu a revisão sem a decisão do comitê (Básico)
    public string? Justificativa { get; set; }
}

/// <summary>GET pdtic/{id}/envio: a prévia do envio ao CGTIC (o que falta, passo a passo).</summary>
public class PeEnvioResponse
{
    public bool PodeEnviar { get; set; }

    public List<PePendenciaResponse> Pendencias { get; set; } = new();

    // A mais que o contrato: por que não pode enviar quando a lista não explica (a situação ou o papel), ou nulo
    public string? Motivo { get; set; }
}

/// <summary>Um passo que falta para o envio: o passo da trilha (id, número e título) e o que fazer nele.</summary>
public class PePendenciaResponse
{
    public long PassoId { get; set; }

    public string PassoNumero { get; set; } = string.Empty;

    public string PassoTitulo { get; set; } = string.Empty;

    public string Motivo { get; set; } = string.Empty;
}

/// <summary>POST pdtic/{id}/encerrar: { Motivo } (obrigatório quando o administrador encerra pela vigência vencida).</summary>
public class PeEncerrarDTO
{
    public string? Motivo { get; set; }
}

/// <summary>POST pdtic/{id}/revisao: { Justificativa } (obrigatória quando o passo 6.3 não está na trilha do órgão).</summary>
public class PeRevisaoDTO
{
    public string? Justificativa { get; set; }
}

/// <summary>
/// POST pdtic/registrar-externo: o PDTIC aprovado fora do sistema. Datas em "aaaa-mm-dd";
/// ArquivoId é o PDF enviado antes por POST arquivos; AprovacaoInstancia "cgtic" ou "outra".
/// Erros de campo em Campos, pelo nome da propriedade ("VigenciaFim").
/// </summary>
public class PeRegistroExternoDTO
{
    // Nulo para a equipe do órgão (o próprio); o órgão escolhido pelo admin geral
    public long? OrgaoId { get; set; }

    // "1.0", "2.1"...
    public string? Versao { get; set; }

    public string? VigenciaInicio { get; set; }

    public string? VigenciaFim { get; set; }

    public long? ArquivoId { get; set; }

    public string? AprovacaoInstancia { get; set; }

    public string? AprovacaoData { get; set; }

    public string? AprovacaoAtoTipo { get; set; }

    public string? AprovacaoAtoNumero { get; set; }

    public string? AprovacaoSei { get; set; }

    public string? PublicacaoData { get; set; }

    public string? PublicacaoEndereco { get; set; }
}

/// <summary>
/// POST api/planejamento/pdtic: abre o PDTIC. Equipe do órgão: OrgaoId nulo (ou o próprio);
/// admin geral: o órgão escolhido.
/// </summary>
public class PePdticCriarDTO
{
    public long? OrgaoId { get; set; }
}

/// <summary>GET api/planejamento/pdtic (papéis globais e admin geral): filtros e página.</summary>
public class PePdticConsulta
{
    public string? Situacao { get; set; }

    // Sigla ou nome do órgão
    public string? Filtro { get; set; }

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 20;
}

// ── Situação dos passos ──────────────────────────────────────────────────────

/// <summary>GET pdtic/{id}/situacao: a situação de cada passo visível e o próximo passo recomendado.</summary>
public class PePdticSituacaoResponse
{
    // Número ("2.3") do passo recomendado: o primeiro atrasado, senão o primeiro em atenção,
    // senão o primeiro pendente, na ordem da trilha (aguardando e externo nunca); na revisão
    // recém-aberta, o primeiro passo da etapa 2; nulo quando não há ou o PDTIC encerrou
    public string? ProximoPasso { get; set; }

    // F1 (B12): por que o próximo passo é esse, quando não é o óbvio (na revisão recém-aberta, o
    // passo recomendado pode estar feito: os dados vieram da versão anterior); nulo nos outros casos
    public string? ProximoPassoMotivo { get; set; }

    public List<PePassoSituacaoResponse> Passos { get; set; } = new();

    // F3: o nível que o PDTIC alcançou pela régua dos níveis e o que falta para o próximo
    public PeNivelDoPdticResponse Nivel { get; set; } = new();
}

/// <summary>
/// O nível que o PDTIC alcançou (F3; nos dois modos, a tela usa no modo livre): o nível mais alto
/// cujos passos obrigatórios que contam (etapas 1 a 3, dos tipos dados, conferência dos temas e
/// aprovação) estão resolvidos, com todos os níveis antes dele também; o próximo nível e o que
/// falta para chegar a ele. No PDTIC registrado fora do sistema, o nível não é calculado (o Motivo diz).
/// </summary>
public class PeNivelDoPdticResponse
{
    // O modo dos níveis de hoje: "livre" ou "definido"
    public string Modo { get; set; } = "definido";

    public long? AlcancadoId { get; set; }

    public string? AlcancadoNome { get; set; }

    // O seguinte ao alcançado (sem nível alcançado, o primeiro); nulo quando alcançou o último
    public long? ProximoId { get; set; }

    public string? ProximoNome { get; set; }

    // O que falta para chegar ao próximo nível (vazia sem próximo nível)
    public List<PeNivelFaltaResponse> Faltam { get; set; } = new();

    // Por que o nível não é calculado (PDTIC registrado fora do sistema), ou nulo
    public string? Motivo { get; set; }
}

/// <summary>Um passo que falta para o próximo nível: o número no passo a passo do órgão (nulo quando o órgão não vê o passo), o título e o que falta.</summary>
public class PeNivelFaltaResponse
{
    public long PassoId { get; set; }

    public string? PassoNumero { get; set; }

    public string PassoTitulo { get; set; } = string.Empty;

    public string Motivo { get; set; } = string.Empty;
}

public class PePassoSituacaoResponse
{
    public long PassoId { get; set; }

    public string Chave { get; set; } = string.Empty;

    public string Numero { get; set; } = string.Empty;

    // feito, pendente, atencao, nao_se_aplica, continuo, aguardando, atrasado, externo ou (F3) opcional
    public string Situacao { get; set; } = string.Empty;

    // F3: a situação do passo no passo a passo do órgão é "obrigatorio"
    public bool Obrigatorio { get; set; }

    // F3: o passo recebe a validação da equipe (os da elaboração dos tipos dados, conferência dos
    // temas, documento e fluxo; sem olhar quem chama; falso antes da versão 8 do modelo inicial)
    public bool AceitaValidacao { get; set; }

    // F3: a validação da equipe, ou nulo
    public PeValidacaoResponse? Validacao { get; set; }

    // Por que o passo está aguardando, atrasado ou externo (texto pronto); nulo nos outros
    public string? Motivo { get; set; }

    // Quem chama edita este passo agora (a equipe do órgão, pela situação do PDTIC e pela etapa)
    public bool PodeEditar { get; set; }

    public PeNaoSeAplicaResponse? NaoSeAplica { get; set; }

    public int ComentariosAbertos { get; set; }

    // Avisos do guia e do caminho da aprovação, em linguagem simples (não bloqueiam)
    public List<string> Avisos { get; set; } = new();
}

public class PeNaoSeAplicaResponse
{
    public string Justificativa { get; set; } = string.Empty;

    public DateTime MarcadoEm { get; set; }

    public string MarcadoPor { get; set; } = string.Empty;
    // F1 (C19): o nome da pessoa (o do cadastro do usuário; sem nome, o e-mail)
    public string MarcadoPorNome { get; set; } = string.Empty;
}

/// <summary>PUT pdtic/{id}/passos/{passoId}/nao-se-aplica.</summary>
public class PeNaoSeAplicaDTO
{
    public string? Justificativa { get; set; }
}

/// <summary>
/// A validação da equipe num passo (F3): quem marcou e quando e, quando os dados do passo mudaram
/// depois, a data da primeira mudança.
/// </summary>
public class PeValidacaoResponse
{
    public DateTime ValidadoEm { get; set; }

    public string ValidadoPor { get; set; } = string.Empty;

    // O nome da pessoa (o do cadastro do usuário; sem nome, o e-mail)
    public string ValidadoPorNome { get; set; } = string.Empty;

    // A primeira mudança nos dados do passo depois da validação, ou nulo
    public DateTime? AlteradoDepoisEm { get; set; }
}

// ── Temas das ações (incisos V, VI e IX) ─────────────────────────────────────

public class PeTemasResponse
{
    public List<PeTemaResponse> Temas { get; set; } = new();
}

public class PeTemaResponse
{
    // Valor da opção do campo acoes.tema (seguranca, transformacao_digital, governanca_dados)
    public string Valor { get; set; } = string.Empty;

    public string Rotulo { get; set; } = string.Empty;

    // V, VI ou IX (art. 12, § 2º, do Decreto nº 48.900/2026)
    public string Inciso { get; set; } = string.Empty;

    public List<PeTemaAcaoResponse> Acoes { get; set; } = new();

    // Justificativa de tema sem ação (seção temas_sem_acao), ou nulo
    public string? Justificativa { get; set; }
}

public class PeTemaAcaoResponse
{
    public long RegistroId { get; set; }

    public string? Codigo { get; set; }

    public string Descricao { get; set; } = string.Empty;

    // O rótulo da situação da ação ("Em andamento"), ou nulo
    public string? Situacao { get; set; }
}

// ── Sistemas de IA do PGIA (inciso VIII) ─────────────────────────────────────

/// <summary>Um sistema de IA do inventário do PGIA do órgão (só leitura).</summary>
public class PeSistemaIaPgiaResponse
{
    public long Id { get; set; }

    // Denominação do sistema
    public string Nome { get; set; } = string.Empty;

    public string Finalidade { get; set; } = string.Empty;

    // Classificação de risco atual (Baixo, Moderado, Alto, Excessivo); nula sem avaliação
    public string? Classificacao { get; set; }

    // Enquadramento legal da classificação ("art. 16, III"); nulo no baixo risco
    public string? Base { get; set; }

    // Fase do ciclo de vida ("Implantado (em uso)", "Em aquisição"...)
    public string Situacao { get; set; } = string.Empty;
}

// ── Comentários ──────────────────────────────────────────────────────────────

/// <summary>Comentário principal de um passo, com as respostas em ordem.</summary>
public class PeComentarioResponse
{
    public long Id { get; set; }

    public long PassoId { get; set; }

    public string Texto { get; set; } = string.Empty;

    public string AutorNome { get; set; } = string.Empty;

    public string AutorEmail { get; set; } = string.Empty;

    public DateTime CriadoEm { get; set; }

    public DateTime? ResolvidoEm { get; set; }

    public string? ResolvidoPor { get; set; }
    // F1 (C19): o nome da pessoa (o do cadastro do usuário; sem nome, o e-mail); nulo com o ResolvidoPor
    public string? ResolvidoPorNome { get; set; }

    public List<PeComentarioRespostaResponse> Respostas { get; set; } = new();
}

public class PeComentarioRespostaResponse
{
    public long Id { get; set; }

    public string Texto { get; set; } = string.Empty;

    public string AutorNome { get; set; } = string.Empty;

    public string AutorEmail { get; set; } = string.Empty;

    public DateTime CriadoEm { get; set; }
}

/// <summary>POST pdtic/{id}/comentarios: { PassoId, PaiId (nulo = comentário principal), Texto }.</summary>
public class PeComentarioCriarDTO
{
    public long? PassoId { get; set; }

    public long? PaiId { get; set; }

    public string? Texto { get; set; }
}
