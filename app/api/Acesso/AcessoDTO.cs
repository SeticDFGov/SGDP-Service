using System.ComponentModel.DataAnnotations;

namespace api.Acesso;

/// <summary>Um acesso a módulo, concedido no sistema ou visto no Keycloak.</summary>
public class AcessoModuloResponse
{
    public string Modulo { get; set; } = string.Empty;

    public DateTime ConcedidoEm { get; set; }

    public string ConcedidoPor { get; set; } = string.Empty;
}

/// <summary>
/// Usuário visto pela gestão de acessos: de onde vem cada acesso e o resultado
/// efetivo. "Keycloak" é o retrato das roles no último login (só leitura: é
/// gerido no Keycloak); "Concedidos" é o que a tela liga e desliga.
/// </summary>
public class UsuarioAcessoResponse
{
    public Guid Id { get; set; }

    public string Nome { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public Guid? UnidadeId { get; set; }

    public string? UnidadeNome { get; set; }

    // Papel dentro do PGIA (PapeisPgia) — também dá acesso ao módulo
    public string? PapelPgia { get; set; }

    // Papel da Supervisão Contínua das Contratações (PapeisContratacoes) — é o acesso ao módulo
    public string? PapelContratacoes { get; set; }

    // Papel na Governança Estratégica (PapeisPlanejamento), de quem tem a concessão do
    // módulo; nulo para os demais. Campo novo: o front antigo o ignora
    public string? PapelPlanejamento { get; set; }

    public List<AcessoModuloResponse> Concedidos { get; set; } = new();

    public List<AcessoModuloResponse> Keycloak { get; set; } = new();

    // Módulos efetivos (ModulosSgdp), somando Keycloak, concessões e papéis
    public List<string> Modulos { get; set; } = new();
}

/// <summary>
/// Acessos de um usuário, gravados de uma vez pela tela de gestão. O que vem do
/// Keycloak não passa por aqui (é gerido lá) e o Perfil do SGDP nunca muda.
/// </summary>
public class AcessoUsuarioUpdateDTO
{
    // Painel e demandas (consulta; editar exige o perfil gestor do Keycloak)
    public bool Demandas { get; set; }

    // Entrada no PGIA. Sem papel, a pessoa atua como agente público (art. 13):
    // registra uso de IA e avisa incidentes
    public bool Pgia { get; set; }

    // PapeisPgia.Todos ou null; exige Pgia = true
    [StringLength(30)]
    public string? PapelPgia { get; set; }

    // Supervisão Contínua das Contratações (grava o papel ctr_analise)
    public bool Contratacoes { get; set; }

    // Governança Estratégica, opcional. Ausente ou nulo não muda nada (o front antigo,
    // que não conhece o módulo, nunca tira o acesso de ninguém). true exige um papel
    // válido (o que vier em PapelPlanejamento ou o que a pessoa já tem); false tira o
    // acesso e o papel
    public bool? Planejamento { get; set; }

    // PapeisPlanejamento.Todos ou nulo (nulo = manter o papel atual); exige Planejamento = true
    [StringLength(30)]
    public string? PapelPlanejamento { get; set; }
}

/// <summary>
/// Filtros da listagem da gestão de acessos. O parâmetro da action se chama
/// "consulta" — nunca o nome de uma propriedade (armadilha do model binding
/// registrada no módulo de contratações).
/// </summary>
public class AcessoUsuariosConsulta
{
    // Parte do nome ou do e-mail
    public string? Filtro { get; set; }

    // ModulosSgdp.Todos ou "nenhum" (sem acesso a módulo algum)
    public string? Modulo { get; set; }

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 20;
}

/// <summary>Liga/desliga uma concessão de módulo (usado pela SGDI para o PGIA).</summary>
public class AcessoModuloToggleDTO
{
    public bool Ativo { get; set; }
}
