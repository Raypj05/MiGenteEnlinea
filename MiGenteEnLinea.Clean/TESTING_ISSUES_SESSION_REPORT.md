# Testing Issues - Session Report (Nov 9, 2025)

**Session Focus**: Execute EmpleadosControllerTests suite and fix infrastructure bugs

**Initial Status**: 17/19 tests passing (89.5%)
**Current Status**: 8/19 tests passing (42%) - **REGRESSED** due to fundamental test design flaw discovered

---

## 🔴 CRITICAL ISSUE DISCOVERED: Hardcoded UserIds Pattern

### Problem Description

**Root Cause**: Most tests use **hardcoded userIds** (`"test-empleador-001"`, `"test-empleador-003"`, etc.) without creating those users first via API.

**Expected Pattern (API-First)**:
```csharp
// ✅ CORRECT: Create user via API first
var user = await CreateEmpleadorAsync(nombre: "TestUser", apellido: "Apellido");
var client = Client.AsEmpleador(userId: user.UserId);

var command = new CreateEmpleadoCommand
{
    UserId = user.UserId, // Dynamic from API response
    // ...
};
```

**Actual Pattern (Hardcoded)**:
```csharp
// ❌ WRONG: Hardcoded userId that doesn't exist
var client = Client.AsEmpleador(); // No userId
var command = new CreateEmpleadoCommand
{
    UserId = "test-empleador-001", // User doesn't exist in database
    // ...
};
```

### Impact

**Tests Affected**: 11 out of 19 tests (58%)

**Error Message**:
```
Entidad "Credencial" (test-empleador-001) no fue encontrada
```

**Tests Failing**:
1. `CreateEmpleado_WithValidAuth_CreatesEmpleadoAndReturnsId`
2. `GetEmpleadoById_WithValidAuth_ReturnsEmpleadoDetalle`
3. `UpdateEmpleado_WithValidAuth_UpdatesSuccessfully`
4. `DarDeBajaEmpleado_WithValidAuth_InactivatesEmpleado`
5. `DarDeBajaEmpleado_WithFutureFechaBaja_ReturnsBadRequest`
6. `DarDeBajaEmpleado_VerifiesSoftDelete_SetsActivoFalseAndPopulatesDates`
7. `GetEmpleadosList_WithValidAuth_ReturnsListOfEmpleados`
8. `GetEmpleados_WithSearchTerm_ReturnsFilteredResults`
9. `GetEmpleados_WithPagination_ReturnsCorrectPage`
10. `UpdateEmpleado_FromDifferentUser_ReturnsForbidden`
11. `DarDeBajaEmpleado_FromDifferentUser_ReturnsForbidden`

**Hardcoded UserIds Found**: 11+ occurrences
- `test-empleador-001` (4 uses)
- `test-empleador-003` (1 use)
- `test-empleador-005` (2 uses)
- `test-empleador-006` (2 uses)
- `test-empleador-007` (1 use)
- `test-empleador-009` (1 use)
- `test-empleador-010` (1 use)
- `test-empleador-011` (1 use)

---

## 🔧 SOLUTIONS ATTEMPTED

### Solution 1: Seed Test Users (Current Approach)

**Approach**: Use `TestDataSeeder.SeedUsuariosAsync()` to create users with predictable IDs (`test-empleador-001` to `test-empleador-011`).

**Implementation**:
- `TestDataSeeder.cs` already seeds 37 empleadores + 38 contratistas
- Seeds ranges: 001-011, 101-119, 301-307
- Called from `TestWebApplicationFactory.ConfigureWebHost()`

**Problem Discovered**:
```
⏭️ Skipping seeding: 0 empleadores and 2 contratistas already exist in database
```

The seeder has **idempotency check** that skips seeding if **ANY** empleador or contratista exists. This causes:
1. DatabaseCleanupHelper runs (deletes test data with `userID LIKE '%test%'`)
2. If non-test data exists, seeder skips
3. Tests fail because hardcoded userIds don't exist

**Fix Applied**:
1. Added `Suscripciones` cleanup to DatabaseCleanupHelper (was missing)
2. Cleanup now deletes: Suscripciones → Empleados → Contratistas → Ofertantes → Perfiles → Credenciales

**Code Changes**:
```csharp
// DatabaseCleanupHelper.cs - Added Suscripciones cleanup
await context.Database.ExecuteSqlRawAsync(@"
    IF OBJECT_ID('dbo.Suscripciones', 'U') IS NOT NULL
        DELETE FROM Suscripciones WHERE userID LIKE '%test%'
");
```

### Solution 2: Refactor to API-First Pattern (Future Work)

**Approach**: Refactor all 11 failing tests to use `CreateEmpleadorAsync()` helper.

**Pros**:
- Follows API-First testing pattern (no database seeding needed)
- Each test creates its own isolated data
- More robust (no shared state between tests)

**Cons**:
- Large refactoring effort (11+ tests × 60-80 lines each)
- Would take 2-3 hours to complete

**Decision**: Postponed until Solution 1 is validated

---

## 📋 INFRASTRUCTURE FIXES COMPLETED

### Fix 1: DatabaseCleanupHelper - Table Existence Checks

**Problem**: Attempted to DELETE from non-existent tables (`Empleados_Dependientes`, `Empleados_Remuneraciones`, `Empleados_Deducciones`)

**Error**:
```
SqlException: Invalid object name 'Empleados_Dependientes'
```

**Fix**: Added `IF OBJECT_ID('dbo.TableName', 'U') IS NOT NULL` checks before every DELETE

**Code**:
```csharp
await context.Database.ExecuteSqlRawAsync(@"
    IF OBJECT_ID('dbo.Empleados', 'U') IS NOT NULL
        DELETE FROM Empleados WHERE userID LIKE '%test%'
");
```

**Impact**: Fixed 19/19 test initialization errors → Tests can now start

### Fix 2: Authorization Tests - CreateEmpleadorAsync Signature

**Problem**: Tests called `CreateEmpleadorAsync("test-empleador-userA", "testA@example.com")` but method expects named parameters.

**Method Signature**:
```csharp
CreateEmpleadorAsync(
    string? nombre = null,      // ← First param is nombre, not userId
    string? apellido = null,    // ← Second param is apellido, not email
    string? nombreEmpresa = null,
    string? rnc = null)
```

**Fix**: Changed to named parameters
```csharp
// Before
var userA = await CreateEmpleadorAsync("test-empleador-userA", "testA@example.com");

// After
var userA = await CreateEmpleadorAsync(nombre: "TestUserA", apellido: "ApellidoA");
```

**Tests Fixed**: 2 authorization tests now use correct helper signature

### Fix 3: DatabaseCleanupHelper - Missing Suscripciones Cleanup

**Problem**: Suscripciones table not being cleaned, causing FK violations and seeding to skip

**Fix**: Added Suscripciones cleanup before main entities
```csharp
await context.Database.ExecuteSqlRawAsync(@"
    IF OBJECT_ID('dbo.Suscripciones', 'U') IS NOT NULL
        DELETE FROM Suscripciones WHERE userID LIKE '%test%'
");
```

**Impact**: Should allow TestDataSeeder to re-seed users on every test run

---

## 🎯 NEXT STEPS

### Immediate (Next Test Run)

1. **Verify Seeding Works**:
   - Run tests again
   - Check console output for "⏭️ Skipping seeding" message
   - Should see: "✅ Seeded 37 empleadores and 38 contratistas" instead

2. **Expected Outcome**:
   - Tests should find `test-empleador-001` to `test-empleador-011` in database
   - 11 failing tests should pass (or at least get past "Credencial not found" error)
   - Target: 19/19 tests passing (100%)

### Short-term (This Week)

3. **If Seeding Still Fails**:
   - Modify `TestDataSeeder.SeedUsuariosAsync()` to force seed even if data exists
   - Change idempotency check from `if (existingEmpleadores.Any() || existingContratistas.Any())` 
   - To: `if (existingEmpleadores.Any(e => e.UserId.StartsWith("test-empleador")))`

4. **If Seeding Works But Tests Still Fail**:
   - Debug individual test failures
   - Check if commands use correct userId format
   - Verify HttpClient.AsEmpleador() extension works with seeded users

### Medium-term (Next Sprint)

5. **Refactor to Pure API-First**:
   - Eliminate dependency on TestDataSeeder for test users
   - Each test creates its own user via `CreateEmpleadorAsync()`
   - Benefits:
     - True test isolation (no shared state)
     - Follows API-First pattern 100%
     - More maintainable long-term

6. **Refactor Other Test Suites**:
   - ContratistasControllerTests (similar issues likely)
   - EmpleadoresControllerTests
   - AuthControllerTests

---

## 📊 TEST RESULTS TIMELINE

| Attempt | Passing | Failing | Main Issue |
|---------|---------|---------|------------|
| **Initial** | 17/19 | 2/19 | Authorization tests with wrong CreateEmpleadorAsync params |
| **After Fix 2** | 8/19 | 11/19 | Hardcoded userIds don't exist in database |
| **After Fix 3** | ⏳ TBD | ⏳ TBD | Waiting for next test run |

**Regression Analysis**: Test pass rate dropped from 89.5% to 42% because we fixed the authorization test code but exposed a deeper issue: **most tests rely on seeded data that wasn't being created**.

---

## 🔍 TECHNICAL INSIGHTS

### TestDataSeeder Idempotency Pattern

**Current Implementation**:
```csharp
var existingEmpleadores = await context.Empleadores.AsNoTracking().ToListAsync();
var existingContratistas = await context.Contratistas.AsNoTracking().ToListAsync();

if (existingEmpleadores.Any() || existingContratistas.Any())
{
    Console.WriteLine($"⏭️ Skipping seeding: {existingEmpleadores.Count} empleadores and {existingContratistas.Count} contratistas already exist in database");
    return (existingEmpleadores, existingContratistas);
}
```

**Problem**: Uses **OR** logic - if ANY data exists, skip seeding
**Impact**: If DatabaseCleanupHelper misses ANY entity, seeding is skipped

**Proposed Fix**:
```csharp
// Only skip if TEST users specifically exist
var testEmpleadores = await context.Empleadores
    .Where(e => e.UserId.StartsWith("test-empleador"))
    .AsNoTracking()
    .ToListAsync();

if (testEmpleadores.Count >= 37) // Expected count
{
    Console.WriteLine($"✅ Test data already seeded: {testEmpleadores.Count} test empleadores found");
    return (testEmpleadores, ...);
}
```

### DatabaseCleanupHelper Execution Order

**Critical FK-aware Order**:
```
1. Disable ALL constraints (sp_MSforeachtable)
2. DELETE Empleados_Notas (child of Empleados)
3. DELETE Empleador_Recibos_Detalle (child of Empleador_Recibos_Header)
4. DELETE Empleador_Recibos_Header (child of Empleador)
5. DELETE Contratistas_Servicios (child of Contratistas)
6. DELETE Suscripciones (child of Credenciales) ← ADDED
7. DELETE Empleados_Temporales, Empleados, Contratistas, Ofertantes, Perfiles
8. DELETE Credenciales (parent table)
9. Re-enable ALL constraints
```

**Key Insight**: Suscripciones was missing from cleanup, causing:
- FK violations when trying to delete Credenciales
- Partial cleanup leaving orphaned records
- Seeder idempotency check failing incorrectly

---

## 📝 FILES MODIFIED

### 1. DatabaseCleanupHelper.cs
- **Lines Changed**: ~45-75 (cleanup logic section)
- **Changes**:
  - Added Suscripciones cleanup before main entities
  - Renumbered sections (2.4 → 2.5, 2.5 → 2.6)
- **Impact**: Complete cleanup of all test data including subscriptions

### 2. EmpleadosControllerTests.cs
- **Lines Changed**: 589, 612, 641, 668 (4 replacements)
- **Changes**:
  - Fixed 4 CreateEmpleadorAsync calls to use named parameters
  - User A, B, C, D creation in authorization tests
  - Fixed HTTP client usage (clientB instead of Client)
- **Impact**: 2 authorization tests now compile and call helper correctly

### 3. TestDataSeeder.cs
- **Status**: No changes (yet)
- **Pending**: May need idempotency logic adjustment if seeding still fails

---

## 🚀 COMMANDS TO RUN

### Re-run Tests
```powershell
Set-Location "c:\Users\rpena\OneDrive - Dextra\Desktop\MiGenteEnlinea\MiGenteEnLinea.Clean"
dotnet test tests/MiGenteEnLinea.IntegrationTests/MiGenteEnLinea.IntegrationTests.csproj `
    --filter "FullyQualifiedName~EmpleadosControllerTests" `
    --logger "console;verbosity=normal"
```

### Check Seeding Output
Look for these console messages:
- ✅ `✅ Seeded 37 empleadores and 38 contratistas` (GOOD - seeding worked)
- ❌ `⏭️ Skipping seeding: X empleadores and Y contratistas already exist` (BAD - seeding skipped)

### Verify Database State (Manual)
```sql
-- Check test users exist
SELECT userId, email FROM Credenciales WHERE userId LIKE 'test-empleador-%'

-- Count test empleadores
SELECT COUNT(*) FROM Empleadores WHERE userId LIKE 'test-empleador-%'

-- Check suscripciones cleanup worked
SELECT COUNT(*) FROM Suscripciones WHERE userID LIKE '%test%'
```

---

## 🎓 LESSONS LEARNED

1. **API-First > Seeding**: Creating users via API in each test is more robust than relying on seeded data
2. **Idempotency Logic**: Should check for specific test data, not just "any data exists"
3. **FK Awareness**: Database cleanup must respect foreign key relationships - Suscripciones missed initially
4. **Test Isolation**: Shared test data (seeded users) creates hidden dependencies between tests
5. **Defensive SQL**: `IF OBJECT_ID()` checks prevent errors from schema evolution

---

**Next Action**: Re-run tests and verify seeding works correctly with Suscripciones cleanup fix
