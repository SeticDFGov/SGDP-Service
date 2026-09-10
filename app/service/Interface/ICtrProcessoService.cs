using api.Common;
using api.Contratacoes;
using Models.Contratacoes;
using service.Contratacoes;

namespace service.Interface;

public interface ICtrProcessoService
{
    Task<PagedResponse<CtrProcessoResponse>> ListarAsync(CtrProcessoFiltro filtro);

    /// <summary>Siglas distintas dos processos ativos, ordenadas (autocompletar).</summary>
    Task<List<string>> ListarSiglasAsync();

    /// <summary>CSV da listagem filtrada, sem paginação (até 5.000 linhas).</summary>
    Task<byte[]> ExportarCsvAsync(CtrProcessoFiltro filtro);

    /// <summary>Entidade ativa (null quando inexistente ou excluída) — para o 404 do controller.</summary>
    Task<CtrProcesso?> GetEntidadeAsync(long id);

    Task<CtrProcessoResponse> GetAsync(long id);

    Task<CtrProcessoResponse> CriarAsync(CtrProcessoCreateDTO dto, CtrUserContext ctx);

    Task<CtrProcessoResponse> AtualizarAsync(long id, CtrProcessoUpdateDTO dto, CtrUserContext ctx);

    /// <summary>Soft delete: some das listas, a linha permanece auditável.</summary>
    Task ExcluirAsync(long id, CtrUserContext ctx);

    Task<CtrProcessoResponse> RegistrarCheckpointAsync(long id, CtrCheckpointDTO dto, CtrUserContext ctx);

    Task<CtrPainelResponse> MontarPainelAsync(int diasSemMovimento);
}
