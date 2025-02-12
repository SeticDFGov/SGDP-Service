using Microsoft.Graph;
using Azure.Identity;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Graph.Models;

public class GraphService
{
    private readonly GraphServiceClient _graphClient;
    private readonly string _siteId;
    private readonly string _listId;

    public GraphService(IConfiguration config)
    {
        var tenantId = config["AzureAd:TenantId"];
        var clientId = config["AzureAd:ClientId"];
        var clientSecret = config["AzureAd:ClientSecret"];

        var credential = new ClientSecretCredential(tenantId, clientId, clientSecret);
        _graphClient = new GraphServiceClient(credential, new[] { "https://graph.microsoft.com/.default" });

        _siteId = config["SharePoint:SiteId"];
        _listId = config["SharePoint:ListId"];
    }

    public async Task<List<IDictionary<string, object>>> GetSharePointListItemsAsync()
{
    try
    {
        var listItems = await _graphClient
            .Sites[_siteId]
            .Lists[_listId]
            .Items
            .GetAsync(requestConfiguration =>
            {
                requestConfiguration.QueryParameters.Expand = new[] { "fields" };
            });

        // Extrai apenas os campos (AdditionalData) de cada item
        var fieldsList = listItems.Value
            .Where(item => item.Fields?.AdditionalData != null)
            .Select(item => item.Fields.AdditionalData)
            .ToList();

        return fieldsList;
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Erro ao obter itens do SharePoint: {ex.Message}");
        return new List<IDictionary<string, object>>(); // Retorna uma lista vazia em caso de erro
    }
}

}
