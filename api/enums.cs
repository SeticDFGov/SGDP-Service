using System.ComponentModel;

public enum ErrorCode
{
    Unknow,
    [Description("Erro ao buscar demandas")]
    ErroAoBuscarDemandas,
    [Description("Demandas não encontradas")]
    DemandasNaoEncontradas,
    [Description("Erro ao criar demanda")]
    ErroAoCriarDemanda,
    [Description("Erro ao editar demanda")]
    ErroAoEditarDemanda,
    [Description("Erro ao excluir demanda")]
    ErroAoExcluirDemanda,
    [Description("Categorias não encontradas")]
    CategoriasNaoEncontradas,
    [Description("Categoria não encontrada")]
    CategoriaNaoEncontrada,
    [Description("Erro ao criar categoria")]
    ErroAoCriarCategoria,
    [Description("Erro ao excluir categoria")]

    ErroAoExcluirCategoria,
    [Description("Erro ao buscar categorias")]
    ErroAoBuscarCategorias,
    [Description("Áreas demandantes não encontradas")]
    AreasDemandantesNaoEncontradas,
    [Description("Erro ao criar área demandante")]
    ErroAoCriarAreaDemandante,
    [Description("Erro ao excluir área demandante")]
    ErroAoExcluirAreaDemandante,
    [Description("Erro ao buscar áreas demandantes")]
    ErroAoBuscarAreasDemandantes,
    [Description("Detalhamento não encontrado")]
    DetalhamentoNaoEncontrado,
    [Description("Erro ao buscar detalhamentos")]
    ErroAoBuscarDetalhamentos,
    [Description("Erro ao criar detalhamento")]
    ErroAoCriarDetalhamento,
}
