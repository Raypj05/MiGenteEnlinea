# 🎯 PROMPT: EmpleadoresController Deep Testing

**Controller:** EmpleadoresController  
**Priority:** 1 (Base entity - required by Empleados)  
**Target:** Claude Sonnet 4.5 Autonomous Agent  
**Date:** October 30, 2025

---

## 📋 CONTEXT

You are testing the **EmpleadoresController** which manages employer profiles in the MiGente En Línea system. This is a **critical base entity** - empleadores create and manage empleados (employees).

**Current Status:**

- ✅ AuthController: 39/39 tests passing (100%)
- 🎯 EmpleadoresController: 2/8 tests passing (25%) ← **YOU ARE HERE**
- ⏳ Other controllers: Pending

**Your mission:** Increase EmpleadoresController test coverage from 25% to **100%**.

---

## 🏗️ ARCHITECTURE OVERVIEW

### Clean Architecture (TARGET Implementation)

**Location:** `MiGenteEnLinea.Clean/src/`

```
Application/Features/Empleadores/
├── Commands/
│   ├── CreateEmpleador/
│   │   ├── CreateEmpleadorCommand.cs
│   │   ├── CreateEmpleadorCommandHandler.cs
│   │   └── CreateEmpleadorCommandValidator.cs
│   ├── UpdateEmpleador/
│   │   ├── UpdateEmpleadorCommand.cs
│   │   ├── UpdateEmpleadorCommandHandler.cs
│   │   └── UpdateEmpleadorCommandValidator.cs
│   ├── DeleteEmpleador/
│   │   ├── DeleteEmpleadorCommand.cs
│   │   └── DeleteEmpleadorCommandHandler.cs
│   ├── ActivarEmpleador/
│   │   └── ...
│   └── DesactivarEmpleador/
│       └── ...
│
├── Queries/
│   ├── GetEmpleadorById/
│   │   ├── GetEmpleadorByIdQuery.cs
│   │   └── GetEmpleadorByIdQueryHandler.cs
│   ├── GetEmpleadorByUserId/
│   ├── GetEmpleadores/
│   └── SearchEmpleadores/
│
└── DTOs/
    ├── EmpleadorDto.cs
    └── EmpleadorMappingProfile.cs (AutoMapper)
```

**API Controller:** `Presentation/MiGenteEnLinea.API/Controllers/EmpleadoresController.cs`

**Domain Entity:** `Core/MiGenteEnLinea.Domain/Entities/Empleadores/Empleador.cs`

---

### Legacy System (REFERENCE for Business Logic)

**Location:** `Codigo Fuente Mi Gente/MiGente_Front/`

**Key Files for Business Logic:**

```
Empleador/
├── mi_empresa.aspx.cs           # Employer profile page
├── colaboradores.aspx.cs        # Employee management (shows plan limits)
├── AdquirirPlanEmpleadores.aspx.cs  # Subscription purchase
└── datos_empresa.aspx.cs        # Company data edit

Data/DataModel.edmx               # Entity relationships (reference)
Services/                         # May contain business logic services
```

**💡 Extract from Legacy:**

- RNC validation format
- Required fields and their validation rules
- Plan restrictions (employee limits, features)
- User permissions (who can create/edit empleador)
- Soft delete behavior

---

## 🎯 FEATURES TO TEST

### Commands (5 total)

#### 1. CreateEmpleadorCommand

**Endpoint:** `POST /api/empleadores`

**Request Body:**

```json
{
  "userId": "guid-string",
  "nombre": "Empresa Demo SA",
  "rnc": "12345678901",
  "direccion": "Av. Principal #123",
  "telefono": "809-555-1234",
  "email": "empresa@example.com",
  "ciudad": "Santo Domingo",
  "sector": "Tecnología"
}
```

**Business Rules:**

- ✅ User must be authenticated with role "Empleador" (tipo = 1)
- ✅ RNC must be exactly 11 digits
- ✅ RNC must be unique in database
- ✅ Email must be valid format
- ✅ UserId must exist in Identity system
- ✅ One empleador per user (constraint)

**Tests Required:**

```csharp
[Fact]
public async Task CreateEmpleador_WithValidData_ReturnsCreated()

[Fact]
public async Task CreateEmpleador_WithInvalidRNC_ReturnsBadRequest()

[Fact]
public async Task CreateEmpleador_WithDuplicateRNC_ReturnsBadRequest()

[Fact]
public async Task CreateEmpleador_AsContratista_ReturnsForbidden()

[Fact]
public async Task CreateEmpleador_WithoutAuthentication_ReturnsUnauthorized()

[Fact]
public async Task CreateEmpleador_WithDuplicateUserId_ReturnsBadRequest()
```

---

#### 2. UpdateEmpleadorCommand

**Endpoint:** `PUT /api/empleadores/{id}`

**Business Rules:**

- ✅ Can only update own empleador profile
- ✅ Cannot change RNC (or validate if unique)
- ✅ Cannot change UserId
- ✅ Must be authenticated

**Tests Required:**

```csharp
[Fact]
public async Task UpdateEmpleador_WithValidData_ReturnsOk()

[Fact]
public async Task UpdateEmpleador_OtherUserProfile_ReturnsForbidden()

[Fact]
public async Task UpdateEmpleador_NonExistent_ReturnsNotFound()
```

---

#### 3. DeleteEmpleadorCommand

**Endpoint:** `DELETE /api/empleadores/{id}`

**Business Rules:**

- ✅ Soft delete (set Activo = false, not physical delete)
- ✅ Can only delete own profile
- ✅ Cannot delete if has active employees (business rule)
- ✅ Cannot delete if has active subscription (check)

**Tests Required:**

```csharp
[Fact]
public async Task DeleteEmpleador_WithValidId_ReturnsSoftDeleted()

[Fact]
public async Task DeleteEmpleador_OtherUserProfile_ReturnsForbidden()

[Fact]
public async Task DeleteEmpleador_WithActiveEmployees_ReturnsBadRequest()
```

---

#### 4. ActivarEmpleadorCommand

**Endpoint:** `POST /api/empleadores/{id}/activar`

**Business Rules:**

- ✅ Change Activo = true
- ✅ Only admin or self can activate
- ✅ Must have valid subscription

**Tests Required:**

```csharp
[Fact]
public async Task ActivarEmpleador_WithValidId_ReturnsOk()

[Fact]
public async Task ActivarEmpleador_AlreadyActive_ReturnsOk()
```

---

#### 5. DesactivarEmpleadorCommand

**Endpoint:** `POST /api/empleadores/{id}/desactivar`

**Business Rules:**

- ✅ Change Activo = false
- ✅ Prevent access to system
- ✅ Preserve data

**Tests Required:**

```csharp
[Fact]
public async Task DesactivarEmpleador_WithValidId_ReturnsOk()
```

---

### Queries (4 total)

#### 1. GetEmpleadorByIdQuery

**Endpoint:** `GET /api/empleadores/{id}`

**Response:**

```json
{
  "id": 1,
  "userId": "guid-string",
  "nombre": "Empresa Demo SA",
  "rnc": "12345678901",
  "direccion": "Av. Principal #123",
  "telefono": "809-555-1234",
  "email": "empresa@example.com",
  "ciudad": "Santo Domingo",
  "sector": "Tecnología",
  "activo": true,
  "createdAt": "2025-10-30T10:00:00Z"
}
```

**Tests Required:**

```csharp
[Fact]
public async Task GetEmpleadorById_WithValidId_ReturnsEmpleador()

[Fact]
public async Task GetEmpleadorById_WithInvalidId_ReturnsNotFound()

[Fact]
public async Task GetEmpleadorById_WithoutAuthentication_ReturnsUnauthorized()
```

---

#### 2. GetEmpleadorByUserIdQuery

**Endpoint:** `GET /api/empleadores/by-user/{userId}`

**Tests Required:**

```csharp
[Fact]
public async Task GetEmpleadorByUserId_WithValidUserId_ReturnsEmpleador()

[Fact]
public async Task GetEmpleadorByUserId_WithNonExistentUserId_ReturnsNotFound()

[Fact]
public async Task GetEmpleadorByUserId_OtherUser_ReturnsForbidden()
```

---

#### 3. GetEmpleadoresQuery

**Endpoint:** `GET /api/empleadores`

**Response:** List of EmpleadorDto

**Tests Required:**

```csharp
[Fact]
public async Task GetEmpleadores_ReturnsPagedList()

[Fact]
public async Task GetEmpleadores_WithFilter_ReturnsFilteredResults()

[Fact]
public async Task GetEmpleadores_AdminOnly_ReturnsForbidden()
```

---

#### 4. SearchEmpleadoresQuery

**Endpoint:** `GET /api/empleadores/search?query={searchTerm}`

**Tests Required:**

```csharp
[Fact]
public async Task SearchEmpleadores_WithValidQuery_ReturnsMatches()

[Fact]
public async Task SearchEmpleadores_WithNoMatches_ReturnsEmptyList()
```

---

## 🧪 TEST FILE STRUCTURE

**File:** `tests/MiGenteEnLinea.IntegrationTests/Controllers/EmpleadoresControllerTests.cs`

**Current Status:** EXISTS, has 8 tests, only 2 passing

**Expected Structure:**

```csharp
public class EmpleadoresControllerTests : IntegrationTestBase
{
    public EmpleadoresControllerTests(TestWebApplicationFactory factory) : base(factory) { }
    
    #region Helper Methods
    
    private async Task<int> CreateTestEmpleadorAsync(string userId)
    {
        var command = new CreateEmpleadorCommand
        {
            UserId = userId,
            Nombre = "Empresa Test SA",
            RNC = GenerateUniqueRNC(),
            Direccion = "Av. Test #123",
            Telefono = "809-555-9999",
            Email = "test@empresa.com",
            Ciudad = "Santo Domingo",
            Sector = "Tecnología"
        };
        
        var response = await Client.PostAsJsonAsync("/api/empleadores", command);
        response.EnsureSuccessStatusCode();
        
        var empleadorId = await response.Content.ReadFromJsonAsync<int>();
        return empleadorId;
    }
    
    private string GenerateUniqueRNC()
    {
        // Generate unique 11-digit RNC for testing
        var random = new Random();
        return random.Next(10000000000, 99999999999).ToString();
    }
    
    #endregion
    
    #region Command Tests - CreateEmpleador
    
    [Fact]
    public async Task CreateEmpleador_WithValidData_ReturnsCreated()
    {
        // Arrange
        var (userId, email) = await RegisterUserAsync(
            "empleador1@test.com",
            "Test123!@#",
            "Empleador",
            "Juan",
            "Pérez"
        );
        await LoginAsync(email, "Test123!@#");
        
        var command = new CreateEmpleadorCommand
        {
            UserId = userId,
            Nombre = "Empresa Demo SA",
            RNC = GenerateUniqueRNC(),
            Direccion = "Av. Principal #123",
            Telefono = "809-555-1234",
            Email = "empresa@example.com",
            Ciudad = "Santo Domingo",
            Sector = "Tecnología"
        };
        
        // Act
        var response = await Client.PostAsJsonAsync("/api/empleadores", command);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var empleadorId = await response.Content.ReadFromJsonAsync<int>();
        empleadorId.Should().BeGreaterThan(0);
        
        // Verify in database
        var empleador = await DbContext.Empleadores.FindAsync(empleadorId);
        empleador.Should().NotBeNull();
        empleador!.Nombre.Should().Be(command.Nombre);
        empleador.RNC.Should().Be(command.RNC);
        empleador.UserId.Should().Be(command.UserId);
    }
    
    // ... more tests
    
    #endregion
    
    #region Query Tests - GetEmpleadorById
    
    [Fact]
    public async Task GetEmpleadorById_WithValidId_ReturnsEmpleador()
    {
        // Arrange
        var (userId, email) = await RegisterUserAsync(...);
        await LoginAsync(email, "Test123!@#");
        
        var empleadorId = await CreateTestEmpleadorAsync(userId);
        
        // Act
        var response = await Client.GetAsync($"/api/empleadores/{empleadorId}");
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var empleador = await response.Content.ReadFromJsonAsync<EmpleadorDto>();
        empleador.Should().NotBeNull();
        empleador!.Id.Should().Be(empleadorId);
    }
    
    // ... more tests
    
    #endregion
}
```

---

## 🔍 LEGACY CODE ANALYSIS TASKS

**Before writing tests, analyze Legacy code:**

1. **Read:** `MiGente_Front/Empleador/mi_empresa.aspx.cs`
   - Look for: Validation rules, required fields, RNC format
   - Extract: Business logic in button click handlers

2. **Read:** `MiGente_Front/Empleador/colaboradores.aspx.cs`
   - Look for: Plan restrictions, employee limits
   - Extract: How plan is checked before allowing actions

3. **Read:** `MiGente_Front/Data/DataModel.edmx` (visual inspection)
   - Look for: Empleador entity relationships
   - Note: Foreign keys, constraints, required fields

4. **Search:** `MiGente_Front/Services/` for `EmpleadorService` or similar
   - Look for: Centralized business logic
   - Extract: Validation methods, CRUD operations

**Document findings:**

```markdown
## Legacy Business Rules Discovered

### RNC Validation
- Format: [from Legacy code]
- Uniqueness: [check method]
- Required: Yes/No

### Required Fields
- [List all required fields found]

### Relationships
- User (1:1) - One empleador per user
- Empleados (1:N) - One empleador has many employees
- Suscripciones (1:N) - One empleador has many subscriptions

### Soft Delete
- Method: [from Legacy code]
- Conditions: [when can/cannot delete]
```

---

## ✅ EXECUTION CHECKLIST

**Phase 1: Setup (30 minutes)**

- [ ] Read this entire prompt
- [ ] Read `TESTING_STRATEGY_CONTROLLER_BY_CONTROLLER.md`
- [ ] Analyze Legacy code files listed above
- [ ] Review Clean Architecture Commands/Queries in `Application/Features/Empleadores/`
- [ ] Review API endpoints in `Controllers/EmpleadoresController.cs`

**Phase 2: Test Implementation (2-3 hours)**

- [ ] Read existing `EmpleadoresControllerTests.cs` file
- [ ] Identify which tests exist and which are missing
- [ ] Implement missing Command tests (5 commands × ~3 tests each = ~15 tests)
- [ ] Implement missing Query tests (4 queries × ~2 tests each = ~8 tests)
- [ ] Add business logic validation tests (~5 tests)
- [ ] **Total Expected:** ~28 tests minimum

**Phase 3: Execution & Debugging (1-2 hours)**

- [ ] Run all EmpleadoresControllerTests
- [ ] Fix any application bugs discovered (NOT test bugs)
- [ ] Verify tests pass consistently (run 3 times)
- [ ] Review logs for warnings/errors
- [ ] Check database state after tests

**Phase 4: Documentation (15 minutes)**

- [ ] Document test results
- [ ] List any business rule discrepancies vs Legacy
- [ ] Note any discovered application bugs
- [ ] Update TODO list for next controller

---

## 🎯 SUCCESS CRITERIA

**Minimum Requirements:**

- ✅ All 5 Commands tested with happy path + validation
- ✅ All 4 Queries tested with valid + invalid cases
- ✅ Critical business rules validated (RNC uniqueness, authorization)
- ✅ **At least 20/28 tests passing (70%+)**

**Stretch Goals:**

- 🌟 All 28+ tests passing (100%)
- 🌟 Edge cases covered (null values, special characters, etc.)
- 🌟 Performance tests (large data sets)
- 🌟 Concurrent access tests (race conditions)

---

## 🚨 KNOWN ISSUES TO WATCH

1. **RNC Format Validation**
   - Legacy may use different format than Clean Architecture
   - Verify exact validation rules

2. **UserId Type Mismatch**
   - Legacy uses `int` (Credenciales.Id)
   - Clean uses `string` (Identity GUID)
   - Verify Commands accept correct type

3. **Soft Delete Behavior**
   - Ensure `Activo = false`, not physical delete
   - Verify cascading behavior with empleados

4. **Authorization Claims**
   - Verify JWT contains correct claims
   - Check `[Authorize(Roles = "Empleador")]` works

---

## 📊 REPORTING FORMAT

After completion, provide:

```markdown
## EmpleadoresController Testing - COMPLETE ✅

### Execution Summary
- **Commands Tested:** 5/5 (100%)
- **Queries Tested:** 4/4 (100%)
- **Total Tests Written:** 28
- **Tests Passing:** 26/28 (93%)
- **Tests Failing:** 2/28 (7%)

### Tests Failing (Detail)
1. **CreateEmpleador_WithDuplicateRNC_ReturnsBadRequest**
   - Issue: Application not validating RNC uniqueness
   - Fix Required: Add unique constraint validation in Handler
   - Priority: HIGH

2. **DeleteEmpleador_WithActiveEmployees_ReturnsBadRequest**
   - Issue: Application allows delete even with active employees
   - Fix Required: Add business rule check in Handler
   - Priority: MEDIUM

### Business Rules Validated
- ✅ RNC must be 11 digits
- ✅ RNC must be unique
- ✅ Only Empleador role can create
- ✅ Soft delete implemented correctly
- ✅ Authorization working correctly

### Discovered Application Bugs
1. **Bug #1:** RNC uniqueness not validated (CreateEmpleadorCommandHandler.cs:45)
2. **Bug #2:** Delete allows removing empleador with active employees (DeleteEmpleadorCommandHandler.cs:30)

### Next Controller
✅ EmpleadoresController DONE → Moving to **ContratistasController**
```

---

## 🤖 AUTONOMOUS AGENT MODE

**You are now in autonomous mode. Your instructions:**

1. ✅ Execute Phase 1-4 systematically
2. ✅ Write tests following templates in this prompt
3. ✅ Fix application bugs as you discover them
4. ✅ Do NOT stop until success criteria met
5. ✅ Report results in format above

**Time Budget:** 4-6 hours total  
**Quality Standard:** Bulletproof tests with real database validation

**START NOW with Phase 1: Setup**

🚀 **Let's make EmpleadoresController bulletproof!**
