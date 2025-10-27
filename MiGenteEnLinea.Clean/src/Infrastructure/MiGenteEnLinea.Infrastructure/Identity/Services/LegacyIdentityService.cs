using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MiGenteEnLinea.Application.Common.Interfaces;
using MiGenteEnLinea.Application.Features.Authentication.DTOs;
using MiGenteEnLinea.Domain.Entities.Authentication;
using MiGenteEnLinea.Domain.Interfaces.Repositories;

namespace MiGenteEnLinea.Infrastructure.Identity.Services;

/// <summary>
/// Implementación del servicio de identidad usando el sistema Legacy
/// (tablas: Credenciales, Perfiles, Contratistas)
/// 
/// Este servicio reemplaza temporalmente IdentityService (ASP.NET Core Identity)
/// para mantener compatibilidad 100% con la base de datos Legacy durante la migración.
/// </summary>
public class LegacyIdentityService : IIdentityService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly ILogger<LegacyIdentityService> _logger;

    public LegacyIdentityService(
        IUnitOfWork unitOfWork,
        IPasswordHasher passwordHasher,
        IJwtTokenService jwtTokenService,
        ILogger<LegacyIdentityService> logger)
    {
        _unitOfWork = unitOfWork;
        _passwordHasher = passwordHasher;
        _jwtTokenService = jwtTokenService;
        _logger = logger;
    }

    public async Task<AuthenticationResultDto> LoginAsync(string email, string password, string ipAddress)
    {
        _logger.LogInformation("Processing login request for email: {Email}", email);

        // 1. Buscar usuario por email en tabla Credenciales (Legacy)
        var credencial = await _unitOfWork.Credenciales.GetByEmailAsync(email);
        
        if (credencial == null)
        {
            _logger.LogWarning("Login failed: User not found with email {Email}", email);
            throw new UnauthorizedAccessException("Credenciales inválidas");
        }

        // 2. Verificar contraseña usando BCrypt
        var passwordValid = _passwordHasher.VerifyPassword(password, credencial.PasswordHash);
        
        if (!passwordValid)
        {
            _logger.LogWarning("Login failed: Invalid password for user {UserId}", credencial.UserId);
            throw new UnauthorizedAccessException("Credenciales inválidas");
        }

        // 3. Verificar si la cuenta está activa (Legacy: Credenciales.Activo)
        if (!credencial.Activo)
        {
            _logger.LogWarning("Login failed: Inactive account for user {UserId}", credencial.UserId);
            throw new UnauthorizedAccessException("Cuenta inactiva. Por favor, activa tu cuenta desde el enlace enviado a tu correo electrónico.");
        }

        // 4. Obtener perfil del usuario (Legacy: tabla Perfiles)
        var perfil = await _unitOfWork.Perfiles.GetByUserIdAsync(credencial.UserId);
        
        if (perfil == null)
        {
            _logger.LogError("Profile not found for user {UserId}", credencial.UserId);
            throw new InvalidOperationException("Perfil de usuario no encontrado");
        }

        // 5. Determinar roles basados en el tipo de usuario (Legacy: Perfile.Tipo)
        var roles = new List<string>();
        if (perfil.Tipo == 1)
        {
            roles.Add("Empleador");
        }
        else if (perfil.Tipo == 2)
        {
            roles.Add("Contratista");
        }

        // 6. Generar access token JWT
        var accessToken = _jwtTokenService.GenerateAccessToken(
            userId: credencial.UserId,
            email: credencial.Email.Value,
            tipo: perfil.Tipo.ToString(),
            nombreCompleto: $"{perfil.Nombre} {perfil.Apellido}",
            planId: 0, // TODO: Agregar PlanId a entidad Perfile (campo existe en DB pero no en dominio)
            roles: roles
        );

        // 7. Generar refresh token
        var refreshTokenData = _jwtTokenService.GenerateRefreshToken(ipAddress);

        // 8. Guardar refresh token en base de datos (Legacy: tabla RefreshTokens)
        // TODO: Implementar RefreshToken en IUnitOfWork cuando se migre la entidad
        // Por ahora, solo generamos el token sin persistir
        
        // 9. Actualizar último login (Legacy: Credencial.UltimoLogin)
        credencial.ActualizarUltimoLogin(DateTime.UtcNow);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Login successful for user {UserId} ({Email}) from IP {IpAddress}", 
            credencial.UserId, credencial.Email.Value, ipAddress);

        // 10. Retornar resultado
        return new AuthenticationResultDto
        {
            AccessToken = accessToken,
            RefreshToken = refreshTokenData.Token,
            AccessTokenExpires = DateTime.UtcNow.AddMinutes(15),
            RefreshTokenExpires = refreshTokenData.Expires,
            User = new UserInfoDto
            {
                UserId = credencial.UserId,
                Email = credencial.Email.Value,
                NombreCompleto = $"{perfil.Nombre} {perfil.Apellido}",
                Tipo = perfil.Tipo.ToString(),
                PlanId = 0, // TODO: Agregar PlanId a Perfile
                VencimientoPlan = null, // TODO: Agregar VencimientoPlan a Perfile
                Roles = roles
            }
        };
    }

    public async Task<AuthenticationResultDto> RefreshTokenAsync(string refreshToken, string ipAddress)
    {
        // TODO: Implementar refresh token cuando se migre RefreshToken entity a IUnitOfWork
        _logger.LogWarning("RefreshTokenAsync not implemented yet in LegacyIdentityService");
        throw new NotImplementedException("Refresh token no implementado aún en sistema Legacy");
    }

    public async Task RevokeTokenAsync(string refreshToken, string ipAddress, string? reason = null)
    {
        // TODO: Implementar revoke token cuando se migre RefreshToken entity a IUnitOfWork
        _logger.LogWarning("RevokeTokenAsync not implemented yet in LegacyIdentityService");
        throw new NotImplementedException("Revoke token no implementado aún en sistema Legacy");
    }

    public async Task<string> RegisterAsync(string email, string password, string nombreCompleto, string tipo)
    {
        // Este método NO se usa - el registro se hace a través de RegisterCommandHandler
        // que tiene lógica mucho más compleja (crear Perfile, Credencial, Contratista, etc.)
        _logger.LogWarning("RegisterAsync called but not implemented - use RegisterCommand instead");
        throw new NotImplementedException("Usar RegisterCommand en lugar de RegisterAsync");
    }

    public async Task<bool> UserExistsAsync(string email)
    {
        var credencial = await _unitOfWork.Credenciales.GetByEmailAsync(email);
        return credencial != null;
    }

    public async Task<bool> ConfirmEmailAsync(string userId, string token)
    {
        // TODO: Implementar confirmación de email usando tabla Credenciales
        _logger.LogWarning("ConfirmEmailAsync not implemented yet in LegacyIdentityService");
        throw new NotImplementedException("Confirmación de email no implementada aún");
    }

    public async Task<bool> ActivateAccountAsync(string userId, string email)
    {
        // Activar cuenta Legacy: Credencial.Activo = true
        var credencial = await _unitOfWork.Credenciales.GetByUserIdAsync(userId);
        
        if (credencial == null)
        {
            _logger.LogWarning("ActivateAccountAsync: Credencial not found for UserId {UserId}", userId);
            return false;
        }

        // Validar email
        if (!credencial.Email.Value.Equals(email, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning(
                "ActivateAccountAsync: Email mismatch. UserId: {UserId}, Expected: {ExpectedEmail}, Received: {ReceivedEmail}",
                userId, credencial.Email.Value, email);
            return false;
        }

        // Activar
        if (credencial.Activo)
        {
            _logger.LogInformation("ActivateAccountAsync: Account already active. UserId: {UserId}", userId);
            return true;
        }

        credencial.Activar();
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("ActivateAccountAsync: Account activated. UserId: {UserId}", userId);
        return true;
    }

    public async Task<bool> ChangePasswordAsync(string userId, string currentPassword, string newPassword)
    {
        // Legacy: Cambiar password en tabla Credenciales
        var credencial = await _unitOfWork.Credenciales.GetByUserIdAsync(userId);
        
        if (credencial == null)
        {
            _logger.LogWarning("ChangePasswordAsync: Credencial not found for UserId {UserId}", userId);
            return false;
        }

        // Verificar contraseña actual
        var passwordValid = _passwordHasher.VerifyPassword(currentPassword, credencial.PasswordHash);
        if (!passwordValid)
        {
            _logger.LogWarning("ChangePasswordAsync: Invalid current password for UserId {UserId}", userId);
            return false;
        }

        // Actualizar password
        var newPasswordHash = _passwordHasher.HashPassword(newPassword);
        credencial.ActualizarPasswordHash(newPasswordHash);
        
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("ChangePasswordAsync: Password changed for UserId {UserId}", userId);
        return true;
    }

    public async Task<string> GeneratePasswordResetTokenAsync(string email)
    {
        // TODO: Implementar generación de token de reset
        _logger.LogWarning("GeneratePasswordResetTokenAsync not implemented yet in LegacyIdentityService");
        throw new NotImplementedException("Reset password no implementado aún");
    }

    public async Task<bool> ResetPasswordAsync(string email, string token, string newPassword)
    {
        // TODO: Implementar reset de password
        _logger.LogWarning("ResetPasswordAsync not implemented yet in LegacyIdentityService");
        throw new NotImplementedException("Reset password no implementado aún");
    }

    // ========================================
    // MÉTODOS ADICIONALES PARA SINCRONIZACIÓN IDENTITY + LEGACY
    // Stubs - LegacyIdentityService is being deprecated in favor of IdentityService
    // ========================================

    public async Task<bool> LockoutUserAsync(string userId)
    {
        _logger.LogWarning("LockoutUserAsync called on LegacyIdentityService - use IdentityService instead");
        throw new NotImplementedException("Use IdentityService for Identity operations");
    }

    public async Task<bool> DeactivateUserAsync(string userId)
    {
        _logger.LogWarning("DeactivateUserAsync called on LegacyIdentityService - use IdentityService instead");
        throw new NotImplementedException("Use IdentityService for Identity operations");
    }

    public async Task<bool> ChangePasswordByIdAsync(string userId, string newPassword)
    {
        _logger.LogWarning("ChangePasswordByIdAsync called on LegacyIdentityService - use IdentityService instead");
        throw new NotImplementedException("Use IdentityService for Identity operations");
    }

    public async Task<bool> UpdateUserEmailAsync(string userId, string newEmail)
    {
        _logger.LogWarning("UpdateUserEmailAsync called on LegacyIdentityService - use IdentityService instead");
        throw new NotImplementedException("Use IdentityService for Identity operations");
    }

    public async Task<(string Email, bool IsActive)?> GetUserByIdAsync(string userId)
    {
        _logger.LogWarning("GetUserByIdAsync called on LegacyIdentityService - use IdentityService instead");
        throw new NotImplementedException("Use IdentityService for Identity operations");
    }
}
