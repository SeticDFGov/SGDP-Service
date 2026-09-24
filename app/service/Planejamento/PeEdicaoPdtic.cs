using System.Globalization;
using api.Planejamento;
using Models.Planejamento;

namespace service.Planejamento;

/// <summary>
/// Quem edita o quê no PDTIC, pela situação e pela etapa (E7; substitui o "em elaboração ou
/// devolvido" da E4). Vale para os registros, o "não se aplica", o documento e os fluxos, e só
/// para quem tem o papel de editar (a equipe do órgão e o admin geral; ver
/// IPePermissionService.PodeEditarPdtic):
/// <list type="bullet">
/// <item>etapas 1 a 3, menos a deliberação (3.12) e a publicação (3.13): em elaboração ou
/// devolvido (o documento e os fluxos também);</item>
/// <item>a publicação (3.13): aprovado ou publicado;</item>
/// <item>etapas 4 a 7: publicado ou em acompanhamento (a rodada B acrescenta o ciclo aberto
/// nas seções por ciclo);</item>
/// <item>a deliberação (3.12) não tem o que editar: quem registra é a Secretaria do CGTIC;</item>
/// <item>em aprovação, encerrado e substituído: nada se edita;</item>
/// <item>PDTIC registrado fora do sistema: as etapas 1 a 3 são "externas" (feitas fora), menos
/// as metas e ações (3.3) e os riscos (3.9), que a equipe preenche para acompanhar, depois da
/// publicação.</item>
/// </list>
/// Os passos são achados pelo tipo (deliberação e publicação) e pela chave da etapa
/// (<see cref="PeDominios.EtapaPdtic"/>), porque a numeração muda com o nível do órgão.
/// </summary>
public static class PeEdicaoPdtic
{
    /// <summary>O grupo do passo para a edição e para a situação.</summary>
    public enum Grupo
    {
        // Etapas 1 a 3 (menos a deliberação e a publicação)
        Elaboracao,
        // A deliberação do CGTIC (3.12)
        Deliberacao,
        // A publicação (3.13)
        Publicacao,
        // Etapas 4 a 7
        Acompanhamento
    }

    public static Grupo GrupoDe(string? etapaChave, string tipoPasso) => tipoPasso switch
    {
        PeDominios.TipoPasso.Deliberacao => Grupo.Deliberacao,
        PeDominios.TipoPasso.Publicacao => Grupo.Publicacao,
        _ => PeDominios.EtapaPdtic.EhDoAcompanhamento(etapaChave) ? Grupo.Acompanhamento : Grupo.Elaboracao
    };

    /// <summary>A etapa (chave) de cada passo visível da trilha, pelo id do passo.</summary>
    public static Dictionary<long, string> EtapasDosPassos(PeTrilhaOrgao trilha) =>
        trilha.Etapas.SelectMany(e => e.Passos.Select(p => (Passo: p.Id, Etapa: e.Chave))).ToDictionary(x => x.Passo, x => x.Etapa);

    /// <summary>O grupo de um passo da trilha do órgão.</summary>
    public static Grupo GrupoDoPasso(PeTrilhaOrgao trilha, PeTrilhaPasso passo) =>
        GrupoDe(trilha.Etapas.FirstOrDefault(e => e.Passos.Any(p => p.Id == passo.Id))?.Chave, passo.Tipo);

    /// <summary>Passo que o PDTIC registrado fora do sistema preenche para acompanhar (3.3 e 3.9).</summary>
    public static bool PreenchidoNoExterno(string passoChave) =>
        passoChave is PeDominios.ChavePdtic.PassoMetasAcoes or PeDominios.ChavePdtic.PassoRiscos;

    /// <summary>Passo feito fora do sistema: no PDTIC registrado externamente, as etapas 1 a 3, menos 3.3 e 3.9.</summary>
    public static bool Externo(PePdtic pdtic, Grupo grupo, string passoChave) =>
        pdtic.RegistradoExternamente && grupo != Grupo.Acompanhamento && !PreenchidoNoExterno(passoChave);

    /// <summary>A elaboração (etapas 1 a 3, o documento e os fluxos) está aberta.</summary>
    public static bool ElaboracaoAberta(PePdtic pdtic) =>
        !pdtic.RegistradoExternamente && PeDominios.SituacaoPdtic.Editaveis.Contains(pdtic.Situacao);

    /// <summary>
    /// Por que o passo não aceita edição agora (a mensagem do 409 PePdticFechado), ou nulo
    /// quando aceita. Não confere o papel de quem chama.
    /// </summary>
    public static string? Recusa(PePdtic pdtic, Grupo grupo, string passoChave)
    {
        var situacao = pdtic.Situacao;
        switch (situacao)
        {
            case PeDominios.SituacaoPdtic.EmAprovacao:
                return "Este PDTIC foi enviado ao CGTIC e não muda até a decisão.";
            case PeDominios.SituacaoPdtic.Encerrado:
                return "Este PDTIC foi encerrado e não muda mais.";
            case PeDominios.SituacaoPdtic.Substituido:
                return "Esta versão do PDTIC foi substituída pela revisão aprovada e não muda mais.";
        }
        if (Externo(pdtic, grupo, passoChave))
            return "Este passo foi feito fora do sistema: o PDTIC foi aprovado fora dele, e aqui a equipe só acompanha.";

        var vigente = PeDominios.SituacaoPdtic.Vigentes.Contains(situacao);
        return grupo switch
        {
            Grupo.Deliberacao => "A deliberação do CGTIC é registrada pela Secretaria Executiva do comitê.",
            Grupo.Publicacao => situacao is PeDominios.SituacaoPdtic.Aprovado or PeDominios.SituacaoPdtic.Publicado
                ? null
                : situacao == PeDominios.SituacaoPdtic.EmAcompanhamento
                    ? "A publicação não muda depois que o acompanhamento começou."
                    : "A publicação é registrada depois da aprovação do CGTIC.",
            Grupo.Acompanhamento => vigente ? null : "Este passo fica disponível depois da publicação do PDTIC.",
            _ when pdtic.RegistradoExternamente => vigente ? null : "Este passo fica disponível depois da publicação do PDTIC.",
            _ => PeDominios.SituacaoPdtic.Editaveis.Contains(situacao)
                ? null
                : "As etapas 1 a 3 não mudam depois do envio ao CGTIC. Para mudar o PDTIC aprovado, abra uma revisão."
        };
    }

    /// <summary>O passo aceita edição agora (sem conferir o papel): a deliberação nunca.</summary>
    public static bool Editavel(PePdtic pdtic, Grupo grupo, string passoChave) =>
        grupo != Grupo.Deliberacao && Recusa(pdtic, grupo, passoChave) == null;

    /// <summary>409 PePdticFechado com a mensagem da recusa.</summary>
    public static ApiException Fechado(string mensagem) => new(ErrorCode.PePdticFechado, mensagem);

    /// <summary>A elaboração fechada (documento, fluxos e as etapas 1 a 3): 409 com a mensagem da situação.</summary>
    public static ApiException ElaboracaoFechada(PePdtic pdtic) =>
        Fechado(Recusa(pdtic, Grupo.Elaboracao, string.Empty) ?? "Este PDTIC não muda na situação em que está.");

    // ── Versões ─────────────────────────────────────────────────────────────

    /// <summary>O número principal e o da revisão de uma versão ("1.2" = 1 e 2; fora do formato, 0 e 0).</summary>
    public static (int Principal, int Revisao) Partes(string? versao)
    {
        var partes = (versao ?? string.Empty).Split('.');
        int Numero(int i) => partes.Length > i && int.TryParse(partes[i], NumberStyles.None, CultureInfo.InvariantCulture, out var n) ? n : 0;
        return (Numero(0), Numero(1));
    }

    /// <summary>A versão da revisão: o mesmo número principal e a maior revisão dele mais um ("1.0" e "1.1" dão "1.2").</summary>
    public static string ProximaRevisao(string atual, IEnumerable<string> versoesDoOrgao)
    {
        var principal = Partes(atual).Principal;
        var maior = versoesDoOrgao.Select(Partes).Where(p => p.Principal == principal).Select(p => p.Revisao).DefaultIfEmpty(0).Max();
        return $"{principal.ToString(CultureInfo.InvariantCulture)}.{(maior + 1).ToString(CultureInfo.InvariantCulture)}";
    }

    /// <summary>
    /// A versão é uma revisão (aberta a partir da vigente, "1.1"): tem anterior, não foi
    /// registrada fora do sistema e o número da revisão não é zero (um ciclo novo é "2.0").
    /// </summary>
    public static bool EhRevisao(PePdtic pdtic) =>
        pdtic.AnteriorId != null && !pdtic.RegistradoExternamente && Partes(pdtic.Versao).Revisao > 0;
}
