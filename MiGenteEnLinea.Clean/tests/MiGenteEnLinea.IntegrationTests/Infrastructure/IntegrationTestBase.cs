using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MiGenteEnLinea.Application.Common.Interfaces;
using MiGenteEnLinea.Infrastructure.Persistence.Contexts;
using Xunit;

namespace MiGenteEnLinea.IntegrationTests.Infrastructure;

/// <summary>
/// Clase base para todos los tests de integración.
/// Proporciona HttpClient configurado y métodos auxiliares para autenticación y limpieza.
/// </summary>
public abstract class IntegrationTestBase : IClassFixture<TestWebApplicationFactory>, IDisposable
{
    protected readonly HttpClient Client;
    protected readonly TestWebApplicationFactory Factory;
    protected readonly MiGenteDbContext DbContext;
    protected readonly IApplicationDbContext AppDbContext; // Interfaz para acceder a DbSets
    
    // Token JWT para autenticación (se obtiene después de login)
    protected string? AccessToken { get; set; }

    protected IntegrationTestBase(TestWebApplicationFactory factory)
    {
        Factory = factory;
        Client = factory.CreateClient();
        
        // Configurar JSON options para serialización consistente
        Client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        
        // Obtener DbContext para poder hacer seed de datos y assertions
        var scope = factory.Services.CreateScope();
        DbContext = scope.ServiceProvider.GetRequiredService<MiGenteDbContext>();
        AppDbContext = DbContext; // MiGenteDbContext implementa IApplicationDbContext
        
        // Seedear datos de prueba automáticamente
        SeedTestData().GetAwaiter().GetResult();
    }
    
    /// <summary>
    /// Seedea datos de prueba en la base de datos SQL Server
    /// </summary>
    private async Task SeedTestData()
    {
        // Verificar si ya hay datos (para evitar duplicados entre tests)
        var hasData = await AppDbContext.Credenciales.AnyAsync();
        if (hasData)
        {
            return; // Ya hay datos seeded
        }
        
        // Ejecutar seeder
        await TestDataSeeder.SeedAllAsync(AppDbContext);
    }

    /// <summary>
    /// Configura el token JWT en los headers del HttpClient para requests autenticados
    /// </summary>
    protected void SetAuthToken(string token)
    {
        AccessToken = token;
        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    /// <summary>
    /// Remueve el token de autenticación (para tests de endpoints no autenticados)
    /// </summary>
    protected void ClearAuthToken()
    {
        AccessToken = null;
        Client.DefaultRequestHeaders.Authorization = null;
    }

    /// <summary>
    /// Helper para hacer login y obtener token JWT
    /// </summary>
    protected async Task<string> LoginAsync(string email, string password)
    {
        var loginRequest = new
        {
            email,
            password
        };

        var response = await Client.PostAsJsonAsync("/api/auth/login", loginRequest);
        response.EnsureSuccessStatusCode();

        var loginResponse = await response.Content.ReadFromJsonAsync<JsonElement>();
        var token = loginResponse.GetProperty("accessToken").GetString();
        
        token.Should().NotBeNullOrEmpty("El login debe devolver un access token");
        
        SetAuthToken(token!);
        return token!;
    }

    /// <summary>
    /// Helper para registrar un usuario de prueba
    /// </summary>
    protected async Task<int> RegisterUserAsync(
        string email, 
        string password, 
        string nombre, 
        string apellido,
        string tipo = "Empleador", // o "Contratista"
        string? identificacion = null)
    {
        var registerRequest = new
        {
            email,
            password,
            nombre,
            apellido,
            tipo,
            identificacion = identificacion ?? GenerateRandomIdentification()
        };

        var response = await Client.PostAsJsonAsync("/api/auth/register", registerRequest);
        response.EnsureSuccessStatusCode();

        var registerResponse = await response.Content.ReadFromJsonAsync<JsonElement>();
        var userId = registerResponse.GetProperty("userId").GetInt32();
        
        userId.Should().BeGreaterThan(0, "El registro debe devolver un userId válido");
        
        return userId;
    }

    /// <summary>
    /// Genera una identificación aleatoria (cédula dominicana simulada)
    /// </summary>
    protected string GenerateRandomIdentification()
    {
        var random = new Random();
        // Formato: XXX-XXXXXXX-X (11 dígitos)
        return $"{random.Next(100, 999)}{random.Next(1000000, 9999999)}{random.Next(0, 9)}";
    }

    /// <summary>
    /// Genera un email único para pruebas
    /// </summary>
    protected string GenerateUniqueEmail(string prefix = "test")
    {
        return $"{prefix}_{Guid.NewGuid():N}@test.com";
    }
    
    /// <summary>
    /// Limpia el ChangeTracker de EF Core para forzar queries frescas desde la base de datos.
    /// Útil cuando el test necesita verificar cambios hechos por HTTP requests.
    /// </summary>
    protected void ClearChangeTracker()
    {
        DbContext.ChangeTracker.Clear();
    }

    /// <summary>
    /// Limpia la base de datos entre tests (opcional, InMemory se recrea automáticamente)
    /// </summary>
    protected async Task CleanupDatabaseAsync()
    {
        // Para InMemory Database, simplemente recreamos
        await DbContext.Database.EnsureDeletedAsync();
        await DbContext.Database.EnsureCreatedAsync();
    }

    /// <summary>
    /// Assertions helper para verificar response status codes
    /// </summary>
    protected void AssertSuccessStatusCode(HttpResponseMessage response, string because = "")
    {
        response.IsSuccessStatusCode.Should().BeTrue(
            $"Expected success status code but got {response.StatusCode}. {because}");
    }

    /// <summary>
    /// Assertions helper para verificar que la response es 400 Bad Request
    /// </summary>
    protected void AssertBadRequest(HttpResponseMessage response)
    {
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.BadRequest);
    }

    /// <summary>
    /// Assertions helper para verificar que la response es 401 Unauthorized
    /// </summary>
    protected void AssertUnauthorized(HttpResponseMessage response)
    {
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// Assertions helper para verificar que la response es 404 Not Found
    /// </summary>
    protected void AssertNotFound(HttpResponseMessage response)
    {
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.NotFound);
    }

    public virtual void Dispose()
    {
        Client?.Dispose();
        DbContext?.Dispose();
    }
}
