using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MiGenteEnLinea.Application.Common.Interfaces;
using MiGenteEnLinea.Application.Features.Authentication.DTOs;
using MiGenteEnLinea.Infrastructure.Persistence.Contexts;

namespace MiGenteEnLinea.Infrastructure.Identity.Services;

public class IdentityService : IIdentityService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly IApplicationDbContext _context;
    private readonly ILogger<IdentityService> _logger;

    public IdentityService(
        UserManager<ApplicationUser> userManager,
        IJwtTokenService jwtTokenService,
        IApplicationDbContext context,
        ILogger<IdentityService> logger)
    {
        _userManager = userManager;
        _jwtTokenService = jwtTokenService;
        _context = context;
        _logger = logger;
    }

    public async Task<AuthenticationResultDto> LoginAsync(string email, string password, string ipAddress)
    {
        // ============================================================
        // STRATEGY: Identity-First + Legacy Fallback
        // ============================================================
        // 1. Try login with Identity (ASP.NET Core Identity - Modern)
        // 2. If not found, try legacy tables (Credenciales + Perfiles)
        // 3. If found in legacy, migrate automatically to Identity
        // 4. Return unified result
        // ============================================================

        // Step 1: Try Identity login first (modern system)
        var user = await _userManager.FindByEmailAsync(email);
        
        if (user != null)
        {
            // User exists in Identity - standard login flow
            return await LoginWithIdentityAsync(user, password, ipAddress);
        }

        // Step 2: User not in Identity, check legacy tables
        _logger.LogInformation("User not found in Identity, checking legacy tables for email: {Email}", email);
        
        // Query legacy Credenciales table (domain entity)
        // NOTA: No usar .Email.Value en LINQ - EF Core no puede traducir Value Objects
        // En su lugar, EF Core usa HasConversion() autom�ticamente
        var credencialesQuery = await _context.Credenciales.ToListAsync();
        var credencial = credencialesQuery.FirstOrDefault(c => c.Email.Value.Equals(email, StringComparison.OrdinalIgnoreCase));

        if (credencial == null)
        {
            _logger.LogWarning("Login failed: User not found in Identity or Legacy for email {Email}", email);
            throw new UnauthorizedAccessException("Credenciales inv�lidas");
        }

        // Step 3: Validate password against legacy hash (BCrypt)
        var passwordValid = BCrypt.Net.BCrypt.Verify(password, credencial.PasswordHash);
        
        if (!passwordValid)
        {
            _logger.LogWarning("Login failed: Invalid password for legacy user {UserId}", credencial.UserId);
            throw new UnauthorizedAccessException("Credenciales inv�lidas");
        }

        if (!credencial.Activo)
        {
            _logger.LogWarning("Login failed: Legacy account not active for user {UserId}", credencial.UserId);
            throw new UnauthorizedAccessException("La cuenta no est� activa. Por favor, verifica tu correo electr�nico.");
        }

        // Query legacy Perfiles table to get additional user info
        var perfil = await _context.Perfiles
            .FirstOrDefaultAsync(p => p.UserId == credencial.UserId);

        // Step 4: Migrate legacy user to Identity automatically
        _logger.LogInformation("Migrating legacy user {UserId} to Identity system", credencial.UserId);
        
        var migratedUser = await MigrateLegacyUserToIdentityAsync(credencial, perfil, password);
        
        // Step 5: Login with newly migrated Identity user
        return await LoginWithIdentityAsync(migratedUser, password, ipAddress);
    }

    /// <summary>
    /// Standard Identity login flow
    /// </summary>
    private async Task<AuthenticationResultDto> LoginWithIdentityAsync(ApplicationUser user, string password, string ipAddress)
    {
        var passwordValid = await _userManager.CheckPasswordAsync(user, password);
        
        if (!passwordValid)
        {
            _logger.LogWarning("Login failed: Invalid password for user {UserId}", user.Id);
            await _userManager.AccessFailedAsync(user);
            throw new UnauthorizedAccessException("Credenciales inv�lidas");
        }

        if (!user.EmailConfirmed)
        {
            _logger.LogWarning("Login failed: Account not confirmed for user {UserId}", user.Id);
            throw new UnauthorizedAccessException("La cuenta no est� activa. Por favor, verifica tu correo electr�nico.");
        }

        if (await _userManager.IsLockedOutAsync(user))
        {
            _logger.LogWarning("Login failed: Account is locked out for user {UserId}", user.Id);
            throw new UnauthorizedAccessException("La cuenta est� bloqueada debido a m�ltiples intentos fallidos.");
        }

        var roles = await _userManager.GetRolesAsync(user);

        var accessToken = _jwtTokenService.GenerateAccessToken(
            userId: user.Id,
            email: user.Email!,
            tipo: user.Tipo,
            nombreCompleto: user.NombreCompleto,
            planId: user.PlanID,
            roles: roles
        );

        var refreshTokenData = _jwtTokenService.GenerateRefreshToken(ipAddress);

        var refreshTokenEntity = new RefreshToken
        {
            UserId = user.Id,
            Token = refreshTokenData.Token,
            Expires = refreshTokenData.Expires,
            Created = DateTime.UtcNow,
            CreatedByIp = refreshTokenData.CreatedByIp
        };

        user.RefreshTokens.Add(refreshTokenEntity);
        user.UltimoLogin = DateTime.UtcNow;
        await _userManager.ResetAccessFailedCountAsync(user);
        await _userManager.UpdateAsync(user);

        _logger.LogInformation("Login successful for user {UserId} from IP {IpAddress}", user.Id, ipAddress);

        return new AuthenticationResultDto
        {
            AccessToken = accessToken,
            RefreshToken = refreshTokenData.Token,
            AccessTokenExpires = DateTime.UtcNow.AddMinutes(15),
            RefreshTokenExpires = refreshTokenData.Expires,
            User = new UserInfoDto
            {
                UserId = user.Id,
                Email = user.Email!,
                NombreCompleto = user.NombreCompleto,
                Tipo = user.Tipo,
                PlanId = user.PlanID,
                VencimientoPlan = user.VencimientoPlan,
                Roles = roles.ToList()
            }
        };
    }

    /// <summary>
    /// Migra un usuario legacy (Credenciales + Perfiles) a Identity (AspNetUsers)
    /// </summary>
    private async Task<ApplicationUser> MigrateLegacyUserToIdentityAsync(
        Domain.Entities.Authentication.Credencial credencial,
        Domain.Entities.Seguridad.Perfile? perfil,
        string plainTextPassword)
    {
        // Buscar suscripci�n activa para obtener PlanID y VencimientoPlan
        var suscripcion = await _context.Suscripciones
            .Where(s => s.UserId == credencial.UserId && !s.Cancelada)
            .OrderByDescending(s => s.FechaInicio)
            .FirstOrDefaultAsync();

        var newUser = new ApplicationUser
        {
            Id = credencial.UserId, // Mantener mismo ID para compatibilidad
            UserName = credencial.Email.Value,
            Email = credencial.Email.Value,
            EmailConfirmed = credencial.Activo,
            NombreCompleto = perfil != null ? $"{perfil.Nombre} {perfil.Apellido}" : credencial.Email.Value,
            Tipo = perfil?.Tipo.ToString() ?? "1", // Default to Empleador if not specified
            PlanID = suscripcion?.PlanId ?? 0,
            VencimientoPlan = suscripcion?.Vencimiento.ToDateTime(TimeOnly.MinValue),
            FechaCreacion = credencial.CreatedAt ?? DateTime.UtcNow,
            SecurityStamp = Guid.NewGuid().ToString()
        };

        // Crear usuario en Identity con el mismo password
        var result = await _userManager.CreateAsync(newUser, plainTextPassword);

        if (!result.Succeeded)
        {
            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            _logger.LogError("Failed to migrate legacy user {UserId} to Identity: {Errors}", credencial.UserId, errors);
            throw new InvalidOperationException($"Error al migrar usuario a Identity: {errors}");
        }

        _logger.LogInformation("Successfully migrated legacy user {UserId} to Identity", credencial.UserId);
        
        return newUser;
    }

    public async Task<AuthenticationResultDto> RefreshTokenAsync(string refreshToken, string ipAddress)
    {
        var tokenEntity = await _context.Set<RefreshToken>()
            .Include(rt => rt.User)
            .FirstOrDefaultAsync(rt => rt.Token == refreshToken);

        if (tokenEntity == null)
        {
            _logger.LogWarning("Refresh token not found: {Token}", refreshToken);
            throw new UnauthorizedAccessException("Refresh token inv�lido");
        }

        if (!tokenEntity.IsActive)
        {
            _logger.LogWarning("Refresh token is not active: {Token}", refreshToken);
            throw new UnauthorizedAccessException("Refresh token expirado o revocado");
        }

        var user = tokenEntity.User;
        var roles = await _userManager.GetRolesAsync(user);

        var accessToken = _jwtTokenService.GenerateAccessToken(
            userId: user.Id,
            email: user.Email!,
            tipo: user.Tipo,
            nombreCompleto: user.NombreCompleto,
            planId: user.PlanID,
            roles: roles
        );

        var newRefreshTokenData = _jwtTokenService.GenerateRefreshToken(ipAddress);

        tokenEntity.Revoked = DateTime.UtcNow;
        tokenEntity.RevokedByIp = ipAddress;
        tokenEntity.ReplacedByToken = newRefreshTokenData.Token;
        tokenEntity.ReasonRevoked = "Replaced by new token";

        var newRefreshTokenEntity = new RefreshToken
        {
            UserId = user.Id,
            Token = newRefreshTokenData.Token,
            Expires = newRefreshTokenData.Expires,
            Created = DateTime.UtcNow,
            CreatedByIp = newRefreshTokenData.CreatedByIp
        };

        user.RefreshTokens.Add(newRefreshTokenEntity);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Refresh token rotated for user {UserId}", user.Id);

        return new AuthenticationResultDto
        {
            AccessToken = accessToken,
            RefreshToken = newRefreshTokenData.Token,
            AccessTokenExpires = DateTime.UtcNow.AddMinutes(15),
            RefreshTokenExpires = newRefreshTokenData.Expires,
            User = new UserInfoDto
            {
                UserId = user.Id,
                Email = user.Email!,
                NombreCompleto = user.NombreCompleto,
                Tipo = user.Tipo,
                PlanId = user.PlanID,
                VencimientoPlan = user.VencimientoPlan,
                Roles = roles.ToList()
            }
        };
    }

    public async Task RevokeTokenAsync(string refreshToken, string ipAddress, string? reason = null)
    {
        var tokenEntity = await _context.Set<RefreshToken>()
            .FirstOrDefaultAsync(rt => rt.Token == refreshToken);

        if (tokenEntity == null)
        {
            _logger.LogWarning("Refresh token not found for revocation: {Token}", refreshToken);
            throw new UnauthorizedAccessException("Refresh token inv�lido");
        }

        if (!tokenEntity.IsActive)
        {
            _logger.LogWarning("Refresh token is already inactive: {Token}", refreshToken);
            return;
        }

        tokenEntity.Revoked = DateTime.UtcNow;
        tokenEntity.RevokedByIp = ipAddress;
        tokenEntity.ReasonRevoked = reason ?? "User logout";

        await _context.SaveChangesAsync();

        _logger.LogInformation("Refresh token revoked for user {UserId}", tokenEntity.UserId);
    }

    public async Task<string> RegisterAsync(string email, string password, string nombreCompleto, string tipo)
    {
        var existingUser = await _userManager.FindByEmailAsync(email);
        if (existingUser != null)
        {
            throw new InvalidOperationException("El email ya est� registrado");
        }

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = false,
            NombreCompleto = nombreCompleto,
            Tipo = tipo,
            PlanID = 0,
            FechaCreacion = DateTime.UtcNow
        };

        var result = await _userManager.CreateAsync(user, password);

        if (!result.Succeeded)
        {
            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            _logger.LogError("User registration failed: {Errors}", errors);
            throw new InvalidOperationException($"Error al registrar usuario: {errors}");
        }

        _logger.LogInformation("User registered successfully: {Email}", email);

        return user.Id;
    }

    public async Task<bool> UserExistsAsync(string email)
    {
        var user = await _userManager.FindByEmailAsync(email);
        return user != null;
    }

    public async Task<bool> ConfirmEmailAsync(string userId, string token)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
        {
            return false;
        }

        var result = await _userManager.ConfirmEmailAsync(user, token);
        return result.Succeeded;
    }

    public async Task<bool> ActivateAccountAsync(string userId, string email)
    {
        // Legacy compatibility: Activar cuenta sin token, solo validando userId + email
        var user = await _userManager.FindByIdAsync(userId);
        
        if (user == null)
        {
            _logger.LogWarning("ActivateAccount failed: User not found with ID {UserId}", userId);
            return false;
        }

        // Validar que el email coincida (case-insensitive)
        if (!user.Email!.Equals(email, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning(
                "ActivateAccount failed: Email mismatch. UserId: {UserId}, Expected: {ExpectedEmail}, Received: {ReceivedEmail}",
                userId, user.Email, email);
            return false;
        }

        // Verificar si ya est� activado
        if (user.EmailConfirmed)
        {
            _logger.LogInformation("ActivateAccount: Account already confirmed. UserId: {UserId}", userId);
            return true; // Ya est� activo
        }

        // Activar cuenta (sin token - Legacy compatibility)
        user.EmailConfirmed = true;
        var result = await _userManager.UpdateAsync(user);

        if (result.Succeeded)
        {
            _logger.LogInformation("Account activated successfully. UserId: {UserId}, Email: {Email}", userId, email);
        }
        else
        {
            _logger.LogError(
                "Failed to activate account. UserId: {UserId}, Errors: {Errors}",
                userId, string.Join(", ", result.Errors.Select(e => e.Description)));
        }

        return result.Succeeded;
    }

    public async Task<bool> ChangePasswordAsync(string userId, string currentPassword, string newPassword)
    {
        var user = await _userManager.FindByIdAsync(userId);
        
        if (user == null)
        {
            _logger.LogWarning("ChangePassword failed: User not found with ID {UserId}", userId);
            return false;
        }

        var result = await _userManager.ChangePasswordAsync(user, currentPassword, newPassword);

        if (result.Succeeded)
        {
            _logger.LogInformation("Password changed successfully for user {UserId}", userId);
        }
        else
        {
            _logger.LogWarning(
                "Failed to change password for user {UserId}. Errors: {Errors}",
                userId, string.Join(", ", result.Errors.Select(e => e.Description)));
        }

        return result.Succeeded;
    }

    public async Task<string> GeneratePasswordResetTokenAsync(string email)
    {
        var user = await _userManager.FindByEmailAsync(email);
        if (user == null)
        {
            throw new InvalidOperationException("Usuario no encontrado");
        }

        return await _userManager.GeneratePasswordResetTokenAsync(user);
    }

    public async Task<bool> ResetPasswordAsync(string email, string token, string newPassword)
    {
        var user = await _userManager.FindByEmailAsync(email);
        if (user == null)
        {
            return false;
        }

        var result = await _userManager.ResetPasswordAsync(user, token, newPassword);
        return result.Succeeded;
    }

    // ========================================
    // M�TODOS ADICIONALES PARA SINCRONIZACI�N IDENTITY + LEGACY
    // GAP-001, GAP-014, GAP-015
    // ========================================

    public async Task<bool> LockoutUserAsync(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);
        
        if (user == null)
        {
            _logger.LogWarning("LockoutUser failed: User not found with ID {UserId}", userId);
            return false;
        }

        // Soft delete: Permanent lockout
        user.LockoutEnabled = true;
        user.LockoutEnd = DateTimeOffset.MaxValue; // Lock permanently

        var result = await _userManager.UpdateAsync(user);

        if (result.Succeeded)
        {
            _logger.LogInformation("User locked out permanently (soft delete). UserId: {UserId}", userId);
        }
        else
        {
            _logger.LogWarning(
                "Failed to lockout user {UserId}. Errors: {Errors}",
                userId, string.Join(", ", result.Errors.Select(e => e.Description)));
        }

        return result.Succeeded;
    }

    public async Task<bool> DeactivateUserAsync(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);
        
        if (user == null)
        {
            _logger.LogWarning("DeactivateUser failed: User not found with ID {UserId}", userId);
            return false;
        }

        // Soft delete: Mark as inactive by:
        // 1. Disabling email confirmation (prevents login)
        // 2. Permanent lockout (double security)
        user.EmailConfirmed = false;
        user.LockoutEnabled = true;
        user.LockoutEnd = DateTimeOffset.MaxValue;

        var result = await _userManager.UpdateAsync(user);

        if (!result.Succeeded)
        {
            _logger.LogWarning(
                "Failed to deactivate user {UserId}. Errors: {Errors}",
                userId, string.Join(", ", result.Errors.Select(e => e.Description)));
            return false;
        }

        // Force immediate database commit
        await _context.SaveChangesAsync();
        
        _logger.LogInformation("User deactivated (soft delete). UserId: {UserId}", userId);
        return true;
    }

    public async Task<bool> ChangePasswordByIdAsync(string userId, string newPassword)
    {
        var user = await _userManager.FindByIdAsync(userId);
        
        if (user == null)
        {
            _logger.LogWarning("ChangePasswordById failed: User not found with ID {UserId}", userId);
            return false;
        }

        // Remove old password and add new one (administrative change, no validation)
        await _userManager.RemovePasswordAsync(user);
        var result = await _userManager.AddPasswordAsync(user, newPassword);

        if (!result.Succeeded)
        {
            _logger.LogWarning(
                "Failed to change password by ID for user {UserId}. Errors: {Errors}",
                userId, string.Join(", ", result.Errors.Select(e => e.Description)));
            return false;
        }

        // CRITICAL: Update security stamp to invalidate old tokens
        await _userManager.UpdateSecurityStampAsync(user);
        
        // CRITICAL: Force SaveChanges to ensure password is immediately usable
        // Without this, subsequent login attempts may fail with "Invalid password"
        await _context.SaveChangesAsync();
        
        _logger.LogInformation("Password changed by ID (administrative) for user {UserId}", userId);
        
        return true;
    }

    public async Task<bool> UpdateUserEmailAsync(string userId, string newEmail)
    {
        var user = await _userManager.FindByIdAsync(userId);
        
        if (user == null)
        {
            _logger.LogWarning("UpdateUserEmail failed: User not found with ID {UserId}", userId);
            return false;
        }

        // Check if new email is already taken
        var existingUser = await _userManager.FindByEmailAsync(newEmail);
        if (existingUser != null && existingUser.Id != userId)
        {
            _logger.LogWarning("UpdateUserEmail failed: Email {Email} already in use", newEmail);
            return false;
        }

        // Update email (UserName = Email in our system)
        user.Email = newEmail;
        user.UserName = newEmail;
        user.EmailConfirmed = false; // Require re-confirmation for new email

        var result = await _userManager.UpdateAsync(user);

        if (result.Succeeded)
        {
            _logger.LogInformation("Email updated for user {UserId} to {NewEmail}", userId, newEmail);
        }
        else
        {
            _logger.LogWarning(
                "Failed to update email for user {UserId}. Errors: {Errors}",
                userId, string.Join(", ", result.Errors.Select(e => e.Description)));
        }

        return result.Succeeded;
    }

    public async Task<(string Email, bool IsActive)?> GetUserByIdAsync(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);
        
        if (user == null)
        {
            return null;
        }

        var isActive = user.EmailConfirmed && !await _userManager.IsLockedOutAsync(user);

        return (user.Email!, isActive);
    }
}

