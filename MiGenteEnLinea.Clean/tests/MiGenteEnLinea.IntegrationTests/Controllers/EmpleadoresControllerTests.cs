using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using MiGenteEnLinea.Application.Features.Empleadores.Commands.CreateEmpleador;
using MiGenteEnLinea.Application.Features.Empleadores.Commands.UpdateEmpleador;
using MiGenteEnLinea.Application.Features.Empleadores.DTOs;
using MiGenteEnLinea.IntegrationTests.Infrastructure;
using Xunit;

namespace MiGenteEnLinea.IntegrationTests.Controllers;

/// <summary>
/// Integration tests for EmpleadoresController
/// BLOQUE 2: Empleadores CRUD operations (8 tests)
/// </summary>
[Collection("IntegrationTests")]
public class EmpleadoresControllerTests : IntegrationTestBase
{
    public EmpleadoresControllerTests(TestWebApplicationFactory factory) : base(factory)
    {
    }

    #region CreateEmpleador Tests (2 tests)

    [Fact]
    public async Task CreateEmpleador_WithValidData_CreatesProfileAndReturnsEmpleadorId()
    {
        // Arrange - Register and login as empleador
        var email = GenerateUniqueEmail("empleador");
        var userId = await RegisterUserAsync(email, "Password123!", "Juan", "Pérez", "Empleador");
        await LoginAsync(email, "Password123!");

        var command = new CreateEmpleadorCommand(
            UserId: userId.ToString(),
            Habilidades: "Gestión de proyectos de construcción",
            Experiencia: "15 años en el sector construcción",
            Descripcion: "Empresa líder en construcción de edificios comerciales"
        );

        // Act
        var response = await Client.PostAsJsonAsync("/api/empleadores", command);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var empleadorId = await response.Content.ReadFromJsonAsync<int>();
        empleadorId.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task CreateEmpleador_WithoutAuthentication_ReturnsUnauthorized()
    {
        // Arrange - No authentication token
        ClearAuthToken();

        var command = new CreateEmpleadorCommand(
            UserId: "some-user-id",
            Habilidades: "Test skills",
            Experiencia: "Test experience",
            Descripcion: "Test description"
        );

        // Act
        var response = await Client.PostAsJsonAsync("/api/empleadores", command);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    #endregion

    #region GetEmpleadorById Tests (2 tests)

    [Fact]
    public async Task GetEmpleadorById_WithValidId_ReturnsEmpleadorDto()
    {
        // Arrange - Register, login, and create empleador
        var email = GenerateUniqueEmail("empleador");
        var userId = await RegisterUserAsync(email, "Password123!", "María", "González", "Empleador");
        await LoginAsync(email, "Password123!");

        var createCommand = new CreateEmpleadorCommand(
            UserId: userId.ToString(),
            Habilidades: "Gestión empresarial",
            Experiencia: "10 años",
            Descripcion: "Empresa de servicios profesionales"
        );
        var createResponse = await Client.PostAsJsonAsync("/api/empleadores", createCommand);
        var empleadorId = await createResponse.Content.ReadFromJsonAsync<int>();

        // Act
        var response = await Client.GetAsync($"/api/empleadores/{empleadorId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var empleadorDto = await response.Content.ReadFromJsonAsync<EmpleadorDto>();
        empleadorDto.Should().NotBeNull();
        empleadorDto!.EmpleadorId.Should().Be(empleadorId);
        empleadorDto.UserId.Should().Be(userId.ToString());
        empleadorDto.Habilidades.Should().Be("Gestión empresarial");
        empleadorDto.Experiencia.Should().Be("10 años");
        empleadorDto.Descripcion.Should().Be("Empresa de servicios profesionales");
    }

    [Fact]
    public async Task GetEmpleadorById_WithNonExistentId_ReturnsNotFound()
    {
        // Arrange - Register and login
        var email = GenerateUniqueEmail("empleador");
        await RegisterUserAsync(email, "Password123!", "Pedro", "Martínez", "Empleador");
        await LoginAsync(email, "Password123!");

        var nonExistentId = 999999;

        // Act
        var response = await Client.GetAsync($"/api/empleadores/{nonExistentId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    #endregion

    #region GetEmpleadoresList Tests (1 test)

    [Fact]
    public async Task GetEmpleadoresList_ReturnsListOfEmpleadores()
    {
        // Arrange - Register and login
        var email = GenerateUniqueEmail("empleador");
        await RegisterUserAsync(email, "Password123!", "Ana", "López", "Empleador");
        await LoginAsync(email, "Password123!");

        // Act
        var response = await Client.GetAsync("/api/empleadores");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var empleadores = await response.Content.ReadFromJsonAsync<List<EmpleadorDto>>();
        empleadores.Should().NotBeNull();
        empleadores.Should().BeOfType<List<EmpleadorDto>>();
        // Note: List might be empty or contain test data
    }

    #endregion

    #region UpdateEmpleador Tests (2 tests)

    [Fact]
    public async Task UpdateEmpleador_WithValidData_UpdatesSuccessfully()
    {
        // Arrange - Register, login, and create empleador
        var email = GenerateUniqueEmail("empleador");
        var userId = await RegisterUserAsync(email, "Password123!", "Carlos", "Ramírez", "Empleador");
        await LoginAsync(email, "Password123!");

        var createCommand = new CreateEmpleadorCommand(
            UserId: userId.ToString(),
            Habilidades: "Original skills",
            Experiencia: "Original experience",
            Descripcion: "Original description"
        );
        var createResponse = await Client.PostAsJsonAsync("/api/empleadores", createCommand);
        var empleadorId = await createResponse.Content.ReadFromJsonAsync<int>();

        // Update empleador
        var updateCommand = new UpdateEmpleadorCommand(
            UserId: userId.ToString(),
            Habilidades: "Updated skills: Gestión de proyectos",
            Experiencia: "Updated experience: 20 años",
            Descripcion: "Updated description: Empresa líder en innovación"
        );

        // Act
        var response = await Client.PutAsJsonAsync($"/api/empleadores/{empleadorId}", updateCommand);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var success = await response.Content.ReadFromJsonAsync<bool>();
        success.Should().BeTrue();

        // Verify update
        var getResponse = await Client.GetAsync($"/api/empleadores/{empleadorId}");
        var updatedEmpleador = await getResponse.Content.ReadFromJsonAsync<EmpleadorDto>();
        updatedEmpleador.Should().NotBeNull();
        updatedEmpleador!.Habilidades.Should().Be("Updated skills: Gestión de proyectos");
        updatedEmpleador.Experiencia.Should().Be("Updated experience: 20 años");
        updatedEmpleador.Descripcion.Should().Be("Updated description: Empresa líder en innovación");
    }

    [Fact]
    public async Task UpdateEmpleador_WithoutAuthentication_ReturnsUnauthorized()
    {
        // Arrange - No authentication token
        ClearAuthToken();

        var updateCommand = new UpdateEmpleadorCommand(
            UserId: "some-user-id",
            Habilidades: "Test skills",
            Experiencia: "Test experience",
            Descripcion: "Test description"
        );

        // Act
        var response = await Client.PutAsJsonAsync("/api/empleadores/123", updateCommand);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    #endregion

    #region GetEmpleadorPerfil Tests (1 test)

    [Fact]
    public async Task GetEmpleadorPerfil_WithValidUserId_ReturnsProfile()
    {
        // Arrange - Register, login, and create empleador
        var email = GenerateUniqueEmail("empleador");
        var userId = await RegisterUserAsync(email, "Password123!", "Laura", "Fernández", "Empleador");
        await LoginAsync(email, "Password123!");

        var createCommand = new CreateEmpleadorCommand(
            UserId: userId.ToString(),
            Habilidades: "Perfil test skills",
            Experiencia: "Perfil test experience",
            Descripcion: "Perfil test description"
        );
        await Client.PostAsJsonAsync("/api/empleadores", createCommand);

        // Act
        var response = await Client.GetAsync($"/api/empleadores/by-user/{userId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var empleadorDto = await response.Content.ReadFromJsonAsync<EmpleadorDto>();
        empleadorDto.Should().NotBeNull();
        empleadorDto!.UserId.Should().Be(userId.ToString());
        empleadorDto.Habilidades.Should().Be("Perfil test skills");
        empleadorDto.Experiencia.Should().Be("Perfil test experience");
        empleadorDto.Descripcion.Should().Be("Perfil test description");
    }

    #endregion
}