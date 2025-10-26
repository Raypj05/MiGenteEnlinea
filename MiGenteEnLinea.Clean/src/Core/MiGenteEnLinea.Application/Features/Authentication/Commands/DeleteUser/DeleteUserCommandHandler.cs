using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MiGenteEnLinea.Application.Common.Interfaces;

namespace MiGenteEnLinea.Application.Features.Authentication.Commands.DeleteUser;

/// <summary>
/// Handler para DeleteUserCommand.
/// Implementa SOFT DELETE marcando el usuario como inactivo.
/// SINCRONIZA con Identity y Legacy Credenciales.
/// </summary>
public class DeleteUserCommandHandler : IRequestHandler<DeleteUserCommand, bool>
{
    private readonly IApplicationDbContext _context;
    private readonly UserManager<IdentityUser> _userManager;
    private readonly ILogger<DeleteUserCommandHandler> _logger;

    public DeleteUserCommandHandler(
        IApplicationDbContext context,
        UserManager<IdentityUser> userManager,
        ILogger<DeleteUserCommandHandler> logger)
    {
        _context = context;
        _userManager = userManager;
        _logger = logger;
    }

    public async Task<bool> Handle(DeleteUserCommand request, CancellationToken cancellationToken)
    {
        try
        {
            // ================================================================================
            // 1. DESACTIVAR EN IDENTITY (Primary)
            // ================================================================================
            var identityUser = await _userManager.FindByIdAsync(request.UserID);
            
            if (identityUser != null)
            {
                // Desactivar en Identity
                identityUser.LockoutEnabled = true;
                identityUser.LockoutEnd = DateTimeOffset.MaxValue; // Lock permanently
                
                var identityResult = await _userManager.UpdateAsync(identityUser);
                
                if (!identityResult.Succeeded)
                {
                    _logger.LogWarning(
                        "No se pudo desactivar usuario en Identity. UserID: {UserID}, Errores: {Errors}",
                        request.UserID,
                        string.Join(", ", identityResult.Errors.Select(e => e.Description)));
                }
                else
                {
                    _logger.LogInformation("Usuario desactivado en Identity. UserID: {UserID}", request.UserID);
                }
            }

            // ================================================================================
            // 2. DESACTIVAR EN LEGACY CREDENCIALES (Compatibility)
            // ================================================================================
            var credencial = await _context.Credenciales
                .FirstOrDefaultAsync(c => c.UserId == request.UserID && c.Id == request.CredencialID, cancellationToken);

            if (credencial == null)
            {
                _logger.LogWarning(
                    "Credencial Legacy no encontrada. UserID: {UserID}, CredencialID: {CredencialID}",
                    request.UserID,
                    request.CredencialID);

                // Si existe en Identity, consideramos exitoso
                return identityUser != null;
            }

            // Desactivar en Legacy
            credencial.Desactivar();

            // Guardar cambios en Legacy
            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Usuario eliminado (soft delete) exitosamente en Identity y Legacy. UserID: {UserID}, CredencialID: {CredencialID}",
                request.UserID,
                request.CredencialID);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error al eliminar usuario. UserID: {UserID}, CredencialID: {CredencialID}",
                request.UserID,
                request.CredencialID);

            return false;
        }
    }
}
