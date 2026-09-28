using Microsoft.EntityFrameworkCore;
using Models;
using Models.Planejamento;

namespace service.Planejamento;

/// <summary>
/// O modo dos níveis (F3) e a marca da versão 8 do modelo inicial, lidos de pe_configuracao.
/// <list type="bullet">
/// <item><see cref="Ativo"/>: o carregador já trouxe a versão 8, que só carrega com a migration
/// PeModoLivreValidacao aplicada (ele lê a tabela pe_orgao_passo_detalhe e as colunas novas de
/// pe_pdtic_passo). Só com a marca são lidas a forma de cada passo, a validação da equipe e o modo
/// livre. Sem ela (o intervalo do deploy: o PR publica o código antes da migration, que só roda no
/// merge), vale o modo definido, sem validação, e os endpoints novos (validação, forma do passo e
/// modo dos níveis) respondem 409 PeModeloIndisponivel com corpo.</item>
/// <item><see cref="Modo"/>: "livre" ou "definido" (a configuração modo_niveis; sem ela, ou com
/// valor fora do domínio, o definido).</item>
/// </list>
/// Nada disso está no caminho de cada requisição: só as telas do módulo leem.
/// </summary>
public sealed record PeModoNiveis(bool Ativo, string Modo)
{
    /// <summary>Sem a versão 8: o modo definido, sem validação (como até a F2).</summary>
    public static readonly PeModoNiveis Anterior = new(false, PeDominios.ModoNiveis.Definido);

    /// <summary>O modo livre vale (a versão 8 carregada e a configuração "livre").</summary>
    public bool Livre => Ativo && Modo == PeDominios.ModoNiveis.Livre;

    /// <summary>O modo que vale agora: "livre" ou "definido" (sem a versão 8, sempre "definido").</summary>
    public string Vigente => Livre ? PeDominios.ModoNiveis.Livre : PeDominios.ModoNiveis.Definido;

    public static async Task<PeModoNiveis> LerAsync(AppDbContext context) =>
        De(await context.PeConfiguracoes.AsNoTracking().ToListAsync());

    /// <summary>A marca e o modo a partir das linhas de pe_configuracao já lidas.</summary>
    public static PeModoNiveis De(IReadOnlyList<PeConfiguracao> configuracoes)
    {
        string? Valor(string chave) => configuracoes.FirstOrDefault(c => c.Chave == chave)?.Valor;
        var versao = PeAcompanhamentoAtivo.Inteiro(Valor(PeConfiguracao.ChaveVersaoModelo)) ?? 0;
        if (versao < PeCarregadorModelo.VersaoDoModoLivre) return Anterior;
        var modo = PeAcompanhamentoAtivo.Texto(Valor(PeConfiguracao.ChaveModoNiveis));
        return new PeModoNiveis(true, modo != null && PeDominios.ModoNiveis.Todos.Contains(modo) ? modo : PeDominios.ModoNiveis.Definido);
    }

    /// <summary>409 PeModeloIndisponivel enquanto a versão 8 não foi carregada (o intervalo da atualização).</summary>
    public PeModoNiveis Exigir() => Ativo
        ? this
        : throw new ApiException(ErrorCode.PeModeloIndisponivel,
            "Esta parte da Governança Estratégica está sendo preparada nesta atualização. Tente de novo em alguns minutos.");
}
