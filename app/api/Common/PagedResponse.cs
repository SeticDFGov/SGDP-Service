namespace api.Common;

/// <summary>
/// DTO para respostas paginadas
/// </summary>
/// <typeparam name="T">Tipo dos itens da lista</typeparam>
public class PagedResponse<T>
{
    /// <summary>
    /// Lista de itens da página atual
    /// </summary>
    public List<T> Items { get; set; } = new();

    /// <summary>
    /// Número da página atual
    /// </summary>
    public int CurrentPage { get; set; }

    /// <summary>
    /// Quantidade de itens por página
    /// </summary>
    public int PageSize { get; set; }

    /// <summary>
    /// Total de itens (em todas as páginas)
    /// </summary>
    public int TotalItems { get; set; }

    /// <summary>
    /// Total de páginas
    /// </summary>
    public int TotalPages { get; set; }

    /// <summary>
    /// Indica se há página anterior
    /// </summary>
    public bool HasPreviousPage => CurrentPage > 1;

    /// <summary>
    /// Indica se há próxima página
    /// </summary>
    public bool HasNextPage => CurrentPage < TotalPages;

    /// <summary>
    /// Construtor vazio
    /// </summary>
    public PagedResponse() { }

    /// <summary>
    /// Construtor com dados
    /// </summary>
    public PagedResponse(List<T> items, int totalItems, int currentPage, int pageSize)
    {
        Items = items;
        TotalItems = totalItems;
        CurrentPage = currentPage;
        PageSize = pageSize;
        TotalPages = (int)Math.Ceiling(totalItems / (double)pageSize);
    }

    /// <summary>
    /// Cria resposta paginada a partir de uma lista completa
    /// </summary>
    public static PagedResponse<T> Create(List<T> items, int totalItems, PagedRequest request)
    {
        return new PagedResponse<T>(items, totalItems, request.Page, request.PageSize);
    }
}
