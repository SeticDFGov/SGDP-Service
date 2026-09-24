using Npgsql;

namespace service.Planejamento;

/// <summary>Apoio de banco do módulo Governança Estratégica.</summary>
public static class PeBanco
{
    /// <summary>
    /// A consulta bateu numa tabela que ainda não existe (42P01 no PostgreSQL): é o
    /// intervalo do deploy, entre publicar o código (PR) e rodar a migration (merge).
    /// </summary>
    public static bool TabelaAusente(Exception? ex)
    {
        for (var atual = ex; atual != null; atual = atual.InnerException)
        {
            if (atual is PostgresException { SqlState: PostgresErrorCodes.UndefinedTable }) return true;
        }
        return false;
    }
}
