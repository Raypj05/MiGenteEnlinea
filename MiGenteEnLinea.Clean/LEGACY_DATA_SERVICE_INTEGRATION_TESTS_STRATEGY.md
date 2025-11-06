# LegacyDataService Integration Tests - API-Based Strategy

**Fecha:** 31 de octubre de 2025  
**Estado:** ✅ Estrategia definida - Implementación en progreso

---

## 🎯 Objetivo

Crear tests de integración **100% API-based** que validen el stack completo:

```
Controller → Application (CQRS) → LegacyDataService → Database → Validate via API
```

**NO** usamos acceso directo a `DbContext.Set<Generated.Entity>()` porque causa conflicto de namespaces (Domain entities vs Generated entities).

---

## ✅ Patrón de Test API-Based

### ❌ ANTES (Incorrecto - Causa conflicto)

```csharp
[Fact]
public async Task DeleteRemuneracionAsync_Test()
{
    // ❌ NO: Acceso directo a DbContext con Generated entities
    var empleado = new Empleado { ... };
    DbContext.Set<Empleado>().Add(empleado);
    await DbContext.SaveChangesAsync();
    
    // Act
    await _legacyDataService.DeleteRemuneracionAsync(...);
    
    // ❌ NO: Validación directa con DbContext
    var deleted = await DbContext.Set<Remuneracione>().FindAsync(id);
    deleted.Should().BeNull();
}
```

**Problema:** `DbContext` en `IntegrationTestBase` solo mapea **Domain entities**, pero LegacyDataService usa **Generated entities**. Causa:

```
Cannot create a DbSet for 'Generated.Empleado' because context contains 'Domain.Empleados.Empleado'
```

### ✅ DESPUÉS (Correcto - API-based)

```csharp
[Fact]
public async Task DeleteRemuneracionAsync_WithValidData_DeletesSuccessfully()
{
    // ✅ Arrange: Create via API
    var (userId, _, empleadoId) = await CreateTestEmpleadoAsync(); // Uses POST /api/empleados
    
    var createDto = new List<RemuneracionItemDto>
    {
        new RemuneracionItemDto { Descripcion = "Test Bono", Monto = 5000 },
        new RemuneracionItemDto { Descripcion = "Comision", Monto = 3000 }
    };
    await _legacyDataService.CreateRemuneracionesAsync(userId, empleadoId, createDto);
    
    // ✅ Get data via API to verify creation
    var response = await Client.GetAsync($"/api/empleados/{empleadoId}/remuneraciones");
    response.EnsureSuccessStatusCode();
    var remuneraciones = await response.Content.ReadFromJsonAsync<List<RemuneracionDto>>();
    remuneraciones.Should().HaveCount(2);
    
    var remuneracionToDelete = remuneraciones!.First();
    
    // ✅ Act: Call LegacyDataService method
    await _legacyDataService.DeleteRemuneracionAsync(userId, remuneracionToDelete.Id);
    
    // ✅ Assert: Validate via API (not DbContext)
    var afterDeleteResponse = await Client.GetAsync($"/api/empleados/{empleadoId}/remuneraciones");
    var afterDelete = await afterDeleteResponse.Content.ReadFromJsonAsync<List<RemuneracionDto>>();
    
    afterDelete.Should().HaveCount(1, "because one was deleted");
    afterDelete.Should().NotContain(r => r.Id == remuneracionToDelete.Id);
}
```

**Ventajas:**

1. ✅ No conflicto de namespaces (Domain vs Generated)
2. ✅ Valida el stack completo (como producción)
3. ✅ Tests más realistas (usan los mismos endpoints que el frontend)
4. ✅ Detectan problemas de integración real (serialización JSON, routing, auth, etc.)

---

## 📋 13 Tests a Refactorizar

### 1. DeleteRemuneracionAsync (3 tests)

**✅ Test 1: WithValidData_DeletesSuccessfully** - COMPLETADO

```csharp
// Pattern: Create via LegacyService → Get via API → Delete via LegacyService → Validate via API
var (userId, _, empleadoId) = await CreateTestEmpleadoAsync();
await _legacyDataService.CreateRemuneracionesAsync(userId, empleadoId, [...]);
var beforeDelete = await Client.GetAsync($"/api/empleados/{empleadoId}/remuneraciones");
await _legacyDataService.DeleteRemuneracionAsync(userId, remId);
var afterDelete = await Client.GetAsync($"/api/empleados/{empleadoId}/remuneraciones");
// Assert: count decreased
```

**⏳ Test 2: WithInvalidId_DoesNotThrowException**

```csharp
// Pattern: Call with non-existent ID → Should handle gracefully
var (userId, _, _) = await CreateTestEmpleadoAsync();
var action = async () => await _legacyDataService.DeleteRemuneracionAsync(userId, 99999);
await action.Should().NotThrowAsync("because method handles null gracefully");
```

**⏳ Test 3: WithDifferentUserId_DoesNotDelete**

```csharp
// Pattern: Create as User1 → Try delete as User2 → Validate not deleted
var (userId1, _, empleadoId) = await CreateTestEmpleadoAsync();
await _legacyDataService.CreateRemuneracionesAsync(userId1, empleadoId, [...]);
var before = await Client.GetAsync($"/api/empleados/{empleadoId}/remuneraciones");
var remId = before.First().Id;

var userId2 = Guid.NewGuid().ToString();
await _legacyDataService.DeleteRemuneracionAsync(userId2, remId); // Should not delete

var after = await Client.GetAsync($"/api/empleados/{empleadoId}/remuneraciones");
after.Should().HaveCount(before.Count, "because userId2 cannot delete userId1's data");
```

### 2. CreateRemuneracionesAsync (2 tests)

**⏳ Test 4: WithMultipleItems_InsertsAll**

```csharp
// Pattern: Create multiple via LegacyService → Validate via API GET
var (userId, _, empleadoId) = await CreateTestEmpleadoAsync();
var nuevas = new List<RemuneracionItemDto>
{
    new() { Descripcion = "Salario Base", Monto = 30000 },
    new() { Descripcion = "Bono Navidad", Monto = 5000 },
    new() { Descripcion = "Comisión", Monto = 8000 }
};

await _legacyDataService.CreateRemuneracionesAsync(userId, empleadoId, nuevas);

var response = await Client.GetAsync($"/api/empleados/{empleadoId}/remuneraciones");
var insertadas = await response.Content.ReadFromJsonAsync<List<RemuneracionDto>>();

insertadas.Should().HaveCount(3);
insertadas.Should().Contain(r => r.Descripcion == "Salario Base" && r.Monto == 30000);
```

**⏳ Test 5: WithEmptyList_InsertsNothing**

```csharp
// Pattern: Create with empty list → Validate count is 0
var (userId, _, empleadoId) = await CreateTestEmpleadoAsync();
await _legacyDataService.CreateRemuneracionesAsync(userId, empleadoId, new List<RemuneracionItemDto>());

var response = await Client.GetAsync($"/api/empleados/{empleadoId}/remuneraciones");
var count = (await response.Content.ReadFromJsonAsync<List<RemuneracionDto>>())!.Count;

count.Should().Be(0, "because empty list should not insert anything");
```

### 3. UpdateRemuneracionesAsync (1 test)

**⏳ Test 6: ReplacesAllInSingleTransaction**

```csharp
// Pattern: Create 2 → Update with 3 new → Validate only new 3 exist
var (userId, _, empleadoId) = await CreateTestEmpleadoAsync();

// Create 2 old
await _legacyDataService.CreateRemuneracionesAsync(userId, empleadoId, [
    new() { Descripcion = "Old 1", Monto = 1000 },
    new() { Descripcion = "Old 2", Monto = 2000 }
]);

// Update with 3 new (replace transaction)
await _legacyDataService.UpdateRemuneracionesAsync(userId, empleadoId, [
    new() { Descripcion = "New 1", Monto = 3000 },
    new() { Descripcion = "New 2", Monto = 4000 },
    new() { Descripcion = "New 3", Monto = 5000 }
]);

// Validate only new exist
var response = await Client.GetAsync($"/api/empleados/{empleadoId}/remuneraciones");
var current = await response.Content.ReadFromJsonAsync<List<RemuneracionDto>>();

current.Should().HaveCount(3, "because 3 new replaced 2 old");
current.Should().NotContain(r => r.Descripcion == "Old 1");
current.Should().Contain(r => r.Descripcion == "New 1" && r.Monto == 3000);
```

### 4. DarDeBajaEmpleadoAsync (2 tests)

**⏳ Test 7: WithValidData_UpdatesSoftDeleteFields**

```csharp
// Pattern: Create empleado → Call DarDeBajaAsync → Get empleado via API → Validate soft delete fields
var (userId, _, empleadoId) = await CreateTestEmpleadoAsync();

var fechaBaja = DateTime.UtcNow.Date;
var result = await _legacyDataService.DarDeBajaEmpleadoAsync(
    empleadoId, userId, fechaBaja, 15000m, "Renuncia");

result.Should().BeTrue();

// Validate via API GET /api/empleados/{id}
var response = await Client.GetAsync($"/api/empleados/{empleadoId}");
var empleado = await response.Content.ReadFromJsonAsync<EmpleadoDetalleDto>();

empleado!.Activo.Should().BeFalse();
empleado.FechaSalida.Should().NotBeNull();
empleado.MotivoBaja.Should().Be("Renuncia");
empleado.Prestaciones.Should().Be(15000m);
```

**⏳ Test 8: WithDifferentUserId_ReturnsFalse**

```csharp
// Pattern: Ownership validation
var (userId1, _, empleadoId) = await CreateTestEmpleadoAsync();
var userId2 = Guid.NewGuid().ToString();

var result = await _legacyDataService.DarDeBajaEmpleadoAsync(
    empleadoId, userId2, DateTime.UtcNow.Date, 0, "Test");

result.Should().BeFalse("because userId2 does not own empleado");

// Validate empleado still active via API
var response = await Client.GetAsync($"/api/empleados/{empleadoId}");
var empleado = await response.Content.ReadFromJsonAsync<EmpleadoDetalleDto>();
empleado!.Activo.Should().BeTrue("because unauthorized baja should not modify");
```

### 5. CancelarTrabajoAsync (1 test)

**⏳ Test 9: WithValidData_SetsStatusToCancelled**

**Nota:** Este test es más complejo porque necesita crear `DetalleContratacione` y `EmpleadosTemporale` (tablas de contrataciones temporales).

**Endpoints disponibles:**

- POST `/api/contratistas/contratar` - Create temporary hire
- GET `/api/empleadores/contrataciones` - Get all contracts
- GET `/api/empleadores/contrataciones/{id}` - Get contract details

```csharp
// Pattern: Create temp hire via API → Get contract detail → Cancel via LegacyService → Validate status=3
var (userId, _, _) = await CreateTestEmpleadoAsync();

// Create temporary contract via API
var contractRequest = new
{
    ContratistaId = 123, // Need to create contratista first
    DescripcionCorta = "Test trabajo",
    MontoAcordado = 5000m,
    FechaInicio = DateTime.Today,
    FechaFinal = DateTime.Today.AddDays(30)
};
var createResponse = await Client.PostAsJsonAsync("/api/contratistas/contratar", contractRequest);
var contractData = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
var contratacionId = contractData.GetProperty("contratacionId").GetInt32();
var detalleId = contractData.GetProperty("detalleId").GetInt32();

// Act: Cancel via LegacyService
var result = await _legacyDataService.CancelarTrabajoAsync(contratacionId, detalleId);
result.Should().BeTrue();

// Validate status=3 via API
var detailResponse = await Client.GetAsync($"/api/empleadores/contrataciones/{contratacionId}");
var detail = await detailResponse.Content.ReadFromJsonAsync<ContratacionDto>();
detail!.Estatus.Should().Be(3, "because trabajo was cancelled");
```

### 6-8. Recibos & EmpleadoTemporal Tests (4 tests)

**⏳ Tests 10-13:** Estos son más complejos porque involucran múltiples tablas relacionadas (receipts, detalles, etc.).

**Estrategia:**

1. Usar endpoints de nómina para crear recibos: POST `/api/empleados/{id}/procesar-pago`
2. Validar con GET endpoints de nómina
3. Eliminar con LegacyDataService
4. Validar eliminación con GET (should return 404 or empty)

---

## 🎯 Helper Methods Necesarios

### CreateTestContratistaAsync()

```csharp
private async Task<(string UserId, int ContratistaId)> CreateTestContratistaAsync()
{
    var email = GenerateUniqueEmail("contratista");
    var (userId, emailUsado) = await RegisterUserAsync(email, "Password123!", "Contratista", "Test", "Worker");
    await LoginAsync(emailUsado, "Password123!");
    
    // Contratista is auto-created on registration
    // Get contratista ID via API
    var response = await Client.GetAsync($"/api/contratistas/perfil");
    response.EnsureSuccessStatusCode();
    var contratista = await response.Content.ReadFromJsonAsync<ContratistaDto>();
    
    return (userId, contratista!.ContratistaId);
}
```

---

## 📊 Endpoints API Disponibles

**Empleados:**

- GET `/api/empleados/{id}` - Detalle empleado
- GET `/api/empleados/{id}/remuneraciones` - List remuneraciones
- POST `/api/empleados` - Create empleado
- PUT `/api/empleados/{id}/dar-de-baja` - Soft delete

**Contratistas:**

- GET `/api/contratistas/perfil` - Get own profile
- POST `/api/contratistas/contratar` - Create temporary hire
- GET `/api/empleadores/contrataciones` - List contracts

**Nómina:**

- POST `/api/empleados/{id}/procesar-pago` - Process payroll (creates receipts)
- GET `/api/empleados/{id}/recibos` - Get employee receipts
- GET `/api/empleadores/recibos` - Get all receipts for empleador

---

## ✅ Tests Completados

1. ✅ DeleteRemuneracionAsync_WithValidData_DeletesSuccessfully

## ⏳ Tests Pendientes

2. DeleteRemuneracionAsync_WithInvalidId_DoesNotThrowException
3. DeleteRemuneracionAsync_WithDifferentUserId_DoesNotDelete
4. CreateRemuneracionesAsync_WithMultipleItems_InsertsAll
5. CreateRemuneracionesAsync_WithEmptyList_InsertsNothing
6. UpdateRemuneracionesAsync_ReplacesAllInSingleTransaction
7. DarDeBajaEmpleadoAsync_WithValidData_UpdatesSoftDeleteFields
8. DarDeBajaEmpleadoAsync_WithDifferentUserId_ReturnsFalse
9. CancelarTrabajoAsync_WithValidData_SetsStatusToCancelled
10. EliminarReciboEmpleadoAsync_WithRecibo_DeletesHeaderAndDetalles
11. EliminarReciboContratacionAsync_WithRecibo_DeletesHeaderAndDetalles
12. EliminarEmpleadoTemporalAsync_WithRecibos_DeletesAll3Levels
13. EliminarEmpleadoTemporalAsync_WithoutRecibos_DeletesEmpleadoOnly

---

## 🎯 Próximos Pasos

1. ✅ Compilar y ejecutar Test 1 para validar patrón
2. ⏳ Implementar Tests 2-8 siguiendo el patrón API-based
3. ⏳ Implementar Tests 9-13 (más complejos, necesitan helpers adicionales)
4. ⏳ Ejecutar full test suite para detectar regresiones
5. ⏳ Documentar resultados finales

---

**Beneficios de esta estrategia:**

- ✅ Tests realistas (validan como producción)
- ✅ No conflicto de namespaces
- ✅ Detectan problemas de integración real
- ✅ Validación end-to-end del stack completo
- ✅ Más mantenibles (usan APIs públicos)
