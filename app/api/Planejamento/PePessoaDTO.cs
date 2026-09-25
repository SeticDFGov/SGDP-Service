using System.ComponentModel.DataAnnotations;

namespace api.Planejamento;

/// <summary>
/// Papel de quem está logado no módulo Governança Estratégica
/// (GET api/planejamento/meu-papel). Papel nulo com EhAdminGeral falso não deveria
/// acontecer (acesso sem papel): o front mostra "fale com o administrador do módulo".
/// </summary>
public class PeMeuPapelResponse
{
    // app.Auth.PapeisPlanejamento.Todos ou nulo
    public string? Papel { get; set; }

    // Perfil admin do SGDP: tudo no módulo, com ou sem papel
    public bool EhAdminGeral { get; set; }

    // Órgão (pgia_orgao ativo) da unidade da pessoa; nulo sem unidade ou sem órgão ativo
    public long? OrgaoId { get; set; }

    public string? OrgaoNome { get; set; }

    public string? OrgaoSigla { get; set; }

    public string? UnidadeNome { get; set; }
}

/// <summary>Pessoa com papel no módulo (tela "Pessoas e acessos").</summary>
public class PePessoaResponse
{
    public Guid UserId { get; set; }

    public string Nome { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string? UnidadeNome { get; set; }

    // Órgão da unidade da pessoa; nulo quando ela não tem unidade com órgão ativo (o
    // front avisa nos papéis de órgão)
    public long? OrgaoId { get; set; }

    public string? OrgaoSigla { get; set; }

    public string? OrgaoNome { get; set; }

    // Sempre preenchido na lista. Nulo só na resposta do PUT que tirou o papel (junto
    // com ConcedidoEm e ConcedidoPor)
    public string? Papel { get; set; }

    public DateTime? ConcedidoEm { get; set; }

    public string? ConcedidoPor { get; set; }
    // F1 (C19): o nome da pessoa (o do cadastro do usuário; sem nome, o e-mail); nulo com o ConcedidoPor
    public string? ConcedidoPorNome { get; set; }

    // Retrato do último login: perfil admin do SGDP (tem tudo, com ou sem papel)
    public bool EhAdminGeral { get; set; }
}

/// <summary>
/// Pessoa que pode receber papel: usuário do SGDP que já entrou pelo menos uma vez e
/// ainda não tem papel no módulo.
/// </summary>
public class PeCandidataResponse
{
    public Guid UserId { get; set; }

    public string Nome { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string? UnidadeNome { get; set; }

    public string? OrgaoSigla { get; set; }
}

/// <summary>
/// Filtros da lista de pessoas do módulo. O parâmetro da action se chama "consulta",
/// nunca o nome de uma propriedade (armadilha do model binding registrada no módulo de
/// contratações).
/// </summary>
public class PePessoasConsulta
{
    // Parte do nome ou do e-mail
    public string? Filtro { get; set; }

    // Um código de app.Auth.PapeisPlanejamento; fora do domínio devolve lista vazia
    public string? Papel { get; set; }

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 20;
}

/// <summary>Papel a gravar: um dos cinco códigos, ou nulo para tirar o papel e o acesso.</summary>
public class PePapelDTO
{
    [StringLength(30)]
    public string? Papel { get; set; }
}
