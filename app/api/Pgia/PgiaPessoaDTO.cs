namespace api.Pgia;

/// <summary>
/// Pessoa do órgão: usuário do SGDP (mesma Unidade do órgão) com os dados
/// de agente público do PGIA (pgia_agente_info), quando preenchidos.
/// </summary>
public class PgiaPessoaResponse
{
    public Guid UserId { get; set; }

    public string Nome { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string? PapelPgia { get; set; }

    public string? Matricula { get; set; }

    public string? CargoFuncao { get; set; }

    public string? Vinculo { get; set; }
}

/// <summary>
/// Pessoa vista pela gestão de acessos da SGDI: usuário do SGDP com o vínculo
/// que o PGIA administra (papel PGIA e unidade) e o órgão resolvido pela unidade.
/// Aparece também sem unidade e sem papel — é assim que a SGDI encontra quem
/// acabou de chegar para vincular a um órgão.
/// </summary>
public class PgiaPessoaAcesso
{
    public Guid UserId { get; set; }

    public string Nome { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string? PapelPgia { get; set; }

    public Guid? UnidadeId { get; set; }

    public string? UnidadeNome { get; set; }

    // Órgão PGIA ativo vinculado à unidade da pessoa (null se não houver)
    public long? OrgaoId { get; set; }

    public string? OrgaoSigla { get; set; }

    // Dados de agente público (pgia_agente_info), quando já preenchidos: a tela
    // "Completar dados" da SGDI pré-preenche a partir daqui e não os zera ao salvar.
    public string? Matricula { get; set; }

    public string? CargoFuncao { get; set; }

    public string? Vinculo { get; set; }

    // Só faz sentido na resposta do POST pessoa (pré-cadastro): true = o e-mail já
    // estava na base e só os campos informados foram aplicados (os demais preservados).
    // No GET pessoas e no PUT vínculo é sempre false. Mantido no mesmo tipo para não
    // quebrar o front, que já tipa a resposta do POST como PgiaPessoaAcesso.
    public bool JaExistia { get; set; }
}

/// <summary>
/// Vínculo de acesso de uma pessoa no PGIA: papel PGIA e unidade do SGDP.
/// Ambos nulos limpam o vínculo (papel nulo = sem função no PGIA; unidade nula
/// desvincula do órgão). O Perfil do SGDP não faz parte deste contrato.
/// </summary>
public class PgiaPessoaVinculoDTO
{
    // PapeisPgia.Todos ou null; espelha o varchar(30) de Users.PapelPgia
    [System.ComponentModel.DataAnnotations.StringLength(30)]
    public string? PapelPgia { get; set; }

    public Guid? UnidadeId { get; set; }
}

/// <summary>
/// Pré-cadastro de pessoa por e-mail: permite à SGDI vincular alguém ao órgão
/// antes do primeiro login no Keycloak. E-mail já conhecido não vira segunda
/// pessoa — o vínculo é aplicado no cadastro existente.
/// </summary>
public class PgiaPessoaCadastroDTO : PgiaPessoaVinculoDTO
{
    // Precisa ser exatamente o e-mail que o token do Keycloak trará no primeiro
    // login; é por ele que o cadastro prévio é reconhecido.
    public string Email { get; set; } = string.Empty;

    public string Nome { get; set; } = string.Empty;
}

/// <summary>
/// Dados de agente público (art. 3º) complementares ao cadastro do usuário.
/// </summary>
public class PgiaAgenteInfoDTO
{
    [System.ComponentModel.DataAnnotations.StringLength(20)]
    public string? Matricula { get; set; }

    public string? CargoFuncao { get; set; }

    // D11 (PgiaDominios.Vinculo)
    [System.ComponentModel.DataAnnotations.StringLength(30)]
    public string Vinculo { get; set; } = string.Empty;
}
