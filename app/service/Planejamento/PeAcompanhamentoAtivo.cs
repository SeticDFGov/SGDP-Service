using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Models;
using Models.Planejamento;

namespace service.Planejamento;

/// <summary>
/// As configurações do acompanhamento (E7, rodada B), lidas de pe_configuracao numa consulta
/// só: se o carregador já trouxe a versão 6 do modelo inicial (as seções por ciclo e os modelos
/// do RA e do RR), os dias de prazo para fechar um ciclo, quantos dias antes do fim da vigência a
/// etapa 7 fica disponível e a periodicidade padrão do monitoramento.
/// <para>
/// Regra do deploy: a versão 6 só carrega com a migration PeAcompanhamento aplicada (o carregador
/// lê e grava pe_secao.por_ciclo). Por isso as consultas que dependem das colunas novas (os
/// ciclos, o ciclo de cada registro, o documento de cada linha da cópia do órgão e das versões)
/// só rodam com <see cref="Ativo"/>: no intervalo entre o PR e a migration, a leitura de sempre
/// continua funcionando e só o que é novo responde 409 com corpo.
/// </para>
/// </summary>
public sealed record PeAcompanhamentoAtivo(bool Ativo, int PrazoFechamentoDias, int DiasAvaliacaoFinal, string PeriodicidadePadrao)
{
    public static async Task<PeAcompanhamentoAtivo> LerAsync(AppDbContext context)
    {
        var configuracoes = await context.PeConfiguracoes.AsNoTracking().ToListAsync();
        string? Valor(string chave) => configuracoes.FirstOrDefault(c => c.Chave == chave)?.Valor;

        var versao = Inteiro(Valor(PeConfiguracao.ChaveVersaoModelo)) ?? 0;
        var prazo = Inteiro(Valor(PeConfiguracao.ChavePrazoFechamentoCiclo));
        var dias = Inteiro(Valor(PeConfiguracao.ChaveDiasAvaliacaoFinal));
        var periodicidade = Texto(Valor(PeConfiguracao.ChavePeriodicidadeMonitoramento));
        return new PeAcompanhamentoAtivo(
            versao >= PeCarregadorModelo.VersaoDoAcompanhamento,
            prazo is >= 0 and <= 365 ? prazo.Value : PeConfiguracao.PrazoFechamentoCicloPadrao,
            dias is >= 0 and <= 3650 ? dias.Value : PeConfiguracao.DiasAvaliacaoFinalPadrao,
            PeDominios.Periodicidade.Meses(periodicidade) != null ? periodicidade! : PeDominios.Periodicidade.Padrao);
    }

    /// <summary>Só a marca (a versão 6 carregada), sem as outras configurações.</summary>
    public static async Task<bool> AtivoAsync(AppDbContext context) => (await LerAsync(context)).Ativo;

    /// <summary>409 PeModeloIndisponivel enquanto a versão 6 não foi carregada (o intervalo da atualização).</summary>
    public PeAcompanhamentoAtivo Exigir() => Ativo
        ? this
        : throw new ApiException(ErrorCode.PeModeloIndisponivel,
            "O acompanhamento do PDTIC está sendo preparado nesta atualização. Tente de novo em alguns minutos.");

    private static int? Inteiro(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            using var documento = JsonDocument.Parse(json);
            var raiz = documento.RootElement;
            if (raiz.ValueKind == JsonValueKind.Number && raiz.TryGetInt32(out var n)) return n;
            if (raiz.ValueKind == JsonValueKind.String
                && int.TryParse(raiz.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var s)) return s;
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? Texto(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            using var documento = JsonDocument.Parse(json);
            return documento.RootElement.ValueKind == JsonValueKind.String ? documento.RootElement.GetString()?.Trim() : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
