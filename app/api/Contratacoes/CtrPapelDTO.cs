using System.ComponentModel.DataAnnotations;

namespace api.Contratacoes;

/// <summary>
/// Atribuição do papel do módulo Análises de Contratações a um usuário
/// (PUT api/contratacoes/admin/papel). PapelContratacoes nulo remove o papel.
/// O Perfil do SGDP nunca é tocado.
/// </summary>
public class AtribuirPapelContratacoesDTO
{
    public string Email { get; set; } = string.Empty;

    [StringLength(30)]
    public string? PapelContratacoes { get; set; }
}
