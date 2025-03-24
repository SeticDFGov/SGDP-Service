using Models;
using Microsoft.EntityFrameworkCore;

namespace Models
{
    public class AppDbContext: DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options)
        {
        }
        public DbSet<Categoria> Categorias { get; set; } 
    }
}
