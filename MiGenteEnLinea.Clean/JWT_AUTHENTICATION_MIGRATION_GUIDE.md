# 🔐 JWT AUTHENTICATION IMPLEMENTATION GUIDE
**Fecha:** 5 Noviembre 2025  
**Branch:** feature/integration-tests-rewrite  
**Status:** ✅ Phase 2 Infrastructure COMPLETADA

---

## 🎯 OBJETIVO

Implementar autenticación JWT en **todos los integration tests** (~140 tests pendientes) para hacer que pasen de `401 Unauthorized` a exitosos.

---

## ✅ INFRAESTRUCTURA COMPLETADA

### 1. JwtTokenGenerator Helper ✅
**Location:** `tests/MiGenteEnLinea.IntegrationTests/Infrastructure/JwtTokenGenerator.cs`

**Métodos disponibles:**
```csharp
// Método genérico con todos los claims
JwtTokenGenerator.GenerateToken(
    userId: "user-001",
    email: "user@test.com",
    role: "Empleador",
    planId: 1,
    nombre: "Test User",
    additionalClaims: new Dictionary<string, string>()
);

// Shortcuts específicos
JwtTokenGenerator.GenerateEmpleadorToken(userId, email, nombre, planId);
JwtTokenGenerator.GenerateContratistaToken(userId, email, nombre, planId);
JwtTokenGenerator.GenerateExpiredPlanToken(userId, email, role);
JwtTokenGenerator.GenerateExpiredToken(userId, email, role);
```

### 2. HttpClientAuthExtensions ✅
**Location:** `tests/MiGenteEnLinea.IntegrationTests/Infrastructure/HttpClientAuthExtensions.cs`

**Métodos extension:**
```csharp
// Método más simple - autenticación con token existente
_client.WithJwtAuth(token);

// Shortcuts por rol
_client.AsEmpleador(userId: "emp-001", email: "emp@test.com", planId: 1);
_client.AsContratista(userId: "cont-001", email: "cont@test.com", planId: 1);

// Sin plan
_client.AsUserWithoutPlan(userId: "user-001", role: "Empleador");

// Token expirado (para tests de autorización)
_client.WithExpiredToken(userId: "exp-001");

// Remover autenticación
_client.WithoutAuth();

// Custom claims
_client.WithCustomAuth(userId, email, role, planId, nombre, additionalClaims);
```

### 3. TestWebApplicationFactory Actualizado ✅
**Cambios aplicados:**
- ✅ Inicialización automática de `JwtTokenGenerator` con configuración de appsettings.Testing.json
- ✅ JWT SecretKey: `TEST_SECRET_KEY_FOR_INTEGRATION_TESTS_MINIMUM_32_CHARACTERS_REQUIRED`
- ✅ Issuer: `MiGenteEnLinea.IntegrationTests`
- ✅ Audience: `MiGenteEnLinea.TestClient`
- ✅ Expiration: 60 minutos

---

## 📋 PATRÓN DE MIGRACIÓN

### ANTES (TODO con 401):
```csharp
[Fact]
public async Task GetDashboardEmpleador_WithValidAuth_ReturnsOkWithMetrics()
{
    // TODO: Implementar cuando JWT esté configurado

    // Arrange
    // _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwtToken);

    // Act
    // var response = await _client.GetAsync("/api/dashboard/empleador");

    // Assert
    // response.StatusCode.Should().Be(HttpStatusCode.OK);
}
```

### DESPUÉS (✅ Con JWT funcional):
```csharp
[Fact]
public async Task GetDashboardEmpleador_WithValidAuth_ReturnsOkWithMetrics()
{
    // ✅ Phase 2: JWT Authentication implementado

    // Arrange - Autenticar como Empleador
    _client.AsEmpleador(
        userId: "test-empleador-001",
        email: "empleador@test.com",
        nombre: "Test Empleador",
        planId: 1
    );

    // Act
    var response = await _client.GetAsync("/api/dashboard/empleador");

    // Assert
    response.StatusCode.Should().Be(HttpStatusCode.OK);
    
    var dashboard = await response.Content.ReadFromJsonAsync<DashboardEmpleadorDto>();
    dashboard.Should().NotBeNull();
}
```

---

## 🔄 PROCESO DE MIGRACIÓN PASO A PASO

### Paso 1: Agregar using statement
```csharp
using MiGenteEnLinea.IntegrationTests.Infrastructure; // ← AGREGAR
```

### Paso 2: Identificar tipo de usuario requerido
- **Empleador:** Para endpoints `/api/empleadores/*`, `/api/empleados/*`, `/api/nominas/*`
- **Contratista:** Para endpoints `/api/contratistas/*`
- **Cualquiera:** Para endpoints que aceptan ambos roles

### Paso 3: Agregar autenticación ANTES del Act
```csharp
// ✅ CORRECTO: Autenticar antes de hacer el request
_client.AsEmpleador();
var response = await _client.GetAsync("/api/endpoint");

// ❌ INCORRECTO: Autenticar después del request (muy tarde)
var response = await _client.GetAsync("/api/endpoint");
_client.AsEmpleador(); // No funciona!
```

### Paso 4: Actualizar assertions
```csharp
// ANTES: Esperaba 401
response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

// DESPUÉS: Espera 200 OK
response.StatusCode.Should().Be(HttpStatusCode.OK);
```

### Paso 5: Remover TODOs y comentarios obsoletos
```csharp
// ❌ REMOVER esto:
// TODO: Implementar cuando JWT esté configurado

// ✅ AGREGAR esto:
// ✅ Phase 2: JWT Authentication implementado
```

---

## 📊 TESTS POR CONTROLLER - CHECKLIST

### ✅ DashboardControllerTests.cs (26 tests)
**Status:** ⚠️ 1 test migrado como ejemplo
**Pending:** 25 tests

**Pattern to apply:**
```csharp
// Empleador dashboard
_client.AsEmpleador();

// Contratista dashboard
_client.AsContratista();

// Health check - NO requiere auth ([AllowAnonymous])
// No cambiar
```

---

### ⏳ NominasControllerTests.cs (48 tests)
**Status:** ⏳ Pending migration
**Auth Required:** Empleador only

**Pattern:**
```csharp
_client.AsEmpleador(userId: "test-empleador-nomina");
```

**Endpoints que requieren auth:**
- POST /api/nominas/procesar-lote
- POST /api/nominas/generar-pdfs
- GET /api/nominas/resumen
- GET /api/nominas/recibo/{id}/pdf
- POST /api/nominas/enviar-emails
- GET /api/nominas/exportar-csv
- POST /api/nominas/contrataciones/procesar-pago

**Health check:** NO requiere auth (keep as-is)

---

### ⏳ PagosControllerTests.cs (46 tests)
**Status:** ⏳ Pending migration
**Auth Required:** Empleador + Contratista

**Pattern:**
```csharp
// Para procesar pagos (cualquier rol)
_client.AsEmpleador(userId: "test-user-payment");

// Para historial (acceso a propio historial)
_client.AsEmpleador(userId: "test-user-001");
var response = await _client.GetAsync("/api/pagos/historial/test-user-001");
```

**Endpoints:**
- GET /api/pagos/idempotency - Puede ser público o auth (verificar)
- POST /api/pagos/procesar - Auth required
- POST /api/pagos/sin-pago - Auth required
- GET /api/pagos/historial/{userId} - Auth required (propio userId)

---

### ⏳ UtilitariosControllerTests.cs (22 tests)
**Status:** ⏳ Pending migration
**Auth Required:** ⚠️ VERIFICAR (puede ser público)

**Pattern:**
```csharp
// Si requiere auth:
_client.AsEmpleador();

// Si es público ([AllowAnonymous]):
// No cambiar nada
```

**Endpoint:**
- GET /api/utilitarios/numero-a-letras - ⚠️ Verificar si es público

---

### ⏳ CalificacionesControllerTests.cs (23 tests)
**Status:** ⏳ Pending migration (150 errors fixed, JWT pending)
**Auth Required:** Empleador + Contratista

**Pattern:**
```csharp
// Calificar perfil - Empleador califica a Contratista
_client.AsEmpleador(userId: "test-empleador-001");

// Ver calificaciones - Contratista ve sus propias calificaciones
_client.AsContratista(userId: "test-contratista-001");
```

---

### ⏳ ContratacionesControllerTests.cs (31 tests)
**Status:** ⏳ Pending migration (150 errors fixed, JWT pending)
**Auth Required:** Empleador + Contratista

**Pattern:**
```csharp
// Crear contratación - Empleador
_client.AsEmpleador(userId: "test-empleador-001");

// Aceptar/Rechazar - Contratista
_client.AsContratista(userId: "test-contratista-001");
```

---

### ⏳ Otros Controllers (pre-existentes)
**Status:** ⏳ Pending migration

**Controllers:**
- AuthControllerTests.cs (11 tests) - Mix de público y privado
- ContratistasControllerTests.cs (24 tests) - Contratista auth
- EmpleadoresControllerTests.cs (24 tests) - Empleador auth
- EmpleadosControllerTests.cs (19 tests) - Empleador auth
- SuscripcionesControllerTests.cs (8 tests) - Auth required

---

## 🎯 CASOS ESPECIALES

### Caso 1: Test sin autenticación (expect 401)
```csharp
[Fact]
public async Task GetEndpoint_WithoutAuth_ReturnsUnauthorized()
{
    // Arrange - Asegurar que NO hay auth
    _client.WithoutAuth();

    // Act
    var response = await _client.GetAsync("/api/endpoint");

    // Assert
    response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
}
```

### Caso 2: Test con token expirado
```csharp
[Fact]
public async Task GetEndpoint_WithExpiredToken_ReturnsUnauthorized()
{
    // Arrange
    _client.WithExpiredToken();

    // Act
    var response = await _client.GetAsync("/api/endpoint");

    // Assert
    response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
}
```

### Caso 3: Test con plan expirado
```csharp
[Fact]
public async Task GetEndpoint_WithExpiredPlan_ReturnsForbidden()
{
    // Arrange
    _client.AsUserWithoutPlan(userId: "user-no-plan");

    // Act
    var response = await _client.GetAsync("/api/endpoint");

    // Assert
    response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    // O HttpStatusCode.OK si el endpoint no valida plan
}
```

### Caso 4: Test con role incorrecto
```csharp
[Fact]
public async Task GetEmpleadorEndpoint_AsContratista_ReturnsForbidden()
{
    // Arrange - Contratista intenta acceder endpoint de Empleador
    _client.AsContratista();

    // Act
    var response = await _client.GetAsync("/api/empleadores/dashboard");

    // Assert
    response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
}
```

### Caso 5: Endpoint público ([AllowAnonymous])
```csharp
[Fact]
public async Task GetPublicEndpoint_WithoutAuth_ReturnsOk()
{
    // Arrange - Sin autenticación (endpoint público)

    // Act
    var response = await _client.GetAsync("/api/health");

    // Assert
    response.StatusCode.Should().Be(HttpStatusCode.OK);
}
```

---

## 🔧 TROUBLESHOOTING

### Error: "JwtTokenGenerator not initialized"
**Solución:** Asegurar que TestWebApplicationFactory está siendo usado correctamente
```csharp
public class MyControllerTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;
    
    public MyControllerTests(TestWebApplicationFactory factory)
    {
        _client = factory.CreateClient(); // ✅ Correcto
    }
}
```

### Error: 401 después de agregar auth
**Posibles causas:**
1. ❌ Auth agregada DESPUÉS del request (moverla al Arrange)
2. ❌ SecretKey no coincide (verificar appsettings.Testing.json)
3. ❌ Issuer/Audience no coinciden con Program.cs
4. ❌ Token expirado (incrementar ExpirationMinutes en appsettings)

### Error: Claims no están en el token
**Solución:** Usar `WithCustomAuth` para agregar claims específicos
```csharp
_client.WithCustomAuth(
    userId: "user-001",
    email: "user@test.com",
    role: "Empleador",
    planId: 1,
    nombre: "Test User",
    additionalClaims: new Dictionary<string, string>
    {
        { "CustomClaim", "CustomValue" }
    }
);
```

---

## 📈 PROGRESS TRACKING

### Phase 2 Checklist
- [x] Crear JwtTokenGenerator helper
- [x] Crear HttpClientAuthExtensions helper
- [x] Actualizar TestWebApplicationFactory
- [x] Build exitoso (0 errors)
- [x] Migrar 1 test como ejemplo (DashboardController)
- [ ] Migrar DashboardControllerTests (25 pending)
- [ ] Migrar NominasControllerTests (48 tests)
- [ ] Migrar PagosControllerTests (46 tests)
- [ ] Migrar UtilitariosControllerTests (22 tests)
- [ ] Migrar CalificacionesControllerTests (23 tests)
- [ ] Migrar ContratacionesControllerTests (31 tests)
- [ ] Migrar AuthControllerTests (11 tests)
- [ ] Migrar ContratistasControllerTests (24 tests)
- [ ] Migrar EmpleadoresControllerTests (24 tests)
- [ ] Migrar EmpleadosControllerTests (19 tests)
- [ ] Migrar SuscripcionesControllerTests (8 tests)
- [ ] Ejecutar todos los tests
- [ ] Validar ~280/285 passing

### Expected Results
```
BEFORE Phase 2:
✅ Build: SUCCESS
⚠️ Tests: ~145/285 passing (~140 fail with 401)

AFTER Phase 2:
✅ Build: SUCCESS
✅ Tests: ~280/285 passing (~5 may fail due to real app bugs)
```

---

## 🚀 NEXT STEPS

### Immediate (Today)
1. ✅ Crear infraestructura JWT (COMPLETADO)
2. ⏳ Migrar 1 controller completo como ejemplo (DashboardController - 1/26 done)
3. ⏳ Ejecutar tests de DashboardController y validar

### Short Term (This Week)
4. Migrar controllers más complejos (NominasController, PagosController)
5. Migrar controllers medianos (CalificacionesController, ContratacionesController)
6. Migrar controllers pre-existentes restantes

### Medium Term (Next Week)
7. File upload tests (foto contratista, documentos)
8. Direct Command/Query unit tests
9. Test documentation and best practices guide

---

## 📚 EJEMPLOS COMPLETOS

### Ejemplo 1: Dashboard Empleador (MIGRADO)
```csharp
[Fact]
public async Task GetDashboardEmpleador_WithValidAuth_ReturnsOkWithMetrics()
{
    // ✅ Phase 2: JWT Authentication implementado

    // Arrange
    _client.AsEmpleador(
        userId: "test-empleador-001",
        email: "empleador@test.com",
        nombre: "Test Empleador",
        planId: 1
    );

    // Act
    var response = await _client.GetAsync("/api/dashboard/empleador");

    // Assert
    response.StatusCode.Should().Be(HttpStatusCode.OK);
    
    var dashboard = await response.Content.ReadFromJsonAsync<DashboardEmpleadorDto>();
    dashboard.Should().NotBeNull();
    dashboard!.TotalEmpleados.Should().BeGreaterOrEqualTo(0);
}
```

### Ejemplo 2: Procesar Nómina (PENDIENTE)
```csharp
[Fact]
public async Task ProcesarLote_WithValidData_ReturnsSuccess()
{
    // ✅ Phase 2: JWT Authentication implementado

    // Arrange
    _client.AsEmpleador(userId: "test-empleador-nomina");
    
    var command = new
    {
        empleadorId = 1,
        empleadoIds = new[] { 1, 2, 3 },
        periodo = "2024-11"
    };

    // Act
    var response = await _client.PostAsJsonAsync("/api/nominas/procesar-lote", command);

    // Assert
    response.StatusCode.Should().Be(HttpStatusCode.OK);
    
    var result = await response.Content.ReadFromJsonAsync<ProcesarLoteResultDto>();
    result.Should().NotBeNull();
    result!.RecibosCreados.Should().BeGreaterThan(0);
}
```

### Ejemplo 3: Calificar Contratista (PENDIENTE)
```csharp
[Fact]
public async Task CalificarPerfil_WithValidData_ReturnsSuccess()
{
    // ✅ Phase 2: JWT Authentication implementado

    // Arrange
    _client.AsEmpleador(userId: "test-empleador-001");
    
    var command = new CalificarPerfilCommand(
        EmpleadorUserId: "test-empleador-001",
        ContratistaIdentificacion: "00100000000",
        ContratistaNombre: "Test Contratista",
        Puntualidad: 5,
        Cumplimiento: 5,
        Conocimientos: 5,
        Recomendacion: 5
    );

    // Act
    var response = await _client.PostAsJsonAsync("/api/calificaciones", command);

    // Assert
    response.StatusCode.Should().Be(HttpStatusCode.Created);
}
```

---

## 📝 NOTAS IMPORTANTES

1. **Using statement requerido:** `using MiGenteEnLinea.IntegrationTests.Infrastructure;`
2. **Auth ANTES del request:** Siempre llamar `.AsEmpleador()` o `.AsContratista()` en el Arrange
3. **Clean auth entre tests:** xUnit crea nueva instancia de HttpClient por test
4. **Public endpoints:** No agregar auth a endpoints con `[AllowAnonymous]`
5. **UserId consistency:** Usar mismo userId en auth y en endpoint params cuando sea relevante

---

**Prepared by:** GitHub Copilot  
**Date:** 5 Noviembre 2025  
**Status:** ✅ Infrastructure Complete, Migration In Progress  
**Next:** Migrate remaining ~140 tests
