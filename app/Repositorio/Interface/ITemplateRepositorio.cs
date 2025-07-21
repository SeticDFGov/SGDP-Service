using Models;

namespace Interface.Repositorio;

public interface ITemplateRepositorio
{
    Task<List<Template>> GetTemplateListItemsAsync();
    Task<Template> GetTemplateById(int id);
    Task CreateTemplate(Template template);
    Task UpdateTemplate(Template template);
    Task DeleteTemplate(int id);
}