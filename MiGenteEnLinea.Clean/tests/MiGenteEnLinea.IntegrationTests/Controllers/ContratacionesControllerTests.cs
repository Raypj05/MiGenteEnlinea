using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using MiGenteEnLinea.Application.Features.Contrataciones.Commands.AcceptContratacion;
using MiGenteEnLinea.Application.Features.Contrataciones.Commands.CancelContratacion;
using MiGenteEnLinea.Application.Features.Contrataciones.Commands.CompleteContratacion;
using MiGenteEnLinea.Application.Features.Contrataciones.Commands.CreateContratacion;
using MiGenteEnLinea.Application.Features.Contrataciones.Commands.RejectContratacion;
using MiGenteEnLinea.Application.Features.Contrataciones.Commands.StartContratacion;
using MiGenteEnLinea.Application.Features.Contrataciones.DTOs;
using MiGenteEnLinea.IntegrationTests.Infrastructure;
using Xunit;

namespace MiGenteEnLinea.IntegrationTests.Controllers;

/// <summary>
/// Integration tests para ContratacionesController
/// Prueba el workflow completo de contratación:
/// 1. Create (Pendiente) → 2. Accept/Reject → 3. Start → 4. Complete/Cancel
/// </summary>
[Collection("Sequential")]
public class ContratacionesControllerTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly TestWebApplicationFactory _factory;

    public ContratacionesControllerTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    #region Create Contratacion Tests

    [Fact]
    public async Task Create_WithValidData_ReturnsOkWithDetalleId()
    {
        // Arrange
        var command = new CreateContratacionCommand
        {
            ContratacionId = null,
            DescripcionCorta = "Reparación de plomería en baño principal",
            DescripcionAmpliada = "Reparación de tubería rota, cambio de llave mezcladora y sellado de filtraciones",
            FechaInicio = DateOnly.FromDateTime(DateTime.Now.AddDays(2)),
            FechaFinal = DateOnly.FromDateTime(DateTime.Now.AddDays(5)),
            MontoAcordado = 5000.00m,
            EsquemaPagos = "50% adelanto, 50% al finalizar",
            Notas = "Trabajo urgente - Cliente disponible de 8am a 5pm"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/contrataciones", command);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var detalleId = await response.Content.ReadFromJsonAsync<int>();
        detalleId.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Create_WithMinimumRequiredData_ReturnsOk()
    {
        // Arrange - solo campos requeridos
        var command = new CreateContratacionCommand
        {
            DescripcionCorta = "Limpieza general",
            FechaInicio = DateOnly.FromDateTime(DateTime.Now.AddDays(1)),
            FechaFinal = DateOnly.FromDateTime(DateTime.Now.AddDays(1)),
            MontoAcordado = 1000.00m
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/contrataciones", command);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Create_WithNegativeMontoAcordado_ReturnsBadRequest()
    {
        // Arrange
        var command = new CreateContratacionCommand
        {
            DescripcionCorta = "Test inválido",
            FechaInicio = DateOnly.FromDateTime(DateTime.Now.AddDays(1)),
            FechaFinal = DateOnly.FromDateTime(DateTime.Now.AddDays(2)),
            MontoAcordado = -100.00m // INVALID
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/contrataciones", command);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_WithEmptyDescripcionCorta_ReturnsBadRequest()
    {
        // Arrange
        var command = new CreateContratacionCommand
        {
            DescripcionCorta = "", // INVALID - Required
            FechaInicio = DateOnly.FromDateTime(DateTime.Now.AddDays(1)),
            FechaFinal = DateOnly.FromDateTime(DateTime.Now.AddDays(2)),
            MontoAcordado = 1000m
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/contrataciones", command);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_WithFechaInicioBeforeFechaFinal_IsValid()
    {
        // Arrange
        var command = new CreateContratacionCommand
        {
            DescripcionCorta = "Prueba fechas válidas",
            FechaInicio = DateOnly.FromDateTime(DateTime.Now.AddDays(1)),
            FechaFinal = DateOnly.FromDateTime(DateTime.Now.AddDays(10)),
            MontoAcordado = 2000m
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/contrataciones", command);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    #endregion

    #region GetById Tests

    [Fact]
    public async Task GetById_WithExistingId_ReturnsContratacionDto()
    {
        // Arrange - crear contratación primero
        var createCommand = new CreateContratacionCommand
        {
            DescripcionCorta = "Test GetById",
            FechaInicio = DateOnly.FromDateTime(DateTime.Now.AddDays(1)),
            FechaFinal = DateOnly.FromDateTime(DateTime.Now.AddDays(3)),
            MontoAcordado = 1500m
        };
        var createResponse = await _client.PostAsJsonAsync("/api/contrataciones", createCommand);
        var detalleId = await createResponse.Content.ReadFromJsonAsync<int>();

        // Act
        var response = await _client.GetAsync($"/api/contrataciones/{detalleId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var contratacion = await response.Content.ReadFromJsonAsync<ContratacionDto>();
        contratacion.Should().NotBeNull();
        contratacion!.DetalleId.Should().Be(detalleId);
        contratacion.DescripcionCorta.Should().Be("Test GetById");
        contratacion.MontoAcordado.Should().Be(1500m);
        contratacion.Estatus.Should().Be(1); // Pendiente
    }

    [Fact]
    public async Task GetById_WithNonExistingId_ReturnsNotFound()
    {
        // Act
        var response = await _client.GetAsync("/api/contrataciones/999999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    #endregion

    #region GetAll Tests

    [Fact]
    public async Task GetAll_ReturnsListOfContrataciones()
    {
        // Arrange - crear algunas contrataciones
        var command1 = new CreateContratacionCommand
        {
            DescripcionCorta = "Contratación 1",
            FechaInicio = DateOnly.FromDateTime(DateTime.Now.AddDays(1)),
            FechaFinal = DateOnly.FromDateTime(DateTime.Now.AddDays(2)),
            MontoAcordado = 1000m
        };
        var command2 = new CreateContratacionCommand
        {
            DescripcionCorta = "Contratación 2",
            FechaInicio = DateOnly.FromDateTime(DateTime.Now.AddDays(3)),
            FechaFinal = DateOnly.FromDateTime(DateTime.Now.AddDays(4)),
            MontoAcordado = 2000m
        };
        await _client.PostAsJsonAsync("/api/contrataciones", command1);
        await _client.PostAsJsonAsync("/api/contrataciones", command2);

        // Act
        var response = await _client.GetAsync("/api/contrataciones");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var contrataciones = await response.Content.ReadFromJsonAsync<List<ContratacionDto>>();
        contrataciones.Should().NotBeNull();
        contrataciones!.Count.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GetAll_WithEstatusFilter_ReturnsFilteredList()
    {
        // Arrange
        var command = new CreateContratacionCommand
        {
            DescripcionCorta = "Test filtro estatus",
            FechaInicio = DateOnly.FromDateTime(DateTime.Now.AddDays(1)),
            FechaFinal = DateOnly.FromDateTime(DateTime.Now.AddDays(2)),
            MontoAcordado = 1000m
        };
        await _client.PostAsJsonAsync("/api/contrataciones", command);

        // Act
        var response = await _client.GetAsync("/api/contrataciones?estatus=1"); // Pendiente

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var contrataciones = await response.Content.ReadFromJsonAsync<List<ContratacionDto>>();
        contrataciones.Should().NotBeNull();
        contrataciones!.All(c => c.Estatus == 1).Should().BeTrue();
    }

    [Fact]
    public async Task GetAll_WithPagination_ReturnsPagedResults()
    {
        // Act
        var response = await _client.GetAsync("/api/contrataciones?pageNumber=1&pageSize=10");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var contrataciones = await response.Content.ReadFromJsonAsync<List<ContratacionDto>>();
        contrataciones.Should().NotBeNull();
        contrataciones!.Count.Should().BeLessThanOrEqualTo(10);
    }

    #endregion

    #region Accept Workflow Tests

    [Fact]
    public async Task Accept_WithPendienteStatus_ReturnsOk()
    {
        // Arrange - crear contratación en estado Pendiente
        var createCommand = new CreateContratacionCommand
        {
            DescripcionCorta = "Test Accept",
            FechaInicio = DateOnly.FromDateTime(DateTime.Now.AddDays(1)),
            FechaFinal = DateOnly.FromDateTime(DateTime.Now.AddDays(3)),
            MontoAcordado = 2000m
        };
        var createResponse = await _client.PostAsJsonAsync("/api/contrataciones", createCommand);
        var detalleId = await createResponse.Content.ReadFromJsonAsync<int>();

        // Act
        var response = await _client.PutAsync($"/api/contrataciones/{detalleId}/accept", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify estado cambió a Aceptada (2)
        var getResponse = await _client.GetAsync($"/api/contrataciones/{detalleId}");
        var contratacion = await getResponse.Content.ReadFromJsonAsync<ContratacionDto>();
        contratacion!.Estatus.Should().Be(2); // Aceptada
    }

    [Fact]
    public async Task Accept_WithNonPendienteStatus_ReturnsBadRequest()
    {
        // Arrange - crear y aceptar contratación
        var createCommand = new CreateContratacionCommand
        {
            DescripcionCorta = "Test double accept",
            FechaInicio = DateOnly.FromDateTime(DateTime.Now.AddDays(1)),
            FechaFinal = DateOnly.FromDateTime(DateTime.Now.AddDays(3)),
            MontoAcordado = 2000m
        };
        var createResponse = await _client.PostAsJsonAsync("/api/contrataciones", createCommand);
        var detalleId = await createResponse.Content.ReadFromJsonAsync<int>();
        await _client.PutAsync($"/api/contrataciones/{detalleId}/accept", null);

        // Act - intentar aceptar de nuevo
        var response = await _client.PutAsync($"/api/contrataciones/{detalleId}/accept", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Accept_WithNonExistingId_ReturnsNotFound()
    {
        // Act
        var response = await _client.PutAsync("/api/contrataciones/999999/accept", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    #endregion

    #region Reject Workflow Tests

    [Fact]
    public async Task Reject_WithValidMotivo_ReturnsOk()
    {
        // Arrange
        var createCommand = new CreateContratacionCommand
        {
            DescripcionCorta = "Test Reject",
            FechaInicio = DateOnly.FromDateTime(DateTime.Now.AddDays(1)),
            FechaFinal = DateOnly.FromDateTime(DateTime.Now.AddDays(3)),
            MontoAcordado = 2000m
        };
        var createResponse = await _client.PostAsJsonAsync("/api/contrataciones", createCommand);
        var detalleId = await createResponse.Content.ReadFromJsonAsync<int>();

        var rejectCommand = new RejectContratacionCommand
        {
            DetalleId = detalleId,
            Motivo = "No estoy disponible en esas fechas"
        };

        // Act
        var response = await _client.PutAsJsonAsync($"/api/contrataciones/{detalleId}/reject", rejectCommand);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify estado cambió a Rechazada (6)
        var getResponse = await _client.GetAsync($"/api/contrataciones/{detalleId}");
        var contratacion = await getResponse.Content.ReadFromJsonAsync<ContratacionDto>();
        contratacion!.Estatus.Should().Be(6); // Rechazada
    }

    [Fact]
    public async Task Reject_WithEmptyMotivo_ReturnsBadRequest()
    {
        // Arrange
        var createCommand = new CreateContratacionCommand
        {
            DescripcionCorta = "Test empty motivo",
            FechaInicio = DateOnly.FromDateTime(DateTime.Now.AddDays(1)),
            FechaFinal = DateOnly.FromDateTime(DateTime.Now.AddDays(3)),
            MontoAcordado = 2000m
        };
        var createResponse = await _client.PostAsJsonAsync("/api/contrataciones", createCommand);
        var detalleId = await createResponse.Content.ReadFromJsonAsync<int>();

        var rejectCommand = new RejectContratacionCommand
        {
            DetalleId = detalleId,
            Motivo = "" // INVALID
        };

        // Act
        var response = await _client.PutAsJsonAsync($"/api/contrataciones/{detalleId}/reject", rejectCommand);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Reject_WithNonPendienteStatus_ReturnsBadRequest()
    {
        // Arrange - crear y aceptar contratación
        var createCommand = new CreateContratacionCommand
        {
            DescripcionCorta = "Test reject accepted",
            FechaInicio = DateOnly.FromDateTime(DateTime.Now.AddDays(1)),
            FechaFinal = DateOnly.FromDateTime(DateTime.Now.AddDays(3)),
            MontoAcordado = 2000m
        };
        var createResponse = await _client.PostAsJsonAsync("/api/contrataciones", createCommand);
        var detalleId = await createResponse.Content.ReadFromJsonAsync<int>();
        await _client.PutAsync($"/api/contrataciones/{detalleId}/accept", null);

        var rejectCommand = new RejectContratacionCommand
        {
            DetalleId = detalleId,
            Motivo = "Test motivo"
        };

        // Act
        var response = await _client.PutAsJsonAsync($"/api/contrataciones/{detalleId}/reject", rejectCommand);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Reject_WithNonExistingId_ReturnsNotFound()
    {
        // Arrange
        var rejectCommand = new RejectContratacionCommand
        {
            DetalleId = 999999,
            Motivo = "Test motivo"
        };

        // Act
        var response = await _client.PutAsJsonAsync("/api/contrataciones/999999/reject", rejectCommand);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    #endregion

    #region Start Workflow Tests

    [Fact]
    public async Task Start_WithAceptadaStatus_ReturnsOk()
    {
        // Arrange - crear y aceptar contratación
        var createCommand = new CreateContratacionCommand
        {
            DescripcionCorta = "Test Start",
            FechaInicio = DateOnly.FromDateTime(DateTime.Now.AddDays(1)),
            FechaFinal = DateOnly.FromDateTime(DateTime.Now.AddDays(3)),
            MontoAcordado = 2000m
        };
        var createResponse = await _client.PostAsJsonAsync("/api/contrataciones", createCommand);
        var detalleId = await createResponse.Content.ReadFromJsonAsync<int>();
        await _client.PutAsync($"/api/contrataciones/{detalleId}/accept", null);

        // Act
        var response = await _client.PutAsync($"/api/contrataciones/{detalleId}/start", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify estado cambió a EnProgreso (5)
        var getResponse = await _client.GetAsync($"/api/contrataciones/{detalleId}");
        var contratacion = await getResponse.Content.ReadFromJsonAsync<ContratacionDto>();
        contratacion!.Estatus.Should().Be(5); // EnProgreso
        contratacion.FechaInicioReal.Should().NotBeNull();
    }

    [Fact]
    public async Task Start_WithNonAceptadaStatus_ReturnsBadRequest()
    {
        // Arrange - crear sin aceptar
        var createCommand = new CreateContratacionCommand
        {
            DescripcionCorta = "Test start pendiente",
            FechaInicio = DateOnly.FromDateTime(DateTime.Now.AddDays(1)),
            FechaFinal = DateOnly.FromDateTime(DateTime.Now.AddDays(3)),
            MontoAcordado = 2000m
        };
        var createResponse = await _client.PostAsJsonAsync("/api/contrataciones", createCommand);
        var detalleId = await createResponse.Content.ReadFromJsonAsync<int>();

        // Act
        var response = await _client.PutAsync($"/api/contrataciones/{detalleId}/start", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Start_WithNonExistingId_ReturnsNotFound()
    {
        // Act
        var response = await _client.PutAsync("/api/contrataciones/999999/start", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    #endregion

    #region Complete Workflow Tests

    [Fact]
    public async Task Complete_WithEnProgresoStatus_ReturnsOk()
    {
        // Arrange - crear, aceptar e iniciar
        var createCommand = new CreateContratacionCommand
        {
            DescripcionCorta = "Test Complete",
            FechaInicio = DateOnly.FromDateTime(DateTime.Now.AddDays(1)),
            FechaFinal = DateOnly.FromDateTime(DateTime.Now.AddDays(3)),
            MontoAcordado = 2000m
        };
        var createResponse = await _client.PostAsJsonAsync("/api/contrataciones", createCommand);
        var detalleId = await createResponse.Content.ReadFromJsonAsync<int>();
        await _client.PutAsync($"/api/contrataciones/{detalleId}/accept", null);
        await _client.PutAsync($"/api/contrataciones/{detalleId}/start", null);

        // Act
        var response = await _client.PutAsync($"/api/contrataciones/{detalleId}/complete", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify estado cambió a Completada (4)
        var getResponse = await _client.GetAsync($"/api/contrataciones/{detalleId}");
        var contratacion = await getResponse.Content.ReadFromJsonAsync<ContratacionDto>();
        contratacion!.Estatus.Should().Be(4); // Completada
        contratacion.FechaFinalizacionReal.Should().NotBeNull();
        contratacion.PorcentajeAvance.Should().Be(100);
    }

    [Fact]
    public async Task Complete_WithNonEnProgresoStatus_ReturnsBadRequest()
    {
        // Arrange - crear y aceptar (pero no iniciar)
        var createCommand = new CreateContratacionCommand
        {
            DescripcionCorta = "Test complete without start",
            FechaInicio = DateOnly.FromDateTime(DateTime.Now.AddDays(1)),
            FechaFinal = DateOnly.FromDateTime(DateTime.Now.AddDays(3)),
            MontoAcordado = 2000m
        };
        var createResponse = await _client.PostAsJsonAsync("/api/contrataciones", createCommand);
        var detalleId = await createResponse.Content.ReadFromJsonAsync<int>();
        await _client.PutAsync($"/api/contrataciones/{detalleId}/accept", null);

        // Act
        var response = await _client.PutAsync($"/api/contrataciones/{detalleId}/complete", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Complete_WithNonExistingId_ReturnsNotFound()
    {
        // Act
        var response = await _client.PutAsync("/api/contrataciones/999999/complete", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    #endregion

    #region Cancel Workflow Tests

    [Fact]
    public async Task Cancel_WithValidMotivo_ReturnsOk()
    {
        // Arrange
        var createCommand = new CreateContratacionCommand
        {
            DescripcionCorta = "Test Cancel",
            FechaInicio = DateOnly.FromDateTime(DateTime.Now.AddDays(1)),
            FechaFinal = DateOnly.FromDateTime(DateTime.Now.AddDays(3)),
            MontoAcordado = 2000m
        };
        var createResponse = await _client.PostAsJsonAsync("/api/contrataciones", createCommand);
        var detalleId = await createResponse.Content.ReadFromJsonAsync<int>();

        var cancelCommand = new CancelContratacionCommand
        {
            DetalleId = detalleId,
            Motivo = "Cliente canceló el proyecto"
        };

        // Act
        var response = await _client.PutAsJsonAsync($"/api/contrataciones/{detalleId}/cancel", cancelCommand);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify estado cambió a Cancelada (3)
        var getResponse = await _client.GetAsync($"/api/contrataciones/{detalleId}");
        var contratacion = await getResponse.Content.ReadFromJsonAsync<ContratacionDto>();
        contratacion!.Estatus.Should().Be(3); // Cancelada
    }

    [Fact]
    public async Task Cancel_WithEmptyMotivo_ReturnsBadRequest()
    {
        // Arrange
        var createCommand = new CreateContratacionCommand
        {
            DescripcionCorta = "Test empty cancel motivo",
            FechaInicio = DateOnly.FromDateTime(DateTime.Now.AddDays(1)),
            FechaFinal = DateOnly.FromDateTime(DateTime.Now.AddDays(3)),
            MontoAcordado = 2000m
        };
        var createResponse = await _client.PostAsJsonAsync("/api/contrataciones", createCommand);
        var detalleId = await createResponse.Content.ReadFromJsonAsync<int>();

        var cancelCommand = new CancelContratacionCommand
        {
            DetalleId = detalleId,
            Motivo = "" // INVALID
        };

        // Act
        var response = await _client.PutAsJsonAsync($"/api/contrataciones/{detalleId}/cancel", cancelCommand);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Cancel_FromDifferentStates_ReturnsOk()
    {
        // Test cancelación desde Pendiente
        var command1 = new CreateContratacionCommand
        {
            DescripcionCorta = "Cancel desde Pendiente",
            FechaInicio = DateOnly.FromDateTime(DateTime.Now.AddDays(1)),
            FechaFinal = DateOnly.FromDateTime(DateTime.Now.AddDays(3)),
            MontoAcordado = 2000m
        };
        var response1 = await _client.PostAsJsonAsync("/api/contrataciones", command1);
        var detalleId1 = await response1.Content.ReadFromJsonAsync<int>();
        var cancel1 = new CancelContratacionCommand { DetalleId = detalleId1, Motivo = "Test" };
        var cancelResponse1 = await _client.PutAsJsonAsync($"/api/contrataciones/{detalleId1}/cancel", cancel1);
        cancelResponse1.StatusCode.Should().Be(HttpStatusCode.OK);

        // Test cancelación desde Aceptada
        var command2 = new CreateContratacionCommand
        {
            DescripcionCorta = "Cancel desde Aceptada",
            FechaInicio = DateOnly.FromDateTime(DateTime.Now.AddDays(1)),
            FechaFinal = DateOnly.FromDateTime(DateTime.Now.AddDays(3)),
            MontoAcordado = 2000m
        };
        var response2 = await _client.PostAsJsonAsync("/api/contrataciones", command2);
        var detalleId2 = await response2.Content.ReadFromJsonAsync<int>();
        await _client.PutAsync($"/api/contrataciones/{detalleId2}/accept", null);
        var cancel2 = new CancelContratacionCommand { DetalleId = detalleId2, Motivo = "Test" };
        var cancelResponse2 = await _client.PutAsJsonAsync($"/api/contrataciones/{detalleId2}/cancel", cancel2);
        cancelResponse2.StatusCode.Should().Be(HttpStatusCode.OK);

        // Test cancelación desde EnProgreso
        var command3 = new CreateContratacionCommand
        {
            DescripcionCorta = "Cancel desde EnProgreso",
            FechaInicio = DateOnly.FromDateTime(DateTime.Now.AddDays(1)),
            FechaFinal = DateOnly.FromDateTime(DateTime.Now.AddDays(3)),
            MontoAcordado = 2000m
        };
        var response3 = await _client.PostAsJsonAsync("/api/contrataciones", command3);
        var detalleId3 = await response3.Content.ReadFromJsonAsync<int>();
        await _client.PutAsync($"/api/contrataciones/{detalleId3}/accept", null);
        await _client.PutAsync($"/api/contrataciones/{detalleId3}/start", null);
        var cancel3 = new CancelContratacionCommand { DetalleId = detalleId3, Motivo = "Test" };
        var cancelResponse3 = await _client.PutAsJsonAsync($"/api/contrataciones/{detalleId3}/cancel", cancel3);
        cancelResponse3.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Cancel_CompletedContratacion_ReturnsBadRequest()
    {
        // Arrange - crear workflow completo hasta Completada
        var createCommand = new CreateContratacionCommand
        {
            DescripcionCorta = "Test cancel completed",
            FechaInicio = DateOnly.FromDateTime(DateTime.Now.AddDays(1)),
            FechaFinal = DateOnly.FromDateTime(DateTime.Now.AddDays(3)),
            MontoAcordado = 2000m
        };
        var createResponse = await _client.PostAsJsonAsync("/api/contrataciones", createCommand);
        var detalleId = await createResponse.Content.ReadFromJsonAsync<int>();
        await _client.PutAsync($"/api/contrataciones/{detalleId}/accept", null);
        await _client.PutAsync($"/api/contrataciones/{detalleId}/start", null);
        await _client.PutAsync($"/api/contrataciones/{detalleId}/complete", null);

        var cancelCommand = new CancelContratacionCommand
        {
            DetalleId = detalleId,
            Motivo = "Intentar cancelar completada"
        };

        // Act
        var response = await _client.PutAsJsonAsync($"/api/contrataciones/{detalleId}/cancel", cancelCommand);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Cancel_WithNonExistingId_ReturnsNotFound()
    {
        // Arrange
        var cancelCommand = new CancelContratacionCommand
        {
            DetalleId = 999999,
            Motivo = "Test motivo"
        };

        // Act
        var response = await _client.PutAsJsonAsync("/api/contrataciones/999999/cancel", cancelCommand);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    #endregion

    #region Filter Queries Tests

    [Fact]
    public async Task GetPendientes_ReturnsOnlyPendienteContrataciones()
    {
        // Arrange - crear contrataciones en diferentes estados
        var command1 = new CreateContratacionCommand
        {
            DescripcionCorta = "Pendiente 1",
            FechaInicio = DateOnly.FromDateTime(DateTime.Now.AddDays(1)),
            FechaFinal = DateOnly.FromDateTime(DateTime.Now.AddDays(3)),
            MontoAcordado = 2000m
        };
        await _client.PostAsJsonAsync("/api/contrataciones", command1);

        var command2 = new CreateContratacionCommand
        {
            DescripcionCorta = "Para aceptar",
            FechaInicio = DateOnly.FromDateTime(DateTime.Now.AddDays(1)),
            FechaFinal = DateOnly.FromDateTime(DateTime.Now.AddDays(3)),
            MontoAcordado = 3000m
        };
        var response2 = await _client.PostAsJsonAsync("/api/contrataciones", command2);
        var detalleId2 = await response2.Content.ReadFromJsonAsync<int>();
        await _client.PutAsync($"/api/contrataciones/{detalleId2}/accept", null);

        // Act
        var response = await _client.GetAsync("/api/contrataciones/pendientes");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var contrataciones = await response.Content.ReadFromJsonAsync<List<ContratacionDto>>();
        contrataciones.Should().NotBeNull();
        contrataciones!.All(c => c.Estatus == 1).Should().BeTrue(); // Solo Pendientes
    }

    [Fact]
    public async Task GetActivas_ReturnsOnlyActivasContrataciones()
    {
        // Arrange - crear contrataciones activas (Aceptada o EnProgreso)
        var command = new CreateContratacionCommand
        {
            DescripcionCorta = "Test Activas",
            FechaInicio = DateOnly.FromDateTime(DateTime.Now.AddDays(1)),
            FechaFinal = DateOnly.FromDateTime(DateTime.Now.AddDays(3)),
            MontoAcordado = 2000m
        };
        var createResponse = await _client.PostAsJsonAsync("/api/contrataciones", command);
        var detalleId = await createResponse.Content.ReadFromJsonAsync<int>();
        await _client.PutAsync($"/api/contrataciones/{detalleId}/accept", null);

        // Act
        var response = await _client.GetAsync("/api/contrataciones/activas");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var contrataciones = await response.Content.ReadFromJsonAsync<List<ContratacionDto>>();
        contrataciones.Should().NotBeNull();
        contrataciones!.All(c => c.Estatus == 2 || c.Estatus == 5).Should().BeTrue(); // Aceptada o EnProgreso
    }

    [Fact]
    public async Task GetSinCalificar_ReturnsOnlyCompletadasSinCalificar()
    {
        // Arrange - crear contratación completada
        var command = new CreateContratacionCommand
        {
            DescripcionCorta = "Test Sin Calificar",
            FechaInicio = DateOnly.FromDateTime(DateTime.Now.AddDays(1)),
            FechaFinal = DateOnly.FromDateTime(DateTime.Now.AddDays(3)),
            MontoAcordado = 2000m
        };
        var createResponse = await _client.PostAsJsonAsync("/api/contrataciones", command);
        var detalleId = await createResponse.Content.ReadFromJsonAsync<int>();
        await _client.PutAsync($"/api/contrataciones/{detalleId}/accept", null);
        await _client.PutAsync($"/api/contrataciones/{detalleId}/start", null);
        await _client.PutAsync($"/api/contrataciones/{detalleId}/complete", null);

        // Act
        var response = await _client.GetAsync("/api/contrataciones/sin-calificar");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var contrataciones = await response.Content.ReadFromJsonAsync<List<ContratacionDto>>();
        contrataciones.Should().NotBeNull();
        contrataciones!.All(c => c.Estatus == 4 && !c.Calificado).Should().BeTrue();
    }

    #endregion
}
