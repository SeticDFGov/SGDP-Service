using Npgsql;

namespace service.Planejamento;

/// <summary>Apoio de banco do módulo Governança Estratégica.</summary>
public static class PeBanco
{
    /// <summary>
    /// A consulta bateu numa tabela (42P01) ou numa coluna (42703) que ainda não existe no
    /// PostgreSQL: é o intervalo do deploy, entre publicar o código (PR) e rodar a migration
    /// (merge). A coluna entra desde a E4, que acrescenta pdtic_id a uma tabela da E3
    /// (pe_registro): toda leitura de registros seleciona a coluna nova.
    /// </summary>
    public static bool TabelaAusente(Exception? ex)
    {
        for (var atual = ex; atual != null; atual = atual.InnerException)
        {
            if (atual is PostgresException { SqlState: PostgresErrorCodes.UndefinedTable or PostgresErrorCodes.UndefinedColumn })
                return true;
        }
        return false;
    }
}
