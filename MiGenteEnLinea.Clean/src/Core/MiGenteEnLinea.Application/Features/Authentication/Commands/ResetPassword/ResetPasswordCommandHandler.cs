using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MiGenteEnLinea.Application.Common.Interfaces;

namespace MiGenteEnLinea.Application.Features.Authentication.Commands.ResetPassword;

/// <summary>
/// Handler para resetear contraseña con token
/// REFACTORED: Identity-First approach - Identity is primary auth system, Legacy for business logic only
/// </summary>
public sealed class ResetPasswordCommandHandler : IRequestHandler<ResetPasswordCommand, bool>
{
    private readonly IApplicationDbContext _context;
    private readonly IIdentityService _identityService;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ILogger<ResetPasswordCommandHandler> _logger;

    public ResetPasswordCommandHandler(
        IApplicationDbContext context,
        IIdentityService identityService,
        IPasswordHasher passwordHasher,
        ILogger<ResetPasswordCommandHandler> logger)
    {
        _context = context;
        _identityService = identityService;
        _passwordHasher = passwordHasher;
        _logger = logger;
    }

    public async Task<bool> Handle(ResetPasswordCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("ResetPassword: Email={Email}", request.Email);

        // Buscar usuario por email
        var credencial = await _context.Credenciales
            .Where(c => c.Email == request.Email && c.Activo)
            .FirstOrDefaultAsync(cancellationToken);

        if (credencial == null)
        {
            _logger.LogWarning("ResetPassword: Email no encontrado o cuenta inactiva - {Email}", request.Email);
            return false;
        }

        // Buscar token válido más reciente para este usuario
        var resetToken = await _context.PasswordResetTokens
            .Where(t => t.UserId == credencial.UserId && 
                       t.Email == request.Email &&
                       t.Token == request.Token)
            .OrderByDescending(t => t.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (resetToken == null)
        {
            _logger.LogWarning("ResetPassword: Token no encontrado - Email={Email}", request.Email);
            return false;
        }

        // Validar token (no usado y no expirado)
        if (!resetToken.ValidateToken(request.Token))
        {
            _logger.LogWarning(
                "ResetPassword: Token inválido - TokenId={TokenId}, IsExpired={IsExpired}, IsUsed={IsUsed}",
                resetToken.Id, resetToken.IsExpired, resetToken.IsUsed);
            return false;
        }
        
        // ================================================================================
        // STEP 1: Update password in Identity (PRIMARY auth system)
        // ================================================================================
        var identitySuccess = await _identityService.ChangePasswordByIdAsync(
            credencial.UserId, 
            request.NewPassword);

        if (!identitySuccess)
        {
            _logger.LogError(
                "CRITICAL: Failed to reset password in Identity. Operation aborted. UserId: {UserId}, Email: {Email}",
                credencial.UserId,
                request.Email);
            return false;
        }

        _logger.LogInformation("Password reset in Identity successfully. UserId: {UserId}", credencial.UserId);

        // ================================================================================
        // STEP 2: Update password in Legacy Credenciales (BUSINESS LOGIC compatibility)
        // ================================================================================
        var newPasswordHash = _passwordHasher.HashPassword(request.NewPassword);
        credencial.ActualizarPasswordHash(newPasswordHash);

        // Marcar token como usado
        resetToken.MarkAsUsed();

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "ResetPassword: Password updated in Identity and Legacy. Email: {Email}, UserId: {UserId}",
            request.Email, 
            credencial.UserId);
        
        return true;
    }
}
