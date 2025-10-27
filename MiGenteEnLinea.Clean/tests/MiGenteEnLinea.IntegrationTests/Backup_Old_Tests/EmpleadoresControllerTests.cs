using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MiGenteEnLinea.Application.Features.Empleadores.Commands.CreateEmpleador;
using MiGenteEnLinea.Application.Features.Empleadores.Commands.UpdateEmpleador;
using MiGenteEnLinea.Application.Features.Empleadores.DTOs;
using MiGenteEnLinea.IntegrationTests.Infrastructure;

namespace MiGenteEnLinea.IntegrationTests.Controllers;

/// <summary>
/// Tests de integración para EmpleadoresController - CRUD completo
/// </summary>
[Collection("Integration Tests")]
public class EmpleadoresControllerTests : IntegrationTestBase
{
    public EmpleadoresControllerTests(TestWebApplicationFactory factory) : base(factory)
    {
    }

    #region Create Tests

    [Fact]
    public async Task CreateEmpleador_WithValidData_CreatesSuccessfully()
    {
        // Arrange - Autenticar como empleador que NO tiene perfil aún
        await LoginAsync("maria.garcia@test.com", TestDataSeeder.TestPasswordPlainText);

        // ✅ Obtener userId desde credenciales (usuario sin empleador creado)
        var credencial = await AppDbContext.Credenciales
            .FirstAsync(c => c.Email.Value == "maria.garcia@test.com");

        var command = new CreateEmpleadorCommand(
            UserId: credencial.UserId,
            Habilidades: "Gestión de equipos, Recursos Humanos",
            Experiencia: "10 años en administración",
            Descripcion: "Empresa dedicada a servicios tecnológicos"
        );

        // Act
        var response = await Client.PostAsJsonAsync("/api/empleadores", command);

        // Assert
        response.IsSuccessStatusCode.Should().BeTrue();
        var empleadorId = await response.Content.ReadFromJsonAsync<int>();
        empleadorId.Should().BeGreaterThan(0);

        // Verificar en DB
        var createdEmpleador = await AppDbContext.Empleadores.FindAsync(empleadorId);
        createdEmpleador.Should().NotBeNull();
        createdEmpleador!.UserId.Should().Be(credencial.UserId);
        createdEmpleador.Habilidades.Should().Be("Gestión de equipos, Recursos Humanos");
    }

    [Fact]
    public async Task CreateEmpleador_WithoutAuthentication_ReturnsUnauthorized()
    {
        // Arrange - NO autenticar
        var command = new CreateEmpleadorCommand(
            UserId: "test-user-id",
            Habilidades: "Test",
            Experiencia: "Test"
        );

        // Act
        var response = await Client.PostAsJsonAsync("/api/empleadores", command);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ✅ NOTA: CreateEmpleadorCommand no valida datos internamente (solo UserId requerido)
    // Los campos Habilidades, Experiencia, Descripcion son todos opcionales
    // No hay validación de longitud en el Command (solo en el dominio al crear)

    #endregion

    #region Get Tests

    [Fact]
    public async Task GetEmpleadorById_WithValidId_ReturnsEmpleador()
    {
        // Arrange
        await LoginAsync("juan.perez@test.com", TestDataSeeder.TestPasswordPlainText);
        var empleador = await TestDataSeeder.GetEmpleadorActivoAsync(AppDbContext);

        // Act
        var response = await Client.GetAsync($"/api/empleadores/{empleador.Id}");

        // Assert
        response.IsSuccessStatusCode.Should().BeTrue();
        var result = await response.Content.ReadFromJsonAsync<EmpleadorDto>();
        result.Should().NotBeNull();
        result!.EmpleadorId.Should().Be(empleador.Id);
        // ✅ Empleador solo tiene Habilidades, Experiencia, Descripcion (no NombreEmpresa ni RncCedula)
        result.Habilidades.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task GetEmpleadorById_WithNonExistentId_ReturnsNotFound()
    {
        // Arrange
        await LoginAsync("juan.perez@test.com", TestDataSeeder.TestPasswordPlainText);

        // Act
        var response = await Client.GetAsync("/api/empleadores/99999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetAllEmpleadores_ReturnsEmpleadoresList()
    {
        // Arrange
        await LoginAsync("juan.perez@test.com", TestDataSeeder.TestPasswordPlainText);

        // Act
        var response = await Client.GetAsync("/api/empleadores");

        // Assert
        response.IsSuccessStatusCode.Should().BeTrue();
        var result = await response.Content.ReadFromJsonAsync<List<EmpleadorDto>>();
        result.Should().NotBeNull();
        result!.Should().HaveCountGreaterOrEqualTo(2); // Tenemos 2 seeded
    }

    [Fact]
    public async Task GetEmpleadorPerfil_WithValidCuentaId_ReturnsProfile()
    {
        // Arrange
        await LoginAsync("juan.perez@test.com", TestDataSeeder.TestPasswordPlainText);
        var empleador = await TestDataSeeder.GetEmpleadorActivoAsync(AppDbContext);

        // Act
        var response = await Client.GetAsync($"/api/empleadores/perfil/{empleador.UserId}");

        // Assert
        response.IsSuccessStatusCode.Should().BeTrue();
        var result = await response.Content.ReadFromJsonAsync<EmpleadorDto>();
        result.Should().NotBeNull();
        // ✅ Empleador tiene Habilidades, Experiencia, Descripcion
        result!.UserId.Should().Be(empleador.UserId);
    }

    [Fact]
    public async Task SearchEmpleadores_WithKeyword_ReturnsMatchingResults()
    {
        // Arrange
        await LoginAsync("juan.perez@test.com", TestDataSeeder.TestPasswordPlainText);

        // Act
        var response = await Client.GetAsync("/api/empleadores/search?keyword=Pérez");

        // Assert
        response.IsSuccessStatusCode.Should().BeTrue();
        var result = await response.Content.ReadFromJsonAsync<List<EmpleadorDto>>();
        result.Should().NotBeNull();
        result!.Should().NotBeEmpty();
        result.Should().Contain(e => e.NombreEmpresa.Contains("Pérez"));
    }

    [Fact]
    public async Task SearchEmpleadores_WithNonMatchingKeyword_ReturnsEmptyList()
    {
        // Arrange
        await LoginAsync("juan.perez@test.com", TestDataSeeder.TestPasswordPlainText);

        // Act
        var response = await Client.GetAsync("/api/empleadores/search?keyword=NonExistentKeyword12345");

        // Assert
        response.IsSuccessStatusCode.Should().BeTrue();
        var result = await response.Content.ReadFromJsonAsync<List<EmpleadorDto>>();
        result.Should().NotBeNull();
        result!.Should().BeEmpty();
    }

    #endregion

    #region Update Tests

    [Fact]
    public async Task UpdateEmpleador_WithValidData_UpdatesSuccessfully()
    {
        // Arrange
        await LoginAsync("juan.perez@test.com", TestDataSeeder.TestPasswordPlainText);
        var empleador = await TestDataSeeder.GetEmpleadorActivoAsync(DbContext);

        var command = new UpdateEmpleadorCommand
        {
            Id = empleador.Id,
            NombreEmpresa = "Pérez Construcciones SRL - ACTUALIZADA",
            RncCedula = empleador.RncCedula,
            Direccion = "Nueva Dirección Actualizada #999",
            Sector = "Construcción y Desarrollo",
            Web = "www.perezconstrucciones-new.com",
            Telefono = "809-999-8888"
        };

        // Act
        var response = await Client.PutAsJsonAsync($"/api/empleadores/{empleador.Id}", command);

        // Assert
        response.IsSuccessStatusCode.Should().BeTrue();

        // Verificar en DB
        await DbContext.Entry(empleador).ReloadAsync();
        empleador.NombreEmpresa.Should().Be("Pérez Construcciones SRL - ACTUALIZADA");
        empleador.Direccion.Should().Be("Nueva Dirección Actualizada #999");
        empleador.Web.Should().Be("www.perezconstrucciones-new.com");
    }

    [Fact]
    public async Task UpdateEmpleador_WithNonExistentId_ReturnsNotFound()
    {
        // Arrange
        await LoginAsync("juan.perez@test.com", TestDataSeeder.TestPasswordPlainText);

        var command = new UpdateEmpleadorCommand
        {
            Id = 99999,
            NombreEmpresa = "Test Empresa",
            RncCedula = "101-99999-9",
            Direccion = "Test Address",
            Sector = "Test"
        };

        // Act
        var response = await Client.PutAsJsonAsync("/api/empleadores/99999", command);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UpdateEmpleador_WithMismatchedIds_ReturnsBadRequest()
    {
        // Arrange
        await LoginAsync("juan.perez@test.com", TestDataSeeder.TestPasswordPlainText);
        var empleador = await TestDataSeeder.GetEmpleadorActivoAsync(DbContext);

        var command = new UpdateEmpleadorCommand
        {
            Id = 999, // ID diferente al de la URL
            NombreEmpresa = "Test",
            RncCedula = "101-99999-9",
            Direccion = "Test",
            Sector = "Test"
        };

        // Act
        var response = await Client.PutAsJsonAsync($"/api/empleadores/{empleador.Id}", command);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    #endregion

    #region Delete Tests

    [Fact]
    public async Task DeleteEmpleador_WithValidId_DeletesSuccessfully()
    {
        // Arrange - Crear un empleador temporal para eliminar
        await LoginAsync("juan.perez@test.com", TestDataSeeder.TestPasswordPlainText);
        var empleador = await TestDataSeeder.GetEmpleadorActivoAsync(DbContext);
        
        // Crear uno nuevo para poder eliminarlo sin afectar los datos seeded
        var newEmpleador = new Domain.Entities.Empleadores.Empleador
        {
            CuentaId = empleador.UserId,
            NombreEmpresa = "Empresa Temporal",
            RncCedula = "101-88888-8",
            Direccion = "Temp Address",
            Sector = "Temp"
        };
        DbContext.Empleadores.Add(newEmpleador);
        await DbContext.SaveChangesAsync();

        // Act
        var response = await Client.DeleteAsync($"/api/empleadores/{newEmpleador.Id}");

        // Assert
        response.IsSuccessStatusCode.Should().BeTrue();

        // Verificar que fue eliminado (soft delete)
        await DbContext.Entry(newEmpleador).ReloadAsync();
        newEmpleador.IsDeleted.Should().BeTrue();
    }

    [Fact]
    public async Task DeleteEmpleador_WithNonExistentId_ReturnsNotFound()
    {
        // Arrange
        await LoginAsync("juan.perez@test.com", TestDataSeeder.TestPasswordPlainText);

        // Act
        var response = await Client.DeleteAsync("/api/empleadores/99999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    #endregion

    #region Update Profile Tests

    [Fact]
    public async Task UpdateEmpleadorProfile_WithValidData_UpdatesSuccessfully()
    {
        // Arrange
        await LoginAsync("juan.perez@test.com", TestDataSeeder.TestPasswordPlainText);
        var empleador = await TestDataSeeder.GetEmpleadorActivoAsync(DbContext);

        var updateData = new
        {
            nombreEmpresa = "Perfil Actualizado SRL",
            sector = "Sector Actualizado",
            web = "www.updated.com"
        };

        // Act
        var response = await Client.PutAsJsonAsync($"/api/empleadores/perfil/{empleador.UserId}", updateData);

        // Assert
        response.IsSuccessStatusCode.Should().BeTrue();

        // Verificar en DB
        await DbContext.Entry(empleador).ReloadAsync();
        empleador.NombreEmpresa.Should().Be("Perfil Actualizado SRL");
        empleador.Sector.Should().Be("Sector Actualizado");
        empleador.Web.Should().Be("www.updated.com");
    }

    #endregion
}

