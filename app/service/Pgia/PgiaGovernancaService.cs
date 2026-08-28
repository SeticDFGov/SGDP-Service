using api.Pgia;
using Models.Pgia;
using Repositorio.Interface;
using service.Interface;

namespace service.Pgia;

/// <summary>
/// Governança central do PGIA: deliberações do CGTIC (art. 7º), homologação de
/// plataformas públicas de IA generativa (arts. 8º, IV, 18 e 20), normas
/// complementares (arts. 8º, III e 26) e autorizações excepcionais (arts. 18, § 2º e 21).
/// </summary>
public class PgiaGovernancaService : IPgiaGovernancaService
{
    private readonly IPgiaGovernancaRepositorio _repositorio;

    public PgiaGovernancaService(IPgiaGovernancaRepositorio repositorio)
    {
        _repositorio = repositorio;
    }

    public async Task<PgiaSistemaIa?> GetSistemaEntidadeAsync(long sistemaId)
    {
        return await _repositorio.GetSistemaByIdAsync(sistemaId);
    }

    public async Task<List<PgiaSistemaResumoResponse>> ListarSistemasResumoAsync()
    {
        var sistemas = await _repositorio.ListarSistemasAsync();
        return sistemas.Select(s => new PgiaSistemaResumoResponse
        {
            Id = s.Id,
            Denominacao = s.Denominacao,
            OrgaoSigla = s.Orgao?.Sigla ?? string.Empty,
            ClassificacaoRiscoAtual = s.ClassificacaoRiscoAtual,
            StatusCicloVida = s.StatusCicloVida,
            SituacaoHomologacao = s.SituacaoHomologacao
        }).ToList();
    }

    // ── Deliberações do CGTIC ─────────────────────────────────────────────────

    public async Task<List<PgiaDeliberacaoResponse>> ListarDeliberacoesAsync()
    {
        var lista = await _repositorio.ListarDeliberacoesAsync();
        return lista.Select(MapDeliberacao).ToList();
    }

    public async Task<List<PgiaDeliberacaoResponse>> ListarDeliberacoesPorSistemaAsync(long sistemaId)
    {
        var lista = await _repositorio.ListarDeliberacoesPorSistemaAsync(sistemaId);
        return lista.Select(MapDeliberacao).ToList();
    }

    public async Task<PgiaDeliberacaoResponse> CriarDeliberacaoAsync(PgiaDeliberacaoCreateDTO dto, PgiaUserContext ctx)
    {
        if (!PgiaDominios.TipoDeliberacao.Todos.Contains(dto.Tipo))
            throw new ApiException(ErrorCode.PgiaDominioInvalido, $"Tipo de deliberação inválido: {dto.Tipo}");

        if (!PgiaDominios.ResultadoDeliberacao.Todos.Contains(dto.Resultado))
            throw new ApiException(ErrorCode.PgiaDominioInvalido, $"Resultado da deliberação inválido: {dto.Resultado}");

        if (dto.DataDeliberacao == default)
            throw new ApiException(ErrorCode.PgiaDominioInvalido, "Informe a data da deliberação.");

        PgiaSistemaIa? sistema = null;
        if (dto.SistemaIaId != null)
        {
            sistema = await _repositorio.GetSistemaByIdAsync(dto.SistemaIaId.Value)
                ?? throw new ApiException(ErrorCode.PgiaSistemaNaoEncontrado);
        }

        if (dto.DocumentoId != null && await _repositorio.GetDocumentoByIdAsync(dto.DocumentoId.Value) == null)
            throw new ApiException(ErrorCode.PgiaDocumentoNaoEncontrado);

        if (dto.ContratoId != null && await _repositorio.GetContratoByIdAsync(dto.ContratoId.Value) == null)
            throw new ApiException(ErrorCode.PgiaContratoNaoEncontrado);

        var agora = DateTime.UtcNow;
        var deliberacao = new PgiaDeliberacaoCgtic
        {
            Tipo = dto.Tipo,
            SistemaIaId = dto.SistemaIaId,
            Sistema = sistema,
            ContratoId = dto.ContratoId,
            DataDeliberacao = dto.DataDeliberacao,
            Resultado = dto.Resultado,
            NumeroAto = string.IsNullOrWhiteSpace(dto.NumeroAto) ? null : dto.NumeroAto.Trim(),
            Ementa = string.IsNullOrWhiteSpace(dto.Ementa) ? null : dto.Ementa.Trim(),
            DocumentoId = dto.DocumentoId,
            CriadoEm = agora,
            CriadoPor = ctx.Email
        };

        _repositorio.AddDeliberacao(deliberacao);
        // Salva antes de vincular ao sistema: o Id gerado é a chave que
        // pgia_sistema_ia.deliberacao_homologacao_id passa a apontar (FK circular).
        await _repositorio.SaveChangesAsync();

        AplicarHomologacaoDelegada(sistema, deliberacao, ctx);
        await _repositorio.SaveChangesAsync();

        return MapDeliberacao(deliberacao);
    }

    /// <summary>
    /// Sistema delegado ao comitê: a deliberação que decide o enquadramento ou a
    /// aquisição de Alto Risco também conclui a homologação (favorável aprova,
    /// desfavorável veta, diligência mantém a fila).
    /// </summary>
    private static void AplicarHomologacaoDelegada(
        PgiaSistemaIa? sistema, PgiaDeliberacaoCgtic deliberacao, PgiaUserContext ctx)
    {
        if (sistema == null) return;
        if (!PgiaDominios.TipoDeliberacao.DecidemHomologacao.Contains(deliberacao.Tipo)) return;
        if (sistema.SituacaoHomologacao != PgiaDominios.SituacaoHomologacao.AguardandoCgtic) return;

        if (deliberacao.Resultado == PgiaDominios.ResultadoDeliberacao.Favoravel)
            sistema.SituacaoHomologacao = PgiaDominios.SituacaoHomologacao.Aprovado;
        else if (deliberacao.Resultado == PgiaDominios.ResultadoDeliberacao.Desfavoravel)
            sistema.SituacaoHomologacao = PgiaDominios.SituacaoHomologacao.Vetado;
        else
            return; // Em diligência: o sistema segue aguardando o comitê

        var agora = DateTime.UtcNow;
        sistema.DeliberacaoHomologacaoId = deliberacao.Id;
        sistema.AvaliadoEm = agora;
        sistema.AvaliadoPor = ctx.UserId;
        sistema.AvaliacaoParecer = string.IsNullOrWhiteSpace(deliberacao.Ementa)
            ? $"Deliberação do CGTIC nº {deliberacao.NumeroAto ?? deliberacao.Id.ToString()}"
            : deliberacao.Ementa;
        sistema.AlteradoEm = agora;
        sistema.AlteradoPor = ctx.Email;
    }

    // ── Plataformas públicas de IA generativa ─────────────────────────────────

    public async Task<List<PgiaPlataformaResponse>> ListarPlataformasAsync()
    {
        var lista = await _repositorio.ListarPlataformasAsync();
        return lista.Select(MapPlataforma).ToList();
    }

    public async Task<PgiaPlataformaResponse> CriarPlataformaAsync(PgiaPlataformaCreateDTO dto, PgiaUserContext ctx)
    {
        var nome = ValidarPlataforma(dto);

        var existente = await _repositorio.GetPlataformaByNomeAsync(nome);
        if (existente != null)
            throw new ApiException(ErrorCode.PgiaPlataformaJaExiste,
                $"Já existe plataforma cadastrada com o nome {nome}.");

        var plataforma = new PgiaPlataformaIaGenerativa
        {
            CriadoEm = DateTime.UtcNow,
            CriadoPor = ctx.Email
        };
        AplicarDadosPlataforma(plataforma, dto, nome);

        _repositorio.AddPlataforma(plataforma);
        await _repositorio.SaveChangesAsync();
        return MapPlataforma(plataforma);
    }

    public async Task<PgiaPlataformaResponse> AtualizarPlataformaAsync(long id, PgiaPlataformaUpdateDTO dto, PgiaUserContext ctx)
    {
        var plataforma = await _repositorio.GetPlataformaByIdAsync(id)
            ?? throw new ApiException(ErrorCode.PgiaPlataformaNaoEncontrada);

        var nome = ValidarPlataforma(dto);

        var existente = await _repositorio.GetPlataformaByNomeAsync(nome);
        if (existente != null && existente.Id != id)
            throw new ApiException(ErrorCode.PgiaPlataformaJaExiste,
                $"Já existe plataforma cadastrada com o nome {nome}.");

        AplicarDadosPlataforma(plataforma, dto, nome);
        plataforma.AlteradoEm = DateTime.UtcNow;
        plataforma.AlteradoPor = ctx.Email;

        await _repositorio.SaveChangesAsync();
        return MapPlataforma(plataforma);
    }

    private static string ValidarPlataforma(PgiaPlataformaCreateDTO dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Nome))
            throw new ApiException(ErrorCode.PgiaDominioInvalido, "Informe o nome da plataforma.");

        if (!PgiaDominios.StatusHomologacaoPlataforma.Todos.Contains(dto.StatusHomologacao))
            throw new ApiException(ErrorCode.PgiaDominioInvalido,
                $"Situação de homologação inválida: {dto.StatusHomologacao}");

        return dto.Nome.Trim();
    }

    private static void AplicarDadosPlataforma(PgiaPlataformaIaGenerativa plataforma, PgiaPlataformaCreateDTO dto, string nome)
    {
        plataforma.Nome = nome;
        plataforma.Fornecedor = dto.Fornecedor?.Trim();
        plataforma.Url = dto.Url?.Trim();
        plataforma.StatusHomologacao = dto.StatusHomologacao;
        plataforma.AptaDadosPessoaisSigilosos = dto.AptaDadosPessoaisSigilosos;
        plataforma.AtoHomologacao = dto.AtoHomologacao?.Trim();
        plataforma.DataAto = dto.DataAto;
        plataforma.PublicadaRelacaoEm = dto.PublicadaRelacaoEm;
        plataforma.DiretrizesUso = dto.DiretrizesUso?.Trim();
    }

    // ── Normas complementares ─────────────────────────────────────────────────

    public async Task<List<PgiaNormaResponse>> ListarNormasAsync()
    {
        var lista = await _repositorio.ListarNormasAsync();
        return lista.Select(MapNorma).ToList();
    }

    public async Task<PgiaNormaResponse> CriarNormaAsync(PgiaNormaCreateDTO dto, PgiaUserContext ctx)
    {
        ValidarNorma(dto);

        var norma = new PgiaNormaComplementar
        {
            CriadoEm = DateTime.UtcNow,
            CriadoPor = ctx.Email
        };
        AplicarDadosNorma(norma, dto);

        _repositorio.AddNorma(norma);
        await _repositorio.SaveChangesAsync();
        return MapNorma(norma);
    }

    public async Task<PgiaNormaResponse> AtualizarNormaAsync(long id, PgiaNormaUpdateDTO dto, PgiaUserContext ctx)
    {
        var norma = await _repositorio.GetNormaByIdAsync(id)
            ?? throw new ApiException(ErrorCode.PgiaNormaNaoEncontrada);

        ValidarNorma(dto);
        AplicarDadosNorma(norma, dto);
        norma.AlteradoEm = DateTime.UtcNow;
        norma.AlteradoPor = ctx.Email;

        await _repositorio.SaveChangesAsync();
        return MapNorma(norma);
    }

    private static void ValidarNorma(PgiaNormaCreateDTO dto)
    {
        if (!PgiaDominios.TipoNorma.Todos.Contains(dto.Tipo))
            throw new ApiException(ErrorCode.PgiaDominioInvalido, $"Tipo de norma inválido: {dto.Tipo}");

        if (!PgiaDominios.EmissorNorma.Todos.Contains(dto.Emissor))
            throw new ApiException(ErrorCode.PgiaDominioInvalido, $"Emissor da norma inválido: {dto.Emissor}");

        if (string.IsNullOrWhiteSpace(dto.Ementa))
            throw new ApiException(ErrorCode.PgiaDominioInvalido, "Informe a ementa da norma.");

        if (dto.DataPublicacao == default)
            throw new ApiException(ErrorCode.PgiaDominioInvalido, "Informe a data de publicação.");
    }

    private static void AplicarDadosNorma(PgiaNormaComplementar norma, PgiaNormaCreateDTO dto)
    {
        norma.Tipo = dto.Tipo;
        norma.Numero = string.IsNullOrWhiteSpace(dto.Numero) ? null : dto.Numero.Trim();
        norma.Ementa = dto.Ementa.Trim();
        norma.Emissor = dto.Emissor;
        norma.DataPublicacao = dto.DataPublicacao;
        norma.Url = dto.Url?.Trim();
        norma.Vigente = dto.Vigente;
        norma.AprovadaCgticEm = dto.AprovadaCgticEm;
    }

    // ── Autorizações excepcionais ─────────────────────────────────────────────

    public async Task<List<PgiaAutorizacaoResponse>> ListarAutorizacoesAsync(long? orgaoId)
    {
        var lista = await _repositorio.ListarAutorizacoesAsync(orgaoId);
        return lista.Select(MapAutorizacao).ToList();
    }

    public async Task<PgiaAutorizacaoResponse> CriarAutorizacaoAsync(PgiaAutorizacaoCreateDTO dto, PgiaUserContext ctx)
    {
        var orgao = await ValidarAutorizacaoAsync(dto);

        var autorizacao = new PgiaAutorizacaoExcepcional
        {
            Orgao = orgao,
            Ativo = true,
            CriadoEm = DateTime.UtcNow,
            CriadoPor = ctx.Email
        };
        AplicarDadosAutorizacao(autorizacao, dto);

        _repositorio.AddAutorizacao(autorizacao);
        await _repositorio.SaveChangesAsync();

        var salva = await _repositorio.GetAutorizacaoByIdAsync(autorizacao.Id);
        return MapAutorizacao(salva ?? autorizacao);
    }

    public async Task<PgiaAutorizacaoResponse> AtualizarAutorizacaoAsync(long id, PgiaAutorizacaoUpdateDTO dto, PgiaUserContext ctx)
    {
        var autorizacao = await _repositorio.GetAutorizacaoByIdAsync(id)
            ?? throw new ApiException(ErrorCode.PgiaAutorizacaoNaoEncontrada);

        var orgao = await ValidarAutorizacaoAsync(dto);

        AplicarDadosAutorizacao(autorizacao, dto);
        // Revogação da exceção: só a edição mexe no Ativo (a criação nasce ativa)
        autorizacao.Ativo = dto.Ativo;
        autorizacao.Orgao = orgao;
        autorizacao.AlteradoEm = DateTime.UtcNow;
        autorizacao.AlteradoPor = ctx.Email;

        await _repositorio.SaveChangesAsync();

        var salva = await _repositorio.GetAutorizacaoByIdAsync(autorizacao.Id);
        return MapAutorizacao(salva ?? autorizacao);
    }

    private async Task<PgiaOrgao> ValidarAutorizacaoAsync(PgiaAutorizacaoCreateDTO dto)
    {
        if (!PgiaDominios.TipoAutorizacao.Todos.Contains(dto.Tipo))
            throw new ApiException(ErrorCode.PgiaDominioInvalido, $"Tipo de autorização inválido: {dto.Tipo}");

        if (!PgiaDominios.AutorizadaPor.Todos.Contains(dto.AutorizadaPor))
            throw new ApiException(ErrorCode.PgiaDominioInvalido,
                $"Instância autorizadora inválida: {dto.AutorizadaPor}");

        if (string.IsNullOrWhiteSpace(dto.Justificativa))
            throw new ApiException(ErrorCode.PgiaDominioInvalido, "Informe a justificativa da autorização.");

        if (dto.DataAutorizacao == default)
            throw new ApiException(ErrorCode.PgiaDominioInvalido, "Informe a data da autorização.");

        // Treinamento com dados do GDF depende de aprovação do CGTIC (art. 21)
        if (dto.Tipo == PgiaDominios.TipoAutorizacao.TreinamentoFornecedor && dto.DeliberacaoCgticId == null)
            throw new ApiException(ErrorCode.PgiaDominioInvalido,
                "O uso de dados do GDF para treinamento exige a deliberação do CGTIC que o aprovou (art. 21).");

        var orgao = await _repositorio.GetOrgaoByIdAsync(dto.OrgaoId)
            ?? throw new ApiException(ErrorCode.PgiaOrgaoNaoEncontrado);

        if (dto.PlataformaId != null)
        {
            _ = await _repositorio.GetPlataformaByIdAsync(dto.PlataformaId.Value)
                ?? throw new ApiException(ErrorCode.PgiaPlataformaNaoEncontrada);
        }

        if (dto.DeliberacaoCgticId != null)
        {
            _ = await _repositorio.GetDeliberacaoByIdAsync(dto.DeliberacaoCgticId.Value)
                ?? throw new ApiException(ErrorCode.PgiaDeliberacaoNaoEncontrada);
        }

        if (dto.AvaliacaoRiscosDocId != null)
        {
            _ = await _repositorio.GetDocumentoByIdAsync(dto.AvaliacaoRiscosDocId.Value)
                ?? throw new ApiException(ErrorCode.PgiaDocumentoNaoEncontrado);
        }

        if (dto.ContratoId != null)
        {
            _ = await _repositorio.GetContratoByIdAsync(dto.ContratoId.Value)
                ?? throw new ApiException(ErrorCode.PgiaContratoNaoEncontrado);
        }

        return orgao;
    }

    private static void AplicarDadosAutorizacao(PgiaAutorizacaoExcepcional autorizacao, PgiaAutorizacaoCreateDTO dto)
    {
        autorizacao.Tipo = dto.Tipo;
        autorizacao.OrgaoId = dto.OrgaoId;
        autorizacao.PlataformaId = dto.PlataformaId;
        autorizacao.ContratoId = dto.ContratoId;
        autorizacao.Justificativa = dto.Justificativa.Trim();
        autorizacao.AvaliacaoRiscosDocId = dto.AvaliacaoRiscosDocId;
        autorizacao.AutorizadaPor = dto.AutorizadaPor;
        autorizacao.DeliberacaoCgticId = dto.DeliberacaoCgticId;
        autorizacao.DataAutorizacao = dto.DataAutorizacao;
        autorizacao.VigenciaFim = dto.VigenciaFim;
    }

    // ── Mapeamentos ───────────────────────────────────────────────────────────

    private static PgiaDeliberacaoResponse MapDeliberacao(PgiaDeliberacaoCgtic d)
    {
        return new PgiaDeliberacaoResponse
        {
            Id = d.Id,
            Tipo = d.Tipo,
            SistemaIaId = d.SistemaIaId,
            ContratoId = d.ContratoId,
            SistemaDenominacao = d.Sistema?.Denominacao,
            OrgaoSigla = d.Sistema?.Orgao?.Sigla,
            DataDeliberacao = d.DataDeliberacao,
            Resultado = d.Resultado,
            NumeroAto = d.NumeroAto,
            Ementa = d.Ementa,
            DocumentoId = d.DocumentoId,
            CriadoEm = d.CriadoEm
        };
    }

    private static PgiaPlataformaResponse MapPlataforma(PgiaPlataformaIaGenerativa p)
    {
        return new PgiaPlataformaResponse
        {
            Id = p.Id,
            Nome = p.Nome,
            Fornecedor = p.Fornecedor,
            Url = p.Url,
            StatusHomologacao = p.StatusHomologacao,
            AptaDadosPessoaisSigilosos = p.AptaDadosPessoaisSigilosos,
            AtoHomologacao = p.AtoHomologacao,
            DataAto = p.DataAto,
            PublicadaRelacaoEm = p.PublicadaRelacaoEm,
            DiretrizesUso = p.DiretrizesUso,
            CriadoEm = p.CriadoEm
        };
    }

    private static PgiaNormaResponse MapNorma(PgiaNormaComplementar n)
    {
        return new PgiaNormaResponse
        {
            Id = n.Id,
            Tipo = n.Tipo,
            Numero = n.Numero,
            Ementa = n.Ementa,
            Emissor = n.Emissor,
            DataPublicacao = n.DataPublicacao,
            Url = n.Url,
            Vigente = n.Vigente,
            AprovadaCgticEm = n.AprovadaCgticEm,
            CriadoEm = n.CriadoEm
        };
    }

    private static PgiaAutorizacaoResponse MapAutorizacao(PgiaAutorizacaoExcepcional a)
    {
        return new PgiaAutorizacaoResponse
        {
            Id = a.Id,
            Tipo = a.Tipo,
            OrgaoId = a.OrgaoId,
            OrgaoSigla = a.Orgao?.Sigla ?? string.Empty,
            PlataformaId = a.PlataformaId,
            PlataformaNome = a.Plataforma?.Nome,
            ContratoId = a.ContratoId,
            Justificativa = a.Justificativa,
            AvaliacaoRiscosDocId = a.AvaliacaoRiscosDocId,
            AutorizadaPor = a.AutorizadaPor,
            DeliberacaoCgticId = a.DeliberacaoCgticId,
            DataAutorizacao = a.DataAutorizacao,
            VigenciaFim = a.VigenciaFim,
            Ativo = a.Ativo,
            CriadoEm = a.CriadoEm
        };
    }
}
