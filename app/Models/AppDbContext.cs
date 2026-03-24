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

        public DbSet<AreaDemandante> AreaDemandantes { get; set; }
        public DbSet<Demanda> Demandas { get; set; }
        public DbSet<Etapa> Etapas { get; set; }
        public DbSet<AreaExecutora> AreasExecutoras { get; set; }
        public DbSet<User> Users { get; set; }
        public DbSet<Unidade> Unidades { get; set; }
        public DbSet<Esteira> Esteiras { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configura relacionamento Etapa -> Demanda
            modelBuilder.Entity<Etapa>()
                .HasOne(e => e.NM_PROJETO)
                .WithMany(d => d.Entregaveis)
                .OnDelete(DeleteBehavior.Cascade);

            // Configura relacionamento Etapa -> AreaExecutora
            modelBuilder.Entity<Etapa>()
                .HasOne(e => e.Responsavel)
                .WithMany()
                .OnDelete(DeleteBehavior.SetNull);

            // Demandas table mapping (renamed from Projetos)
            modelBuilder.Entity<Demanda>().ToTable("Demandas");
        }
    }

}
