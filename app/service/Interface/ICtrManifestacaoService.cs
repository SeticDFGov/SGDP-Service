using api.Common;
using api.Contratacoes;
using Models.Contratacoes;
using service.Contratacoes;

namespace service.Interface;

public interface ICtrManifestacaoService
{
    Task<PagedResponse<CtrManifestacaoResponse>> ListarAsync(CtrManifestacaoFiltro filtro);

    /// <summary>Manifestações de um processo, mais recente primeiro.</summary>
    Task<List<CtrManifestacaoResponse>> ListarDoProcessoAsync(long processoId);

    /// <summary>
    /// Entidade de manifestação de processo ATIVO (null quando inexistente ou de
    /// processo excluído) — para o 404 do controller.
    /// </summary>
    Task<CtrManifestacaoTcdf?> GetEntidadeAsync(long id);

    Task<CtrManifestacaoResponse> GetAsync(long id);

    Task<CtrManifestacaoResponse> CriarAsync(long processoId, CtrManifestacaoCreateDTO dto, CtrUserContext ctx);

    Task<CtrManifestacaoResponse> AtualizarAsync(long id, CtrManifestacaoUpdateDTO dto, CtrUserContext ctx);

    /// <summary>Despacho padrão da SGDI ao TCDF, preenchido, em PDF. Nada persiste.</summary>
    Task<byte[]> GerarDespachoPdfAsync(long id, string local, string nome, string cargo);
}
