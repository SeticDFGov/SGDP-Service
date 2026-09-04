using Microsoft.EntityFrameworkCore;
using Models;
using Models.Pgia;
using service.Interface;

namespace service.Pgia;

/// <summary>
/// Anexos gravados no PRÓPRIO BANCO (bytea), por decisão do responsável pelo
/// sistema: sem dependência de disco do contêiner nem de volume compartilhado
/// entre réplicas — o backup do banco já leva os anexos junto.
///
/// O binário fica em <c>pgia_documento_arquivo</c>, tabela SEPARADA e 1:1 com
/// <c>pgia_documento</c>, sem navegação do lado do documento: nenhuma listagem ou
/// Include consegue arrastar o blob; só o download seleciona <c>conteudo</c>.
///
/// **Futuro:** quando a Infra entregar o servidor de arquivos, troca-se esta
/// implementação por outra de <see cref="IPgiaArquivoStorage"/> — endpoints,
/// telas e metadados seguem iguais.
/// </summary>
public class PgiaArquivoStorage : IPgiaArquivoStorage
{
    private readonly AppDbContext _context;

    public PgiaArquivoStorage(AppDbContext context)
    {
        _context = context;
    }

    public async Task SalvarAsync(long documentoId, byte[] conteudo, CancellationToken cancellationToken = default)
    {
        var existente = await _context.PgiaDocumentoArquivos
            .FirstOrDefaultAsync(a => a.DocumentoId == documentoId, cancellationToken);

        if (existente == null)
        {
            _context.PgiaDocumentoArquivos.Add(new PgiaDocumentoArquivo
            {
                DocumentoId = documentoId,
                Conteudo = conteudo
            });
        }
        else
        {
            // Reenvio substitui o conteúdo da mesma linha (1:1)
            existente.Conteudo = conteudo;
        }
    }

    public async Task<byte[]?> AbrirAsync(long documentoId, CancellationToken cancellationToken = default)
    {
        return await _context.PgiaDocumentoArquivos
            .AsNoTracking()
            .Where(a => a.DocumentoId == documentoId)
            .Select(a => a.Conteudo)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task ReconciliarConflitoAsync(
        long documentoId, byte[] conteudo, CancellationToken cancellationToken = default)
    {
        // Larga o INSERT que colidiu para poder reconsultar a linha vencedora
        var pendente = _context.ChangeTracker.Entries<PgiaDocumentoArquivo>()
            .FirstOrDefault(e => e.Entity.DocumentoId == documentoId
                && e.State == Microsoft.EntityFrameworkCore.EntityState.Added);
        if (pendente != null) pendente.State = Microsoft.EntityFrameworkCore.EntityState.Detached;

        var existente = await _context.PgiaDocumentoArquivos
            .FirstOrDefaultAsync(a => a.DocumentoId == documentoId, cancellationToken);

        if (existente == null)
        {
            // A linha sumiu no meio do caminho: refaz o insert
            _context.PgiaDocumentoArquivos.Add(new PgiaDocumentoArquivo
            {
                DocumentoId = documentoId,
                Conteudo = conteudo
            });
            return;
        }

        existente.Conteudo = conteudo;
    }

    public async Task ApagarAsync(long documentoId, CancellationToken cancellationToken = default)
    {
        var existente = await _context.PgiaDocumentoArquivos
            .FirstOrDefaultAsync(a => a.DocumentoId == documentoId, cancellationToken);

        if (existente != null) _context.PgiaDocumentoArquivos.Remove(existente);
    }
}
