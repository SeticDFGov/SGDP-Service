using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace service.Planejamento;

/// <summary>
/// Roda o <see cref="PeCarregadorModelo"/> ao subir a API, fora do caminho das
/// requisições, sem nunca derrubar o boot. Regra do deploy: o PR publica o código antes
/// de a migration rodar (só no merge), e no merge a imagem nova pode subir antes de a
/// migration terminar. Por isso, sem as tabelas (ou com qualquer outra falha), o serviço
/// registra no log e tenta de novo mais tarde (30 s, 1 min, 2 min, 5 min e depois a cada
/// 10 min), até carregar uma vez. Com a versão já carregada, a tentativa é uma consulta só.
/// </summary>
public sealed class PeCarregadorModeloHostedService : BackgroundService
{
    public static readonly TimeSpan[] Esperas =
    {
        TimeSpan.FromSeconds(30), TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(2),
        TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(10)
    };

    private readonly IServiceScopeFactory _escopos;
    private readonly ILogger<PeCarregadorModeloHostedService> _logger;

    public PeCarregadorModeloHostedService(IServiceScopeFactory escopos, ILogger<PeCarregadorModeloHostedService> logger)
    {
        _escopos = escopos;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Sai do caminho do boot antes de qualquer trabalho
        await Task.Yield();

        for (var tentativa = 0; !stoppingToken.IsCancellationRequested; tentativa++)
        {
            if (await TentarAsync(stoppingToken)) return;
            try
            {
                await Task.Delay(Esperas[Math.Min(tentativa, Esperas.Length - 1)], stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    /// <summary>Uma tentativa. Verdadeiro quando o modelo está carregado (ou a API está parando).</summary>
    public async Task<bool> TentarAsync(CancellationToken ct)
    {
        try
        {
            using var escopo = _escopos.CreateScope();
            var carregador = escopo.ServiceProvider.GetRequiredService<PeCarregadorModelo>();
            var resultado = await carregador.CarregarAsync(ct);
            if (resultado.Executou)
                _logger.LogInformation(
                    "Governança Estratégica: modelo inicial versão {Versao} carregado (antes: {Anterior}). Novos: {Niveis} níveis, "
                    + "{Etapas} etapas, {Passos} passos, {Secoes} seções, {Campos} campos, {Opcoes} opções, {Configuracoes} configurações, "
                    + "{Registros} registros do sistema, {Documentos} modelos de documento, {Capitulos} capítulos, {Blocos} blocos, "
                    + "{Fluxos} fluxos do guia e {SecoesPorCiclo} seções marcadas por ciclo.",
                    resultado.Versao, resultado.VersaoAnterior, resultado.Niveis, resultado.Etapas, resultado.Passos,
                    resultado.Secoes, resultado.Campos, resultado.Opcoes, resultado.Configuracoes, resultado.Registros,
                    resultado.Documentos, resultado.Capitulos, resultado.Blocos, resultado.Fluxos, resultado.SecoesPorCiclo);
            return true;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            return true;
        }
        catch (Exception ex) when (PeBanco.TabelaAusente(ex))
        {
            // Nada foi gravado (uma transação só): a versão anterior do modelo continua valendo
            _logger.LogWarning(
                "Governança Estratégica: as tabelas do módulo ainda não existem (migration pendente). "
                + "O carregador tenta de novo mais tarde; o resto da API segue normal.");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Governança Estratégica: o modelo inicial não foi carregado. O carregador tenta de novo mais tarde.");
            return false;
        }
    }
}
