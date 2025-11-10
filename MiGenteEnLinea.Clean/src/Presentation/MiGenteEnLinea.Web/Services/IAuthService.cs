namespace MiGenteEnLinea.Web.Services;

/// <summary>
/// Service for authentication and JWT token management
/// </summary>
public interface IAuthService
{
    /// <summary>
    /// Authenticates user and creates cookie-based session with JWT claims
    /// </summary>
    Task<LoginResult> LoginAsync(string email, string password, bool rememberMe);
    
    /// <summary>
    /// Registers a new user (Empleador or Contratista)
    /// </summary>
    Task<RegisterResult> RegisterAsync(AuthRegisterRequest request);
    
    /// <summary>
    /// Logs out current user and clears session
    /// </summary>
    Task LogoutAsync();
    
    /// <summary>
    /// Refreshes the JWT token using refresh token
    /// </summary>
    Task<bool> RefreshTokenAsync();
    
    /// <summary>
    /// Gets the current authenticated user's ID from claims
    /// </summary>
    string? GetCurrentUserId();
    
    /// <summary>
    /// Gets the current authenticated user's name from claims
    /// </summary>
    string? GetCurrentUserName();
    
    /// <summary>
    /// Gets the current authenticated user's role from claims (Empleador/Contratista)
    /// </summary>
    string? GetCurrentUserRole();
    
    /// <summary>
    /// Checks if current user is authenticated
    /// </summary>
    bool IsAuthenticated();
}

/// <summary>
/// Result of login operation
/// </summary>
public record LoginResult(bool Success, string? Message, string? Role);

/// <summary>
/// Result of registration operation
/// </summary>
public record RegisterResult(bool Success, string? Message);

/// <summary>
/// Registration request data for authentication
/// </summary>
public record AuthRegisterRequest(
    string Email,
    string Password,
    string Nombre,
    string Apellido,
    string TipoUsuario); // "Empleador" or "Contratista"
