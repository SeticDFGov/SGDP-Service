using api.Planejamento;
using Microsoft.EntityFrameworkCore;
using Models;
using Models.Planejamento;

namespace service.Planejamento;

/// <summary>
/// A validação da equipe num passo da elaboração (F3): a equipe do PDTIC marca o passo quando
/// revisou o conteúdo e o considera pronto. Não é o "feito" (que o sistema calcula) nem a aprovação
/// formal (a do SGTIC e a do CGTIC ficam nos passos próprios). A marca guarda quem e quando; se os
/// dados do passo mudarem depois, a marca fica, com a data da primeira mudança
/// (<see cref="MarcarMudancaAsync"/>).
/// <list type="bullet">
/// <item>Aceitam a validação os passos das etapas 1 a 3 do grupo da elaboração dos tipos dados,
/// conferência dos temas, documento e fluxo; no PDTIC registrado fora do sistema, os passos
/// externos não aceitam (<see cref="Aceita(PePdtic, string?, PeTrilhaPasso)"/>).</item>
/// <item>A mudança depois da validação é um UPDATE condicional (só onde há validação e ainda sem
/// mudança registrada), sem INSERT: a linha já existe, porque a marca a criou. Assim duas pessoas
/// gravando no mesmo passo não entram em conflito.</item>
/// <item>Tudo só com a versão 8 do modelo inicial carregada (as colunas são da migration
/// PeModoLivreValidacao).</item>
/// </list>
/// </summary>
public static class PeValidacaoDaEquipe
{
    public const string SoPassoFeito = "Só dá para validar um passo feito. Complete o que falta no passo primeiro.";
    public const string TipoNaoAceita = "Este passo não recebe a validação da equipe.";

    /// <summary>O passo recebe a validação da equipe (sem olhar quem chama).</summary>
    public static bool Aceita(PePdtic pdtic, string? etapaChave, PeTrilhaPasso passo)
    {
        var grupo = PeEdicaoPdtic.GrupoDe(etapaChave, passo.Tipo);
        return grupo == PeEdicaoPdtic.Grupo.Elaboracao
               && passo.Tipo is PeDominios.TipoPasso.Dados or PeDominios.TipoPasso.ConferenciaTemas
                   or PeDominios.TipoPasso.Documento or PeDominios.TipoPasso.Fluxo
               && !PeEdicaoPdtic.Externo(pdtic, grupo, passo.Chave);
    }

    /// <summary>O passo da trilha do órgão recebe a validação da equipe.</summary>
    public static bool Aceita(PePdtic pdtic, PeTrilhaOrgao trilha, PeTrilhaPasso passo) =>
        Aceita(pdtic, trilha.Etapas.FirstOrDefault(e => e.Passos.Any(p => p.Id == passo.Id))?.Chave, passo);

    public static PeValidacaoResponse Resposta(PePdticPassoValidacao validacao, PeNomes nomes) => new()
    {
        ValidadoEm = validacao.ValidadoEm,
        ValidadoPor = validacao.ValidadoPor,
        ValidadoPorNome = nomes.DeObrigatorio(validacao.ValidadoPor),
        AlteradoDepoisEm = validacao.AlteradaEm
    };

    /// <summary>409 PeValidacaoRecusada com a mensagem.</summary>
    public static ApiException Recusada(string mensagem) => new(ErrorCode.PeValidacaoRecusada, mensagem);

    /// <summary>
    /// Registra a mudança nos dados dos passos dados depois da validação: só nos passos que o órgão
    /// vê e que recebem a validação, e só com a versão 8 carregada. Quem chama já gravou a mudança.
    /// </summary>
    public static Task MarcarMudancaAsync(AppDbContext context, PePdtic pdtic, PeTrilhaOrgao trilha, IEnumerable<long> passoIds, DateTime agora)
    {
        if (!trilha.Dados.ModoNiveis.Ativo) return Task.CompletedTask;
        var aceitam = passoIds.Distinct().Where(id => trilha.Passo(id) is { } passo && Aceita(pdtic, trilha, passo)).ToList();
        return aceitam.Count == 0 ? Task.CompletedTask : MarcarAsync(context, pdtic.Id, aceitam, agora);
    }

    /// <summary>
    /// O UPDATE condicional: a data da primeira mudança, onde há validação e ainda não há mudança
    /// registrada. No PostgreSQL, um comando só (sem ler a linha); no InMemory dos testes, pelas entidades.
    /// </summary>
    internal static async Task MarcarAsync(AppDbContext context, long pdticId, IReadOnlyCollection<long> passoIds, DateTime agora)
    {
        var ids = passoIds.ToArray();
        if (context.Database.IsRelational())
        {
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE pe_pdtic_passo SET validacao_alterada_em = {agora} WHERE pdtic_id = {pdticId} AND passo_id = ANY({ids}) AND validado_em IS NOT NULL AND validacao_alterada_em IS NULL");
            return;
        }
        var linhas = await context.PePdticPassosValidacao
            .Where(v => v.PdticId == pdticId && ids.Contains(v.PassoId) && v.AlteradaEm == null)
            .ToListAsync();
        foreach (var linha in linhas) linha.AlteradaEm = agora;
        if (linhas.Count > 0) await context.SaveChangesAsync();
    }

    /// <summary>Os passos do tipo documento (o texto e os capítulos do documento do PDTIC são o conteúdo deles).</summary>
    public static IEnumerable<long> PassosDoDocumento(PeTrilhaOrgao trilha) =>
        trilha.Passos.Where(p => p.Tipo == PeDominios.TipoPasso.Documento).Select(p => p.Id);

    /// <summary>
    /// Gravar ou restaurar a cópia de um fluxo muda o conteúdo dos passos com fluxos (os do tipo
    /// fluxo e a metodologia de elaboração, 1.5, que mostra os fluxos do órgão). Sem ler o modelo
    /// inteiro: a marca da versão 8, os passos pelo tipo e pela chave e o UPDATE condicional (só a
    /// linha com validação muda; os fluxos só se gravam com a elaboração aberta, nunca no PDTIC
    /// registrado fora do sistema).
    /// </summary>
    public static async Task MarcarMudancaNosFluxosAsync(AppDbContext context, PePdtic pdtic, DateTime agora)
    {
        if (!(await PeModoNiveis.LerAsync(context)).Ativo) return;
        var passos = await context.PePassos.AsNoTracking()
            .Where(p => p.ExcluidoEm == null && (p.Tipo == PeDominios.TipoPasso.Fluxo || p.Chave == PeDominios.ChavePdtic.PassoMetodologia))
            .Select(p => p.Id)
            .ToListAsync();
        if (passos.Count > 0) await MarcarAsync(context, pdtic.Id, passos, agora);
    }
}

/// <summary>
/// O conteúdo de cada passo num PDTIC (F3, o passo opcional que não cobra): tem conteúdo o passo
/// com algum registro numa seção dele (em qualquer ciclo, nas seções por ciclo; também nas seções
/// que a forma de hoje esconde); o passo do tipo documento, com alguma versão do PDF do PDTIC gerada.
/// </summary>
public static class PeConteudoDosPassos
{
    /// <summary>Os passos (id) com conteúdo, pelas seções com registro e pelo PDF gerado.</summary>
    public static HashSet<long> ComConteudo(PeModeloDados dados, IEnumerable<long> secoesComRegistro, bool temDocumento)
    {
        var secoes = secoesComRegistro.ToHashSet();
        var passos = dados.Secoes.Where(s => s.PassoId != null && secoes.Contains(s.Id)).Select(s => s.PassoId!.Value).ToHashSet();
        if (temDocumento)
            foreach (var passo in dados.Passos.Where(p => p.Tipo == PeDominios.TipoPasso.Documento))
                passos.Add(passo.Id);
        return passos;
    }

    /// <summary>Os passos com conteúdo num PDTIC, lidos do banco (duas consultas).</summary>
    public static async Task<HashSet<long>> ComConteudoAsync(AppDbContext context, PeModeloDados dados, long pdticId)
    {
        var secoes = await context.PeRegistros.AsNoTracking()
            .Where(r => r.PdticId == pdticId)
            .Select(r => r.SecaoId)
            .Distinct()
            .ToListAsync();
        var temDocumento = await PeDocumentoService.TemVersaoDoPdticAsync(context, pdticId, dados.Acompanhamento.Ativo);
        return ComConteudo(dados, secoes, temDocumento);
    }

    /// <summary>O passo é opcional para o órgão e está sem conteúdo: não é cobrado nem sai no documento e na planilha completa.</summary>
    public static bool OpcionalSemConteudo(PeTrilhaPasso passo, IReadOnlySet<long> comConteudo) =>
        passo.Situacao == PeDominios.Situacao.Opcional && !comConteudo.Contains(passo.Id);
}
