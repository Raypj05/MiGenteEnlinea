using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MiGenteEnLinea.Application.Common.Interfaces;
using MiGenteEnLinea.Domain.Interfaces.Repositories;
using MiGenteEnLinea.Domain.Interfaces.Repositories.Authentication;

namespace MiGenteEnLinea.Application.Features.Authentication.Commands.DeleteUser;

/// <summary>
/// Handler para DeleteUserCommand.
/// Implementa SOFT DELETE marcando el usuario como inactivo.
/// SINCRONIZA con Identity y Legacy Credenciales.
/// FIXED: Use Repository Pattern (same as ActivateAccountCommandHandler) for consistency
/// </summary>
public class DeleteUserCommandHandler : IRequestHandler<DeleteUserCommand, bool>
{
    private readonly IIdentityService _identityService;
    private readonly ICredencialRepository _credencialRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IApplicationDbContext _context; // ✅ Agregar para acceso directo al ChangeTracker
    private readonly ILogger<DeleteUserCommandHandler> _logger;

    public DeleteUserCommandHandler(
        IIdentityService identityService,
        ICredencialRepository credencialRepository,
        IUnitOfWork unitOfWork,
        IApplicationDbContext context, // ✅ Inyectar DbContext
        ILogger<DeleteUserCommandHandler> logger)
    {
        _identityService = identityService;
        _credencialRepository = credencialRepository;
        _unitOfWork = unitOfWork;
        _context = context; // ✅ Almacenar para acceso al ChangeTracker
        _logger = logger;
    }

    public async Task<bool> Handle(DeleteUserCommand request, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("[DELETE-HANDLER] Starting delete. UserID: {UserID}, CredencialID: {CredencialID}", request.UserID, request.CredencialID);

            // ================================================================================
            // 1. DESACTIVAR EN IDENTITY (PRIMARY auth system)
            // ================================================================================
            var identitySuccess = await _identityService.DeactivateUserAsync(request.UserID);
            
            if (!identitySuccess)
            {
                _logger.LogError(
                    "CRITICAL: Failed to deactivate user in Identity. Operation aborted. UserID: {UserID}",
                    request.UserID);
                return false;
            }

            _logger.LogInformation("[DELETE-HANDLER] User deactivated in Identity successfully. UserID: {UserID}", request.UserID);

            // ================================================================================
            // 2. DESACTIVAR EN LEGACY CREDENCIALES (BUSINESS LOGIC compatibility)
            // ================================================================================
            // FIXED: Use Repository Pattern (same as ActivateAccountCommandHandler)
            _logger.LogInformation("[DELETE-HANDLER] Querying Legacy Credenciales via Repository...");
            var credencial = await _credencialRepository.GetByUserIdAsync(request.UserID, cancellationToken);

            if (credencial == null)
            {
                _logger.LogWarning(
                    "Legacy Credencial not found. UserID: {UserID}. Identity deactivation successful.",
                    request.UserID);

                // Identity deactivation succeeded, that's sufficient for auth
                return true;
            }

            // ADDITIONAL VALIDATION: Check CredencialID matches (security)
            if (credencial.Id != request.CredencialID)
            {
                _logger.LogError(
                    "SECURITY: CredencialID mismatch! Expected: {ExpectedId}, Found: {FoundId}. UserID: {UserID}",
                    request.CredencialID,
                    credencial.Id,
                    request.UserID);
                return false;
            }

            _logger.LogInformation("[DELETE-HANDLER] BEFORE Desactivar(): Activo={Activo}, UserId={UserId}, CredencialId={CredencialId}", 
                credencial.Activo, credencial.UserId, credencial.Id);

            // Desactivar en Legacy (for business logic queries)
            credencial.Desactivar();

            _logger.LogInformation("[DELETE-HANDLER] AFTER Desactivar(): Activo={Activo}", credencial.Activo);

            // ✅ SOLUCIÓN EF CORE: Marcar explícitamente la propiedad Activo como Modified
            // IApplicationDbContext es realmente un DbContext, castear para acceso al ChangeTracker
            var dbContext = _context as DbContext;
            if (dbContext != null)
            {
                var entry = dbContext.Entry(credencial);
                entry.Property(nameof(credencial.Activo)).IsModified = true;
                
                _logger.LogInformation("[DELETE-HANDLER] EF Core Entry State: {State}, Activo IsModified: {IsModified}", 
                    entry.State, entry.Property(nameof(credencial.Activo)).IsModified);
            }
            else
            {
                // Fallback: llamar Update() explícitamente
                _credencialRepository.Update(credencial);
            }

            // Guardar cambios (EF Core ahora SABE que Activo cambió)
            var changesSaved = await _unitOfWork.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("[DELETE-HANDLER] SaveChanges completed. Changes saved: {ChangesSaved}", changesSaved);

            _logger.LogInformation(
                "User deleted (soft delete) successfully in Identity and Legacy. UserID: {UserID}, CredencialID: {CredencialID}",
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
