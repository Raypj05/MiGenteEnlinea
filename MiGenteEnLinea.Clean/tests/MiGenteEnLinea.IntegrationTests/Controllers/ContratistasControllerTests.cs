using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MiGenteEnLinea.Application.Features.Contratistas.Commands.CreateContratista;
using MiGenteEnLinea.Application.Features.Contratistas.Commands.UpdateContratista;
using MiGenteEnLinea.Application.Features.Contratistas.Common;
using MiGenteEnLinea.IntegrationTests.Infrastructure;

namespace MiGenteEnLinea.IntegrationTests.Controllers;

/// <summary>
/// Tests de integración para ContratistasController - CRUD + Servicios + Calificaciones
/// </summary>
[Collection("Integration Tests")]
public class ContratistasControllerTests : IntegrationTestBase
{
    public ContratistasControllerTests(TestWebApplicationFactory factory) : base(factory)
    {
    }

    #region Create Tests

    [Fact]
    public async Task CreateContratista_WithValidData_CreatesSuccessfully()
    {
        // Arrange
        await LoginAsync("carlos.rodriguez@test.com", TestDataSeeder.TestPasswordPlainText);
        var contratista = await TestDataSeeder.GetContratistaActivoAsync(DbContext);

        var command = new CreateContratistaCommand(
            UserId: contratista.UserId,
            Nombre: "Nuevo",
            Apellido: "Contratista",
            Tipo: 1,
            Titulo: "Ingeniero Civil",
            Identificacion: "001-9999999-9",
            Sector: "Construcción",
            Experiencia: 8,
            Presentacion: "Contratista con experiencia",
            Telefono1: "809-555-9999",
            Whatsapp1: true,
            Provincia: "Santo Domingo");

        // Act
        var response = await Client.PostAsJsonAsync("/api/contratistas", command);

        // Assert
        response.IsSuccessStatusCode.Should().BeTrue();
        var contratistaId = await response.Content.ReadFromJsonAsync<int>();
        contratistaId.Should().BeGreaterThan(0);

        // Verificar en DB
        var created = await DbContext.Contratistas.FindAsync(contratistaId);
        created.Should().NotBeNull();
        created!.Identificacion.Should().Be("001-9999999-9");
    }

    [Theory]
    [InlineData("", "Apellido")] // Nombre vacío
    [InlineData("Nombre", "")] // Apellido vacío
    [InlineData(null, "Apellido")] // Nombre null
    [InlineData("Nombre", null)] // Apellido null
    public async Task CreateContratista_WithInvalidData_ReturnsBadRequest(
        string nombre, string apellido)
    {
        // Arrange
        await LoginAsync("carlos.rodriguez@test.com", TestDataSeeder.TestPasswordPlainText);
        var contratista = await TestDataSeeder.GetContratistaActivoAsync(DbContext);

        var command = new CreateContratistaCommand(
            UserId: contratista.UserId,
            Nombre: nombre,
            Apellido: apellido,
            Tipo: 1);

        // Act
        var response = await Client.PostAsJsonAsync("/api/contratistas", command);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    #endregion

    #region Get Tests

    [Fact]
    public async Task GetContratistaById_WithValidId_ReturnsContratista()
    {
        // Arrange
        await LoginAsync("carlos.rodriguez@test.com", TestDataSeeder.TestPasswordPlainText);
        var contratista = await TestDataSeeder.GetContratistaActivoAsync(DbContext);

        // Act
        var response = await Client.GetAsync($"/api/contratistas/{contratista.Id}");

        // Assert
        response.IsSuccessStatusCode.Should().BeTrue();
        var result = await response.Content.ReadFromJsonAsync<ContratistaDto>();
        result.Should().NotBeNull();
        result!.ContratistaId.Should().Be(contratista.Id); // ✅ ContratistaDto usa ContratistaId
        result.Identificacion.Should().NotBeNullOrEmpty(); // ✅ ContratistaDto tiene Identificacion (not ContratistaIdentificacion)
    }

    [Fact]
    public async Task GetContratistaById_WithNonExistentId_ReturnsNotFound()
    {
        // Arrange
        await LoginAsync("carlos.rodriguez@test.com", TestDataSeeder.TestPasswordPlainText);

        // Act
        var response = await Client.GetAsync("/api/contratistas/99999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetAllContratistas_ReturnsContratistasList()
    {
        // Arrange
        await LoginAsync("carlos.rodriguez@test.com", TestDataSeeder.TestPasswordPlainText);

        // Act
        var response = await Client.GetAsync("/api/contratistas");

        // Assert
        response.IsSuccessStatusCode.Should().BeTrue();
        var result = await response.Content.ReadFromJsonAsync<List<ContratistaDto>>();
        result.Should().NotBeNull();
        result!.Should().HaveCountGreaterOrEqualTo(2); // Tenemos 2 seeded
    }

    [Fact]
    public async Task GetContratistaPerfil_WithValidCuentaId_ReturnsProfile()
    {
        // Arrange
        await LoginAsync("carlos.rodriguez@test.com", TestDataSeeder.TestPasswordPlainText);
        var contratista = await TestDataSeeder.GetContratistaActivoAsync(DbContext);

        // Act
        var response = await Client.GetAsync($"/api/contratistas/perfil/{contratista.UserId}");

        // Assert
        response.IsSuccessStatusCode.Should().BeTrue();
        var result = await response.Content.ReadFromJsonAsync<ContratistaDto>();
        result.Should().NotBeNull();
        result!.Identificacion.Should().Be("001-0000001-0");
    }

    [Fact]
    public async Task SearchContratistas_WithKeyword_ReturnsMatchingResults()
    {
        // Arrange
        await LoginAsync("carlos.rodriguez@test.com", TestDataSeeder.TestPasswordPlainText);

        // Act
        var response = await Client.GetAsync("/api/contratistas/search?keyword=Rodriguez");

        // Assert
        response.IsSuccessStatusCode.Should().BeTrue();
        var result = await response.Content.ReadFromJsonAsync<List<ContratistaDto>>();
        result.Should().NotBeNull();
        result!.Should().NotBeEmpty();
    }

    #endregion

    #region Update Tests

    [Fact]
    public async Task UpdateContratista_WithValidData_UpdatesSuccessfully()
    {
        // Arrange
        await LoginAsync("carlos.rodriguez@test.com", TestDataSeeder.TestPasswordPlainText);
        var contratista = await TestDataSeeder.GetContratistaActivoAsync(DbContext);

        var command = new UpdateContratistaCommand
        {
            Id = contratista.Id,
            Cedula = contratista.Identificacion,
            Direccion = "Dirección Actualizada #777",
            FechaNacimiento = contratista.FechaNacimiento,
            EstadoCivil = "Casado",
            Sexo = contratista.Sexo,
            Nacionalidad = "Dominicana"
        };

        // Act
        var response = await Client.PutAsJsonAsync($"/api/contratistas/{contratista.Id}", command);

        // Assert
        response.IsSuccessStatusCode.Should().BeTrue();

        // Verificar en DB
        await DbContext.Entry(contratista).ReloadAsync();
        contratista.Direccion.Should().Be("Dirección Actualizada #777");
        contratista.EstadoCivil.Should().Be("Casado");
    }

    [Fact]
    public async Task UpdateContratista_WithNonExistentId_ReturnsNotFound()
    {
        // Arrange
        await LoginAsync("carlos.rodriguez@test.com", TestDataSeeder.TestPasswordPlainText);

        var command = new UpdateContratistaCommand
        {
            Id = 99999,
            Cedula = "001-9999999-9",
            Direccion = "Test",
            FechaNacimiento = DateTime.Now,
            EstadoCivil = "Soltero",
            Sexo = "M",
            Nacionalidad = "Dominicana"
        };

        // Act
        var response = await Client.PutAsJsonAsync("/api/contratistas/99999", command);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    #endregion

    #region Delete Tests

    [Fact]
    public async Task DeleteContratista_WithValidId_DeletesSuccessfully()
    {
        // Arrange - Crear uno temporal
        await LoginAsync("carlos.rodriguez@test.com", TestDataSeeder.TestPasswordPlainText);
        var contratista = await TestDataSeeder.GetContratistaActivoAsync(DbContext);

        var newContratista = new Domain.Entities.Contratistas.Contratista
        {
            CuentaId = contratista.UserId,
            Cedula = "001-8888888-8",
            Direccion = "Temp",
            FechaNacimiento = DateTime.Now.AddYears(-30),
            EstadoCivil = "Soltero",
            Sexo = "M",
            Nacionalidad = "Dominicana"
        };
        DbContext.Contratistas.Add(newContratista);
        await DbContext.SaveChangesAsync();

        // Act
        var response = await Client.DeleteAsync($"/api/contratistas/{newContratista.Id}");

        // Assert
        response.IsSuccessStatusCode.Should().BeTrue();

        // Verificar soft delete
        await DbContext.Entry(newContratista).ReloadAsync();
        newContratista.IsDeleted.Should().BeTrue();
    }

    #endregion

    #region Servicios Tests

    [Fact]
    public async Task AddServicio_WithValidData_AddsSuccessfully()
    {
        // Arrange
        await LoginAsync("carlos.rodriguez@test.com", TestDataSeeder.TestPasswordPlainText);
        var contratista = await TestDataSeeder.GetContratistaActivoAsync(DbContext);

        var servicioData = new
        {
            contratistaId = contratista.Id,
            descripcion = "Electricidad Residencial",
            categoria = "Servicios Técnicos",
            precioHora = 500.00m
        };

        // Act
        var response = await Client.PostAsJsonAsync("/api/contratistas/servicios", servicioData);

        // Assert
        response.IsSuccessStatusCode.Should().BeTrue();

        // Verificar en DB
        var servicios = await DbContext.ContratistaServicios
            .Where(s => s.ContratistaId == contratista.Id)
            .ToListAsync();
        servicios.Should().Contain(s => s.Descripcion == "Electricidad Residencial");
    }

    [Fact]
    public async Task GetServiciosByContratista_ReturnsServiciosList()
    {
        // Arrange
        await LoginAsync("carlos.rodriguez@test.com", TestDataSeeder.TestPasswordPlainText);
        var contratista = await TestDataSeeder.GetContratistaActivoAsync(DbContext);

        // Agregar un servicio primero
        var servicio = new Domain.Entities.Contratistas.ContratistaServicio
        {
            ContratistaId = contratista.Id,
            Descripcion = "Plomería",
            Categoria = "Servicios del Hogar",
            PrecioHora = 400.00m
        };
        DbContext.ContratistaServicios.Add(servicio);
        await DbContext.SaveChangesAsync();

        // Act
        var response = await Client.GetAsync($"/api/contratistas/{contratista.Id}/servicios");

        // Assert
        response.IsSuccessStatusCode.Should().BeTrue();
        var result = await response.Content.ReadFromJsonAsync<List<object>>();
        result.Should().NotBeNull();
        result!.Should().NotBeEmpty();
    }

    #endregion

    #region Calificaciones Tests

    [Fact]
    public async Task GetCalificaciones_ForContratista_ReturnsCalificacionesList()
    {
        // Arrange
        await LoginAsync("carlos.rodriguez@test.com", TestDataSeeder.TestPasswordPlainText);
        var contratista = await TestDataSeeder.GetContratistaActivoAsync(DbContext);

        // Act
        var response = await Client.GetAsync($"/api/calificaciones/contratista/{contratista.Id}");

        // Assert
        response.IsSuccessStatusCode.Should().BeTrue();
        var result = await response.Content.ReadFromJsonAsync<List<object>>();
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task GetPromedioCalificacion_ForContratista_ReturnsPromedio()
    {
        // Arrange
        await LoginAsync("carlos.rodriguez@test.com", TestDataSeeder.TestPasswordPlainText);
        var contratista = await TestDataSeeder.GetContratistaActivoAsync(DbContext);

        // Agregar calificaciones de prueba
        var calificacion1 = new Domain.Entities.Calificaciones.Calificacion
        {
            ContratistaId = contratista.Id,
            EmpleadorId = 1,
            Puntuacion = 5,
            Comentario = "Excelente trabajo",
            FechaCalificacion = DateTime.Now
        };
        var calificacion2 = new Domain.Entities.Calificaciones.Calificacion
        {
            ContratistaId = contratista.Id,
            EmpleadorId = 1,
            Puntuacion = 4,
            Comentario = "Muy bueno",
            FechaCalificacion = DateTime.Now
        };
        DbContext.Calificaciones.AddRange(calificacion1, calificacion2);
        await DbContext.SaveChangesAsync();

        // Act
        var response = await Client.GetAsync($"/api/calificaciones/contratista/{contratista.Id}/promedio");

        // Assert
        response.IsSuccessStatusCode.Should().BeTrue();
        var promedio = await response.Content.ReadFromJsonAsync<decimal>();
        promedio.Should().Be(4.5m); // (5 + 4) / 2
    }

    #endregion
}


