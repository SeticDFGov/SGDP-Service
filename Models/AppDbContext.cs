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
        public DbSet<AreaDemandante> AreaDemandantes {get;set;}
        public DbSet<Demanda> Demandas {get;set;}
        public DbSet<Projeto> Projetos {get;set;}
        public DbSet<Etapa> Etapas {get;set;}
    }
}
