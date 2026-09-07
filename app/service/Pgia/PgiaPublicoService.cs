using System.Security.Cryptography;
using api.Common;
using api.Pgia;
using demanda_service.Helpers;
using Microsoft.EntityFrameworkCore;
using Models.Pgia;
using Repositorio.Interface;
using service.Interface;

namespace service.Pgia;

/// <summary>
/// Transparência pública: Registro Público de Sistemas de IA (art. 24) e
/// solicitações do cidadão (arts. 11, IV e 23). A superfície anônima devolve
/// apenas os campos do art. 24 e nunca dados pessoais do solicitante.
/// </summary>
public class PgiaPublicoService : IPgiaPublicoService
{
    private readonly IPgiaPublicoRepositorio _repositorio;

    // Tentativas de sorteio antes de desistir; a colisão é improvável e o laço é curto
    private const int TentativasDeProtocolo = 10;

    public PgiaPublicoService(IPgiaPublicoRepositorio repositorio)
    {
        _repositorio = repositorio;
    }

    // ── Registro Público (art. 24) ────────────────────────────────────────────

    public async Task<PagedResponse<PgiaRegistroPublicoItemResponse>> ListarRegistroPublicoAsync(PagedRequest request)
    {
        var pageSize = Math.Clamp(request.PageSize, 1, 100);
        // Teto no Page: (page-1)*pageSize é aritmética int e estouraria com valores absurdos
        var page = Math.Clamp(request.Page, 1, int.MaxValue / pageSize);
        var skip = (page - 1) * pageSize;

        var query = _repositorio.QuerySistemasPublicados();

        var totalItems = await query.CountAsync();
        var sistemas = await query
            .OrderBy(s => s.Orgao!.Sigla)
            .ThenBy(s => s.Denominacao)
            .Skip(skip)
            .Take(pageSize)
            .ToListAsync();

        // Dois lookups em lote para a página inteira, em vez de um por sistema
        var ids = sistemas.Select(s => s.Id).ToList();
        var urlsDeAia = await _repositorio.ListarUrlsDeAiaPublicadaAsync(ids);
        var indicadores = await _repositorio.ContarIndicadoresPublicadosAsync(ids);

        var itens = sistemas.Select(s => new PgiaRegistroPublicoItemResponse
        {
            Id = s.Id,
            Denominacao = s.Denominacao,
            Finalidade = s.Finalidade,
            OrgaoSigla = s.Orgao?.Sigla ?? string.Empty,
            OrgaoNome = s.Orgao?.Nome ?? string.Empty,
            ClassificacaoRiscoAtual = s.ClassificacaoRiscoAtual,
            NaturezaDecisoes = s.NaturezaDecisoes,
            EfeitosCidadao = s.EfeitosCidadao,
            AiaResultadoUrl = urlsDeAia.TryGetValue(s.Id, out var url) ? url : null,
            IndicadoresPublicados = indicadores.TryGetValue(s.Id, out var total) ? total : 0,
            DataPublicacaoRegistro = s.DataPublicacaoRegistro
        }).ToList();

        return new PagedResponse<PgiaRegistroPublicoItemResponse>(itens, totalItems, page, pageSize);
    }

    // ── Solicitações do cidadão (arts. 11, IV e 23) ───────────────────────────

    public async Task<PgiaSolicitacaoProtocoloResponse> CriarSolicitacaoAsync(PgiaSolicitacaoCreateDTO dto)
    {
        // Só se pede sobre o que é público: o inventário interno não se revela aqui
        var sistema = await _repositorio.GetSistemaPublicadoAsync(dto.SistemaIaId)
            ?? throw new ApiException(ErrorCode.PgiaSistemaNaoPublicado,
                "O sistema informado não está no Registro Público.");

        if (!PgiaDominios.TipoSolicitacao.Todos.Contains(dto.Tipo))
            throw new ApiException(ErrorCode.PgiaSolicitacaoInvalida,
                "Escolha um dos tipos de pedido disponíveis.");

        var nome = dto.SolicitanteNome?.Trim() ?? string.Empty;
        var contato = dto.SolicitanteContato?.Trim() ?? string.Empty;
        var descricao = dto.Descricao?.Trim() ?? string.Empty;

        if (nome.Length == 0)
            throw new ApiException(ErrorCode.PgiaSolicitacaoInvalida, "Informe o seu nome.");

        if (contato.Length == 0)
            throw new ApiException(ErrorCode.PgiaSolicitacaoInvalida,
                "Informe um e-mail ou telefone para receber a resposta.");

        if (descricao.Length == 0)
            throw new ApiException(ErrorCode.PgiaSolicitacaoInvalida, "Descreva o seu pedido.");

        // Reclamação de titular de dados vai direto ao Encarregado de Dados (art. 11, IV)
        var paraDpo = dto.Tipo == PgiaDominios.TipoSolicitacao.ReclamacaoDados;

        var agora = DateTime.UtcNow;
        var solicitacao = new PgiaSolicitacaoCidadao
        {
            Protocolo = await GerarProtocoloAsync(),
            SistemaIaId = sistema.Id,
            Sistema = sistema,
            Tipo = dto.Tipo,
            SolicitanteNome = nome,
            SolicitanteContato = contato,
            ReferenciaDecisao = string.IsNullOrWhiteSpace(dto.ReferenciaDecisao) ? null : dto.ReferenciaDecisao.Trim(),
            Descricao = descricao,
            DataAbertura = agora,
            Status = paraDpo
                ? PgiaDominios.StatusSolicitacao.EncaminhadaDpo
                : PgiaDominios.StatusSolicitacao.Recebida,
            EncaminhadaDpo = paraDpo,
            CriadoEm = agora,
            // Abertura anônima: não há usuário autenticado a registrar
            CriadoPor = "publico"
        };

        _repositorio.AddSolicitacao(solicitacao);
        await _repositorio.SaveChangesAsync();

        // A abertura devolve só o protocolo
        return new PgiaSolicitacaoProtocoloResponse { Protocolo = solicitacao.Protocolo };
    }

    // Letras maiúsculas sem I e O (confundem com 1 e 0): o cidadão anota o protocolo à mão
    private const string LetrasDeProtocolo = "ABCDEFGHJKLMNPQRSTUVWXYZ";

    /// <summary>
    /// "PGIA-{ano}-{6 dígitos}-{4 letras}", sorteado no servidor e conferido contra o banco.
    /// O protocolo é a única credencial de acompanhamento: o sufixo de letras leva o
    /// espaço a ~3×10^11 por ano, inviabilizando varredura do espaço inteiro.
    /// </summary>
    private async Task<string> GerarProtocoloAsync()
    {
        var ano = DateTimeHelper.TodayBrasilia().Year;

        for (var tentativa = 0; tentativa < TentativasDeProtocolo; tentativa++)
        {
            var sufixo = string.Create(4, LetrasDeProtocolo,
                (span, letras) =>
                {
                    for (var i = 0; i < span.Length; i++)
                        span[i] = letras[RandomNumberGenerator.GetInt32(letras.Length)];
                });
            var protocolo = $"PGIA-{ano}-{RandomNumberGenerator.GetInt32(0, 1_000_000):D6}-{sufixo}";
            if (!await _repositorio.ProtocoloExisteAsync(protocolo))
                return protocolo;
        }

        throw new ApiException(ErrorCode.PgiaSolicitacaoInvalida,
            "Não foi possível gerar o protocolo agora. Tente novamente em instantes.");
    }

    public async Task<PgiaSolicitacaoPublicaResponse?> ConsultarPorProtocoloAsync(string protocolo)
    {
        if (string.IsNullOrWhiteSpace(protocolo)) return null;

        var solicitacao = await _repositorio.GetSolicitacaoPorProtocoloAsync(protocolo.Trim());
        // Protocolo desconhecido não distingue "não existe" de "não é seu"
        return solicitacao == null ? null : MapPublica(solicitacao);
    }

    // ── Tratamento pelo órgão ─────────────────────────────────────────────────

    public async Task<PgiaSolicitacaoCidadao?> GetSolicitacaoEntidadeAsync(long id)
    {
        return await _repositorio.GetSolicitacaoByIdAsync(id);
    }

    public async Task<List<PgiaSolicitacaoOrgaoResponse>> ListarSolicitacoesPorOrgaoAsync(long orgaoId)
    {
        var lista = await _repositorio.ListarSolicitacoesPorOrgaoAsync(orgaoId);
        return lista.Select(MapOrgao).ToList();
    }

    public async Task<PgiaSolicitacaoOrgaoResponse> ResponderAsync(
        long id, PgiaSolicitacaoRespostaDTO dto, PgiaUserContext ctx)
    {
        var solicitacao = await _repositorio.GetSolicitacaoByIdAsync(id)
            ?? throw new ApiException(ErrorCode.PgiaSolicitacaoNaoEncontrada);

        if (string.IsNullOrWhiteSpace(dto.Resposta))
            throw new ApiException(ErrorCode.PgiaSolicitacaoInvalida,
                "Escreva a resposta ao cidadão, em linguagem simples (art. 23, III).");

        var agora = DateTime.UtcNow;
        solicitacao.Resposta = dto.Resposta.Trim();
        solicitacao.RespondidoPor = ctx.UserId;
        solicitacao.DataResposta = agora;
        solicitacao.Status = PgiaDominios.StatusSolicitacao.Respondida;
        solicitacao.AlteradoEm = agora;
        solicitacao.AlteradoPor = ctx.Email;

        await _repositorio.SaveChangesAsync();

        var salva = await _repositorio.GetSolicitacaoByIdAsync(id);
        return MapOrgao(salva ?? solicitacao);
    }

    public async Task<PgiaSolicitacaoOrgaoResponse> EncaminharAoDpoAsync(long id, PgiaUserContext ctx)
    {
        var solicitacao = await _repositorio.GetSolicitacaoByIdAsync(id)
            ?? throw new ApiException(ErrorCode.PgiaSolicitacaoNaoEncontrada);

        solicitacao.EncaminhadaDpo = true;
        solicitacao.Status = PgiaDominios.StatusSolicitacao.EncaminhadaDpo;
        solicitacao.AlteradoEm = DateTime.UtcNow;
        solicitacao.AlteradoPor = ctx.Email;

        await _repositorio.SaveChangesAsync();
        return MapOrgao(solicitacao);
    }

    public async Task<PgiaSolicitacaoOrgaoResponse> MarcarEmAnaliseAsync(long id, PgiaUserContext ctx)
    {
        var solicitacao = await _repositorio.GetSolicitacaoByIdAsync(id)
            ?? throw new ApiException(ErrorCode.PgiaSolicitacaoNaoEncontrada);

        solicitacao.Status = "Em análise";
        solicitacao.AlteradoEm = DateTime.UtcNow;
        solicitacao.AlteradoPor = ctx.Email;

        await _repositorio.SaveChangesAsync();
        return MapOrgao(solicitacao);
    }

    // ── Mapeamentos ───────────────────────────────────────────────────────────

    /// <summary>Sem nome, contato, descrição ou referência: o público vê o andamento.</summary>
    private static PgiaSolicitacaoPublicaResponse MapPublica(PgiaSolicitacaoCidadao s) => new()
    {
        Protocolo = s.Protocolo,
        SistemaDenominacao = s.Sistema?.Denominacao ?? string.Empty,
        OrgaoSigla = s.Sistema?.Orgao?.Sigla ?? string.Empty,
        Tipo = s.Tipo,
        DataAbertura = s.DataAbertura,
        Status = s.Status,
        Resposta = s.Resposta,
        DataResposta = s.DataResposta
    };

    private static PgiaSolicitacaoOrgaoResponse MapOrgao(PgiaSolicitacaoCidadao s) => new()
    {
        Id = s.Id,
        Protocolo = s.Protocolo,
        SistemaIaId = s.SistemaIaId,
        SistemaDenominacao = s.Sistema?.Denominacao ?? string.Empty,
        OrgaoSigla = s.Sistema?.Orgao?.Sigla ?? string.Empty,
        Tipo = s.Tipo,
        SolicitanteNome = s.SolicitanteNome,
        SolicitanteContato = s.SolicitanteContato,
        ReferenciaDecisao = s.ReferenciaDecisao,
        Descricao = s.Descricao,
        DataAbertura = s.DataAbertura,
        Status = s.Status,
        Resposta = s.Resposta,
        RespondidoPorNome = s.RespondidoPorUser?.Nome,
        DataResposta = s.DataResposta,
        EncaminhadaDpo = s.EncaminhadaDpo
    };
}
