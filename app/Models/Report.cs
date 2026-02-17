using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Models;

/// <summary>
/// Report (relatório semanal) de um projeto
/// </summary>
public class Report
{
    [Key]
    public int ReportId {get;set;}

    public Projeto NM_PROJETO {get;set;}

    [Required]
    [StringLength(100)]
    public string descricao {get;set;} = "";

    [Required]
    [StringLength(100)]
    public string fase {get;set;}

    /// <summary>
    /// Atividades associadas ao report
    /// Nota: Atividade agora está vinculada à Etapa, não ao Projeto
    /// </summary>
    [Required]
    public ICollection<Atividade> Atividades {get;set;}

    [Required]
    public DateTime Data_criacao {get;set;}

    [Required]
    public DateTime Data_fim {get;set;}
}

// REMOVIDA classe Atividade daqui - agora está em Models/Atividade.cs
