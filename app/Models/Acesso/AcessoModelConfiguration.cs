using app.Auth;
using Microsoft.EntityFrameworkCore;

namespace Models.Acesso;

/// <summary>
/// Mapeamento EF da gestão de acessos por módulo: tabelas acesso_modulo e
/// pedido_acesso, colunas snake_case e CHECKs dos domínios de <see cref="ModulosSgdp"/>,
/// <see cref="OrigemAcesso"/> e <see cref="SituacaoPedidoAcesso"/> (mesmo padrão do
/// PgiaModelConfiguration/CtrModelConfiguration). Tabelas novas e isoladas: nenhuma
/// tabela existente é alterada.
/// </summary>
public static class AcessoModelConfiguration
{
    private static string EmLista(string coluna, IEnumerable<string> valores) =>
        $"{coluna} IN ({string.Join(",", valores.Select(v => $"'{v.Replace("'", "''")}'"))})";

    public static void ApplyAcessoConfiguration(this ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AcessoModulo>(entity =>
        {
            entity.ToTable("acesso_modulo", t =>
            {
                t.HasCheckConstraint("ck_acesso_modulo_modulo", EmLista("modulo", ModulosSgdp.Todos));
                t.HasCheckConstraint("ck_acesso_modulo_origem", EmLista("origem", OrigemAcesso.Todos));
            });

            entity.HasKey(a => a.Id).HasName("pk_acesso_modulo");
            entity.Property(a => a.Id).HasColumnName("id").UseIdentityByDefaultColumn();
            entity.Property(a => a.UserId).HasColumnName("user_id");
            entity.Property(a => a.Modulo).HasColumnName("modulo").HasMaxLength(30).IsRequired();
            entity.Property(a => a.Origem).HasColumnName("origem").HasMaxLength(20).IsRequired();
            entity.Property(a => a.ConcedidoEm).HasColumnName("concedido_em").HasDefaultValueSql("NOW()");
            entity.Property(a => a.ConcedidoPor).HasColumnName("concedido_por").HasMaxLength(200).IsRequired();

            // Uma linha por (usuário, módulo, origem): conceder de novo não duplica
            entity.HasIndex(a => new { a.UserId, a.Modulo, a.Origem })
                .IsUnique()
                .HasDatabaseName("ux_acesso_modulo_usuario");

            // Apagar o usuário apaga os acessos dele (não há histórico a preservar aqui)
            entity.HasOne(a => a.User)
                .WithMany()
                .HasForeignKey(a => a.UserId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("fk_acesso_modulo_user");
        });

        modelBuilder.Entity<PedidoAcesso>(entity =>
        {
            entity.ToTable("pedido_acesso", t =>
            {
                t.HasCheckConstraint("ck_pedido_acesso_modulo", EmLista("modulo", ModulosSgdp.Liberaveis));
                t.HasCheckConstraint("ck_pedido_acesso_situacao", EmLista("situacao", SituacaoPedidoAcesso.Todas));
            });

            entity.HasKey(p => p.Id).HasName("pk_pedido_acesso");
            entity.Property(p => p.Id).HasColumnName("id").UseIdentityByDefaultColumn();
            entity.Property(p => p.UserId).HasColumnName("user_id");
            entity.Property(p => p.Modulo).HasColumnName("modulo").HasMaxLength(30).IsRequired();
            entity.Property(p => p.Justificativa).HasColumnName("justificativa").HasMaxLength(500);
            // Token de concorrência: duas pessoas decidindo o mesmo pedido ao mesmo
            // tempo, a segunda gravação encontra a situação mudada e é recusada
            entity.Property(p => p.Situacao).HasColumnName("situacao").HasMaxLength(20).IsRequired()
                .IsConcurrencyToken();
            entity.Property(p => p.CriadoEm).HasColumnName("criado_em").HasDefaultValueSql("NOW()");
            entity.Property(p => p.DecididoEm).HasColumnName("decidido_em");
            entity.Property(p => p.DecididoPor).HasColumnName("decidido_por").HasMaxLength(200);
            entity.Property(p => p.PapelPgia).HasColumnName("papel_pgia").HasMaxLength(30);
            entity.Property(p => p.MotivoRecusa).HasColumnName("motivo_recusa").HasMaxLength(500);

            // Um pedido pendente por pessoa e módulo: pedir de novo devolve o mesmo
            entity.HasIndex(p => new { p.UserId, p.Modulo })
                .IsUnique()
                .HasFilter("situacao = 'pendente'")
                .HasDatabaseName("ux_pedido_acesso_pendente");

            // Fila de quem decide: os pendentes, por ordem de chegada
            entity.HasIndex(p => new { p.Situacao, p.CriadoEm })
                .HasDatabaseName("ix_pedido_acesso_situacao");

            // Apagar o usuário apaga os pedidos dele
            entity.HasOne(p => p.User)
                .WithMany()
                .HasForeignKey(p => p.UserId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("fk_pedido_acesso_user");
        });
    }
}
