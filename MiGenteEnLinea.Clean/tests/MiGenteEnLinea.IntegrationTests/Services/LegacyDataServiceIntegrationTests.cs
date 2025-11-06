using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MiGenteEnLinea.Application.Common.Interfaces;
using MiGenteEnLinea.Application.Features.Empleados.Commands.CreateRemuneraciones;
using MiGenteEnLinea.Application.Features.Empleados.DTOs;
using MiGenteEnLinea.Domain.Entities.Contrataciones;
using MiGenteEnLinea.Domain.Entities.Empleados;
using MiGenteEnLinea.Domain.Entities.Nominas;
using MiGenteEnLinea.Infrastructure.Persistence.Entities.Generated;
using MiGenteEnLinea.Infrastructure.Services;
using MiGenteEnLinea.IntegrationTests.Infrastructure;
using Xunit;
using Xunit.Abstractions;

// Alias para resolver ambigüedad entre entidades DDD y Generated
using DomainEmpleadorRecibosHeaderContratacione = MiGenteEnLinea.Domain.Entities.Pagos.EmpleadorRecibosHeaderContratacione;
using DomainEmpleadorRecibosDetalleContratacione = MiGenteEnLinea.Domain.Entities.Pagos.EmpleadorRecibosDetalleContratacione;

namespace MiGenteEnLinea.IntegrationTests.Services;

/// <summary>
/// Integration tests para LegacyDataService - Validación de migración SQL Raw → EF Core
/// 
/// COBERTURA: 13 tests sobre 8 métodos migrados
/// - DeleteRemuneracionAsync (3 tests)
/// - CreateRemuneracionesAsync (2 tests)
/// - UpdateRemuneracionesAsync (1 test)
/// - DarDeBajaEmpleadoAsync (2 tests)
/// - CancelarTrabajoAsync (1 test)
/// - EliminarReciboEmpleadoAsync (1 test)
/// - EliminarReciboContratacionAsync (1 test)
/// - EliminarEmpleadoTemporalAsync (2 tests)
/// 
/// ESTRATEGIA:
/// - Usa SQL Server real (Docker container mda-308)
/// - Sigue patrón de EmpleadoresControllerTests
/// - Validación con base de datos persistente
/// </summary>
[Collection("Integration Tests")]
public class LegacyDataServiceIntegrationTests : IntegrationTestBase
{
    private readonly ITestOutputHelper _output;
    private readonly ILegacyDataService _legacyDataService;
    
    public LegacyDataServiceIntegrationTests(
        TestWebApplicationFactory factory,
        ITestOutputHelper output) : base(factory)
    {
        _output = output;
        
        // Get ILegacyDataService from DI container (registered as Scoped)
        var scope = factory.Services.CreateScope();
        _legacyDataService = scope.ServiceProvider.GetRequiredService<ILegacyDataService>();
    }

    #region Helper Methods

    /// <summary>
    /// Crea usuario y empleado de prueba a través del API (evita conflictos Entity vs Generated)
    /// Sigue patrón de EmpleadosControllerTests
    /// </summary>
    private async Task<(string UserId, string Email, int EmpleadoId)> CreateTestEmpleadoAsync()
    {
        // Register and login as Empleador
        var email = GenerateUniqueEmail("empleador");
        var (userId, emailUsado) = await RegisterUserAsync(email, "Password123!", "Empleador", "Test", "User");
        await LoginAsync(emailUsado, "Password123!");

        // Create Empleado through API (avoids DbContext entity type conflicts)
        var createCommand = new
        {
            UserId = userId,
            Identificacion = GenerateRandomIdentification(),
            Nombre = "Juan",
            Apellido = "Pérez",
            FechaInicio = DateTime.Today, // ✅ Use Today to avoid "fecha futura" validation error
            Posicion = "Test Position",
            Salario = 25000m,
            PeriodoPago = 3, // Mensual
            Tss = true,
            Telefono1 = "8091234567",
            Direccion = "Test Address",
            Provincia = "Santo Domingo"
        };

        var response = await Client.PostAsJsonAsync("/api/empleados", createCommand);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content).RootElement;
        
        var hasId = json.TryGetProperty("empleadoId", out var prop);
        if (!hasId) hasId = json.TryGetProperty("EmpleadoId", out prop);
        
        var empleadoId = prop.GetInt32();
        
        _output.WriteLine($"✅ Created test Empleado via API: UserId={userId}, EmpleadoId={empleadoId}");
        return (userId, emailUsado, empleadoId);
    }

    #endregion

    #region 1. DeleteRemuneracionAsync Tests (3 tests)

    [Fact]
    public async Task DeleteRemuneracionAsync_WithValidData_DeletesSuccessfully()
    {
        // Arrange: Create empleado via API
        var (userId, _, empleadoId) = await CreateTestEmpleadoAsync();

        // ✅ Create remuneraciones via API BATCH endpoint (not LegacyDataService)
        var remuneracionesArray = new[]
        {
            new { Descripcion = "Test Bono", Monto = 5000m },
            new { Descripcion = "Comision", Monto = 3000m }
        };
        
        var createResponse = await Client.PostAsJsonAsync($"/api/empleados/{empleadoId}/remuneraciones/batch", remuneracionesArray);
        createResponse.EnsureSuccessStatusCode();
        
        _output.WriteLine($"✅ Created remuneraciones via API for EmpleadoId={empleadoId}");

        // Get remuneraciones via API endpoint to get IDs
        var response = await Client.GetAsync($"/api/empleados/{empleadoId}/remuneraciones");
        response.EnsureSuccessStatusCode();
        var remuneraciones = await response.Content.ReadFromJsonAsync<List<RemuneracionDto>>();
        
        remuneraciones.Should().HaveCount(2, "because we created 2 remuneraciones");
        var remuneracionToDelete = remuneraciones!.First();

        _output.WriteLine($"📋 Remuneracion to delete: Id={remuneracionToDelete.Id}, Description={remuneracionToDelete.Descripcion}");

        // Act: Delete one remuneracion via LegacyDataService
        await _legacyDataService.DeleteRemuneracionAsync(userId, remuneracionToDelete.Id);

        // Assert: Verify count decreased via API
        var afterDeleteResponse = await Client.GetAsync($"/api/empleados/{empleadoId}/remuneraciones");
        afterDeleteResponse.EnsureSuccessStatusCode();
        var afterDelete = await afterDeleteResponse.Content.ReadFromJsonAsync<List<RemuneracionDto>>();
        
        afterDelete.Should().HaveCount(1, "because one remuneracion was deleted");
        afterDelete.Should().NotContain(r => r.Id == remuneracionToDelete.Id, "because deleted remuneracion should not exist");

        _output.WriteLine("✅ Test PASSED: DeleteRemuneracionAsync with valid data (validated via API)");
    }

    [Fact]
    public async Task DeleteRemuneracionAsync_WithInvalidId_DoesNotThrowException()
    {
        // Arrange: Create empleado
        var (userId, _, _) = await CreateTestEmpleadoAsync();

        // Act: Try to delete non-existent remuneracion (should handle gracefully)
        var deleteAction = async () => await _legacyDataService.DeleteRemuneracionAsync(userId, 99999);

        // Assert: Should not throw exception (null-safe handling)
        await deleteAction.Should().NotThrowAsync("because method handles null gracefully");

        _output.WriteLine("✅ Test PASSED: DeleteRemuneracionAsync handles invalid ID without exception");
    }

    [Fact]
    public async Task DeleteRemuneracionAsync_WithDifferentUserId_DoesNotDelete()
    {
        // Arrange: Create empleado and remuneracion via API
        var (userId1, _, empleadoId) = await CreateTestEmpleadoAsync();

        // ✅ Create remuneracion via API BATCH endpoint
        var remuneracionArray = new[]
        {
            new { Descripcion = "Protected Bono", Monto = 3000m }
        };
        
        var createResponse = await Client.PostAsJsonAsync($"/api/empleados/{empleadoId}/remuneraciones/batch", remuneracionArray);
        createResponse.EnsureSuccessStatusCode();
        
        // Get remuneracion ID via API
        var getResponse = await Client.GetAsync($"/api/empleados/{empleadoId}/remuneraciones");
        var remuneraciones = await getResponse.Content.ReadFromJsonAsync<List<RemuneracionDto>>();
        remuneraciones.Should().HaveCount(1);
        var remuneracionId = remuneraciones!.First().Id;
        
        _output.WriteLine($"📋 Created remuneracion Id={remuneracionId} owned by UserId={userId1}");

        // Act: Try to delete with different userId (ownership validation)
        var userId2 = Guid.NewGuid().ToString();
        await _legacyDataService.DeleteRemuneracionAsync(userId2, remuneracionId);

        // Assert: Remuneracion should still exist (validate via API)
        var afterDeleteResponse = await Client.GetAsync($"/api/empleados/{empleadoId}/remuneraciones");
        var afterDelete = await afterDeleteResponse.Content.ReadFromJsonAsync<List<RemuneracionDto>>();
        
        afterDelete.Should().HaveCount(1, "because different userId cannot delete (ownership validation)");
        afterDelete.Should().Contain(r => r.Id == remuneracionId, "because unauthorized delete should not work");

        _output.WriteLine("✅ Test PASSED: Ownership validation prevents unauthorized delete");
    }

    #endregion

    #region 2. CreateRemuneracionesAsync Tests (2 tests)

    [Fact]
    public async Task CreateRemuneracionesAsync_WithMultipleItems_InsertsAll()
    {
        // Arrange: Create empleado via API
        var (userId, _, empleadoId) = await CreateTestEmpleadoAsync();

        var nuevasRemuneraciones = new List<RemuneracionItemDto>
        {
            new RemuneracionItemDto { Descripcion = "Salario Base", Monto = 30000 },
            new RemuneracionItemDto { Descripcion = "Bono Navidad", Monto = 5000 },
            new RemuneracionItemDto { Descripcion = "Comisión", Monto = 8000 }
        };

        // Act: Insert multiple remuneraciones via LegacyDataService
        await _legacyDataService.CreateRemuneracionesAsync(userId, empleadoId, nuevasRemuneraciones);

        // Assert: Verify all 3 were inserted via API GET
        var response = await Client.GetAsync($"/api/empleados/{empleadoId}/remuneraciones");
        response.EnsureSuccessStatusCode();
        var insertadas = await response.Content.ReadFromJsonAsync<List<RemuneracionDto>>();

        insertadas.Should().HaveCount(3, "because 3 remuneraciones were created");
        insertadas.Should().Contain(r => r.Descripcion == "Salario Base" && r.Monto == 30000);
        insertadas.Should().Contain(r => r.Descripcion == "Bono Navidad" && r.Monto == 5000);
        insertadas.Should().Contain(r => r.Descripcion == "Comisión" && r.Monto == 8000);

        _output.WriteLine($"✅ Test PASSED: Created {insertadas!.Count} remuneraciones successfully (validated via API)");
    }

    [Fact]
    public async Task CreateRemuneracionesAsync_WithEmptyList_InsertsNothing()
    {
        // Arrange: Create empleado via API
        var (userId, _, empleadoId) = await CreateTestEmpleadoAsync();

        var listaVacia = new List<RemuneracionItemDto>();

        // Act: Try to insert empty list via LegacyDataService
        await _legacyDataService.CreateRemuneracionesAsync(userId, empleadoId, listaVacia);

        // Assert: Count should be 0 via API GET
        var response = await Client.GetAsync($"/api/empleados/{empleadoId}/remuneraciones");
        response.EnsureSuccessStatusCode();
        var remuneraciones = await response.Content.ReadFromJsonAsync<List<RemuneracionDto>>();

        remuneraciones.Should().BeEmpty("because empty list should not insert anything");

        _output.WriteLine("✅ Test PASSED: Empty list handled correctly (validated via API)");
    }

    #endregion

    #region 3. UpdateRemuneracionesAsync Tests (1 test)

    [Fact]
    public async Task UpdateRemuneracionesAsync_ReplacesAllInSingleTransaction()
    {
        // Arrange: Create empleado and 2 old remuneraciones via API
        var (userId, _, empleadoId) = await CreateTestEmpleadoAsync();

        // ✅ Create 2 old remuneraciones via API BATCH endpoint
        var oldRemuneracionesArray = new[]
        {
            new { Descripcion = "Old 1", Monto = 1000m },
            new { Descripcion = "Old 2", Monto = 2000m }
        };
        
        var createOldResponse = await Client.PostAsJsonAsync($"/api/empleados/{empleadoId}/remuneraciones/batch", oldRemuneracionesArray);
        createOldResponse.EnsureSuccessStatusCode();
        
        _output.WriteLine($"✅ Created 2 old remuneraciones via API");

        // New remuneraciones to replace old ones
        var nuevasRemuneraciones = new List<RemuneracionItemDto>
        {
            new RemuneracionItemDto { Descripcion = "New 1", Monto = 3000 },
            new RemuneracionItemDto { Descripcion = "New 2", Monto = 4000 },
            new RemuneracionItemDto { Descripcion = "New 3", Monto = 5000 }
        };

        // Act: Update (replace all in single transaction) via LegacyDataService
        await _legacyDataService.UpdateRemuneracionesAsync(userId, empleadoId, nuevasRemuneraciones);

        // Assert: Only new remuneraciones should exist via API GET
        var response = await Client.GetAsync($"/api/empleados/{empleadoId}/remuneraciones");
        response.EnsureSuccessStatusCode();
        var current = await response.Content.ReadFromJsonAsync<List<RemuneracionDto>>();

        current.Should().HaveCount(3, "because 3 new remuneraciones replace 2 old ones");
        current.Should().NotContain(r => r.Descripcion == "Old 1");
        current.Should().NotContain(r => r.Descripcion == "Old 2");
        current.Should().Contain(r => r.Descripcion == "New 1" && r.Monto == 3000);
        current.Should().Contain(r => r.Descripcion == "New 2" && r.Monto == 4000);
        current.Should().Contain(r => r.Descripcion == "New 3" && r.Monto == 5000);

        _output.WriteLine("✅ Test PASSED: All remuneraciones replaced in single ACID transaction (validated via API)");
    }

    #endregion

    #region 4. DarDeBajaEmpleadoAsync Tests (2 tests)

    [Fact]
    public async Task DarDeBajaEmpleadoAsync_WithValidData_UpdatesSoftDeleteFields()
    {
        // Arrange: Create empleado via API
        var (userId, _, empleadoId) = await CreateTestEmpleadoAsync();
        
        _output.WriteLine($"📋 Testing soft delete for EmpleadoId={empleadoId}, UserId={userId}");

        var fechaBaja = DateTime.UtcNow.Date;
        var motivo = "Renuncia";
        var prestaciones = 15000m;

        // Act: Soft delete empleado via LegacyDataService
        var result = await _legacyDataService.DarDeBajaEmpleadoAsync(empleadoId, userId, fechaBaja, prestaciones, motivo);

        // Assert: Verify soft delete fields updated via API GET
        result.Should().BeTrue("because empleado exists and belongs to userId");
        
        var response = await Client.GetAsync($"/api/empleados/{empleadoId}");
        response.EnsureSuccessStatusCode();
        var empleadoDto = await response.Content.ReadFromJsonAsync<JsonElement>();
        
        empleadoDto.GetProperty("activo").GetBoolean().Should().BeFalse("because empleado was given de baja");
        empleadoDto.TryGetProperty("fechaSalida", out var fechaSalidaProp).Should().BeTrue("fechaSalida should exist");
        empleadoDto.TryGetProperty("motivoBaja", out var motivoProp).Should().BeTrue("motivoBaja should exist");
        motivoProp.GetString().Should().Be(motivo);
        empleadoDto.TryGetProperty("prestaciones", out var prestacionesProp).Should().BeTrue("prestaciones should exist");
        prestacionesProp.GetDecimal().Should().Be(prestaciones);

        _output.WriteLine("✅ Test PASSED: Empleado soft deleted successfully (validated via API)");
    }

    [Fact]
    public async Task DarDeBajaEmpleadoAsync_WithDifferentUserId_ReturnsFalse()
    {
        // Arrange: Create empleado via API
        var (userId, _, empleadoId) = await CreateTestEmpleadoAsync();

        // Act: Try to delete with different userId (ownership validation) via LegacyDataService
        var userId2 = Guid.NewGuid().ToString();
        var result = await _legacyDataService.DarDeBajaEmpleadoAsync(
            empleadoId, 
            userId2, 
            DateTime.UtcNow.Date, 
            0, 
            "Test");

        // Assert: Operation should fail
        result.Should().BeFalse("because empleado does not belong to userId2");

        // Verify empleado still active via API GET
        var response = await Client.GetAsync($"/api/empleados/{empleadoId}");
        response.EnsureSuccessStatusCode();
        var empleadoDto = await response.Content.ReadFromJsonAsync<JsonElement>();
        
        empleadoDto.GetProperty("activo").GetBoolean().Should().BeTrue("because unauthorized baja should not modify empleado");

        _output.WriteLine("✅ Test PASSED: Ownership validation prevents unauthorized baja (validated via API)");
    }

    #endregion

    #region 5. CancelarTrabajoAsync Tests (1 test)

    [Fact]
    public async Task CancelarTrabajoAsync_WithValidData_SetsStatusToCancelled()
    {
        // Arrange: Create detalle contratacion with valid FK
        // First create EmpleadoTemporal (parent) using DDD entity
        var (userId, _, _) = await CreateTestEmpleadoAsync();
        
        var empleadoTemporal = EmpleadoTemporal.CreatePersonaFisica(
            userId: userId,
            identificacion: GenerateRandomIdentification(),
            nombre: "Test",
            apellido: "Temporal",
            telefono1: null
        );
        DbContext.EmpleadosTemporales.Add(empleadoTemporal);
        await DbContext.SaveChangesAsync();
        var contratacionId = empleadoTemporal.ContratacionId;
        
        // Now create DetalleContratacion using DDD entity
        var detalle = DetalleContratacion.Crear(
            descripcionCorta: "Test trabajo",
            fechaInicio: DateOnly.FromDateTime(DateTime.UtcNow),
            fechaFinal: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7)),
            montoAcordado: 5000,
            descripcionAmpliada: "Trabajo de prueba para testing",
            esquemaPagos: null,
            contratacionId: contratacionId
        );
        DbContext.DetalleContrataciones.Add(detalle);
        await DbContext.SaveChangesAsync();
        var detalleId = detalle.DetalleId;
        ClearChangeTracker();

        _output.WriteLine($"📋 Created DetalleContratacion (DDD): ContratacionId={contratacionId}, DetalleId={detalleId}, Estatus=1");

        // Act: Cancel trabajo
        var result = await _legacyDataService.CancelarTrabajoAsync(contratacionId, detalleId);
        ClearChangeTracker();

        // Assert: Estatus should be 3 (Cancelled)
        result.Should().BeTrue("because detalle exists");

        var cancelled = await DbContext.DetalleContrataciones.FindAsync(detalleId);
        cancelled.Should().NotBeNull();
        cancelled!.Estatus.Should().Be(3, "because trabajo was cancelled");

        _output.WriteLine("✅ Test PASSED: Trabajo cancelled successfully (estatus = 3)");
    }

    #endregion

    #region 6. EliminarReciboEmpleadoAsync Tests (1 test)

    [Fact]
    public async Task EliminarReciboEmpleadoAsync_WithRecibo_DeletesHeaderAndDetalles()
    {
        // Arrange: Create empleado, header + 2 detalles (using DDD entities)
        var (userId, _, empleadoId) = await CreateTestEmpleadoAsync();

        var header = ReciboHeader.Create(
            userId: userId,
            empleadoId: empleadoId,
            fechaPago: DateTime.UtcNow,
            conceptoPago: "Salario Quincenal"
        );
        DbContext.RecibosHeader.Add(header);
        await DbContext.SaveChangesAsync();
        var pagoId = header.PagoId;

        var detalle1 = ReciboDetalle.Create(pagoId, "Salario", 25000);
        var detalle2 = ReciboDetalle.Create(pagoId, "Bono", 5000);
        DbContext.RecibosDetalle.AddRange(detalle1, detalle2);
        await DbContext.SaveChangesAsync();
        ClearChangeTracker();

        _output.WriteLine($"📋 Created ReciboHeader (DDD): PagoId={pagoId} with 2 detalles");

        // Act: Delete recibo (should cascade delete detalles)
        var result = await _legacyDataService.EliminarReciboEmpleadoAsync(pagoId);
        ClearChangeTracker();

        // Assert: Header and detalles should be deleted
        result.Should().BeTrue("because recibo exists");

        var deletedHeader = await DbContext.RecibosHeader.FindAsync(pagoId);
        deletedHeader.Should().BeNull("because header was deleted");

        var deletedDetalles = await DbContext.RecibosDetalle
            .Where(d => d.PagoId == pagoId)
            .ToListAsync();
        deletedDetalles.Should().BeEmpty("because detalles cascade deleted");

        _output.WriteLine("✅ Test PASSED: Recibo header and detalles deleted (2-level cascade) - DDD");
    }

    #endregion

    #region 7. EliminarReciboContratacionAsync Tests (1 test)

    [Fact]
    public async Task EliminarReciboContratacionAsync_WithRecibo_DeletesHeaderAndDetalles()
    {
        // Arrange: Create empleadoTemporal (DDD), header + 2 detalles
        var (userId, _, _) = await CreateTestEmpleadoAsync();
        
        var empleadoTemporal = EmpleadoTemporal.Create(
            userId: userId,
            nombre: "Test",
            apellido: "Worker",
            identificacion: GenerateRandomIdentification(),
            telefono: "8091234567"
        );
        DbContext.EmpleadosTemporales.Add(empleadoTemporal);
        await DbContext.SaveChangesAsync();
        var contratacionId = empleadoTemporal.ContratacionId;

        var header = DomainEmpleadorRecibosHeaderContratacione.Create(
            contratacionId: contratacionId,
            userId: userId,
            fechaPago: DateTime.UtcNow,
            conceptoPago: "Pago Servicio"
        );
        DbContext.EmpleadorRecibosHeaderContrataciones.Add(header);
        await DbContext.SaveChangesAsync();
        var pagoId = header.PagoId;

        var detalle1 = DomainEmpleadorRecibosDetalleContratacione.Create(pagoId, "Pago 1", 6000);
        var detalle2 = DomainEmpleadorRecibosDetalleContratacione.Create(pagoId, "Pago 2", 4000);
        DbContext.EmpleadorRecibosDetalleContrataciones.AddRange(detalle1, detalle2);
        await DbContext.SaveChangesAsync();
        ClearChangeTracker();

        _output.WriteLine($"📋 Created EmpleadorRecibosHeaderContratacione (DDD): PagoId={pagoId} with 2 detalles");

        // Act: Delete recibo
        var result = await _legacyDataService.EliminarReciboContratacionAsync(pagoId);
        ClearChangeTracker();

        // Assert: Header and detalles deleted
        result.Should().BeTrue("because recibo exists");

        var deletedHeader = await DbContext.EmpleadorRecibosHeaderContrataciones.FindAsync(pagoId);
        deletedHeader.Should().BeNull("because header was deleted");

        var deletedDetalles = await DbContext.EmpleadorRecibosDetalleContrataciones
            .Where(d => d.PagoId == pagoId)
            .ToListAsync();
        deletedDetalles.Should().BeEmpty("because detalles cascade deleted");

        _output.WriteLine("✅ Test PASSED: Recibo contratacion header and detalles deleted (2-level cascade) - DDD");
    }

    #endregion

    #region 8. EliminarEmpleadoTemporalAsync Tests (2 tests)

    [Fact]
    public async Task EliminarEmpleadoTemporalAsync_WithRecibos_DeletesAll3Levels()
    {
        // Arrange: Create empleadoTemporal (DDD) + 2 headers + 3 detalles (3-level cascade)
        var (userId, _, _) = await CreateTestEmpleadoAsync();
        
        var empleadoTemporal = EmpleadoTemporal.Create(
            userId: userId,
            nombre: "Temporal",
            apellido: "Worker",
            identificacion: GenerateRandomIdentification(),
            telefono: "8091234567"
        );
        DbContext.EmpleadosTemporales.Add(empleadoTemporal);
        await DbContext.SaveChangesAsync();
        var contratacionId = empleadoTemporal.ContratacionId;

        // Level 2: Headers (DDD)
        var header1 = DomainEmpleadorRecibosHeaderContratacione.Create(contratacionId, userId, DateTime.UtcNow, "Pago 1");
        var header2 = DomainEmpleadorRecibosHeaderContratacione.Create(contratacionId, userId, DateTime.UtcNow, "Pago 2");
        DbContext.EmpleadorRecibosHeaderContrataciones.AddRange(header1, header2);
        await DbContext.SaveChangesAsync();

        // Level 1: Detalles (DDD)
        var detalle1 = DomainEmpleadorRecibosDetalleContratacione.Create(header1.PagoId, "Pago 1", 5000);
        var detalle2 = DomainEmpleadorRecibosDetalleContratacione.Create(header2.PagoId, "Pago 2A", 1500);
        var detalle3 = DomainEmpleadorRecibosDetalleContratacione.Create(header2.PagoId, "Pago 2B", 1500);
        DbContext.EmpleadorRecibosDetalleContrataciones.AddRange(detalle1, detalle2, detalle3);
        await DbContext.SaveChangesAsync();
        ClearChangeTracker();

        _output.WriteLine($"📋 Created EmpleadoTemporal (DDD) with 2 headers and 3 detalles (3-level cascade)");

        // Act: Delete empleado temporal (should cascade 3 levels)
        var result = await _legacyDataService.EliminarEmpleadoTemporalAsync(contratacionId);
        ClearChangeTracker();

        // Assert: All 3 levels deleted
        result.Should().BeTrue("because empleado temporal exists");

        // Level 3: EmpleadoTemporal should be deleted
        var deletedEmpleado = await DbContext.EmpleadosTemporales
            .FirstOrDefaultAsync(et => et.ContratacionId == contratacionId);
        deletedEmpleado.Should().BeNull("because empleado temporal was deleted");

        // Level 2: Headers should be deleted
        var deletedHeaders = await DbContext.EmpleadorRecibosHeaderContrataciones
            .Where(h => h.ContratacionId == contratacionId)
            .ToListAsync();
        deletedHeaders.Should().BeEmpty("because headers cascade deleted");

        // Level 1: Detalles should be deleted
        var deletedDetalles = await DbContext.EmpleadorRecibosDetalleContrataciones
            .Where(d => d.PagoId == header1.PagoId || d.PagoId == header2.PagoId)
            .ToListAsync();
        deletedDetalles.Should().BeEmpty("because detalles cascade deleted");

        _output.WriteLine("✅ Test PASSED: 3-level cascade delete (Detalles → Headers → EmpleadoTemporal) - DDD");
    }

    [Fact]
    public async Task EliminarEmpleadoTemporalAsync_WithoutRecibos_DeletesEmpleadoOnly()
    {
        // Arrange: Create empleadoTemporal (DDD) without receipts
        var (userId, _, _) = await CreateTestEmpleadoAsync();
        
        var empleadoTemporal = EmpleadoTemporal.Create(
            userId: userId,
            nombre: "Simple",
            apellido: "Worker",
            identificacion: GenerateRandomIdentification(),
            telefono: "8091234567"
        );
        DbContext.EmpleadosTemporales.Add(empleadoTemporal);
        await DbContext.SaveChangesAsync();
        var contratacionId = empleadoTemporal.ContratacionId;
        ClearChangeTracker();

        _output.WriteLine($"📋 Created EmpleadoTemporal (DDD) without receipts: ContratacionId={contratacionId}");

        // Act: Delete empleado temporal (no receipts to cascade)
        var result = await _legacyDataService.EliminarEmpleadoTemporalAsync(contratacionId);
        ClearChangeTracker();

        // Assert: EmpleadoTemporal deleted, no errors
        result.Should().BeTrue("because empleado temporal exists");

        var deleted = await DbContext.EmpleadosTemporales
            .FirstOrDefaultAsync(et => et.ContratacionId == contratacionId);
        deleted.Should().BeNull("because empleado temporal was deleted");

        _output.WriteLine("✅ Test PASSED: EmpleadoTemporal deleted without errors (no receipts) - DDD");
    }

    #endregion
}
