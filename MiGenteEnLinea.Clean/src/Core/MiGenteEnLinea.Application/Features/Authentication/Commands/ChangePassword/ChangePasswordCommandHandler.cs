using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MiGenteEnLinea.Application.Common.Interfaces;
using MiGenteEnLinea.Application.Features.Authentication.DTOs;
using MiGenteEnLinea.Domain.Interfaces.Repositories;
using MiGenteEnLinea.Domain.Interfaces.Repositories.Authentication;

namespace MiGenteEnLinea.Application.Features.Authentication.Commands.ChangePassword;

/// <summary>
/// Handler para cambiar contraseña
/// ESTRATEGIA DE MIGRACIÓN: Identity Primary + Legacy Sync
/// 1. Cambiar password en ASP.NET Core Identity - PRIMARIO
/// 2. Sincronizar con tabla Legacy Credenciales - SECUNDARIO
/// Réplica de SuscripcionesService.actualizarPass()
/// </summary>
public class ChangePasswordCommandHandler : IRequestHandler<ChangePasswordCommand, ChangePasswordResult>
{
    private readonly IIdentityService _identityService; // ✅ Sistema Identity (primario)
    private readonly ICredencialRepository _credencialRepository; // ✅ Para sincronizar Legacy
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ILogger<ChangePasswordCommandHandler> _logger;

    public ChangePasswordCommandHandler(
        IIdentityService identityService,
        ICredencialRepository credencialRepository,
        IUnitOfWork unitOfWork,
        IPasswordHasher passwordHasher,
        ILogger<ChangePasswordCommandHandler> logger)
    {
        _identityService = identityService;
        _credencialRepository = credencialRepository;
        _unitOfWork = unitOfWork;
        _passwordHasher = passwordHasher;
        _logger = logger;
    }

    public async Task<ChangePasswordResult> Handle(ChangePasswordCommand request, CancellationToken cancellationToken)
    {
        // ================================================================================
        // PASO 1: CAMBIAR PASSWORD EN IDENTITY (PRIMARIO) ✅
        // ================================================================================
        var identitySuccess = await _identityService.ChangePasswordAsync(
            request.UserId,
            request.CurrentPassword,
            request.NewPassword
        );

        if (!identitySuccess)
        {
            _logger.LogWarning(
                "Cambio de contraseña fallido en Identity. UserId: {UserId}",
                request.UserId);
            
            return new ChangePasswordResult
            {
                Success = false,
                Message = "Contraseña actual incorrecta o error al actualizar"
            };
        }

        _logger.LogInformation(
            "Contraseña actualizada en Identity. UserId: {UserId}",
            request.UserId);

        // ================================================================================
        // PASO 2: SINCRONIZAR CON TABLA LEGACY (SECUNDARIO) ✅
        // ================================================================================
        try
        {
            var credencial = await _credencialRepository.GetByUserIdAsync(request.UserId, cancellationToken);

            if (credencial == null)
            {
                _logger.LogWarning(
                    "Credencial Legacy no encontrada para sincronización. UserId: {UserId}. Password actualizado en Identity.",
                    request.UserId);
                
                return new ChangePasswordResult
                {
                    Success = true,
                    Message = "Contraseña actualizada exitosamente"
                };
            }

            // Validación adicional: verificar email
            if (credencial.Email.Value != request.Email)
            {
                _logger.LogWarning(
                    "Email mismatch al sincronizar Legacy. UserId: {UserId}, CredencialEmail: {CredencialEmail}, RequestEmail: {RequestEmail}",
                    request.UserId, credencial.Email.Value, request.Email);
                
                return new ChangePasswordResult
                {
                    Success = true,
                    Message = "Contraseña actualizada exitosamente (sincronización Legacy omitida)"
                };
            }

            // Actualizar password en Legacy
            var newPasswordHash = _passwordHasher.HashPassword(request.NewPassword);
            credencial.ActualizarPasswordHash(newPasswordHash);
            _credencialRepository.Update(credencial);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Tabla Legacy Credenciales sincronizada. UserId: {UserId}",
                request.UserId);
        }
        catch (Exception ex)
        {
            // NO fallar la operación si Legacy sync falla
            _logger.LogError(
                ex,
                "Error al sincronizar password con Legacy Credenciales. UserId: {UserId}. Password actualizado en Identity correctamente.",
                request.UserId);
        }

        return new ChangePasswordResult
        {
            Success = true,
            Message = "Contraseña actualizada exitosamente"
        };
    }
}
