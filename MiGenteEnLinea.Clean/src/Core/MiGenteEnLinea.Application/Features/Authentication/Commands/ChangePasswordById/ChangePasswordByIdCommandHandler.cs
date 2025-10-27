using MediatR;
using Microsoft.Extensions.Logging;
using MiGenteEnLinea.Application.Common.Interfaces;
using MiGenteEnLinea.Domain.Interfaces.Repositories;

namespace MiGenteEnLinea.Application.Features.Authentication.Commands.ChangePasswordById;

/// <summary>
/// Handler para ChangePasswordByIdCommand
/// SINCRONIZA cambios de password en Identity y Legacy Credenciales
/// GAP-014: Cambia password usando credential ID en lugar de userID
/// </summary>
public sealed class ChangePasswordByIdCommandHandler : IRequestHandler<ChangePasswordByIdCommand, bool>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IIdentityService _identityService; // CHANGED: Use IIdentityService abstraction
    private readonly IPasswordHasher _passwordHasher;
    private readonly ILogger<ChangePasswordByIdCommandHandler> _logger;

    public ChangePasswordByIdCommandHandler(
        IUnitOfWork unitOfWork,
        IIdentityService identityService, // CHANGED: Use IIdentityService abstraction
        IPasswordHasher _passwordHasher,
        ILogger<ChangePasswordByIdCommandHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _identityService = identityService;
        this._passwordHasher = _passwordHasher;
        _logger = logger;
    }

    public async Task<bool> Handle(ChangePasswordByIdCommand request, CancellationToken cancellationToken)
    {
        // ================================================================================
        // PASO 1: OBTENER CREDENCIAL LEGACY POR ID
        // ================================================================================
        var credencial = await _unitOfWork.Credenciales.GetByIdAsync(request.CredencialId, cancellationToken);

        if (credencial == null)
        {
            _logger.LogWarning(
                "No se encontró credencial para actualizar password. CredencialId: {CredencialId}",
                request.CredencialId);
            return false;
        }

        // ================================================================================
        // PASO 2: HASHEAR NUEVA CONTRASEÑA
        // ================================================================================
        var passwordHasheado = _passwordHasher.HashPassword(request.NewPassword);

        // ================================================================================
        // PASO 3: ACTUALIZAR EN IDENTITY (Primary) - Using IIdentityService
        // ================================================================================
        var identitySuccess = await _identityService.ChangePasswordByIdAsync(credencial.UserId, request.NewPassword);
        
        if (!identitySuccess)
        {
            _logger.LogError(
                "CRITICAL: Failed to update password in Identity. Operation aborted. UserId: {UserId}, CredencialId: {CredencialId}",
                credencial.UserId,
                request.CredencialId);
            
            // NO continuar si Identity falla - Identity es el sistema primario
            return false;
        }

        _logger.LogInformation("Password actualizado en Identity. UserId: {UserId}", credencial.UserId);

        // ================================================================================
        // PASO 4: ACTUALIZAR EN LEGACY CREDENCIALES (Compatibility)
        // ================================================================================
        credencial.ActualizarPasswordHash(passwordHasheado);

        // ================================================================================
        // PASO 5: GUARDAR CAMBIOS EN LEGACY
        // ================================================================================
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Contraseña actualizada exitosamente en Identity y Legacy. CredencialId: {CredencialId}, UserId: {UserId}",
            request.CredencialId,
            credencial.UserId);

        return true;
    }
}
