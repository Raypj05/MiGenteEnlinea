using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using MiGenteEnLinea.Application.Features.Empleados.Commands.CreateEmpleado;
using MiGenteEnLinea.Application.Features.Empleados.Commands.UpdateEmpleado;
using MiGenteEnLinea.Application.Features.Empleados.Commands.DarDeBajaEmpleado;
using MiGenteEnLinea.Application.Features.Empleados.DTOs;
using MiGenteEnLinea.IntegrationTests.Infrastructure;
using Xunit;

namespace MiGenteEnLinea.IntegrationTests.Controllers;

/// <summary>
/// Integration tests for EmpleadosController
/// BLOQUE 4: Empleados CRUD operations (12 tests simplified)
/// </summary>
[Collection("IntegrationTests")]
public class EmpleadosControllerTests : IntegrationTestBase
{
    public EmpleadosControllerTests(TestWebApplicationFactory factory) : base(factory)
    {
    }

    #region CreateEmpleado Tests (2 tests)

    [Fact]
    public async Task CreateEmpleado_WithValidData_CreatesEmpleadoAndReturnsId()
    {
        // Arrange - Register and login as empleador
        var email = GenerateUniqueEmail("empleador");
        var userId = await RegisterUserAsync(email, "Password123!", "Empresa", "Test", "Empleador");
        await LoginAsync(email, "Password123!");

        var command = new CreateEmpleadoCommand
        {
            UserId = userId.ToString(),
            Identificacion = GenerateRandomIdentification(),
            Nombre = "Juan",
            Apellido = "Pérez",
            FechaInicio = DateTime.Now,
            Posicion = "Desarrollador",
            Salario = 50000m,
            PeriodoPago = 3, // Mensual
            Tss = true,
            Telefono1 = "8091234567",
            Direccion = "Calle Principal #123",
            Provincia = "Santo Domingo"
        };

        // Act
        var response = await Client.PostAsJsonAsync("/api/empleados", command);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var empleadoId = await response.Content.ReadFromJsonAsync<int>();
        empleadoId.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task CreateEmpleado_WithoutAuthentication_ReturnsUnauthorized()
    {
        // Arrange - No authentication
        ClearAuthToken();

        var command = new CreateEmpleadoCommand
        {
            UserId = "test-user",
            Identificacion = "12345678901",
            Nombre = "Test",
            Apellido = "User",
            FechaInicio = DateTime.Now,
            Salario = 30000m,
            PeriodoPago = 3
        };

        // Act
        var response = await Client.PostAsJsonAsync("/api/empleados", command);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    #endregion

    #region GetEmpleado Tests (2 tests)

    [Fact]
    public async Task GetEmpleadoById_WithValidId_ReturnsEmpleadoDetalle()
    {
        // Arrange - Create empleado first
        var email = GenerateUniqueEmail("empleador");
        var userId = await RegisterUserAsync(email, "Password123!", "Empresa", "Test", "Empleador");
        await LoginAsync(email, "Password123!");

        var createCommand = new CreateEmpleadoCommand
        {
            UserId = userId.ToString(),
            Identificacion = GenerateRandomIdentification(),
            Nombre = "María",
            Apellido = "González",
            FechaInicio = DateTime.Now,
            Posicion = "Contadora",
            Salario = 45000m,
            PeriodoPago = 2, // Quincenal
            Tss = true,
            Telefono1 = "8099876543"
        };
        var createResponse = await Client.PostAsJsonAsync("/api/empleados", createCommand);
        var empleadoId = await createResponse.Content.ReadFromJsonAsync<int>();

        // Act
        var response = await Client.GetAsync($"/api/empleados/{empleadoId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var empleado = await response.Content.ReadFromJsonAsync<EmpleadoDetalleDto>();
        empleado.Should().NotBeNull();
        empleado!.EmpleadoId.Should().Be(empleadoId);
        empleado.Nombre.Should().Be("María");
        empleado.Apellido.Should().Be("González");
        empleado.Posicion.Should().Be("Contadora");
        empleado.Salario.Should().Be(45000m);
        empleado.PeriodoPago.Should().Be(2);
    }

    [Fact]
    public async Task GetEmpleadoById_WithNonExistentId_ReturnsNotFound()
    {
        // Arrange
        var email = GenerateUniqueEmail("empleador");
        await RegisterUserAsync(email, "Password123!", "Empresa", "Test", "Empleador");
        await LoginAsync(email, "Password123!");

        var nonExistentId = 999999;

        // Act
        var response = await Client.GetAsync($"/api/empleados/{nonExistentId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    #endregion

    #region GetEmpleadosList Tests (2 tests)

    [Fact]
    public async Task GetEmpleadosList_ReturnsListOfEmpleados()
    {
        // Arrange
        var email = GenerateUniqueEmail("empleador");
        var userId = await RegisterUserAsync(email, "Password123!", "Empresa", "Test", "Empleador");
        await LoginAsync(email, "Password123!");

        // Create at least one empleado
        var createCommand = new CreateEmpleadoCommand
        {
            UserId = userId.ToString(),
            Identificacion = GenerateRandomIdentification(),
            Nombre = "Carlos",
            Apellido = "Martínez",
            FechaInicio = DateTime.Now,
            Salario = 35000m,
            PeriodoPago = 3
        };
        await Client.PostAsJsonAsync("/api/empleados", createCommand);

        // Act
        var response = await Client.GetAsync($"/api/empleados/by-user/{userId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var empleados = await response.Content.ReadFromJsonAsync<List<EmpleadoListDto>>();
        empleados.Should().NotBeNull();
        empleados.Should().HaveCountGreaterOrEqualTo(1);
        empleados![0].Nombre.Should().Be("Carlos");
    }

    [Fact]
    public async Task GetEmpleadosActivos_ReturnsOnlyActiveEmpleados()
    {
        // Arrange
        var email = GenerateUniqueEmail("empleador");
        var userId = await RegisterUserAsync(email, "Password123!", "Empresa", "Test", "Empleador");
        await LoginAsync(email, "Password123!");

        // Act
        var response = await Client.GetAsync($"/api/empleados/by-user/{userId}?soloActivos=true");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var empleados = await response.Content.ReadFromJsonAsync<List<EmpleadoListDto>>();
        empleados.Should().NotBeNull();
        empleados.Should().AllSatisfy(e => e.Activo.Should().BeTrue());
    }

    #endregion

    #region UpdateEmpleado Tests (2 tests)

    [Fact]
    public async Task UpdateEmpleado_WithValidData_UpdatesSuccessfully()
    {
        // Arrange - Create empleado first
        var email = GenerateUniqueEmail("empleador");
        var userId = await RegisterUserAsync(email, "Password123!", "Empresa", "Test", "Empleador");
        await LoginAsync(email, "Password123!");

        var createCommand = new CreateEmpleadoCommand
        {
            UserId = userId.ToString(),
            Identificacion = GenerateRandomIdentification(),
            Nombre = "Ana",
            Apellido = "López",
            FechaInicio = DateTime.Now,
            Posicion = "Asistente",
            Salario = 30000m,
            PeriodoPago = 3,
            Telefono1 = "8091111111"
        };
        var createResponse = await Client.PostAsJsonAsync("/api/empleados", createCommand);
        var empleadoId = await createResponse.Content.ReadFromJsonAsync<int>();

        // Update empleado
        var updateCommand = new UpdateEmpleadoCommand
        {
            EmpleadoId = empleadoId,
            Posicion = "Gerente de Ventas",
            Salario = 60000m,
            Telefono1 = "8092222222",
            Direccion = "Nueva Dirección 456"
        };

        // Act
        var response = await Client.PutAsJsonAsync($"/api/empleados/{empleadoId}", updateCommand);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify update
        var getResponse = await Client.GetAsync($"/api/empleados/{empleadoId}");
        var updatedEmpleado = await getResponse.Content.ReadFromJsonAsync<EmpleadoDetalleDto>();
        updatedEmpleado.Should().NotBeNull();
        updatedEmpleado!.Posicion.Should().Be("Gerente de Ventas");
        updatedEmpleado.Salario.Should().Be(60000m);
        updatedEmpleado.Telefono1.Should().Be("8092222222");
    }

    [Fact]
    public async Task UpdateEmpleado_WithoutAuthentication_ReturnsUnauthorized()
    {
        // Arrange - No authentication
        ClearAuthToken();

        var updateCommand = new UpdateEmpleadoCommand
        {
            EmpleadoId = 123,
            Posicion = "Test"
        };

        // Act
        var response = await Client.PutAsJsonAsync("/api/empleados/123", updateCommand);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    #endregion

    #region DarDeBajaEmpleado Tests (2 tests)

    [Fact]
    public async Task DarDeBajaEmpleado_WithValidData_InactivatesEmpleado()
    {
        // Arrange - Create empleado first
        var email = GenerateUniqueEmail("empleador");
        var userId = await RegisterUserAsync(email, "Password123!", "Empresa", "Test", "Empleador");
        await LoginAsync(email, "Password123!");

        var createCommand = new CreateEmpleadoCommand
        {
            UserId = userId.ToString(),
            Identificacion = GenerateRandomIdentification(),
            Nombre = "Pedro",
            Apellido = "Ramírez",
            FechaInicio = DateTime.Now.AddMonths(-6),
            Salario = 40000m,
            PeriodoPago = 3
        };
        var createResponse = await Client.PostAsJsonAsync("/api/empleados", createCommand);
        var empleadoId = await createResponse.Content.ReadFromJsonAsync<int>();

        // Dar de baja (primary constructor)
        var bajaCommand = new DarDeBajaEmpleadoCommand(
            EmpleadoId: empleadoId,
            UserId: userId.ToString(),
            FechaBaja: DateTime.Now,
            Prestaciones: 15000m,
            Motivo: "Renuncia voluntaria"
        );

        // Act
        var response = await Client.PostAsJsonAsync($"/api/empleados/{empleadoId}/dar-baja", bajaCommand);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify empleado is inactive
        var getResponse = await Client.GetAsync($"/api/empleados/{empleadoId}");
        var empleado = await getResponse.Content.ReadFromJsonAsync<EmpleadoDetalleDto>();
        empleado.Should().NotBeNull();
        empleado!.Activo.Should().BeFalse();
        empleado.FechaSalida.Should().NotBeNull();
        empleado.MotivoBaja.Should().Be("Renuncia voluntaria");
        empleado.Prestaciones.Should().Be(15000m);
    }

    [Fact]
    public async Task DarDeBajaEmpleado_WithoutAuthentication_ReturnsUnauthorized()
    {
        // Arrange - No authentication
        ClearAuthToken();

        var bajaCommand = new DarDeBajaEmpleadoCommand(
            EmpleadoId: 123,
            UserId: "test-user",
            FechaBaja: DateTime.Now,
            Prestaciones: 0m,
            Motivo: "Test"
        );

        // Act
        var response = await Client.PostAsJsonAsync("/api/empleados/123/dar-baja", bajaCommand);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    #endregion

    #region ValidationTests (2 tests)

    [Fact]
    public async Task CreateEmpleado_WithInvalidCedula_ReturnsBadRequest()
    {
        // Arrange
        var email = GenerateUniqueEmail("empleador");
        var userId = await RegisterUserAsync(email, "Password123!", "Empresa", "Test", "Empleador");
        await LoginAsync(email, "Password123!");

        var command = new CreateEmpleadoCommand
        {
            UserId = userId.ToString(),
            Identificacion = "123", // Invalid: too short
            Nombre = "Test",
            Apellido = "User",
            FechaInicio = DateTime.Now,
            Salario = 30000m,
            PeriodoPago = 3
        };

        // Act
        var response = await Client.PostAsJsonAsync("/api/empleados", command);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateEmpleado_WithNegativeSalary_ReturnsBadRequest()
    {
        // Arrange
        var email = GenerateUniqueEmail("empleador");
        var userId = await RegisterUserAsync(email, "Password123!", "Empresa", "Test", "Empleador");
        await LoginAsync(email, "Password123!");

        var command = new CreateEmpleadoCommand
        {
            UserId = userId.ToString(),
            Identificacion = GenerateRandomIdentification(),
            Nombre = "Test",
            Apellido = "User",
            FechaInicio = DateTime.Now,
            Salario = -1000m, // Invalid: negative
            PeriodoPago = 3
        };

        // Act
        var response = await Client.PostAsJsonAsync("/api/empleados", command);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    #endregion
}