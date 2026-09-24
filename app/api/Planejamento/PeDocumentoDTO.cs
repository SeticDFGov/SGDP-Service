using System.Text.Json;
using service;

namespace api.Planejamento;

// ── Documento do PDTIC (E5) ──────────────────────────────────────────────────

/// <summary>
/// GET api/planejamento/pdtic/{id}/documento: a estrutura resolvida do documento do órgão,
/// com os capítulos numerados, os blocos com o texto efetivo (o do órgão ou o do modelo), os
/// marcadores resolvidos e os dados das tabelas e listas; e as versões geradas em PDF.
/// </summary>
public class PeDocumentoResponse
{
    public long PdticId { get; set; }

    // Versão do PDTIC ("1.0")
    public string Versao { get; set; } = string.Empty;

    public string OrgaoSigla { get; set; } = string.Empty;

    public string OrgaoNome { get; set; } = string.Empty;

    public string Titulo { get; set; } = string.Empty;

    // Quem chama edita os textos, esconde e renomeia capítulos e gera o PDF (equipe do órgão com o PDTIC aberto)
    public bool PodeEditar { get; set; }

    // A mais que o contrato: o endereço do logotipo do órgão (campo logotipo do dicionário de nomes), ou nulo
    public string? Logotipo { get; set; }

    // Na ordem do documento (o subcapítulo logo depois do capítulo dele)
    public List<PeDocCapituloResponse> Capitulos { get; set; } = new();

    // Da mais nova para a mais antiga
    public List<PeDocVersaoResponse> Versoes { get; set; } = new();
}

public class PeDocCapituloResponse
{
    public long Id { get; set; }

    public string Chave { get; set; } = string.Empty;

    // "6" ou "6.1" pela posição entre os visíveis; nulo no capítulo sem número e no oculto
    public string? Numero { get; set; }

    // 1 no capítulo, 2 no subcapítulo
    public int Nivel { get; set; }

    // O título efetivo (o próprio do órgão ou o do modelo)
    public string Titulo { get; set; } = string.Empty;

    public string TituloModelo { get; set; } = string.Empty;

    public string? TituloProprio { get; set; }

    // Oculto pelo órgão: vem só com o título, sem blocos (a prévia oferece "Mostrar de novo")
    public bool Oculto { get; set; }

    public bool Obrigatorio { get; set; }

    public bool Travado { get; set; }

    public string? IncisoDecreto { get; set; }

    public string? PassoChave { get; set; }

    // O número do passo na trilha do órgão ("2.3"), para o link "editar os dados no passo"
    public string? PassoNumero { get; set; }

    public List<PeDocBlocoResponse> Blocos { get; set; } = new();
}

public class PeDocBlocoResponse
{
    public long Id { get; set; }

    // texto, tabela_secao, lista_tema, matriz_swot, fluxo ou quebra_pagina
    public string Tipo { get; set; } = string.Empty;

    public int Ordem { get; set; }

    public bool PaginaDeitada { get; set; }

    // Bloco de texto: o JSON do TipTap com os marcadores escritos como texto ("{orgao.nome}")
    public JsonElement? TextoBruto { get; set; }

    // O mesmo com os marcadores trocados pelos valores (o marcador sem valor continua escrito)
    public JsonElement? TextoResolvido { get; set; }

    public bool EditadoPeloOrgao { get; set; }

    // O órgão editou e o texto do modelo mudou depois
    public bool ModeloMudou { get; set; }

    // Com ModeloMudou: o texto de hoje do modelo, com os marcadores resolvidos
    public JsonElement? TextoModeloAtual { get; set; }

    public DateTime? EditadoEm { get; set; }

    public string? EditadoPor { get; set; }

    // Chaves dos marcadores do texto sem valor para o órgão ("nomes.comite")
    public List<string> MarcadoresSemValor { get; set; } = new();

    public PeDocTabelaResponse? Tabela { get; set; }

    public PeDocListaResponse? Lista { get; set; }

    public PeDocSwotResponse? Swot { get; set; }

    public PeDocFluxoResponse? Fluxo { get; set; }
}

public class PeDocTabelaResponse
{
    public string SecaoChave { get; set; } = string.Empty;

    public string SecaoTitulo { get; set; } = string.Empty;

    // A mais que o contrato: formulario (um registro, desenhado como rótulo e valor) ou tabela
    public string SecaoTipo { get; set; } = string.Empty;

    // A mais que o contrato: o número do passo da seção na trilha do órgão ("2.4")
    public string? PassoNumero { get; set; }

    public List<PeDocColunaResponse> Colunas { get; set; } = new();

    public List<PeDocLinhaResponse> Linhas { get; set; } = new();

    public bool Vazia { get; set; }
}

public class PeDocColunaResponse
{
    public string Chave { get; set; } = string.Empty;

    public string Rotulo { get; set; } = string.Empty;
}

public class PeDocLinhaResponse
{
    public string? Codigo { get; set; }

    // Chave da coluna para o texto pronto ("-" quando vazio)
    public Dictionary<string, string> Celulas { get; set; } = new();

    // A mais que o contrato: nas colunas de texto formatado, o JSON do TipTap (nulo quando não há)
    public Dictionary<string, JsonElement>? Ricos { get; set; }
}

public class PeDocListaResponse
{
    // O rótulo do tema ("Segurança da informação e continuidade")
    public string Tema { get; set; } = string.Empty;

    public List<PeDocListaItemResponse> Itens { get; set; } = new();

    // A mais que o contrato: sem ação no tema, a justificativa do órgão (passo 3.4), ou nulo
    public string? Justificativa { get; set; }
}

public class PeDocListaItemResponse
{
    public string? Codigo { get; set; }

    public string Texto { get; set; } = string.Empty;

    // O rótulo da situação da ação, ou nulo
    public string? Situacao { get; set; }
}

public class PeDocSwotResponse
{
    public List<string> Forcas { get; set; } = new();

    public List<string> Fraquezas { get; set; } = new();

    public List<string> Oportunidades { get; set; } = new();

    public List<string> Ameacas { get; set; } = new();
}

public class PeDocFluxoResponse
{
    public string Chave { get; set; } = string.Empty;

    // O nome do fluxo com a figura do guia ("Preparação (figura 6 do guia)")
    public string Nome { get; set; } = string.Empty;

    // O desenho em SVG (desde a E6: a cópia do órgão ou o modelo, com os nomes do dicionário);
    // nulo quando o fluxo não existe (ou antes de a migration da E6 rodar)
    public string? Svg { get; set; }

    // A mais que o contrato (E6): o órgão adaptou o fluxo
    public bool Personalizado { get; set; }

    // A mais que o contrato (E6): o texto alternativo do desenho (as raias e os passos em lista numerada)
    public List<string> Descricao { get; set; } = new();
}

/// <summary>Uma versão gerada do documento (a lista de GET .../versoes e a resposta do POST .../pdf).</summary>
public class PeDocVersaoResponse
{
    public int Numero { get; set; }

    // minuta, enviada, aprovada ou publicada
    public string Situacao { get; set; } = string.Empty;

    public DateTime GeradoEm { get; set; }

    public string GeradoPor { get; set; } = string.Empty;

    // Bytes do PDF
    public long Tamanho { get; set; }

    public int Paginas { get; set; }
}

/// <summary>O PDF de uma versão para baixar.</summary>
public record PeDocArquivo(byte[] Conteudo, string NomeArquivo);

/// <summary>PUT pdtic/{id}/documento/capitulos/{capituloId}: { Oculto, TituloProprio } (campo ausente não muda).</summary>
public class PeDocCapituloOrgaoDTO : PeCorpoParcial
{
    public bool? Oculto { get; set; }

    // Nulo ou vazio volta ao título do modelo
    public string? TituloProprio { get; set; }
}

/// <summary>Leitura do corpo { "Texto": { JSON do TipTap } } do PUT de um bloco de texto.</summary>
public static class PeDocTextoDTO
{
    public static JsonElement Ler(JsonElement corpo)
    {
        if (corpo.ValueKind != JsonValueKind.Object)
            throw new ApiException(ErrorCode.PeDadosInvalidos, "Envie { \"Texto\": ... } com o texto do editor.");
        PeCorpo.ConferirTexto(corpo);
        foreach (var p in corpo.EnumerateObject())
        {
            if (!string.Equals(p.Name, "Texto", StringComparison.OrdinalIgnoreCase)) continue;
            if (p.Value.ValueKind != JsonValueKind.Object)
                throw new ApiException(ErrorCode.PeDocTextoInvalido, "O texto veio num formato que não serve. Atualize a tela e tente de novo.");
            return p.Value.Clone();
        }
        throw new ApiException(ErrorCode.PeDadosInvalidos, "Envie { \"Texto\": ... } com o texto do editor.");
    }
}

// ── Modelo do documento (administrador do módulo) ────────────────────────────

/// <summary>GET api/planejamento/modelo/documento?tipo=pdtic.</summary>
public class PeDocModeloResponse
{
    public long Id { get; set; }

    public string Tipo { get; set; } = string.Empty;

    public string Nome { get; set; } = string.Empty;

    // Na ordem do documento (o subcapítulo logo depois do capítulo dele)
    public List<PeDocModeloCapituloResponse> Capitulos { get; set; } = new();

    public List<PeDocMarcadorResponse> Marcadores { get; set; } = new();
}

public class PeDocModeloCapituloResponse
{
    public long Id { get; set; }

    public long? PaiId { get; set; }

    public string Chave { get; set; } = string.Empty;

    public string Titulo { get; set; } = string.Empty;

    public bool Numerado { get; set; }

    public int Ordem { get; set; }

    public bool Obrigatorio { get; set; }

    public bool Travado { get; set; }

    public string? IncisoDecreto { get; set; }

    public string? PassoChave { get; set; }

    public bool Sistema { get; set; }

    public List<PeDocModeloBlocoResponse> Blocos { get; set; } = new();
}

public class PeDocModeloBlocoResponse
{
    public long Id { get; set; }

    public int Ordem { get; set; }

    public string Tipo { get; set; } = string.Empty;

    public JsonElement Config { get; set; }
}

public class PeDocMarcadorResponse
{
    public string Chave { get; set; } = string.Empty;

    public string Descricao { get; set; } = string.Empty;

    public string Exemplo { get; set; } = string.Empty;
}

/// <summary>POST modelo/documento/capitulos.</summary>
public class PeDocCapituloCriarDTO
{
    // pdtic (padrão)
    public string? Tipo { get; set; }

    // O capítulo pai (subcapítulo); nulo = capítulo
    public long? PaiId { get; set; }

    // Opcional: sem ela o servidor gera a partir do título
    public string? Chave { get; set; }

    public string? Titulo { get; set; }

    public bool? Numerado { get; set; }

    public bool? Obrigatorio { get; set; }

    public string? PassoChave { get; set; }
}

/// <summary>PUT modelo/documento/capitulos/{id} (campo ausente não muda).</summary>
public class PeDocCapituloAtualizarDTO : PeCorpoParcial
{
    public string? Titulo { get; set; }

    public bool? Numerado { get; set; }

    public bool? Obrigatorio { get; set; }

    public string? PassoChave { get; set; }
}

/// <summary>POST modelo/documento/blocos.</summary>
public class PeDocBlocoCriarDTO
{
    public long? CapituloId { get; set; }

    public string? Tipo { get; set; }

    public JsonElement? Config { get; set; }
}

/// <summary>PUT modelo/documento/blocos/{id}: { Config } (o tipo não muda).</summary>
public class PeDocBlocoAtualizarDTO
{
    public JsonElement? Config { get; set; }
}
