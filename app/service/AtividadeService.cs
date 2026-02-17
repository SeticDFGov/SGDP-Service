using api.Atividade;
using demanda_service.Helpers;
using demanda_service.Repositorio.Interface;
using Models;
using service;
using service.Interface;

namespace demanda_service.service;

/// <summary>
/// Serviço de Atividade - contém lógica de negócio e validações
/// </summary>
public class AtividadeService : IAtividadeService
{
    private readonly IAtividadeRepositorio _atividadeRepositorio;

    public AtividadeService(IAtividadeRepositorio atividadeRepositorio)
    {
        _atividadeRepositorio = atividadeRepositorio;
    }

    /// <summary>
    /// Cria uma nova atividade
    /// </summary>
    public async Task<AtividadeResponseDTO> CreateAtividadeAsync(AtividadeCreateDTO dto)
    {
        // Validação: Etapa existe?
        var etapa = await _atividadeRepositorio.GetEtapaByIdAsync(dto.EtapaProjetoId);
        if (etapa == null)
            throw new ApiException(ErrorCode.EtapaNaoEncontrada);

        // Validação: Data de término deve ser após data de início
        if (dto.DT_TERMINO_PREVISTO <= dto.DT_INICIO_PREVISTO)
            throw new ApiException(ErrorCode.DataInvalida, "Data de término deve ser posterior à data de início");

        // Converte datas de Brasília para UTC
        var atividade = new Atividade
        {
            EtapaProjetoId = dto.EtapaProjetoId,
            Titulo = dto.Titulo,
            Categoria = dto.Categoria,
            Descricao = dto.Descricao,
            DT_INICIO_PREVISTO = DateTimeHelper.ToUtc(dto.DT_INICIO_PREVISTO),
            DT_TERMINO_PREVISTO = DateTimeHelper.ToUtc(dto.DT_TERMINO_PREVISTO),
            RESPONSAVEL_ATIVIDADE = dto.RESPONSAVEL_ATIVIDADE,
            Order = dto.Order
        };

        _atividadeRepositorio.Add(atividade);
        await _atividadeRepositorio.SaveChangesAsync();

        return await GetAtividadeByIdAsync(atividade.AtividadeId);
    }

    /// <summary>
    /// Atualiza uma atividade existente (apenas campos editáveis)
    /// </summary>
    public async Task<AtividadeResponseDTO> UpdateAtividadeAsync(int id, AtividadeUpdateDTO dto)
    {
        var atividade = await _atividadeRepositorio.GetByIdAsync(id);
        if (atividade == null)
            throw new ApiException(ErrorCode.AtividadeNaoEncontrada);

        // Validação: Se informar DT_TERMINO_REAL, deve ter DT_INICIO_REAL
        if (dto.DT_TERMINO_REAL.HasValue && !dto.DT_INICIO_REAL.HasValue && !atividade.DT_INICIO_REAL.HasValue)
            throw new ApiException(ErrorCode.DataInvalida, "Não é possível concluir uma atividade sem data de início");

        // Atualiza apenas campos editáveis
        if (dto.DT_INICIO_REAL.HasValue)
            atividade.DT_INICIO_REAL = DateTimeHelper.ToUtc(dto.DT_INICIO_REAL.Value);

        if (dto.DT_TERMINO_REAL.HasValue)
            atividade.DT_TERMINO_REAL = DateTimeHelper.ToUtc(dto.DT_TERMINO_REAL.Value);

        if (dto.Categoria != null)
            atividade.Categoria = dto.Categoria;

        if (dto.Descricao != null)
            atividade.Descricao = dto.Descricao;

        if (dto.RESPONSAVEL_ATIVIDADE != null)
            atividade.RESPONSAVEL_ATIVIDADE = dto.RESPONSAVEL_ATIVIDADE;

        await _atividadeRepositorio.SaveChangesAsync();

        return await GetAtividadeByIdAsync(id);
    }

    /// <summary>
    /// Busca uma atividade por ID
    /// </summary>
    public async Task<AtividadeResponseDTO> GetAtividadeByIdAsync(int id)
    {
        var atividade = await _atividadeRepositorio.GetByIdAsync(id);
        if (atividade == null)
            throw new ApiException(ErrorCode.AtividadeNaoEncontrada);

        return MapToResponseDTO(atividade);
    }

    /// <summary>
    /// Busca todas as atividades de uma etapa
    /// </summary>
    public async Task<List<AtividadeResponseDTO>> GetAtividadesByEtapaIdAsync(int etapaId)
    {
        var atividades = await _atividadeRepositorio.GetAtividadesByEtapaIdAsync(etapaId);
        return atividades.Select(MapToResponseDTO).ToList();
    }

    /// <summary>
    /// Busca todas as atividades de um projeto
    /// </summary>
    public async Task<List<AtividadeResponseDTO>> GetAtividadesByProjetoIdAsync(int projetoId)
    {
        var atividades = await _atividadeRepositorio.GetAtividadesByProjetoIdAsync(projetoId);
        return atividades.Select(MapToResponseDTO).ToList();
    }

    /// <summary>
    /// Deleta uma atividade
    /// </summary>
    public async Task DeleteAtividadeAsync(int id)
    {
        var atividade = await _atividadeRepositorio.GetByIdAsync(id);
        if (atividade == null)
            throw new ApiException(ErrorCode.AtividadeNaoEncontrada);

        _atividadeRepositorio.Remove(atividade);
        await _atividadeRepositorio.SaveChangesAsync();
    }

    /// <summary>
    /// Inicia uma atividade (seta DT_INICIO_REAL)
    /// </summary>
    public async Task<AtividadeResponseDTO> IniciarAtividadeAsync(int id, DateTime? dtInicio = null)
    {
        var atividade = await _atividadeRepositorio.GetByIdAsync(id);
        if (atividade == null)
            throw new ApiException(ErrorCode.AtividadeNaoEncontrada);

        // Validação: Já foi iniciada?
        if (atividade.DT_INICIO_REAL.HasValue)
            throw new ApiException(ErrorCode.AtividadeJaIniciada, "Atividade já foi iniciada");

        // Usa data atual UTC se não informada, ou converte a data informada
        atividade.DT_INICIO_REAL = dtInicio.HasValue
            ? DateTimeHelper.ToUtc(dtInicio.Value)
            : DateTime.UtcNow;

        await _atividadeRepositorio.SaveChangesAsync();

        return await GetAtividadeByIdAsync(id);
    }

    /// <summary>
    /// Conclui uma atividade (seta DT_TERMINO_REAL)
    /// </summary>
    public async Task<AtividadeResponseDTO> ConcluirAtividadeAsync(int id, DateTime? dtTermino = null)
    {
        var atividade = await _atividadeRepositorio.GetByIdAsync(id);
        if (atividade == null)
            throw new ApiException(ErrorCode.AtividadeNaoEncontrada);

        // Validação: Já foi concluída?
        if (atividade.DT_TERMINO_REAL.HasValue)
            throw new ApiException(ErrorCode.AtividadeJaConcluida, "Atividade já foi concluída");

        // Validação: Foi iniciada?
        if (!atividade.DT_INICIO_REAL.HasValue)
        {
            // Auto-inicia com a data de início prevista
            atividade.DT_INICIO_REAL = atividade.DT_INICIO_PREVISTO;
        }

        // Usa data atual UTC se não informada, ou converte a data informada
        atividade.DT_TERMINO_REAL = dtTermino.HasValue
            ? DateTimeHelper.ToUtc(dtTermino.Value)
            : DateTime.UtcNow;

        await _atividadeRepositorio.SaveChangesAsync();

        return await GetAtividadeByIdAsync(id);
    }

    /// <summary>
    /// Mapeia Atividade para AtividadeResponseDTO
    /// </summary>
    private async Task<AtividadeResponseDTO> MapToResponseDTOAsync(Atividade atividade)
    {
        // Buscar todas as atividades da mesma etapa para calcular peso
        var atividadesDaEtapa = await _atividadeRepositorio.GetByEtapaIdAsync(atividade.EtapaProjetoId);
        var totalDiasEtapa = atividadesDaEtapa.Sum(a => a.DIAS_PREVISTOS);

        // Calcular PESO_ATIVIDADE (% que essa atividade representa no entregável)
        decimal pesoAtividade = totalDiasEtapa > 0
            ? (decimal)atividade.DIAS_PREVISTOS / totalDiasEtapa * 100
            : 0;

        // Calcular PERCENT_EXECUTADO (0-100% de conclusão da atividade)
        decimal percentExecutado = CalcularPercentExecutado(atividade);

        return new AtividadeResponseDTO
        {
            AtividadeId = atividade.AtividadeId,
            EtapaProjetoId = atividade.EtapaProjetoId,
            EtapaNome = atividade.Etapa?.NM_ETAPA,
            ProjetoId = atividade.Etapa?.NM_PROJETO?.projetoId,
            ProjetoNome = atividade.Etapa?.NM_PROJETO?.NM_PROJETO,
            Titulo = atividade.Titulo,
            Categoria = atividade.Categoria,
            Descricao = atividade.Descricao,
            DT_INICIO_PREVISTO = DateTimeHelper.ToBrasilia(atividade.DT_INICIO_PREVISTO),
            DT_TERMINO_PREVISTO = DateTimeHelper.ToBrasilia(atividade.DT_TERMINO_PREVISTO),
            DT_INICIO_REAL = DateTimeHelper.ToBrasilia(atividade.DT_INICIO_REAL),
            DT_TERMINO_REAL = DateTimeHelper.ToBrasilia(atividade.DT_TERMINO_REAL),
            SITUACAO = atividade.SITUACAO,
            RESPONSAVEL_ATIVIDADE = atividade.RESPONSAVEL_ATIVIDADE,
            PERCENT_PLANEJADO = atividade.PERCENT_PLANEJADO,
            DIAS_PREVISTOS = atividade.DIAS_PREVISTOS,
            DIAS_EXECUTADOS = atividade.DIAS_EXECUTADOS,
            Order = atividade.Order,
            PESO_ATIVIDADE = Math.Round(pesoAtividade, 2),
            PERCENT_EXECUTADO = Math.Round(percentExecutado, 2)
        };
    }

    /// <summary>
    /// Calcula o percentual de execução de uma atividade (0-100%)
    /// </summary>
    private decimal CalcularPercentExecutado(Atividade atividade)
    {
        // Se concluída: 100%
        if (atividade.DT_TERMINO_REAL.HasValue)
            return 100m;

        // Se não iniciada: 0%
        if (!atividade.DT_INICIO_REAL.HasValue)
            return 0m;

        // Se iniciada mas não concluída: calcular baseado no tempo decorrido
        var hoje = DateTime.UtcNow;
        var diasDecorridos = (hoje - atividade.DT_INICIO_REAL.Value).Days;
        var diasPrevistos = atividade.DIAS_PREVISTOS;

        if (diasPrevistos <= 0)
            return 50m; // Se não há prazo definido, considera 50% quando em andamento

        var percent = (decimal)diasDecorridos / diasPrevistos * 100;

        // Limita entre 1% e 99% (nunca 0% nem 100% quando em andamento)
        return Math.Max(1m, Math.Min(99m, percent));
    }

    /// <summary>
    /// Versão síncrona do MapToResponseDTO (para compatibilidade)
    /// </summary>
    private AtividadeResponseDTO MapToResponseDTO(Atividade atividade)
    {
        return MapToResponseDTOAsync(atividade).GetAwaiter().GetResult();
    }
}
