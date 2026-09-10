namespace service.Contratacoes;

/// <summary>
/// Textos-base do despacho padrão da SGDI ao TCDF (supervisão contínua do
/// Decreto nº 48.899/2026, regulamentado pela IN SGDI nº 1/2026), NA LITERALIDADE
/// do documento oficial. Ficam em constantes para o teste conferir palavra por
/// palavra e para o PDF e o formulário do front usarem a mesma fonte.
///
/// Os colchetes do papel ([data], [Alta/Média/Baixa], [__]) são substituídos pelos
/// helpers abaixo na hora de gerar o despacho.
/// </summary>
public static class CtrDespachoTextos
{
    public const string Orgao = "SECRETARIA DE ESTADO DE GOVERNANÇA DIGITAL E INTEGRAÇÃO – SGDI";

    public const string Titulo = "DESPACHO";

    public const string Abertura =
        "Em atenção à comunicação do Tribunal de Contas do Distrito Federal – TCDF acerca da contratação "
        + "de TIC acima identificada, manifesta-se esta SGDI, conforme abaixo, no âmbito do regime de "
        + "supervisão contínua instituído pelo Decreto nº 48.899/2026 e regulamentado pela Instrução "
        + "Normativa SGDI nº 1/2026.";

    public const string AlcanceSupervisao =
        "A supervisão contínua da SGDI compreende a classificação da contratação por criticidade "
        + "(art. 11 da IN), a análise técnica de alinhamento estratégico, arquitetura, interoperabilidade "
        + "e segurança da informação (art. 25) e, quando cabível, a emissão de recomendação técnica ou a "
        + "adoção de medida cautelar (arts. 30 a 34).";

    public const string TituloSituacao = "Situação no Portfólio Estratégico de Contratações de TIC";

    public const string IncisoI =
        "A contratação já se encontrava comunicada a esta SGDI e inserida no monitoramento contínuo desde "
        + "[data]. Foi, por sua vez, classificada como de criticidade [Alta/Média/Baixa] (art. 11 da IN SGDI "
        + "nº 1/2026) e apresentou como resultado da análise e providência adotada:";

    public const string ResultadoAlinhada =
        "a contratação está alinhada às diretrizes da IN e segue em acompanhamento contínuo, sem "
        + "necessidade de ação adicional (art. 27, I, e art. 28 da IN);";

    public const string ResultadoInformacoesComplementares =
        "foram identificadas oportunidades de melhoria ou pontos a esclarecer, desse modo, foi(foram) "
        + "solicitada(s) informação(ões) complementar(es) ao órgão/entidade, nos termos do art. 29 da IN;";

    public const string ResultadoRiscosSignificativos =
        "foram identificados riscos significativos ou desvios relevantes, conforme os critérios dos "
        + "arts. 11 e 25 da IN, tendo a SGDI:";

    public const string RiscoRecomendouSuspensao =
        "recomendado a suspensão temporária da contratação para reanálise (art. 33 da IN);";

    public const string RiscoComunicouControleInterno =
        "comunicado o fato ao órgão de controle interno competente, mediante nota de motivação "
        + "(art. 34, VI, da IN c/c art. 6º, IV, do Decreto nº 48.899/2026);";

    public const string RiscoAguardandoResposta =
        "Aguardando resposta da área demandante sobre os riscos e desvios identificados;";

    public const string RiscoResolvido =
        "Risco resolvido — após recomendações da SGDI e ações de mitigação adotadas pela área demandante, "
        + "os riscos foram adequadamente tratados, prosseguindo a contratação em acompanhamento contínuo;";

    public const string RiscoNaoPodeProsseguir =
        "Não pode prosseguir — os riscos apontados não foram adequadamente tratados, tendo a SGDI "
        + "determinado a suspensão completa da contratação, com prazo para nova manifestação do "
        + "órgão/entidade, permanecendo o feito em acompanhamento contínuo.";

    public const string IncisoII =
        "A contratação não constava como previamente comunicada a esta SGDI. Diante da comunicação do TCDF, "
        + "o órgão/a entidade demandante será notificado para regularizar a comunicação obrigatória "
        + "(Anexo I/II da IN SGDI nº 1/2026), no prazo de [__] dias (art. 40 da IN).";

    public const string Ressalva =
        "Cabe ressaltar que em todas as hipóteses supra descritas, a contratação é inserida e mantida no "
        + "portfólio estratégico de contratações de TIC, sob regime de supervisão contínua da SGDI, com "
        + "acompanhamento a prosseguir conforme o art. 34 da IN SGDI nº 1/2026.";

    public const string NaturezaTecnica =
        "Reitera-se que a supervisão exercida por esta SGDI tem natureza técnica e orientadora, não "
        + "implicando aprovação, validação ou substituição das competências dos órgãos de controle interno "
        + "e externo (art. 9º e art. 25, §2º, da IN SGDI nº 1/2026), permanecendo a SGDI à disposição para "
        + "esclarecimentos adicionais.";

    public const string Fecho = "À consideração superior.";

    /// <summary>Inciso I com o "[data]" e o "[Alta/Média/Baixa]" preenchidos.</summary>
    public static string IncisoIPreenchido(DateOnly? comunicadaDesde, string? criticidade) =>
        IncisoI
            .Replace("[data]", comunicadaDesde?.ToString("dd/MM/yyyy") ?? "[data]")
            .Replace("[Alta/Média/Baixa]", string.IsNullOrWhiteSpace(criticidade) ? "[Alta/Média/Baixa]" : criticidade);

    /// <summary>Inciso II com o prazo em dias preenchido.</summary>
    public static string IncisoIIPreenchido(int? prazoDias) =>
        IncisoII.Replace("[__]", prazoDias?.ToString() ?? "[__]");

    /// <summary>Caixa de opção do papel: marcada ou em branco.</summary>
    public static string Caixa(bool marcada) => marcada ? "( X )" : "(   )";
}
