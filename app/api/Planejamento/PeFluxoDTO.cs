using System.Text.Json;
using service;

namespace api.Planejamento;

// ── Definição de um fluxo (a mesma forma no modelo e na cópia do órgão) ──────────

/// <summary>
/// A definição de um fluxo (contrato da E6, PascalCase): o prefixo da numeração, as raias, os
/// elementos e as ligações. O número das tarefas e dos subprocessos é calculado no servidor
/// (prefixo + a ordem no desenho: da esquerda para a direita, de cima para baixo); o que vier
/// em Numero é ignorado. Sem prefixo, nada é numerado (os macrofluxos do guia).
/// </summary>
public class PeFluxoDefinicao
{
    // "1", "4"; nulo = sem número
    public string? PrefixoNumeracao { get; set; }

    public List<PeFluxoRaia> Raias { get; set; } = new();

    public List<PeFluxoElemento> Elementos { get; set; } = new();

    public List<PeFluxoLigacao> Ligacoes { get; set; } = new();
}

public class PeFluxoRaia
{
    // Texto curto gerado pelo front, único no fluxo
    public string Id { get; set; } = string.Empty;

    // Texto ou marcador do dicionário de nomes ("{nomes.comite}")
    public string Nome { get; set; } = string.Empty;

    // 1, 2, 3 (de cima para baixo)
    public int Ordem { get; set; }
}

public class PeFluxoElemento
{
    public string Id { get; set; } = string.Empty;

    // inicio, fim, ligacao, tarefa, subprocesso, decisao ou paralelo
    public string Tipo { get; set; } = string.Empty;

    public string RaiaId { get; set; } = string.Empty;

    // Obrigatório na tarefa, no subprocesso e na ligação com outro fluxo; pode ter marcadores
    public string Nome { get; set; } = string.Empty;

    // Calculado no servidor (tarefa e subprocesso com prefixo); nulo nos outros
    public string? Numero { get; set; }

    // Documentos que a tarefa ou o subprocesso gera (só neles)
    public List<string> Artefatos { get; set; } = new();
}

public class PeFluxoLigacao
{
    public string Id { get; set; } = string.Empty;

    // Id do elemento de onde sai
    public string De { get; set; } = string.Empty;

    // Id do elemento para onde vai
    public string Para { get; set; } = string.Empty;

    // "Sim", "Não"; obrigatório nas saídas de uma decisão
    public string? Rotulo { get; set; }
}

// ── Respostas ────────────────────────────────────────────────────────────────

/// <summary>Um fluxo do guia como modelo (GET e PUT api/planejamento/modelo/fluxos).</summary>
public class PeFluxoModeloResponse
{
    public string Chave { get; set; } = string.Empty;

    public string Nome { get; set; } = string.Empty;

    public string? FiguraGuia { get; set; }

    public int Ordem { get; set; }

    public PeFluxoDefinicao Definicao { get; set; } = new();

    // A mais que o contrato: a última mudança do administrador (nulo se nunca mudou)
    public DateTime? AlteradoEm { get; set; }

    public string? AlteradoPor { get; set; }
    // F1 (C19): o nome da pessoa (o do cadastro do usuário; sem nome, o e-mail); nulo com o AlteradoPor
    public string? AlteradoPorNome { get; set; }
}

/// <summary>Um fluxo na lista do PDTIC (GET api/planejamento/pdtic/{id}/fluxos).</summary>
public class PeFluxoResumoResponse
{
    public string Chave { get; set; } = string.Empty;

    // O nome da cópia do órgão, ou o do modelo
    public string Nome { get; set; } = string.Empty;

    public string? FiguraGuia { get; set; }

    // O órgão tem cópia própria (mudou alguma coisa)
    public bool Personalizado { get; set; }

    // O órgão tem cópia e o modelo mudou depois que ele gravou
    public bool ModeloMudou { get; set; }

    // Da cópia do órgão (nulos sem cópia)
    public DateTime? AlteradoEm { get; set; }

    public string? AlteradoPor { get; set; }
    // F1 (C19): o nome da pessoa (o do cadastro do usuário; sem nome, o e-mail); nulo com o AlteradoPor
    public string? AlteradoPorNome { get; set; }

    // A mais que o contrato: a ordem do modelo
    public int Ordem { get; set; }
}

/// <summary>O fluxo do PDTIC (GET, PUT e POST .../restaurar de api/planejamento/pdtic/{id}/fluxos/{chave}).</summary>
public class PeFluxoResponse
{
    public string Chave { get; set; } = string.Empty;

    public string Nome { get; set; } = string.Empty;

    public string? FiguraGuia { get; set; }

    // A cópia do órgão ou, sem cópia, a do modelo (com os números calculados)
    public PeFluxoDefinicao Definicao { get; set; } = new();

    public bool Personalizado { get; set; }

    public bool ModeloMudou { get; set; }

    // A equipe do órgão com o PDTIC em elaboração ou devolvido
    public bool PodeEditar { get; set; }

    // A mais que o contrato: da cópia do órgão (nulos sem cópia)
    public DateTime? AlteradoEm { get; set; }

    public string? AlteradoPor { get; set; }
    // F1 (C19): o nome da pessoa (o do cadastro do usuário; sem nome, o e-mail); nulo com o AlteradoPor
    public string? AlteradoPorNome { get; set; }
}

/// <summary>
/// Um nome do dicionário que as raias e os passos podem usar (GET api/planejamento/fluxos/nomes,
/// a mais que o contrato): o marcador, o que ele é, o nome que aparece no desenho (o do
/// órgão ou, sem ele, o padrão) e o nome padrão.
/// </summary>
public class PeFluxoNomeResponse
{
    // "{nomes.comite}"
    public string Marcador { get; set; } = string.Empty;

    public string Descricao { get; set; } = string.Empty;

    public string Nome { get; set; } = string.Empty;

    public string Padrao { get; set; } = string.Empty;
}

/// <summary>
/// O resultado de conferir uma definição sem gravar (POST api/planejamento/fluxos/validacao, a
/// mais que o contrato): os erros em linguagem simples e, sem erros, a definição com os números.
/// </summary>
public class PeFluxoValidacaoResponse
{
    public bool Valida { get; set; }

    public List<string> Erros { get; set; } = new();

    public PeFluxoDefinicao? Definicao { get; set; }
}

// ── Corpos ───────────────────────────────────────────────────────────────────

/// <summary>{ Nome, Definicao } do PUT do modelo e da cópia do órgão.</summary>
public class PeFluxoSalvarDTO
{
    public string? Nome { get; set; }

    public JsonElement? Definicao { get; set; }

    /// <summary>Lê o corpo com mensagens em linguagem simples (a definição é conferida depois).</summary>
    public static PeFluxoSalvarDTO Ler(JsonElement corpo)
    {
        if (corpo.ValueKind != JsonValueKind.Object)
            throw new ApiException(ErrorCode.PeDadosInvalidos, "Envie o fluxo como um objeto JSON com Nome e Definicao.");
        PeCorpo.ConferirTexto(corpo);
        var dto = new PeFluxoSalvarDTO();
        foreach (var p in corpo.EnumerateObject())
        {
            if (string.Equals(p.Name, "Nome", StringComparison.OrdinalIgnoreCase))
            {
                if (p.Value.ValueKind is not (JsonValueKind.String or JsonValueKind.Null))
                    throw new ApiException(ErrorCode.PeDadosInvalidos, "O nome do fluxo é um texto.");
                dto.Nome = p.Value.ValueKind == JsonValueKind.String ? p.Value.GetString() : null;
            }
            else if (string.Equals(p.Name, "Definicao", StringComparison.OrdinalIgnoreCase))
            {
                dto.Definicao = p.Value.Clone();
            }
        }
        return dto;
    }
}

/// <summary>
/// { Definicao, PdticId, Nome } do POST api/planejamento/fluxos/desenho (e da validação e, desde
/// a E9, da geometria). O Nome é o título do desenho: com duas raias ou mais, ele abre a faixa do
/// fluxo à esquerda, como no SVG gravado e no PDF.
/// </summary>
public class PeFluxoDesenhoDTO
{
    public JsonElement? Definicao { get; set; }

    // Com o PDTIC, os nomes do dicionário do órgão; sem ele, os nomes padrão
    public long? PdticId { get; set; }

    // A mais que o contrato: o nome do fluxo (a faixa da esquerda quando há mais de uma raia)
    public string? Nome { get; set; }

    public static PeFluxoDesenhoDTO Ler(JsonElement corpo)
    {
        if (corpo.ValueKind != JsonValueKind.Object)
            throw new ApiException(ErrorCode.PeDadosInvalidos, "Envie a definição do fluxo como um objeto JSON (Definicao).");
        PeCorpo.ConferirTexto(corpo);
        var dto = new PeFluxoDesenhoDTO();
        foreach (var p in corpo.EnumerateObject())
        {
            if (string.Equals(p.Name, "Definicao", StringComparison.OrdinalIgnoreCase))
                dto.Definicao = p.Value.Clone();
            else if (string.Equals(p.Name, "PdticId", StringComparison.OrdinalIgnoreCase))
            {
                if (p.Value.ValueKind == JsonValueKind.Null) continue;
                if (p.Value.ValueKind != JsonValueKind.Number || !p.Value.TryGetInt64(out var id))
                    throw new ApiException(ErrorCode.PeDadosInvalidos, "PdticId é o número do PDTIC (ou nulo).");
                dto.PdticId = id;
            }
            else if (string.Equals(p.Name, "Nome", StringComparison.OrdinalIgnoreCase))
            {
                if (p.Value.ValueKind is not (JsonValueKind.String or JsonValueKind.Null))
                    throw new ApiException(ErrorCode.PeDadosInvalidos, "O nome do fluxo é um texto.");
                dto.Nome = p.Value.ValueKind == JsonValueKind.String ? p.Value.GetString() : null;
            }
        }
        return dto;
    }
}
