using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using MiGenteEnLinea.Application.Features.Empleados.Commands.CreateEmpleado;
using MiGenteEnLinea.Application.Features.Empleados.DTOs;
using MiGenteEnLinea.IntegrationTests.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace MiGenteEnLinea.IntegrationTests.Services;

/// <summary>
/// Tests para validar la funcionalidad migrada de LegacyDataService usando 100% API endpoints.
/// Este enfoque evita conflictos de DbContext (Domain vs Generated entities).
/// Todos los tests usan POST/GET/PUT/DELETE del API REST.
/// </summary>
[Collection("IntegrationTests")]
public class LegacyDataServiceApiTests : IntegrationTestBase
{
    private readonly ITestOutputHelper _output;

    public LegacyDataServiceApiTests(TestWebApplicationFactory factory, ITestOutputHelper output)
        : base(factory)
    {
        _output = output;
    }

    #region Helper Methods

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

    #region 1. Delete Remuneracion Tests (3 tests)

    [Fact]
    public async Task DeleteRemuneracion_WithValidData_DeletesSuccessfully()
    {
        // Arrange: Create empleado
        var (userId, _, empleadoId) = await CreateTestEmpleadoAsync();
        _output.WriteLine($"✅ Created Empleado: UserId={userId}, EmpleadoId={empleadoId}");

        // Create remuneraciones via batch endpoint
        var remuneraciones = new List<object>
        {
            new { Descripcion = "Bono Test", Monto = 5000m }
        };

        var createResponse = await Client.PostAsJsonAsync($"/api/empleados/{empleadoId}/remuneraciones/batch", remuneraciones);
        createResponse.EnsureSuccessStatusCode();

        // Get remuneraciones to obtain ID
        var getResponse = await Client.GetAsync($"/api/empleados/{empleadoId}/remuneraciones");
        var created = await getResponse.Content.ReadFromJsonAsync<List<RemuneracionDto>>();
        created.Should().HaveCount(1);
        var remId = created![0].Id;

        _output.WriteLine($"📋 Created Remuneracion: Id={remId}");

        // Act: Delete via API
        var deleteResponse = await Client.DeleteAsync($"/api/empleados/remuneraciones/{remId}");
        deleteResponse.EnsureSuccessStatusCode();

        // Assert: Verify deleted
        var afterDelete = await Client.GetAsync($"/api/empleados/{empleadoId}/remuneraciones");
        var remaining = await afterDelete.Content.ReadFromJsonAsync<List<RemuneracionDto>>();
        remaining.Should().BeEmpty();

        _output.WriteLine("✅ Test PASSED: Delete via API successful");
    }

    [Fact]
    public async Task DeleteRemuneracion_WithInvalidId_Returns404OrNoContent()
    {
        // Arrange: Create empleado (no remuneraciones)
        var (userId, _, empleadoId) = await CreateTestEmpleadoAsync();

        // Act: Try to delete non-existent remuneracion
        var deleteResponse = await Client.DeleteAsync($"/api/empleados/remuneraciones/99999");

        // Assert: Should return 404 or 204 (depending on implementation)
        // 204 = graceful handling (no-op), 404 = strict validation
        deleteResponse.StatusCode.Should().Match(code => 
            code == System.Net.HttpStatusCode.NotFound || 
            code == System.Net.HttpStatusCode.NoContent);

        _output.WriteLine($"✅ Test PASSED: Invalid delete returned {deleteResponse.StatusCode}");
    }

    [Fact]
    public async Task DeleteRemuneracion_WithDifferentUser_PreventsDeletion()
    {
        // Arrange: Create empleado and remuneracion as user1
        var (userId1, email1, empleadoId) = await CreateTestEmpleadoAsync();

        var remuneraciones = new List<object>
        {
            new { Descripcion = "Protected Bono", Monto = 3000m }
        };

        await Client.PostAsJsonAsync($"/api/empleados/{empleadoId}/remuneraciones/batch", remuneraciones);

        var getResponse = await Client.GetAsync($"/api/empleados/{empleadoId}/remuneraciones");
        var created = await getResponse.Content.ReadFromJsonAsync<List<RemuneracionDto>>();
        var remId = created![0].Id;

        _output.WriteLine($"📋 User1 ({userId1}) created Remuneracion Id={remId}");

        // Act: Login as different user and try to delete
        var (userId2, email2) = await RegisterUserAsync(
            GenerateUniqueEmail("user2"), "Password123!", "Empleador", "User", "Two");
        await LoginAsync(email2, "Password123!");

        var deleteResponse = await Client.DeleteAsync($"/api/empleados/remuneraciones/{remId}");

        // Assert: Should fail (403 Forbidden or 404 Not Found)
        deleteResponse.StatusCode.Should().Match(code =>
            code == System.Net.HttpStatusCode.Forbidden ||
            code == System.Net.HttpStatusCode.NotFound ||
            code == System.Net.HttpStatusCode.Unauthorized);

        _output.WriteLine($"✅ Test PASSED: Unauthorized delete prevented ({deleteResponse.StatusCode})");
    }

    #endregion

    #region 2. Create Remuneraciones Tests (2 tests)

    [Fact]
    public async Task CreateRemuneraciones_WithMultipleItems_InsertsAll()
    {
        // Arrange
        var (userId, _, empleadoId) = await CreateTestEmpleadoAsync();

        var remuneraciones = new List<object>
        {
            new { Descripcion = "Salario Base", Monto = 30000m },
            new { Descripcion = "Bono Navidad", Monto = 5000m },
            new { Descripcion = "Comisión", Monto = 8000m }
        };

        // Act: Create via batch endpoint
        var createResponse = await Client.PostAsJsonAsync($"/api/empleados/{empleadoId}/remuneraciones/batch", remuneraciones);
        createResponse.EnsureSuccessStatusCode();

        // Assert: Verify all 3 inserted
        var getResponse = await Client.GetAsync($"/api/empleados/{empleadoId}/remuneraciones");
        var inserted = await getResponse.Content.ReadFromJsonAsync<List<RemuneracionDto>>();

        inserted.Should().HaveCount(3);
        inserted.Should().Contain(r => r.Descripcion == "Salario Base" && r.Monto == 30000);
        inserted.Should().Contain(r => r.Descripcion == "Bono Navidad" && r.Monto == 5000);
        inserted.Should().Contain(r => r.Descripcion == "Comisión" && r.Monto == 8000);

        _output.WriteLine($"✅ Test PASSED: Created {inserted!.Count} remuneraciones");
    }

    [Fact]
    public async Task CreateRemuneraciones_WithEmptyList_ReturnsValidationError()
    {
        // Arrange
        var (userId, _, empleadoId) = await CreateTestEmpleadoAsync();

        var emptyList = new List<object>();

        // Act: Create with empty list (should fail validation)
        var createResponse = await Client.PostAsJsonAsync($"/api/empleados/{empleadoId}/remuneraciones/batch", emptyList);

        // Assert: Should return 400 Bad Request (validation error)
        createResponse.StatusCode.Should().Be(System.Net.HttpStatusCode.BadRequest);

        var errorContent = await createResponse.Content.ReadAsStringAsync();
        errorContent.Should().Contain("remuneración", "error message should mention remuneraciones");

        _output.WriteLine("✅ Test PASSED: Empty list rejected with validation error");
    }

    #endregion

    #region 3. Update Remuneraciones Tests (1 test)

    [Fact]
    public async Task UpdateRemuneraciones_ReplacesAllInSingleTransaction()
    {
        // Arrange: Create empleado
        var (userId, _, empleadoId) = await CreateTestEmpleadoAsync();

        // Create 2 old remuneraciones
        var oldRems = new List<object>
        {
            new { Descripcion = "Old 1", Monto = 1000m },
            new { Descripcion = "Old 2", Monto = 2000m }
        };

        await Client.PostAsJsonAsync($"/api/empleados/{empleadoId}/remuneraciones/batch", oldRems);

        var beforeUpdate = await Client.GetAsync($"/api/empleados/{empleadoId}/remuneraciones");
        var before = await beforeUpdate.Content.ReadFromJsonAsync<List<RemuneracionDto>>();
        before.Should().HaveCount(2);

        _output.WriteLine($"📋 Before update: {before!.Count} remuneraciones");

        // Act: Update (replace all) with 3 new remuneraciones
        var newRems = new List<object>
        {
            new { Descripcion = "New 1", Monto = 3000m },
            new { Descripcion = "New 2", Monto = 4000m },
            new { Descripcion = "New 3", Monto = 5000m }
        };

        var updateResponse = await Client.PutAsJsonAsync($"/api/empleados/{empleadoId}/remuneraciones/batch", newRems);
        updateResponse.EnsureSuccessStatusCode();

        // Assert: Only 3 new remuneraciones exist
        var afterUpdate = await Client.GetAsync($"/api/empleados/{empleadoId}/remuneraciones");
        var after = await afterUpdate.Content.ReadFromJsonAsync<List<RemuneracionDto>>();

        after.Should().HaveCount(3, "because update replaces all");
        after.Should().NotContain(r => r.Descripcion == "Old 1");
        after.Should().NotContain(r => r.Descripcion == "Old 2");
        after.Should().Contain(r => r.Descripcion == "New 1" && r.Monto == 3000);
        after.Should().Contain(r => r.Descripcion == "New 2" && r.Monto == 4000);
        after.Should().Contain(r => r.Descripcion == "New 3" && r.Monto == 5000);

        _output.WriteLine($"✅ Test PASSED: Update replaced 2 old with 3 new remuneraciones");
    }

    #endregion

    #region 4. Dar de Baja Empleado Tests (2 tests)

    [Fact]
    public async Task DarDeBaja_WithValidData_UpdatesSoftDeleteFields()
    {
        // Arrange: Create empleado
        var (userId, _, empleadoId) = await CreateTestEmpleadoAsync();
        _output.WriteLine($"📋 Testing soft delete for EmpleadoId={empleadoId}");

        // Act: Dar de baja via API (✅ PUT not POST)
        var request = new
        {
            FechaBaja = DateTime.UtcNow.Date,
            Prestaciones = 15000m,
            Motivo = "Renuncia"
        };

        var response = await Client.PutAsJsonAsync($"/api/empleados/{empleadoId}/dar-de-baja", request);
        response.EnsureSuccessStatusCode();

        // Assert: Verify soft delete fields via GET empleado
        var getResponse = await Client.GetAsync($"/api/empleados/{empleadoId}");
        getResponse.EnsureSuccessStatusCode();
        
        var empleadoJson = await getResponse.Content.ReadAsStringAsync();
        empleadoJson.Should().Contain("\"activo\":false", "empleado should be inactive");
        empleadoJson.Should().Contain("\"motivoBaja\":\"Renuncia\"");

        _output.WriteLine("✅ Test PASSED: Soft delete updated fields correctly");
    }

    [Fact]
    public async Task DarDeBaja_WithDifferentUser_ReturnsForbiddenOrNotFound()
    {
        // Arrange: Create empleado as user1
        var (userId1, email1, empleadoId) = await CreateTestEmpleadoAsync();

        // Act: Login as user2 and try to dar de baja
        var (userId2, email2) = await RegisterUserAsync(
            GenerateUniqueEmail("user2"), "Password123!", "Empleador", "User", "Two");
        await LoginAsync(email2, "Password123!");

        var request = new
        {
            FechaBaja = DateTime.UtcNow.Date,
            Prestaciones = 10000m,
            Motivo = "Unauthorized attempt"
        };

        var response = await Client.PutAsJsonAsync($"/api/empleados/{empleadoId}/dar-de-baja", request);

        // Assert: Should fail (403 or 404)
        response.StatusCode.Should().Match(code =>
            code == System.Net.HttpStatusCode.Forbidden ||
            code == System.Net.HttpStatusCode.NotFound ||
            code == System.Net.HttpStatusCode.Unauthorized);

        _output.WriteLine($"✅ Test PASSED: Unauthorized dar de baja prevented ({response.StatusCode})");
    }

    #endregion
}
