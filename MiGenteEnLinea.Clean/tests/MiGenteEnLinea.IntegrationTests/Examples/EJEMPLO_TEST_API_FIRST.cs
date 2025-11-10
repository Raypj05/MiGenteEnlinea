using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using MiGenteEnLinea.IntegrationTests.Infrastructure;
using Xunit;

namespace MiGenteEnLinea.IntegrationTests.Examples;

/// <summary>
/// 🎯 EJEMPLO: Cómo escribir tests usando el enfoque API-First
/// 
/// PRINCIPIOS:
/// 1. ✅ Usa endpoints reales del API (POST /api/contratistas, GET /api/contratistas/{id}, etc.)
/// 2. ✅ No uses entidades Legacy directamente (Credenciale, Perfile, Ofertante)
/// 3. ✅ Usa helpers de IntegrationTestBase: CreateContratistaAsync(), CreateEmpleadorAsync()
/// 4. ✅ La base de datos se limpia UNA VEZ al inicio (no en cada test)
/// 5. ✅ Cada test crea sus propios datos usando el API
/// 6. ✅ Tests independientes - no dependen de orden de ejecución
/// 
/// ESTRUCTURA RECOMENDADA:
/// - Arrange: Crear datos usando API helpers (CreateContratistaAsync, etc.)
/// - Act: Llamar endpoint que quieres probar
/// - Assert: Verificar respuesta HTTP + datos devueltos
/// </summary>
[Collection("IntegrationTests")]
public class ContratistasControllerRealApiTests : IntegrationTestBase
{
    public ContratistasControllerRealApiTests(TestWebApplicationFactory factory) : base(factory)
    {
    }

    /// <summary>
    /// TEST 1: Crear un contratista completo usando solo el API
    /// ENDPOINT: POST /api/contratistas
    /// </summary>
    [Fact]
    public async Task CreateContratista_ConDatosValidos_DebeCrearExitosamente()
    {
        // Arrange - Crear contratista usando helper (que usa API internamente)
        var (userId, email, token, contratistaId) = await CreateContratistaAsync(
            nombre: "Juan",
            apellido: "Pérez",
            titulo: "Plomero Profesional"
        );

        // Act - Obtener el contratista creado
        var response = await Client.GetAsync($"/api/contratistas/{contratistaId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var contratista = await response.Content.ReadFromJsonAsync<dynamic>();
        contratista.Should().NotBeNull();
        
        // Verificar datos
        string nombre = contratista.GetProperty("nombre").GetString();
        string apellido = contratista.GetProperty("apellido").GetString();
        string titulo = contratista.GetProperty("titulo").GetString();
        
        nombre.Should().Be("Juan");
        apellido.Should().Be("Pérez");
        titulo.Should().Be("Plomero Profesional");
    }

    /// <summary>
    /// TEST 2: Actualizar contratista usando el API
    /// ENDPOINT: PUT /api/contratistas/{id}
    /// </summary>
    [Fact]
    public async Task UpdateContratista_ConDatosValidos_DebeActualizarExitosamente()
    {
        // Arrange - Crear contratista
        var (userId, email, token, contratistaId) = await CreateContratistaAsync(
            nombre: "Pedro",
            apellido: "González"
        );

        // Act - Actualizar datos
        var updateRequest = new
        {
            contratistaId,
            nombre = "Pedro Actualizado",
            apellido = "González Modificado",
            titulo = "Electricista Certificado",
            presentacion = "Nueva presentación",
            telefono1 = "8095551234",
            activo = true
        };

        var updateResponse = await Client.PutAsJsonAsync($"/api/contratistas/{contratistaId}", updateRequest);

        // Assert
        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        
        // Verificar que los cambios se aplicaron
        var getResponse = await Client.GetAsync($"/api/contratistas/{contratistaId}");
        var contratista = await getResponse.Content.ReadFromJsonAsync<dynamic>();
        
        string nombreActualizado = contratista.GetProperty("nombre").GetString();
        nombreActualizado.Should().Be("Pedro Actualizado");
    }

    /// <summary>
    /// TEST 3: Buscar contratistas por criterios
    /// ENDPOINT: GET /api/contratistas/search?nombre={nombre}
    /// </summary>
    [Fact]
    public async Task SearchContratistas_PorNombre_DebeEncontrarCoincidencias()
    {
        // Arrange - Crear varios contratistas con nombres únicos
        var uniqueName = $"BuscaMe_{Guid.NewGuid():N}";
        
        await CreateContratistaAsync(nombre: uniqueName, apellido: "Primero");
        await CreateContratistaAsync(nombre: uniqueName, apellido: "Segundo");
        await CreateContratistaAsync(nombre: "OtroNombre", apellido: "Tercero");

        // Act - Buscar por nombre
        var response = await Client.GetAsync($"/api/contratistas/search?nombre={uniqueName}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var results = await response.Content.ReadFromJsonAsync<List<dynamic>>();
        results.Should().NotBeNull();
        results.Should().HaveCountGreaterOrEqualTo(2, "Debe encontrar al menos los 2 contratistas con ese nombre");
    }

    /// <summary>
    /// TEST 4: Agregar servicio a contratista
    /// ENDPOINT: POST /api/contratistas/{id}/servicios
    /// </summary>
    [Fact]
    public async Task AddServicio_ConDatosValidos_DebeAgregarExitosamente()
    {
        // Arrange - Crear contratista
        var (userId, email, token, contratistaId) = await CreateContratistaAsync();

        // Act - Agregar servicio
        var addServicioRequest = new
        {
            contratistaId,
            servicioId = 1, // Servicio de referencia (seed data)
            detalleServicio = "Reparación de tuberías y grifos"
        };

        var response = await Client.PostAsJsonAsync($"/api/contratistas/{contratistaId}/servicios", addServicioRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        // Verificar que el servicio se agregó
        var getServiciosResponse = await Client.GetAsync($"/api/contratistas/{contratistaId}/servicios");
        var servicios = await getServiciosResponse.Content.ReadFromJsonAsync<List<dynamic>>();
        
        servicios.Should().NotBeNull();
        servicios.Should().HaveCount(1);
    }

    /// <summary>
    /// TEST 5: Desactivar perfil de contratista
    /// ENDPOINT: PUT /api/contratistas/{id}/desactivar
    /// </summary>
    [Fact]
    public async Task DesactivarContratista_DebeMarcarComoInactivo()
    {
        // Arrange - Crear contratista activo
        var (userId, email, token, contratistaId) = await CreateContratistaAsync();

        // Act - Desactivar
        var response = await Client.PutAsync($"/api/contratistas/{contratistaId}/desactivar", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        // Verificar que está inactivo
        var getResponse = await Client.GetAsync($"/api/contratistas/{contratistaId}");
        var contratista = await getResponse.Content.ReadFromJsonAsync<dynamic>();
        
        bool activo = contratista.GetProperty("activo").GetBoolean();
        activo.Should().BeFalse("El contratista debe estar marcado como inactivo");
    }
}

/// <summary>
/// 🎯 EJEMPLO 2: Tests de Empleadores
/// </summary>
[Collection("IntegrationTests")]
public class EmpleadoresControllerRealApiTests : IntegrationTestBase
{
    public EmpleadoresControllerRealApiTests(TestWebApplicationFactory factory) : base(factory)
    {
    }

    /// <summary>
    /// TEST: Crear empleador completo
    /// ENDPOINT: POST /api/empleadores
    /// </summary>
    [Fact]
    public async Task CreateEmpleador_ConDatosValidos_DebeCrearExitosamente()
    {
        // Arrange - Crear empleador usando helper
        var (userId, email, token, empleadorId) = await CreateEmpleadorAsync(
            nombre: "Carlos",
            apellido: "Rodríguez",
            nombreEmpresa: "Empresa Test SRL",
            rnc: "123456789"
        );

        // Act - Obtener empleador creado
        var response = await Client.GetAsync($"/api/empleadores/{empleadorId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var empleador = await response.Content.ReadFromJsonAsync<dynamic>();
        
        string nombreEmpresa = empleador.GetProperty("nombreEmpresa").GetString();
        nombreEmpresa.Should().Be("Empresa Test SRL");
    }
}

/// <summary>
/// 🎯 EJEMPLO 3: Tests de Autenticación
/// </summary>
[Collection("IntegrationTests")]
public class AuthenticationRealApiTests : IntegrationTestBase
{
    public AuthenticationRealApiTests(TestWebApplicationFactory factory) : base(factory)
    {
    }

    /// <summary>
    /// TEST: Registro + Login + Perfil completo
    /// ENDPOINTS: POST /api/auth/register → POST /api/auth/login → GET /api/auth/perfil
    /// </summary>
    [Fact]
    public async Task RegistroLoginPerfil_FlujoCCompleto_DebeFuncionar()
    {
        // Arrange - Email y password únicos
        var email = GenerateUniqueEmail("flow");
        var password = "Test123!";

        // PASO 1: Register
        var (userId, emailUsado) = await RegisterUserAsync(
            email, 
            password, 
            "Contratista", 
            "Flow", 
            "Test"
        );

        userId.Should().NotBeNullOrEmpty();

        // PASO 2: Login
        var token = await LoginAsync(emailUsado, password);
        token.Should().NotBeNullOrEmpty();

        // PASO 3: Obtener perfil
        var perfilResponse = await Client.GetAsync("/api/auth/perfil");
        perfilResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var perfil = await perfilResponse.Content.ReadFromJsonAsync<dynamic>();
        string emailPerfil = perfil.GetProperty("email").GetString();
        emailPerfil.Should().Be(emailUsado);
    }
}
