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

    public async Task<List<Template>> GetTemplateListItemsAsync()
    {
        List<Template> listItems = await _context.Templates.ToListAsync();
        return listItems;
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