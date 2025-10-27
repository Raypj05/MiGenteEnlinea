using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MiGenteEnLinea.Application.Common.Interfaces;
using MiGenteEnLinea.Domain.Interfaces.Repositories;
using MiGenteEnLinea.Domain.Interfaces.Repositories.Authentication;

namespace MiGenteEnLinea.Application.Features.Authentication.Commands.ActivateAccount;

/// <summary>
/// Handler para ActivateAccountCommand
/// ESTRATEGIA DE MIGRACIÓN: Identity Primary + Legacy Sync
/// 1. Activar cuenta en ASP.NET Core Identity (EmailConfirmed = true) - PRIMARIO
/// 2. Sincronizar con tabla Legacy Credenciales (Activo = true) - SECUNDARIO
/// Réplica EXACTA de Activar.aspx.cs del Legacy
/// </summary>
public sealed class ActivateAccountCommandHandler : IRequestHandler<ActivateAccountCommand, bool>
{
    private readonly IIdentityService _identityService; // ✅ Sistema Identity (primario)
    private readonly ICredencialRepository _credencialRepository; // ✅ Para sincronizar Legacy
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ActivateAccountCommandHandler> _logger;

    public ActivateAccountCommandHandler(
        IIdentityService identityService,
        ICredencialRepository credencialRepository,
        IUnitOfWork unitOfWork,
        ILogger<ActivateAccountCommandHandler> logger)
    {
        _identityService = identityService;
        _credencialRepository = credencialRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<bool> Handle(ActivateAccountCommand request, CancellationToken cancellationToken)
    {
        // ================================================================================
        // PASO 1: ACTIVAR CUENTA EN IDENTITY (PRIMARIO) ✅
        // ================================================================================
        // Identity maneja: EmailConfirmed = true
        var identitySuccess = await _identityService.ActivateAccountAsync(request.UserId, request.Email);

        if (!identitySuccess)
        {
            _logger.LogWarning(
                "Activación fallida en Identity. UserId: {UserId}, Email: {Email}",
                request.UserId, request.Email);
            return false;
        }

        _logger.LogInformation(
            "Cuenta activada en Identity. UserId: {UserId}, Email: {Email}",
            request.UserId, request.Email);

        // ================================================================================
        // PASO 2: SINCRONIZAR CON TABLA LEGACY (SECUNDARIO) ✅
        // ================================================================================
        // Nota: Si esta sincronización falla, el usuario aún puede autenticarse con Identity
        try
        {
            var credencial = await _credencialRepository.GetByUserIdAsync(request.UserId, cancellationToken);

            if (credencial == null)
            {
                _logger.LogWarning(
                    "Credencial Legacy no encontrada para sincronización. UserId: {UserId}. Usuario puede autenticarse con Identity.",
                    request.UserId);
                return true; // Retornar success porque Identity ya está activado
            }

            // Activar en Legacy
            credencial.Activar();
            _credencialRepository.Update(credencial);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Tabla Legacy Credenciales sincronizada. UserId: {UserId}",
                request.UserId);
        }
        catch (Exception ex)
        {
            // NO fallar la operación si Legacy sync falla
            // El usuario ya está activado en Identity y puede autenticarse
            _logger.LogError(
                ex,
                "Error al sincronizar con Legacy Credenciales. UserId: {UserId}. Usuario activado en Identity correctamente.",
                request.UserId);
        }

        return true;
    }
}
