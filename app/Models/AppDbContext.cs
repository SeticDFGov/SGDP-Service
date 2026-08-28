using Models;
using Microsoft.EntityFrameworkCore;
using app.Models;
using Models.Pgia;

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

        // Módulo PGIA (Decreto nº 48.901/2026) — tabelas com prefixo pgia_
        public DbSet<PgiaOrgao> PgiaOrgaos { get; set; }
        public DbSet<PgiaAgenteInfo> PgiaAgenteInfos { get; set; }
        public DbSet<PgiaResponsavelIa> PgiaResponsaveisIa { get; set; }
        public DbSet<PgiaEncarregadoDados> PgiaEncarregadosDados { get; set; }
        public DbSet<PgiaPrazoConformidade> PgiaPrazosConformidade { get; set; }
        public DbSet<PgiaSistemaIa> PgiaSistemasIa { get; set; }
        public DbSet<PgiaClassificacaoRisco> PgiaClassificacoesRisco { get; set; }
        public DbSet<PgiaDocumento> PgiaDocumentos { get; set; }
        public DbSet<PgiaAia> PgiaAias { get; set; }
        public DbSet<PgiaDeliberacaoCgtic> PgiaDeliberacoesCgtic { get; set; }
        public DbSet<PgiaPlataformaIaGenerativa> PgiaPlataformasIaGenerativa { get; set; }
        public DbSet<PgiaAutorizacaoExcepcional> PgiaAutorizacoesExcepcionais { get; set; }
        public DbSet<PgiaNormaComplementar> PgiaNormasComplementares { get; set; }
        public DbSet<PgiaIncidente> PgiaIncidentes { get; set; }
        public DbSet<PgiaNaoConformidade> PgiaNaoConformidades { get; set; }
        public DbSet<PgiaCapacitacao> PgiaCapacitacoes { get; set; }
        public DbSet<PgiaRegistroUsoIa> PgiaRegistrosUsoIa { get; set; }
        public DbSet<PgiaContratoIa> PgiaContratosIa { get; set; }
        public DbSet<PgiaInstrumentoLegado> PgiaInstrumentosLegados { get; set; }
        public DbSet<PgiaIndicadorDesempenho> PgiaIndicadoresDesempenho { get; set; }
        public DbSet<PgiaRelatorioSemestral> PgiaRelatoriosSemestrais { get; set; }
        public DbSet<PgiaRelatorioAnual> PgiaRelatoriosAnuais { get; set; }
        public DbSet<PgiaAuditoriaTecnica> PgiaAuditoriasTecnicas { get; set; }
        public DbSet<PgiaSolicitacaoCidadao> PgiaSolicitacoesCidadao { get; set; }

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

            modelBuilder.Entity<Demanda>()
                .Property(d => d.CriadoEm)
                .HasDefaultValueSql("NOW()");

            modelBuilder.Entity<Etapa>()
                .Property(e => e.CriadoEm)
                .HasDefaultValueSql("NOW()");

            // Módulo PGIA: mapeamento isolado em PgiaModelConfiguration
            modelBuilder.ApplyPgiaConfiguration(Database.IsNpgsql());
        }
    }

}
