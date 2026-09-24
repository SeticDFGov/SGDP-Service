using Models;
using Microsoft.EntityFrameworkCore;
using app.Models;
using Models.Pgia;
using Models.Contratacoes;
using Models.Acesso;
using Models.Planejamento;

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
        public DbSet<PgiaRiscoOutro> PgiaRiscosOutros { get; set; }
        public DbSet<PgiaDocumento> PgiaDocumentos { get; set; }
        public DbSet<PgiaDocumentoArquivo> PgiaDocumentoArquivos { get; set; }
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

        // Módulo Supervisão Contínua das Contratações — tabelas com prefixo ctr_
        public DbSet<CtrProcesso> CtrProcessos { get; set; }
        public DbSet<CtrManifestacaoTcdf> CtrManifestacoesTcdf { get; set; }
        public DbSet<CtrRiscoDeclarado> CtrRiscosDeclarados { get; set; }

        // Gestão de acessos por módulo (isolamento entre módulos) — tabela acesso_modulo
        public DbSet<AcessoModulo> AcessosModulo { get; set; }

        // Pedidos de acesso feitos no card da tela inicial: tabela pedido_acesso
        public DbSet<PedidoAcesso> PedidosAcesso { get; set; }

        // Módulo Governança Estratégica: tabelas com prefixo pe_. O cálculo do acesso de
        // cada requisição nunca lê estas tabelas (regra do deploy, ver PapeisPlanejamento)
        public DbSet<PePapelUsuario> PePapeisUsuario { get; set; }
        public DbSet<PePapelUsuarioHistorico> PePapeisUsuarioHistorico { get; set; }
        // Modelo configurável e níveis de maturidade (E2)
        public DbSet<PeNivel> PeNiveis { get; set; }
        public DbSet<PeEtapa> PeEtapas { get; set; }
        public DbSet<PePasso> PePassos { get; set; }
        public DbSet<PePassoNivel> PePassosNivel { get; set; }
        public DbSet<PeSecao> PeSecoes { get; set; }
        public DbSet<PeSecaoNivel> PeSecoesNivel { get; set; }
        public DbSet<PeCampo> PeCampos { get; set; }
        public DbSet<PeCampoNivel> PeCamposNivel { get; set; }
        public DbSet<PeOpcao> PeOpcoes { get; set; }
        public DbSet<PeOrgaoConfig> PeOrgaosConfig { get; set; }
        public DbSet<PeOrgaoAjuste> PeOrgaosAjuste { get; set; }
        public DbSet<PeConfiguracao> PeConfiguracoes { get; set; }
        public DbSet<PeModeloHistorico> PeModeloHistorico { get; set; }
        // Referenciais e registros (E3): PETIC-DF, deliberações do CGTIC, registros e arquivos
        public DbSet<PePetic> PePetics { get; set; }
        public DbSet<PeDeliberacao> PeDeliberacoes { get; set; }
        public DbSet<PeRegistro> PeRegistros { get; set; }
        public DbSet<PeVinculo> PeVinculos { get; set; }
        public DbSet<PeRegistroSequencia> PeRegistroSequencias { get; set; }
        public DbSet<PeArquivo> PeArquivos { get; set; }
        public DbSet<PeArquivoConteudo> PeArquivosConteudo { get; set; }
        // PDTIC dos órgãos (E4): o PDTIC, o "não se aplica" de cada passo e os comentários
        public DbSet<PePdtic> PePdtics { get; set; }
        public DbSet<PePdticPasso> PePdticPassos { get; set; }
        public DbSet<PeComentario> PeComentarios { get; set; }

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

            // Módulo Supervisão Contínua das Contratações: mapeamento isolado em CtrModelConfiguration
            modelBuilder.ApplyCtrConfiguration(Database.IsNpgsql());

            // Gestão de acessos por módulo: mapeamento isolado em AcessoModelConfiguration
            modelBuilder.ApplyAcessoConfiguration();

            // Módulo Governança Estratégica: mapeamento isolado em PeModelConfiguration
            modelBuilder.ApplyPeConfiguration();
        }
    }

}
