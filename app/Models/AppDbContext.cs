using Models;
using Microsoft.EntityFrameworkCore;
using app.Models;

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
        public DbSet<Template> Templates {get;set;}
        public DbSet<ProjetoAnalise> Analises {get;set;}
        public DbSet<Detalhamento> Detalhamentos {get;set;}
        public DbSet<User> Users {get;set;}
        public DbSet<Unidade> Unidades {get;set;}
    }
}
