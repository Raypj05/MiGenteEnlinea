# EmpleadosController Phase 1 - Test Fix Analysis

**Date:** October 30, 2025  
**Status:** Phase 1.1 Complete (Authentication Fixed) ✅ → Phase 1.2 In Progress (API Contract Mismatches)

---

## ✅ PHASE 1.1 COMPLETE: Authentication Fixed

**Achievement:** All 12 tests now authenticate correctly using new `RegisterUserAsync` tuple signature.

**Changes Applied:**
- Fixed 10 occurrences of old authentication pattern
- Updated from: `var userId = await RegisterUserAsync(...)` 
- Updated to: `var (userId, registeredEmail) = await RegisterUserAsync(...)`
- Fixed all `LoginAsync()` calls to use `registeredEmail` instead of input `email`

**Result:** ✅ No more 401 Unauthorized errors

---

## 🔄 PHASE 1.2 IN PROGRESS: API Contract Mismatches

### Test Execution Results (After Auth Fix)

**Status:** 2/12 Passing (16.7%)

**Passing Tests:**
1. ✅ `CreateEmpleado_WithoutAuthentication_ReturnsUnauthorized` 
2. ✅ `UpdateEmpleado_WithoutAuthentication_ReturnsUnauthorized`

**Failing Tests (10):**

| Test | Expected | Actual | Root Cause |
|------|----------|--------|------------|
| CreateEmpleado_WithValidData | 200 OK | 201 Created | Test expects wrong status code |
| CreateEmpleado_WithInvalidCedula | 400 BadRequest | 201 Created | No cedula validation in API |
| CreateEmpleado_WithNegativeSalary | 400 BadRequest | 500 InternalServerError | Validation not working correctly |
| GetEmpleadoById_WithValidId | Deserialize int | JSON object | API returns `{ empleadoId }`, test expects int |
| GetEmpleadoById_WithNonExistentId | 404 NotFound | 204 NoContent | API behavior different than expected |
| GetEmpleadosList | 200 OK (at `/by-user/{userId}`) | 404 NotFound | Wrong endpoint - should be `/api/empleados` |
| GetEmpleadosActivos | 200 OK (at `/by-user/{userId}`) | 404 NotFound | Wrong endpoint - should be `/api/empleados?soloActivos=true` |
| UpdateEmpleado_WithValidData | Deserialize int | JSON object | Same as CreateEmpleado |
| DarDeBajaEmpleado_WithValidData | Deserialize int | JSON object | POST to `/dar-baja`, should be PUT to `/dar-de-baja` |
| DarDeBajaEmpleado_WithoutAuth | 401 Unauthorized (at `/dar-baja`) | 404 NotFound | Wrong endpoint |

---

## 🎯 Identified Issues & Fixes Needed

### Issue 1: CreateEmpleado Response Contract Mismatch

**Controller Implementation (Lines 77-97):**
```csharp
[HttpPost]
[ProducesResponseType(typeof(int), StatusCodes.Status201Created)]
public async Task<ActionResult<int>> CreateEmpleado([FromBody] CreateEmpleadoCommand command)
{
    var empleadoId = await _mediator.Send(command);
    
    return CreatedAtAction(
        nameof(GetEmpleadoById),
        new { id = empleadoId },
        new { empleadoId });  // ❌ Returns object, not int
}
```

**Problem:** 
- API documentation says it returns `int`
- Actually returns: `{ "empleadoId": 123 }` (object)
- HTTP status: `201 Created` (correct REST standard)

**Fix Options:**
1. **Option A (Recommended):** Update tests to expect `201 Created` and deserialize object
2. **Option B:** Change API to return just int (breaks REST convention)

**Recommendation:** Fix tests (Option A)

---

### Issue 2: GetEmpleados Endpoint Wrong

**Controller Implementation (Line 650):**
```csharp
[HttpGet]  // No route parameter
public async Task<ActionResult<PaginatedList<EmpleadoListDto>>> GetEmpleados(
    [FromQuery] bool? soloActivos = true,
    [FromQuery] string? searchTerm = null,
    [FromQuery] int pageIndex = 1,
    [FromQuery] int pageSize = 20)
{
    var query = new GetEmpleadosByEmpleadorQuery
    {
        UserId = GetUserId(),  // Uses authenticated user
        // ...
    };
}
```

**Test Currently Using:**
```csharp
// ❌ WRONG
var response = await Client.GetAsync($"/api/empleados/by-user/{userId}");
```

**Correct Usage:**
```csharp
// ✅ CORRECT
var response = await Client.GetAsync("/api/empleados");  // Uses auth token for userId
var response = await Client.GetAsync("/api/empleados?soloActivos=true");
var response = await Client.GetAsync("/api/empleados?searchTerm=Carlos&pageIndex=1&pageSize=20");
```

---

### Issue 3: DarDeBaja Endpoint Wrong

**Controller Implementation (Line 195):**
```csharp
[HttpPut("{empleadoId}/dar-de-baja")]  // ✅ Note: PUT not POST, route has hyphens
public async Task<ActionResult<bool>> DarDeBajaEmpleado(
    int empleadoId, 
    [FromBody] DarDeBajaRequest request)
{
    var command = new DarDeBajaEmpleadoCommand(
        EmpleadoId: empleadoId,
        UserId: GetUserId(),
        FechaBaja: request.FechaBaja,
        Prestaciones: request.Prestaciones,
        Motivo: request.Motivo
    );
    
    var result = await _mediator.Send(command);
    return Ok(result);
}

public record DarDeBajaRequest
{
    public DateTime FechaBaja { get; init; }
    public decimal Prestaciones { get; init; }
    public string Motivo { get; init; } = string.Empty;
}
```

**Test Currently Using:**
```csharp
// ❌ WRONG
var bajaCommand = new DarDeBajaEmpleadoCommand(
    EmpleadoId: empleadoId,
    UserId: userId,
    FechaBaja: DateTime.Now,
    Prestaciones: 15000m,
    Motivo: "Renuncia voluntaria"
);
var response = await Client.PostAsJsonAsync($"/api/empleados/{empleadoId}/dar-baja", bajaCommand);
```

**Correct Usage:**
```csharp
// ✅ CORRECT
var bajaRequest = new
{
    FechaBaja = DateTime.Now,
    Prestaciones = 15000m,
    Motivo = "Renuncia voluntaria"
};
var response = await Client.PutAsJsonAsync($"/api/empleados/{empleadoId}/dar-de-baja", bajaRequest);
```

---

### Issue 4: GetEmpleadoById Non-Existent Returns NoContent

**Expected:** 404 NotFound when empleado doesn't exist  
**Actual:** 204 NoContent

**Needs Investigation:** Check GetEmpleadoById handler implementation

---

### Issue 5: Validation Not Working

**Test:** `CreateEmpleado_WithInvalidCedula` expects 400 BadRequest  
**Actual:** 201 Created (no validation triggered)

**Test:** `CreateEmpleado_WithNegativeSalary` expects 400 BadRequest  
**Actual:** 500 InternalServerError (validation throwing exception)

**Root Cause:** FluentValidation not configured or CreateEmpleadoCommandValidator missing rules

**Needs:** Review CreateEmpleadoCommandValidator

---

## 📋 Fixes Required (Priority Order)

### Fix 1: Update Test Expectations - CreateEmpleado (HIGH PRIORITY)

**Files to Fix:**
- `EmpleadosControllerTests.cs` lines 54, 109, 222, 291

**Changes:**
```csharp
// OLD:
response.StatusCode.Should().Be(HttpStatusCode.OK);
var empleadoId = await response.Content.ReadFromJsonAsync<int>();

// NEW:
response.StatusCode.Should().Be(HttpStatusCode.Created);
var result = await response.Content.ReadFromJsonAsync<JsonElement>();
var empleadoId = result.GetProperty("empleadoId").GetInt32();
```

---

### Fix 2: Update GetEmpleados Endpoint (HIGH PRIORITY)

**Files to Fix:**
- `EmpleadosControllerTests.cs` lines 172, 191

**Changes:**
```csharp
// OLD:
var response = await Client.GetAsync($"/api/empleados/by-user/{userId}");

// NEW:
var response = await Client.GetAsync("/api/empleados");
var response = await Client.GetAsync("/api/empleados?soloActivos=true");
```

---

### Fix 3: Update DarDeBaja Endpoint (HIGH PRIORITY)

**Files to Fix:**
- `EmpleadosControllerTests.cs` lines 291, 336

**Changes:**
```csharp
// OLD:
var bajaCommand = new DarDeBajaEmpleadoCommand(...);
var response = await Client.PostAsJsonAsync($"/api/empleados/{empleadoId}/dar-baja", bajaCommand);

// NEW:
var bajaRequest = new {
    FechaBaja = DateTime.Now,
    Prestaciones = 15000m,
    Motivo = "Renuncia voluntaria"
};
var response = await Client.PutAsJsonAsync($"/api/empleados/{empleadoId}/dar-de-baja", bajaRequest);
```

---

### Fix 4: Add FluentValidation Rules (MEDIUM PRIORITY)

**File:** `CreateEmpleadoCommandValidator.cs`

**Rules Needed:**
```csharp
RuleFor(x => x.Identificacion)
    .NotEmpty()
    .Length(11).WithMessage("La cédula debe tener 11 dígitos");

RuleFor(x => x.Salario)
    .GreaterThan(0).WithMessage("El salario debe ser mayor a cero");
```

---

### Fix 5: Investigate GetEmpleadoById NotFound Behavior (LOW PRIORITY)

**Needs:** Check if GetEmpleadoByIdQuery handler returns null correctly

---

## 🎯 Expected Outcome After Fixes

**After Fix 1-3 Applied:**
- ✅ 8-10/12 tests passing (66-83%)
- ❌ 2-4 tests still failing (validation tests)

**After Fix 4 Applied:**
- ✅ 10-12/12 tests passing (83-100%)

**After Fix 5 Applied:**
- ✅ 12/12 tests passing (100%) 🎊

---

## 📝 Next Steps

1. ✅ Mark TODO task 1 (Phase 1.1) as COMPLETE
2. 🔄 Apply Fix 1 (CreateEmpleado response handling)
3. 🔄 Apply Fix 2 (GetEmpleados endpoint)
4. 🔄 Apply Fix 3 (DarDeBaja endpoint and method)
5. ⏳ Run tests → Expect 8-10/12 passing
6. ⏳ Apply Fix 4 (Validation rules)
7. ⏳ Run tests → Expect 10-12/12 passing
8. ⏳ Apply Fix 5 if needed
9. ✅ Mark TODO task 2 (Phase 1.2) as COMPLETE
10. ➡️ Move to Phase 2

---

## 📊 Progress Tracking

**Session Start:** 2/12 passing (16.7%) - Authentication broken  
**After Phase 1.1:** 2/12 passing (16.7%) - Authentication fixed, API contracts mismatched  
**After Phase 1.2 (Target):** 12/12 passing (100%) - All basic tests working  
**Phase 2 Target:** 16/16 tests (add 4 new tests)  
**Phase 3 Target:** 20/20 tests (add 4 new tests)  
**Phase 4 Target:** 24-30 tests (add 4-10 new tests)

**Overall Goal:** 24-30 tests (120-150% coverage) for most complex controller

---

**Generated:** October 30, 2025  
**Next Update:** After applying Fixes 1-3
