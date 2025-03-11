using Microsoft.Graph;
using Azure.Identity;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Graph.Models;
using DotNetEnv;

public class ProjetoService
{
    private readonly GraphServiceClient _graphClient;
    private readonly string _siteId;
    private readonly string _listId;

    public ProjetoService(IConfiguration config)
    {
        var tenantId = Environment.GetEnvironmentVariable("TenantId");
        var clientId = Environment.GetEnvironmentVariable("ClientId");
        var clientSecret = Environment.GetEnvironmentVariable("ClientSecret");

        var credential = new ClientSecretCredential(tenantId, clientId, clientSecret);
        _graphClient = new GraphServiceClient(credential, new[] { "https://graph.microsoft.com/.default" });

        _siteId = Environment.GetEnvironmentVariable("SiteId");
        _listId = Environment.GetEnvironmentVariable("ListProjetoId");
    }

    // Obter todos os projetos
    public async Task<List<IDictionary<string, object>>> GetProjetoListItemsAsync()
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
                .Where(item => item.Fields?.AdditionalData != null)
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
            Console.WriteLine($"Erro ao obter projetos: {ex.Message}");
            return new List<IDictionary<string, object>>();
        }
    }

    // Obter um projeto pelo ID
    public async Task<IDictionary<string, object>?> GetProjetoListItemByIdAsync(string itemId)
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
            Console.WriteLine($"Erro ao obter projeto {itemId}: {ex.Message}");
            return null;
        }
    }

    // Criar um novo projeto
    public async Task<bool> CreateProjetoAsync(Dictionary<string, object> fields)
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
            Console.WriteLine($"Erro ao criar projeto: {ex.Message}");
            return false;
        }
    }

    // Excluir um projeto pelo ID
    public async Task<bool> DeleteProjetoAsync(string itemId)
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
            Console.WriteLine($"Erro ao excluir projeto {itemId}: {ex.Message}");
            return false;
        }
    }
    public async Task<bool> UpdateProjetoAsync(string itemId, Dictionary<string, object> fields)
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
            Console.WriteLine($"Erro ao atualizar projeto {itemId}: {ex.Message}");
            return false;
        }
    }
}
