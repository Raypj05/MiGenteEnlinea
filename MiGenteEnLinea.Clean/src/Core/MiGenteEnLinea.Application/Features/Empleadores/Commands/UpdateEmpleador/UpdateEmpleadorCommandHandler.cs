using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MiGenteEnLinea.Application.Common.Interfaces;
using MiGenteEnLinea.Application.Common.Exceptions;
using MiGenteEnLinea.Domain.Interfaces.Repositories;
using MiGenteEnLinea.Domain.Interfaces.Repositories.Empleadores;

namespace MiGenteEnLinea.Application.Features.Empleadores.Commands.UpdateEmpleador;

/// <summary>
/// Handler: Procesa la actualización del perfil de Empleador
/// </summary>
/// <remarks>
/// ✅ SECURITY FIX (Oct 2025): Ownership validation implementada
/// - Verifica que el usuario solo pueda editar su propio perfil
/// - Admins pueden editar cualquier perfil (bypass)
/// - Lanza ForbiddenAccessException (403) si no tiene permisos
/// </remarks>
public sealed class UpdateEmpleadorCommandHandler : IRequestHandler<UpdateEmpleadorCommand, bool>
{
    private readonly IEmpleadorRepository _empleadorRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<UpdateEmpleadorCommandHandler> _logger;

    public UpdateEmpleadorCommandHandler(
        IEmpleadorRepository empleadorRepository,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        ILogger<UpdateEmpleadorCommandHandler> logger)
    {
        _empleadorRepository = empleadorRepository;
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    /// <summary>
    /// Maneja la actualización del empleador
    /// </summary>
    /// <exception cref="InvalidOperationException">Si empleador no existe</exception>
    /// <exception cref="ForbiddenAccessException">Si usuario no tiene permisos</exception>
    public async Task<bool> Handle(UpdateEmpleadorCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Actualizando empleador para userId: {UserId}", request.UserId);

        // ============================================
        // PASO 1: Buscar empleador por userId
        // ============================================
        var empleador = await _empleadorRepository.GetByUserIdAsync(request.UserId, cancellationToken);

        if (empleador == null)
        {
            _logger.LogWarning("Empleador no encontrado para userId: {UserId}", request.UserId);
            throw new InvalidOperationException($"Empleador no encontrado para usuario {request.UserId}");
        }

        // ============================================
        // PASO 2: SECURITY CHECK - Ownership validation
        // ============================================
        var currentUserId = _currentUserService.UserId;
        var isAdmin = _currentUserService.IsInRole("Admin");

        // Verificar que el usuario actual sea el dueño del perfil O sea Admin
        if (currentUserId != request.UserId && !isAdmin)
        {
            _logger.LogWarning(
                "⚠️ INTENTO DE ACCESO NO AUTORIZADO: Usuario {CurrentUserId} intentó editar perfil de {TargetUserId}",
                currentUserId, request.UserId);

            throw new ForbiddenAccessException("No tiene permisos para editar este perfil.");
        }

        _logger.LogInformation(
            "✅ Authorization check passed. CurrentUser: {CurrentUserId}, TargetUser: {TargetUserId}, IsAdmin: {IsAdmin}",
            currentUserId, request.UserId, isAdmin);

        // ============================================
        // PASO 3: Actualizar con método de dominio
        // ============================================
        // El método ActualizarPerfil() de la entidad Empleador maneja:
        // - Validaciones de longitud
        // - Trim de strings
        // - Levanta eventos de dominio (PerfilActualizadoEvent)
        empleador.ActualizarPerfil(
            habilidades: request.Habilidades,
            experiencia: request.Experiencia,
            descripcion: request.Descripcion
        );

        // ============================================
        // PASO 4: Guardar cambios
        // ============================================
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Empleador actualizado exitosamente. EmpleadorId: {EmpleadorId}, UserId: {UserId}",
            empleador.Id, request.UserId);

        return true;
    }
}

