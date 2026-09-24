using app.Auth;
using Microsoft.EntityFrameworkCore;

namespace Models.Planejamento;

/// <summary>
/// Mapeamento EF das tabelas do módulo Governança Estratégica: prefixo pe_, colunas
/// snake_case e CHECKs gerados dos domínios (<see cref="PapeisPlanejamento"/> e
/// <see cref="PeDominios"/>). Constraints e índices com nomes explícitos e prefixo pe
/// (pk_pe_*, ck_pe_*, ix_pe_*, fk_pe_*) para a revisão do script da migration, no
/// mesmo padrão do PgiaModelConfiguration e do CtrModelConfiguration. Nenhuma tabela
/// existente é alterada por aqui.
/// </summary>
public static class PeModelConfiguration
{
    private static string EmLista(string coluna, IEnumerable<string> valores) =>
        $"{coluna} IN ({string.Join(",", valores.Select(v => $"'{v.Replace("'", "''")}'"))})";

    public static void ApplyPeConfiguration(this ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PePapelUsuario>(entity =>
        {
            entity.ToTable("pe_papel_usuario", t =>
                t.HasCheckConstraint("ck_pe_papel_usuario_papel", EmLista("papel", PapeisPlanejamento.Todos)));

            // Uma linha por usuário: a chave é o próprio usuário
            entity.HasKey(p => p.UserId).HasName("pk_pe_papel_usuario");
            entity.Property(p => p.UserId).HasColumnName("user_id").ValueGeneratedNever();
            entity.Property(p => p.Papel).HasColumnName("papel").HasMaxLength(30).IsRequired();
            entity.Property(p => p.ConcedidoEm).HasColumnName("concedido_em").HasDefaultValueSql("NOW()");
            entity.Property(p => p.ConcedidoPor).HasColumnName("concedido_por").HasMaxLength(200).IsRequired();
            entity.Property(p => p.AlteradoEm).HasColumnName("alterado_em");
            entity.Property(p => p.AlteradoPor).HasColumnName("alterado_por").HasMaxLength(200);

            // Apagar o usuário apaga o papel
            entity.HasOne(p => p.User)
                .WithOne()
                .HasForeignKey<PePapelUsuario>(p => p.UserId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("fk_pe_papel_usuario_user");
        });

        modelBuilder.Entity<PePapelUsuarioHistorico>(entity =>
        {
            entity.ToTable("pe_papel_usuario_historico", t =>
                t.HasCheckConstraint("ck_pe_papel_usuario_historico_origem",
                    EmLista("origem", PeDominios.OrigemPapel.Todas)));

            entity.HasKey(h => h.Id).HasName("pk_pe_papel_usuario_historico");
            entity.Property(h => h.Id).HasColumnName("id").UseIdentityByDefaultColumn();
            entity.Property(h => h.UserId).HasColumnName("user_id");
            // Sem CHECK nos papéis de propósito: o histórico guarda o valor da época,
            // mesmo que um papel deixe de existir no domínio
            entity.Property(h => h.PapelAnterior).HasColumnName("papel_anterior").HasMaxLength(30);
            entity.Property(h => h.PapelNovo).HasColumnName("papel_novo").HasMaxLength(30);
            entity.Property(h => h.Origem).HasColumnName("origem").HasMaxLength(20).IsRequired();
            entity.Property(h => h.PedidoAcessoId).HasColumnName("pedido_acesso_id");
            entity.Property(h => h.AlteradoEm).HasColumnName("alterado_em").HasDefaultValueSql("NOW()");
            entity.Property(h => h.AlteradoPor).HasColumnName("alterado_por").HasMaxLength(200).IsRequired();

            // Histórico de uma pessoa, em ordem
            entity.HasIndex(h => new { h.UserId, h.AlteradoEm })
                .HasDatabaseName("ix_pe_papel_usuario_historico_user");

            // Apagar o usuário apaga o histórico dele
            entity.HasOne(h => h.User)
                .WithMany()
                .HasForeignKey(h => h.UserId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("fk_pe_papel_usuario_historico_user");
        });

        modelBuilder.ApplyPeModeloConfiguration();
        modelBuilder.ApplyPeReferenciaisConfiguration();
    }

    // ── Modelo configurável e níveis de maturidade (E2) ──────────────────────

    // Chaves: minúsculas e números, com hífen (etapa e passo) ou sublinhado (o resto).
    // A chave do campo é a chave do valor no jsonb dos registros
    private const string ChaveComHifen = "^[a-z][a-z0-9-]*$";
    private const string ChaveComSublinhado = "^[a-z][a-z0-9_]*$";

    // "I" a "IX", ou vários separados por vírgula ("V,VI,IX")
    private const string Incisos = "^(I|II|III|IV|V|VI|VII|VIII|IX)(,(I|II|III|IV|V|VI|VII|VIII|IX))*$";

    private static string SituacaoOuNula(string coluna) =>
        $"{coluna} IS NULL OR {EmLista(coluna, PeDominios.Situacao.Todas)}";

    private static void Auditoria<T>(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<T> entity)
        where T : class
    {
        entity.Property<DateTime>("CriadoEm").HasColumnName("criado_em").HasDefaultValueSql("NOW()");
        entity.Property<string>("CriadoPor").HasColumnName("criado_por").HasMaxLength(200).IsRequired();
        entity.Property<DateTime?>("AlteradoEm").HasColumnName("alterado_em");
        entity.Property<string?>("AlteradoPor").HasColumnName("alterado_por").HasMaxLength(200);
    }

    private static void ApplyPeModeloConfiguration(this ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PeNivel>(entity =>
        {
            entity.ToTable("pe_nivel", t =>
                t.HasCheckConstraint("ck_pe_nivel_codigo", $"codigo ~ '{ChaveComSublinhado}'"));

            entity.HasKey(n => n.Id).HasName("pk_pe_nivel");
            entity.Property(n => n.Id).HasColumnName("id").UseIdentityByDefaultColumn();
            entity.Property(n => n.Codigo).HasColumnName("codigo").HasMaxLength(40).IsRequired();
            entity.Property(n => n.Nome).HasColumnName("nome").HasMaxLength(100).IsRequired();
            entity.Property(n => n.Descricao).HasColumnName("descricao").HasMaxLength(1000);
            entity.Property(n => n.Ordem).HasColumnName("ordem");
            entity.Property(n => n.Ativo).HasColumnName("ativo");
            Auditoria(entity);

            entity.HasIndex(n => n.Codigo).IsUnique().HasDatabaseName("ux_pe_nivel_codigo");
        });

        modelBuilder.Entity<PeEtapa>(entity =>
        {
            entity.ToTable("pe_etapa", t =>
                t.HasCheckConstraint("ck_pe_etapa_chave", $"chave ~ '{ChaveComHifen}'"));

            entity.HasKey(e => e.Id).HasName("pk_pe_etapa");
            entity.Property(e => e.Id).HasColumnName("id").UseIdentityByDefaultColumn();
            entity.Property(e => e.Chave).HasColumnName("chave").HasMaxLength(60).IsRequired();
            entity.Property(e => e.Titulo).HasColumnName("titulo").HasMaxLength(200).IsRequired();
            entity.Property(e => e.Descricao).HasColumnName("descricao").HasMaxLength(1000);
            entity.Property(e => e.ReferenciaGuia).HasColumnName("referencia_guia").HasMaxLength(100);
            entity.Property(e => e.Ordem).HasColumnName("ordem");
            entity.Property(e => e.Sistema).HasColumnName("sistema");
            Auditoria(entity);

            entity.HasIndex(e => e.Chave).IsUnique().HasDatabaseName("ux_pe_etapa_chave");
        });

        modelBuilder.Entity<PePasso>(entity =>
        {
            entity.ToTable("pe_passo", t =>
            {
                t.HasCheckConstraint("ck_pe_passo_tipo", EmLista("tipo", PeDominios.TipoPasso.Todos));
                t.HasCheckConstraint("ck_pe_passo_inciso", $"inciso_decreto IS NULL OR inciso_decreto ~ '{Incisos}'");
                // "etapa.passo", com hífen nas duas partes ("preparacao.abrangencia")
                t.HasCheckConstraint("ck_pe_passo_chave", "chave ~ '^[a-z][a-z0-9-]*\\.[a-z0-9][a-z0-9-]*$'");
            });

            entity.HasKey(p => p.Id).HasName("pk_pe_passo");
            entity.Property(p => p.Id).HasColumnName("id").UseIdentityByDefaultColumn();
            entity.Property(p => p.EtapaId).HasColumnName("etapa_id");
            entity.Property(p => p.Chave).HasColumnName("chave").HasMaxLength(100).IsRequired();
            entity.Property(p => p.Titulo).HasColumnName("titulo").HasMaxLength(200).IsRequired();
            entity.Property(p => p.OQueFazer).HasColumnName("o_que_fazer").HasMaxLength(2000).IsRequired();
            entity.Property(p => p.BaseLegal).HasColumnName("base_legal").HasMaxLength(200);
            entity.Property(p => p.ReferenciaGuia).HasColumnName("referencia_guia").HasMaxLength(100);
            entity.Property(p => p.Tipo).HasColumnName("tipo").HasMaxLength(30).IsRequired();
            entity.Property(p => p.IncisoDecreto).HasColumnName("inciso_decreto").HasMaxLength(40);
            entity.Property(p => p.Travado).HasColumnName("travado");
            entity.Property(p => p.AceitaNaoSeAplica).HasColumnName("aceita_nao_se_aplica");
            entity.Property(p => p.Ordem).HasColumnName("ordem");
            entity.Property(p => p.Sistema).HasColumnName("sistema");
            entity.Property(p => p.ExcluidoEm).HasColumnName("excluido_em");
            Auditoria(entity);

            entity.HasIndex(p => p.Chave).IsUnique().HasDatabaseName("ux_pe_passo_chave");
            entity.HasIndex(p => new { p.EtapaId, p.Ordem }).HasDatabaseName("ix_pe_passo_etapa");

            // Etapa não é apagada nesta entrega; se um dia for, não leva os passos junto
            entity.HasOne(p => p.Etapa)
                .WithMany(e => e.Passos)
                .HasForeignKey(p => p.EtapaId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_pe_passo_etapa");
        });

        modelBuilder.Entity<PePassoNivel>(entity =>
        {
            entity.ToTable("pe_passo_nivel", t =>
                t.HasCheckConstraint("ck_pe_passo_nivel_situacao", EmLista("situacao", PeDominios.Situacao.Todas)));

            entity.HasKey(n => new { n.PassoId, n.NivelId }).HasName("pk_pe_passo_nivel");
            entity.Property(n => n.PassoId).HasColumnName("passo_id");
            entity.Property(n => n.NivelId).HasColumnName("nivel_id");
            entity.Property(n => n.Situacao).HasColumnName("situacao").HasMaxLength(20).IsRequired();

            entity.HasIndex(n => n.NivelId).HasDatabaseName("ix_pe_passo_nivel_nivel");

            entity.HasOne(n => n.Passo)
                .WithMany(p => p.Niveis)
                .HasForeignKey(n => n.PassoId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("fk_pe_passo_nivel_passo");

            entity.HasOne(n => n.Nivel)
                .WithMany()
                .HasForeignKey(n => n.NivelId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("fk_pe_passo_nivel_nivel");
        });

        modelBuilder.Entity<PeSecao>(entity =>
        {
            entity.ToTable("pe_secao", t =>
            {
                t.HasCheckConstraint("ck_pe_secao_escopo", EmLista("escopo", PeDominios.Escopo.Todos));
                t.HasCheckConstraint("ck_pe_secao_tipo", EmLista("tipo", PeDominios.TipoSecao.Todos));
                t.HasCheckConstraint("ck_pe_secao_inciso", $"inciso_decreto IS NULL OR inciso_decreto ~ '{Incisos}'");
                t.HasCheckConstraint("ck_pe_secao_chave", $"chave ~ '{ChaveComSublinhado}'");
                t.HasCheckConstraint("ck_pe_secao_situacao_geral", SituacaoOuNula("situacao_geral"));
                // Seção do PDTIC pertence a um passo e depende do nível; as do PETIC-DF e do
                // DF não têm passo e usam a situação geral
                t.HasCheckConstraint("ck_pe_secao_escopo_passo",
                    $"(escopo = '{PeDominios.Escopo.Pdtic}') = (passo_id IS NOT NULL)");
                t.HasCheckConstraint("ck_pe_secao_escopo_situacao",
                    $"(escopo = '{PeDominios.Escopo.Pdtic}') = (situacao_geral IS NULL)");
            });

            entity.HasKey(s => s.Id).HasName("pk_pe_secao");
            entity.Property(s => s.Id).HasColumnName("id").UseIdentityByDefaultColumn();
            entity.Property(s => s.PassoId).HasColumnName("passo_id");
            entity.Property(s => s.Escopo).HasColumnName("escopo").HasMaxLength(10).IsRequired();
            entity.Property(s => s.Chave).HasColumnName("chave").HasMaxLength(60).IsRequired();
            entity.Property(s => s.Titulo).HasColumnName("titulo").HasMaxLength(200).IsRequired();
            entity.Property(s => s.Ajuda).HasColumnName("ajuda").HasMaxLength(2000);
            entity.Property(s => s.Tipo).HasColumnName("tipo").HasMaxLength(20).IsRequired();
            entity.Property(s => s.PrefixoCodigo).HasColumnName("prefixo_codigo").HasMaxLength(10);
            entity.Property(s => s.Ordem).HasColumnName("ordem");
            entity.Property(s => s.NoDocumento).HasColumnName("no_documento");
            entity.Property(s => s.NaPlanilha).HasColumnName("na_planilha");
            entity.Property(s => s.Travada).HasColumnName("travada");
            entity.Property(s => s.IncisoDecreto).HasColumnName("inciso_decreto").HasMaxLength(40);
            entity.Property(s => s.SituacaoGeral).HasColumnName("situacao_geral").HasMaxLength(20);
            entity.Property(s => s.Sistema).HasColumnName("sistema");
            entity.Property(s => s.ExcluidoEm).HasColumnName("excluido_em");
            Auditoria(entity);

            entity.HasIndex(s => s.Chave).IsUnique().HasDatabaseName("ux_pe_secao_chave");
            entity.HasIndex(s => new { s.PassoId, s.Ordem }).HasDatabaseName("ix_pe_secao_passo");

            entity.HasOne(s => s.Passo)
                .WithMany(p => p.Secoes)
                .HasForeignKey(s => s.PassoId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_pe_secao_passo");
        });

        modelBuilder.Entity<PeSecaoNivel>(entity =>
        {
            entity.ToTable("pe_secao_nivel", t =>
                t.HasCheckConstraint("ck_pe_secao_nivel_situacao", EmLista("situacao", PeDominios.Situacao.Todas)));

            entity.HasKey(n => new { n.SecaoId, n.NivelId }).HasName("pk_pe_secao_nivel");
            entity.Property(n => n.SecaoId).HasColumnName("secao_id");
            entity.Property(n => n.NivelId).HasColumnName("nivel_id");
            entity.Property(n => n.Situacao).HasColumnName("situacao").HasMaxLength(20).IsRequired();

            entity.HasIndex(n => n.NivelId).HasDatabaseName("ix_pe_secao_nivel_nivel");

            entity.HasOne(n => n.Secao)
                .WithMany(s => s.Niveis)
                .HasForeignKey(n => n.SecaoId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("fk_pe_secao_nivel_secao");

            entity.HasOne(n => n.Nivel)
                .WithMany()
                .HasForeignKey(n => n.NivelId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("fk_pe_secao_nivel_nivel");
        });

        modelBuilder.Entity<PeCampo>(entity =>
        {
            entity.ToTable("pe_campo", t =>
            {
                t.HasCheckConstraint("ck_pe_campo_tipo", EmLista("tipo", PeDominios.TipoCampo.Todos));
                t.HasCheckConstraint("ck_pe_campo_chave", $"chave ~ '{ChaveComSublinhado}'");
                t.HasCheckConstraint("ck_pe_campo_largura",
                    $"largura IS NULL OR {EmLista("largura", PeDominios.Largura.Todas)}");
                t.HasCheckConstraint("ck_pe_campo_situacao_geral", SituacaoOuNula("situacao_geral"));
            });

            entity.HasKey(c => c.Id).HasName("pk_pe_campo");
            entity.Property(c => c.Id).HasColumnName("id").UseIdentityByDefaultColumn();
            entity.Property(c => c.SecaoId).HasColumnName("secao_id");
            entity.Property(c => c.Chave).HasColumnName("chave").HasMaxLength(60).IsRequired();
            entity.Property(c => c.Rotulo).HasColumnName("rotulo").HasMaxLength(200).IsRequired();
            entity.Property(c => c.Ajuda).HasColumnName("ajuda").HasMaxLength(2000);
            entity.Property(c => c.Tipo).HasColumnName("tipo").HasMaxLength(30).IsRequired();
            entity.Property(c => c.Config).HasColumnName("config").HasColumnType("jsonb").IsRequired();
            entity.Property(c => c.Principal).HasColumnName("principal");
            entity.Property(c => c.Travado).HasColumnName("travado");
            entity.Property(c => c.Ordem).HasColumnName("ordem");
            entity.Property(c => c.NoDocumento).HasColumnName("no_documento");
            entity.Property(c => c.NaPlanilha).HasColumnName("na_planilha");
            entity.Property(c => c.Largura).HasColumnName("largura").HasMaxLength(10);
            entity.Property(c => c.SituacaoGeral).HasColumnName("situacao_geral").HasMaxLength(20);
            entity.Property(c => c.Sistema).HasColumnName("sistema");
            entity.Property(c => c.ExcluidoEm).HasColumnName("excluido_em");
            Auditoria(entity);

            // A chave é única na seção, inclusive entre os apagados: os dados de um campo
            // apagado continuam no jsonb com a chave dele
            entity.HasIndex(c => new { c.SecaoId, c.Chave }).IsUnique().HasDatabaseName("ux_pe_campo_secao_chave");

            entity.HasOne(c => c.Secao)
                .WithMany(s => s.Campos)
                .HasForeignKey(c => c.SecaoId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_pe_campo_secao");
        });

        modelBuilder.Entity<PeCampoNivel>(entity =>
        {
            entity.ToTable("pe_campo_nivel", t =>
                t.HasCheckConstraint("ck_pe_campo_nivel_situacao", EmLista("situacao", PeDominios.Situacao.Todas)));

            entity.HasKey(n => new { n.CampoId, n.NivelId }).HasName("pk_pe_campo_nivel");
            entity.Property(n => n.CampoId).HasColumnName("campo_id");
            entity.Property(n => n.NivelId).HasColumnName("nivel_id");
            entity.Property(n => n.Situacao).HasColumnName("situacao").HasMaxLength(20).IsRequired();

            entity.HasIndex(n => n.NivelId).HasDatabaseName("ix_pe_campo_nivel_nivel");

            entity.HasOne(n => n.Campo)
                .WithMany(c => c.Niveis)
                .HasForeignKey(n => n.CampoId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("fk_pe_campo_nivel_campo");

            entity.HasOne(n => n.Nivel)
                .WithMany()
                .HasForeignKey(n => n.NivelId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("fk_pe_campo_nivel_nivel");
        });

        modelBuilder.Entity<PeOpcao>(entity =>
        {
            entity.ToTable("pe_opcao", t =>
            {
                t.HasCheckConstraint("ck_pe_opcao_valor", "valor ~ '^[a-z0-9][a-z0-9_]*$'");
                t.HasCheckConstraint("ck_pe_opcao_cor", $"cor IS NULL OR {EmLista("cor", PeDominios.Cor.Todas)}");
            });

            entity.HasKey(o => o.Id).HasName("pk_pe_opcao");
            entity.Property(o => o.Id).HasColumnName("id").UseIdentityByDefaultColumn();
            entity.Property(o => o.CampoId).HasColumnName("campo_id");
            entity.Property(o => o.Valor).HasColumnName("valor").HasMaxLength(60).IsRequired();
            entity.Property(o => o.Rotulo).HasColumnName("rotulo").HasMaxLength(200).IsRequired();
            entity.Property(o => o.Ordem).HasColumnName("ordem");
            entity.Property(o => o.Ativa).HasColumnName("ativa");
            entity.Property(o => o.Cor).HasColumnName("cor").HasMaxLength(20);
            entity.Property(o => o.Travada).HasColumnName("travada");
            entity.Property(o => o.Sistema).HasColumnName("sistema");
            Auditoria(entity);

            entity.HasIndex(o => new { o.CampoId, o.Valor }).IsUnique().HasDatabaseName("ux_pe_opcao_campo_valor");

            entity.HasOne(o => o.Campo)
                .WithMany(c => c.Opcoes)
                .HasForeignKey(o => o.CampoId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("fk_pe_opcao_campo");
        });

        modelBuilder.Entity<PeOrgaoConfig>(entity =>
        {
            entity.ToTable("pe_orgao_config");

            entity.HasKey(c => c.OrgaoId).HasName("pk_pe_orgao_config");
            entity.Property(c => c.OrgaoId).HasColumnName("orgao_id").ValueGeneratedNever();
            entity.Property(c => c.NivelId).HasColumnName("nivel_id");
            entity.Property(c => c.Justificativa).HasColumnName("justificativa").HasMaxLength(1000).IsRequired();
            entity.Property(c => c.DefinidoEm).HasColumnName("definido_em").HasDefaultValueSql("NOW()");
            entity.Property(c => c.DefinidoPor).HasColumnName("definido_por").HasMaxLength(200).IsRequired();

            entity.HasIndex(c => c.NivelId).HasDatabaseName("ix_pe_orgao_config_nivel");

            // Órgão do PGIA não é apagado (só desativado): a FK só impede apagar por engano
            entity.HasOne(c => c.Orgao)
                .WithMany()
                .HasForeignKey(c => c.OrgaoId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_pe_orgao_config_orgao");

            // Nível em uso não é apagado (é desativado)
            entity.HasOne(c => c.Nivel)
                .WithMany()
                .HasForeignKey(c => c.NivelId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_pe_orgao_config_nivel");
        });

        modelBuilder.Entity<PeOrgaoAjuste>(entity =>
        {
            entity.ToTable("pe_orgao_ajuste", t =>
            {
                t.HasCheckConstraint("ck_pe_orgao_ajuste_alvo", EmLista("alvo_tipo", PeDominios.AlvoAjuste.Todos));
                t.HasCheckConstraint("ck_pe_orgao_ajuste_situacao", EmLista("situacao", PeDominios.Situacao.Todas));
            });

            entity.HasKey(a => a.Id).HasName("pk_pe_orgao_ajuste");
            entity.Property(a => a.Id).HasColumnName("id").UseIdentityByDefaultColumn();
            entity.Property(a => a.OrgaoId).HasColumnName("orgao_id");
            entity.Property(a => a.AlvoTipo).HasColumnName("alvo_tipo").HasMaxLength(10).IsRequired();
            entity.Property(a => a.AlvoId).HasColumnName("alvo_id");
            entity.Property(a => a.Situacao).HasColumnName("situacao").HasMaxLength(20).IsRequired();
            entity.Property(a => a.Justificativa).HasColumnName("justificativa").HasMaxLength(1000);
            Auditoria(entity);

            entity.HasIndex(a => new { a.OrgaoId, a.AlvoTipo, a.AlvoId }).IsUnique()
                .HasDatabaseName("ux_pe_orgao_ajuste_alvo");

            entity.HasOne(a => a.Orgao)
                .WithMany()
                .HasForeignKey(a => a.OrgaoId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_pe_orgao_ajuste_orgao");
        });

        modelBuilder.Entity<PeConfiguracao>(entity =>
        {
            entity.ToTable("pe_configuracao");

            entity.HasKey(c => c.Chave).HasName("pk_pe_configuracao");
            entity.Property(c => c.Chave).HasColumnName("chave").HasMaxLength(100);
            entity.Property(c => c.Valor).HasColumnName("valor").HasColumnType("jsonb").IsRequired();
            Auditoria(entity);
        });

        modelBuilder.Entity<PeModeloHistorico>(entity =>
        {
            entity.ToTable("pe_modelo_historico", t =>
            {
                t.HasCheckConstraint("ck_pe_modelo_historico_entidade",
                    EmLista("entidade", PeDominios.EntidadeHistorico.Todas));
                t.HasCheckConstraint("ck_pe_modelo_historico_acao", EmLista("acao", PeDominios.AcaoHistorico.Todas));
            });

            entity.HasKey(h => h.Id).HasName("pk_pe_modelo_historico");
            entity.Property(h => h.Id).HasColumnName("id").UseIdentityByDefaultColumn();
            entity.Property(h => h.Entidade).HasColumnName("entidade").HasMaxLength(20).IsRequired();
            entity.Property(h => h.EntidadeId).HasColumnName("entidade_id");
            entity.Property(h => h.Acao).HasColumnName("acao").HasMaxLength(20).IsRequired();
            entity.Property(h => h.Antes).HasColumnName("antes").HasColumnType("jsonb");
            entity.Property(h => h.Depois).HasColumnName("depois").HasColumnType("jsonb");
            entity.Property(h => h.AlteradoEm).HasColumnName("alterado_em").HasDefaultValueSql("NOW()");
            entity.Property(h => h.AlteradoPor).HasColumnName("alterado_por").HasMaxLength(200).IsRequired();

            // Histórico de um item e o "o que mudou" geral, do mais novo para o mais antigo
            entity.HasIndex(h => new { h.Entidade, h.EntidadeId }).HasDatabaseName("ix_pe_modelo_historico_entidade");
            entity.HasIndex(h => h.AlteradoEm).HasDatabaseName("ix_pe_modelo_historico_data");
        });
    }

    // ── Referenciais e registros (E3) ────────────────────────────────────────

    // Código do registro: prefixo da seção (letra, depois letras ou números) + números
    private const string CodigoRegistro = "^[A-Z][A-Z0-9]*[0-9]$";

    private static void ApplyPeReferenciaisConfiguration(this ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PePetic>(entity =>
        {
            entity.ToTable("pe_petic", t =>
            {
                t.HasCheckConstraint("ck_pe_petic_situacao", EmLista("situacao", PeDominios.SituacaoPetic.Todas));
                t.HasCheckConstraint("ck_pe_petic_versao", "versao ~ '^[0-9]+\\.[0-9]+$'");
                t.HasCheckConstraint("ck_pe_petic_vigencia",
                    "vigencia_inicio IS NULL OR vigencia_fim IS NULL OR vigencia_fim >= vigencia_inicio");
                // Aprovada ou substituída tem a data da aprovação; rascunho e em deliberação, não
                t.HasCheckConstraint("ck_pe_petic_aprovado_em",
                    $"({EmLista("situacao", new[] { PeDominios.SituacaoPetic.Aprovado, PeDominios.SituacaoPetic.Substituido })}) = (aprovado_em IS NOT NULL)");
            });

            entity.HasKey(p => p.Id).HasName("pk_pe_petic");
            entity.Property(p => p.Id).HasColumnName("id").UseIdentityByDefaultColumn();
            entity.Property(p => p.Versao).HasColumnName("versao").HasMaxLength(10).IsRequired();
            entity.Property(p => p.Titulo).HasColumnName("titulo").HasMaxLength(200).IsRequired();
            entity.Property(p => p.VigenciaInicio).HasColumnName("vigencia_inicio");
            entity.Property(p => p.VigenciaFim).HasColumnName("vigencia_fim");
            // Token de concorrência: enviar, decidir, apagar e gravar um registro da versão
            // ao mesmo tempo não passam os dois
            entity.Property(p => p.Situacao).HasColumnName("situacao").HasMaxLength(20).IsRequired().IsConcurrencyToken();
            entity.Property(p => p.AnteriorId).HasColumnName("anterior_id");
            entity.Property(p => p.AprovadoEm).HasColumnName("aprovado_em");
            Auditoria(entity);

            entity.HasIndex(p => p.Versao).IsUnique().HasDatabaseName("ux_pe_petic_versao");
            // Uma versão em rascunho, uma em deliberação e uma aprovada (a vigente), no máximo
            entity.HasIndex(p => p.Situacao).IsUnique()
                .HasFilter(EmLista("situacao", PeDominios.SituacaoPetic.Unicas))
                .HasDatabaseName("ux_pe_petic_situacao");
            entity.HasIndex(p => p.AnteriorId).HasDatabaseName("ix_pe_petic_anterior");

            entity.HasOne(p => p.Anterior)
                .WithMany()
                .HasForeignKey(p => p.AnteriorId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_pe_petic_anterior");
        });

        modelBuilder.Entity<PeDeliberacao>(entity =>
        {
            entity.ToTable("pe_deliberacao", t =>
            {
                t.HasCheckConstraint("ck_pe_deliberacao_objeto", EmLista("objeto_tipo", PeDominios.ObjetoDeliberacao.Todos));
                t.HasCheckConstraint("ck_pe_deliberacao_situacao", EmLista("situacao", PeDominios.SituacaoDeliberacao.Todas));
                // Decidida tem data e autor; aguardando, não
                t.HasCheckConstraint("ck_pe_deliberacao_decisao",
                    $"(situacao = '{PeDominios.SituacaoDeliberacao.Aguardando}') = (decidido_em IS NULL) AND (decidido_em IS NULL) = (decidido_por IS NULL)");
                t.HasCheckConstraint("ck_pe_deliberacao_aprovado",
                    $"situacao <> '{PeDominios.SituacaoDeliberacao.Aprovado}' OR (ato_numero IS NOT NULL AND ato_data IS NOT NULL)");
                t.HasCheckConstraint("ck_pe_deliberacao_devolvido",
                    $"situacao <> '{PeDominios.SituacaoDeliberacao.Devolvido}' OR observacao IS NOT NULL");
            });

            entity.HasKey(d => d.Id).HasName("pk_pe_deliberacao");
            entity.Property(d => d.Id).HasColumnName("id").UseIdentityByDefaultColumn();
            entity.Property(d => d.ObjetoTipo).HasColumnName("objeto_tipo").HasMaxLength(10).IsRequired();
            entity.Property(d => d.ObjetoId).HasColumnName("objeto_id");
            entity.Property(d => d.VersaoObjeto).HasColumnName("versao_objeto").HasMaxLength(20).IsRequired();
            entity.Property(d => d.EnviadoEm).HasColumnName("enviado_em");
            entity.Property(d => d.EnviadoPor).HasColumnName("enviado_por").HasMaxLength(200).IsRequired();
            entity.Property(d => d.Situacao).HasColumnName("situacao").HasMaxLength(20).IsRequired().IsConcurrencyToken();
            entity.Property(d => d.DecididoEm).HasColumnName("decidido_em");
            entity.Property(d => d.DecididoPor).HasColumnName("decidido_por").HasMaxLength(200);
            entity.Property(d => d.AtoTipo).HasColumnName("ato_tipo").HasMaxLength(60);
            entity.Property(d => d.AtoNumero).HasColumnName("ato_numero").HasMaxLength(60);
            entity.Property(d => d.AtoData).HasColumnName("ato_data");
            entity.Property(d => d.Sei).HasColumnName("sei").HasMaxLength(40);
            entity.Property(d => d.Observacao).HasColumnName("observacao").HasMaxLength(2000);
            Auditoria(entity);

            // Índices com nome no modelo: os dois primeiros têm as mesmas colunas
            entity.HasIndex(d => new { d.ObjetoTipo, d.ObjetoId }, "ix_pe_deliberacao_objeto");
            // Um envio aguardando por objeto (dois cliques em "enviar" não viram dois)
            entity.HasIndex(d => new { d.ObjetoTipo, d.ObjetoId }, "ux_pe_deliberacao_aguardando").IsUnique()
                .HasFilter($"situacao = '{PeDominios.SituacaoDeliberacao.Aguardando}'");
            entity.HasIndex(d => new { d.Situacao, d.EnviadoEm }).HasDatabaseName("ix_pe_deliberacao_situacao");
        });

        modelBuilder.Entity<PeRegistro>(entity =>
        {
            entity.ToTable("pe_registro", t =>
                t.HasCheckConstraint("ck_pe_registro_codigo", $"codigo IS NULL OR codigo ~ '{CodigoRegistro}'"));

            entity.HasKey(r => r.Id).HasName("pk_pe_registro");
            entity.Property(r => r.Id).HasColumnName("id").UseIdentityByDefaultColumn();
            entity.Property(r => r.SecaoId).HasColumnName("secao_id");
            entity.Property(r => r.PeticId).HasColumnName("petic_id");
            entity.Property(r => r.Codigo).HasColumnName("codigo").HasMaxLength(20);
            entity.Property(r => r.Ordem).HasColumnName("ordem");
            entity.Property(r => r.Dados).HasColumnName("dados").HasColumnType("jsonb").IsRequired();
            entity.Property(r => r.Sistema).HasColumnName("sistema");
            Auditoria(entity);

            // Registros de uma seção (catálogo do DF), na ordem
            entity.HasIndex(r => new { r.SecaoId, r.Ordem }).HasDatabaseName("ix_pe_registro_secao");
            // Código único na seção dentro da versão do PETIC-DF (linhas sem petic_id ou sem
            // código não entram: NULL não repete). No catálogo do DF, quem garante é a
            // sequência (pe_registro_sequencia)
            entity.HasIndex(r => new { r.PeticId, r.SecaoId, r.Codigo }).IsUnique()
                .HasDatabaseName("ux_pe_registro_petic_codigo");

            // Seção não é apagada de verdade (exclusão lógica)
            entity.HasOne(r => r.Secao)
                .WithMany()
                .HasForeignKey(r => r.SecaoId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_pe_registro_secao");

            // Apagar um rascunho de PETIC (nunca enviado) leva os registros dele
            entity.HasOne(r => r.Petic)
                .WithMany()
                .HasForeignKey(r => r.PeticId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("fk_pe_registro_petic");
        });

        modelBuilder.Entity<PeVinculo>(entity =>
        {
            entity.ToTable("pe_vinculo", t =>
                t.HasCheckConstraint("ck_pe_vinculo_origem_destino", "registro_origem_id <> registro_destino_id"));

            entity.HasKey(v => v.Id).HasName("pk_pe_vinculo");
            entity.Property(v => v.Id).HasColumnName("id").UseIdentityByDefaultColumn();
            entity.Property(v => v.RegistroOrigemId).HasColumnName("registro_origem_id");
            entity.Property(v => v.CampoId).HasColumnName("campo_id");
            entity.Property(v => v.RegistroDestinoId).HasColumnName("registro_destino_id");

            entity.HasIndex(v => new { v.RegistroOrigemId, v.CampoId, v.RegistroDestinoId }).IsUnique()
                .HasDatabaseName("ux_pe_vinculo");
            // Quem liga este registro (para recusar apagar o que está ligado)
            entity.HasIndex(v => v.RegistroDestinoId).HasDatabaseName("ix_pe_vinculo_destino");
            entity.HasIndex(v => v.CampoId).HasDatabaseName("ix_pe_vinculo_campo");

            // Apagar a origem apaga as ligações dela
            entity.HasOne(v => v.RegistroOrigem)
                .WithMany()
                .HasForeignKey(v => v.RegistroOrigemId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("fk_pe_vinculo_origem");

            // O destino ligado não é apagado. NO ACTION (e não RESTRICT) recusa do mesmo jeito,
            // mas confere no fim do comando: apagar um rascunho inteiro em cascata não falha
            // pela ordem em que as linhas saem
            entity.HasOne(v => v.RegistroDestino)
                .WithMany()
                .HasForeignKey(v => v.RegistroDestinoId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("fk_pe_vinculo_destino");

            entity.HasOne(v => v.Campo)
                .WithMany()
                .HasForeignKey(v => v.CampoId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_pe_vinculo_campo");
        });

        modelBuilder.Entity<PeRegistroSequencia>(entity =>
        {
            entity.ToTable("pe_registro_sequencia", t =>
            {
                // "df", "petic:12" (a E4 usa "pdtic:ID")
                t.HasCheckConstraint("ck_pe_registro_sequencia_dono", "dono ~ '^[a-z]+(:[0-9]+)?$'");
                t.HasCheckConstraint("ck_pe_registro_sequencia_ultimo", "ultimo >= 0");
            });

            entity.HasKey(s => new { s.SecaoId, s.Dono }).HasName("pk_pe_registro_sequencia");
            entity.Property(s => s.SecaoId).HasColumnName("secao_id");
            entity.Property(s => s.Dono).HasColumnName("dono").HasMaxLength(30);
            // Token de concorrência: duas inclusões ao mesmo tempo não dão o mesmo código
            entity.Property(s => s.Ultimo).HasColumnName("ultimo").IsConcurrencyToken();

            entity.HasOne(s => s.Secao)
                .WithMany()
                .HasForeignKey(s => s.SecaoId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_pe_registro_sequencia_secao");
        });

        modelBuilder.Entity<PeArquivo>(entity =>
        {
            entity.ToTable("pe_arquivo", t =>
            {
                t.HasCheckConstraint("ck_pe_arquivo_dono_tipo",
                    $"dono_tipo IS NULL OR {EmLista("dono_tipo", PeDominios.DonoArquivo.Todos)}");
                t.HasCheckConstraint("ck_pe_arquivo_dono", "(dono_tipo IS NULL) = (dono_id IS NULL)");
                t.HasCheckConstraint("ck_pe_arquivo_tamanho", "tamanho > 0");
                // O binário mora na mesma linha (table splitting deixa a coluna anulável no
                // banco): sempre presente e do tamanho registrado
                t.HasCheckConstraint("ck_pe_arquivo_conteudo", "conteudo IS NOT NULL AND octet_length(conteudo) = tamanho");
            });

            entity.HasKey(a => a.Id).HasName("pk_pe_arquivo");
            entity.Property(a => a.Id).HasColumnName("id").UseIdentityByDefaultColumn();
            entity.Property(a => a.Nome).HasColumnName("nome").HasMaxLength(200).IsRequired();
            entity.Property(a => a.TipoMime).HasColumnName("tipo_mime").HasMaxLength(100).IsRequired();
            entity.Property(a => a.Tamanho).HasColumnName("tamanho");
            entity.Property(a => a.Hash).HasColumnName("hash").HasMaxLength(64).IsRequired();
            entity.Property(a => a.DonoTipo).HasColumnName("dono_tipo").HasMaxLength(20);
            entity.Property(a => a.DonoId).HasColumnName("dono_id");
            Auditoria(entity);

            entity.HasIndex(a => new { a.DonoTipo, a.DonoId }).HasDatabaseName("ix_pe_arquivo_dono");
        });

        // O binário na mesma tabela (table splitting): só o download o seleciona
        modelBuilder.Entity<PeArquivoConteudo>(entity =>
        {
            entity.ToTable("pe_arquivo");
            entity.HasKey(c => c.Id).HasName("pk_pe_arquivo");
            entity.Property(c => c.Id).HasColumnName("id");
            entity.Property(c => c.Conteudo).HasColumnName("conteudo").IsRequired();

            entity.HasOne(c => c.Arquivo)
                .WithOne()
                .HasForeignKey<PeArquivoConteudo>(c => c.Id);
        });
    }
}
