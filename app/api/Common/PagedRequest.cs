namespace api.Common;

/// <summary>
/// DTO para requisições paginadas
/// </summary>
public class PagedRequest
{
    private const int MaxPageSize = 100;
    private int _pageSize = 20;

    /// <summary>
    /// Número da página (começa em 1)
    /// </summary>
    public int Page { get; set; } = 1;

    /// <summary>
    /// Quantidade de itens por página (máximo 100)
    /// </summary>
    public int PageSize
    {
        get => _pageSize;
        set => _pageSize = value > MaxPageSize ? MaxPageSize : value;
    }

    /// <summary>
    /// Campo para ordenação (opcional)
    /// </summary>
    public string? OrderBy { get; set; }

    /// <summary>
    /// Direção da ordenação (asc ou desc)
    /// </summary>
    public string OrderDirection { get; set; } = "asc";

    /// <summary>
    /// Calcula quantos itens pular (para Skip)
    /// </summary>
    public int Skip => (Page - 1) * PageSize;

    /// <summary>
    /// Valida se a ordenação é ascendente
    /// </summary>
    public bool IsAscending => OrderDirection.ToLower() != "desc";
}
