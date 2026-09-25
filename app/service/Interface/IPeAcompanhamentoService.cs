using api.Planejamento;
using service.Planejamento;

namespace service.Interface;

/// <summary>
/// O acompanhamento do PDTIC (E7, rodada B): os ciclos de monitoramento (criados sozinhos pela
/// periodicidade quando a lista é lida) e de avaliação intermediária (abertos pela equipe), o
/// fechamento (com as pendências e o RA do ciclo em minuta) e a reabertura, as grades da
/// situação das ações e das medições (gravadas pelo motor de registros) e o painel do PDTIC
/// (AC-PDTIC). Ler: quem vê o órgão. Gravar, abrir, fechar e reabrir: a equipe do órgão (e o
/// admin geral), com o PDTIC vigente. Erros como ApiException (1120 a 1139 e os anteriores).
/// </summary>
public interface IPeAcompanhamentoService
{
    /// <summary>Os ciclos do PDTIC (tipo monitoramento ou avaliacao; sem tipo, os dois; fora do domínio, lista vazia).</summary>
    Task<List<PeCicloResponse>> ListarCiclosAsync(long pdticId, string? tipo, PeUserContext ctx);

    /// <summary>Abre uma avaliação intermediária (uma aberta por vez); devolve o ciclo.</summary>
    Task<PeCicloResponse> CriarCicloAsync(long pdticId, PeCicloCriarDTO dto, PeUserContext ctx);

    /// <summary>Fecha o ciclo (400 com as pendências) e gera o RA do ciclo em minuta; devolve o ciclo.</summary>
    Task<PeCicloResponse> FecharCicloAsync(long cicloId, PeUserContext ctx);

    /// <summary>Reabre o ciclo fechado (guarda quem e quando); devolve o ciclo.</summary>
    Task<PeCicloResponse> ReabrirCicloAsync(long cicloId, PeUserContext ctx);

    /// <summary>A grade da situação das ações no ciclo de monitoramento.</summary>
    Task<List<PeCicloAcaoResponse>> AcoesAsync(long cicloId, PeUserContext ctx);

    /// <summary>Grava a grade das ações pelo motor (seção monitoramento_acoes); devolve a grade.</summary>
    Task<List<PeCicloAcaoResponse>> SalvarAcoesAsync(long cicloId, PeCicloAcoesDTO dto, PeUserContext ctx);

    /// <summary>A grade das medições dos indicadores no ciclo de monitoramento.</summary>
    Task<List<PeCicloMedicaoResponse>> MedicoesAsync(long cicloId, PeUserContext ctx);

    /// <summary>Grava a grade das medições pelo motor (seção medicoes_indicadores); devolve a grade.</summary>
    Task<List<PeCicloMedicaoResponse>> SalvarMedicoesAsync(long cicloId, PeCicloMedicoesDTO dto, PeUserContext ctx);

    /// <summary>O painel do PDTIC (AC-PDTIC) no ciclo dado ou no último ciclo de monitoramento com dado.</summary>
    Task<PePainelResponse> PainelAsync(long pdticId, long? cicloId, PeUserContext ctx);
}
