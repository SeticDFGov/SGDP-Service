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
    }
}
