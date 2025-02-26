using Microsoft.Graph;
using Azure.Identity;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Graph.Models;
using DotNetEnv;

public class DemandanteService
{
    private readonly GraphServiceClient _graphClient;
    private readonly string _siteId;
    private readonly string _listId;

    public DemandanteService(IConfiguration config)
    {
        var tenantId = Environment.GetEnvironmentVariable("TenantId");
        var clientId = Environment.GetEnvironmentVariable("ClientId");
        var clientSecret = Environment.GetEnvironmentVariable("ClientSecret");

        var credential = new ClientSecretCredential(tenantId, clientId, clientSecret);
        _graphClient = new GraphServiceClient(credential, new[] { "https://graph.microsoft.com/.default" });

        _siteId = Environment.GetEnvironmentVariable("SiteId");
        _listId = Environment.GetEnvironmentVariable("ListDemandanteId");
    }

    // 1. Obter todos os itens da lista
    public async Task<List<IDictionary<string, object>>> GetDemandanteListItemsAsync()
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
                    // Clona os dados existentes como IDictionary<string, object>
                    var itemData = new Dictionary<string, object>(item.Fields.AdditionalData) as IDictionary<string, object>;
                    
                    // Adiciona o ID do item no dicionário
                    itemData["ID"] = item.Id;

                    return itemData;
                })
                .ToList();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erro ao obter itens do SharePoint: {ex.Message}");
            return new List<IDictionary<string, object>>();
        }
    }


    // 2. Obter um único item pelo ID
    public async Task<IDictionary<string, object>?> GetDemandateListItemByIdAsync(string itemId)
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
            Console.WriteLine($"Erro ao obter item {itemId}: {ex.Message}");
            return null;
        }
    }

    // 3. Criar um novo item na lista
    public async Task<bool> CreateDemandateAsync(Dictionary<string, object> fields)
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
            Console.WriteLine("aqio");
            Console.WriteLine($"Erro ao criar item: {ex.Message}");
            return false;
        }
    }

    // 4. Excluir um item pelo ID
    public async Task<bool> DeleteDemandanteAsync(string itemId)
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
            Console.WriteLine($"Erro ao excluir item {itemId}: {ex.Message}");
            return false;
        }
    }

}
