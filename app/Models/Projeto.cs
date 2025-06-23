using System.ComponentModel.DataAnnotations;
using System;
using app.Models;

namespace Models;

public class Projeto
{
    [Key]
    public int projetoId {get;set;}
    
    [StringLength(200)]
    public string NM_PROJETO {get;set;}

    [StringLength(300)]
    public string? GERENTE_PROJETO {get;set;}
    
    [StringLength(300)]
    public string? SITUACAO {get;set;}


    [StringLength(300)]
    public string? NR_PROCESSO_SEI {get;set;}

    [StringLength(300)]
    public string? NM_AREA_DEMANDANTE {get;set;}

    [StringLength(100)]
    public string? ANO {get;set;}

    [StringLength(200)]
    public string? TEMPLATE {get;set;}

    public bool? PROFISCOII {get;set;}

    public bool? PDTIC2427 {get;set;}

    public bool? PTD2427 {get;set;}
    public decimal? valorEstimado { get; set; }

    public Unidade? Unidade { get; set; }
    public Esteira? Esteira { get; set; }
}