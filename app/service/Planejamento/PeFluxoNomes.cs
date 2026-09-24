using api.Planejamento;
using Models.Planejamento;

namespace service.Planejamento;

/// <summary>
/// Os nomes do dicionário nos fluxos (decisão 36): as raias e os passos usam os marcadores do
/// documento ("{nomes.comite}") e o desenho troca pelo nome do órgão (passo 1.2). Sem o nome do
/// órgão, e no modelo, vale o nome padrão, o do guia ("Comitê de Governança Digital"); o campo
/// que o administrador criar no dicionário tem o rótulo dele como padrão. Trocar o nome do
/// comitê no dicionário troca em todos os fluxos.
/// </summary>
public static class PeFluxoNomes
{
    public sealed record Nome(string Chave, string Descricao, string Padrao);

    /// <summary>
    /// Os nomes fixos, na ordem em que o editor mostra. A raia da autoridade máxima usa o cargo
    /// ({nomes.autoridade_cargo}): a raia é um papel, e {nomes.autoridade} é o nome da pessoa.
    /// </summary>
    public static readonly IReadOnlyList<Nome> Fixos = new[]
    {
        new Nome("nomes.comite", "Comitê interno de TIC (SGTIC)", "Comitê de Governança Digital"),
        new Nome("nomes.equipe", "Equipe de elaboração do PDTIC", "Equipe de Elaboração do PDTIC"),
        new Nome("nomes.equipe_acompanhamento", "Equipe de acompanhamento do PDTIC", "Equipe de Acompanhamento do PDTIC"),
        new Nome("nomes.autoridade_cargo", "Autoridade máxima do órgão (o cargo)", "Autoridade Máxima"),
        new Nome("nomes.autoridade", "Autoridade máxima do órgão (o nome da pessoa)", "Autoridade Máxima"),
        new Nome("nomes.unidade_tic", "Unidade de TIC", "Unidade de TIC"),
        new Nome("orgao.sigla", "Sigla do órgão", "Órgão"),
        new Nome("orgao.nome", "Nome do órgão", "Órgão")
    };

    // Campos do dicionário que já têm nome fixo com outra chave
    private static readonly HashSet<string> Cobertos = new(StringComparer.Ordinal)
    {
        PeDominios.DicionarioNomes.Equipe, PeDominios.DicionarioNomes.AutoridadeNome, PeDominios.DicionarioNomes.SiglaOrgao
    };

    /// <summary>
    /// Os nomes que o desenho usa, pela chave do marcador ("nomes.comite"): o do órgão (os
    /// marcadores do documento, <see cref="PeDocumentoService.Marcadores"/>) ou, sem ele, o
    /// padrão. Os campos de texto do dicionário que o administrador criou entram com o rótulo.
    /// </summary>
    public static Dictionary<string, string?> Mapa(IReadOnlyDictionary<string, string?>? doOrgao, IEnumerable<PeCampo>? camposDoDicionario)
    {
        var mapa = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var nome in Lista(camposDoDicionario))
        {
            var valor = doOrgao != null && doOrgao.TryGetValue(nome.Chave, out var v) && !string.IsNullOrWhiteSpace(v) ? v!.Trim() : null;
            mapa[nome.Chave] = valor ?? nome.Padrao;
        }
        return mapa;
    }

    /// <summary>A lista para o editor (GET api/planejamento/fluxos/nomes): o marcador, o que é, o nome no desenho e o padrão.</summary>
    public static List<PeFluxoNomeResponse> Resposta(IReadOnlyDictionary<string, string?> mapa, IEnumerable<PeCampo>? camposDoDicionario) =>
        Lista(camposDoDicionario)
            .Select(n => new PeFluxoNomeResponse
            {
                Marcador = "{" + n.Chave + "}",
                Descricao = n.Descricao,
                Nome = mapa.GetValueOrDefault(n.Chave) ?? n.Padrao,
                Padrao = n.Padrao
            })
            .ToList();

    private static List<Nome> Lista(IEnumerable<PeCampo>? camposDoDicionario)
    {
        var lista = Fixos.ToList();
        foreach (var campo in (camposDoDicionario ?? Enumerable.Empty<PeCampo>())
                     .Where(c => c.ExcluidoEm == null && PeDocMarcadores.EhDeTexto(c))
                     .OrderBy(c => c.Ordem).ThenBy(c => c.Id))
        {
            var chave = "nomes." + campo.Chave;
            if (Cobertos.Contains(campo.Chave) || lista.Any(n => n.Chave == chave)) continue;
            lista.Add(new Nome(chave, campo.Rotulo, campo.Rotulo));
        }
        return lista;
    }
}
