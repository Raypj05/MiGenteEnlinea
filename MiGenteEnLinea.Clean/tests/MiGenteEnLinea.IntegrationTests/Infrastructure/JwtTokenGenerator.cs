using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace MiGenteEnLinea.IntegrationTests.Infrastructure;

/// <summary>
/// Genera tokens JWT válidos para testing con claims personalizables.
/// 
/// USAGE:
/// var token = JwtTokenGenerator.GenerateToken(userId: "user-001", email: "test@example.com", role: "Empleador");
/// _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
/// </summary>
public static class JwtTokenGenerator
{
    private static string? _secretKey;
    private static string? _issuer;
    private static string? _audience;
    private static int _expirationMinutes;

    /// <summary>
    /// Inicializa el generador con la configuración del appsettings.Testing.json
    /// </summary>
    public static void Initialize(IConfiguration configuration)
    {
        _secretKey = configuration["Jwt:SecretKey"] 
            ?? throw new InvalidOperationException("Jwt:SecretKey not found in configuration");
        _issuer = configuration["Jwt:Issuer"] 
            ?? throw new InvalidOperationException("Jwt:Issuer not found in configuration");
        _audience = configuration["Jwt:Audience"] 
            ?? throw new InvalidOperationException("Jwt:Audience not found in configuration");
        _expirationMinutes = int.Parse(configuration["Jwt:ExpirationMinutes"] ?? "60");

        if (_secretKey.Length < 32)
        {
            throw new InvalidOperationException("Jwt:SecretKey must be at least 32 characters");
        }
    }

    /// <summary>
    /// Genera un token JWT con claims personalizados
    /// </summary>
    public static string GenerateToken(
        string userId,
        string email,
        string role = "Empleador",
        int? planId = null,
        string? nombre = null,
        Dictionary<string, string>? additionalClaims = null)
    {
        EnsureInitialized();

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, userId),
            new Claim(ClaimTypes.Name, nombre ?? email),
            new Claim(ClaimTypes.Email, email),
            new Claim(ClaimTypes.Role, role),
            new Claim(JwtRegisteredClaimNames.Sub, userId),
            new Claim(JwtRegisteredClaimNames.Email, email),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim(JwtRegisteredClaimNames.Iat, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString())
        };

        if (planId.HasValue)
        {
            claims.Add(new Claim("PlanID", planId.Value.ToString()));
        }

        // Agregar claims adicionales
        if (additionalClaims != null)
        {
            foreach (var claim in additionalClaims)
            {
                claims.Add(new Claim(claim.Key, claim.Value));
            }
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secretKey!));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _issuer,
            audience: _audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_expirationMinutes),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    /// <summary>
    /// Genera un token para un Empleador con plan activo
    /// </summary>
    public static string GenerateEmpleadorToken(
        string userId = "test-empleador-001",
        string email = "empleador@test.com",
        string nombre = "Test Empleador",
        int planId = 1)
    {
        return GenerateToken(
            userId: userId,
            email: email,
            role: "Empleador",
            planId: planId,
            nombre: nombre
        );
    }

    /// <summary>
    /// Genera un token para un Contratista con plan activo
    /// </summary>
    public static string GenerateContratistaToken(
        string userId = "test-contratista-001",
        string email = "contratista@test.com",
        string nombre = "Test Contratista",
        int planId = 1)
    {
        return GenerateToken(
            userId: userId,
            email: email,
            role: "Contratista",
            planId: planId,
            nombre: nombre
        );
    }

    /// <summary>
    /// Genera un token para un usuario sin plan (plan expirado)
    /// </summary>
    public static string GenerateExpiredPlanToken(
        string userId = "test-user-noplan",
        string email = "noplan@test.com",
        string role = "Empleador")
    {
        return GenerateToken(
            userId: userId,
            email: email,
            role: role,
            planId: null
        );
    }

    /// <summary>
    /// Genera un token expirado (para tests de autorización)
    /// </summary>
    public static string GenerateExpiredToken(
        string userId = "test-user-expired",
        string email = "expired@test.com",
        string role = "Empleador")
    {
        EnsureInitialized();

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, userId),
            new Claim(ClaimTypes.Email, email),
            new Claim(ClaimTypes.Role, role)
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secretKey!));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _issuer,
            audience: _audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(-10), // ⚠️ Expirado hace 10 minutos
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static void EnsureInitialized()
    {
        if (string.IsNullOrEmpty(_secretKey))
        {
            throw new InvalidOperationException(
                "JwtTokenGenerator not initialized. Call Initialize(configuration) first.");
        }
    }
}
