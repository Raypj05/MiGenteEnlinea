using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MiGenteEnLinea.Application.Common.Interfaces;
using MiGenteEnLinea.Application.Common.Exceptions;
using MiGenteEnLinea.Domain.Interfaces.Repositories;
using MiGenteEnLinea.Domain.Interfaces.Repositories.Empleadores;

namespace MiGenteEnLinea.Application.Features.Empleadores.Commands.DeleteEmpleador;

/// <summary>
/// Handler: Procesa la eliminación lógica (soft delete) del Empleador
/// </summary>
/// <remarks>
/// ✅ SOFT DELETE IMPLEMENTADO (Oct 2025)
/// 
/// La entidad Empleador ahora hereda de SoftDeletableEntity.
/// Este handler marca el registro como eliminado (IsDeleted=true) sin borrado físico.
/// 
/// BENEFICIOS:
/// - Auditoría completa (quién y cuándo eliminó)
/// - Posibilidad de restaurar (método Undelete)
/// - Preserva integridad referencial
/// - Historial completo de datos
/// 
/// ✅ SECURITY FIX (Oct 2025): Ownership validation implementada
/// - Verifica que el usuario solo pueda eliminar su propio perfil
/// - Admins pueden eliminar cualquier perfil (bypass)
/// - Lanza ForbiddenAccessException (403) si no tiene permisos
/// </remarks>
public sealed class DeleteEmpleadorCommandHandler : IRequestHandler<DeleteEmpleadorCommand, bool>
{
    private readonly IEmpleadorRepository _empleadorRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<DeleteEmpleadorCommandHandler> _logger;

    public DeleteEmpleadorCommandHandler(
        IEmpleadorRepository empleadorRepository,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        ILogger<DeleteEmpleadorCommandHandler> logger)
    {
        _empleadorRepository = empleadorRepository;
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    /// <summary>
    /// Maneja la eliminación lógica del empleador
    /// </summary>
    /// <exception cref="InvalidOperationException">Si empleador no existe</exception>
    /// <exception cref="ForbiddenAccessException">Si usuario no tiene permisos</exception>
    public async Task<bool> Handle(DeleteEmpleadorCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Procesando eliminación lógica (soft delete) de empleador. UserId: {UserId}",
            request.UserId);

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
                "⚠️ INTENTO DE ACCESO NO AUTORIZADO: Usuario {CurrentUserId} intentó eliminar perfil de {TargetUserId}",
                currentUserId, request.UserId);

            throw new ForbiddenAccessException("No tiene permisos para eliminar este perfil.");
        }

        _logger.LogInformation(
            "✅ Authorization check passed. CurrentUser: {CurrentUserId}, TargetUser: {TargetUserId}, IsAdmin: {IsAdmin}",
            currentUserId, request.UserId, isAdmin);

        // ============================================
        // PASO 3: Soft Delete (marca como eliminado)
        // ============================================
        var deletedBy = currentUserId ?? "system";
        empleador.Delete(deletedBy);

        _logger.LogInformation(
            "Empleador marcado como eliminado. EmpleadorId: {EmpleadorId}, EliminadoPor: {DeletedBy}",
            empleador.Id, deletedBy);

        // ============================================
        // PASO 4: Guardar cambios
        // ============================================
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "✅ Soft delete completado exitosamente. EmpleadorId: {EmpleadorId}, UserId: {UserId}",
            empleador.Id, request.UserId);

        return true;
    }
}


