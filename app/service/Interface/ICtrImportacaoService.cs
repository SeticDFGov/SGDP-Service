using api.Contratacoes;
using service.Contratacoes;

namespace service.Interface;

public interface ICtrImportacaoService
{
    /// <summary>Interpreta o arquivo sem gravar nada (prévia da importação).</summary>
    Task<CtrImportacaoPrevia> PreviaAsync(byte[] conteudo);

    /// <summary>
    /// Importa a planilha: upsert por número de processo entre os ativos, num único
    /// SaveChanges. As linhas rejeitadas ficam no relatório com o motivo.
    /// </summary>
    Task<CtrImportacaoRelatorio> ImportarAsync(byte[] conteudo, CtrUserContext ctx);
}
