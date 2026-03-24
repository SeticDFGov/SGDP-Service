using Models;

public interface IAreaExecutoraRepositorio
{
    Task<List<AreaExecutora>> GetAllAsync();
    Task<AreaExecutora?> GetByIdAsync(int id);
    Task AddAsync(AreaExecutora area);
    Task UpdateAsync(AreaExecutora area);
    Task DeleteAsync(AreaExecutora area);
}
