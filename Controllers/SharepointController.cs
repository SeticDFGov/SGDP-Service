using Microsoft.AspNetCore.Mvc;

[Route("api/sharepoint")]
[ApiController]
public class SharePointController : ControllerBase
{
    private readonly GraphService _graphService;

    public SharePointController(GraphService graphService)
    {
        _graphService = graphService;
    }

    [HttpGet("list")]
    public async Task<ActionResult<List<Dictionary<string, object>>>> GetSharePointListItems()
        {
            try
            {
                var listItems = await _graphService.GetSharePointListItemsAsync();
                if (listItems == null || listItems.Count == 0)
                {
                    return NotFound("Nenhum item encontrado.");
                }
                return Ok(listItems); // Retorna os itens encontrados
            }
            catch (System.Exception ex)
            {
                return StatusCode(500, $"Erro ao obter os itens do SharePoint: {ex.Message}");
            }
        }
}
