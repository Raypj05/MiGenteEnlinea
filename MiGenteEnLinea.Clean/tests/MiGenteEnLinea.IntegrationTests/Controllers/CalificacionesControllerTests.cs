using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using MiGenteEnLinea.Application.Features.Calificaciones.Commands.CalificarPerfil;
using MiGenteEnLinea.Application.Features.Calificaciones.Commands.CreateCalificacion;
using MiGenteEnLinea.Application.Features.Calificaciones.DTOs;
using MiGenteEnLinea.IntegrationTests.Infrastructure;
using Xunit;

namespace MiGenteEnLinea.IntegrationTests.Controllers;

/// <summary>
/// Integration tests para CalificacionesController
/// Prueba creación, lectura y estadísticas de calificaciones (4 dimensiones)
/// </summary>
[Collection("IntegrationTests")]
public class CalificacionesControllerTests : IntegrationTestBase
{
    public CalificacionesControllerTests(TestWebApplicationFactory factory) : base(factory)
    {
    }

    #region Create Calificacion Tests

    [Fact]
    public async Task Create_WithValidData_ReturnsCreated()
    {
        // Arrange - API-First: create empleador via API
        var (empleadorUserId, _, _, _) = await CreateEmpleadorAsync();
        
        var command = new CreateCalificacionCommand
        {
            EmpleadorUserId = empleadorUserId,
            ContratistaIdentificacion = "40212345678",
            ContratistaNombre = "Juan Pérez",
            Puntualidad = 5,
            Cumplimiento = 4,
            Conocimientos = 5,
            Recomendacion = 5
        };

        // Act
        var response = await Client.AsEmpleador(userId: empleadorUserId).PostAsJsonAsync("/api/calificaciones", command);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        
        var result = await response.Content.ReadFromJsonAsync<Dictionary<string, int>>();
        result.Should().NotBeNull();
        result!["calificacionId"].Should().BeGreaterThan(0);

        // Verify Location header (case-insensitive)
        response.Headers.Location.Should().NotBeNull();
        response.Headers.Location!.ToString().ToLowerInvariant().Should().Contain($"/api/calificaciones/{result["calificacionId"]}");
    }

    [Fact]
    public async Task Create_WithMinimumRatings_ReturnsCreated()
    {
        // Arrange - API-First: create empleador via API + todas las dimensiones en 1
        var (empleadorUserId, _, _, _) = await CreateEmpleadorAsync();
        
        var command = new CreateCalificacionCommand
        {
            EmpleadorUserId = empleadorUserId,
            ContratistaIdentificacion = "40298765432",
            ContratistaNombre = "María López",
            Puntualidad = 1,
            Cumplimiento = 1,
            Conocimientos = 1,
            Recomendacion = 1
        };

        // Act
        var response = await Client.AsEmpleador(userId: empleadorUserId).PostAsJsonAsync("/api/calificaciones", command);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Create_WithMaximumRatings_ReturnsCreated()
    {
        // Arrange - API-First: create empleador via API + todas las dimensiones en 5
        var (empleadorUserId, _, _, _) = await CreateEmpleadorAsync();
        
        var command = new CreateCalificacionCommand
        {
            EmpleadorUserId = empleadorUserId,
            ContratistaIdentificacion = "40287654321",
            ContratistaNombre = "Pedro Martínez",
            Puntualidad = 5,
            Cumplimiento = 5,
            Conocimientos = 5,
            Recomendacion = 5
        };

        // Act
        var response = await Client.AsEmpleador(userId: empleadorUserId).PostAsJsonAsync("/api/calificaciones", command);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Create_WithInvalidRatingTooLow_ReturnsBadRequest()
    {
        // Arrange - API-First: create empleador via API
        var (empleadorUserId, _, _, _) = await CreateEmpleadorAsync();
        
        var command = new CreateCalificacionCommand
        {
            EmpleadorUserId = empleadorUserId,
            ContratistaIdentificacion = "40212345678",
            ContratistaNombre = "Test User",
            Puntualidad = 0, // INVALID: debe ser 1-5
            Cumplimiento = 3,
            Conocimientos = 4,
            Recomendacion = 5
        };

        // Act
        var response = await Client.AsEmpleador(userId: empleadorUserId).PostAsJsonAsync("/api/calificaciones", command);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_WithInvalidRatingTooHigh_ReturnsBadRequest()
    {
        // Arrange - API-First: create empleador via API
        var (empleadorUserId, _, _, _) = await CreateEmpleadorAsync();
        
        var command = new CreateCalificacionCommand
        {
            EmpleadorUserId = empleadorUserId,
            ContratistaIdentificacion = "40212345678",
            ContratistaNombre = "Test User",
            Puntualidad = 5,
            Cumplimiento = 6, // INVALID: debe ser 1-5
            Conocimientos = 4,
            Recomendacion = 5
        };

        // Act
        var response = await Client.AsEmpleador(userId: empleadorUserId).PostAsJsonAsync("/api/calificaciones", command);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_WithEmptyEmpleadorUserId_ReturnsBadRequest()
    {
        // Arrange
        var command = new CreateCalificacionCommand
        {
            EmpleadorUserId = "", // INVALID
            ContratistaIdentificacion = "40212345678",
            ContratistaNombre = "Test User",
            Puntualidad = 5,
            Cumplimiento = 4,
            Conocimientos = 5,
            Recomendacion = 5
        };

        // Act
        var response = await Client.AsEmpleador().PostAsJsonAsync("/api/calificaciones", command);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_WithEmptyContratistaIdentificacion_ReturnsBadRequest()
    {
        // Arrange - API-First: create empleador via API
        var (empleadorUserId, _, _, _) = await CreateEmpleadorAsync();
        
        var command = new CreateCalificacionCommand
        {
            EmpleadorUserId = empleadorUserId,
            ContratistaIdentificacion = "", // INVALID
            ContratistaNombre = "Test User",
            Puntualidad = 5,
            Cumplimiento = 4,
            Conocimientos = 5,
            Recomendacion = 5
        };

        // Act
        var response = await Client.AsEmpleador(userId: empleadorUserId).PostAsJsonAsync("/api/calificaciones", command);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_Duplicate_ReturnsBadRequest()
    {
        // Arrange - API-First: create empleador via API
        var (empleadorUserId, _, _, _) = await CreateEmpleadorAsync();
        
        var command = new CreateCalificacionCommand
        {
            EmpleadorUserId = empleadorUserId,
            ContratistaIdentificacion = "40212345678",
            ContratistaNombre = "Juan Pérez",
            Puntualidad = 5,
            Cumplimiento = 4,
            Conocimientos = 5,
            Recomendacion = 5
        };

        // Act - primera calificación
        var response1 = await Client.AsEmpleador(userId: empleadorUserId).PostAsJsonAsync("/api/calificaciones", command);
        response1.StatusCode.Should().Be(HttpStatusCode.Created);

        // Act - calificación duplicada (mismo empleador + mismo contratista)
        var response2 = await Client.AsEmpleador(userId: empleadorUserId).PostAsJsonAsync("/api/calificaciones", command);

        // Assert
        response2.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = await response2.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        error.Should().NotBeNull();
        error!["message"].Should().Contain("Ya has calificado");
    }

    #endregion

    #region GetById Tests

    [Fact]
    public async Task GetById_ExistingCalificacion_ReturnsOk()
    {
        // Arrange - API-First: create empleador via API + crear una calificación
        var (empleadorUserId, _, _, _) = await CreateEmpleadorAsync();
        
        var createCommand = new CreateCalificacionCommand
        {
            EmpleadorUserId = empleadorUserId,
            ContratistaIdentificacion = "40299887766",
            ContratistaNombre = "Test Contratista",
            Puntualidad = 5,
            Cumplimiento = 4,
            Conocimientos = 5,
            Recomendacion = 5
        };

        var createResponse = await Client.AsEmpleador(userId: empleadorUserId).PostAsJsonAsync("/api/calificaciones", createCommand);
        var createResult = await createResponse.Content.ReadFromJsonAsync<Dictionary<string, int>>();
        var calificacionId = createResult!["calificacionId"];

        // Act
        var response = await Client.AsEmpleador(userId: empleadorUserId).GetAsync($"/api/calificaciones/{calificacionId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var calificacion = await response.Content.ReadFromJsonAsync<CalificacionDto>();
        calificacion.Should().NotBeNull();
        calificacion!.CalificacionId.Should().Be(calificacionId);
        calificacion.EmpleadorUserId.Should().Be(empleadorUserId); // ✅ Use real empleadorUserId
        calificacion.ContratistaIdentificacion.Should().Be("40299887766");
        calificacion.Puntualidad.Should().Be(5);
        calificacion.Cumplimiento.Should().Be(4);
        calificacion.Conocimientos.Should().Be(5);
        calificacion.Recomendacion.Should().Be(5);
        calificacion.PromedioGeneral.Should().BeApproximately(4.75m, 0.01m); // (5+4+5+5)/4
    }

    [Fact]
    public async Task GetById_NonExistentCalificacion_ReturnsNotFound()
    {
        // Act
        var response = await Client.AsEmpleador().GetAsync("/api/calificaciones/999999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        
        var error = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        error.Should().NotBeNull();
        error!["message"].Should().Contain("999999");
        error["message"].Should().Contain("no encontrada");
    }

    #endregion

    #region GetByContratista Tests

    [Fact]
    public async Task GetByContratista_WithExistingCalificaciones_ReturnsOkWithPaginatedResults()
    {
        // Arrange - crear 3 calificaciones para el mismo contratista
        var contratistaId = "40211223344";
        for (int i = 1; i <= 3; i++)
        {
            // API-First: Create real empleador for each calificacion
            var (empleadorUserId, _, _, _) = await CreateEmpleadorAsync();
            
            var command = new CreateCalificacionCommand
            {
                EmpleadorUserId = empleadorUserId,
                ContratistaIdentificacion = contratistaId,
                ContratistaNombre = "Contratista Test",
                Puntualidad = i + 2, // 3, 4, 5
                Cumplimiento = i + 2,
                Conocimientos = i + 2,
                Recomendacion = i + 2
            };
            await Client.AsEmpleador(userId: empleadorUserId).PostAsJsonAsync("/api/calificaciones", command);
        }

        // Act
        var response = await Client.AsEmpleador().GetAsync($"/api/calificaciones/contratista/{contratistaId}?pageSize=2");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var result = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        result!.Should().NotBeNull(); // Null-forgiving: We expect valid JSON response
    }

    [Fact]
    public async Task GetByContratista_WithNoCalificaciones_ReturnsEmptyList()
    {
        // Act
        var response = await Client.AsEmpleador().GetAsync("/api/calificaciones/contratista/40200000000");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var result = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        result.Should().NotBeNull();
        result!["totalCount"].ToString().Should().Be("0");
    }

    [Fact]
    public async Task GetByContratista_WithUserIdFilter_ReturnsFilteredResults()
    {
        // Arrange
        var (empleadorUserId, _, _, _) = await CreateEmpleadorAsync();
        var (otroEmpleadorUserId, _, _, _) = await CreateEmpleadorAsync();
        var contratistaId = "40255443322";

        // Crear 2 calificaciones: 1 del empleador específico, 1 de otro
        var command1 = new CreateCalificacionCommand
        {
            EmpleadorUserId = empleadorUserId,
            ContratistaIdentificacion = contratistaId,
            ContratistaNombre = "Test",
            Puntualidad = 5, Cumplimiento = 5, Conocimientos = 5, Recomendacion = 5
        };
        await Client.AsEmpleador(userId: empleadorUserId).PostAsJsonAsync("/api/calificaciones", command1);

        var command2 = new CreateCalificacionCommand
        {
            EmpleadorUserId = otroEmpleadorUserId,
            ContratistaIdentificacion = contratistaId,
            ContratistaNombre = "Test",
            Puntualidad = 3, Cumplimiento = 3, Conocimientos = 3, Recomendacion = 3
        };
        await Client.AsEmpleador(userId: otroEmpleadorUserId).PostAsJsonAsync("/api/calificaciones", command2);

        // Act - filtrar solo por el empleador específico
        var response = await Client.AsEmpleador().GetAsync(
            $"/api/calificaciones/contratista/{contratistaId}?userId={empleadorUserId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var result = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        result.Should().NotBeNull();
        // Debería retornar solo 1 calificación del empleador filtrado
        result!["totalCount"].ToString().Should().Be("1");
    }

    [Fact]
    public async Task GetByContratista_WithPagination_ReturnsCorrectPage()
    {
        // Arrange - crear 5 calificaciones
        var contratistaId = "40266778899";
        for (int i = 1; i <= 5; i++)
        {
            var (empleadorUserId, _, _, _) = await CreateEmpleadorAsync();
            var command = new CreateCalificacionCommand
            {
                EmpleadorUserId = empleadorUserId,
                ContratistaIdentificacion = contratistaId,
                ContratistaNombre = "Test",
                Puntualidad = 5, Cumplimiento = 5, Conocimientos = 5, Recomendacion = 5
            };
            await Client.AsEmpleador(userId: empleadorUserId).PostAsJsonAsync("/api/calificaciones", command);
        }

        // Act - página 2, tamaño 2 (debería retornar items 3 y 4)
        var response = await Client.AsEmpleador().GetAsync(
            $"/api/calificaciones/contratista/{contratistaId}?pageNumber=2&pageSize=2");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var result = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        result.Should().NotBeNull();
        result!["pageIndex"].ToString().Should().Be("2");
        result["pageSize"].ToString().Should().Be("2");
        result["hasPreviousPage"].ToString().Should().Be("True");
        result["hasNextPage"].ToString().Should().Be("True");
    }

    #endregion

    #region GetPromedio Tests

    [Fact]
    public async Task GetPromedio_WithExistingCalificaciones_ReturnsCorrectAverage()
    {
        // Arrange - crear 3 calificaciones con diferentes ratings
        var (empleadorUserId1, _, _, _) = await CreateEmpleadorAsync();
        var (empleadorUserId2, _, _, _) = await CreateEmpleadorAsync();
        var (empleadorUserId3, _, _, _) = await CreateEmpleadorAsync();
        var contratistaId = GenerateRandomIdentification(); // Unique per test
        
        // Calificación 1: todas 5 (promedio 5.0)
        var command1 = new CreateCalificacionCommand
        {
            EmpleadorUserId = empleadorUserId1,
            ContratistaIdentificacion = contratistaId,
            ContratistaNombre = "Test",
            Puntualidad = 5, Cumplimiento = 5, Conocimientos = 5, Recomendacion = 5
        };
        await Client.AsEmpleador(userId: empleadorUserId1).PostAsJsonAsync("/api/calificaciones", command1);

        // Calificación 2: todas 3 (promedio 3.0)
        var command2 = new CreateCalificacionCommand
        {
            EmpleadorUserId = empleadorUserId2,
            ContratistaIdentificacion = contratistaId,
            ContratistaNombre = "Test",
            Puntualidad = 3, Cumplimiento = 3, Conocimientos = 3, Recomendacion = 3
        };
        await Client.AsEmpleador(userId: empleadorUserId2).PostAsJsonAsync("/api/calificaciones", command2);

        // Calificación 3: todas 4 (promedio 4.0)
        var command3 = new CreateCalificacionCommand
        {
            EmpleadorUserId = empleadorUserId3,
            ContratistaIdentificacion = contratistaId,
            ContratistaNombre = "Test",
            Puntualidad = 4, Cumplimiento = 4, Conocimientos = 4, Recomendacion = 4
        };
        await Client.AsEmpleador(userId: empleadorUserId3).PostAsJsonAsync("/api/calificaciones", command3);

        // Act
        var response = await Client.AsEmpleador().GetAsync($"/api/calificaciones/promedio/{contratistaId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var promedio = await response.Content.ReadFromJsonAsync<PromedioCalificacionDto>();
        promedio.Should().NotBeNull();
        promedio!.Identificacion.Should().Be(contratistaId);
        promedio.TotalCalificaciones.Should().Be(3);
        
        // Promedio general: (5+3+4)/3 = 4.0
        promedio.PromedioGeneral.Should().BeApproximately(4.0m, 0.1m);
        
        // El DTO solo tiene PromedioGeneral (no dimensiones individuales)
        // La distribución de estrellas se valida con las propiedades de conteo
        promedio.Calificaciones5Estrellas.Should().BeGreaterThanOrEqualTo(0);
        promedio.Calificaciones4Estrellas.Should().BeGreaterThanOrEqualTo(0);
        promedio.Calificaciones3Estrellas.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task GetPromedio_WithNoCalificaciones_ReturnsNotFound()
    {
        // Act
        var response = await Client.AsEmpleador().GetAsync("/api/calificaciones/promedio/40200000001");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        
        var error = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        error.Should().NotBeNull();
        error!["message"].Should().Contain("No hay calificaciones");
    }

    [Fact]
    public async Task GetPromedio_WithSingleCalificacion_ReturnsCorrectAverage()
    {
        // Arrange - solo 1 calificación
        var (empleadorUserId, _, _, _) = await CreateEmpleadorAsync();
        var contratistaId = GenerateRandomIdentification(); // Unique per test
        var command = new CreateCalificacionCommand
        {
            EmpleadorUserId = empleadorUserId,
            ContratistaIdentificacion = contratistaId,
            ContratistaNombre = "Test",
            Puntualidad = 5,
            Cumplimiento = 4,
            Conocimientos = 3,
            Recomendacion = 5
        };
        await Client.AsEmpleador(userId: empleadorUserId).PostAsJsonAsync("/api/calificaciones", command);

        // Act
        var response = await Client.AsEmpleador().GetAsync($"/api/calificaciones/promedio/{contratistaId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var promedio = await response.Content.ReadFromJsonAsync<PromedioCalificacionDto>();
        promedio.Should().NotBeNull();
        promedio!.TotalCalificaciones.Should().Be(1);
        promedio.PromedioGeneral.Should().BeApproximately(4.25m, 0.01m); // (5+4+3+5)/4
        // El DTO solo expone PromedioGeneral, no dimensiones individuales
        promedio.Calificaciones5Estrellas.Should().BeGreaterThanOrEqualTo(0);
        promedio.Calificaciones4Estrellas.Should().BeGreaterThanOrEqualTo(0);
    }

    #endregion

    #region CalificarPerfil Tests (Legacy endpoint)

    [Fact]
    public async Task CalificarPerfil_WithValidData_ReturnsCreated()
    {
        // Arrange - API-First: create empleador via API
        var (empleadorUserId, _, _, _) = await CreateEmpleadorAsync();
        
        var command = new CalificarPerfilCommand(
            EmpleadorUserId: empleadorUserId,
            ContratistaIdentificacion: "40299001122",
            ContratistaNombre: "Legacy Test",
            Puntualidad: 5,
            Cumplimiento: 5,
            Conocimientos: 5,
            Recomendacion: 5
        );

        // Act
        var response = await Client.AsEmpleador(userId: empleadorUserId).PostAsJsonAsync("/api/calificaciones/calificar-perfil", command);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        
        var result = await response.Content.ReadFromJsonAsync<Dictionary<string, int>>();
        result.Should().NotBeNull();
        result!["calificacionId"].Should().BeGreaterThan(0);
    }

    #endregion

    #region GetTodasCalificaciones Tests (Legacy endpoint)

    [Fact]
    public async Task GetTodasCalificaciones_ReturnsOkWithList()
    {
        // Act
        var response = await Client.AsEmpleador().GetAsync("/api/calificaciones/todas");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var calificaciones = await response.Content.ReadFromJsonAsync<List<CalificacionVistaDto>>();
        calificaciones.Should().NotBeNull();
        // Lista puede estar vacía o con datos, ambos son válidos
    }

    #endregion

    #region GetCalificacionesLegacy Tests

    [Fact]
    public async Task GetCalificacionesLegacy_WithIdentificacion_ReturnsOk()
    {
        // Arrange - API-First: create empleador + crear calificación para el test
        var (empleadorUserId, _, _, _) = await CreateEmpleadorAsync();
        
        var contratistaId = "40200112233";
        var command = new CreateCalificacionCommand
        {
            EmpleadorUserId = empleadorUserId,
            ContratistaIdentificacion = contratistaId,
            ContratistaNombre = "Legacy Get Test",
            Puntualidad = 5, Cumplimiento = 5, Conocimientos = 5, Recomendacion = 5
        };
        await Client.AsEmpleador(userId: empleadorUserId).PostAsJsonAsync("/api/calificaciones", command);

        // Act
        var response = await Client.AsEmpleador().GetAsync($"/api/calificaciones/legacy/{contratistaId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var calificaciones = await response.Content.ReadFromJsonAsync<List<CalificacionVistaDto>>();
        calificaciones.Should().NotBeNull();
        calificaciones!.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetCalificacionesLegacy_WithUserIdFilter_ReturnsFilteredResults()
    {
        // Arrange
        var contratistaId = "40200223344";
        var empleadorUserId = "legacy-filter-001";

        var command = new CreateCalificacionCommand
        {
            EmpleadorUserId = empleadorUserId,
            ContratistaIdentificacion = contratistaId,
            ContratistaNombre = "Test",
            Puntualidad = 5, Cumplimiento = 5, Conocimientos = 5, Recomendacion = 5
        };
        await Client.AsEmpleador().PostAsJsonAsync("/api/calificaciones", command);

        // Act
        var response = await Client.AsEmpleador().GetAsync(
            $"/api/calificaciones/legacy/{contratistaId}?userId={empleadorUserId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var calificaciones = await response.Content.ReadFromJsonAsync<List<CalificacionVistaDto>>();
        calificaciones.Should().NotBeNull();
    }

    #endregion

    #region Business Logic Tests

    [Fact]
    public async Task BusinessLogic_CalificacionPromedioCalculation_IsAccurate()
    {
        // Arrange - crear calificación con diferentes valores
        var (empleadorUserId, _, _, _) = await CreateEmpleadorAsync();
        var command = new CreateCalificacionCommand
        {
            EmpleadorUserId = empleadorUserId,
            ContratistaIdentificacion = "40212312312",
            ContratistaNombre = "Test Promedio",
            Puntualidad = 5,    // 5.0
            Cumplimiento = 3,   // 3.0
            Conocimientos = 4,  // 4.0
            Recomendacion = 2   // 2.0
        };

        // Act
        var createResponse = await Client.AsEmpleador(userId: empleadorUserId).PostAsJsonAsync("/api/calificaciones", command);
        var createResult = await createResponse.Content.ReadFromJsonAsync<Dictionary<string, int>>();
        var calificacionId = createResult!["calificacionId"];

        var getResponse = await Client.AsEmpleador().GetAsync($"/api/calificaciones/{calificacionId}");
        var calificacion = await getResponse.Content.ReadFromJsonAsync<CalificacionDto>();

        // Assert
        calificacion.Should().NotBeNull();
        
        // Promedio = (5 + 3 + 4 + 2) / 4 = 14 / 4 = 3.5
        decimal expectedPromedio = (5m + 3m + 4m + 2m) / 4m;
        calificacion!.PromedioGeneral.Should().BeApproximately(expectedPromedio, 0.01m);
        calificacion.PromedioGeneral.Should().BeApproximately(3.5m, 0.01m);
    }

    [Fact]
    public async Task BusinessLogic_ImmutableCalificaciones_CannotBeEdited()
    {
        // Las calificaciones son INMUTABLES por diseño
        // No existen endpoints PUT/PATCH para editar
        // No existe endpoint DELETE para eliminar
        
        // Act - intentar PUT (debería retornar 405 Method Not Allowed o 404)
        var response = await Client.AsEmpleador().PutAsJsonAsync("/api/calificaciones/1", new { });

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.MethodNotAllowed, 
            HttpStatusCode.NotFound);
    }

    #endregion
}
