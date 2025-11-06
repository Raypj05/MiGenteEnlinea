using System.Net.Http.Headers;

namespace MiGenteEnLinea.IntegrationTests.Infrastructure;

/// <summary>
/// Extension methods para agregar autenticación JWT a HttpClient de forma sencilla.
/// 
/// USAGE:
/// // Método 1: Con token existente
/// _client.WithJwtAuth(token);
/// 
/// // Método 2: Generar token Empleador
/// _client.AsEmpleador(userId: "emp-001", email: "empleador@test.com");
/// 
/// // Método 3: Generar token Contratista
/// _client.AsContratista(userId: "cont-001", email: "contratista@test.com");
/// </summary>
public static class HttpClientAuthExtensions
{
    /// <summary>
    /// Agrega un token JWT al HttpClient
    /// </summary>
    public static HttpClient WithJwtAuth(this HttpClient client, string token)
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    /// <summary>
    /// Configura el HttpClient como un Empleador autenticado
    /// </summary>
    public static HttpClient AsEmpleador(
        this HttpClient client,
        string userId = "test-empleador-001",
        string email = "empleador@test.com",
        string nombre = "Test Empleador",
        int planId = 1)
    {
        var token = JwtTokenGenerator.GenerateEmpleadorToken(userId, email, nombre, planId);
        return client.WithJwtAuth(token);
    }

    /// <summary>
    /// Configura el HttpClient como un Contratista autenticado
    /// </summary>
    public static HttpClient AsContratista(
        this HttpClient client,
        string userId = "test-contratista-001",
        string email = "contratista@test.com",
        string nombre = "Test Contratista",
        int planId = 1)
    {
        var token = JwtTokenGenerator.GenerateContratistaToken(userId, email, nombre, planId);
        return client.WithJwtAuth(token);
    }

    /// <summary>
    /// Configura el HttpClient con un usuario sin plan activo
    /// </summary>
    public static HttpClient AsUserWithoutPlan(
        this HttpClient client,
        string userId = "test-user-noplan",
        string email = "noplan@test.com",
        string role = "Empleador")
    {
        var token = JwtTokenGenerator.GenerateExpiredPlanToken(userId, email, role);
        return client.WithJwtAuth(token);
    }

    /// <summary>
    /// Configura el HttpClient con un token expirado (para tests de autorización)
    /// </summary>
    public static HttpClient WithExpiredToken(
        this HttpClient client,
        string userId = "test-user-expired",
        string email = "expired@test.com",
        string role = "Empleador")
    {
        var token = JwtTokenGenerator.GenerateExpiredToken(userId, email, role);
        return client.WithJwtAuth(token);
    }

    /// <summary>
    /// Remueve la autenticación del HttpClient
    /// </summary>
    public static HttpClient WithoutAuth(this HttpClient client)
    {
        client.DefaultRequestHeaders.Authorization = null;
        return client;
    }

    /// <summary>
    /// Genera un token con claims personalizados
    /// </summary>
    public static HttpClient WithCustomAuth(
        this HttpClient client,
        string userId,
        string email,
        string role = "Empleador",
        int? planId = null,
        string? nombre = null,
        Dictionary<string, string>? additionalClaims = null)
    {
        var token = JwtTokenGenerator.GenerateToken(
            userId: userId,
            email: email,
            role: role,
            planId: planId,
            nombre: nombre,
            additionalClaims: additionalClaims
        );
        return client.WithJwtAuth(token);
    }
}
