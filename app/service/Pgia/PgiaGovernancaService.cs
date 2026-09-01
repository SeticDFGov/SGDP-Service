using System.Net.Mail;
using api.Pgia;
using app.Auth;
using app.Models;
using demanda_service.Helpers;
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

    /// <summary>
    /// Instâncias centrais (SGDI, CGTIC e admin) enxergam o painel do comitê inteiro.
    /// Fonte única da regra que o PgiaGovernancaController aplica nas suas actions.
    /// </summary>
    public static bool EhEscopoCentral(PgiaUserContext ctx) =>
        ctx.PapelEfetivo is Perfis.Admin or PapeisPgia.Sgdi or PapeisPgia.Cgtic;

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

    // ── Gestão de pessoas pela SGDI (papel PGIA + unidade) ────────────────────

    /// <summary>
    /// Cap de segurança da listagem: a base de usuários do SGDP é grande, então a
    /// tela abre com os primeiros nomes e a busca refina. Aplicado SEMPRE (com ou
    /// sem filtro/órgão) para não devolver a base inteira a um filtro largo (ex. "@").
    /// </summary>
    public const int LimitePessoas = 200;

    public async Task<List<PgiaPessoaAcesso>> ListarPessoasAcessoAsync(string? filtro, long? orgaoId)
    {
        // Uma consulta só para todos os órgãos ativos: resolve Unidade → órgão em
        // lote, sem uma ida ao banco por pessoa
        var orgaoPorUnidade = await MapaOrgaoPorUnidadeAsync();

        Guid? unidadeId = null;
        if (orgaoId != null)
        {
            // Órgão inexistente, inativo ou sem unidade vinculada não tem pessoa alguma
            var orgao = orgaoPorUnidade.Values.FirstOrDefault(o => o.Id == orgaoId);
            if (orgao?.UnidadeId == null) return new List<PgiaPessoaAcesso>();
            unidadeId = orgao.UnidadeId;
        }

        // Cap sempre: qualquer combinação de filtro devolve no máximo LimitePessoas
        var usuarios = await _repositorio.ListarUsuariosAsync(filtro, unidadeId, LimitePessoas);
        var infos = await _repositorio.ListarAgenteInfosAsync(usuarios.Select(u => u.Id));

        return usuarios
            .Select(u => MapPessoaAcesso(u, ResolverOrgao(orgaoPorUnidade, u), InfoDe(infos, u.Id)))
            .ToList();
    }

    public async Task<PgiaPessoaAcesso> AtualizarVinculoPessoaAsync(Guid userId, PgiaPessoaVinculoDTO dto)
    {
        ValidarPapel(dto.PapelPgia);

        var user = await _repositorio.GetUserByIdAsync(userId)
            ?? throw new ApiException(ErrorCode.PgiaUsuarioNaoEncontrado);

        var unidade = await ResolverUnidadeAsync(dto.UnidadeId);

        // O PUT sempre grava a unidade (null limpa): a nova unidade não pode deixar
        // órfã uma designação vigente da pessoa
        await GarantirQueNaoOrfanizaDesignacaoAsync(userId, dto.UnidadeId);

        AplicarVinculo(user, dto.PapelPgia, unidade);
        await _repositorio.SaveChangesAsync();

        return await MapPessoaComOrgaoAsync(user);
    }

    public async Task<PgiaPessoaAcesso> CriarOuVincularPessoaAsync(PgiaPessoaCadastroDTO dto)
    {
        var email = (dto.Email ?? string.Empty).Trim();
        var nome = (dto.Nome ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(nome))
            throw new ApiException(ErrorCode.PgiaDominioInvalido,
                "E-mail e nome da pessoa são obrigatórios.");

        if (!EmailValido(email))
            throw new ApiException(ErrorCode.PgiaDominioInvalido, $"E-mail inválido: {email}");

        ValidarPapel(dto.PapelPgia);
        var unidade = await ResolverUnidadeAsync(dto.UnidadeId);

        // E-mail já conhecido não vira segunda pessoa: aplica o vínculo no cadastro
        // que já está lá — mas SÓ os campos informados (ver AplicarVinculoParcial)
        var user = await _repositorio.GetUserByEmailAsync(email);
        var jaExistia = user != null;

        if (user == null)
        {
            user = new User
            {
                Nome = nome,
                // Normalizado para minúsculas: o Keycloak também normaliza, então
                // gravar como digitado (caixa diferente) geraria um 2º cadastro no
                // 1º login. Corrige o dedup do lado do pré-cadastro, sem tocar no
                // GetOrCreateUserAsync (cujo fallback por e-mail assume esta linha).
                Email = email.ToLowerInvariant(),
                // Placeholder: o Perfil real vem da primeira role do token no primeiro
                // login, quando o GetOrCreateUserAsync (intocado) sobrescreve Nome e
                // Perfil. Com KeycloakId nulo, é o fallback por e-mail daquele método
                // que assume esta linha — e ele preserva Unidade e PapelPgia.
                Perfil = Perfis.Basico,
                KeycloakId = null
            };
            _repositorio.AddUser(user);

            // Cadastro novo: aplica o vínculo por inteiro (papel/unidade informados)
            AplicarVinculo(user, dto.PapelPgia, unidade);
        }
        else
        {
            // Pessoa já existente: NUNCA limpar por omissão. Papel/unidade nulos no
            // corpo significam "não mexer"; só o que veio preenchido é aplicado.
            if (dto.UnidadeId != null)
                await GarantirQueNaoOrfanizaDesignacaoAsync(user.Id, dto.UnidadeId);

            AplicarVinculoParcial(user, dto.PapelPgia, dto.UnidadeId, unidade);
        }

        await _repositorio.SaveChangesAsync();

        var pessoa = await MapPessoaComOrgaoAsync(user);
        pessoa.JaExistia = jaExistia;
        return pessoa;
    }

    private static void ValidarPapel(string? papelPgia)
    {
        if (!PapeisPgia.EhValido(papelPgia))
            throw new ApiException(ErrorCode.PgiaPapelInvalido, $"Papel PGIA inválido: {papelPgia}");
    }

    private async Task<Unidade?> ResolverUnidadeAsync(Guid? unidadeId)
    {
        if (unidadeId == null) return null;

        return await _repositorio.GetUnidadeByIdAsync(unidadeId.Value)
            ?? throw new ApiException(ErrorCode.PgiaUnidadeNaoEncontrada);
    }

    /// <summary>
    /// Trocar a unidade de quem é Responsável de IA ou Encarregado de Dados VIGENTE
    /// deixaria a designação órfã (apontando para fora do órgão). Recusa a troca; a
    /// designação precisa ser encerrada antes (art. 10). Sem troca de órgão (nova
    /// unidade == a do órgão da designação) nada é barrado.
    /// </summary>
    private async Task GarantirQueNaoOrfanizaDesignacaoAsync(Guid agenteId, Guid? novaUnidadeId)
    {
        var responsavel = await _repositorio.GetResponsavelVigenteDoAgenteAsync(agenteId);
        if (responsavel != null && responsavel.Orgao?.UnidadeId != novaUnidadeId)
            throw new ApiException(ErrorCode.PgiaDesignacaoVigenteImpedeTroca,
                $"Esta pessoa é Responsável de IA vigente da {responsavel.Orgao?.Sigla}; " +
                "encerre a designação antes de mudar o órgão dela.");

        var encarregado = await _repositorio.GetEncarregadoVigenteDoAgenteAsync(agenteId);
        if (encarregado != null && encarregado.Orgao?.UnidadeId != novaUnidadeId)
            throw new ApiException(ErrorCode.PgiaDesignacaoVigenteImpedeTroca,
                $"Esta pessoa é Encarregado de Dados vigente da {encarregado.Orgao?.Sigla}; " +
                "encerre a designação antes de mudar o órgão dela.");
    }

    /// <summary>
    /// Só o vínculo do PGIA muda; o Perfil do SGDP nunca é tocado aqui (ele vem das
    /// roles do Keycloak, reescrito a cada login pelo GetOrCreateUserAsync) — mesma
    /// regra do AtribuirPapelAsync do PgiaAdminService.
    /// </summary>
    private static void AplicarVinculo(User user, string? papelPgia, Unidade? unidade)
    {
        user.PapelPgia = papelPgia;
        user.Unidade = unidade; // null desvincula a pessoa do órgão
    }

    /// <summary>
    /// Aplicação parcial (pré-cadastro sobre pessoa existente): campo nulo = não
    /// mexer. Evita apagar o vínculo de alguém ao "pré-cadastrar" um e-mail já ativo.
    /// </summary>
    private static void AplicarVinculoParcial(User user, string? papelPgia, Guid? unidadeId, Unidade? unidade)
    {
        if (papelPgia != null) user.PapelPgia = papelPgia;
        if (unidadeId != null) user.Unidade = unidade;
    }

    /// <summary>Formato mínimo de e-mail: endereço único, sem apelido, com domínio pontuado.</summary>
    private static bool EmailValido(string email) =>
        MailAddress.TryCreate(email, out var endereco)
        && endereco.Address == email
        && endereco.Host.Contains('.');

    private async Task<PgiaPessoaAcesso> MapPessoaComOrgaoAsync(User user)
    {
        var orgaoPorUnidade = await MapaOrgaoPorUnidadeAsync();
        var info = await _repositorio.GetAgenteInfoAsync(user.Id);
        return MapPessoaAcesso(user, ResolverOrgao(orgaoPorUnidade, user), info);
    }

    private async Task<Dictionary<Guid, PgiaOrgao>> MapaOrgaoPorUnidadeAsync()
    {
        var orgaos = await _repositorio.ListarOrgaosAtivosComUnidadeAsync();
        // ux_pgia_orgao_unidade garante 1 unidade ↔ 1 órgão; o agrupamento só
        // protege o mapa em bases sem o índice relacional (testes InMemory)
        return orgaos
            .GroupBy(o => o.UnidadeId!.Value)
            .ToDictionary(g => g.Key, g => g.First());
    }

    private static PgiaOrgao? ResolverOrgao(Dictionary<Guid, PgiaOrgao> orgaoPorUnidade, User user)
    {
        if (user.Unidade == null) return null;
        return orgaoPorUnidade.TryGetValue(user.Unidade.id, out var orgao) ? orgao : null;
    }

    private static PgiaAgenteInfo? InfoDe(Dictionary<Guid, PgiaAgenteInfo> infos, Guid userId) =>
        infos.TryGetValue(userId, out var info) ? info : null;

    private static PgiaPessoaAcesso MapPessoaAcesso(User user, PgiaOrgao? orgao, PgiaAgenteInfo? info) => new()
    {
        UserId = user.Id,
        Nome = user.Nome,
        Email = user.Email,
        Perfil = user.Perfil,
        PapelPgia = user.PapelPgia,
        UnidadeId = user.Unidade?.id,
        UnidadeNome = user.Unidade?.Nome,
        OrgaoId = orgao?.Id,
        OrgaoSigla = orgao?.Sigla,
        Matricula = info?.Matricula,
        CargoFuncao = info?.CargoFuncao,
        Vinculo = info?.Vinculo
    };

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

    // ── Histórico de decisões do CGTIC (relatório de auditoria) ───────────────

    /// <summary>
    /// Monta o histórico do comitê: os casos que passam por ele (Alto Risco e
    /// Risco Excessivo, cuja homologação é delegada ao CGTIC), com as respectivas
    /// deliberações, e as demais deliberações do colegiado.
    /// </summary>
    public async Task<PgiaCgticHistoricoResponse> ObterHistoricoCgticAsync()
    {
        var casos = await _repositorio.ListarCasosCgticAsync();
        var idsCasos = casos.Select(s => s.Id).ToHashSet();

        // Entrada na pauta e deliberações em lote: nenhuma consulta por caso
        var datasEntrada = await _repositorio.ListarDatasClassificacaoVigenteAsync(idsCasos);
        var deliberacoes = await _repositorio.ListarDeliberacoesAsync();

        var porSistema = deliberacoes
            .Where(d => d.SistemaIaId != null && idsCasos.Contains(d.SistemaIaId.Value))
            .GroupBy(d => d.SistemaIaId!.Value)
            .ToDictionary(g => g.Key, g => g.Select(MapDeliberacao).ToList());

        var lista = casos.Select(sistema => new PgiaCgticCasoHistorico
        {
            SistemaIaId = sistema.Id,
            Denominacao = sistema.Denominacao,
            OrgaoSigla = sistema.Orgao?.Sigla ?? string.Empty,
            OrgaoNome = sistema.Orgao?.Nome ?? string.Empty,
            ClassificacaoRiscoAtual = sistema.ClassificacaoRiscoAtual,
            SituacaoHomologacao = sistema.SituacaoHomologacao,
            Situacao = SituacaoDoCaso(sistema.SituacaoHomologacao),
            DataEntrada = DataEntradaDoCaso(sistema, datasEntrada),
            Deliberacoes = porSistema.TryGetValue(sistema.Id, out var doCaso)
                ? doCaso
                : new List<PgiaDeliberacaoResponse>()
        })
        // Pendentes primeiro (é a pauta a decidir); depois do mais recente ao mais antigo
        .OrderByDescending(c => c.Situacao == PgiaCgticSituacaoCaso.Pendente)
        .ThenByDescending(c => c.DataEntrada)
        .ThenBy(c => c.SistemaIaId)
        .ToList();

        return new PgiaCgticHistoricoResponse
        {
            Contagens = new PgiaCgticContagens
            {
                Pendentes = lista.Count(c => c.Situacao == PgiaCgticSituacaoCaso.Pendente),
                // Analisada = já houve deliberação, mesmo que ainda em diligência
                Analisadas = lista.Count(c => c.Deliberacoes.Count > 0),
                Aprovadas = lista.Count(c => c.Situacao == PgiaCgticSituacaoCaso.Aprovada),
                Negadas = lista.Count(c => c.Situacao == PgiaCgticSituacaoCaso.Negada),
                Total = lista.Count
            },
            Casos = lista,
            // Tudo que o comitê deliberou fora dos casos: contratos, diretrizes,
            // guias e também sistemas que não correm pela rota do CGTIC
            OutrasDeliberacoes = deliberacoes
                .Where(d => d.SistemaIaId == null || !idsCasos.Contains(d.SistemaIaId.Value))
                .Select(MapDeliberacao)
                .ToList()
        };
    }

    public async Task<byte[]> GerarPdfHistoricoCgticAsync()
    {
        return PgiaCgticHistoricoPdf.Gerar(await ObterHistoricoCgticAsync());
    }

    private static string SituacaoDoCaso(string situacaoHomologacao) => situacaoHomologacao switch
    {
        PgiaDominios.SituacaoHomologacao.Aprovado => PgiaCgticSituacaoCaso.Aprovada,
        PgiaDominios.SituacaoHomologacao.Vetado => PgiaCgticSituacaoCaso.Negada,
        _ => PgiaCgticSituacaoCaso.Pendente
    };

    /// <summary>
    /// Entrada do caso na pauta: a data da classificação de risco vigente, que é o
    /// ato que enquadra o sistema em Alto Risco/Risco Excessivo e delega a
    /// homologação ao comitê. Sem histórico de classificação (registro anterior à
    /// fase 1), cai na data de cadastro do sistema no dia civil de Brasília.
    /// </summary>
    private static DateOnly? DataEntradaDoCaso(PgiaSistemaIa sistema, IReadOnlyDictionary<long, DateOnly> datas)
    {
        if (datas.TryGetValue(sistema.Id, out var dataClassificacao))
            return dataClassificacao;

        return sistema.CriadoEm == default
            ? null
            : DateOnly.FromDateTime(DateTimeHelper.ToBrasilia(sistema.CriadoEm));
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
