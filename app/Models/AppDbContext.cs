using Models;
using Microsoft.EntityFrameworkCore;
using app.Models;

namespace Models
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options)
        {
        }
        public DbSet<Categoria> Categorias { get; set; }
        public DbSet<AreaDemandante> AreaDemandantes { get; set; }
        public DbSet<Demanda> Demandas { get; set; }
        public DbSet<Projeto> Projetos { get; set; }
        public DbSet<Etapa> Etapas { get; set; }
        public DbSet<Template> Templates { get; set; }
        public DbSet<Report> Reports { get; set; }
        public DbSet<Detalhamento> Detalhamentos { get; set; }
        public DbSet<User> Users { get; set; }
        public DbSet<Unidade> Unidades { get; set; }
        public DbSet<Esteira> Esteiras { get; set; }
        public DbSet<Despacho> Despachos { get; set; }
        public DbSet<Export> Exports { get; set; }
        public DbSet<Atividade> Atividades { get; set; }
         public DbSet<AtividadeExport> AtividadeExport { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configura relacionamento Atividade -> Etapa
            modelBuilder.Entity<Atividade>()
                .HasOne(a => a.Etapa)
                .WithMany(e => e.Atividades)
                .HasForeignKey(a => a.EtapaProjetoId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }

}
