using Microsoft.EntityFrameworkCore;

namespace DbContext.Repository
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<SeuModelo> Modelos { get; set; } // Substitua pelo seu modelo
    }
}
