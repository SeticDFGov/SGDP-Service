namespace Models.Planejamento;

/// <summary>Item do modelo com auditoria (criado e alterado em e por).</summary>
public interface IPeAuditavel
{
    DateTime CriadoEm { get; set; }

    string CriadoPor { get; set; }

    DateTime? AlteradoEm { get; set; }

    string? AlteradoPor { get; set; }
}

/// <summary>Item do modelo com ordem dentro do grupo (nível, etapa, passo, seção, campo, opção).</summary>
public interface IPeOrdenavel
{
    long Id { get; }

    int Ordem { get; set; }
}

/// <summary>Linha de situação num nível (pe_passo_nivel, pe_secao_nivel, pe_campo_nivel).</summary>
public interface IPeSituacaoNivel
{
    long NivelId { get; }

    string Situacao { get; set; }
}
