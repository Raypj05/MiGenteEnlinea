# 🧪 TESTING STRATEGY: Controller-by-Controller Deep Validation

**Created:** October 30, 2025  
**Project:** MiGente En Línea - Clean Architecture Migration  
**Target Model:** Claude Sonnet 4.5 (Autonomous Agent Mode)  
**Branch:** `feature/integration-tests-rewrite`

---

## 🎯 OBJECTIVE

Realizar **testing exhaustivo y profundo** de cada Controller, validando:
1. ✅ Todos los **Commands** ejecutan correctamente
2. ✅ Todas las **Queries** retornan datos esperados
3. ✅ Cada **Endpoint** funciona con autenticación/autorización correcta
4. ✅ **Lógica de negocio** se ejecuta como en Legacy pero con Clean Architecture
5. ✅ **Validaciones** funcionan (FluentValidation)
6. ✅ **Manejo de errores** es robusto
7. ✅ **Integración con DB real** funciona sin mocks

---

## 📋 TESTING ORDER (Controller Priority)

**✅ COMPLETADO:**
- [x] **AuthController** - 39/39 tests passing (100%)
  - 15 AuthControllerIntegrationTests
  - 7 AuthFlowTests  
  - 17 AuthenticationCommandsTests

**🎯 PENDING (Execute in this order):**
1. [ ] **EmpleadoresController** (Priority 1 - Base entity, 8 tests)
2. [ ] **ContratistasController** (Priority 2 - Base entity, 6 tests)
3. [ ] **EmpleadosController** (Priority 3 - Depends on Empleadores, 11 tests)
4. [ ] **SuscripcionesController** (Priority 4 - Business critical, 8 tests)
5. [ ] **ContratacionesController** (Priority 5 - Complex flows, estimated 10+ tests)
6. [ ] **NominasController** (Priority 6 - Depends on Empleados, estimated 8+ tests)
7. [ ] **BusinessLogicTests** (FINAL - End-to-end flows, 11 tests)

---

## 🏗️ PROJECT STRUCTURE REFERENCE

### Clean Architecture Project (TARGET - Implement here)
```
MiGenteEnLinea.Clean/
├── src/
│   ├── Core/
│   │   ├── Domain/              # Entities, Value Objects, Domain Events
│   │   └── Application/         # Commands, Queries, DTOs, Handlers
│   │       └── Features/
│   │           ├── Authentication/
│   │           ├── Empleadores/
│   │           ├── Contratistas/
│   │           ├── Empleados/
│   │           ├── Suscripciones/
│   │           ├── Contrataciones/
│   │           ├── Nominas/
│   │           └── Calificaciones/
│   ├── Infrastructure/          # EF Core, External Services
│   └── Presentation/
│       └── API/
│           └── Controllers/     # REST endpoints
└── tests/
    └── IntegrationTests/
        └── Controllers/         # Integration tests per controller
```

### Legacy Project (REFERENCE ONLY - Business Logic Source)
```
Codigo Fuente Mi Gente/
└── MiGente_Front/
    ├── Data/                    # EF6 EDMX models
    ├── Services/                # Business logic (reference for validation)
    │   ├── LoginService.cs
    │   ├── EmpleadoresService.cs
    │   ├── ContratistasService.cs
    │   └── EmpleadosService.cs
    ├── Empleador/               # Employer pages (.aspx.cs - business logic in code-behind)
    ├── Contratista/             # Contractor pages
    └── *.aspx.cs                # Code-behind with business logic
```

---

## 📚 AVAILABLE FEATURES BY MODULE

### 1️⃣ EmpleadoresController Features

**Commands (src/Core/MiGenteEnLinea.Application/Features/Empleadores/Commands/):**
```
CreateEmpleador/
UpdateEmpleador/
DeleteEmpleador/
ActivarEmpleador/
DesactivarEmpleador/
```

**Queries (src/Core/MiGenteEnLinea.Application/Features/Empleadores/Queries/):**
```
GetEmpleadorById/
GetEmpleadorByUserId/
GetEmpleadores/
SearchEmpleadores/
```

**Legacy Reference:** 
- `MiGente_Front/Empleador/*.aspx.cs` (business logic)
- `MiGente_Front/Services/EmpleadoresService.cs` (if exists)

**Key Business Rules to Validate:**
- ✅ Solo usuarios tipo 1 (Empleador) pueden crear empleador
- ✅ RNC debe ser único y válido (formato dominicano)
- ✅ Empleador debe tener plan activo para crear empleados
- ✅ Soft delete (no eliminar físicamente)

---

### 2️⃣ ContratistasController Features

**Commands:**
```
CreateContratista/
UpdateContratista/
ActivarPerfil/
DesactivarPerfil/
AddServicio/
RemoveServicio/
UpdateContratistaImagen/
```

**Queries:**
```
GetContratistaById/
GetContratistaByUserId/
SearchContratistas/
GetServiciosContratista/
GetCedulaByUserId/
```

**Legacy Reference:**
- `MiGente_Front/Contratista/*.aspx.cs`
- `MiGente_Front/Services/ContratistasService.cs`

**Key Business Rules:**
- ✅ Solo usuarios tipo 2 (Contratista) pueden crear perfil
- ✅ Cédula debe ser única y válida (11 dígitos)
- ✅ Servicios ofrecidos deben existir en catálogo
- ✅ Imagen de perfil debe ser procesada correctamente

---

### 3️⃣ EmpleadosController Features

**Commands:**
```
CreateEmpleado/
UpdateEmpleado/
DarDeBajaEmpleado/
AddRemuneracion/
AddDeduccion/
UpdateRemuneraciones/
GuardarOtrasRemuneraciones/
ConsultarPadron/ (Query externa API)
```

**Queries:**
```
GetEmpleadoById/
GetEmpleados/
GetEmpleadosByEmpleador/
GetRecibos/
GetReciboById/
GetDeduccionesTss/
```

**Legacy Reference:**
- `MiGente_Front/Empleador/colaboradores.aspx.cs`
- `MiGente_Front/Empleador/fichaEmpleado.aspx.cs`

**Key Business Rules:**
- ✅ Solo empleador puede crear empleados
- ✅ Empleador debe tener plan activo con límite de empleados
- ✅ Cédula debe ser validada con API Padrón RD
- ✅ Remuneraciones y deducciones TSS deben calcularse correctamente
- ✅ Recibos deben generarse con formato legal correcto

---

### 4️⃣ SuscripcionesController Features

**Commands:**
```
CreateSuscripcion/
UpdateSuscripcion/
CancelSuscripcion/
```

**Queries:**
```
GetSuscripcion/
GetSuscripcionByUserId/
GetPlanesEmpleadores/
GetPlanesContratistas/
GetVentasByUserId/
```

**Legacy Reference:**
- `MiGente_Front/Empleador/AdquirirPlanEmpleadores.aspx.cs`
- `MiGente_Front/Contratista/AdquirirPlanContratista.aspx.cs`

**Key Business Rules:**
- ✅ Plan debe existir y estar activo
- ✅ FechaVencimiento calculada según duración del plan
- ✅ Solo un plan activo por usuario
- ✅ Integración con Cardnet payment gateway
- ✅ Validar restricciones de plan (límite empleados, etc.)

---

### 5️⃣ ContratacionesController Features

**Commands:**
```
CreateContratacion/
AcceptContratacion/
RejectContratacion/
StartContratacion/
CompleteContratacion/
CancelContratacion/
CancelarTrabajo/
EliminarEmpleadoTemporal/
```

**Queries:**
```
GetContratacionById/
GetContrataciones/
GetContratacionesByEmpleador/
GetContratacionesByContratista/
```

**Key Business Rules:**
- ✅ Estado workflow: Pendiente → Aceptada → En Progreso → Completada
- ✅ Solo empleador puede crear contratación
- ✅ Solo contratista puede aceptar/rechazar
- ✅ Pago procesado al completar
- ✅ Empleados temporales creados para contratación

---

### 6️⃣ NominasController Features

**Commands:**
```
ProcesarNomina/
ProcesarPago/
ProcesarPagoContratacion/
AnularRecibo/
```

**Queries:**
```
GetNominas/
GetRecibosPendientes/
GetRecibosEmpleado/
```

**Key Business Rules:**
- ✅ Calcular salario bruto, deducciones TSS, salario neto
- ✅ Generar PDF recibo con formato legal dominicano
- ✅ TSS: AFP (2.87%), SFS (3.04%), INFOTEP (1%)
- ✅ Validar que empleador tenga fondos suficientes

---

## 🧪 TESTING APPROACH (Per Controller)

### Phase 1: Setup & Infrastructure (Per Controller)

```csharp
// Example: EmpleadoresControllerTests.cs
public class EmpleadoresControllerTests : IntegrationTestBase
{
    public EmpleadoresControllerTests(TestWebApplicationFactory factory) : base(factory) { }
    
    // 1. Test helper methods (if needed beyond IntegrationTestBase)
    private async Task<string> CreateTestEmpleadorAsync()
    {
        var (userId, email) = await RegisterUserAsync(
            "empleador.test@example.com", 
            "Test123!@#", 
            "Empleador", 
            "Juan", 
            "Pérez"
        );
        await LoginAsync(email, "Test123!@#");
        return userId;
    }
}
```

### Phase 2: Command Testing (One test per Command)

**Template Pattern:**
```csharp
[Fact]
public async Task CommandName_WithValidData_ReturnsSuccess()
{
    // Arrange
    var (userId, email) = await RegisterUserAsync(...);
    await LoginAsync(email, password);
    
    var command = new CommandNameCommand
    {
        // Valid data according to business rules
    };
    
    // Act
    var response = await Client.PostAsJsonAsync("/api/controllerName", command);
    
    // Assert
    response.StatusCode.Should().Be(HttpStatusCode.Created); // or OK
    var result = await response.Content.ReadFromJsonAsync<ResultDto>();
    result.Should().NotBeNull();
    result.Id.Should().BeGreaterThan(0);
    
    // Verify in database (optional but recommended)
    var entity = await DbContext.EntitySet.FindAsync(result.Id);
    entity.Should().NotBeNull();
    entity.Property.Should().Be(command.Property);
}

[Fact]
public async Task CommandName_WithInvalidData_ReturnsBadRequest()
{
    // Arrange
    var (userId, email) = await RegisterUserAsync(...);
    await LoginAsync(email, password);
    
    var command = new CommandNameCommand
    {
        // INVALID data to trigger validation
    };
    
    // Act
    var response = await Client.PostAsJsonAsync("/api/controllerName", command);
    
    // Assert
    response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    var errorResponse = await response.Content.ReadAsStringAsync();
    errorResponse.Should().Contain("validation error message");
}

[Fact]
public async Task CommandName_WithoutAuthentication_ReturnsUnauthorized()
{
    // Arrange
    Client.DefaultRequestHeaders.Authorization = null; // Remove auth
    
    var command = new CommandNameCommand { /* valid data */ };
    
    // Act
    var response = await Client.PostAsJsonAsync("/api/controllerName", command);
    
    // Assert
    response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
}
```

### Phase 3: Query Testing (One test per Query)

**Template Pattern:**
```csharp
[Fact]
public async Task GetEntityById_WithValidId_ReturnsEntity()
{
    // Arrange
    var (userId, email) = await RegisterUserAsync(...);
    await LoginAsync(email, password);
    
    // Create entity first via Command
    var createCommand = new CreateEntityCommand { /* data */ };
    var createResponse = await Client.PostAsJsonAsync("/api/controller", createCommand);
    var createdId = await createResponse.Content.ReadFromJsonAsync<int>();
    
    // Act
    var response = await Client.GetAsync($"/api/controller/{createdId}");
    
    // Assert
    response.StatusCode.Should().Be(HttpStatusCode.OK);
    var entity = await response.Content.ReadFromJsonAsync<EntityDto>();
    entity.Should().NotBeNull();
    entity.Id.Should().Be(createdId);
}

[Fact]
public async Task GetEntities_ReturnsPagedList()
{
    // Arrange
    var (userId, email) = await RegisterUserAsync(...);
    await LoginAsync(email, password);
    
    // Create multiple entities
    for (int i = 0; i < 3; i++)
    {
        var command = new CreateEntityCommand { /* data */ };
        await Client.PostAsJsonAsync("/api/controller", command);
    }
    
    // Act
    var response = await Client.GetAsync("/api/controller");
    
    // Assert
    response.StatusCode.Should().Be(HttpStatusCode.OK);
    var entities = await response.Content.ReadFromJsonAsync<List<EntityDto>>();
    entities.Should().NotBeNull();
    entities.Should().HaveCountGreaterOrEqualTo(3);
}
```

### Phase 4: Business Logic Validation

**Critical Business Rules per Controller:**

```csharp
// Example: Empleadores - RNC uniqueness
[Fact]
public async Task CreateEmpleador_WithDuplicateRNC_ReturnsBadRequest()
{
    // Arrange
    var (userId1, email1) = await RegisterUserAsync("emp1@test.com", "Test123!@#", "Empleador", "Juan", "Pérez");
    await LoginAsync(email1, "Test123!@#");
    
    var command1 = new CreateEmpleadorCommand { RNC = "12345678901", /* other data */ };
    await Client.PostAsJsonAsync("/api/empleadores", command1);
    
    // Create second user
    var (userId2, email2) = await RegisterUserAsync("emp2@test.com", "Test123!@#", "Empleador", "Pedro", "González");
    await LoginAsync(email2, "Test123!@#");
    
    var command2 = new CreateEmpleadorCommand { RNC = "12345678901", /* other data */ }; // DUPLICATE
    
    // Act
    var response = await Client.PostAsJsonAsync("/api/empleadores", command2);
    
    // Assert
    response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    var error = await response.Content.ReadAsStringAsync();
    error.Should().Contain("RNC ya está registrado");
}

// Example: Empleados - Plan limit validation
[Fact]
public async Task CreateEmpleado_ExceedingPlanLimit_ReturnsBadRequest()
{
    // Arrange - Create empleador with plan that allows only 5 employees
    var (userId, email) = await RegisterUserAsync(...);
    await LoginAsync(email, "Test123!@#");
    
    // Create 5 employees (at limit)
    for (int i = 0; i < 5; i++)
    {
        var command = new CreateEmpleadoCommand { Cedula = $"0011223344{i}", /* data */ };
        await Client.PostAsJsonAsync("/api/empleados", command);
    }
    
    // Act - Try to create 6th employee (exceeds limit)
    var exceededCommand = new CreateEmpleadoCommand { Cedula = "00112233445", /* data */ };
    var response = await Client.PostAsJsonAsync("/api/empleados", exceededCommand);
    
    // Assert
    response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    var error = await response.Content.ReadAsStringAsync();
    error.Should().Contain("límite de empleados");
}
```

### Phase 5: Authorization Testing

```csharp
[Fact]
public async Task CreateEmpleado_AsContratista_ReturnsForbidden()
{
    // Arrange - Register as CONTRATISTA (not Empleador)
    var (userId, email) = await RegisterUserAsync(
        "contratista@test.com", 
        "Test123!@#", 
        "Contratista", // Wrong role!
        "María", 
        "López"
    );
    await LoginAsync(email, "Test123!@#");
    
    var command = new CreateEmpleadoCommand { /* valid data */ };
    
    // Act
    var response = await Client.PostAsJsonAsync("/api/empleados", command);
    
    // Assert
    response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
}
```

---

## 🔍 VALIDATION CHECKLIST (Per Test)

**Before writing test:**
- [ ] Check Legacy code for business logic reference (`*.aspx.cs`, `Services/*.cs`)
- [ ] Identify Command/Query in `Application/Features/`
- [ ] Identify Controller endpoint in `API/Controllers/`
- [ ] List all business rules that must be validated
- [ ] Identify required data setup (master data, related entities)

**During test implementation:**
- [ ] Use `RegisterUserAsync()` + `LoginAsync()` for authentication
- [ ] Use real database (no mocks) - verify with `DbContext` queries
- [ ] Test happy path (valid data)
- [ ] Test validation errors (invalid data)
- [ ] Test authorization (wrong role, no auth)
- [ ] Test business rule violations
- [ ] Use descriptive test names: `Method_Scenario_ExpectedResult`

**After test execution:**
- [ ] Verify test passes consistently (run 3 times)
- [ ] Check database state after test (data created/updated correctly)
- [ ] Review logs for warnings/errors
- [ ] Document any discovered bugs in application code

---

## 🛠️ TOOLS & HELPERS AVAILABLE

### IntegrationTestBase Helpers

```csharp
protected async Task<(string UserId, string Email)> RegisterUserAsync(
    string email, 
    string password, 
    string tipo, // "Empleador" or "Contratista"
    string nombre, 
    string apellido
)
// ✅ Automatically creates unique email with GUID suffix
// ✅ Automatically activates account for immediate login
// ✅ Returns (identityUserId as GUID string, actual email used)

protected async Task LoginAsync(string email, string password)
// ✅ Sets JWT token in Client.DefaultRequestHeaders.Authorization
// ✅ Token valid for entire test execution

protected HttpClient Client { get; }
// ✅ Authenticated HttpClient with base URL configured
// ✅ JSON serialization configured

protected IApplicationDbContext DbContext { get; }
// ✅ Direct database access for verification
// ✅ Real SQL Server database (db_a9f8ff_migente)
```

### FluentAssertions Patterns

```csharp
// Status codes
response.StatusCode.Should().Be(HttpStatusCode.OK);
response.StatusCode.Should().Be(HttpStatusCode.Created);
response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
response.StatusCode.Should().Be(HttpStatusCode.NotFound);

// Collections
list.Should().NotBeNull();
list.Should().NotBeEmpty();
list.Should().HaveCount(5);
list.Should().HaveCountGreaterThan(0);
list.Should().Contain(x => x.Property == value);

// Objects
entity.Should().NotBeNull();
entity.Id.Should().BeGreaterThan(0);
entity.Property.Should().Be(expectedValue);
entity.Property.Should().NotBeNullOrEmpty();

// Strings
errorMessage.Should().Contain("expected substring");
errorMessage.Should().Match("*pattern*");
```

---

## 📊 SUCCESS CRITERIA (Per Controller)

**Minimum Requirements:**
- ✅ All Commands tested (happy path + validation errors)
- ✅ All Queries tested (with data + without data + not found)
- ✅ All Endpoints tested (authenticated + unauthorized)
- ✅ Critical business rules validated
- ✅ **80%+ tests passing** before moving to next controller

**Quality Standards:**
- ✅ Tests use real database (no mocks)
- ✅ Tests are isolated (each creates own data)
- ✅ Tests are repeatable (can run multiple times)
- ✅ Test names are descriptive
- ✅ Assertions are specific and meaningful
- ✅ No hard-coded IDs or data (except master data references)

---

## 🚨 COMMON ISSUES & SOLUTIONS

### Issue 1: "Account not confirmed" error
**Solution:** `RegisterUserAsync` now auto-activates accounts ✅

### Issue 2: "Duplicate email" error
**Solution:** `RegisterUserAsync` now auto-generates unique emails with GUID ✅

### Issue 3: Missing master data (TSS deductions, planes, etc.)
**Solution:** Create data seed in test setup OR skip test with `[Fact(Skip = "Requires master data")]`

### Issue 4: "Unauthorized" on authenticated test
**Solution:** Verify `LoginAsync` was called AFTER `RegisterUserAsync` with correct email

### Issue 5: Null reference in DTO
**Solution:** Check AutoMapper profile configuration, verify all properties mapped

### Issue 6: Business logic differs from Legacy
**Solution:** Document difference, verify with business owner, implement correct logic in Clean Architecture

---

## 📝 TASK EXECUTION TEMPLATE

**For each Controller:**

```markdown
## 🎯 Testing: [ControllerName]Controller

### Step 1: Feature Inventory
- Commands: [list from Features/ folder]
- Queries: [list from Features/ folder]
- Endpoints: [list from Controller file]

### Step 2: Legacy Business Logic Review
- Reference files: [list .aspx.cs or Service.cs files]
- Key business rules: [list extracted rules]

### Step 3: Test Implementation Plan
- [ ] Command tests (X tests)
- [ ] Query tests (Y tests)
- [ ] Business logic tests (Z tests)
- [ ] Authorization tests (W tests)

### Step 4: Execution Results
- Tests written: X
- Tests passing: Y
- Tests failing: Z
- Issues discovered: [list with issue descriptions]

### Step 5: Next Actions
- [ ] Fix application bugs discovered
- [ ] Update test assertions if needed
- [ ] Document business rule changes
- [ ] Move to next controller
```

---

## 🎯 FINAL GOAL

**Complete Coverage:**
```
✅ AuthController:          39/39 tests (100%) ✅ DONE
⏳ EmpleadoresController:    0/8 tests   (0%)  ← START HERE
⏳ ContratistasController:   0/6 tests   (0%)
⏳ EmpleadosController:      0/11 tests  (0%)
⏳ SuscripcionesController:  0/8 tests   (0%)
⏳ ContratacionesController: 0/10 tests  (0%)
⏳ NominasController:        0/8 tests   (0%)
⏳ BusinessLogicTests:       1/11 tests  (9%)  ← END HERE

TOTAL: 40/101 tests (40%) → TARGET: 101/101 (100%)
```

---

## 🤖 AGENT INSTRUCTIONS

**You are Claude Sonnet 4.5, an autonomous testing agent for a .NET 8 Clean Architecture migration project.**

**Your mission:** Test EVERY Command, Query, and Endpoint for **[ControllerName]Controller** with deep validation of business logic.

**Work autonomously:**
1. ✅ Read Legacy code to understand business rules
2. ✅ Read Clean Architecture Commands/Queries to understand implementation
3. ✅ Write comprehensive integration tests following templates above
4. ✅ Execute tests and verify results
5. ✅ Fix application bugs if discovered (NOT test bugs - tests should be simple)
6. ✅ Document results and move to next Command/Query
7. ✅ Do NOT stop until ALL Commands/Queries for this controller are tested

**Quality over speed:** Take time to understand business logic deeply. One well-tested controller is better than superficial coverage.

**Report format:**
```
### [ControllerName]Controller Testing - [Status]

**Commands Tested:** X/Y
**Queries Tested:** A/B
**Tests Passing:** X
**Tests Failing:** Y
**Application Bugs Found:** Z

**Next:** [Next controller or "All done!"]
```

---

## 📞 SUPPORT REFERENCES

- **Architecture Guide:** `INDICE_COMPLETO_DOCUMENTACION.md`
- **Testing Setup:** `INTEGRATION_TESTS_SETUP_REPORT.md`
- **Backend Status:** `BACKEND_100_COMPLETE_VERIFIED.md`
- **GAPS Analysis:** `GAPS_AUDIT_COMPLETO_FINAL.md`

---

**START WITH:** `EmpleadoresController` (8 tests expected)  
**END WITH:** `BusinessLogicTests` (11 end-to-end flow tests)

🚀 **Let's build bulletproof integration tests!**
