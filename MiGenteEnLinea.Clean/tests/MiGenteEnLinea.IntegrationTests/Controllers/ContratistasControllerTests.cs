using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using MiGenteEnLinea.Application.Features.Contratistas.Commands.CreateContratista;
using MiGenteEnLinea.Application.Features.Contratistas.Commands.UpdateContratista;
using MiGenteEnLinea.Application.Features.Contratistas.Common;
using MiGenteEnLinea.IntegrationTests.Infrastructure;
using Xunit;

namespace MiGenteEnLinea.IntegrationTests.Controllers;

/// <summary>
/// Integration tests for ContratistasController
/// BLOQUE 3: Contratistas CRUD operations (6 tests)
/// </summary>
[Collection("IntegrationTests")]
public class ContratistasControllerTests : IntegrationTestBase
{
    public ContratistasControllerTests(TestWebApplicationFactory factory) : base(factory)
    {
    }

    #region CreateContratista Tests (2 tests)

    [Fact]
    public async Task CreateContratista_WithValidData_CreatesProfileAndReturnsContratistaId()
    {
        // Arrange - Register and login as contratista
        var email = GenerateUniqueEmail("contratista");
        var userId = await RegisterUserAsync(email, "Password123!", "Pedro", "García", "Contratista");
        await LoginAsync(email, "Password123!");

        var command = new CreateContratistaCommand(
            UserId: userId.ToString(),
            Nombre: "Pedro",
            Apellido: "García",
            Tipo: 1, // Persona Física
            Titulo: "Plomero profesional certificado",
            Identificacion: GenerateRandomIdentification(),
            Sector: "Reparaciones del hogar",
            Experiencia: 10,
            Presentacion: "Plomero con 10 años de experiencia en instalaciones residenciales",
            Telefono1: "8091234567",
            Whatsapp1: true,
            Provincia: "Santo Domingo"
        );

        // Act
        var response = await Client.PostAsJsonAsync("/api/contratistas", command);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var contratistaId = await response.Content.ReadFromJsonAsync<int>();
        contratistaId.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task CreateContratista_WithoutAuthentication_ReturnsUnauthorized()
    {
        // Arrange - No authentication token
        ClearAuthToken();

        var command = new CreateContratistaCommand(
            UserId: "some-user-id",
            Nombre: "Test",
            Apellido: "User"
        );

        // Act
        var response = await Client.PostAsJsonAsync("/api/contratistas", command);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    #endregion

    #region GetContratistaById Tests (1 test)

    [Fact]
    public async Task GetContratistaById_WithValidId_ReturnsContratistaDto()
    {
        // Arrange - Register, login, and create contratista
        var email = GenerateUniqueEmail("contratista");
        var userId = await RegisterUserAsync(email, "Password123!", "María", "López", "Contratista");
        await LoginAsync(email, "Password123!");

        var createCommand = new CreateContratistaCommand(
            UserId: userId.ToString(),
            Nombre: "María",
            Apellido: "López",
            Tipo: 1,
            Titulo: "Electricista certificada",
            Sector: "Electricidad",
            Experiencia: 8,
            Presentacion: "Electricista especializada en instalaciones comerciales",
            Telefono1: "8099876543",
            Whatsapp1: true,
            Provincia: "Santiago"
        );
        var createResponse = await Client.PostAsJsonAsync("/api/contratistas", createCommand);
        var contratistaId = await createResponse.Content.ReadFromJsonAsync<int>();

        // Act
        var response = await Client.GetAsync($"/api/contratistas/{contratistaId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var contratistaDto = await response.Content.ReadFromJsonAsync<ContratistaDto>();
        contratistaDto.Should().NotBeNull();
        contratistaDto!.ContratistaId.Should().Be(contratistaId);
        contratistaDto.UserId.Should().Be(userId.ToString());
        contratistaDto.Nombre.Should().Be("María");
        contratistaDto.Apellido.Should().Be("López");
        contratistaDto.Titulo.Should().Be("Electricista certificada");
        contratistaDto.Sector.Should().Be("Electricidad");
        contratistaDto.Experiencia.Should().Be(8);
        contratistaDto.Provincia.Should().Be("Santiago");
    }

    #endregion

    #region GetContratistasList Tests (1 test)

    [Fact]
    public async Task GetContratistasList_ReturnsListOfContratistas()
    {
        // Arrange - Register and login
        var email = GenerateUniqueEmail("contratista");
        await RegisterUserAsync(email, "Password123!", "Carlos", "Martínez", "Contratista");
        await LoginAsync(email, "Password123!");

        // Act
        var response = await Client.GetAsync("/api/contratistas");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var contratistas = await response.Content.ReadFromJsonAsync<List<ContratistaDto>>();
        contratistas.Should().NotBeNull();
        contratistas.Should().BeOfType<List<ContratistaDto>>();
        // Note: List might be empty or contain test data
    }

    #endregion

    #region UpdateContratista Tests (2 tests)

    [Fact]
    public async Task UpdateContratista_WithValidData_UpdatesSuccessfully()
    {
        // Arrange - Register, login, and create contratista
        var email = GenerateUniqueEmail("contratista");
        var userId = await RegisterUserAsync(email, "Password123!", "Ana", "Rodríguez", "Contratista");
        await LoginAsync(email, "Password123!");

        var createCommand = new CreateContratistaCommand(
            UserId: userId.ToString(),
            Nombre: "Ana",
            Apellido: "Rodríguez",
            Tipo: 1,
            Titulo: "Carpintera",
            Sector: "Carpintería",
            Experiencia: 5,
            Presentacion: "Original presentation",
            Telefono1: "8091111111",
            Provincia: "La Vega"
        );
        var createResponse = await Client.PostAsJsonAsync("/api/contratistas", createCommand);
        var contratistaId = await createResponse.Content.ReadFromJsonAsync<int>();

        // Update contratista
        var updateCommand = new UpdateContratistaCommand(
            UserId: userId.ToString(),
            Titulo: "Carpintera profesional certificada",
            Sector: "Carpintería y Ebanistería",
            Experiencia: 7,
            Presentacion: "Updated: Carpintera especializada en muebles a medida",
            Provincia: "Santo Domingo",
            Telefono1: "8092222222",
            Whatsapp1: true,
            Telefono2: "8093333333",
            Email: "ana.carpintera@test.com"
        );

        // Act
        var response = await Client.PutAsJsonAsync($"/api/contratistas/{contratistaId}", updateCommand);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify update
        var getResponse = await Client.GetAsync($"/api/contratistas/{contratistaId}");
        var updatedContratista = await getResponse.Content.ReadFromJsonAsync<ContratistaDto>();
        updatedContratista.Should().NotBeNull();
        updatedContratista!.Titulo.Should().Be("Carpintera profesional certificada");
        updatedContratista.Sector.Should().Be("Carpintería y Ebanistería");
        updatedContratista.Experiencia.Should().Be(7);
        updatedContratista.Presentacion.Should().Be("Updated: Carpintera especializada en muebles a medida");
        updatedContratista.Provincia.Should().Be("Santo Domingo");
        updatedContratista.Telefono1.Should().Be("8092222222");
        updatedContratista.Email.Should().Be("ana.carpintera@test.com");
    }

    [Fact]
    public async Task UpdateContratista_WithoutAuthentication_ReturnsUnauthorized()
    {
        // Arrange - No authentication token
        ClearAuthToken();

        var updateCommand = new UpdateContratistaCommand(
            UserId: "some-user-id",
            Titulo: "Test title"
        );

        // Act
        var response = await Client.PutAsJsonAsync("/api/contratistas/123", updateCommand);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    #endregion
}