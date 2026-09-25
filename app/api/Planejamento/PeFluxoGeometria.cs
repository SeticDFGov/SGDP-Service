namespace api.Planejamento;

// ── Geometria do desenho automático de um fluxo (E9) ────────────────────────────

/// <summary>
/// A geometria do desenho automático de um fluxo (E9; contrato em PascalCase): as mesmas
/// medidas, em unidades do SVG da E6 (px, origem no canto de cima à esquerda, y para baixo),
/// que o servidor usa para desenhar o SVG da prévia e do PDF. O SVG é desenhado a partir desta
/// geometria (service.Planejamento.PeFluxoSvg): a tela do editor visual e o PDF não divergem.
/// <list type="bullet">
/// <item>Raias, Elementos, Ligacoes e Artefatos na ordem em que o SVG os desenha;</item>
/// <item>Erros: os mesmos textos da validação da E6 (vazio quando a definição é válida);</item>
/// <item>Numeros: o número de cada tarefa e subprocesso, pela ordem de leitura do desenho;</item>
/// <item>a mais que o contrato: Titulo, FaixaTitulo, Cor, Descricao e Textos (cada texto que o
/// SVG escreve, com a posição exata).</item>
/// </list>
/// </summary>
public class PeFluxoGeometria
{
    // O tamanho do desenho inteiro (o viewBox do SVG começa em 0 0)
    public double Largura { get; set; }

    public double Altura { get; set; }

    // De cima para baixo
    public List<PeFluxoGeometriaRaia> Raias { get; set; } = new();

    // Na ordem da definição
    public List<PeFluxoGeometriaElemento> Elementos { get; set; } = new();

    // Só as ligações entre elementos que existem (a ligação para um elemento que não existe fica
    // de fora do desenho e aparece em Erros): primeiro as de avanço, da esquerda para a direita,
    // depois os retornos
    public List<PeFluxoGeometriaLigacao> Ligacoes { get; set; } = new();

    // Os documentos que as tarefas e os subprocessos geram, embaixo deles
    public List<PeFluxoGeometriaArtefato> Artefatos { get; set; } = new();

    // Os problemas da definição, em linguagem simples (os mesmos textos da validação da E6)
    public List<string> Erros { get; set; } = new();

    // Id do elemento: número calculado ("1.3"); só tarefas e subprocessos de fluxo com prefixo
    public Dictionary<string, string> Numeros { get; set; } = new();

    // A mais que o contrato: o nome do fluxo como aparece no desenho (com os nomes do dicionário)
    public string Titulo { get; set; } = string.Empty;

    // A mais que o contrato: a faixa do fluxo à esquerda das raias (o "pool" do guia), só com
    // duas raias ou mais e com o nome do fluxo; nula nos outros casos
    public PeFluxoGeometriaFaixa? FaixaTitulo { get; set; }

    // A mais que o contrato: as cores das tarefas e dos subprocessos (pela etapa do prefixo)
    public PeFluxoGeometriaCor Cor { get; set; } = new();

    // A mais que o contrato: a descrição em lista numerada (o texto alternativo do desenho)
    public List<string> Descricao { get; set; } = new();

    // A mais que o contrato: cada linha de texto que o SVG escreve, na ordem, com a posição
    public List<PeFluxoGeometriaTexto> Textos { get; set; } = new();
}

/// <summary>Uma raia: a faixa horizontal, com o cabeçalho (o nome na vertical) à esquerda.</summary>
public class PeFluxoGeometriaRaia
{
    public string Id { get; set; } = string.Empty;

    // O nome como aparece no desenho (marcador trocado pelo nome do dicionário)
    public string Nome { get; set; } = string.Empty;

    // O nome como está na definição ("{nomes.comite}")
    public string NomeOriginal { get; set; } = string.Empty;

    // O topo e a altura da raia
    public double Y { get; set; }

    public double Altura { get; set; }

    // A largura do cabeçalho (a faixa com o nome, à esquerda)
    public double LarguraCabecalho { get; set; }

    // A mais que o contrato: onde a raia começa (depois da faixa do fluxo) e a largura dela,
    // do começo do cabeçalho até a borda da direita
    public double X { get; set; }

    public double Largura { get; set; }

    // A mais que o contrato: o nome quebrado nas linhas do cabeçalho (na vertical, de baixo
    // para cima; até três)
    public List<string> Linhas { get; set; } = new();
}

/// <summary>Um elemento do fluxo: a caixa que a forma ocupa.</summary>
public class PeFluxoGeometriaElemento
{
    public string Id { get; set; } = string.Empty;

    // inicio, fim, ligacao, tarefa, subprocesso, decisao ou paralelo
    public string Tipo { get; set; } = string.Empty;

    // A raia em que o elemento foi desenhado (sem raia que exista, a primeira)
    public string RaiaId { get; set; } = string.Empty;

    // A coluna, da esquerda para a direita, a partir de 0
    public int Camada { get; set; }

    // A caixa da forma: o retângulo da tarefa; o quadrado em volta do losango (40 x 40) e do
    // círculo (30 x 30)
    public double X { get; set; }

    public double Y { get; set; }

    public double Largura { get; set; }

    public double Altura { get; set; }

    // Só tarefa e subprocesso de fluxo com prefixo
    public string? Numero { get; set; }

    // O nome como aparece no desenho (marcadores trocados)
    public string Nome { get; set; } = string.Empty;

    // O nome quebrado em linhas como no SVG (sem o número, que vem em Numero): dentro da
    // tarefa, até sete linhas (a última com reticências quando não cabe); fora do losango e
    // embaixo do círculo, até quatro
    public List<string> Linhas { get; set; } = new();

    // Sem nenhuma ligação (de entrada ou de saída): desenhado no fim da raia dele
    public bool Solto { get; set; }

    // A mais que o contrato: a linha dentro da raia (0 é a principal; os caminhos que se
    // abrem ficam empilhados)
    public int Faixa { get; set; }

    // A mais que o contrato: onde fica o nome: "dentro" (tarefa e subprocesso), "acima",
    // "direita" ou "abaixo" (losango), "abaixo" (círculo); nulo sem nome
    public string? PosicaoNome { get; set; }
}

/// <summary>Uma ligação desenhada: a linha ortogonal, com a seta no último ponto.</summary>
public class PeFluxoGeometriaLigacao
{
    public string Id { get; set; } = string.Empty;

    public string De { get; set; } = string.Empty;

    public string Para { get; set; } = string.Empty;

    // O rótulo da saída da decisão ("Sim"), ou nulo
    public string? Rotulo { get; set; }

    // Volta para um passo anterior (passa por baixo do conteúdo da raia)
    public bool Retorno { get; set; }

    // Do ponto de saída (na borda do elemento De) ao ponto de chegada (na borda do elemento
    // Para, onde fica a ponta da seta); segmentos só na horizontal ou na vertical
    public List<PeFluxoGeometriaPonto> Pontos { get; set; } = new();

    // Onde o rótulo é escrito: o começo do texto (à esquerda) e a linha de base, como o x e o
    // y do elemento text do SVG; nulos sem rótulo
    public double? RotuloX { get; set; }

    public double? RotuloY { get; set; }

    // A mais que o contrato: o retângulo branco atrás do rótulo; nulo sem rótulo
    public PeFluxoGeometriaArea? RotuloArea { get; set; }
}

/// <summary>Um artefato (documento que a tarefa gera): o ícone do documento, embaixo da tarefa.</summary>
public class PeFluxoGeometriaArtefato
{
    public string ElementoId { get; set; } = string.Empty;

    // A posição na lista de artefatos do elemento, a partir de 0
    public int Indice { get; set; }

    public string Nome { get; set; } = string.Empty;

    // O ícone do documento (com a ponta dobrada no canto de cima à direita)
    public double X { get; set; }

    public double Y { get; set; }

    public double Largura { get; set; }

    public double Altura { get; set; }

    // A mais que o contrato: o nome quebrado em linhas, centradas embaixo do ícone
    public List<string> Linhas { get; set; } = new();

    // A mais que o contrato: a área do nome embaixo do ícone
    public PeFluxoGeometriaArea Rotulo { get; set; } = new();

    // A mais que o contrato: a linha pontilhada da tarefa ao ícone (a ponta aberta no último ponto)
    public List<PeFluxoGeometriaPonto> Conector { get; set; } = new();
}

/// <summary>Um ponto do desenho.</summary>
public class PeFluxoGeometriaPonto
{
    public double X { get; set; }

    public double Y { get; set; }
}

/// <summary>Um retângulo do desenho (canto de cima à esquerda, largura e altura).</summary>
public class PeFluxoGeometriaArea
{
    public double X { get; set; }

    public double Y { get; set; }

    public double Largura { get; set; }

    public double Altura { get; set; }
}

/// <summary>A faixa do fluxo à esquerda das raias, com o nome do fluxo na vertical.</summary>
public class PeFluxoGeometriaFaixa
{
    public double X { get; set; }

    public double Y { get; set; }

    public double Largura { get; set; }

    public double Altura { get; set; }

    public List<string> Linhas { get; set; } = new();
}

/// <summary>As cores das tarefas e dos subprocessos (fundo e borda).</summary>
public class PeFluxoGeometriaCor
{
    public string Fundo { get; set; } = string.Empty;

    public string Borda { get; set; } = string.Empty;
}

/// <summary>
/// Uma linha de texto do desenho, como o SVG a escreve: X é o começo da linha (à esquerda) e Y
/// a linha de base. Na vertical (nomes das raias e do fluxo, de baixo para cima), X é a linha de
/// base e Y o pé do texto. A largura foi medida com a fonte do desenho (Lato).
/// </summary>
public class PeFluxoGeometriaTexto
{
    public string Texto { get; set; } = string.Empty;

    public double X { get; set; }

    public double Y { get; set; }

    public double Tamanho { get; set; }

    public bool Negrito { get; set; }

    public bool Vertical { get; set; }

    public string Cor { get; set; } = string.Empty;

    public double Largura { get; set; }

    // De quem é o texto: o id do elemento, "raia:ID", "pool" (a faixa do fluxo),
    // "artefato:ID:n" (o nome do artefato n do elemento) ou "ligacao:ID" (o rótulo)
    public string Dono { get; set; } = string.Empty;
}
