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
        
        // Obtener DbContext para poder hacer assertions y queries directos cuando sea necesario
        var scope = factory.Services.CreateScope();
        DbContext = scope.ServiceProvider.GetRequiredService<MiGenteDbContext>();
        AppDbContext = DbContext; // MiGenteDbContext implementa IApplicationDbContext
        
        // ✅ NO SEED AQUÍ - TestWebApplicationFactory ya hizo cleanup + seed UNA SOLA VEZ
        // ✅ USUARIOS: Cada test crea sus propios usuarios usando CreateContratistaAsync() o CreateEmpleadorAsync()
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

        // ✅ La API devuelve camelCase por JsonNamingPolicy
        var loginResponse = await response.Content.ReadFromJsonAsync<JsonElement>();
        
        // Intentar ambas versiones para compatibilidad
        string? token = null;
        if (loginResponse.TryGetProperty("accessToken", out var accessTokenProperty))
        {
            token = accessTokenProperty.GetString();
        }
        else if (loginResponse.TryGetProperty("AccessToken", out var accessTokenPropertyPascal))
        {
            token = accessTokenPropertyPascal.GetString();
        }
        
        token.Should().NotBeNullOrEmpty("El login debe devolver un access token");
        
        SetAuthToken(token!);
        return token!;
    }

    /// <summary>
    /// Helper para registrar un usuario de prueba
    /// ✅ SIGNATURE: RegisterUserAsync(email, password, tipo, nombre, apellido)
    /// ⚠️ Genera email único automáticamente para evitar conflictos
    /// 📧 RETORNA: (userId, emailUsado) - el email puede ser diferente al proporcionado
    /// ✅ ACTIVA la cuenta automáticamente para permitir login inmediato
    /// </summary>
    protected async Task<(string UserId, string Email)> RegisterUserAsync(
        string email, 
        string password,
        string tipo, // "Empleador" o "Contratista"  
        string nombre, 
        string apellido,
        string? identificacion = null)
    {
        // ✅ FIX: Generar email único para evitar conflictos de emails duplicados en DB
        var uniqueSuffix = Guid.NewGuid().ToString("N").Substring(0, 8);
        var emailParts = email.Split('@');
        var emailUnico = $"{emailParts[0]}+{uniqueSuffix}@{emailParts[1]}";
        
        // ✅ FIX: RegisterCommand expects Tipo as int (1=Empleador, 2=Contratista) and Host property
        int tipoInt = tipo.Equals("Contratista", StringComparison.OrdinalIgnoreCase) ? 2 : 1;
        
        var registerRequest = new
        {
            email = emailUnico,
            password,
            nombre,
            apellido,
            tipo = tipoInt, // ✅ Changed from string to int
            host = "https://localhost:5015" // ✅ Added required Host property
            // ✅ Removed identificacion - not used by RegisterCommand
        };

        var response = await Client.PostAsJsonAsync("/api/auth/register", registerRequest);
        response.EnsureSuccessStatusCode();

        var registerResponse = await response.Content.ReadFromJsonAsync<JsonElement>();
        
        // ✅ FIX: RegisterResult retorna DOS IDs:
        // - "userId" (int) = Credencial.Id (Legacy)
        // - "identityUserId" (string GUID) = Identity UserId (PRIMARY KEY)
        // Los tests necesitan el identityUserId para autenticación
        string? userId = null;
        if (registerResponse.TryGetProperty("identityUserId", out var identityUserIdProperty))
        {
            userId = identityUserIdProperty.GetString();
        }
        else if (registerResponse.TryGetProperty("IdentityUserId", out var identityUserIdPropertyPascal))
        {
            userId = identityUserIdPropertyPascal.GetString();
        }
        
        userId.Should().NotBeNullOrEmpty("El registro debe devolver un identityUserId válido");
        
        // ✅ FIX: Activar la cuenta automáticamente para permitir login inmediato en tests
        var activateRequest = new { userId, email = emailUnico };
        var activateResponse = await Client.PostAsJsonAsync("/api/auth/activate", activateRequest);
        activateResponse.EnsureSuccessStatusCode();
        
        return (userId!, emailUnico);
    }

    /// <summary>
    /// Helper para crear un Contratista completo usando el API.
    /// 1. Registra usuario con tipo Contratista
    /// 2. Activa cuenta automáticamente
    /// 3. Hace login y obtiene token
    /// 4. Crea perfil de contratista usando POST /api/contratistas
    /// RETORNA: (userId, email, token, contratistaId)
    /// </summary>
    protected async Task<(string UserId, string Email, string Token, int ContratistaId)> CreateContratistaAsync(
        string? nombre = null,
        string? apellido = null,
        string? identificacion = null,
        string? titulo = null)
    {
        // ✅ GAP-010: RegisterCommand auto-creates Contratista profile
        // No need to POST /api/contratistas (endpoint doesn't exist)
        // Just register user and get the auto-created profile
        
        // PASO 1: Register user (auto-creates Contratista profile)
        var email = GenerateUniqueEmail("contratista");
        var password = "Test123!";
        var (userId, emailUsado) = await RegisterUserAsync(
            email, 
            password, 
            "Contratista", 
            nombre ?? "TestContratista",
            apellido ?? "Apellido",
            identificacion ?? GenerateRandomIdentification()
        );
        
        // PASO 2: Login to get token
        var token = await LoginAsync(emailUsado, password);
        
        // PASO 3: Get the auto-created Contratista profile
        var client = Client;
        client.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            
        var response = await client.GetAsync($"/api/contratistas/by-user/{userId}");
        response.EnsureSuccessStatusCode();
        
        var contratistaDto = await response.Content.ReadFromJsonAsync<JsonElement>();
        
        // Try both camelCase and PascalCase
        var hasId = contratistaDto.TryGetProperty("contratistaId", out var idProp);
        if (!hasId) hasId = contratistaDto.TryGetProperty("ContratistaId", out idProp);
        
        var contratistaId = hasId ? idProp.GetInt32() : throw new InvalidOperationException("ContratistaId not found in response");
        
        return (userId, emailUsado, token, contratistaId);
    }
    
    /// <summary>
    /// Helper para crear un Empleador completo usando el API.
    /// Similar a CreateContratistaAsync pero para Empleadores.
    /// RETORNA: (userId, email, token, empleadorId)
    /// </summary>
    protected async Task<(string UserId, string Email, string Token, int EmpleadorId)> CreateEmpleadorAsync(
        string? nombre = null,
        string? apellido = null,
        string? nombreEmpresa = null,
        string? rnc = null)
    {
        // PASO 1: Register user
        var email = GenerateUniqueEmail("empleador");
        var password = "Test123!";
        var (userId, emailUsado) = await RegisterUserAsync(
            email, 
            password, 
            "Empleador", 
            nombre ?? "TestEmpleador",
            apellido ?? "Apellido"
        );
        
        // PASO 2: Login to get token
        var token = await LoginAsync(emailUsado, password);
        
        // PASO 3: Create Empleador profile via API (with authentication)
        var authenticatedClient = Client.AsEmpleador(userId: userId);
        
        // CreateEmpleadorCommand solo requiere UserId (otros campos opcionales)
        var createRequest = new
        {
            userId = userId,
            habilidades = "Test habilidades",
            experiencia = "5 años",
            descripcion = $"Empleador de prueba: {nombre} {apellido}"
        };
        
        var response = await authenticatedClient.PostAsJsonAsync("/api/empleadores", createRequest);
        response.EnsureSuccessStatusCode();
        
        var result = await response.Content.ReadFromJsonAsync<JsonElement>();
        
        // Try both property name casings (API might return empleadorId or EmpleadorId)
        var hasId = result.TryGetProperty("empleadorId", out var idProp);
        if (!hasId) hasId = result.TryGetProperty("EmpleadorId", out idProp);
        
        var empleadorId = idProp.GetInt32();
        
        return (userId, emailUsado, token, empleadorId);
    }

    /// <summary>
    /// Helper para crear un Empleado completo usando el API.
    /// Requiere un empleadorUserId existente.
    /// RETORNA: empleadoId
    /// </summary>
    protected async Task<int> CreateEmpleadoAsync(
        string empleadorUserId,
        string? nombre = null,
        string? apellido = null,
        decimal? salario = null)
    {
        var client = Client.AsEmpleador(userId: empleadorUserId);
        
        var command = new
        {
            userId = empleadorUserId,
            identificacion = GenerateRandomIdentification(),
            nombre = nombre ?? "TestEmpleado",
            apellido = apellido ?? "Apellido",
            fechaInicio = DateTime.Now,
            posicion = "Empleado",
            salario = salario ?? 45000m,
            periodoPago = 3, // Mensual
            tss = true,
            telefono1 = "8091234567",
            direccion = "Calle Test #123",
            provincia = "Santo Domingo"
        };
        
        var response = await client.PostAsJsonAsync("/api/empleados", command);
        response.EnsureSuccessStatusCode();
        
        var result = await response.Content.ReadFromJsonAsync<JsonElement>();
        
        // Try both property name casings
        var hasId = result.TryGetProperty("empleadoId", out var idProp);
        if (!hasId) hasId = result.TryGetProperty("EmpleadoId", out idProp);
        
        return idProp.GetInt32();
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
    /// Genera un RNC aleatorio (para empleadores)
    /// </summary>
    protected string GenerateRandomRNC()
    {
        var random = new Random();
        // Formato: 1-XX-XXXXX-X (9 dígitos)
        return $"1{random.Next(10, 99)}{random.Next(10000, 99999)}{random.Next(0, 9)}";
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
