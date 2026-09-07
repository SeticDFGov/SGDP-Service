namespace Models.Pgia;

/// <summary>
/// Domínios fechados do PGIA (aba Domínios do modelo de dados / CHECKs do schema_pgia_48901.sql).
/// Os CHECKs de banco são reproduzidos em PgiaModelConfiguration.
/// </summary>
public static class PgiaDominios
{
    // D10 — natureza jurídica do órgão (art. 1º e § único)
    public static class NaturezaJuridica
    {
        public const string AdministracaoDireta = "Administração direta";
        public const string Autarquia = "Autarquia";
        public const string FundacaoPublica = "Fundação pública";
        public const string EmpresaPublica = "Empresa pública";
        public const string SociedadeEconomiaMista = "Sociedade de economia mista";

        public static readonly string[] Todos =
        {
            AdministracaoDireta, Autarquia, FundacaoPublica, EmpresaPublica, SociedadeEconomiaMista
        };
    }

    // D11 — vínculo do agente público (art. 3º)
    public static class Vinculo
    {
        public const string ServidorEfetivo = "Servidor efetivo";
        public const string Comissionado = "Comissionado";
        public const string Estagiario = "Estagiário";
        public const string PrestadorServico = "Prestador de serviço";

        public static readonly string[] Todos =
        {
            ServidorEfetivo, Comissionado, Estagiario, PrestadorServico
        };
    }

    // Situação calculada dos prazos de conformidade (v_prazos_situacao do schema)
    public static class SituacaoPrazo
    {
        public const string CumpridaNoPrazo = "Cumprida no prazo";
        public const string CumpridaEmAtraso = "Cumprida em atraso";
        public const string Vencida = "Vencida";
        public const string NoPrazo = "No prazo";
    }

    // D01 — classificação de risco (arts. 14 a 18)
    public static class ResultadoRisco
    {
        public const string Excessivo = "Risco Excessivo";
        public const string Alto = "Alto Risco";
        public const string Moderado = "Risco Moderado";
        public const string Baixo = "Baixo Risco";

        public static readonly string[] Todos = { Excessivo, Alto, Moderado, Baixo };
    }

    // D06 — origem do registro (art. 37)
    public static class OrigemRegistro
    {
        // Exige descrição livre em origem_registro_descricao
        public const string Outros = "Outros";

        public static readonly string[] Todos =
        {
            "Nova iniciativa", "Instrumento vigente em revisão", Outros
        };
    }

    // D07 — tipo de sistema (arts. 1º e 2º, III)
    public static class TipoSistema
    {
        public const string Contratado = "Contratado";

        public static readonly string[] Todos =
        {
            "Desenvolvido internamente", Contratado,
            "Embarcado em solução adquirida", "Plataforma pública de IA generativa"
        };
    }

    // D08 — tecnologia (art. 2º, I, II e VI)
    public static class Tecnologia
    {
        public static readonly string[] Todos =
        {
            "IA generativa", "IA preditiva ou aprendizado de máquina",
            "Visão computacional", "Processamento de linguagem natural", "Biometria", "Outra"
        };
    }

    // D05 — fase do ciclo de vida (art. 2º, VIII e XI)
    public static class StatusCicloVida
    {
        public const string Implantado = "Implantado (em uso)";
        public const string Monitoramento = "Monitoramento";

        public static readonly string[] Todos =
        {
            "Concepção", "Planejamento", "Em aquisição", "Desenvolvimento",
            "Treinamento", "Testagem", "Validação", Implantado,
            Monitoramento, "Descontinuado"
        };

        // Fases que caracterizam sistema em uso (bloqueadas para Risco Excessivo, art. 15)
        public static readonly string[] EmUso = { Implantado, Monitoramento };
    }

    // D09 — natureza dos dados tratados (arts. 18 a 20)
    public static class EscopoDados
    {
        public const string DadosPessoais = "Dados pessoais";
        public const string DadosPessoaisSensiveis = "Dados pessoais sensíveis";

        public static readonly string[] Todos =
        {
            "Somente dados públicos", DadosPessoais, DadosPessoaisSensiveis, "Dados sigilosos"
        };

        // Escopos que abrem o bloco LGPD (art. 19)
        public static readonly string[] ComDadosPessoais = { DadosPessoais, DadosPessoaisSensiveis };
    }

    // D27 — base legal do tratamento (art. 19, I)
    public static class BaseLegalLgpd
    {
        public static readonly string[] Todos =
        {
            "Execução de políticas públicas", "Cumprimento de obrigação legal ou regulatória",
            "Consentimento do titular", "Proteção da vida", "Tutela da saúde",
            "Exercício regular de direitos", "Outra hipótese dos arts. 7º e 11 da LGPD"
        };
    }

    // D31 — motivo da classificação (arts. 14, 16, § 2º e 37)
    public static class MotivoClassificacao
    {
        public static readonly string[] Todos =
        {
            "Classificação inicial", "Nova contratação", "Revisão de instrumento vigente",
            "Reclassificação por resolução do CGTIC", "Revisão periódica"
        };
    }

    // D28 — tipo de documento (apoio)
    public static class TipoDocumento
    {
        public static readonly string[] Todos =
        {
            "Ato de designação", "AIA", "RIPD", "Ata ou deliberação", "Relatório semestral",
            "Relatório anual", "Certificado de capacitação", "Contrato", "Termo aditivo",
            "Parecer de auditoria", "Avaliação de riscos", "Comprovação de triagem", "Outro"
        };
    }

    // Incisos válidos do checklist de risco (arts. 15 a 17)
    public static class ChecklistIncisos
    {
        public static readonly string[] Art15 = { "I", "II", "III", "IV", "V", "VI" };
        public static readonly string[] Art16 = { "I", "II", "III", "IV", "V", "VI", "VII", "VIII", "IX" };
        public static readonly string[] Art17 = { "I", "II", "III", "IV" };
    }

    /// <summary>
    /// Matriz de riscos da CGDF, usada no grupo "Outros" do questionário (riscos
    /// declarados pelo órgão). A escala completa fica registrada como constante;
    /// neste grupo só vale o SUBCONJUNTO permitido — riscos que o órgão declara
    /// aqui não podem ser de probabilidade ou consequência alta (esses são os dos
    /// arts. 15 a 17, que têm grupo próprio).
    /// </summary>
    public static class EscalaCgdf
    {
        public static class Probabilidade
        {
            public const string Improvavel = "Improvável";
            public const string Raro = "Raro";
            public const string Possivel = "Possível";
            public const string Provavel = "Provável";
            public const string QuaseCerto = "Quase certo";

            // Escala completa da CGDF, com o peso de cada grau (1..5)
            public static readonly IReadOnlyDictionary<string, int> Pesos = new Dictionary<string, int>
            {
                [Improvavel] = 1,
                [Raro] = 2,
                [Possivel] = 3,
                [Provavel] = 4,
                [QuaseCerto] = 5
            };

            public static readonly string[] Todos = { Improvavel, Raro, Possivel, Provavel, QuaseCerto };

            // Subconjunto aceito no grupo "Outros"
            public static readonly string[] Permitidos = { Improvavel, Raro, Possivel };
        }

        public static class Consequencia
        {
            public const string Desprezivel = "Desprezível";
            public const string Menor = "Menor";
            public const string Moderada = "Moderada";
            public const string Maior = "Maior";
            public const string Catastrofica = "Catastrófica";

            public static readonly IReadOnlyDictionary<string, int> Pesos = new Dictionary<string, int>
            {
                [Desprezivel] = 1,
                [Menor] = 2,
                [Moderada] = 3,
                [Maior] = 4,
                [Catastrofica] = 5
            };

            public static readonly string[] Todos = { Desprezivel, Menor, Moderada, Maior, Catastrofica };

            // Subconjunto aceito no grupo "Outros"
            public static readonly string[] Permitidos = { Desprezivel, Menor, Moderada };
        }
    }

    // ── Fase 2: governança central ─────────────────────────────────────────────

    // Fluxo de homologação do inventário (desenho da chefia): o órgão sempre envia;
    // Baixo/Moderado são avaliados pela SGDI; Alto/Excessivo são delegados ao CGTIC.
    public static class SituacaoHomologacao
    {
        public const string AguardandoSgdi = "Aguardando avaliação da SGDI";
        public const string AguardandoCgtic = "Aguardando deliberação do CGTIC";
        public const string Aprovado = "Aprovado";
        public const string Vetado = "Vetado";

        public static readonly string[] Todos = { AguardandoSgdi, AguardandoCgtic, Aprovado, Vetado };

        /// <summary>Rota inicial pela classificação: acima de Moderado vai ao comitê.</summary>
        public static string RotaInicial(string resultadoRisco) =>
            resultadoRisco is ResultadoRisco.Alto or ResultadoRisco.Excessivo
                ? AguardandoCgtic
                : AguardandoSgdi;
    }

    // D12 — situação da AIA (art. 22)
    public static class StatusAia
    {
        public const string NaoIniciada = "Não iniciada";
        public const string Concluida = "Concluída";

        public static readonly string[] Todos =
        {
            NaoIniciada, "Em elaboração", Concluida, "Em revisão"
        };
    }

    // D13 — tipo de deliberação do CGTIC (art. 7º)
    public static class TipoDeliberacao
    {
        public const string ClassificacaoAltoRisco = "Classificação de sistema como Alto Risco";
        public const string AprovacaoAquisicaoAltoRisco = "Aprovação de aquisição de Alto Risco";

        public static readonly string[] Todos =
        {
            ClassificacaoAltoRisco, AprovacaoAquisicaoAltoRisco,
            "Critérios e requisitos de aquisição", "Constituição de grupo de trabalho temático",
            "Apreciação do Relatório Anual", "Resolução normativa",
            "Autorização de treinamento com dados do GDF", "Ampliação do rol de Alto Risco",
            "Aprovação do Guia de Contratações", "Suspensão de sistema"
        };

        // Tipos cuja deliberação decide a homologação de um sistema delegado ao comitê
        public static readonly string[] DecidemHomologacao = { ClassificacaoAltoRisco, AprovacaoAquisicaoAltoRisco };
    }

    // D14 — resultado da deliberação (art. 7º, IV)
    public static class ResultadoDeliberacao
    {
        public const string Favoravel = "Favorável";
        public const string Desfavoravel = "Desfavorável";
        public const string EmDiligencia = "Em diligência";

        public static readonly string[] Todos = { Favoravel, Desfavoravel, EmDiligencia };
    }

    // D17 — situação da homologação de plataforma pública de IA generativa (arts. 8º, IV, 18 e 20)
    public static class StatusHomologacaoPlataforma
    {
        public static readonly string[] Todos =
        {
            "Em avaliação", "Homologada", "Homologada apta a dados pessoais e sigilosos",
            "Não homologada", "Homologação revogada"
        };
    }

    // D18 — tipo de autorização excepcional (arts. 18, § 2º e 21)
    public static class TipoAutorizacao
    {
        public const string TreinamentoFornecedor = "Uso de dados do GDF para treinamento pelo fornecedor";

        public static readonly string[] Todos =
        {
            "Uso de plataforma pública com dados não públicos", TreinamentoFornecedor
        };
    }

    // D34 — quem autorizou a exceção
    public static class AutorizadaPor
    {
        public static readonly string[] Todos = { "SGDI", "Órgão com aprovação do CGTIC" };
    }

    // D24 — tipo de norma complementar (arts. 8º, III, 26 e 36)
    public static class TipoNorma
    {
        public static readonly string[] Todos =
        {
            "Resolução do CGTIC", "Instrução normativa da SGDI", "Norma técnica ou guia",
            "Guia de Contratações de IA", "Recomendação técnica"
        };
    }

    // Emissor da norma complementar
    public static class EmissorNorma
    {
        public static readonly string[] Todos = { "SGDI", "CGTIC" };
    }

    // ── Fase 3: operação contínua ──────────────────────────────────────────────

    // D15 — hipóteses de incidente grave, incisos do art. 30
    public static class HipoteseIncidente
    {
        public static readonly string[] Todos = { "I", "II", "III", "IV", "V" };
    }

    // D16 — situação da apuração pela SGDI (arts. 8º, IX e 30, § único)
    public static class StatusApuracao
    {
        public const string Recebida = "Recebida";

        public static readonly string[] Todos =
        {
            Recebida, "Em apuração", "Concluída com recomendações",
            "Concluída sem medidas", "Encaminhada ao CGTIC"
        };
    }

    // D36 — origem da não conformidade (arts. 8º, X e 9º, II)
    public static class OrigemNaoConformidade
    {
        public const string Sgtic = "Acompanhamento do SGTIC";
        public const string Sgdi = "Supervisão da SGDI";

        public static readonly string[] Todos = { Sgtic, Sgdi };
    }

    public static class SituacaoNaoConformidade
    {
        public const string Registrada = "Registrada";

        public static readonly string[] Todos =
        {
            Registrada, "Reportada à SGDI", "Em tratamento", "Sanada",
            "Comunicada ao controle interno"
        };
    }

    // D19 — trilhas ProCapIA/DF (art. 29, I a IV)
    public static class TrilhaCapacitacao
    {
        public static readonly string[] Todos =
        {
            "Letramento em IA", "Uso responsável de IA", "Governança de IA",
            "Desenvolvimento e auditoria de sistemas de IA"
        };
    }

    // D20 — situação da capacitação
    public static class StatusCapacitacao
    {
        public const string Concluida = "Concluída";

        public static readonly string[] Todos = { "Prevista", "Em andamento", Concluida };
    }

    // ── Fase 4: contratações, relatórios e auditorias ──────────────────────────

    // D32 — situação do contrato de IA
    public static class StatusContrato
    {
        public static readonly string[] Todos = { "Vigente", "Suspenso", "Encerrado" };
    }

    // D30 — tipo de instrumento anterior ao decreto (art. 37)
    public static class TipoInstrumentoLegado
    {
        public static readonly string[] Todos =
        {
            "Contrato", "Ato normativo", "Convênio ou instrumento congênere"
        };
    }

    // D29 — o instrumento envolve IA? Incerto é tratado como sim até confirmação
    public static class EnvolveIa
    {
        public const string Sim = "Sim";
        public const string Incerto = "Incerto";

        public static readonly string[] Todos = { Sim, "Não", Incerto };
    }

    // D23 — categoria do indicador de desempenho (arts. 25, § 2º e 31)
    public static class CategoriaIndicador
    {
        public static readonly string[] Todos =
        {
            "Desempenho", "Adoção", "Conformidade", "Impacto",
            "Acurácia", "Equidade", "Disponibilidade"
        };
    }

    // D25 — situação do relatório semestral (art. 32, § único)
    public static class StatusRelatorioSemestral
    {
        public const string Pendente = "Pendente";
        public const string EnviadoNoPrazo = "Enviado no prazo";
        public const string EnviadoEmAtraso = "Enviado em atraso";
        public const string Inadimplente = "Inadimplente";

        public static readonly string[] Todos = { Pendente, EnviadoNoPrazo, EnviadoEmAtraso, Inadimplente };
    }

    // D26 — tipo de auditoria técnica (arts. 25, § 2º e 34)
    public static class TipoAuditoria
    {
        public static readonly string[] Todos = { "Periódica", "Independente contratual" };
    }

    // ── Fase 5: transparência pública ──────────────────────────────────────────

    // D21 — tipo de pedido do cidadão (arts. 11, IV e 23)
    public static class TipoSolicitacao
    {
        public const string ReclamacaoDados = "Reclamação de titular de dados";

        public static readonly string[] Todos =
        {
            "Informação sobre uso de IA", "Revisão de decisão", "Explicação da decisão",
            "Impugnação por discriminação ou erro", ReclamacaoDados
        };
    }

    public static class StatusSolicitacao
    {
        public const string Recebida = "Recebida";
        public const string Respondida = "Respondida";
        public const string EncaminhadaDpo = "Encaminhada ao Encarregado de Dados";

        public static readonly string[] Todos =
        {
            Recebida, "Em análise", Respondida, EncaminhadaDpo
        };
    }
}
