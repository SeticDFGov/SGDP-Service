namespace Models.Contratacoes;

/// <summary>
/// Domínios fechados do módulo Supervisão Contínua das Contratações. Os CHECKs de banco são
/// reproduzidos em <see cref="CtrModelConfiguration"/>; os valores vêm da planilha
/// legada e do despacho padrão da SGDI ao TCDF (Decreto nº 48.899/2026 + IN SGDI nº 1/2026).
/// </summary>
public static class CtrDominios
{
    /// <summary>
    /// Categoria do objeto contratado: os 12 valores observados na planilha mais
    /// "Serviços de TIC" (pedido de 2026-09-22).
    /// </summary>
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
        public const string ServicosTic = "Serviços de TIC";

        /// <summary>Default da importação quando a célula "Categoria do Objeto" vem vazia.</summary>
        public const string SemObjeto = "Sem objeto / Indefinido";

        public const string Outros = "Outros";

        public static readonly string[] Todos =
        {
            InfraestruturaRede, DesenvolvimentoSoftware, OutsourcingImpressao, LicenciamentoSoftware,
            InteligenciaArtificial, SegurancaCibernetica, SistemasGestao, CertificacaoDigital,
            CaptacaoAudiovisual, TelefoniaVoip, ServicosTic, SemObjeto, Outros
        };
    }

    /// <summary>
    /// Situação derivada do processo — NUNCA gravada. Fonte única do cálculo:
    /// <c>CtrProcessoService.CalcularSituacao</c>. Desde 2026-09-22 a assinatura do
    /// contrato é a data FINAL do trâmite: com ela o processo está Concluído (sai da
    /// lista e vai para a relação de concluídos do painel). A devolução ao órgão, que
    /// até então se chamava "Concluído", passou a "Análise concluída".
    /// </summary>
    public static class Situacao
    {
        public const string Concluido = "Concluído";
        public const string Restituido = "Restituído";
        public const string AnaliseConcluida = "Análise concluída";
        public const string RetornadoGabSgdi = "Retornado ao Gab SGDI";
        public const string EmAnaliseUgtic = "Em análise na UGTIC";
        public const string EmAnaliseSubgd = "Em análise na SUBGD";
        public const string EmAnaliseSgdi = "Em análise na SGDI";
        public const string SemMovimentacao = "Sem movimentação";

        public static readonly string[] Todos =
        {
            Concluido, Restituido, AnaliseConcluida, RetornadoGabSgdi, EmAnaliseUgtic,
            EmAnaliseSubgd, EmAnaliseSgdi, SemMovimentacao
        };
    }

    /// <summary>
    /// Checkpoints do trâmite, na ordem cronológica esperada. A assinatura do contrato é a
    /// sexta e última data: fora da cronologia dos outros cinco (o TCDF também analisa
    /// contrato já assinado), mas conclui o processo.
    /// </summary>
    public static class Etapa
    {
        public const string ChegadaSgdi = "ChegadaSgdi";
        public const string ChegadaSubgd = "ChegadaSubgd";
        public const string ChegadaUgtic = "ChegadaUgtic";
        public const string RetornoGabSgdi = "RetornoGabSgdi";
        public const string RetornoOrgao = "RetornoOrgao";
        public const string AssinaturaContrato = "AssinaturaContrato";

        public static readonly string[] Todos =
        {
            ChegadaSgdi, ChegadaSubgd, ChegadaUgtic, RetornoGabSgdi, RetornoOrgao, AssinaturaContrato
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
    /// Como o processo chegou à SGDI: comunicado pelo órgão (o caminho normal) ou
    /// cadastrado a partir de análise do próprio TCDF sobre o contrato.
    /// </summary>
    public static class Origem
    {
        public const string OrgaoComunicante = "Órgão comunicante";
        public const string Tcdf = "TCDF";

        public static readonly string[] Todos = { OrgaoComunicante, Tcdf };
    }

    /// <summary>
    /// Se a solução contratada fica hospedada no CeTIC-DF. "Não aplicável (SaaS)" é o
    /// software contratado como serviço, que roda na infraestrutura do fornecedor. Nulo no
    /// processo = não informado.
    /// </summary>
    public static class HospedagemCetic
    {
        public const string Sim = "Sim";
        public const string Nao = "Não";
        public const string Parcialmente = "Parcialmente";
        public const string NaoAplicavelSaas = "Não aplicável (SaaS)";

        public static readonly string[] Todos = { Sim, Nao, Parcialmente, NaoAplicavelSaas };
    }

    /// <summary>Situação no Portfólio Estratégico de Contratações de TIC (incisos I e II do despacho).</summary>
    public static class SituacaoPortfolio
    {
        public const string ComunicadaPreviamente = "Comunicada previamente";       // inciso I
        public const string NaoComunicadaPreviamente = "Não comunicada previamente"; // inciso II

        public static readonly string[] Todos = { ComunicadaPreviamente, NaoComunicadaPreviamente };
    }

    /// <summary>
    /// Criticidade da contratação (art. 11, § 1º, da IN SGDI nº 1/2026). Desde 2026-09-22 é
    /// DERIVADA das respostas aos critérios (<see cref="CriterioCriticidade"/>; fonte única
    /// do cálculo: <c>CtrCriticidade.Calcular</c>) e gravada na coluna, para filtro e ordem.
    /// Processo sem respostas guarda a criticidade registrada antes da regra automática.
    /// </summary>
    public static class Criticidade
    {
        public const string Alta = "Alta";
        public const string Media = "Média";
        public const string Baixa = "Baixa";

        /// <summary>Da mais alta para a mais baixa: é a ordem padrão da lista.</summary>
        public static readonly string[] Todos = { Alta, Media, Baixa };
    }

    /// <summary>
    /// Critérios de priorização do art. 11, § 3º, da IN SGDI nº 1/2026, respondidos no
    /// cadastro do processo. I, III, V e VII são Sim/Não; II, IV e VI são graduados
    /// (Nenhum, Baixo, Médio, Alto), e o IV aceita ainda "Não foi possível avaliar com as
    /// informações apresentadas". A resposta padrão de cada um é a primeira da lista.
    /// </summary>
    public static class CriterioCriticidade
    {
        /// <summary>I: alinhamento às diretrizes da EGD/DF.</summary>
        public const string AlinhamentoEgd = "I";

        /// <summary>II: impacto sobre a prestação de serviços públicos digitais.</summary>
        public const string ImpactoServicos = "II";

        /// <summary>III: potencial de compartilhamento ou utilização corporativa da solução.</summary>
        public const string Compartilhamento = "III";

        /// <summary>IV: impacto sobre a arquitetura corporativa, a interoperabilidade ou a governança de dados.</summary>
        public const string ImpactoArquitetura = "IV";

        /// <summary>V: tecnologias emergentes, computação em nuvem ou soluções baseadas em IA.</summary>
        public const string TecnologiasEmergentes = "V";

        /// <summary>VI: riscos de segurança da informação, proteção de dados pessoais ou continuidade dos serviços.</summary>
        public const string RiscosSeguranca = "VI";

        /// <summary>VII: valor estimado igual ou superior ao limite das alíneas a, b ou c.</summary>
        public const string ValorEstimado = "VII";

        public static readonly string[] Todos =
        {
            AlinhamentoEgd, ImpactoServicos, Compartilhamento, ImpactoArquitetura,
            TecnologiasEmergentes, RiscosSeguranca, ValorEstimado
        };

        public const string Nao = "Não";
        public const string Sim = "Sim";

        public static readonly string[] RespostasSimNao = { Nao, Sim };

        public const string Nenhum = "Nenhum";
        public const string Baixo = "Baixo";
        public const string Medio = "Médio";
        public const string Alto = "Alto";

        public static readonly string[] RespostasGrau = { Nenhum, Baixo, Medio, Alto };

        /// <summary>
        /// Só no critério IV: as informações apresentadas não permitiram avaliar o impacto.
        /// Vale 0 ponto, como "Nenhum" (decisão do usuário, 2026-09-22).
        /// </summary>
        public const string NaoFoiPossivelAvaliar = "Não foi possível avaliar com as informações apresentadas";

        /// <summary>
        /// Atalho aceito na entrada (tela e planilha) para <see cref="NaoFoiPossivelAvaliar"/>;
        /// nunca é gravado: vira sempre a frase completa.
        /// </summary>
        public const string NaoFoiPossivelAvaliarAtalho = "Não foi possível avaliar";

        /// <summary>As respostas do critério IV: a graduação e, por último, a que não avalia.</summary>
        public static readonly string[] RespostasImpactoArquitetura = { Nenhum, Baixo, Medio, Alto, NaoFoiPossivelAvaliar };

        /// <summary>Os três critérios graduados (os demais são Sim/Não).</summary>
        public static readonly string[] Graduados = { ImpactoServicos, ImpactoArquitetura, RiscosSeguranca };

        public static string[] RespostasDe(string codigo) =>
            codigo == ImpactoArquitetura ? RespostasImpactoArquitetura
            : Graduados.Contains(codigo) ? RespostasGrau
            : RespostasSimNao;

        /// <summary>Resposta assumida quando o critério não é respondido (Não ou Nenhum).</summary>
        public static string RespostaPadrao(string codigo) => RespostasDe(codigo)[0];
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
