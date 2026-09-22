using Microsoft.EntityFrameworkCore;
using Models.Pgia;

namespace Models.Contratacoes;

/// <summary>
/// Mapeamento EF das tabelas do módulo Supervisão Contínua das Contratações: prefixo ctr_,
/// colunas snake_case e CHECKs reproduzindo os domínios de <see cref="CtrDominios"/>.
/// Nomes de constraints e índices levam o prefixo ctr para facilitar a revisão do
/// script de migration (mesmo padrão do PgiaModelConfiguration).
/// </summary>
public static class CtrModelConfiguration
{
    private static string EmLista(string coluna, IEnumerable<string> valores) =>
        $"{coluna} IN ({string.Join(",", valores.Select(v => $"'{v.Replace("'", "''")}'"))})";

    /// <param name="indicesRelacionais">
    /// true no PostgreSQL. Cobre o índice único PARCIAL do número do processo
    /// (WHERE ativo), que usa HasFilter — ignorado pelo provider InMemory dos testes,
    /// onde exigiria unicidade incondicional e impediria reaproveitar o número de um
    /// processo excluído (soft delete).
    /// </param>
    public static void ApplyCtrConfiguration(this ModelBuilder modelBuilder, bool indicesRelacionais = true)
    {
        modelBuilder.Entity<CtrProcesso>(entity =>
        {
            entity.ToTable("ctr_processo", t =>
            {
                t.HasCheckConstraint("ck_ctr_processo_categoria",
                    EmLista("categoria_objeto", CtrDominios.CategoriaObjeto.Todos));
                // "Não se aplica" e data preenchida são mutuamente exclusivos
                t.HasCheckConstraint("ck_ctr_processo_ugtic",
                    "NOT ugtic_nao_se_aplica OR chegada_ugtic IS NULL");
                // Mesma simetria para a devolução ao órgão comunicante
                t.HasCheckConstraint("ck_ctr_processo_retorno_orgao",
                    "NOT retorno_orgao_nao_se_aplica OR retorno_orgao IS NULL");
                // Restituição exige data e motivo
                t.HasCheckConstraint("ck_ctr_processo_restituicao",
                    "NOT restituido OR (restituido_em IS NOT NULL AND restituido_motivo IS NOT NULL)");
                t.HasCheckConstraint("ck_ctr_processo_etapa_planejamento",
                    "etapa_planejamento IS NULL OR "
                    + EmLista("etapa_planejamento", CtrDominios.EtapaPlanejamento.Todos));
                t.HasCheckConstraint("ck_ctr_processo_criticidade",
                    "criticidade IS NULL OR " + EmLista("criticidade", CtrDominios.Criticidade.Todos));
                t.HasCheckConstraint("ck_ctr_processo_origem",
                    EmLista("origem", CtrDominios.Origem.Todos));
                // Resposta sem pedido não existe; pedido exige o que foi pedido
                t.HasCheckConstraint("ck_ctr_processo_esclarecimento",
                    "(esclarecimento_solicitado_em IS NULL AND esclarecimento_descricao IS NULL "
                    + "AND esclarecimento_respondido_em IS NULL) OR "
                    + "(esclarecimento_solicitado_em IS NOT NULL AND esclarecimento_descricao IS NOT NULL)");
                // Os dois CHECKs abaixo são das colunas LEGADAS do questionário do PGIA (sem
                // uso desde 2026-09-21, ver CtrProcesso); ficam até a migration que as remover.
                // Mesmos quatro resultados do PGIA (fonte única: PgiaDominios.ResultadoRisco)
                t.HasCheckConstraint("ck_ctr_processo_risco_classificado",
                    "risco_classificado IS NULL OR "
                    + EmLista("risco_classificado", PgiaDominios.ResultadoRisco.Todos));
                // Classificação é tudo ou nada: sem checklist não há resultado, pontuação
                // nem instante; com checklist, os três vêm juntos
                t.HasCheckConstraint("ck_ctr_processo_classificacao",
                    "(checklist_risco IS NULL AND risco_classificado IS NULL AND pontuacao_risco IS NULL "
                    + "AND risco_classificado_em IS NULL) OR "
                    + "(checklist_risco IS NOT NULL AND risco_classificado IS NOT NULL AND pontuacao_risco IS NOT NULL "
                    + "AND risco_classificado_em IS NOT NULL)");
            });

            entity.HasKey(p => p.Id).HasName("pk_ctr_processo");
            entity.Property(p => p.Id).HasColumnName("id").UseIdentityByDefaultColumn();
            entity.Property(p => p.NumeroProcesso).HasColumnName("numero_processo").HasMaxLength(25).IsRequired();
            entity.Property(p => p.OrgaoNome).HasColumnName("orgao_nome").HasMaxLength(200).IsRequired();
            entity.Property(p => p.OrgaoSigla).HasColumnName("orgao_sigla").HasMaxLength(20).IsRequired();
            entity.Property(p => p.ComplementoArea).HasColumnName("complemento_area").HasMaxLength(200);
            entity.Property(p => p.Objeto).HasColumnName("objeto").IsRequired();
            entity.Property(p => p.CategoriaObjeto).HasColumnName("categoria_objeto").HasMaxLength(60).IsRequired();
            entity.Property(p => p.ChegadaSgdi).HasColumnName("chegada_sgdi");
            entity.Property(p => p.ChegadaSubgd).HasColumnName("chegada_subgd");
            entity.Property(p => p.ChegadaUgtic).HasColumnName("chegada_ugtic");
            entity.Property(p => p.UgticNaoSeAplica).HasColumnName("ugtic_nao_se_aplica").HasDefaultValue(false);
            entity.Property(p => p.RetornoGabSgdi).HasColumnName("retorno_gab_sgdi");
            entity.Property(p => p.RetornoOrgao).HasColumnName("retorno_orgao");
            entity.Property(p => p.RetornoOrgaoNaoSeAplica)
                .HasColumnName("retorno_orgao_nao_se_aplica").HasDefaultValue(false);
            entity.Property(p => p.EtapaPlanejamento).HasColumnName("etapa_planejamento").HasMaxLength(10);
            entity.Property(p => p.DataAssinaturaContrato).HasColumnName("data_assinatura_contrato");
            entity.Property(p => p.Criticidade).HasColumnName("criticidade").HasMaxLength(10);
            entity.Property(p => p.Origem).HasColumnName("origem").HasMaxLength(20).IsRequired()
                .HasDefaultValue(CtrDominios.Origem.OrgaoComunicante);
            entity.Property(p => p.EsclarecimentoSolicitadoEm).HasColumnName("esclarecimento_solicitado_em");
            entity.Property(p => p.EsclarecimentoDescricao).HasColumnName("esclarecimento_descricao");
            entity.Property(p => p.EsclarecimentoRespondidoEm).HasColumnName("esclarecimento_respondido_em");
            entity.Property(p => p.ChecklistRisco).HasColumnName("checklist_risco").HasColumnType("jsonb");
            entity.Property(p => p.RiscoClassificado).HasColumnName("risco_classificado").HasMaxLength(20);
            entity.Property(p => p.EnquadramentoRisco).HasColumnName("enquadramento_risco").HasMaxLength(20);
            entity.Property(p => p.PontuacaoRisco).HasColumnName("pontuacao_risco");
            entity.Property(p => p.RiscoClassificadoEm).HasColumnName("risco_classificado_em");
            entity.Property(p => p.RiscoClassificadoPor).HasColumnName("risco_classificado_por").HasMaxLength(200);
            entity.Property(p => p.Restituido).HasColumnName("restituido").HasDefaultValue(false);
            entity.Property(p => p.RestituidoEm).HasColumnName("restituido_em");
            entity.Property(p => p.RestituidoMotivo).HasColumnName("restituido_motivo");
            entity.Property(p => p.Observacao).HasColumnName("observacao");
            entity.Property(p => p.Ativo).HasColumnName("ativo").HasDefaultValue(true);
            entity.Property(p => p.CriadoEm).HasColumnName("criado_em").HasDefaultValueSql("NOW()");
            entity.Property(p => p.CriadoPor).HasColumnName("criado_por").HasMaxLength(200).IsRequired();
            entity.Property(p => p.AlteradoEm).HasColumnName("alterado_em");
            entity.Property(p => p.AlteradoPor).HasColumnName("alterado_por").HasMaxLength(200);

            if (indicesRelacionais)
            {
                // Único entre os ATIVOS: o número volta a ficar livre depois do soft delete
                entity.HasIndex(p => p.NumeroProcesso)
                    .IsUnique()
                    .HasFilter("ativo")
                    .HasDatabaseName("ux_ctr_processo_numero");
            }
            else
            {
                entity.HasIndex(p => p.NumeroProcesso).HasDatabaseName("ix_ctr_processo_numero");
            }

            entity.HasIndex(p => p.OrgaoSigla).HasDatabaseName("ix_ctr_processo_sigla");
            entity.HasIndex(p => p.ChegadaSgdi).HasDatabaseName("ix_ctr_processo_chegada_sgdi");
        });

        modelBuilder.Entity<CtrManifestacaoTcdf>(entity =>
        {
            entity.ToTable("ctr_manifestacao_tcdf", t =>
            {
                t.HasCheckConstraint("ck_ctr_manifestacao_situacao",
                    EmLista("situacao_portfolio", CtrDominios.SituacaoPortfolio.Todos));
                // Fato do Tribunal: vale em QUALQUER inciso, por isso fica fora dos
                // CHECKs condicionais dos incisos
                t.HasCheckConstraint("ck_ctr_manifestacao_status",
                    "status_tcdf IS NULL OR " + EmLista("status_tcdf", CtrDominios.StatusTcdf.Todos));
                t.HasCheckConstraint("ck_ctr_manifestacao_resultado",
                    "resultado_analise IS NULL OR " + EmLista("resultado_analise", CtrDominios.ResultadoAnalise.Todos));
                t.HasCheckConstraint("ck_ctr_manifestacao_desfecho",
                    "desfecho_risco IS NULL OR " + EmLista("desfecho_risco", CtrDominios.DesfechoRisco.Todos));
                t.HasCheckConstraint("ck_ctr_manifestacao_prazo",
                    "prazo_regularizacao_dias IS NULL OR prazo_regularizacao_dias > 0");
                // Coerência dos incisos I e II do despacho (o provider InMemory ignora
                // CHECKs, por isso o service valida exatamente o mesmo)
                // A criticidade saiu daqui: passou a ser do PROCESSO (o despacho só a reporta)
                t.HasCheckConstraint("ck_ctr_manifestacao_inciso",
                    "(situacao_portfolio = 'Comunicada previamente' AND comunicada_desde IS NOT NULL "
                    + "AND resultado_analise IS NOT NULL "
                    + "AND prazo_regularizacao_dias IS NULL) OR "
                    + "(situacao_portfolio = 'Não comunicada previamente' AND prazo_regularizacao_dias IS NOT NULL "
                    + "AND comunicada_desde IS NULL AND resultado_analise IS NULL "
                    + "AND desfecho_risco IS NULL AND NOT recomendou_suspensao AND NOT comunicou_controle_interno)");
                t.HasCheckConstraint("ck_ctr_manifestacao_risco",
                    "(resultado_analise = 'Riscos significativos' AND desfecho_risco IS NOT NULL) OR "
                    + "(resultado_analise IS DISTINCT FROM 'Riscos significativos' AND desfecho_risco IS NULL "
                    + "AND NOT recomendou_suspensao AND NOT comunicou_controle_interno)");
            });

            entity.HasKey(m => m.Id).HasName("pk_ctr_manifestacao_tcdf");
            entity.Property(m => m.Id).HasColumnName("id").UseIdentityByDefaultColumn();
            entity.Property(m => m.ProcessoId).HasColumnName("processo_id");
            entity.Property(m => m.OficioTcdf).HasColumnName("oficio_tcdf").HasMaxLength(60).IsRequired();
            entity.Property(m => m.DataOficio).HasColumnName("data_oficio");
            entity.Property(m => m.SituacaoPortfolio).HasColumnName("situacao_portfolio").HasMaxLength(30).IsRequired();
            entity.Property(m => m.EsclarecimentosAdicionais).HasColumnName("esclarecimentos_adicionais");
            entity.Property(m => m.StatusTcdf).HasColumnName("status_tcdf").HasMaxLength(30);
            entity.Property(m => m.ComunicadaDesde).HasColumnName("comunicada_desde");
            entity.Property(m => m.ResultadoAnalise).HasColumnName("resultado_analise").HasMaxLength(30);
            entity.Property(m => m.RecomendouSuspensao).HasColumnName("recomendou_suspensao").HasDefaultValue(false);
            entity.Property(m => m.ComunicouControleInterno).HasColumnName("comunicou_controle_interno").HasDefaultValue(false);
            entity.Property(m => m.DesfechoRisco).HasColumnName("desfecho_risco").HasMaxLength(25);
            entity.Property(m => m.PrazoRegularizacaoDias).HasColumnName("prazo_regularizacao_dias");
            entity.Property(m => m.Observacao).HasColumnName("observacao");
            entity.Property(m => m.CriadoEm).HasColumnName("criado_em").HasDefaultValueSql("NOW()");
            entity.Property(m => m.CriadoPor).HasColumnName("criado_por").HasMaxLength(200).IsRequired();
            entity.Property(m => m.AlteradoEm).HasColumnName("alterado_em");
            entity.Property(m => m.AlteradoPor).HasColumnName("alterado_por").HasMaxLength(200);

            entity.HasIndex(m => m.ProcessoId).HasDatabaseName("ix_ctr_manifestacao_processo");

            // RESTRICT: manifestação é peça de resposta a órgão de controle, nunca cai em cascata
            entity.HasOne(m => m.Processo)
                .WithMany(p => p.Manifestacoes)
                .HasForeignKey(m => m.ProcessoId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_ctr_manifestacao_processo");
        });

        modelBuilder.Entity<CtrRiscoDeclarado>(entity =>
        {
            entity.ToTable("ctr_risco_declarado", t =>
            {
                // Escala COMPLETA da CGDF — diferente do grupo "Outros" do PGIA, que
                // aceita só o quadrante baixo (o service valida antes, com mensagem)
                t.HasCheckConstraint("ck_ctr_risco_declarado_probabilidade",
                    EmLista("probabilidade", PgiaDominios.EscalaCgdf.Probabilidade.Todos));
                t.HasCheckConstraint("ck_ctr_risco_declarado_consequencia",
                    EmLista("consequencia", PgiaDominios.EscalaCgdf.Consequencia.Todos));
            });

            entity.HasKey(r => r.Id).HasName("pk_ctr_risco_declarado");
            entity.Property(r => r.Id).HasColumnName("id").UseIdentityByDefaultColumn();
            entity.Property(r => r.ProcessoId).HasColumnName("processo_id");
            entity.Property(r => r.DescricaoRisco).HasColumnName("descricao_risco").IsRequired();
            entity.Property(r => r.AcaoMitigacao).HasColumnName("acao_mitigacao").IsRequired();
            entity.Property(r => r.ResponsavelNome).HasColumnName("responsavel_nome").HasMaxLength(200).IsRequired();
            entity.Property(r => r.ResponsavelEmail).HasColumnName("responsavel_email").HasMaxLength(200).IsRequired();
            entity.Property(r => r.Probabilidade).HasColumnName("probabilidade").HasMaxLength(15).IsRequired();
            entity.Property(r => r.Consequencia).HasColumnName("consequencia").HasMaxLength(15).IsRequired();
            entity.Property(r => r.CriadoEm).HasColumnName("criado_em").HasDefaultValueSql("NOW()");
            entity.Property(r => r.CriadoPor).HasColumnName("criado_por").HasMaxLength(200).IsRequired();

            entity.HasIndex(r => r.ProcessoId).HasDatabaseName("ix_ctr_risco_declarado_processo");

            // CASCADE: os riscos declarados são parte da classificação do processo
            // (como pgia_risco_outro), não peça autônoma
            entity.HasOne(r => r.Processo)
                .WithMany(p => p.RiscosDeclarados)
                .HasForeignKey(r => r.ProcessoId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("fk_ctr_risco_declarado_processo");
        });
    }
}
