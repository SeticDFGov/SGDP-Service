using api.Contratacoes;

namespace service.Interface;

public interface ICtrAdminService
{
    /// <summary>
    /// Atribui (ou remove, com PapelContratacoes nulo) o papel do módulo.
    /// O Perfil do SGDP nunca é tocado.
    /// </summary>
    Task AtribuirPapelAsync(AtribuirPapelContratacoesDTO dto, string adminEmail);
}
