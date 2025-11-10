using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace MiGenteEnLinea.Web.Services;

/// <summary>
/// Implementation of IAuthService for JWT-based authentication
/// Manages cookie-based sessions with JWT claims
/// </summary>
public class AuthService : IAuthService
{
    private readonly IApiService _apiService;
    private readonly IHttpContextAccessor _contextAccessor;
    private readonly ILogger<AuthService> _logger;
    
    public AuthService(
        IApiService apiService,
        IHttpContextAccessor contextAccessor,
        ILogger<AuthService> logger)
    {
        _apiService = apiService;
        _contextAccessor = contextAccessor;
        _logger = logger;
    }
    
    public async Task<LoginResult> LoginAsync(string email, string password, bool rememberMe)
    {
        try
        {
            // Call legacy API service
            var response = await _apiService.LoginAsync(email, password);
            
            if (!response.Success || response.Data?.Token == null)
            {
                _logger.LogWarning("Login failed for email: {Email}", email);
                return new LoginResult(false, response.Message ?? "Credenciales inválidas", null);
            }
            
            var loginData = response.Data;
            
            // Parse JWT token to extract claims
            var handler = new JwtSecurityTokenHandler();
            var token = handler.ReadJwtToken(loginData.Token);
            var claims = token.Claims.ToList();
            
            // Add token claim for future API calls
            claims.Add(new System.Security.Claims.Claim("jwt_token", loginData.Token));
            claims.Add(new System.Security.Claims.Claim("refresh_token", loginData.RefreshToken ?? string.Empty));
            
            // Get role from claims (Empleador or Contratista)
            var role = claims.FirstOrDefault(c => c.Type == ClaimTypes.Role)?.Value;
            
            // Create authentication cookie with claims
            var claimsIdentity = new ClaimsIdentity(
                claims, CookieAuthenticationDefaults.AuthenticationScheme);
            
            var context = _contextAccessor.HttpContext!;
            await context.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(claimsIdentity),
                new AuthenticationProperties
                {
                    IsPersistent = rememberMe,
                    ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8)
                });
            
            _logger.LogInformation("User logged in successfully: {Email}, Role: {Role}", email, role);
            
            return new LoginResult(true, "Login exitoso", role);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during login for {Email}", email);
            return new LoginResult(false, "Error al iniciar sesión. Intente nuevamente.", null);
        }
    }
    
    public async Task<RegisterResult> RegisterAsync(AuthRegisterRequest request)
    {
        try
        {
            // Map to legacy RegisterRequest
            var legacyRequest = new RegisterRequest
            {
                TipoCuenta = request.TipoUsuario == "Empleador" ? 1 : 2,
                Nombre = request.Nombre,
                Apellido = request.Apellido,
                Email = request.Email,
                Telefono1 = "", // Not provided in AuthRegisterRequest
                Telefono2 = null
            };
            
            var response = await _apiService.RegisterAsync(legacyRequest);
            
            if (response.Success)
            {
                _logger.LogInformation("User registered successfully: {Email}, Tipo: {TipoUsuario}", 
                    request.Email, request.TipoUsuario);
                return new RegisterResult(true, response.Message ?? "Registro exitoso. Revise su correo para activar su cuenta.");
            }
            
            _logger.LogWarning("Registration failed for email: {Email}", request.Email);
            return new RegisterResult(false, response.Message ?? "Error al registrar usuario");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during registration for {Email}", request.Email);
            return new RegisterResult(false, "Error al registrar. Intente nuevamente.");
        }
    }
    
    public async Task LogoutAsync()
    {
        try
        {
            var context = _contextAccessor.HttpContext!;
            await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            _logger.LogInformation("User logged out");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during logout");
        }
    }
    
    public async Task<bool> RefreshTokenAsync()
    {
        // TODO: Implement refresh token logic when API endpoint is available
        // Legacy API doesn't have refresh token endpoint yet
        await Task.CompletedTask;
        _logger.LogWarning("RefreshTokenAsync not yet implemented");
        return false;
    }
    
    public string? GetCurrentUserId()
    {
        return _contextAccessor.HttpContext?.User
            .FindFirst(ClaimTypes.NameIdentifier)?.Value;
    }
    
    public string? GetCurrentUserName()
    {
        return _contextAccessor.HttpContext?.User
            .FindFirst(ClaimTypes.Name)?.Value;
    }
    
    public string? GetCurrentUserRole()
    {
        return _contextAccessor.HttpContext?.User
            .FindFirst(ClaimTypes.Role)?.Value;
    }
    
    public bool IsAuthenticated()
    {
        return _contextAccessor.HttpContext?.User?.Identity?.IsAuthenticated ?? false;
    }
}
