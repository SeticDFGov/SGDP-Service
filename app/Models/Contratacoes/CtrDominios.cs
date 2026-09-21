namespace Models.Contratacoes;

/// <summary>
/// Domínios fechados do módulo Supervisão Contínua das Contratações. Os CHECKs de banco são
/// reproduzidos em <see cref="CtrModelConfiguration"/>; os valores vêm da planilha
/// legada e do despacho padrão da SGDI ao TCDF (Decreto nº 48.899/2026 + IN SGDI nº 1/2026).
/// </summary>
public static class CtrDominios
{
    /// <summary>Categoria do objeto contratado (os 12 valores observados na planilha).</summary>
    public static class CategoriaObjeto
    {
        public const string InfraestruturaRede = "Infraestrutura de Rede";
        public const string DesenvolvimentoSoftware = "Desenvolvimento / Fábrica de Software";
        public const string OutsourcingImpressao = "Outsourcing de Impressão";
        public const string LicenciamentoSoftware = "Licenciamento de Software";
        public const string InteligenciaArtificial = "Inteligência Artificial";
        public const string SegurancaCibernetica = "Segurança Cibernética";
        public const string SistemasGestao = "Sistemas de Gestão";
        public const string CertificacaoDigital = "Certificação Digital";
        public const string CaptacaoAudiovisual = "Captação Audiovisual";
        public const string TelefoniaVoip = "Telefonia / VoIP";

        /// <summary>Default da importação quando a célula "Categoria do Objeto" vem vazia.</summary>
        public const string SemObjeto = "Sem objeto / Indefinido";

        public const string Outros = "Outros";

        public static readonly string[] Todos =
        {
            InfraestruturaRede, DesenvolvimentoSoftware, OutsourcingImpressao, LicenciamentoSoftware,
            InteligenciaArtificial, SegurancaCibernetica, SistemasGestao, CertificacaoDigital,
            CaptacaoAudiovisual, TelefoniaVoip, SemObjeto, Outros
        };
    }

    /// <summary>
    /// Situação derivada do processo — NUNCA gravada. Fonte única do cálculo:
    /// <c>CtrProcessoService.CalcularSituacao</c>.
    /// </summary>
    public static class Situacao
    {
        public const string Restituido = "Restituído";
        public const string Concluido = "Concluído";
        public const string RetornadoGabSgdi = "Retornado ao Gab SGDI";
        public const string EmAnaliseUgtic = "Em análise na UGTIC";
        public const string EmAnaliseSubgd = "Em análise na SUBGD";
        public const string EmAnaliseSgdi = "Em análise na SGDI";
        public const string SemMovimentacao = "Sem movimentação";

        public static readonly string[] Todos =
        {
            Restituido, Concluido, RetornadoGabSgdi, EmAnaliseUgtic,
            EmAnaliseSubgd, EmAnaliseSgdi, SemMovimentacao
        };
    }

    /// <summary>Checkpoints do trâmite, na ordem cronológica esperada.</summary>
    public static class Etapa
    {
        public const string ChegadaSgdi = "ChegadaSgdi";
        public const string ChegadaSubgd = "ChegadaSubgd";
        public const string ChegadaUgtic = "ChegadaUgtic";
        public const string RetornoGabSgdi = "RetornoGabSgdi";
        public const string RetornoOrgao = "RetornoOrgao";

        public static readonly string[] Todos =
        {
            ChegadaSgdi, ChegadaSubgd, ChegadaUgtic, RetornoGabSgdi, RetornoOrgao
        };
    }

    /// <summary>
    /// Etapa do planejamento da contratação (siglas são o jargão da área): Documento de
    /// Formalização da Demanda, Estudo Técnico Preliminar e Termo de Referência. Continua
    /// registrada depois da assinatura do contrato — é onde o planejamento parou.
    /// </summary>
    public static class EtapaPlanejamento
    {
        public const string Dfd = "DFD";
        public const string Etp = "ETP";
        public const string Tr = "TR";

        public static readonly string[] Todos = { Dfd, Etp, Tr };
    }

    /// <summary>
    /// Fase da contratação — DERIVADA, nunca gravada. Fonte única do cálculo:
    /// <c>CtrProcessoService.CalcularFase</c> (assinatura do contrato = execução).
    /// </summary>
    public static class Fase
    {
        public const string Planejamento = "Planejamento";
        public const string Execucao = "Execução";

        public static readonly string[] Todos = { Planejamento, Execucao };
    }

    /// <summary>
    /// Como o processo chegou à SGDI: comunicado pelo órgão (o caminho normal) ou
    /// cadastrado a partir de análise do próprio TCDF sobre o contrato.
    /// </summary>
    public static class Origem
    {
        public const string OrgaoComunicante = "Órgão comunicante";
        public const string Tcdf = "TCDF";

        public static readonly string[] Todos = { OrgaoComunicante, Tcdf };
    }

    /// <summary>Situação no Portfólio Estratégico de Contratações de TIC (incisos I e II do despacho).</summary>
    public static class SituacaoPortfolio
    {
        public const string ComunicadaPreviamente = "Comunicada previamente";       // inciso I
        public const string NaoComunicadaPreviamente = "Não comunicada previamente"; // inciso II

        public static readonly string[] Todos = { ComunicadaPreviamente, NaoComunicadaPreviamente };
    }

    /// <summary>Criticidade da contratação (art. 11 da IN SGDI nº 1/2026).</summary>
    public static class Criticidade
    {
        public const string Alta = "Alta";
        public const string Media = "Média";
        public const string Baixa = "Baixa";

        public static readonly string[] Todos = { Alta, Media, Baixa };
    }

    /// <summary>Resultado da análise e providência adotada (inciso I do despacho).</summary>
    public static class ResultadoAnalise
    {
        public const string Alinhada = "Alinhada";
        public const string InformacoesComplementares = "Informações complementares";
        public const string RiscosSignificativos = "Riscos significativos";

        public static readonly string[] Todos = { Alinhada, InformacoesComplementares, RiscosSignificativos };
    }

    /// <summary>Desfecho exclusivo do bloco de riscos significativos.</summary>
    public static class DesfechoRisco
    {
        public const string AguardandoResposta = "Aguardando resposta";
        public const string RiscoResolvido = "Risco resolvido";
        public const string NaoPodeProsseguir = "Não pode prosseguir";

        public static readonly string[] Todos = { AguardandoResposta, RiscoResolvido, NaoPodeProsseguir };
    }

    /// <summary>
    /// Status do processo no TCDF: fato do Tribunal, não desfecho da análise da SGDI —
    /// vale em qualquer inciso e com qualquer resultado. Nulo = sem status especial.
    /// </summary>
    public static class StatusTcdf
    {
        public const string SuspensoIrregularidades = "Suspenso por irregularidades";
        public const string EditalRevogado = "Edital revogado";

        public static readonly string[] Todos = { SuspensoIrregularidades, EditalRevogado };
    }

    /// <summary>
    /// Nível de um risco da contratação: DERIVADO da célula da matriz da CGDF (5 × 5),
    /// nunca gravado. Fonte única do cálculo: <c>CtrClassificacaoRisco.CalcularNivel</c>.
    /// Ordem crescente. (O "risco classificado" pelo questionário do PGIA saiu do módulo
    /// em 2026-09-21.)
    /// </summary>
    public static class NivelRisco
    {
        public const string Baixo = "Baixo";     // verde
        public const string Medio = "Médio";     // amarelo
        public const string Alto = "Alto";       // laranja
        public const string Extremo = "Extremo"; // vermelho

        /// <summary>Rótulo de ausência (filtro e painel): processo sem risco declarado.</summary>
        public const string SemRiscosDeclarados = "Sem riscos declarados";

        public static readonly string[] Todos = { Baixo, Medio, Alto, Extremo };
    }

    /// <summary>
    /// Estágio derivado da manifestação — NUNCA gravado. Fonte única do cálculo:
    /// <c>CtrManifestacaoService.CalcularEstagio</c>. Os dois últimos valores vêm do
    /// <see cref="StatusTcdf"/>, que tem PRECEDÊNCIA sobre o desfecho da análise.
    /// </summary>
    public static class Estagio
    {
        public const string NotificacaoRegularizar = "Notificação para regularizar";
        public const string Alinhada = "Alinhada";
        public const string InformacoesSolicitadas = "Informações solicitadas";
        public const string AguardandoResposta = "Aguardando resposta";
        public const string RiscoResolvido = "Risco resolvido";
        public const string NaoPodeProsseguir = "Não pode prosseguir";
        public const string SuspensoIrregularidades = StatusTcdf.SuspensoIrregularidades;
        public const string EditalRevogado = StatusTcdf.EditalRevogado;

        public static readonly string[] Todos =
        {
            NotificacaoRegularizar, Alinhada, InformacoesSolicitadas,
            AguardandoResposta, RiscoResolvido, NaoPodeProsseguir,
            SuspensoIrregularidades, EditalRevogado
        };
    }
}
