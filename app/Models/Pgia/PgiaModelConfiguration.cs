using Microsoft.EntityFrameworkCore;

namespace Models.Pgia;

/// <summary>
/// Mapeamento EF das tabelas do módulo PGIA: prefixo pgia_, colunas snake_case e
/// CHECKs reproduzindo o schema_pgia_48901.sql. Nomes de constraints e índices
/// levam o prefixo pgia para facilitar a revisão do script de migration.
/// </summary>
public static class PgiaModelConfiguration
{
    // Data fixa para o seed (HasData exige valores estáticos)
    private static readonly DateTime SeedCriadoEm = new(2026, 8, 25, 0, 0, 0, DateTimeKind.Utc);

    /// <param name="indicesRelacionais">
    /// true no PostgreSQL. Cobre os índices únicos que dependem de semântica relacional:
    /// os parciais (WHERE ativo) usam HasFilter, que o provider InMemory dos testes ignora
    /// (exigiria unicidade incondicional de orgao_id e quebraria o histórico de designações),
    /// e o único de unidade_id depende de múltiplos NULLs serem permitidos.
    /// </param>
    public static void ApplyPgiaConfiguration(this ModelBuilder modelBuilder, bool indicesRelacionais = true)
    {
        modelBuilder.Entity<PgiaOrgao>(entity =>
        {
            entity.ToTable("pgia_orgao", t =>
            {
                t.HasCheckConstraint("ck_pgia_orgao_natureza",
                    "natureza_juridica IN ('Administração direta','Autarquia','Fundação pública','Empresa pública','Sociedade de economia mista')");
                t.HasCheckConstraint("ck_pgia_orgao_servico_cidadao",
                    "natureza_juridica IN ('Empresa pública','Sociedade de economia mista') OR presta_servico_cidadao IS NULL");
            });

            entity.HasKey(o => o.Id).HasName("pk_pgia_orgao");
            entity.Property(o => o.Id).HasColumnName("id").UseIdentityByDefaultColumn();
            entity.Property(o => o.Sigla).HasColumnName("sigla").HasMaxLength(20).IsRequired();
            entity.Property(o => o.Nome).HasColumnName("nome").IsRequired();
            entity.Property(o => o.NaturezaJuridica).HasColumnName("natureza_juridica").HasMaxLength(40).IsRequired();
            entity.Property(o => o.PrestaServicoCidadao).HasColumnName("presta_servico_cidadao");
            entity.Property(o => o.Ativo).HasColumnName("ativo").HasDefaultValue(true);
            entity.Property(o => o.UnidadeId).HasColumnName("unidade_id");
            entity.Property(o => o.CriadoEm).HasColumnName("criado_em").HasDefaultValueSql("NOW()");
            entity.Property(o => o.CriadoPor).HasColumnName("criado_por").HasMaxLength(200);
            entity.Property(o => o.AlteradoEm).HasColumnName("alterado_em");
            entity.Property(o => o.AlteradoPor).HasColumnName("alterado_por").HasMaxLength(200);

            entity.HasIndex(o => o.Sigla).IsUnique().HasDatabaseName("ux_pgia_orgao_sigla");
            if (indicesRelacionais)
            {
                // Uma unidade vincula no máximo um órgão: é assim que o sistema resolve
                // o órgão de cada usuário (NULLs múltiplos são permitidos no PostgreSQL)
                entity.HasIndex(o => o.UnidadeId).IsUnique().HasDatabaseName("ux_pgia_orgao_unidade");
            }
            else
            {
                entity.HasIndex(o => o.UnidadeId).HasDatabaseName("ix_pgia_orgao_unidade");
            }

            entity.HasOne(o => o.Unidade)
                .WithMany()
                .HasForeignKey(o => o.UnidadeId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("fk_pgia_orgao_unidade");
        });

        modelBuilder.Entity<PgiaAgenteInfo>(entity =>
        {
            entity.ToTable("pgia_agente_info", t =>
            {
                t.HasCheckConstraint("ck_pgia_agente_info_vinculo",
                    "vinculo IN ('Servidor efetivo','Comissionado','Estagiário','Prestador de serviço')");
            });

            entity.HasKey(a => a.UserId).HasName("pk_pgia_agente_info");
            entity.Property(a => a.UserId).HasColumnName("user_id");
            entity.Property(a => a.Matricula).HasColumnName("matricula").HasMaxLength(20);
            entity.Property(a => a.CargoFuncao).HasColumnName("cargo_funcao");
            entity.Property(a => a.Vinculo).HasColumnName("vinculo").HasMaxLength(30).IsRequired();
            entity.Property(a => a.CriadoEm).HasColumnName("criado_em").HasDefaultValueSql("NOW()");
            entity.Property(a => a.CriadoPor).HasColumnName("criado_por").HasMaxLength(200);
            entity.Property(a => a.AlteradoEm).HasColumnName("alterado_em");
            entity.Property(a => a.AlteradoPor).HasColumnName("alterado_por").HasMaxLength(200);

            entity.HasOne(a => a.User)
                .WithOne()
                .HasForeignKey<PgiaAgenteInfo>(a => a.UserId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("fk_pgia_agente_info_user");
        });

        modelBuilder.Entity<PgiaResponsavelIa>(entity =>
        {
            entity.ToTable("pgia_responsavel_ia");

            entity.HasKey(r => r.Id).HasName("pk_pgia_responsavel_ia");
            entity.Property(r => r.Id).HasColumnName("id").UseIdentityByDefaultColumn();
            entity.Property(r => r.OrgaoId).HasColumnName("orgao_id");
            entity.Property(r => r.AgenteId).HasColumnName("agente_id");
            entity.Property(r => r.AtoTipo).HasColumnName("ato_tipo").HasMaxLength(30).IsRequired();
            entity.Property(r => r.AtoNumero).HasColumnName("ato_numero").HasMaxLength(30).IsRequired();
            entity.Property(r => r.AtoData).HasColumnName("ato_data");
            entity.Property(r => r.ProcessoSeiComunicacao).HasColumnName("processo_sei_comunicacao").HasMaxLength(25).IsRequired();
            entity.Property(r => r.DataComunicacaoSgdi).HasColumnName("data_comunicacao_sgdi");
            entity.Property(r => r.AcumulaFuncaoTic).HasColumnName("acumula_funcao_tic").HasDefaultValue(false);
            entity.Property(r => r.CapacitacaoAdequada).HasColumnName("capacitacao_adequada");
            entity.Property(r => r.InicioVigencia).HasColumnName("inicio_vigencia");
            entity.Property(r => r.FimVigencia).HasColumnName("fim_vigencia");
            entity.Property(r => r.Ativo).HasColumnName("ativo").HasDefaultValue(true);
            entity.Property(r => r.CriadoEm).HasColumnName("criado_em").HasDefaultValueSql("NOW()");
            entity.Property(r => r.CriadoPor).HasColumnName("criado_por").HasMaxLength(200);
            entity.Property(r => r.AlteradoEm).HasColumnName("alterado_em");
            entity.Property(r => r.AlteradoPor).HasColumnName("alterado_por").HasMaxLength(200);

            // Índice único parcial: uma designação ativa por órgão (ux_responsavel_ia_vigente do schema)
            if (indicesRelacionais)
            {
                entity.HasIndex(r => r.OrgaoId)
                    .IsUnique()
                    .HasFilter("ativo")
                    .HasDatabaseName("ux_pgia_responsavel_ia_vigente");
            }
            entity.HasIndex(r => r.AgenteId).HasDatabaseName("ix_pgia_responsavel_ia_agente");

            entity.HasOne(r => r.Orgao)
                .WithMany()
                .HasForeignKey(r => r.OrgaoId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_pgia_responsavel_ia_orgao");

            entity.HasOne(r => r.Agente)
                .WithMany()
                .HasForeignKey(r => r.AgenteId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_pgia_responsavel_ia_agente");
        });

        modelBuilder.Entity<PgiaEncarregadoDados>(entity =>
        {
            entity.ToTable("pgia_encarregado_dados");

            entity.HasKey(e => e.Id).HasName("pk_pgia_encarregado_dados");
            entity.Property(e => e.Id).HasColumnName("id").UseIdentityByDefaultColumn();
            entity.Property(e => e.OrgaoId).HasColumnName("orgao_id");
            entity.Property(e => e.AgenteId).HasColumnName("agente_id");
            entity.Property(e => e.AtoTipo).HasColumnName("ato_tipo").HasMaxLength(30).IsRequired();
            entity.Property(e => e.AtoNumero).HasColumnName("ato_numero").HasMaxLength(30).IsRequired();
            entity.Property(e => e.AtoData).HasColumnName("ato_data");
            entity.Property(e => e.ProcessoSeiComunicacao).HasColumnName("processo_sei_comunicacao").HasMaxLength(25).IsRequired();
            entity.Property(e => e.DataComunicacaoSgdi).HasColumnName("data_comunicacao_sgdi");
            entity.Property(e => e.InicioVigencia).HasColumnName("inicio_vigencia");
            entity.Property(e => e.FimVigencia).HasColumnName("fim_vigencia");
            entity.Property(e => e.Ativo).HasColumnName("ativo").HasDefaultValue(true);
            entity.Property(e => e.CriadoEm).HasColumnName("criado_em").HasDefaultValueSql("NOW()");
            entity.Property(e => e.CriadoPor).HasColumnName("criado_por").HasMaxLength(200);
            entity.Property(e => e.AlteradoEm).HasColumnName("alterado_em");
            entity.Property(e => e.AlteradoPor).HasColumnName("alterado_por").HasMaxLength(200);

            if (indicesRelacionais)
            {
                entity.HasIndex(e => e.OrgaoId)
                    .IsUnique()
                    .HasFilter("ativo")
                    .HasDatabaseName("ux_pgia_encarregado_vigente");
            }
            entity.HasIndex(e => e.AgenteId).HasDatabaseName("ix_pgia_encarregado_agente");

            entity.HasOne(e => e.Orgao)
                .WithMany()
                .HasForeignKey(e => e.OrgaoId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_pgia_encarregado_orgao");

            entity.HasOne(e => e.Agente)
                .WithMany()
                .HasForeignKey(e => e.AgenteId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_pgia_encarregado_agente");
        });

        modelBuilder.Entity<PgiaPrazoConformidade>(entity =>
        {
            entity.ToTable("pgia_prazo_conformidade");

            entity.HasKey(p => p.Id).HasName("pk_pgia_prazo_conformidade");
            entity.Property(p => p.Id).HasColumnName("id").UseIdentityByDefaultColumn();
            entity.Property(p => p.Obrigacao).HasColumnName("obrigacao").IsRequired();
            entity.Property(p => p.BaseLegal).HasColumnName("base_legal").HasMaxLength(60).IsRequired();
            entity.Property(p => p.OrgaoId).HasColumnName("orgao_id");
            entity.Property(p => p.DataLimite).HasColumnName("data_limite");
            entity.Property(p => p.CumpridoEm).HasColumnName("cumprido_em");
            entity.Property(p => p.CriadoEm).HasColumnName("criado_em").HasDefaultValueSql("NOW()");
            entity.Property(p => p.CriadoPor).HasColumnName("criado_por").HasMaxLength(200);
            entity.Property(p => p.AlteradoEm).HasColumnName("alterado_em");
            entity.Property(p => p.AlteradoPor).HasColumnName("alterado_por").HasMaxLength(200);

            entity.HasIndex(p => new { p.OrgaoId, p.DataLimite }).HasDatabaseName("ix_pgia_prazo_orgao");

            entity.HasOne(p => p.Orgao)
                .WithMany()
                .HasForeignKey(p => p.OrgaoId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("fk_pgia_prazo_orgao");

            // Carga inicial: obrigações com prazo do decreto (art. 35 e correlatos).
            // Linhas com OrgaoId nulo servem de modelo; a aplicação instancia uma cópia
            // por órgão na adesão (as duas obrigações "SGDI:" são exclusivas da SGDI).
            entity.HasData(
                new PgiaPrazoConformidade { Id = 1, Obrigacao = "Designar e comunicar à SGDI o Responsável de IA", BaseLegal = "arts. 10, § 1º e 35, I", DataLimite = new DateOnly(2026, 8, 2), CriadoEm = SeedCriadoEm },
                new PgiaPrazoConformidade { Id = 2, Obrigacao = "Designar e comunicar à SGDI o Encarregado de Dados", BaseLegal = "art. 35, I", DataLimite = new DateOnly(2026, 8, 2), CriadoEm = SeedCriadoEm },
                new PgiaPrazoConformidade { Id = 3, Obrigacao = "Elaborar e remeter o inventário inicial de sistemas de IA", BaseLegal = "arts. 10, II e 35, II", DataLimite = new DateOnly(2026, 10, 1), CriadoEm = SeedCriadoEm },
                new PgiaPrazoConformidade { Id = 4, Obrigacao = "Incluir as trilhas ProCapIA/DF no plano de capacitação", BaseLegal = "arts. 29, § único e 35, III", DataLimite = new DateOnly(2026, 12, 30), CriadoEm = SeedCriadoEm },
                new PgiaPrazoConformidade { Id = 5, Obrigacao = "Revisar e adequar atos e contratos anteriores ao decreto", BaseLegal = "art. 37", DataLimite = new DateOnly(2026, 12, 30), CriadoEm = SeedCriadoEm },
                new PgiaPrazoConformidade { Id = 6, Obrigacao = "Concluir e publicar as AIA dos sistemas de Alto Risco em operação", BaseLegal = "art. 35, IV", DataLimite = new DateOnly(2027, 7, 3), CriadoEm = SeedCriadoEm },
                new PgiaPrazoConformidade { Id = 7, Obrigacao = "SGDI: elaborar o Guia de Contratações de IA, aprovado pelo CGTIC", BaseLegal = "art. 26, § 3º", DataLimite = new DateOnly(2026, 12, 30), CriadoEm = SeedCriadoEm },
                new PgiaPrazoConformidade { Id = 8, Obrigacao = "SGDI: publicar o Relatório Anual de Governança de IA", BaseLegal = "art. 33, caput", DataLimite = new DateOnly(2027, 3, 31), CriadoEm = SeedCriadoEm }
            );
        });

        modelBuilder.Entity<PgiaSistemaIa>(entity =>
        {
            entity.ToTable("pgia_sistema_ia", t =>
            {
                t.HasCheckConstraint("ck_pgia_sistema_origem",
                    "origem_registro IN ('Nova iniciativa','Instrumento vigente em revisão','Outros')");
                t.HasCheckConstraint("ck_pgia_sistema_origem_outros",
                    "origem_registro <> 'Outros' OR origem_registro_descricao IS NOT NULL");
                t.HasCheckConstraint("ck_pgia_sistema_tipo",
                    "tipo_sistema IN ('Desenvolvido internamente','Contratado','Embarcado em solução adquirida','Plataforma pública de IA generativa')");
                t.HasCheckConstraint("ck_pgia_sistema_tecnologia",
                    "tecnologia IN ('IA generativa','IA preditiva ou aprendizado de máquina','Visão computacional','Processamento de linguagem natural','Biometria','Outra')");
                t.HasCheckConstraint("ck_pgia_sistema_status",
                    "status_ciclo_vida IN ('Concepção','Planejamento','Em aquisição','Desenvolvimento','Treinamento','Testagem','Validação','Implantado (em uso)','Monitoramento','Descontinuado')");
                t.HasCheckConstraint("ck_pgia_sistema_escopo",
                    "escopo_dados IN ('Somente dados públicos','Dados pessoais','Dados pessoais sensíveis','Dados sigilosos')");
                t.HasCheckConstraint("ck_pgia_sistema_risco",
                    "classificacao_risco_atual IN ('Risco Excessivo','Alto Risco','Risco Moderado','Baixo Risco')");
                t.HasCheckConstraint("ck_pgia_sistema_homologacao",
                    "situacao_homologacao IN ('Aguardando avaliação da SGDI','Aguardando deliberação do CGTIC','Aprovado','Vetado')");
                t.HasCheckConstraint("ck_pgia_sistema_base_legal",
                    "base_legal_lgpd IN ('Execução de políticas públicas','Cumprimento de obrigação legal ou regulatória','Consentimento do titular','Proteção da vida','Tutela da saúde','Exercício regular de direitos','Outra hipótese dos arts. 7º e 11 da LGPD')");
            });

            entity.HasKey(s => s.Id).HasName("pk_pgia_sistema_ia");
            entity.Property(s => s.Id).HasColumnName("id").UseIdentityByDefaultColumn();
            entity.Property(s => s.OrgaoId).HasColumnName("orgao_id");
            entity.Property(s => s.Denominacao).HasColumnName("denominacao").IsRequired();
            entity.Property(s => s.Finalidade).HasColumnName("finalidade").IsRequired();
            // varchar(40) e não o varchar(20) do schema: "Instrumento vigente em revisão"
            // tem 30 caracteres e não caberia no tipo declarado lá
            entity.Property(s => s.OrigemRegistro).HasColumnName("origem_registro").HasMaxLength(40).IsRequired();
            entity.Property(s => s.OrigemRegistroDescricao).HasColumnName("origem_registro_descricao");
            entity.Property(s => s.TipoSistema).HasColumnName("tipo_sistema").HasMaxLength(40).IsRequired();
            entity.Property(s => s.Tecnologia).HasColumnName("tecnologia").HasMaxLength(40).IsRequired();
            entity.Property(s => s.StatusCicloVida).HasColumnName("status_ciclo_vida").HasMaxLength(30).IsRequired();
            entity.Property(s => s.DataImplantacao).HasColumnName("data_implantacao");
            entity.Property(s => s.DataDescontinuacao).HasColumnName("data_descontinuacao");
            entity.Property(s => s.EscopoDados).HasColumnName("escopo_dados").HasMaxLength(30).IsRequired();
            entity.Property(s => s.AfetaCidadao).HasColumnName("afeta_cidadao");
            entity.Property(s => s.NaturezaDecisoes).HasColumnName("natureza_decisoes");
            entity.Property(s => s.EfeitosCidadao).HasColumnName("efeitos_cidadao");
            entity.Property(s => s.ClassificacaoRiscoAtual).HasColumnName("classificacao_risco_atual").HasMaxLength(20);
            entity.Property(s => s.EnquadramentoLegal).HasColumnName("enquadramento_legal").HasMaxLength(40);
            entity.Property(s => s.SupervisaoHumanaDescricao).HasColumnName("supervisao_humana_descricao");
            entity.Property(s => s.AvisoInteracaoIa).HasColumnName("aviso_interacao_ia");
            entity.Property(s => s.IdentificadorAutenticidade).HasColumnName("identificador_autenticidade");
            entity.Property(s => s.BaseLegalLgpd).HasColumnName("base_legal_lgpd").HasMaxLength(60);
            entity.Property(s => s.CategoriasDadosPessoais).HasColumnName("categorias_dados_pessoais");
            entity.Property(s => s.FinalidadeTratamentoDados).HasColumnName("finalidade_tratamento_dados");
            entity.Property(s => s.MedidasSeguranca).HasColumnName("medidas_seguranca");
            entity.Property(s => s.InteroperavelPadroesSgdi).HasColumnName("interoperavel_padroes_sgdi");
            entity.Property(s => s.JustificativaNaoRedundancia).HasColumnName("justificativa_nao_redundancia");
            entity.Property(s => s.DataAnaliseSgtic).HasColumnName("data_analise_sgtic");
            entity.Property(s => s.ParecerSgtic).HasColumnName("parecer_sgtic");
            entity.Property(s => s.ResponsavelIaId).HasColumnName("responsavel_ia_id");
            entity.Property(s => s.ProcessoSei).HasColumnName("processo_sei").HasMaxLength(25);
            entity.Property(s => s.ComunicadoSgdiEm).HasColumnName("comunicado_sgdi_em");
            entity.Property(s => s.SituacaoHomologacao).HasColumnName("situacao_homologacao").HasMaxLength(40)
                .IsRequired().HasDefaultValue(PgiaDominios.SituacaoHomologacao.AguardandoSgdi);
            entity.Property(s => s.AvaliacaoParecer).HasColumnName("avaliacao_parecer");
            entity.Property(s => s.AvaliadoPor).HasColumnName("avaliado_por");
            entity.Property(s => s.AvaliadoEm).HasColumnName("avaliado_em");
            entity.Property(s => s.DeliberacaoHomologacaoId).HasColumnName("deliberacao_homologacao_id");
            entity.Property(s => s.PublicadoRegistroPublico).HasColumnName("publicado_registro_publico").HasDefaultValue(false);
            entity.Property(s => s.DataPublicacaoRegistro).HasColumnName("data_publicacao_registro");
            entity.Property(s => s.CriadoEm).HasColumnName("criado_em").HasDefaultValueSql("NOW()");
            entity.Property(s => s.CriadoPor).HasColumnName("criado_por").HasMaxLength(200);
            entity.Property(s => s.AlteradoEm).HasColumnName("alterado_em");
            entity.Property(s => s.AlteradoPor).HasColumnName("alterado_por").HasMaxLength(200);

            // Uma denominação por órgão (ux_sistema_orgao_nome do schema)
            entity.HasIndex(s => new { s.OrgaoId, s.Denominacao })
                .IsUnique()
                .HasDatabaseName("ux_pgia_sistema_orgao_nome");
            entity.HasIndex(s => s.OrgaoId).HasDatabaseName("ix_pgia_sistema_orgao");
            entity.HasIndex(s => s.ClassificacaoRiscoAtual).HasDatabaseName("ix_pgia_sistema_classificacao");
            entity.HasIndex(s => s.ResponsavelIaId).HasDatabaseName("ix_pgia_sistema_responsavel");

            entity.HasOne(s => s.Orgao)
                .WithMany()
                .HasForeignKey(s => s.OrgaoId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_pgia_sistema_orgao");

            entity.HasOne(s => s.Responsavel)
                .WithMany()
                .HasForeignKey(s => s.ResponsavelIaId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_pgia_sistema_responsavel");

            entity.HasIndex(s => s.AvaliadoPor).HasDatabaseName("ix_pgia_sistema_avaliador");
            entity.HasIndex(s => s.DeliberacaoHomologacaoId).HasDatabaseName("ix_pgia_sistema_delib_homolog");

            entity.HasOne(s => s.AvaliadoPorUser)
                .WithMany()
                .HasForeignKey(s => s.AvaliadoPor)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_pgia_sistema_avaliador");

            entity.HasOne<PgiaDeliberacaoCgtic>()
                .WithMany()
                .HasForeignKey(s => s.DeliberacaoHomologacaoId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_pgia_sistema_deliberacao_homolog");
        });

        modelBuilder.Entity<PgiaClassificacaoRisco>(entity =>
        {
            entity.ToTable("pgia_classificacao_risco", t =>
            {
                t.HasCheckConstraint("ck_pgia_classificacao_motivo",
                    "motivo IN ('Classificação inicial','Nova contratação','Revisão de instrumento vigente','Reclassificação por resolução do CGTIC','Revisão periódica')");
                t.HasCheckConstraint("ck_pgia_classificacao_resultado",
                    "resultado IN ('Risco Excessivo','Alto Risco','Risco Moderado','Baixo Risco')");
                t.HasCheckConstraint("ck_pgia_classificacao_enquadramento",
                    "resultado = 'Baixo Risco' OR enquadramento_legal IS NOT NULL");
            });

            entity.HasKey(c => c.Id).HasName("pk_pgia_classificacao_risco");
            entity.Property(c => c.Id).HasColumnName("id").UseIdentityByDefaultColumn();
            entity.Property(c => c.SistemaIaId).HasColumnName("sistema_ia_id");
            entity.Property(c => c.DataClassificacao).HasColumnName("data_classificacao");
            entity.Property(c => c.Motivo).HasColumnName("motivo").HasMaxLength(40).IsRequired();
            entity.Property(c => c.RespostasChecklist).HasColumnName("respostas_checklist").HasColumnType("jsonb").IsRequired();
            entity.Property(c => c.Resultado).HasColumnName("resultado").HasMaxLength(20).IsRequired();
            entity.Property(c => c.Pontuacao).HasColumnName("pontuacao_risco");
            entity.Property(c => c.EnquadramentoLegal).HasColumnName("enquadramento_legal").HasMaxLength(40);
            entity.Property(c => c.Justificativa).HasColumnName("justificativa").IsRequired();
            entity.Property(c => c.ClassificadoPor).HasColumnName("classificado_por");
            entity.Property(c => c.DeliberacaoCgticId).HasColumnName("deliberacao_cgtic_id");
            entity.Property(c => c.CriadoEm).HasColumnName("criado_em").HasDefaultValueSql("NOW()");
            entity.Property(c => c.CriadoPor).HasColumnName("criado_por").HasMaxLength(200);
            entity.Property(c => c.AlteradoEm).HasColumnName("alterado_em");
            entity.Property(c => c.AlteradoPor).HasColumnName("alterado_por").HasMaxLength(200);

            entity.HasIndex(c => c.SistemaIaId).HasDatabaseName("ix_pgia_classificacao_sistema");
            entity.HasIndex(c => c.ClassificadoPor).HasDatabaseName("ix_pgia_classificacao_agente");

            entity.HasOne(c => c.Sistema)
                .WithMany()
                .HasForeignKey(c => c.SistemaIaId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_pgia_classificacao_sistema");

            entity.HasOne(c => c.ClassificadoPorUser)
                .WithMany()
                .HasForeignKey(c => c.ClassificadoPor)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_pgia_classificacao_agente");

            entity.HasIndex(c => c.DeliberacaoCgticId).HasDatabaseName("ix_pgia_classificacao_deliberacao");

            entity.HasOne<PgiaDeliberacaoCgtic>()
                .WithMany()
                .HasForeignKey(c => c.DeliberacaoCgticId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_pgia_classificacao_deliberacao");
        });

        modelBuilder.Entity<PgiaDocumento>(entity =>
        {
            entity.ToTable("pgia_documento", t =>
            {
                t.HasCheckConstraint("ck_pgia_documento_tipo",
                    "tipo IN ('Ato de designação','AIA','RIPD','Ata ou deliberação','Relatório semestral','Relatório anual','Certificado de capacitação','Contrato','Termo aditivo','Parecer de auditoria','Avaliação de riscos','Comprovação de triagem','Outro')");
            });

            entity.HasKey(d => d.Id).HasName("pk_pgia_documento");
            entity.Property(d => d.Id).HasColumnName("id").UseIdentityByDefaultColumn();
            entity.Property(d => d.Tipo).HasColumnName("tipo").HasMaxLength(40).IsRequired();
            entity.Property(d => d.OrgaoId).HasColumnName("orgao_id");
            entity.Property(d => d.SistemaIaId).HasColumnName("sistema_ia_id");
            entity.Property(d => d.ProcessoSei).HasColumnName("processo_sei").HasMaxLength(25);
            entity.Property(d => d.NomeArquivo).HasColumnName("nome_arquivo").IsRequired();
            entity.Property(d => d.UrlStorage).HasColumnName("url_storage");
            entity.Property(d => d.DataEnvio).HasColumnName("data_envio").HasDefaultValueSql("NOW()");
            entity.Property(d => d.EnviadoPor).HasColumnName("enviado_por");
            entity.Property(d => d.CriadoEm).HasColumnName("criado_em").HasDefaultValueSql("NOW()");
            entity.Property(d => d.CriadoPor).HasColumnName("criado_por").HasMaxLength(200);
            entity.Property(d => d.AlteradoEm).HasColumnName("alterado_em");
            entity.Property(d => d.AlteradoPor).HasColumnName("alterado_por").HasMaxLength(200);

            entity.HasIndex(d => d.OrgaoId).HasDatabaseName("ix_pgia_documento_orgao");
            entity.HasIndex(d => d.SistemaIaId).HasDatabaseName("ix_pgia_documento_sistema");
            entity.HasIndex(d => d.EnviadoPor).HasDatabaseName("ix_pgia_documento_agente");

            entity.HasOne(d => d.Orgao)
                .WithMany()
                .HasForeignKey(d => d.OrgaoId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_pgia_documento_orgao");

            entity.HasOne(d => d.Sistema)
                .WithMany()
                .HasForeignKey(d => d.SistemaIaId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_pgia_documento_sistema");

            entity.HasOne(d => d.EnviadoPorUser)
                .WithMany()
                .HasForeignKey(d => d.EnviadoPor)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_pgia_documento_agente");
        });

        // ── Fase 2: governança central ────────────────────────────────────────

        modelBuilder.Entity<PgiaDeliberacaoCgtic>(entity =>
        {
            entity.ToTable("pgia_deliberacao_cgtic", t =>
            {
                t.HasCheckConstraint("ck_pgia_deliberacao_tipo",
                    "tipo IN ('Classificação de sistema como Alto Risco','Aprovação de aquisição de Alto Risco','Critérios e requisitos de aquisição','Constituição de grupo de trabalho temático','Apreciação do Relatório Anual','Resolução normativa','Autorização de treinamento com dados do GDF','Ampliação do rol de Alto Risco','Aprovação do Guia de Contratações','Suspensão de sistema')");
                t.HasCheckConstraint("ck_pgia_deliberacao_resultado",
                    "resultado IN ('Favorável','Desfavorável','Em diligência')");
            });

            entity.HasKey(d => d.Id).HasName("pk_pgia_deliberacao_cgtic");
            entity.Property(d => d.Id).HasColumnName("id").UseIdentityByDefaultColumn();
            entity.Property(d => d.Tipo).HasColumnName("tipo").HasMaxLength(50).IsRequired();
            entity.Property(d => d.SistemaIaId).HasColumnName("sistema_ia_id");
            entity.Property(d => d.ContratoId).HasColumnName("contrato_id");
            entity.Property(d => d.DataDeliberacao).HasColumnName("data_deliberacao");
            entity.Property(d => d.Resultado).HasColumnName("resultado").HasMaxLength(20).IsRequired();
            entity.Property(d => d.NumeroAto).HasColumnName("numero_ato").HasMaxLength(30);
            entity.Property(d => d.Ementa).HasColumnName("ementa");
            entity.Property(d => d.DocumentoId).HasColumnName("documento_id");
            entity.Property(d => d.CriadoEm).HasColumnName("criado_em").HasDefaultValueSql("NOW()");
            entity.Property(d => d.CriadoPor).HasColumnName("criado_por").HasMaxLength(200);
            entity.Property(d => d.AlteradoEm).HasColumnName("alterado_em");
            entity.Property(d => d.AlteradoPor).HasColumnName("alterado_por").HasMaxLength(200);

            entity.HasIndex(d => d.SistemaIaId).HasDatabaseName("ix_pgia_deliberacao_sistema");

            entity.HasOne(d => d.Sistema)
                .WithMany()
                .HasForeignKey(d => d.SistemaIaId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_pgia_deliberacao_sistema");

            entity.HasIndex(d => d.ContratoId).HasDatabaseName("ix_pgia_deliberacao_contrato");

            entity.HasOne<PgiaContratoIa>()
                .WithMany()
                .HasForeignKey(d => d.ContratoId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_pgia_deliberacao_contrato");

            entity.HasIndex(d => d.DocumentoId).HasDatabaseName("ix_pgia_deliberacao_doc");

            entity.HasOne<PgiaDocumento>()
                .WithMany()
                .HasForeignKey(d => d.DocumentoId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_pgia_deliberacao_doc");
        });

        modelBuilder.Entity<PgiaAia>(entity =>
        {
            entity.ToTable("pgia_aia", t =>
            {
                t.HasCheckConstraint("ck_pgia_aia_status",
                    "status IN ('Não iniciada','Em elaboração','Concluída','Em revisão')");
            });

            entity.HasKey(a => a.Id).HasName("pk_pgia_aia");
            entity.Property(a => a.Id).HasColumnName("id").UseIdentityByDefaultColumn();
            entity.Property(a => a.SistemaIaId).HasColumnName("sistema_ia_id");
            entity.Property(a => a.Status).HasColumnName("status").HasMaxLength(30)
                .IsRequired().HasDefaultValue(PgiaDominios.StatusAia.NaoIniciada);
            entity.Property(a => a.DataInicio).HasColumnName("data_inicio");
            entity.Property(a => a.DataConclusao).HasColumnName("data_conclusao");
            entity.Property(a => a.ImpactosDireitosFundamentais).HasColumnName("impactos_direitos_fundamentais").IsRequired();
            entity.Property(a => a.MedidasPreventivas).HasColumnName("medidas_preventivas").IsRequired();
            entity.Property(a => a.MedidasMitigadoras).HasColumnName("medidas_mitigadoras").IsRequired();
            entity.Property(a => a.MedidasReversao).HasColumnName("medidas_reversao").IsRequired();
            entity.Property(a => a.PreviaLicitacao).HasColumnName("previa_licitacao");
            entity.Property(a => a.ConjuntaRipd).HasColumnName("conjunta_ripd").HasDefaultValue(false);
            entity.Property(a => a.RipdDocumentoId).HasColumnName("ripd_documento_id");
            entity.Property(a => a.ElaboradaPor).HasColumnName("elaborada_por");
            entity.Property(a => a.DocumentoId).HasColumnName("documento_id");
            entity.Property(a => a.DeliberacaoCgticId).HasColumnName("deliberacao_cgtic_id");
            entity.Property(a => a.PublicadaPortal).HasColumnName("publicada_portal").HasDefaultValue(false);
            entity.Property(a => a.DataPublicacaoPortal).HasColumnName("data_publicacao_portal");
            entity.Property(a => a.UrlPublicacao).HasColumnName("url_publicacao");
            entity.Property(a => a.ProximaRevisao).HasColumnName("proxima_revisao");
            entity.Property(a => a.CriadoEm).HasColumnName("criado_em").HasDefaultValueSql("NOW()");
            entity.Property(a => a.CriadoPor).HasColumnName("criado_por").HasMaxLength(200);
            entity.Property(a => a.AlteradoEm).HasColumnName("alterado_em");
            entity.Property(a => a.AlteradoPor).HasColumnName("alterado_por").HasMaxLength(200);

            entity.HasIndex(a => a.SistemaIaId).HasDatabaseName("ix_pgia_aia_sistema");

            entity.HasOne(a => a.Sistema)
                .WithMany()
                .HasForeignKey(a => a.SistemaIaId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_pgia_aia_sistema");

            entity.HasIndex(a => a.ElaboradaPor).HasDatabaseName("ix_pgia_aia_agente");
            entity.HasIndex(a => a.DeliberacaoCgticId).HasDatabaseName("ix_pgia_aia_deliberacao");
            entity.HasIndex(a => a.RipdDocumentoId).HasDatabaseName("ix_pgia_aia_ripd_doc");
            entity.HasIndex(a => a.DocumentoId).HasDatabaseName("ix_pgia_aia_doc");

            entity.HasOne(a => a.ElaboradaPorUser)
                .WithMany()
                .HasForeignKey(a => a.ElaboradaPor)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_pgia_aia_agente");

            entity.HasOne(a => a.Deliberacao)
                .WithMany()
                .HasForeignKey(a => a.DeliberacaoCgticId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_pgia_aia_deliberacao");

            // Metadados de documento (pgia_documento) sem navegação: só o vínculo importa
            entity.HasOne<PgiaDocumento>()
                .WithMany()
                .HasForeignKey(a => a.RipdDocumentoId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_pgia_aia_ripd_doc");

            entity.HasOne<PgiaDocumento>()
                .WithMany()
                .HasForeignKey(a => a.DocumentoId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_pgia_aia_doc");
        });

        modelBuilder.Entity<PgiaPlataformaIaGenerativa>(entity =>
        {
            entity.ToTable("pgia_plataforma_ia_generativa", t =>
            {
                t.HasCheckConstraint("ck_pgia_plataforma_status",
                    "status_homologacao IN ('Em avaliação','Homologada','Homologada apta a dados pessoais e sigilosos','Não homologada','Homologação revogada')");
            });

            entity.HasKey(p => p.Id).HasName("pk_pgia_plataforma_ia_generativa");
            entity.Property(p => p.Id).HasColumnName("id").UseIdentityByDefaultColumn();
            entity.Property(p => p.Nome).HasColumnName("nome").IsRequired();
            entity.Property(p => p.Fornecedor).HasColumnName("fornecedor");
            entity.Property(p => p.Url).HasColumnName("url");
            entity.Property(p => p.StatusHomologacao).HasColumnName("status_homologacao").HasMaxLength(50)
                .IsRequired().HasDefaultValue("Em avaliação");
            entity.Property(p => p.AptaDadosPessoaisSigilosos).HasColumnName("apta_dados_pessoais_sigilosos").HasDefaultValue(false);
            entity.Property(p => p.AtoHomologacao).HasColumnName("ato_homologacao").HasMaxLength(30);
            entity.Property(p => p.DataAto).HasColumnName("data_ato");
            entity.Property(p => p.PublicadaRelacaoEm).HasColumnName("publicada_relacao_em");
            entity.Property(p => p.DiretrizesUso).HasColumnName("diretrizes_uso");
            entity.Property(p => p.CriadoEm).HasColumnName("criado_em").HasDefaultValueSql("NOW()");
            entity.Property(p => p.CriadoPor).HasColumnName("criado_por").HasMaxLength(200);
            entity.Property(p => p.AlteradoEm).HasColumnName("alterado_em");
            entity.Property(p => p.AlteradoPor).HasColumnName("alterado_por").HasMaxLength(200);

            entity.HasIndex(p => p.Nome).IsUnique().HasDatabaseName("ux_pgia_plataforma_nome");
        });

        modelBuilder.Entity<PgiaAutorizacaoExcepcional>(entity =>
        {
            entity.ToTable("pgia_autorizacao_excepcional", t =>
            {
                t.HasCheckConstraint("ck_pgia_autorizacao_tipo",
                    "tipo IN ('Uso de plataforma pública com dados não públicos','Uso de dados do GDF para treinamento pelo fornecedor')");
                t.HasCheckConstraint("ck_pgia_autorizacao_autorizada_por",
                    "autorizada_por IN ('SGDI','Órgão com aprovação do CGTIC')");
            });

            entity.HasKey(a => a.Id).HasName("pk_pgia_autorizacao_excepcional");
            entity.Property(a => a.Id).HasColumnName("id").UseIdentityByDefaultColumn();
            entity.Property(a => a.Tipo).HasColumnName("tipo").HasMaxLength(60).IsRequired();
            entity.Property(a => a.OrgaoId).HasColumnName("orgao_id");
            entity.Property(a => a.PlataformaId).HasColumnName("plataforma_id");
            entity.Property(a => a.ContratoId).HasColumnName("contrato_id");
            entity.Property(a => a.Justificativa).HasColumnName("justificativa").IsRequired();
            entity.Property(a => a.AvaliacaoRiscosDocId).HasColumnName("avaliacao_riscos_doc_id");
            // varchar(30) e não o varchar(20) do schema: "Órgão com aprovação do CGTIC"
            // tem 28 caracteres e não caberia no tipo declarado lá (mesmo caso de origem_registro)
            entity.Property(a => a.AutorizadaPor).HasColumnName("autorizada_por").HasMaxLength(30).IsRequired();
            entity.Property(a => a.DeliberacaoCgticId).HasColumnName("deliberacao_cgtic_id");
            entity.Property(a => a.DataAutorizacao).HasColumnName("data_autorizacao");
            entity.Property(a => a.VigenciaFim).HasColumnName("vigencia_fim");
            entity.Property(a => a.Ativo).HasColumnName("ativo").HasDefaultValue(true);
            entity.Property(a => a.CriadoEm).HasColumnName("criado_em").HasDefaultValueSql("NOW()");
            entity.Property(a => a.CriadoPor).HasColumnName("criado_por").HasMaxLength(200);
            entity.Property(a => a.AlteradoEm).HasColumnName("alterado_em");
            entity.Property(a => a.AlteradoPor).HasColumnName("alterado_por").HasMaxLength(200);

            entity.HasIndex(a => a.OrgaoId).HasDatabaseName("ix_pgia_autorizacao_orgao");
            entity.HasIndex(a => a.ContratoId).HasDatabaseName("ix_pgia_autorizacao_contrato");

            entity.HasOne(a => a.Orgao)
                .WithMany()
                .HasForeignKey(a => a.OrgaoId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_pgia_autorizacao_orgao");

            entity.HasOne<PgiaContratoIa>()
                .WithMany()
                .HasForeignKey(a => a.ContratoId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_pgia_autorizacao_contrato");

            entity.HasIndex(a => a.PlataformaId).HasDatabaseName("ix_pgia_autorizacao_plataforma");
            entity.HasIndex(a => a.DeliberacaoCgticId).HasDatabaseName("ix_pgia_autorizacao_deliberacao");
            entity.HasIndex(a => a.AvaliacaoRiscosDocId).HasDatabaseName("ix_pgia_autorizacao_doc");

            entity.HasOne(a => a.Plataforma)
                .WithMany()
                .HasForeignKey(a => a.PlataformaId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_pgia_autorizacao_plataforma");

            entity.HasOne<PgiaDeliberacaoCgtic>()
                .WithMany()
                .HasForeignKey(a => a.DeliberacaoCgticId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_pgia_autorizacao_deliberacao");

            entity.HasOne<PgiaDocumento>()
                .WithMany()
                .HasForeignKey(a => a.AvaliacaoRiscosDocId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_pgia_autorizacao_doc");
        });

        modelBuilder.Entity<PgiaNormaComplementar>(entity =>
        {
            entity.ToTable("pgia_norma_complementar", t =>
            {
                t.HasCheckConstraint("ck_pgia_norma_tipo",
                    "tipo IN ('Resolução do CGTIC','Instrução normativa da SGDI','Norma técnica ou guia','Guia de Contratações de IA','Recomendação técnica')");
                t.HasCheckConstraint("ck_pgia_norma_emissor", "emissor IN ('SGDI','CGTIC')");
            });

            entity.HasKey(n => n.Id).HasName("pk_pgia_norma_complementar");
            entity.Property(n => n.Id).HasColumnName("id").UseIdentityByDefaultColumn();
            entity.Property(n => n.Tipo).HasColumnName("tipo").HasMaxLength(40).IsRequired();
            entity.Property(n => n.Numero).HasColumnName("numero").HasMaxLength(30);
            entity.Property(n => n.Ementa).HasColumnName("ementa").IsRequired();
            entity.Property(n => n.Emissor).HasColumnName("emissor").HasMaxLength(20).IsRequired();
            entity.Property(n => n.DataPublicacao).HasColumnName("data_publicacao");
            entity.Property(n => n.Url).HasColumnName("url");
            entity.Property(n => n.Vigente).HasColumnName("vigente").HasDefaultValue(true);
            entity.Property(n => n.AprovadaCgticEm).HasColumnName("aprovada_cgtic_em");
            entity.Property(n => n.CriadoEm).HasColumnName("criado_em").HasDefaultValueSql("NOW()");
            entity.Property(n => n.CriadoPor).HasColumnName("criado_por").HasMaxLength(200);
            entity.Property(n => n.AlteradoEm).HasColumnName("alterado_em");
            entity.Property(n => n.AlteradoPor).HasColumnName("alterado_por").HasMaxLength(200);
        });

        // ── Fase 3: operação contínua ─────────────────────────────────────────

        modelBuilder.Entity<PgiaIncidente>(entity =>
        {
            entity.ToTable("pgia_incidente", t =>
            {
                // NULL passa no CHECK: o aviso do agente (art. 13, II) nasce sem hipótese
                t.HasCheckConstraint("ck_pgia_incidente_hipotese", "hipotese IN ('I','II','III','IV','V')");
                t.HasCheckConstraint("ck_pgia_incidente_status",
                    "status_apuracao IN ('Recebida','Em apuração','Concluída com recomendações','Concluída sem medidas','Encaminhada ao CGTIC')");
            });

            entity.HasKey(i => i.Id).HasName("pk_pgia_incidente");
            entity.Property(i => i.Id).HasColumnName("id").UseIdentityByDefaultColumn();
            entity.Property(i => i.SistemaIaId).HasColumnName("sistema_ia_id");
            entity.Property(i => i.OrgaoId).HasColumnName("orgao_id");
            // Divergência registrada do schema (NOT NULL lá): ver PgiaIncidente.Hipotese
            entity.Property(i => i.Hipotese).HasColumnName("hipotese").HasMaxLength(60);
            entity.Property(i => i.Descricao).HasColumnName("descricao").IsRequired();
            entity.Property(i => i.DataOcorrencia).HasColumnName("data_ocorrencia");
            entity.Property(i => i.DataDeteccao).HasColumnName("data_deteccao");
            entity.Property(i => i.NotificadoResponsavelEm).HasColumnName("notificado_responsavel_em");
            entity.Property(i => i.ComunicadoPor).HasColumnName("comunicado_por");
            // Nulas até a comunicação formal à SGDI (mesma divergência da hipótese)
            entity.Property(i => i.DataComunicacaoSgdi).HasColumnName("data_comunicacao_sgdi");
            entity.Property(i => i.ProcessoSei).HasColumnName("processo_sei").HasMaxLength(25);
            entity.Property(i => i.StatusApuracao).HasColumnName("status_apuracao").HasMaxLength(40)
                .IsRequired().HasDefaultValue(PgiaDominios.StatusApuracao.Recebida);
            entity.Property(i => i.RecomendacoesSgdi).HasColumnName("recomendacoes_sgdi");
            entity.Property(i => i.PropostaCgtic).HasColumnName("proposta_cgtic");
            entity.Property(i => i.EncaminhadoOrgaoCompetenteEm).HasColumnName("encaminhado_orgao_competente_em");
            entity.Property(i => i.SistemaSuspenso).HasColumnName("sistema_suspenso").HasDefaultValue(false);
            entity.Property(i => i.DataSuspensao).HasColumnName("data_suspensao");
            entity.Property(i => i.MedidasAdotadas).HasColumnName("medidas_adotadas");
            entity.Property(i => i.CriadoEm).HasColumnName("criado_em").HasDefaultValueSql("NOW()");
            entity.Property(i => i.CriadoPor).HasColumnName("criado_por").HasMaxLength(200);
            entity.Property(i => i.AlteradoEm).HasColumnName("alterado_em");
            entity.Property(i => i.AlteradoPor).HasColumnName("alterado_por").HasMaxLength(200);

            entity.HasIndex(i => i.SistemaIaId).HasDatabaseName("ix_pgia_incidente_sistema");
            entity.HasIndex(i => i.OrgaoId).HasDatabaseName("ix_pgia_incidente_orgao");
            entity.HasIndex(i => i.ComunicadoPor).HasDatabaseName("ix_pgia_incidente_agente");

            entity.HasOne(i => i.Sistema)
                .WithMany()
                .HasForeignKey(i => i.SistemaIaId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_pgia_incidente_sistema");

            entity.HasOne(i => i.Orgao)
                .WithMany()
                .HasForeignKey(i => i.OrgaoId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_pgia_incidente_orgao");

            entity.HasOne(i => i.ComunicadoPorUser)
                .WithMany()
                .HasForeignKey(i => i.ComunicadoPor)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_pgia_incidente_agente");
        });

        modelBuilder.Entity<PgiaNaoConformidade>(entity =>
        {
            entity.ToTable("pgia_nao_conformidade", t =>
            {
                t.HasCheckConstraint("ck_pgia_nc_origem",
                    "origem IN ('Acompanhamento do SGTIC','Supervisão da SGDI')");
                t.HasCheckConstraint("ck_pgia_nc_situacao",
                    "situacao IN ('Registrada','Reportada à SGDI','Em tratamento','Sanada','Comunicada ao controle interno')");
            });

            entity.HasKey(n => n.Id).HasName("pk_pgia_nao_conformidade");
            entity.Property(n => n.Id).HasColumnName("id").UseIdentityByDefaultColumn();
            entity.Property(n => n.OrgaoId).HasColumnName("orgao_id");
            entity.Property(n => n.Descricao).HasColumnName("descricao").IsRequired();
            entity.Property(n => n.Origem).HasColumnName("origem").HasMaxLength(30).IsRequired();
            entity.Property(n => n.DataRegistro).HasColumnName("data_registro");
            entity.Property(n => n.ReportadaSgdiEm).HasColumnName("reportada_sgdi_em");
            entity.Property(n => n.Situacao).HasColumnName("situacao").HasMaxLength(40)
                .IsRequired().HasDefaultValue(PgiaDominios.SituacaoNaoConformidade.Registrada);
            entity.Property(n => n.ComunicadaControleInternoEm).HasColumnName("comunicada_controle_interno_em");
            entity.Property(n => n.CriadoEm).HasColumnName("criado_em").HasDefaultValueSql("NOW()");
            entity.Property(n => n.CriadoPor).HasColumnName("criado_por").HasMaxLength(200);
            entity.Property(n => n.AlteradoEm).HasColumnName("alterado_em");
            entity.Property(n => n.AlteradoPor).HasColumnName("alterado_por").HasMaxLength(200);

            entity.HasIndex(n => n.OrgaoId).HasDatabaseName("ix_pgia_nc_orgao");

            entity.HasOne(n => n.Orgao)
                .WithMany()
                .HasForeignKey(n => n.OrgaoId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_pgia_nc_orgao");
        });

        modelBuilder.Entity<PgiaCapacitacao>(entity =>
        {
            entity.ToTable("pgia_capacitacao", t =>
            {
                t.HasCheckConstraint("ck_pgia_capacitacao_trilha",
                    "trilha IN ('Letramento em IA','Uso responsável de IA','Governança de IA','Desenvolvimento e auditoria de sistemas de IA')");
                t.HasCheckConstraint("ck_pgia_capacitacao_status",
                    "status IN ('Prevista','Em andamento','Concluída')");
            });

            entity.HasKey(c => c.Id).HasName("pk_pgia_capacitacao");
            entity.Property(c => c.Id).HasColumnName("id").UseIdentityByDefaultColumn();
            entity.Property(c => c.AgenteId).HasColumnName("agente_id");
            entity.Property(c => c.OrgaoId).HasColumnName("orgao_id");
            entity.Property(c => c.Trilha).HasColumnName("trilha").HasMaxLength(50).IsRequired();
            entity.Property(c => c.Status).HasColumnName("status").HasMaxLength(20)
                .IsRequired().HasDefaultValue("Prevista");
            entity.Property(c => c.DataConclusao).HasColumnName("data_conclusao");
            entity.Property(c => c.PrevistaPlanoCapacitacao).HasColumnName("prevista_plano_capacitacao").HasDefaultValue(false);
            entity.Property(c => c.CertificadoDocId).HasColumnName("certificado_doc_id");
            entity.Property(c => c.CriadoEm).HasColumnName("criado_em").HasDefaultValueSql("NOW()");
            entity.Property(c => c.CriadoPor).HasColumnName("criado_por").HasMaxLength(200);
            entity.Property(c => c.AlteradoEm).HasColumnName("alterado_em");
            entity.Property(c => c.AlteradoPor).HasColumnName("alterado_por").HasMaxLength(200);

            // Uma linha por par agente + trilha (ux_capacitacao do schema)
            entity.HasIndex(c => new { c.AgenteId, c.Trilha }).IsUnique().HasDatabaseName("ux_pgia_capacitacao");
            entity.HasIndex(c => c.OrgaoId).HasDatabaseName("ix_pgia_capacitacao_orgao");
            entity.HasIndex(c => c.CertificadoDocId).HasDatabaseName("ix_pgia_capacitacao_doc");

            entity.HasOne(c => c.Agente)
                .WithMany()
                .HasForeignKey(c => c.AgenteId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_pgia_capacitacao_agente");

            entity.HasOne(c => c.Orgao)
                .WithMany()
                .HasForeignKey(c => c.OrgaoId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_pgia_capacitacao_orgao");

            entity.HasOne<PgiaDocumento>()
                .WithMany()
                .HasForeignKey(c => c.CertificadoDocId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_pgia_capacitacao_doc");
        });

        modelBuilder.Entity<PgiaRegistroUsoIa>(entity =>
        {
            entity.ToTable("pgia_registro_uso_ia", t =>
            {
                // Exatamente uma fonte. CHECK relacional: o provider InMemory dos testes
                // não o reproduz, então a regra também é validada no service.
                t.HasCheckConstraint("ck_pgia_uso_fonte", "num_nonnulls(sistema_ia_id, plataforma_id) = 1");
            });

            entity.HasKey(u => u.Id).HasName("pk_pgia_registro_uso_ia");
            entity.Property(u => u.Id).HasColumnName("id").UseIdentityByDefaultColumn();
            entity.Property(u => u.AgenteId).HasColumnName("agente_id");
            entity.Property(u => u.OrgaoId).HasColumnName("orgao_id");
            entity.Property(u => u.SistemaIaId).HasColumnName("sistema_ia_id");
            entity.Property(u => u.PlataformaId).HasColumnName("plataforma_id");
            entity.Property(u => u.ProdutoRef).HasColumnName("produto_ref").IsRequired();
            entity.Property(u => u.ProcessoSei).HasColumnName("processo_sei").HasMaxLength(25);
            entity.Property(u => u.DataUso).HasColumnName("data_uso");
            entity.Property(u => u.RevisaoHumanaConfirmada).HasColumnName("revisao_humana_confirmada");
            entity.Property(u => u.CriadoEm).HasColumnName("criado_em").HasDefaultValueSql("NOW()");
            entity.Property(u => u.CriadoPor).HasColumnName("criado_por").HasMaxLength(200);
            entity.Property(u => u.AlteradoEm).HasColumnName("alterado_em");
            entity.Property(u => u.AlteradoPor).HasColumnName("alterado_por").HasMaxLength(200);

            entity.HasIndex(u => u.AgenteId).HasDatabaseName("ix_pgia_uso_agente");
            entity.HasIndex(u => u.OrgaoId).HasDatabaseName("ix_pgia_uso_orgao");

            entity.HasOne(u => u.Agente)
                .WithMany()
                .HasForeignKey(u => u.AgenteId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_pgia_uso_agente");

            entity.HasOne(u => u.Orgao)
                .WithMany()
                .HasForeignKey(u => u.OrgaoId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_pgia_uso_orgao");

            entity.HasIndex(u => u.SistemaIaId).HasDatabaseName("ix_pgia_uso_sistema");
            entity.HasIndex(u => u.PlataformaId).HasDatabaseName("ix_pgia_uso_plataforma");

            entity.HasOne(u => u.Sistema)
                .WithMany()
                .HasForeignKey(u => u.SistemaIaId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_pgia_uso_sistema");

            entity.HasOne(u => u.Plataforma)
                .WithMany()
                .HasForeignKey(u => u.PlataformaId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_pgia_uso_plataforma");
        });

        // ── Fase 4: contratações, relatórios e auditorias ─────────────────────

        modelBuilder.Entity<PgiaContratoIa>(entity =>
        {
            entity.ToTable("pgia_contrato_ia", t =>
            {
                t.HasCheckConstraint("ck_pgia_contrato_status", "status IN ('Vigente','Suspenso','Encerrado')");
            });

            entity.HasKey(c => c.Id).HasName("pk_pgia_contrato_ia");
            entity.Property(c => c.Id).HasColumnName("id").UseIdentityByDefaultColumn();
            entity.Property(c => c.OrgaoId).HasColumnName("orgao_id");
            // Sem a coluna espelho sistema_ia.contrato_id do schema: o vínculo vive só aqui
            entity.Property(c => c.SistemaIaId).HasColumnName("sistema_ia_id");
            entity.Property(c => c.NumeroContrato).HasColumnName("numero_contrato").HasMaxLength(30).IsRequired();
            entity.Property(c => c.ProcessoSei).HasColumnName("processo_sei").HasMaxLength(25).IsRequired();
            entity.Property(c => c.Objeto).HasColumnName("objeto").IsRequired();
            entity.Property(c => c.FornecedorNome).HasColumnName("fornecedor_nome").IsRequired();
            entity.Property(c => c.Status).HasColumnName("status").HasMaxLength(20)
                .IsRequired().HasDefaultValue("Vigente");
            entity.Property(c => c.PrevistoPdtic).HasColumnName("previsto_pdtic");
            entity.Property(c => c.HomologacaoSgdiDocId).HasColumnName("homologacao_sgdi_doc_id");
            entity.Property(c => c.ClausulaVedacaoTreinamento).HasColumnName("clausula_vedacao_treinamento");
            entity.Property(c => c.AutorizacaoTreinamentoId).HasColumnName("autorizacao_treinamento_id");
            entity.Property(c => c.ReqExplicabilidade).HasColumnName("req_explicabilidade");
            entity.Property(c => c.ReqAuditabilidade).HasColumnName("req_auditabilidade");
            entity.Property(c => c.ReqPortabilidade).HasColumnName("req_portabilidade");
            entity.Property(c => c.ReqSemAprisionamento).HasColumnName("req_sem_aprisionamento");
            entity.Property(c => c.ReqAcessibilidade).HasColumnName("req_acessibilidade");
            entity.Property(c => c.ClausulaAuditoriaIndependente).HasColumnName("clausula_auditoria_independente");
            entity.Property(c => c.SlaDesempenho).HasColumnName("sla_desempenho");
            entity.Property(c => c.SlaAcuracia).HasColumnName("sla_acuracia");
            entity.Property(c => c.SlaEquidade).HasColumnName("sla_equidade");
            entity.Property(c => c.SlaDisponibilidade).HasColumnName("sla_disponibilidade");
            entity.Property(c => c.SlaPenalidades).HasColumnName("sla_penalidades");
            entity.Property(c => c.ConformeGuiaContratacoes).HasColumnName("conforme_guia_contratacoes");
            entity.Property(c => c.CriadoEm).HasColumnName("criado_em").HasDefaultValueSql("NOW()");
            entity.Property(c => c.CriadoPor).HasColumnName("criado_por").HasMaxLength(200);
            entity.Property(c => c.AlteradoEm).HasColumnName("alterado_em");
            entity.Property(c => c.AlteradoPor).HasColumnName("alterado_por").HasMaxLength(200);

            entity.HasIndex(c => c.OrgaoId).HasDatabaseName("ix_pgia_contrato_orgao");
            entity.HasIndex(c => c.SistemaIaId).HasDatabaseName("ix_pgia_contrato_sistema");
            entity.HasIndex(c => c.HomologacaoSgdiDocId).HasDatabaseName("ix_pgia_contrato_homologacao_doc");
            entity.HasIndex(c => c.AutorizacaoTreinamentoId).HasDatabaseName("ix_pgia_contrato_autorizacao");

            entity.HasOne(c => c.Orgao)
                .WithMany()
                .HasForeignKey(c => c.OrgaoId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_pgia_contrato_orgao");

            entity.HasOne(c => c.Sistema)
                .WithMany()
                .HasForeignKey(c => c.SistemaIaId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_pgia_contrato_sistema");

            entity.HasOne<PgiaDocumento>()
                .WithMany()
                .HasForeignKey(c => c.HomologacaoSgdiDocId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_pgia_contrato_homologacao_doc");

            entity.HasOne<PgiaAutorizacaoExcepcional>()
                .WithMany()
                .HasForeignKey(c => c.AutorizacaoTreinamentoId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_pgia_contrato_autorizacao");
        });

        modelBuilder.Entity<PgiaInstrumentoLegado>(entity =>
        {
            entity.ToTable("pgia_instrumento_legado", t =>
            {
                t.HasCheckConstraint("ck_pgia_legado_tipo",
                    "tipo_instrumento IN ('Contrato','Ato normativo','Convênio ou instrumento congênere')");
                t.HasCheckConstraint("ck_pgia_legado_envolve_ia", "envolve_ia IN ('Sim','Não','Incerto')");
            });

            entity.HasKey(l => l.Id).HasName("pk_pgia_instrumento_legado");
            entity.Property(l => l.Id).HasColumnName("id").UseIdentityByDefaultColumn();
            entity.Property(l => l.OrgaoId).HasColumnName("orgao_id");
            entity.Property(l => l.TipoInstrumento).HasColumnName("tipo_instrumento").HasMaxLength(30).IsRequired();
            entity.Property(l => l.Descricao).HasColumnName("descricao").IsRequired();
            entity.Property(l => l.Numero).HasColumnName("numero").HasMaxLength(30);
            entity.Property(l => l.EnvolveIa).HasColumnName("envolve_ia").HasMaxLength(10)
                .IsRequired().HasDefaultValue(PgiaDominios.EnvolveIa.Incerto);
            entity.Property(l => l.DataTriagem).HasColumnName("data_triagem");
            entity.Property(l => l.TriadoPor).HasColumnName("triado_por");
            entity.Property(l => l.Revisado).HasColumnName("revisado").HasDefaultValue(false);
            entity.Property(l => l.DataRevisao).HasColumnName("data_revisao");
            entity.Property(l => l.AditivoClausulaTreinamento).HasColumnName("aditivo_clausula_treinamento");
            entity.Property(l => l.DataAditivo).HasColumnName("data_aditivo");
            entity.Property(l => l.ProcessoSei).HasColumnName("processo_sei").HasMaxLength(25);
            entity.Property(l => l.ComprovacaoDocId).HasColumnName("comprovacao_doc_id");
            entity.Property(l => l.SistemaIaId).HasColumnName("sistema_ia_id");
            entity.Property(l => l.CriadoEm).HasColumnName("criado_em").HasDefaultValueSql("NOW()");
            entity.Property(l => l.CriadoPor).HasColumnName("criado_por").HasMaxLength(200);
            entity.Property(l => l.AlteradoEm).HasColumnName("alterado_em");
            entity.Property(l => l.AlteradoPor).HasColumnName("alterado_por").HasMaxLength(200);

            entity.HasIndex(l => l.OrgaoId).HasDatabaseName("ix_pgia_legado_orgao");
            entity.HasIndex(l => l.SistemaIaId).HasDatabaseName("ix_pgia_legado_sistema");
            entity.HasIndex(l => l.TriadoPor).HasDatabaseName("ix_pgia_legado_triador");
            entity.HasIndex(l => l.ComprovacaoDocId).HasDatabaseName("ix_pgia_legado_doc");

            entity.HasOne(l => l.Orgao)
                .WithMany()
                .HasForeignKey(l => l.OrgaoId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_pgia_legado_orgao");

            entity.HasOne(l => l.Sistema)
                .WithMany()
                .HasForeignKey(l => l.SistemaIaId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_pgia_legado_sistema");

            entity.HasOne(l => l.TriadoPorUser)
                .WithMany()
                .HasForeignKey(l => l.TriadoPor)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_pgia_legado_triador");

            entity.HasOne<PgiaDocumento>()
                .WithMany()
                .HasForeignKey(l => l.ComprovacaoDocId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_pgia_legado_doc");
        });

        modelBuilder.Entity<PgiaIndicadorDesempenho>(entity =>
        {
            entity.ToTable("pgia_indicador_desempenho", t =>
            {
                t.HasCheckConstraint("ck_pgia_indicador_categoria",
                    "categoria IN ('Desempenho','Adoção','Conformidade','Impacto','Acurácia','Equidade','Disponibilidade')");
                t.HasCheckConstraint("ck_pgia_indicador_periodo", "periodo_fim >= periodo_inicio");
            });

            entity.HasKey(i => i.Id).HasName("pk_pgia_indicador_desempenho");
            entity.Property(i => i.Id).HasColumnName("id").UseIdentityByDefaultColumn();
            entity.Property(i => i.SistemaIaId).HasColumnName("sistema_ia_id");
            entity.Property(i => i.Nome).HasColumnName("nome").IsRequired();
            entity.Property(i => i.Categoria).HasColumnName("categoria").HasMaxLength(30).IsRequired();
            entity.Property(i => i.Valor).HasColumnName("valor");
            entity.Property(i => i.Unidade).HasColumnName("unidade").HasMaxLength(20);
            // Adaptação: o daterange do schema vira duas colunas date
            entity.Property(i => i.PeriodoInicio).HasColumnName("periodo_inicio");
            entity.Property(i => i.PeriodoFim).HasColumnName("periodo_fim");
            entity.Property(i => i.Meta).HasColumnName("meta");
            entity.Property(i => i.PublicadoRegistroPublico).HasColumnName("publicado_registro_publico").HasDefaultValue(false);
            entity.Property(i => i.CriadoEm).HasColumnName("criado_em").HasDefaultValueSql("NOW()");
            entity.Property(i => i.CriadoPor).HasColumnName("criado_por").HasMaxLength(200);
            entity.Property(i => i.AlteradoEm).HasColumnName("alterado_em");
            entity.Property(i => i.AlteradoPor).HasColumnName("alterado_por").HasMaxLength(200);

            entity.HasIndex(i => i.SistemaIaId).HasDatabaseName("ix_pgia_indicador_sistema");

            entity.HasOne(i => i.Sistema)
                .WithMany()
                .HasForeignKey(i => i.SistemaIaId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_pgia_indicador_sistema");
        });

        modelBuilder.Entity<PgiaRelatorioSemestral>(entity =>
        {
            entity.ToTable("pgia_relatorio_semestral", t =>
            {
                t.HasCheckConstraint("ck_pgia_relatorio_semestre", "semestre IN (1, 2)");
                t.HasCheckConstraint("ck_pgia_relatorio_status",
                    "status IN ('Pendente','Enviado no prazo','Enviado em atraso','Inadimplente')");
            });

            entity.HasKey(r => r.Id).HasName("pk_pgia_relatorio_semestral");
            entity.Property(r => r.Id).HasColumnName("id").UseIdentityByDefaultColumn();
            entity.Property(r => r.OrgaoId).HasColumnName("orgao_id");
            entity.Property(r => r.Ano).HasColumnName("ano");
            entity.Property(r => r.Semestre).HasColumnName("semestre");
            entity.Property(r => r.DataEnvio).HasColumnName("data_envio");
            entity.Property(r => r.ProcessoSei).HasColumnName("processo_sei").HasMaxLength(25);
            entity.Property(r => r.DocumentoId).HasColumnName("documento_id");
            entity.Property(r => r.Status).HasColumnName("status").HasMaxLength(30)
                .IsRequired().HasDefaultValue(PgiaDominios.StatusRelatorioSemestral.Pendente);
            entity.Property(r => r.RegistradoPainelEm).HasColumnName("registrado_painel_em");
            entity.Property(r => r.ComunicadoControleInternoEm).HasColumnName("comunicado_controle_interno_em");
            entity.Property(r => r.PrazoEnvio).HasColumnName("prazo_envio");
            entity.Property(r => r.CriadoEm).HasColumnName("criado_em").HasDefaultValueSql("NOW()");
            entity.Property(r => r.CriadoPor).HasColumnName("criado_por").HasMaxLength(200);
            entity.Property(r => r.AlteradoEm).HasColumnName("alterado_em");
            entity.Property(r => r.AlteradoPor).HasColumnName("alterado_por").HasMaxLength(200);

            entity.HasIndex(r => new { r.OrgaoId, r.Ano, r.Semestre })
                .IsUnique().HasDatabaseName("ux_pgia_relatorio_semestral");
            entity.HasIndex(r => r.DocumentoId).HasDatabaseName("ix_pgia_relatorio_semestral_doc");

            entity.HasOne(r => r.Orgao)
                .WithMany()
                .HasForeignKey(r => r.OrgaoId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_pgia_relatorio_semestral_orgao");

            entity.HasOne<PgiaDocumento>()
                .WithMany()
                .HasForeignKey(r => r.DocumentoId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_pgia_relatorio_semestral_doc");
        });

        modelBuilder.Entity<PgiaRelatorioAnual>(entity =>
        {
            entity.ToTable("pgia_relatorio_anual");

            entity.HasKey(r => r.Id).HasName("pk_pgia_relatorio_anual");
            entity.Property(r => r.Id).HasColumnName("id").UseIdentityByDefaultColumn();
            entity.Property(r => r.Ano).HasColumnName("ano");
            entity.Property(r => r.DataPublicacao).HasColumnName("data_publicacao");
            entity.Property(r => r.UrlPublicacao).HasColumnName("url_publicacao");
            entity.Property(r => r.DocumentoId).HasColumnName("documento_id");
            entity.Property(r => r.ApreciadoCgticEm).HasColumnName("apreciado_cgtic_em");
            entity.Property(r => r.Recomendacoes).HasColumnName("recomendacoes");
            entity.Property(r => r.AgendaInovacao).HasColumnName("agenda_inovacao");
            entity.Property(r => r.CriadoEm).HasColumnName("criado_em").HasDefaultValueSql("NOW()");
            entity.Property(r => r.CriadoPor).HasColumnName("criado_por").HasMaxLength(200);
            entity.Property(r => r.AlteradoEm).HasColumnName("alterado_em");
            entity.Property(r => r.AlteradoPor).HasColumnName("alterado_por").HasMaxLength(200);

            entity.HasIndex(r => r.Ano).IsUnique().HasDatabaseName("ux_pgia_relatorio_anual_ano");
            entity.HasIndex(r => r.DocumentoId).HasDatabaseName("ix_pgia_relatorio_anual_doc");

            entity.HasOne<PgiaDocumento>()
                .WithMany()
                .HasForeignKey(r => r.DocumentoId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_pgia_relatorio_anual_doc");
        });

        modelBuilder.Entity<PgiaAuditoriaTecnica>(entity =>
        {
            entity.ToTable("pgia_auditoria_tecnica", t =>
            {
                t.HasCheckConstraint("ck_pgia_auditoria_tipo",
                    "tipo IN ('Periódica','Independente contratual')");
            });

            entity.HasKey(a => a.Id).HasName("pk_pgia_auditoria_tecnica");
            entity.Property(a => a.Id).HasColumnName("id").UseIdentityByDefaultColumn();
            entity.Property(a => a.SistemaIaId).HasColumnName("sistema_ia_id");
            entity.Property(a => a.Tipo).HasColumnName("tipo").HasMaxLength(40).IsRequired();
            entity.Property(a => a.EntidadeAuditora).HasColumnName("entidade_auditora").IsRequired();
            // Adaptação: designa a auditoria a um usuário de papel pgia_auditoria
            entity.Property(a => a.AuditorUserId).HasColumnName("auditor_user_id");
            entity.Property(a => a.ExternaFornecedor).HasColumnName("externa_fornecedor");
            entity.Property(a => a.ApoioFapdf).HasColumnName("apoio_fapdf");
            entity.Property(a => a.DataInicio).HasColumnName("data_inicio");
            entity.Property(a => a.DataFim).HasColumnName("data_fim");
            entity.Property(a => a.Parecer).HasColumnName("parecer");
            entity.Property(a => a.PublicadoPortal).HasColumnName("publicado_portal").HasDefaultValue(false);
            entity.Property(a => a.DataPublicacao).HasColumnName("data_publicacao");
            entity.Property(a => a.Url).HasColumnName("url");
            entity.Property(a => a.CriadoEm).HasColumnName("criado_em").HasDefaultValueSql("NOW()");
            entity.Property(a => a.CriadoPor).HasColumnName("criado_por").HasMaxLength(200);
            entity.Property(a => a.AlteradoEm).HasColumnName("alterado_em");
            entity.Property(a => a.AlteradoPor).HasColumnName("alterado_por").HasMaxLength(200);

            entity.HasIndex(a => a.SistemaIaId).HasDatabaseName("ix_pgia_auditoria_sistema");
            entity.HasIndex(a => a.AuditorUserId).HasDatabaseName("ix_pgia_auditoria_auditor");

            entity.HasOne(a => a.Sistema)
                .WithMany()
                .HasForeignKey(a => a.SistemaIaId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_pgia_auditoria_sistema");

            entity.HasOne(a => a.AuditorUser)
                .WithMany()
                .HasForeignKey(a => a.AuditorUserId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_pgia_auditoria_auditor");
        });

        // ── Fase 5: transparência pública ─────────────────────────────────────

        modelBuilder.Entity<PgiaSolicitacaoCidadao>(entity =>
        {
            entity.ToTable("pgia_solicitacao_cidadao", t =>
            {
                t.HasCheckConstraint("ck_pgia_solicitacao_tipo",
                    "tipo IN ('Informação sobre uso de IA','Revisão de decisão','Explicação da decisão','Impugnação por discriminação ou erro','Reclamação de titular de dados')");
                t.HasCheckConstraint("ck_pgia_solicitacao_status",
                    "status IN ('Recebida','Em análise','Respondida','Encaminhada ao Encarregado de Dados')");
            });

            entity.HasKey(s => s.Id).HasName("pk_pgia_solicitacao_cidadao");
            entity.Property(s => s.Id).HasColumnName("id").UseIdentityByDefaultColumn();
            entity.Property(s => s.Protocolo).HasColumnName("protocolo").HasMaxLength(30).IsRequired();
            entity.Property(s => s.SistemaIaId).HasColumnName("sistema_ia_id");
            entity.Property(s => s.Tipo).HasColumnName("tipo").HasMaxLength(50).IsRequired();
            entity.Property(s => s.SolicitanteNome).HasColumnName("solicitante_nome").IsRequired();
            entity.Property(s => s.SolicitanteContato).HasColumnName("solicitante_contato").IsRequired();
            entity.Property(s => s.ReferenciaDecisao).HasColumnName("referencia_decisao");
            entity.Property(s => s.Descricao).HasColumnName("descricao").IsRequired();
            entity.Property(s => s.DataAbertura).HasColumnName("data_abertura").HasDefaultValueSql("NOW()");
            // varchar(40) e não o varchar(20) do schema: "Encaminhada ao Encarregado de
            // Dados" tem 35 caracteres e não caberia no tipo declarado lá
            entity.Property(s => s.Status).HasColumnName("status").HasMaxLength(40)
                .IsRequired().HasDefaultValue(PgiaDominios.StatusSolicitacao.Recebida);
            entity.Property(s => s.Resposta).HasColumnName("resposta");
            entity.Property(s => s.RespondidoPor).HasColumnName("respondido_por");
            entity.Property(s => s.DataResposta).HasColumnName("data_resposta");
            entity.Property(s => s.EncaminhadaDpo).HasColumnName("encaminhada_dpo").HasDefaultValue(false);
            entity.Property(s => s.CriadoEm).HasColumnName("criado_em").HasDefaultValueSql("NOW()");
            entity.Property(s => s.CriadoPor).HasColumnName("criado_por").HasMaxLength(200);
            entity.Property(s => s.AlteradoEm).HasColumnName("alterado_em");
            entity.Property(s => s.AlteradoPor).HasColumnName("alterado_por").HasMaxLength(200);

            entity.HasIndex(s => s.Protocolo).IsUnique().HasDatabaseName("ux_pgia_solicitacao_protocolo");
            entity.HasIndex(s => s.SistemaIaId).HasDatabaseName("ix_pgia_solicitacao_sistema");
            entity.HasIndex(s => s.RespondidoPor).HasDatabaseName("ix_pgia_solicitacao_agente");

            entity.HasOne(s => s.Sistema)
                .WithMany()
                .HasForeignKey(s => s.SistemaIaId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_pgia_solicitacao_sistema");

            entity.HasOne(s => s.RespondidoPorUser)
                .WithMany()
                .HasForeignKey(s => s.RespondidoPor)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_pgia_solicitacao_agente");
        });
    }
}
