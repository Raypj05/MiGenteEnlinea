# 🔴 INTEGRATION TESTS - BLOQUEADO POR EMAIL TIMEOUTS#  INTEGRATION TESTS - EJECUCIÓN PARCIAL (BLOQUEADO POR EMAIL TIMEOUTS)



**Fecha:** 2025-01-27 15:36:00  **Fecha:** 2025-10-26 15:39:12

**Branch:** `feature/integration-tests-rewrite`  **Branch:** feature/integration-tests-rewrite

**Commit:** `3cb9271` (BLOQUE 6 completado)**Commit:** 3cb9271 (BLOQUE 6 completado)



------



## 📊 RESUMEN EJECUTIVO##  RESUMEN EJECUTIVO



**ESTADO:** ⚠️ **BLOQUEADO - EmailService causando timeouts masivos****ESTADO GENERAL:**  **BLOQUEADO - Email Service causando timeouts masivos**



- ✅ **58/58 tests escritos** (100%)-  **58/58 tests escritos** (100%)

- ✅ **6/6 archivos completados** (100%)-  **6/6 archivos completados** (100%)

- ✅ **1,707 líneas de código** (compilación exitosa)-  **1,707 líneas de código** (compilación exitosa)

- ⏳ **Ejecución interrumpida** - timeouts SMTP (90s por test)-  **Ejecución interrumpida** - timeouts SMTP (3 reintentos  30s = 90s por test)

- ❌ **2 test errors identificados**-  **2 test errors identificados** (antes de cancelación por timeout)

- ⏱️ **Tiempo total:** ~342s (~5 min 42s) antes de cancelación-  **Tiempo total ejecutado:** ~342 segundos (~5 min 42s)

- 📈 **Tests completados:** ~4-5 tests (mayoría con timeouts)-  **Tests completados antes de interrupción:** ~4-5 tests (mayoría exitosos)



------



## 🔴 ISSUE CRÍTICO: EmailService Timeouts##  ISSUE CRÍTICO: EmailService Timeouts



### Problema### Problema

`RegisterCommand` intenta enviar email → `EmailService.SendActivationEmailAsync()` → SMTP timeout (30s × 3 = 90s)RegisterCommand intenta enviar email de activación en cada registro  EmailService.SendActivationEmailAsync()  SMTP connection timeout (30s  3 reintentos = 90s por test)



### Root Cause### Síntomas

- Tests usan servicios reales (no mockeados)```

- SMTP real no responde en testing[15:31:26 INF] Intento 1 de 3 para enviar email a: test_*@test.com

- **58 tests** → potencialmente **92 minutos** solo en timeouts[15:31:56 WRN] Fallo al enviar email en intento 1/3

System.TimeoutException: Operation timed out after 30000 milliseconds

### Solución (CRÍTICA)System.Threading.Tasks.TaskCanceledException: A task was canceled

Mock `IEmailService` en `TestWebApplicationFactory`:```



```csharp### Root Cause

// MockEmailService.cs- Tests usan `TestWebApplicationFactory` con servicios reales (no mockeados)

public class MockEmailService : IEmailService- `EmailService` configurado con SMTP real que no responde en ambiente de testing

{- Cada `RegisterUserAsync()` intenta enviar email  90s delay por test

    public Task<bool> SendActivationEmailAsync(string email, string name, string url) 

        => Task.FromResult(true);### Impacto

}- **58 tests con registration**  potencialmente **1 hora 27 min** solo en timeouts SMTP

- Tests ejecutándose pero extremadamente lentos

// TestWebApplicationFactory.cs- Suite inviable para CI/CD

services.Remove(services.Single(d => d.ServiceType == typeof(IEmailService)));

services.AddSingleton<IEmailService, MockEmailService>();### Solución Requerida (CRÍTICA)

```**Opción 1 (Recomendada):** Mock EmailService en TestWebApplicationFactory

```csharp

---// TestWebApplicationFactory.cs

services.AddSingleton<IEmailService, MockEmailService>();

## ❌ TEST ERRORS (2)

public class MockEmailService : IEmailService

### 1. Register_ValidEmpleadorData (Line 46){

**Error:** `KeyNotFoundException` - JSON property `perfilId` no encontrada      public Task<bool> SendActivationEmailAsync(string email, string name, string url) 

**Fix:** Revisar estructura de `RegisterResult` y actualizar test        => Task.FromResult(true);

    // ... otros métodos mock

### 2. Login_InactiveUser (Line 173)}

**Error:** Mensaje esperado "inactiv", recibido "credenciales inválidas"  ```

**Fix:** Diferenciar error messages en `LoginCommandHandler`

**Opción 2:** Feature flag `DisableEmailInTests` en appsettings.Testing.json

---**Opción 3:** Sobrescribir configuración SMTP en tests para usar servidor mock local



## 🚀 SIGUIENTE SESIÓN (2-3 horas)---



1. Crear `MockEmailService`##  TEST ERRORS IDENTIFICADOS (2)

2. Configurar en `TestWebApplicationFactory`

3. Corregir 2 errors### Error 1: Register_ValidEmpleadorData_ReturnsSuccessAndCreatesUser

4. Re-ejecutar suite completa**Test File:** AuthControllerTests.cs, line 46

5. Validar 58/58 PASS**Error Type:** `KeyNotFoundException`

6. Push y PR**Message:** `The given key was not present in the dictionary`



---**Análisis:**

```csharp

**Generado:** 2025-01-27 15:36:00// Line 46 probablemente:

var perfilId = responseJson.GetProperty("perfilId").GetInt32();

// Pero el API devuelve:
[15:31:25 INF] Usuario registrado exitosamente - PerfilId: MiGenteEnLinea.Application.Features.Authentication.DTOs.RegisterResult
```

**Root Cause:** Test espera `perfilId` en JSON response, pero `RegisterCommand` devuelve objeto `RegisterResult` con estructura diferente.

**Fix Requerido:**
1. Revisar `RegisterCommandHandler` - qué devuelve exactamente?
2. Actualizar test para leer estructura correcta (`RegisterResult.PerfilId` o similar)

---

### Error 2: Login_InactiveUser_ReturnsUnauthorized
**Test File:** AuthControllerTests.cs, line 173
**Error Type:** FluentAssertions failure
**Expected:** Error message contains "inactiv"
**Actual:** `{"message": "credenciales inválidas"}`

**Análisis:**
Test valida que usuario inactivo reciba mensaje específico, pero API devuelve mensaje genérico.

**Fix Requerido:**
**Opción 1:** Actualizar LoginCommandHandler para diferenciar errores:
```csharp
if (!usuario.Activo)
    return LoginResult.Failed("Cuenta no activada");
if (passwordInvalida)
    return LoginResult.Failed("Credenciales inválidas");
```

**Opción 2:** Cambiar assertion en test:
```csharp
errorMessage.Should().Contain("credenciales", "Error for inactive account");
```

---

##  TESTS QUE PASARON (Antes de interrupción)

1.  `ValidarEmail_ExistingEmail_ReturnsTrue` [1m 36s]
2.  `ValidarEmail_NonExistentEmail_ReturnsFalse` [4ms]

*(Mayoría de tests no llegaron a ejecutarse debido a timeouts SMTP)*

---

##  MÉTRICAS DE PERFORMANCE

| Métrica | Valor | Observaciones |
|---------|-------|---------------|
| Tiempo promedio por test con email | ~96-97s | 90s de timeouts SMTP + ~6s lógica |
| Tiempo promedio sin email | ~4-15ms | Tests de validación rápidos |
| Tests completados | ~4-5 | Mayoría interrumpidos por timeouts |
| Total tiempo ejecución | 342.5s | ~5 min 42s antes de cancelación |
| Warnings MSB5021 | 1 | Proceso cancelado manualmente |

---

##  ACCIONES REQUERIDAS (ORDEN PRIORIDAD)

### PRIORIDAD CRÍTICA (BLOCKER)
1. ** Mockear EmailService en tests** - sin esto, suite inviable
   - Crear `MockEmailService` en `Infrastructure/`
   - Configurar en `TestWebApplicationFactory`
   - Verificar todos los tests pasan sin SMTP

### PRIORIDAD ALTA
2. **Corregir Error 1:** JSON response structure en Register endpoint
   - Investigar qué devuelve `RegisterCommandHandler`
   - Actualizar test para leer propiedad correcta
   
3. **Corregir Error 2:** Login error message para usuario inactivo
   - Decidir estrategia (cambiar API o test)
   - Implementar fix

### PRIORIDAD MEDIA
4. **Re-ejecutar suite completa** después de fixes
5. **Documentar coverage report**
6. **Push branch y crear PR**

---

##  LECCIONES APRENDIDAS

1.  **Integration Tests deben mockear servicios externos** (SMTP, Payment Gateways, etc.)
2.  **TestWebApplicationFactory debe configurar servicios fake** para ambiente de testing
3.  **NO ejecutar tests con servicios externos reales**  timeouts masivos
4.  **API response structure debe validarse antes de escribir tests**  evitar KeyNotFoundException

---

##  SIGUIENTE SESIÓN

**Objetivo Principal:** Desbloquear suite de tests eliminando dependencias SMTP

**Tareas:**
1. Crear `MockEmailService` con implementación fake
2. Configurar en `TestWebApplicationFactory.ConfigureWebHost()`
3. Corregir 2 test errors identificados
4. Re-ejecutar `dotnet test` completo
5. Validar 58/58 tests PASS
6. Documentar cobertura final
7. Push y PR

**Estimado:** 2-3 horas

---

##  REFERENCIAS

- **Test Suite:** `tests/MiGenteEnLinea.IntegrationTests/`
- **Total Tests:** 58 (AuthController: 14, Empleadores: 8, Contratistas: 6, Empleados: 12, Suscripciones: 8, BusinessLogic: 10)
- **Branch:** `feature/integration-tests-rewrite`
- **Last Commit:** `3cb9271 -  BLOQUE 6: Business Logic tests rewritten (10 tests, 0 errors)`
- **Test Framework:** xUnit 2.9.2, FluentAssertions 6.12.1
- **Web Testing:** Microsoft.AspNetCore.Mvc.Testing 8.0.0

---

**Generado automáticamente:** 2025-10-26 15:39:12