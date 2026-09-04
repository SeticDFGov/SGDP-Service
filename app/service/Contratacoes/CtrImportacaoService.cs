using api.Contratacoes;
using Models.Contratacoes;
using Repositorio.Interface;
using service.Interface;

namespace service.Contratacoes;

/// <summary>
/// Importação da planilha legada: upsert idempotente por número de processo entre
/// os ativos. Cada linha passa pelo mesmo <c>CtrProcessoService.ValidarProcesso</c>
/// do CRUD — o que não passa vira linha rejeitada no relatório, com o motivo.
/// </summary>
public class CtrImportacaoService : ICtrImportacaoService
{
    private readonly ICtrProcessoRepositorio _repositorio;

    public CtrImportacaoService(ICtrProcessoRepositorio repositorio)
    {
        _repositorio = repositorio;
    }

    public async Task<CtrImportacaoPrevia> PreviaAsync(byte[] conteudo)
    {
        var linhas = await ProcessarAsync(conteudo, ctx: null);
        return new CtrImportacaoPrevia { TotalLinhas = linhas.Count, Linhas = linhas };
    }

    public async Task<CtrImportacaoRelatorio> ImportarAsync(byte[] conteudo, CtrUserContext ctx)
    {
        var linhas = await ProcessarAsync(conteudo, ctx);

        // Uma escrita só para o arquivo inteiro
        await _repositorio.SaveChangesAsync();

        return new CtrImportacaoRelatorio
        {
            Criados = linhas.Count(l => l.Acao == CtrImportacaoAcao.Criar),
            Atualizados = linhas.Count(l => l.Acao == CtrImportacaoAcao.Atualizar),
            Rejeitados = linhas.Count(l => l.Acao == CtrImportacaoAcao.Rejeitar),
            Linhas = linhas
        };
    }

    /// <summary>
    /// Interpreta o arquivo. Com ctx nulo é prévia (nada é tocado no banco); com ctx
    /// aplica as linhas válidas às entidades — o SaveChanges fica com quem chamou.
    /// </summary>
    private async Task<List<CtrImportacaoLinha>> ProcessarAsync(byte[]? conteudo, CtrUserContext? ctx)
    {
        if (conteudo == null || conteudo.Length == 0)
            throw new ApiException(ErrorCode.CtrImportacaoInvalida, "Envie o arquivo CSV da planilha.");

        List<CtrCsvLinha> lidas;
        try
        {
            lidas = CtrCsv.Ler(conteudo);
        }
        catch (Exception ex)
        {
            throw new ApiException(ErrorCode.CtrImportacaoInvalida,
                $"Não foi possível ler o arquivo: {ex.Message}");
        }

        if (lidas.Count == 0)
            throw new ApiException(ErrorCode.CtrImportacaoInvalida,
                "Nenhuma linha de dados encontrada: confira se a planilha tem a coluna \"Processo\" no cabeçalho.");

        // Os ativos vêm de uma consulta só: o upsert e a checagem de duplicidade
        // rodam em memória, sem N+1
        var ativos = (await _repositorio.ListarAtivosAsync())
            .GroupBy(p => p.NumeroProcesso)
            .ToDictionary(g => g.Key, g => g.First());

        var resultado = new List<CtrImportacaoLinha>();

        foreach (var lida in lidas)
        {
            var linha = new CtrImportacaoLinha
            {
                Linha = lida.Linha,
                NumeroProcesso = lida.NumeroProcesso,
                OrgaoSigla = lida.OrgaoSigla,
                CategoriaObjeto = lida.CategoriaObjeto
            };

            if (lida.Erro != null || lida.Dados == null)
            {
                linha.Acao = CtrImportacaoAcao.Rejeitar;
                linha.Motivo = lida.Erro ?? "linha inválida";
                resultado.Add(linha);
                continue;
            }

            // Valida SEMPRE num candidato solto: linha inválida não pode deixar
            // rastro na entidade já existente (o SaveChanges é único, no fim)
            var candidato = new CtrProcesso();
            CtrProcessoService.AplicarDto(candidato, lida.Dados);

            var existente = ativos.TryGetValue(candidato.NumeroProcesso.Trim(), out var achado) ? achado : null;

            try
            {
                // Número repetido no arquivo já foi barrado pelo parser; o que existe
                // no banco é justamente o alvo do update, então nunca é "duplicado".
                CtrProcessoService.ValidarProcesso(candidato, numeroDuplicado: false);
            }
            catch (ApiException ex)
            {
                linha.Acao = CtrImportacaoAcao.Rejeitar;
                linha.Motivo = ex.Error.Message;
                resultado.Add(linha);
                continue;
            }

            linha.NumeroProcesso = candidato.NumeroProcesso;
            linha.OrgaoSigla = candidato.OrgaoSigla;
            linha.CategoriaObjeto = candidato.CategoriaObjeto;
            linha.Situacao = CtrProcessoService.CalcularSituacao(candidato);
            linha.Dados = CtrProcessoService.DtoDe(candidato);
            linha.Acao = existente == null ? CtrImportacaoAcao.Criar : CtrImportacaoAcao.Atualizar;

            if (ctx == null) // prévia: nada é gravado
            {
                resultado.Add(linha);
                continue;
            }

            if (existente != null)
            {
                // Todos os campos vêm da planilha (a planilha é a fonte na importação)
                CtrProcessoService.AplicarDto(existente, linha.Dados);
                existente.AlteradoEm = DateTime.UtcNow;
                existente.AlteradoPor = ctx.Email;
            }
            else
            {
                candidato.CriadoEm = DateTime.UtcNow;
                candidato.CriadoPor = ctx.Email;
                _repositorio.Add(candidato);
                ativos[candidato.NumeroProcesso] = candidato;
            }

            resultado.Add(linha);
        }

        return resultado;
    }
}
