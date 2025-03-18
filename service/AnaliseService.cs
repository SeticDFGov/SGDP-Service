using Microsoft.Graph;
using Azure.Identity;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Graph.Models;
using DotNetEnv;

public class AnaliseService
{
    private readonly GraphServiceClient _graphClient;
    private readonly string _siteId;
    private readonly string _listId;

    public AnaliseService(IConfiguration config)
    {
        var tenantId = Environment.GetEnvironmentVariable("TenantId");
        var clientId = Environment.GetEnvironmentVariable("ClientId");
        var clientSecret = Environment.GetEnvironmentVariable("ClientSecret");

        var credential = new ClientSecretCredential(tenantId, clientId, clientSecret);
        _graphClient = new GraphServiceClient(credential, new[] { "https://graph.microsoft.com/.default" });

        _siteId = Environment.GetEnvironmentVariable("SiteId");
        _listId = Environment.GetEnvironmentVariable("ListAnaliseId");
    }

    // Obter todas as análises
    public async Task<List<IDictionary<string, object>>> GetAnaliseListItemsAsync(string nomeProjeto)
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

            return listItems.Value
                .Where(item => item.Fields?.AdditionalData != null &&
                               item.Fields.AdditionalData.ContainsKey("NM_PROJETO") &&
                               item.Fields.AdditionalData["NM_PROJETO"]?.ToString() == nomeProjeto)
                .Select(item =>
                {
                    var itemData = new Dictionary<string, object>(item.Fields.AdditionalData) as IDictionary<string, object>;
                    itemData["ID"] = item.Id;
                    return itemData;
                })
                .ToList();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erro ao obter análises para o projeto {nomeProjeto}: {ex.Message}");
            return new List<IDictionary<string, object>>();
        }
    }

    // Obter uma análise pelo ID
    public async Task<IDictionary<string, object>?> GetAnaliseListItemByIdAsync(string itemId)
    {
        try
        {
            var item = await _graphClient
                .Sites[_siteId]
                .Lists[_listId]
                .Items[itemId]
                .GetAsync(requestConfiguration =>
                {
                    requestConfiguration.QueryParameters.Expand = new[] { "fields" };
                });

            return item.Fields?.AdditionalData;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erro ao obter análise {itemId}: {ex.Message}");
            return null;
        }
    }

    // Criar uma nova análise
    public async Task<bool> CreateAnaliseAsync(Dictionary<string, object> fields)
    {
        try
        {
            var newItem = new ListItem
            {
                Fields = new FieldValueSet
                {
                    AdditionalData = fields
                }
            };

            await _graphClient
                .Sites[_siteId]
                .Lists[_listId]
                .Items
                .PostAsync(newItem);

            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erro ao criar análise: {ex.Message}");
            return false;
        }
    }

    // Excluir uma análise pelo ID
    public async Task<bool> DeleteAnaliseAsync(string itemId)
    {
        try
        {
            await _graphClient
                .Sites[_siteId]
                .Lists[_listId]
                .Items[itemId]
                .DeleteAsync();

            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erro ao excluir análise {itemId}: {ex.Message}");
            return false;
        }
    }

    // Atualizar uma análise pelo ID
    public async Task<bool> UpdateAnaliseAsync(string itemId, Dictionary<string, object> fields)
    {
        try
        {
            var updatedItem = new FieldValueSet
            {
                AdditionalData = fields
            };

            await _graphClient
                .Sites[_siteId]
                .Lists[_listId]
                .Items[itemId]
                .Fields
                .PatchAsync(updatedItem);

            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erro ao atualizar análise {itemId}: {ex.Message}");
            return false;
        }
    }
}
