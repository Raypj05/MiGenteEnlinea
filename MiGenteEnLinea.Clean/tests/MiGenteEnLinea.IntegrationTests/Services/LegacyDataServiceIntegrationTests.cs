using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MiGenteEnLinea.Application.Features.Empleados.Commands.CreateRemuneraciones;
using MiGenteEnLinea.Infrastructure.Persistence.Entities.Generated;
using MiGenteEnLinea.Infrastructure.Services;
using MiGenteEnLinea.IntegrationTests.Infrastructure;
using Xunit;
using Xunit.Abstractions;

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
    private readonly LegacyDataService _legacyDataService;
    
    public LegacyDataServiceIntegrationTests(
        TestWebApplicationFactory factory,
        ITestOutputHelper output) : base(factory)
    {
        _output = output;
        
        // Get LegacyDataService from DI container
        var scope = factory.Services.CreateScope();
        _legacyDataService = scope.ServiceProvider.GetRequiredService<LegacyDataService>();
    }

    #region Helper Methods

    /// <summary>
    /// Crea usuario y empleado de prueba (sin Ofertante, solo para remuneraciones)
    /// Sigue patrón de EmpleadoresControllerTests
    /// </summary>
    private async Task<(string UserId, string Email, int EmpleadoId)> CreateTestEmpleadoAsync()
    {
        // Register and login as Empleador
        var email = GenerateUniqueEmail("empleador");
        var (userId, emailUsado) = await RegisterUserAsync(email, "Password123!", "Empleador", "Test", "User");
        await LoginAsync(emailUsado, "Password123!");

        // Create Empleado directly in database
        var empleado = new Empleado
        {
            UserId = userId,
            Identificacion = GenerateRandomIdentification(),
            Nombre = "Juan",
            Apellido = "Pérez",
            FechaRegistro = DateTime.UtcNow,
            FechaInicio = DateOnly.FromDateTime(DateTime.UtcNow),
            Activo = true,
            Salario = 25000
        };

        DbContext.Set<Empleado>().Add(empleado);
        await DbContext.SaveChangesAsync();
        ClearChangeTracker();
        
        _output.WriteLine($"✅ Created test Empleado: UserId={userId}, EmpleadoId={empleado.EmpleadoId}");
        return (userId, emailUsado, empleado.EmpleadoId);
    }

    #endregion

    #region 1. DeleteRemuneracionAsync Tests (3 tests)

    [Fact]
    public async Task DeleteRemuneracionAsync_WithValidData_DeletesSuccessfully()
    {
        // Arrange: Create empleado and remuneracion
        var (userId, _, empleadoId) = await CreateTestEmpleadoAsync();

        var remuneracion = new Remuneracione
        {
            UserId = userId,
            EmpleadoId = empleadoId,
            Descripcion = "Test Bono",
            Monto = 5000
        };
        DbContext.Set<Remuneracione>().Add(remuneracion);
        await DbContext.SaveChangesAsync();
        var remuneracionId = remuneracion.Id;
        ClearChangeTracker();

        _output.WriteLine($"📋 Remuneracion created: Id={remuneracionId}, UserId={userId}, EmpleadoId={empleadoId}");

        // Act: Delete the remuneracion
        await _legacyDataService.DeleteRemuneracionAsync(userId, remuneracionId);
        ClearChangeTracker();

        // Assert: Verify it was deleted
        var deleted = await DbContext.Set<Remuneracione>().FindAsync(remuneracionId);
        deleted.Should().BeNull("because the remuneracion should be deleted from database");

        _output.WriteLine("✅ Test PASSED: DeleteRemuneracionAsync with valid data");
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
        // Arrange: Create empleado and remuneracion
        var (userId1, _, empleadoId) = await CreateTestEmpleadoAsync();

        var remuneracion = new Remuneracione
        {
            UserId = userId1,
            EmpleadoId = empleadoId,
            Descripcion = "Protected Bono",
            Monto = 3000
        };
        DbContext.Set<Remuneracione>().Add(remuneracion);
        await DbContext.SaveChangesAsync();
        var remuneracionId = remuneracion.Id;
        ClearChangeTracker();

        // Act: Try to delete with different userId (ownership validation)
        var userId2 = Guid.NewGuid().ToString();
        await _legacyDataService.DeleteRemuneracionAsync(userId2, remuneracionId);
        ClearChangeTracker();

        // Assert: Remuneracion should still exist
        var stillExists = await DbContext.Set<Remuneracione>().FindAsync(remuneracionId);
        stillExists.Should().NotBeNull("because different userId cannot delete (ownership validation)");

        _output.WriteLine("✅ Test PASSED: Ownership validation prevents unauthorized delete");
    }

    #endregion

    #region 2. CreateRemuneracionesAsync Tests (2 tests)

    [Fact]
    public async Task CreateRemuneracionesAsync_WithMultipleItems_InsertsAll()
    {
        // Arrange: Create empleado
        var (userId, _, empleadoId) = await CreateTestEmpleadoAsync();

        var nuevasRemuneraciones = new List<RemuneracionItemDto>
        {
            new RemuneracionItemDto { Descripcion = "Salario Base", Monto = 30000 },
            new RemuneracionItemDto { Descripcion = "Bono Navidad", Monto = 5000 },
            new RemuneracionItemDto { Descripcion = "Comisión", Monto = 8000 }
        };

        // Act: Insert multiple remuneraciones
        await _legacyDataService.CreateRemuneracionesAsync(userId, empleadoId, nuevasRemuneraciones);
        ClearChangeTracker();

        // Assert: Verify all 3 were inserted
        var insertadas = await DbContext.Set<Remuneracione>()
            .Where(r => r.UserId == userId && r.EmpleadoId == empleadoId)
            .ToListAsync();

        insertadas.Should().HaveCount(3, "because 3 remuneraciones were created");
        insertadas.Should().Contain(r => r.Descripcion == "Salario Base" && r.Monto == 30000);
        insertadas.Should().Contain(r => r.Descripcion == "Bono Navidad" && r.Monto == 5000);
        insertadas.Should().Contain(r => r.Descripcion == "Comisión" && r.Monto == 8000);

        _output.WriteLine($"✅ Test PASSED: Created {insertadas.Count} remuneraciones successfully");
    }

    [Fact]
    public async Task CreateRemuneracionesAsync_WithEmptyList_InsertsNothing()
    {
        // Arrange: Create empleado
        var (userId, _, empleadoId) = await CreateTestEmpleadoAsync();

        var listaVacia = new List<RemuneracionItemDto>();

        // Act: Try to insert empty list
        await _legacyDataService.CreateRemuneracionesAsync(userId, empleadoId, listaVacia);
        ClearChangeTracker();

        // Assert: Count should be 0
        var count = await DbContext.Set<Remuneracione>()
            .CountAsync(r => r.UserId == userId && r.EmpleadoId == empleadoId);

        count.Should().Be(0, "because empty list should not insert anything");

        _output.WriteLine("✅ Test PASSED: Empty list handled correctly");
    }

    #endregion

    #region 3. UpdateRemuneracionesAsync Tests (1 test)

    [Fact]
    public async Task UpdateRemuneracionesAsync_ReplacesAllInSingleTransaction()
    {
        // Arrange: Create empleado with 2 existing remuneraciones
        var (userId, _, empleadoId) = await CreateTestEmpleadoAsync();

        var oldRem1 = new Remuneracione { UserId = userId, EmpleadoId = empleadoId, Descripcion = "Old 1", Monto = 1000 };
        var oldRem2 = new Remuneracione { UserId = userId, EmpleadoId = empleadoId, Descripcion = "Old 2", Monto = 2000 };
        DbContext.Set<Remuneracione>().AddRange(oldRem1, oldRem2);
        await DbContext.SaveChangesAsync();
        ClearChangeTracker();

        // New remuneraciones to replace old ones
        var nuevasRemuneraciones = new List<RemuneracionItemDto>
        {
            new RemuneracionItemDto { Descripcion = "New 1", Monto = 3000 },
            new RemuneracionItemDto { Descripcion = "New 2", Monto = 4000 },
            new RemuneracionItemDto { Descripcion = "New 3", Monto = 5000 }
        };

        // Act: Update (replace all in single transaction)
        await _legacyDataService.UpdateRemuneracionesAsync(userId, empleadoId, nuevasRemuneraciones);
        ClearChangeTracker();

        // Assert: Only new remuneraciones should exist
        var current = await DbContext.Set<Remuneracione>()
            .Where(r => r.UserId == userId && r.EmpleadoId == empleadoId)
            .ToListAsync();

        current.Should().HaveCount(3, "because 3 new remuneraciones replace 2 old ones");
        current.Should().NotContain(r => r.Descripcion == "Old 1");
        current.Should().NotContain(r => r.Descripcion == "Old 2");
        current.Should().Contain(r => r.Descripcion == "New 1" && r.Monto == 3000);
        current.Should().Contain(r => r.Descripcion == "New 2" && r.Monto == 4000);
        current.Should().Contain(r => r.Descripcion == "New 3" && r.Monto == 5000);

        _output.WriteLine("✅ Test PASSED: All remuneraciones replaced in single ACID transaction");
    }

    #endregion

    #region 4. DarDeBajaEmpleadoAsync Tests (2 tests)

    [Fact]
    public async Task DarDeBajaEmpleadoAsync_WithValidData_UpdatesSoftDeleteFields()
    {
        // Arrange: Create empleado
        var (userId, _, empleadoId) = await CreateTestEmpleadoAsync();
        
        var empleado = await DbContext.Set<Empleado>().FindAsync(empleadoId);
        empleado.Should().NotBeNull();
        empleado!.Activo.Should().BeTrue("before soft delete");

        var fechaBaja = DateTime.UtcNow.Date;
        var motivo = "Renuncia";
        var prestaciones = 15000m;

        // Act: Soft delete empleado
        var result = await _legacyDataService.DarDeBajaEmpleadoAsync(empleadoId, userId, fechaBaja, prestaciones, motivo);
        ClearChangeTracker();

        // Assert: Verify soft delete fields updated
        result.Should().BeTrue("because empleado exists and belongs to userId");
        
        var updated = await DbContext.Set<Empleado>().FindAsync(empleadoId);
        updated.Should().NotBeNull();
        updated!.Activo.Should().BeFalse("because empleado was given de baja");
        updated.FechaSalida.Should().NotBeNull();
        updated.FechaSalida.Value.Date.Should().Be(fechaBaja);
        updated.MotivoBaja.Should().Be(motivo);
        updated.Prestaciones.Should().Be(prestaciones);

        _output.WriteLine("✅ Test PASSED: Empleado soft deleted successfully");
    }

    [Fact]
    public async Task DarDeBajaEmpleadoAsync_WithDifferentUserId_ReturnsFalse()
    {
        // Arrange: Create empleado
        var (userId, _, empleadoId) = await CreateTestEmpleadoAsync();

        // Act: Try to delete with different userId (ownership validation)
        var userId2 = Guid.NewGuid().ToString();
        var result = await _legacyDataService.DarDeBajaEmpleadoAsync(
            empleadoId, 
            userId2, 
            DateTime.UtcNow.Date, 
            0, 
            "Test");
        ClearChangeTracker();

        // Assert: Operation should fail
        result.Should().BeFalse("because empleado does not belong to userId2");

        var unchanged = await DbContext.Set<Empleado>().FindAsync(empleadoId);
        unchanged!.Activo.Should().BeTrue("because unauthorized baja should not modify empleado");

        _output.WriteLine("✅ Test PASSED: Ownership validation prevents unauthorized baja");
    }

    #endregion

    #region 5. CancelarTrabajoAsync Tests (1 test)

    [Fact]
    public async Task CancelarTrabajoAsync_WithValidData_SetsStatusToCancelled()
    {
        // Arrange: Create detalle contratacion with valid FK
        // First create EmpleadoTemporal (parent)
        var (userId, _, _) = await CreateTestEmpleadoAsync();
        
        var empleadoTemporal = new EmpleadosTemporale
        {
            UserId = userId,
            Nombre = "Test",
            Apellido = "Temporal",
            Identificacion = GenerateRandomIdentification(),
            FechaRegistro = DateTime.UtcNow
        };
        DbContext.Set<EmpleadosTemporale>().Add(empleadoTemporal);
        await DbContext.SaveChangesAsync();
        var contratacionId = empleadoTemporal.ContratacionId;
        
        // Now create DetalleContratacione
        var detalle = new DetalleContratacione
        {
            ContratacionId = contratacionId,
            DescripcionCorta = "Test trabajo",
            Estatus = 1, // Active
            MontoAcordado = 5000
        };
        DbContext.Set<DetalleContratacione>().Add(detalle);
        await DbContext.SaveChangesAsync();
        var detalleId = detalle.DetalleId;
        ClearChangeTracker();

        _output.WriteLine($"📋 Created DetalleContratacione: ContratacionId={contratacionId}, DetalleId={detalleId}, Estatus=1");

        // Act: Cancel trabajo
        var result = await _legacyDataService.CancelarTrabajoAsync(contratacionId, detalleId);
        ClearChangeTracker();

        // Assert: Estatus should be 3 (Cancelled)
        result.Should().BeTrue("because detalle exists");

        var cancelled = await DbContext.Set<DetalleContratacione>().FindAsync(detalleId);
        cancelled.Should().NotBeNull();
        cancelled!.Estatus.Should().Be(3, "because trabajo was cancelled");

        _output.WriteLine("✅ Test PASSED: Trabajo cancelled successfully (estatus = 3)");
    }

    #endregion

    #region 6. EliminarReciboEmpleadoAsync Tests (1 test)

    [Fact]
    public async Task EliminarReciboEmpleadoAsync_WithRecibo_DeletesHeaderAndDetalles()
    {
        // Arrange: Create empleado, header + 2 detalles
        var (userId, _, empleadoId) = await CreateTestEmpleadoAsync();

        var header = new EmpleadorRecibosHeader
        {
            UserId = userId,
            EmpleadoId = empleadoId,
            FechaPago = DateTime.UtcNow,
            ConceptoPago = "Salario Quincenal"
        };
        DbContext.Set<EmpleadorRecibosHeader>().Add(header);
        await DbContext.SaveChangesAsync();
        var pagoId = header.PagoId;

        var detalle1 = new EmpleadorRecibosDetalle { PagoId = pagoId, Concepto = "Salario", Monto = 25000 };
        var detalle2 = new EmpleadorRecibosDetalle { PagoId = pagoId, Concepto = "Bono", Monto = 5000 };
        DbContext.Set<EmpleadorRecibosDetalle>().AddRange(detalle1, detalle2);
        await DbContext.SaveChangesAsync();
        ClearChangeTracker();

        _output.WriteLine($"📋 Created EmpleadorRecibosHeader: PagoId={pagoId} with 2 detalles");

        // Act: Delete recibo (should cascade delete detalles)
        var result = await _legacyDataService.EliminarReciboEmpleadoAsync(pagoId);
        ClearChangeTracker();

        // Assert: Header and detalles should be deleted
        result.Should().BeTrue("because recibo exists");

        var deletedHeader = await DbContext.Set<EmpleadorRecibosHeader>().FindAsync(pagoId);
        deletedHeader.Should().BeNull("because header was deleted");

        var deletedDetalles = await DbContext.Set<EmpleadorRecibosDetalle>()
            .Where(d => d.PagoId == pagoId)
            .ToListAsync();
        deletedDetalles.Should().BeEmpty("because detalles cascade deleted");

        _output.WriteLine("✅ Test PASSED: Recibo header and detalles deleted (2-level cascade)");
    }

    #endregion

    #region 7. EliminarReciboContratacionAsync Tests (1 test)

    [Fact]
    public async Task EliminarReciboContratacionAsync_WithRecibo_DeletesHeaderAndDetalles()
    {
        // Arrange: Create empleadoTemporal, header + 2 detalles
        var (userId, _, _) = await CreateTestEmpleadoAsync();
        
        var empleadoTemporal = new EmpleadosTemporale
        {
            UserId = userId,
            Nombre = "Test",
            Apellido = "Worker",
            Identificacion = GenerateRandomIdentification()
        };
        DbContext.Set<EmpleadosTemporale>().Add(empleadoTemporal);
        await DbContext.SaveChangesAsync();
        var contratacionId = empleadoTemporal.ContratacionId;

        var header = new EmpleadorRecibosHeaderContratacione
        {
            ContratacionId = contratacionId,
            UserId = userId,
            FechaPago = DateTime.UtcNow,
            ConceptoPago = "Pago Servicio"
        };
        DbContext.Set<EmpleadorRecibosHeaderContratacione>().Add(header);
        await DbContext.SaveChangesAsync();
        var pagoId = header.PagoId;

        var detalle1 = new EmpleadorRecibosDetalleContratacione { PagoId = pagoId, Concepto = "Pago 1", Monto = 6000 };
        var detalle2 = new EmpleadorRecibosDetalleContratacione { PagoId = pagoId, Concepto = "Pago 2", Monto = 4000 };
        DbContext.Set<EmpleadorRecibosDetalleContratacione>().AddRange(detalle1, detalle2);
        await DbContext.SaveChangesAsync();
        ClearChangeTracker();

        _output.WriteLine($"📋 Created EmpleadorRecibosHeaderContratacione: PagoId={pagoId} with 2 detalles");

        // Act: Delete recibo
        var result = await _legacyDataService.EliminarReciboContratacionAsync(pagoId);
        ClearChangeTracker();

        // Assert: Header and detalles deleted
        result.Should().BeTrue("because recibo exists");

        var deletedHeader = await DbContext.Set<EmpleadorRecibosHeaderContratacione>().FindAsync(pagoId);
        deletedHeader.Should().BeNull("because header was deleted");

        var deletedDetalles = await DbContext.Set<EmpleadorRecibosDetalleContratacione>()
            .Where(d => d.PagoId == pagoId)
            .ToListAsync();
        deletedDetalles.Should().BeEmpty("because detalles cascade deleted");

        _output.WriteLine("✅ Test PASSED: Recibo contratacion header and detalles deleted (2-level cascade)");
    }

    #endregion

    #region 8. EliminarEmpleadoTemporalAsync Tests (2 tests)

    [Fact]
    public async Task EliminarEmpleadoTemporalAsync_WithRecibos_DeletesAll3Levels()
    {
        // Arrange: Create empleadoTemporal + 2 headers + 3 detalles (3-level cascade)
        var (userId, _, _) = await CreateTestEmpleadoAsync();
        
        var empleadoTemporal = new EmpleadosTemporale
        {
            UserId = userId,
            Nombre = "Temporal",
            Apellido = "Worker",
            Identificacion = GenerateRandomIdentification()
        };
        DbContext.Set<EmpleadosTemporale>().Add(empleadoTemporal);
        await DbContext.SaveChangesAsync();
        var contratacionId = empleadoTemporal.ContratacionId;

        // Level 2: Headers
        var header1 = new EmpleadorRecibosHeaderContratacione { ContratacionId = contratacionId, UserId = userId, FechaPago = DateTime.UtcNow };
        var header2 = new EmpleadorRecibosHeaderContratacione { ContratacionId = contratacionId, UserId = userId, FechaPago = DateTime.UtcNow };
        DbContext.Set<EmpleadorRecibosHeaderContratacione>().AddRange(header1, header2);
        await DbContext.SaveChangesAsync();

        // Level 1: Detalles
        var detalle1 = new EmpleadorRecibosDetalleContratacione { PagoId = header1.PagoId, Concepto = "Pago 1", Monto = 5000 };
        var detalle2 = new EmpleadorRecibosDetalleContratacione { PagoId = header2.PagoId, Concepto = "Pago 2A", Monto = 1500 };
        var detalle3 = new EmpleadorRecibosDetalleContratacione { PagoId = header2.PagoId, Concepto = "Pago 2B", Monto = 1500 };
        DbContext.Set<EmpleadorRecibosDetalleContratacione>().AddRange(detalle1, detalle2, detalle3);
        await DbContext.SaveChangesAsync();
        ClearChangeTracker();

        _output.WriteLine($"📋 Created EmpleadoTemporal with 2 headers and 3 detalles (3-level cascade)");

        // Act: Delete empleado temporal (should cascade 3 levels)
        var result = await _legacyDataService.EliminarEmpleadoTemporalAsync(contratacionId);
        ClearChangeTracker();

        // Assert: All 3 levels deleted
        result.Should().BeTrue("because empleado temporal exists");

        // Level 3: EmpleadoTemporal should be deleted
        var deletedEmpleado = await DbContext.Set<EmpleadosTemporale>()
            .FirstOrDefaultAsync(et => et.ContratacionId == contratacionId);
        deletedEmpleado.Should().BeNull("because empleado temporal was deleted");

        // Level 2: Headers should be deleted
        var deletedHeaders = await DbContext.Set<EmpleadorRecibosHeaderContratacione>()
            .Where(h => h.ContratacionId == contratacionId)
            .ToListAsync();
        deletedHeaders.Should().BeEmpty("because headers cascade deleted");

        // Level 1: Detalles should be deleted
        var deletedDetalles = await DbContext.Set<EmpleadorRecibosDetalleContratacione>()
            .Where(d => d.PagoId == header1.PagoId || d.PagoId == header2.PagoId)
            .ToListAsync();
        deletedDetalles.Should().BeEmpty("because detalles cascade deleted");

        _output.WriteLine("✅ Test PASSED: 3-level cascade delete (Detalles → Headers → EmpleadoTemporal)");
    }

    [Fact]
    public async Task EliminarEmpleadoTemporalAsync_WithoutRecibos_DeletesEmpleadoOnly()
    {
        // Arrange: Create empleadoTemporal without receipts
        var (userId, _, _) = await CreateTestEmpleadoAsync();
        
        var empleadoTemporal = new EmpleadosTemporale
        {
            UserId = userId,
            Nombre = "Simple",
            Apellido = "Worker",
            Identificacion = GenerateRandomIdentification()
        };
        DbContext.Set<EmpleadosTemporale>().Add(empleadoTemporal);
        await DbContext.SaveChangesAsync();
        var contratacionId = empleadoTemporal.ContratacionId;
        ClearChangeTracker();

        _output.WriteLine($"📋 Created EmpleadoTemporal without receipts: ContratacionId={contratacionId}");

        // Act: Delete empleado temporal (no receipts to cascade)
        var result = await _legacyDataService.EliminarEmpleadoTemporalAsync(contratacionId);
        ClearChangeTracker();

        // Assert: EmpleadoTemporal deleted, no errors
        result.Should().BeTrue("because empleado temporal exists");

        var deleted = await DbContext.Set<EmpleadosTemporale>()
            .FirstOrDefaultAsync(et => et.ContratacionId == contratacionId);
        deleted.Should().BeNull("because empleado temporal was deleted");

        _output.WriteLine("✅ Test PASSED: EmpleadoTemporal deleted without errors (no receipts)");
    }

    #endregion
}
