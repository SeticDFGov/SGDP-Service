using api.Contratacoes;
using app.Auth;
using Microsoft.EntityFrameworkCore;
using Models;
using service.Interface;

namespace service.Contratacoes;

/// <summary>
/// Administração do papel do módulo (tela de admin do SGDP). Escreve direto no
/// AppDbContext, como o PgiaAdminService: o AuthRepositorio não é tocado.
/// </summary>
public class CtrAdminService : ICtrAdminService
{
    private readonly AppDbContext _context;

    public CtrAdminService(AppDbContext context)
    {
        _context = context;
    }

    public async Task AtribuirPapelAsync(AtribuirPapelContratacoesDTO dto, string adminEmail)
    {
        if (!PapeisContratacoes.EhValido(dto.PapelContratacoes))
            throw new ApiException(ErrorCode.CtrPapelInvalido,
                $"Papel inválido para o módulo Análises de Contratações: {dto.PapelContratacoes}");

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == dto.Email)
            ?? throw new ApiException(ErrorCode.CtrUsuarioNaoEncontrado);

        // Só o papel do módulo muda; o Perfil do SGDP nunca é tocado aqui.
        user.PapelContratacoes = dto.PapelContratacoes;
        await _context.SaveChangesAsync();
    }
}
