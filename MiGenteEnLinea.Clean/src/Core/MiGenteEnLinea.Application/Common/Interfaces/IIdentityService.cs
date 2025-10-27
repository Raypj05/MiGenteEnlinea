using MiGenteEnLinea.Application.Features.Authentication.DTOs;

namespace MiGenteEnLinea.Application.Common.Interfaces;

/// <summary>
/// Servicio de identidad y autenticación usando ASP.NET Core Identity
/// </summary>
/// <remarks>
/// Esta interfaz abstrae UserManager y permite a Application layer
/// trabajar con autenticación sin depender de Infrastructure
/// </remarks>
public interface IIdentityService
{
    /// <summary>
    /// Autentica un usuario con email y contraseña, genera tokens JWT
    /// </summary>
    /// <param name="email">Email del usuario</param>
    /// <param name="password">Contraseña</param>
    /// <param name="ipAddress">IP del cliente (para refresh token tracking)</param>
    /// <returns>Resultado de autenticación con tokens y datos del usuario</returns>
    Task<AuthenticationResultDto> LoginAsync(string email, string password, string ipAddress);

    /// <summary>
    /// Renueva un access token usando un refresh token válido
    /// </summary>
    /// <param name="refreshToken">Refresh token activo</param>
    /// <param name="ipAddress">IP del cliente</param>
    /// <returns>Nuevo access token y refresh token rotado</returns>
    Task<AuthenticationResultDto> RefreshTokenAsync(string refreshToken, string ipAddress);

    /// <summary>
    /// Revoca un refresh token (logout)
    /// </summary>
    /// <param name="refreshToken">Refresh token a revocar</param>
    /// <param name="ipAddress">IP del cliente que revoca</param>
    /// <param name="reason">Razón de revocación (ej: "User logout")</param>
    Task RevokeTokenAsync(string refreshToken, string ipAddress, string? reason = null);

    /// <summary>
    /// Registra un nuevo usuario en el sistema
    /// </summary>
    /// <param name="email">Email del usuario</param>
    /// <param name="password">Contraseña</param>
    /// <param name="nombreCompleto">Nombre completo</param>
    /// <param name="tipo">Tipo de usuario ("1" Empleador, "2" Contratista)</param>
    /// <returns>ID del usuario creado</returns>
    Task<string> RegisterAsync(string email, string password, string nombreCompleto, string tipo);

    /// <summary>
    /// Verifica si un usuario existe por email
    /// </summary>
    Task<bool> UserExistsAsync(string email);

    /// <summary>
    /// Confirma el email de un usuario (activación de cuenta) usando token
    /// </summary>
    Task<bool> ConfirmEmailAsync(string userId, string token);

    /// <summary>
    /// Activa cuenta de usuario sin token (Legacy compatibility)
    /// Solo valida que el userId y email coincidan, luego activa EmailConfirmed
    /// </summary>
    Task<bool> ActivateAccountAsync(string userId, string email);

    /// <summary>
    /// Cambia la contraseña de un usuario
    /// </summary>
    /// <param name="userId">ID del usuario</param>
    /// <param name="currentPassword">Contraseña actual</param>
    /// <param name="newPassword">Nueva contraseña</param>
    Task<bool> ChangePasswordAsync(string userId, string currentPassword, string newPassword);

    /// <summary>
    /// Genera token para reset de contraseña
    /// </summary>
    Task<string> GeneratePasswordResetTokenAsync(string email);

    /// <summary>
    /// Resetea la contraseña de un usuario
    /// </summary>
    Task<bool> ResetPasswordAsync(string email, string token, string newPassword);

    // ========================================
    // MÉTODOS ADICIONALES PARA SINCRONIZACIÓN IDENTITY + LEGACY
    // GAP-001, GAP-014, GAP-015
    // ========================================

    /// <summary>
    /// Desactiva un usuario bloqueándolo permanentemente (Soft Delete)
    /// Sets LockoutEnd to DateTimeOffset.MaxValue to permanently disable login
    /// </summary>
    /// <param name="userId">ID del usuario a desactivar</param>
    /// <returns>True si se desactivó exitosamente</returns>
    Task<bool> LockoutUserAsync(string userId);

    /// <summary>
    /// Desactiva un usuario completamente (Soft Delete)
    /// Alternative to LockoutUserAsync - marks user as inactive preventing all access
    /// This is the PRIMARY method for soft delete operations
    /// </summary>
    /// <param name="userId">ID del usuario a desactivar</param>
    /// <returns>True si se desactivó exitosamente</returns>
    Task<bool> DeactivateUserAsync(string userId);

    /// <summary>
    /// Cambia la contraseña de un usuario sin validar la contraseña anterior
    /// (Para cambios administrativos o reset después de token validado)
    /// </summary>
    /// <param name="userId">ID del usuario</param>
    /// <param name="newPassword">Nueva contraseña</param>
    /// <returns>True si se cambió exitosamente</returns>
    Task<bool> ChangePasswordByIdAsync(string userId, string newPassword);

    /// <summary>
    /// Actualiza el email de un usuario en Identity
    /// </summary>
    /// <param name="userId">ID del usuario</param>
    /// <param name="newEmail">Nuevo email</param>
    /// <returns>True si se actualizó exitosamente</returns>
    Task<bool> UpdateUserEmailAsync(string userId, string newEmail);

    /// <summary>
    /// Obtiene información básica de un usuario por ID
    /// </summary>
    /// <param name="userId">ID del usuario</param>
    /// <returns>Email y estado activo del usuario, o null si no existe</returns>
    Task<(string Email, bool IsActive)?> GetUserByIdAsync(string userId);
}
