using System.Security.Claims;
using api.Atividade;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using service.Interface;

namespace demanda_service.Controllers;

/// <summary>
/// Controller para gerenciar Atividades (nova arquitetura)
/// </summary>
[ApiController]
[Route("api/atividade")]
[Authorize]
public class AtividadeController : ControllerBase
{
    private readonly IAtividadeService _atividadeService;
    private readonly IPermissionService _permissionService;

    public AtividadeController(IAtividadeService atividadeService, IPermissionService permissionService)
    {
        _atividadeService = atividadeService;
        _permissionService = permissionService;
    }

    private string? GetUserEmail()
    {
        return User.FindFirst(ClaimTypes.Email)?.Value;
    }

    /// <summary>
    /// Cria uma nova atividade
    /// </summary>
    /// <param name="dto">Dados da atividade a ser criada</param>
    /// <returns>Atividade criada com ID gerado</returns>
    [HttpPost]
    [ProducesResponseType(typeof(AtividadeResponseDTO), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreateAtividade([FromBody] AtividadeCreateDTO dto)
    {
        var email = GetUserEmail();
        if (string.IsNullOrEmpty(email))
            return Unauthorized();

        var perfil = await _permissionService.GetUserPerfilAsync(email);
        if (!_permissionService.CanCreate(perfil, "atividade"))
            return Forbid();

        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var result = await _atividadeService.CreateAtividadeAsync(dto);
        return CreatedAtAction(nameof(GetAtividadeById), new { id = result.AtividadeId }, result);
    }

    /// <summary>
    /// Atualiza uma atividade existente
    /// </summary>
    /// <param name="id">ID da atividade</param>
    /// <param name="dto">Dados a serem atualizados</param>
    /// <returns>Atividade atualizada</returns>
    [HttpPut("{id}")]
    [ProducesResponseType(typeof(AtividadeResponseDTO), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> UpdateAtividade(int id, [FromBody] AtividadeUpdateDTO dto)
    {
        var email = GetUserEmail();
        if (string.IsNullOrEmpty(email))
            return Unauthorized();

        var perfil = await _permissionService.GetUserPerfilAsync(email);
        if (!_permissionService.CanEdit(perfil, "atividade"))
            return Forbid();

        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var result = await _atividadeService.UpdateAtividadeAsync(id, dto);
        return Ok(result);
    }

    /// <summary>
    /// Busca uma atividade por ID
    /// </summary>
    /// <param name="id">ID da atividade</param>
    /// <returns>Dados da atividade</returns>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(AtividadeResponseDTO), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAtividadeById(int id)
    {
        var result = await _atividadeService.GetAtividadeByIdAsync(id);
        return Ok(result);
    }

    /// <summary>
    /// Lista todas as atividades de uma etapa
    /// </summary>
    /// <param name="etapaId">ID da etapa</param>
    /// <returns>Lista de atividades</returns>
    [HttpGet("etapa/{etapaId}")]
    [ProducesResponseType(typeof(List<AtividadeResponseDTO>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAtividadesByEtapaId(int etapaId)
    {
        var result = await _atividadeService.GetAtividadesByEtapaIdAsync(etapaId);
        return Ok(result);
    }

    /// <summary>
    /// Lista todas as atividades de um projeto
    /// </summary>
    /// <param name="projetoId">ID do projeto</param>
    /// <returns>Lista de atividades</returns>
    [HttpGet("projeto/{projetoId}")]
    [ProducesResponseType(typeof(List<AtividadeResponseDTO>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAtividadesByProjetoId(int projetoId)
    {
        var result = await _atividadeService.GetAtividadesByProjetoIdAsync(projetoId);
        return Ok(result);
    }

    /// <summary>
    /// Deleta uma atividade
    /// </summary>
    /// <param name="id">ID da atividade</param>
    /// <returns>NoContent se sucesso</returns>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> DeleteAtividade(int id)
    {
        var email = GetUserEmail();
        if (string.IsNullOrEmpty(email))
            return Unauthorized();

        var perfil = await _permissionService.GetUserPerfilAsync(email);
        if (!_permissionService.CanDelete(perfil, "atividade"))
            return Forbid();

        await _atividadeService.DeleteAtividadeAsync(id);
        return NoContent();
    }

    /// <summary>
    /// Inicia uma atividade (seta DT_INICIO_REAL)
    /// </summary>
    /// <param name="id">ID da atividade</param>
    /// <param name="request">Opcionalmente pode informar a data de início</param>
    /// <returns>Atividade atualizada</returns>
    [HttpPut("{id}/iniciar")]
    [ProducesResponseType(typeof(AtividadeResponseDTO), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> IniciarAtividade(int id, [FromBody] IniciarAtividadeRequest? request = null)
    {
        var email = GetUserEmail();
        if (string.IsNullOrEmpty(email))
            return Unauthorized();

        var perfil = await _permissionService.GetUserPerfilAsync(email);
        if (!_permissionService.CanEdit(perfil, "atividade"))
            return Forbid();

        var result = await _atividadeService.IniciarAtividadeAsync(id, request?.DataInicio);
        return Ok(result);
    }

    /// <summary>
    /// Conclui uma atividade (seta DT_TERMINO_REAL)
    /// </summary>
    /// <param name="id">ID da atividade</param>
    /// <param name="request">Opcionalmente pode informar a data de conclusão</param>
    /// <returns>Atividade atualizada</returns>
    [HttpPut("{id}/concluir")]
    [ProducesResponseType(typeof(AtividadeResponseDTO), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ConcluirAtividade(int id, [FromBody] ConcluirAtividadeRequest? request = null)
    {
        var email = GetUserEmail();
        if (string.IsNullOrEmpty(email))
            return Unauthorized();

        var perfil = await _permissionService.GetUserPerfilAsync(email);
        if (!_permissionService.CanEdit(perfil, "atividade"))
            return Forbid();

        var result = await _atividadeService.ConcluirAtividadeAsync(id, request?.DataConclusao);
        return Ok(result);
    }
}

/// <summary>
/// Request para iniciar atividade
/// </summary>
public class IniciarAtividadeRequest
{
    /// <summary>
    /// Data de início (opcional, usa DateTime.Now se não informado)
    /// </summary>
    public DateTime? DataInicio { get; set; }
}

/// <summary>
/// Request para concluir atividade
/// </summary>
public class ConcluirAtividadeRequest
{
    /// <summary>
    /// Data de conclusão (opcional, usa DateTime.Now se não informado)
    /// </summary>
    public DateTime? DataConclusao { get; set; }
}
