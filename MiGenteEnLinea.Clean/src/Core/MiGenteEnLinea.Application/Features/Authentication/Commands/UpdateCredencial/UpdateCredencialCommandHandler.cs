using MediatR;
using Microsoft.Extensions.Logging;
using MiGenteEnLinea.Application.Common.Interfaces;
using MiGenteEnLinea.Domain.Interfaces.Repositories;

namespace MiGenteEnLinea.Application.Features.Authentication.Commands.UpdateCredencial;

/// <summary>
/// Handler para UpdateCredencialCommand
/// Réplica EXACTA de SuscripcionesService.actualizarCredenciales() del Legacy
/// GAP-012: Actualiza password, email y estado activo en una credencial
/// 
/// ✅ DUAL-WRITE IMPLEMENTATION:
/// Updates BOTH Legacy (Credenciales) AND Identity (AspNetUsers) systems
/// Ensures synchronization between old and new authentication systems
/// </summary>
public sealed class UpdateCredencialCommandHandler : IRequestHandler<UpdateCredencialCommand, bool>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IIdentityService _identityService;
    private readonly ILogger<UpdateCredencialCommandHandler> _logger;

    public UpdateCredencialCommandHandler(
        IUnitOfWork unitOfWork,
        IPasswordHasher passwordHasher,
        IIdentityService identityService,
        ILogger<UpdateCredencialCommandHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _passwordHasher = passwordHasher;
        _identityService = identityService;
        _logger = logger;
    }

    /// <summary>
    /// Actualiza credencial completa (password + email + activo)
    /// 
    /// Legacy behavior (SuscripcionesService.cs líneas 157-177):
    /// - Query: db.Credenciales.Where(x => x.email == c.email AND x.userID == c.userID).FirstOrDefault()
    /// - Si existe: result.password = c.password; result.activo = c.activo; result.email = c.email;
    /// - db.SaveChanges()
    /// - Retorna true
    /// 
    /// ⚠️ PROBLEMA LEGACY:
    /// - El WHERE usa email + userID, pero el password ya viene ENCRIPTADO desde el cliente
    /// - En MiPerfilEmpleador.aspx.cs línea 275: cr.password = crypt.Encrypt(txtPassword.Text);
    /// - Legacy NO valida si el email ya existe en otra credencial (puede causar duplicados)
    /// 
    /// Clean behavior:
    /// - Query por userID solamente (más seguro)
    /// - Hashea password con BCrypt (si se provee)
    /// - Valida que nuevo email no exista en otra credencial
    /// - Actualiza email, password (hasheado) y activo
    /// </summary>
    public async Task<bool> Handle(UpdateCredencialCommand request, CancellationToken cancellationToken)
    {
        // ================================================================================
        // PASO 1: OBTENER CREDENCIAL ACTUAL POR USERID
        // ================================================================================
        // Legacy línea 163: db.Credenciales.Where(x => x.email == c.email && x.userID == c.userID).FirstOrDefault()
        // Clean: Solo por userId (más seguro)
        var credencial = await _unitOfWork.Credenciales
            .GetByUserIdAsync(request.UserId, cancellationToken);

        if (credencial == null)
        {
            _logger.LogWarning(
                "No se encontró credencial para actualizar. UserId: {UserId}",
                request.UserId);
            return false;
        }

        // ================================================================================
        // PASO 2: VALIDAR QUE NUEVO EMAIL NO EXISTA EN OTRA CREDENCIAL
        // ================================================================================
        // ⚠️ Legacy NO hace esta validación, pero Clean sí debe hacerla para evitar duplicados
        if (credencial.Email.Value != request.Email)
        {
            var emailExiste = await _unitOfWork.Credenciales
                .ExistsByEmailAsync(request.Email, cancellationToken);

            if (emailExiste)
            {
                _logger.LogWarning(
                    "Email ya existe en otra credencial. Email: {Email}",
                    request.Email);
                return false;
            }
        }

        // ================================================================================
        // PASO 3: ACTUALIZAR CREDENCIAL
        // ================================================================================
        // Legacy líneas 166-168:
        // result.password = c.password;  // ⚠️ Ya viene encriptado desde cliente
        // result.activo = c.activo;
        // result.email = c.email;

        // 🔑 CRITICAL: Capture original values BEFORE updating for dual-write sync
        var originalEmail = credencial.Email.Value;
        var originalActivo = credencial.Activo;
        bool emailChanged = originalEmail != request.Email;
        bool passwordChanged = !string.IsNullOrWhiteSpace(request.Password);

        // Actualizar email
        var nuevoEmail = Domain.ValueObjects.Email.Create(request.Email);
        if (nuevoEmail == null)
        {
            _logger.LogWarning("Email inválido: {Email}", request.Email);
            return false;
        }
        credencial.ActualizarEmail(nuevoEmail);

        // Actualizar password (solo si se provee)
        if (passwordChanged)
        {
            var passwordHasheado = _passwordHasher.HashPassword(request.Password);
            credencial.ActualizarPasswordHash(passwordHasheado);
        }

        // Actualizar estado activo
        if (request.Activo && !credencial.Activo)
        {
            credencial.Activar();
        }
        else if (!request.Activo && credencial.Activo)
        {
            credencial.Desactivar();
        }

        // ================================================================================
        // PASO 3.5: SYNC TO IDENTITY (DUAL-WRITE PATTERN)
        // ================================================================================
        // ✅ CRITICAL: Update AspNetUsers to keep both systems in sync
        // This prevents issues where Legacy user is deactivated but Identity still allows login
        
        try
        {
            // Update email in Identity if changed (using captured ORIGINAL email)
            if (emailChanged)
            {
                _logger.LogInformation(
                    "Email changed from {OldEmail} to {NewEmail}, syncing to Identity for user {UserId}",
                    originalEmail, request.Email, request.UserId);
                
                var emailUpdated = await _identityService.UpdateUserEmailAsync(request.UserId, request.Email);
                if (!emailUpdated)
                {
                    _logger.LogWarning(
                        "Failed to sync email to Identity for user {UserId}. Legacy updated but Identity failed.",
                        request.UserId);
                    // Continue - Legacy is updated, Identity sync failed (log warning)
                }
            }

            // Update password in Identity if provided
            if (passwordChanged)
            {
                _logger.LogInformation("Password changed, syncing to Identity for user {UserId}", request.UserId);
                
                // Safe: passwordChanged is true only when request.Password is not null/whitespace
                var passwordUpdated = await _identityService.ChangePasswordByIdAsync(request.UserId, request.Password!);
                if (!passwordUpdated)
                {
                    _logger.LogWarning(
                        "Failed to sync password to Identity for user {UserId}. Legacy updated but Identity failed.",
                        request.UserId);
                    // Continue - Legacy is updated, Identity sync failed (log warning)
                }
            }

            // Update active status in Identity
            if (!request.Activo)
            {
                _logger.LogInformation("User deactivated, syncing to Identity for user {UserId}", request.UserId);
                
                // Deactivate user in Identity by locking out permanently
                var deactivated = await _identityService.DeactivateUserAsync(request.UserId);
                if (!deactivated)
                {
                    _logger.LogWarning(
                        "Failed to deactivate user in Identity for user {UserId}. Legacy updated but Identity failed.",
                        request.UserId);
                    // Continue - Legacy is updated, Identity sync failed (log warning)
                }
            }
            // Note: No need to reactivate - login will migrate Legacy user automatically if needed

            _logger.LogInformation(
                "Identity sync completed for user {UserId}. Email: {EmailChanged}, Password: {PasswordChanged}, Deactivated: {Deactivated}",
                request.UserId,
                emailChanged,
                passwordChanged,
                !request.Activo);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error syncing to Identity for user {UserId}. Legacy update succeeded but Identity sync failed.",
                request.UserId);
            // Continue - don't fail the whole operation if Identity sync fails
        }

        // ================================================================================
        // PASO 4: GUARDAR CAMBIOS (LEGACY)
        // ================================================================================
        // No necesitamos llamar UpdateAsync, el DbContext detecta los cambios automáticamente
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Credencial actualizada exitosamente. UserId: {UserId}, Email: {Email}, Activo: {Activo}",
            request.UserId,
            request.Email,
            request.Activo);

        return true;
    }
}
