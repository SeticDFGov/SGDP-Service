using Interface.Repositorio;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Models;
using service;

namespace Repositorio;

public class TemplateRepositorio : ITemplateRepositorio
{
    public readonly AppDbContext _context;
    public TemplateRepositorio(AppDbContext context)
    {
        _context = context;
    }

    public async Task<Dictionary<string, List<Template>>> GetTemplateListItemsAsync()
    {
        var templates = await _context.Templates
            .ToListAsync();

        var groupedTemplates = templates
            .GroupBy(t => t.NM_TEMPLATE)
            .ToDictionary(g => g.Key, g => g.ToList());

        return groupedTemplates;
    }


    public async Task<List<string>> GetNameTemplate()
    {
        var templates = await _context.Templates.Select(e => e.NM_TEMPLATE).Distinct().ToListAsync();
        return templates;
    }

    public async Task<Template> GetTemplateById(int id)
    {
        Template? item = await _context.Templates.FirstOrDefaultAsync(e => e.TemplateId == id);
        return item ?? throw new ApiException(ErrorCode.TemplateNaoEncontrado);
    }

    public async Task CreateTemplate(Template template)
    {
        _context.Templates.Add(template);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateTemplate(Template template)
    {
        _context.Templates.Update(template);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteTemplate(int id)
    {
        Template? item = await _context.Templates.FirstOrDefaultAsync(e => e.TemplateId == id);
        if (item == null)
            throw new ApiException(ErrorCode.TemplateNaoEncontrado);

        _context.Templates.Remove(item);
        await _context.SaveChangesAsync();
    }
   

}