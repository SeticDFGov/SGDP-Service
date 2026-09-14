using Xunit;

namespace test.contratacoes;

/// <summary>
/// Serializa as classes que geram despacho em PDF.
///
/// Motivo (defeito observado): o QuestPDF/SkiaSharp mantém estado de processo
/// (licença, cache de fontes e o subconjunto de glifos que acaba embutido no
/// arquivo). Com duas classes gerando PDF em PARALELO — o padrão do xUnit —, os
/// testes que comparam o TAMANHO de dois PDFs para provar que um campo NÃO é
/// impresso (a Observação, e agora o Status no TCDF) falhavam de forma
/// intermitente: o mesmo conteúdo saía com alguns bytes de diferença. Rodando as
/// duas classes na MESMA coleção, o xUnit não as executa ao mesmo tempo e a
/// comparação volta a ser determinística.
/// </summary>
[CollectionDefinition(Nome, DisableParallelization = true)]
public class CtrDespachoPdfCollection
{
    public const string Nome = "Despacho em PDF";
}
