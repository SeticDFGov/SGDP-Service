namespace Models.Contratacoes;

/// <summary>
/// Domínios fechados do módulo Análises de Contratações. Os CHECKs de banco são
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
    /// Estágio derivado da manifestação — NUNCA gravado. Fonte única do cálculo:
    /// <c>CtrManifestacaoService.CalcularEstagio</c>.
    /// </summary>
    public static class Estagio
    {
        public const string NotificacaoRegularizar = "Notificação para regularizar";
        public const string Alinhada = "Alinhada";
        public const string InformacoesSolicitadas = "Informações solicitadas";
        public const string AguardandoResposta = "Aguardando resposta";
        public const string RiscoResolvido = "Risco resolvido";
        public const string NaoPodeProsseguir = "Não pode prosseguir";

        public static readonly string[] Todos =
        {
            NotificacaoRegularizar, Alinhada, InformacoesSolicitadas,
            AguardandoResposta, RiscoResolvido, NaoPodeProsseguir
        };
    }
}
