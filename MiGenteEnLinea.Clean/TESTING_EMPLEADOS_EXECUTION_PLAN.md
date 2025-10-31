# 🎯 EmpleadosController Testing - Execution Plan

**Date:** October 30, 2025  
**Priority:** 3 (Critical - Depends on Empleadores)  
**Strategy:** Follow proven EmpleadoresController + ContratistasController patterns  
**Target:** 24-30 tests (120-150% of minimum 20)  
**Current Status:** 2/12 tests passing (16.7%) - **NEEDS COMPLETE REWRITE**

---

## 📊 Current Situation Analysis

### Test Execution Results

```
Failed!  - Failed: 10, Passed: 2, Skipped: 0, Total: 12, Duration: 7 s
```

**Root Cause:** Tests using **old RegisterUserAsync signature** incompatible with Identity migration.

### Passing Tests (2)
- ✅ `CreateEmpleado_WithoutAuthentication_ReturnsUnauthorized` 
- ✅ `UpdateEmpleado_WithoutAuthentication_ReturnsUnauthorized`

### Failing Tests (10)
All failing with `401 Unauthorized` in `LoginAsync()` - authentication infrastructure issue.

---

## 🏗️ Controller Analysis

### EmpleadosController Complexity

**Endpoints:** 31 total (most complex controller in the system)  
**Dependencies:** 
- Empleadores (must exist first)
- Suscripciones (plan validation)
- TSS calculations (payroll)
- Nominas (salary processing)

### Endpoint Categories (31 endpoints)

| Category | Endpoints | Complexity |
|----------|-----------|------------|
| **Basic CRUD** | 4 | Low |
| **Nómina/Payroll** | 8 | High (TSS, calculations) |
| **Remuneraciones** | 7 | Medium (batch operations) |
| **Contrataciones Temporales** | 6 | Medium (contractor hiring) |
| **Recibos/Receipts** | 4 | Medium (payment records) |
| **Calificaciones** | 2 | Low (ratings) |

---

## 🎯 Testing Strategy - 4 Phases

Following the proven pattern from EmpleadoresController and ContratistasController testing.

### Phase 1: Fix Infrastructure + Basic CRUD (Target: 8 tests)

**Duration:** ~1 hour  
**Objective:** Get tests compiling and running, validate basic operations

#### 1.1 Fix Test Infrastructure
- [ ] Update all `RegisterUserAsync()` calls to new signature (returns tuple)
- [ ] Add `CreateEmpleadorProfile()` helper (empleados need empleador)
- [ ] Verify authentication flow works correctly
- [ ] Update all test setup code

#### 1.2 Create Tests (3 tests)
- [ ] `CreateEmpleado_WithValidData_CreatesSuccessfully`
- [ ] `CreateEmpleado_WithoutAuthentication_ReturnsUnauthorized` ✅ (already passing)
- [ ] `CreateEmpleado_WithInvalidCedula_ReturnsBadRequest`

#### 1.3 Read Tests (3 tests)
- [ ] `GetEmpleadoById_WithValidId_ReturnsEmpleadoDto`
- [ ] `GetEmpleadoById_WithNonExistentId_ReturnsNotFound`
- [ ] `GetEmpleadosList_WithValidEmpleadorId_ReturnsList`

#### 1.4 Update Tests (2 tests)
- [ ] `UpdateEmpleado_WithValidData_UpdatesSuccessfully`
- [ ] `UpdateEmpleado_WithoutAuthentication_ReturnsUnauthorized` ✅ (already passing)

**Success Criteria:** 8/8 tests passing, infrastructure working

---

### Phase 2: Soft Delete + Authorization + Search (Target: 16 tests)

**Duration:** ~1 hour  
**Objective:** Add delete operations, ownership validation, and filtering

#### 2.1 Soft Delete Tests (3 tests)
- [ ] `DarDeBajaEmpleado_WithValidData_InactivatesEmployeeAndCalculatesLiquidation`
- [ ] `DarDeBajaEmpleado_WithNonExistentId_ReturnsNotFound`
- [ ] `DarDeBajaEmpleado_WithoutAuthentication_ReturnsUnauthorized`

#### 2.2 Authorization Tests (2 tests)
- [ ] `UpdateEmpleado_OtherUserEmpleado_ReturnsForbidden`
- [ ] `DeleteEmpleado_OtherUserEmpleado_ReturnsForbidden`

#### 2.3 Search & Filtering Tests (3 tests)
- [ ] `GetEmpleadosActivos_ReturnsOnlyActiveEmployees`
- [ ] `GetEmpleadosByEmpleador_WithValidEmpleadorId_ReturnsFiltered`
- [ ] `GetEmpleados_WithPagination_ReturnsCorrectPage`

**Success Criteria:** 16/16 tests passing

---

### Phase 3: Remuneraciones & Batch Operations (Target: 20 tests)

**Duration:** ~45 minutes  
**Objective:** Validate compensation management and batch operations

#### 3.1 Remuneraciones CRUD Tests (4 tests)
- [ ] `AddRemuneracion_WithValidData_AddsToEmployee`
- [ ] `AddRemuneraciones_BatchOperation_AddsMultiple`
- [ ] `UpdateRemuneraciones_BatchOperation_ReplacesAll`
- [ ] `DeleteRemuneracion_WithValidId_RemovesSuccessfully`

**Success Criteria:** 20/20 tests passing (100% of minimum target)

---

### Phase 4: Nómina & Business Logic (Target: 24-30 tests)

**Duration:** ~1-2 hours  
**Objective:** Validate complex payroll processing and business rules

#### 4.1 Nómina Processing Tests (4 tests)
- [ ] `ProcesarNomina_WithValidData_GeneratesReciboWithTssCalculations`
- [ ] `ProcesarNomina_WithoutActivePlan_ReturnsForbidden`
- [ ] `GetReciboDetalle_WithValidPagoId_ReturnsCompleteRecibo`
- [ ] `AnularRecibo_WithValidMotivo_MarksReciboAsAnulado`

#### 4.2 Contrataciones Temporales Tests (3-5 tests)
- [ ] `CreateEmpleadoTemporal_WithValidData_CreatesTemporaryEmployee`
- [ ] `ProcesarPagoContratacion_WithValidData_ProcessesPayment`
- [ ] `CancelarContratacion_WithValidReason_CancelsHiring`
- [ ] (Optional) `GetFichaEmpleadoTemporal_ReturnsCompleteProfile`
- [ ] (Optional) `GetVistaEmpleadosTemporal_ReturnsSummaryList`

#### 4.3 Validation Tests (3 tests)
- [ ] `CreateEmpleado_WithNegativeSalary_ReturnsBadRequest`
- [ ] `CreateEmpleado_WithFutureStartDate_ReturnsBadRequest`
- [ ] `UpdateEmpleado_WithInvalidPeriodoPago_ReturnsBadRequest`

**Success Criteria:** 24-30 tests passing (120-150% of minimum target)

---

## 🔧 Technical Patterns to Apply

### 1. Test Setup Pattern (from EmpleadoresController)

```csharp
private async Task<(string userId, string email, int empleadorId)> CreateEmpleadorWithLoginAsync()
{
    // Register user as Empleador
    var email = GenerateUniqueEmail("empleador");
    var (userId, registeredEmail) = await RegisterUserAsync(
        email, "Password123!", "Empleador", "Test", "Company"
    );
    
    // Login
    await LoginAsync(registeredEmail, "Password123!");
    
    // Create Empleador profile (REQUIRED for empleados)
    var createEmpleadorResponse = await Client.PostAsJsonAsync("/api/empleadores", new
    {
        UserId = userId,
        NombreComercial = "Test Company",
        RNC = GenerateRandomRNC(),
        Sector = "Tecnología"
    });
    
    var empleadorId = await createEmpleadorResponse.Content.ReadFromJsonAsync<int>();
    
    return (userId, registeredEmail, empleadorId);
}
```

### 2. JSON Property Fallback Pattern (from ContratistasController)

```csharp
// Handle both camelCase and PascalCase responses
var hasId = json.TryGetProperty("empleadoId", out var prop);
if (!hasId) hasId = json.TryGetProperty("EmpleadoId", out prop);
hasId.Should().BeTrue();
```

### 3. Business Rule Validation Pattern

```csharp
[Fact]
public async Task ProcesarNomina_WithoutActivePlan_ReturnsForbidden()
{
    // Arrange: Create empleador with expired plan
    var (userId, email, empleadorId) = await CreateEmpleadorWithExpiredPlanAsync();
    
    // Create employee
    var empleadoId = await CreateEmpleadoAsync(userId);
    
    // Act: Try to process payroll
    var response = await Client.PostAsJsonAsync($"/api/empleados/{empleadoId}/nomina", new
    {
        PeriodoInicio = DateTime.Now.AddMonths(-1),
        PeriodoFin = DateTime.Now
    });
    
    // Assert
    response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
}
```

---

## 📋 Execution Checklist

### Pre-Execution
- [ ] Review EmpleadoresController tests (working reference)
- [ ] Review ContratistasController tests (working reference)
- [ ] Understand EmpleadosController endpoints (31 total)
- [ ] Verify database has test data setup

### Phase 1 Execution
- [ ] Fix `RegisterUserAsync` calls in all 12 existing tests
- [ ] Add `CreateEmpleadorProfile` helper method
- [ ] Run tests → verify infrastructure works
- [ ] Implement missing CRUD tests if needed
- [ ] Document: **Phase 1 Checkpoint** (8/8 tests passing)

### Phase 2 Execution
- [ ] Implement soft delete tests
- [ ] Add ownership validation tests
- [ ] Add search/filtering tests
- [ ] Run all tests → verify 16/16 passing
- [ ] Document: **Phase 2 Checkpoint** (16/16 tests passing)

### Phase 3 Execution
- [ ] Implement remuneraciones tests
- [ ] Test batch operations
- [ ] Run all tests → verify 20/20 passing
- [ ] Document: **Phase 3 Checkpoint** (20/20 tests passing, 100% minimum)

### Phase 4 Execution
- [ ] Implement nómina processing tests
- [ ] Add contrataciones temporales tests
- [ ] Add validation tests
- [ ] Run all tests → verify 24-30 passing
- [ ] Document: **FINAL Checkpoint** (120-150% coverage)

---

## 🎯 Success Criteria

### Phase 1 Success
- ✅ All infrastructure issues fixed
- ✅ 8/8 basic CRUD tests passing
- ✅ No authentication errors
- ✅ Empleador profile creation working

### Phase 2 Success
- ✅ 16/16 tests passing
- ✅ Soft delete implemented correctly
- ✅ Authorization working (no cross-user edits)
- ✅ Search/filtering validated

### Phase 3 Success
- ✅ 20/20 tests passing
- ✅ Remuneraciones CRUD working
- ✅ Batch operations validated
- ✅ **Minimum target achieved (100%)**

### Phase 4 Success (Optional Enhancement)
- ✅ 24-30 tests passing
- ✅ Nómina processing validated with TSS calculations
- ✅ Contrataciones temporales working
- ✅ All business rules validated
- ✅ **Target exceeded (120-150%)**

---

## 📚 Reference Documents

### Completed Controllers (Working Examples)
- `TESTING_EMPLEADORES_PROGRESS_CHECKPOINT.md` - EmpleadoresController (24/24 tests)
- `CONTRATISTAS_CONTROLLER_100_PERCENT_COMPLETE.md` - ContratistasController (24/24 tests)

### Testing Infrastructure
- `tests/MiGenteEnLinea.IntegrationTests/Infrastructure/IntegrationTestBase.cs`
- `tests/MiGenteEnLinea.IntegrationTests/Infrastructure/TestWebApplicationFactory.cs`

### Application Layer Reference
- `src/Core/MiGenteEnLinea.Application/Features/Empleados/Commands/`
- `src/Core/MiGenteEnLinea.Application/Features/Empleados/Queries/`
- `src/Presentation/MiGenteEnLinea.API/Controllers/EmpleadosController.cs`

---

## ⏭️ Next Steps After Completion

After EmpleadosController is complete (24+ tests), continue with remaining controllers:

4. **SuscripcionesController** (Priority 4 - Business critical)
   - Estimated: 16-20 tests
   - Focus: Plan management, payment processing

5. **NominasController** (Priority 5 - Depends on Empleados)
   - Estimated: 12-16 tests
   - Focus: Payroll reporting, bulk operations

6. **CalificacionesController** (Priority 6 - Simple)
   - Estimated: 8-12 tests
   - Focus: Rating CRUD operations

7. **End-to-End Workflows** (Final validation)
   - Complete employer journey
   - Complete contractor journey
   - Complete employee lifecycle

---

## 🎉 Expected Outcome

**Before:** 2/12 tests passing (16.7%)  
**After Phase 1:** 8/8 tests passing (100% of Phase 1)  
**After Phase 2:** 16/16 tests passing (80% of target)  
**After Phase 3:** 20/20 tests passing (100% minimum target) ✅  
**After Phase 4:** 24-30 tests passing (120-150% target) 🎊  

**Estimated Total Time:** 3-5 hours  
**Complexity:** HIGH (most complex controller, many dependencies)  
**Business Impact:** CRITICAL (core HR functionality)

---

**Status:** 📋 READY TO EXECUTE  
**Next Action:** Start Phase 1 - Fix infrastructure and implement basic CRUD tests  
**Estimated Completion:** October 30, 2025 (Evening)
