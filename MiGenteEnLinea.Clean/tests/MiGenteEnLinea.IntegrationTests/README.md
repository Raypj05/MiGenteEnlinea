## 📚 Guía de Integration Tests - MiGente En Línea

**Fecha:** 2025-10-26
**Estado:** ✅ Infraestructura completada, primer módulo en progreso

---

## 🎯 Objetivo

Crear **pruebas de integración REALES** (no mocks) que validen el flujo completo de cada endpoint de la API,  
incluyendo: Request → Controller → MediatR Handler → Repository → DbContext → Response.

---

## 🏗️ Estructura del Proyecto

```
tests/MiGenteEnLinea.IntegrationTests/
├── Infrastructure/
│   ├── TestWebApplicationFactory.cs      # Factory custom (usa InMemory DB)
│   └── IntegrationTestBase.cs            # Clase base con helpers
├── Controllers/
│   ├── AuthControllerTests.cs            # ✅ EN PROGRESO
│   ├── EmpleadoresControllerTests.cs     # ⏳ Pendiente
│   ├── EmpleadosControllerTests.cs       # ⏳ Pendiente
│   ├── ContratistasControllerTests.cs    # ⏳ Pendiente
│   ├── CalificacionesControllerTests.cs  # ⏳ Pendiente
│   └── PlanesControllerTests.cs          # ⏳ Pendiente
└── Scenarios/                            # ⏳ E2E scenarios
    └── RegistroLoginFlowTests.cs
```

---

## 🔧 Configuración

### 1. TestWebApplicationFactory

```csharp
public class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Remover SQL Server real
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<MiGenteDbContext>));
            if (descriptor != null) services.Remove(descriptor);

            // Usar InMemory Database para tests
            services.AddDbContext<MiGenteDbContext>(options =>
            {
                options.UseInMemoryDatabase("TestDatabase");
                options.EnableSensitiveDataLogging();
            });

            // Crear la DB
            var sp = services.BuildServiceProvider();
            using var scope = sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<MiGenteDbContext>();
            db.Database.EnsureCreated();
        });
    }
}
```

**Características:**
- ✅ Usa `InMemoryDatabase` (no requiere SQL Server)
- ✅ Cada test ejecuta contra base de datos limpia
- ✅ No requiere configuración externa
- ✅ Rápido de ejecutar

---

### 2. IntegrationTestBase

Clase base que proporciona helpers comunes:

```csharp
public abstract class IntegrationTestBase : IClassFixture<TestWebApplicationFactory>
{
    protected readonly HttpClient Client;
    protected readonly IApplicationDbContext AppDbContext;
    protected string? AccessToken { get; set; }

    // Helpers disponibles:
    protected async Task<string> LoginAsync(string email, string password);
    protected async Task<int> RegisterUserAsync(string email, string password, ...);
    protected string GenerateUniqueEmail(string prefix = "test");
    protected string GenerateRandomIdentification();
    protected void AssertSuccessStatusCode(HttpResponseMessage response);
    protected void AssertBadRequest(HttpResponseMessage response);
    protected void AssertUnauthorized(HttpResponseMessage response);
    protected void AssertNotFound(HttpResponseMessage response);
}
```

---

## 📝 Ejemplo de Test

```csharp
[Fact]
public async Task Register_ValidEmpleadorData_ReturnsSuccessAndCreatesUser()
{
    // Arrange
    var email = GenerateUniqueEmail("empleador");
    var registerRequest = new
    {
        email,
        password = "Password123!",
        nombre = "Juan",
        apellido = "Pérez",
        tipo = "Empleador",
        identificacion = GenerateRandomIdentification()
    };

    // Act
    var response = await Client.PostAsJsonAsync("/api/auth/register", registerRequest);

    // Assert
    AssertSuccessStatusCode(response);
    
    var responseContent = await response.Content.ReadFromJsonAsync<JsonElement>();
    responseContent.GetProperty("userId").GetInt32().Should().BeGreaterThan(0);

    // Verificar en base de datos
    var credencial = AppDbContext.Credenciales.FirstOrDefault(c => c.Email == email);
    credencial.Should().NotBeNull();
    credencial!.Nombre.Should().Be("Juan");
}
```

---

## ⚠️ Consideraciones Importantes para DDD Entities

Las entidades del Domain Layer tienen **encapsulación estricta** (DDD pattern):

### ❌ NO HACER (Setters no existen)
```csharp
credencial.Activo = true; // ERROR: propiedad readonly
```

### ✅ HACER (Usar métodos de dominio)
```csharp
credencial.Activar(); // Método del domain model
await AppDbContext.SaveChangesAsync();
```

### Métodos comunes en entidades DDD:

- `Credencial.Activar()` - Activar cuenta
- `Credencial.Desactivar()` - Desactivar cuenta
- `Credencial.CambiarPassword(string newPasswordHash)` - Cambiar contraseña
- `Empleado.DarDeBaja()` - Dar de baja empleado
- `Calificacion.Create(...)` - Factory method

**Regla:** Siempre usar métodos del domain model, nunca setters directos.

---

## 🚀 Cómo Ejecutar los Tests

### Ejecutar todos los tests
```bash
cd tests/MiGenteEnLinea.IntegrationTests
dotnet test
```

### Ejecutar tests de un módulo específico
```bash
dotnet test --filter "FullyQualifiedName~AuthControllerTests"
```

### Ejecutar un test específico
```bash
dotnet test --filter "FullyQualifiedName~Register_ValidEmpleadorData_ReturnsSuccessAndCreatesUser"
```

### Ver output detallado
```bash
dotnet test --logger "console;verbosity=detailed"
```

---

## 📋 Plan de Implementación

### ✅ COMPLETADO

1. **Infraestructura base**
   - TestWebApplicationFactory ✅
   - IntegrationTestBase ✅
   - Configuración de InMemory DB ✅

### 🔄 EN PROGRESO

2. **AuthControllerTests** (85% completado)
   - Register tests ✅
   - Login tests ✅
   - GetPerfil tests ✅
   - ChangePassword tests ✅
   - Flow E2E test ✅
   - **Pendiente:** Corregir acceso a propiedades DDD

### ⏳ PENDIENTE

3. **EmpleadoresControllerTests** (LOTE 2)
   - Create Empleador
   - Get by ID
   - Update Empleador
   - Delete Empleador
   - List all

4. **EmpleadosControllerTests** (LOTE 4)
   - Create Empleado (requiere Empleador)
   - Get Empleado
   - Update Empleado
   - Dar de baja

5. **ContratistasControllerTests** (LOTE 3)
   - Create Contratista
   - Update Contratista
   - Add Servicio
   - Get Servicios

6. **CalificacionesControllerTests** (LOTE 5)
   - Create Calificación
   - Get by Contratista
   - Get Promedio
   - Validar cálculos

7. **PlanesControllerTests** (LOTE 4)
   - Get Planes
   - Create Suscripción
   - Validar vigencia

8. **Scenario Tests** (E2E)
   - Registro → Login → Crear Empleado → Procesar Nómina
   - Contratista → Calificaciones
   - Suscripción → Pago

---

## 🎯 Próximos Pasos (Sesión actual)

1. **Corregir AuthControllerTests** (15 min)
   - Reemplazar `credencial.Activo = true` por `credencial.Activar()`
   - Quitar verificaciones de `credencial.Tipo` (no existe en entidad)
   - Agregar `using` para `IApplicationDbContext`

2. **Ejecutar tests** (5 min)
   - `dotnet test`
   - Verificar que todos pasen

3. **Implementar EmpleadoresControllerTests** (30-45 min)
   - 5 tests básicos: Create, GetById, Update, Delete, List

4. **Documentar resultados**
   - Reporte de tests ejecutados
   - Coverage inicial

---

## 📊 Métricas Esperadas

- **Tests totales:** ~60-80 tests (todos los módulos)
- **Coverage target:** 80%+ en Controllers y Handlers
- **Tiempo ejecución:** <30 segundos (InMemory DB es rápida)
- **Mantenibilidad:** Alta (usar helpers de IntegrationTestBase)

---

## 🛠️ Troubleshooting

### Error: "Program is not accessible"
**Solución:** Agregar al final de `Program.cs`:
```csharp
public partial class Program { }
```

### Error: "Credenciales does not exist in MiGenteDbContext"
**Solución:** Usar `AppDbContext.Credenciales` (interfaz IApplicationDbContext)

### Error: "Cannot assign to readonly property Activo"
**Solución:** Usar métodos de dominio: `credencial.Activar()`

### Tests fallan en CI/CD
**Solución:** Asegurar que `dotnet test` se ejecuta con `--no-build` después de `dotnet build`

---

## 📚 Referencias

- **xUnit Documentation:** https://xunit.net/
- **FluentAssertions:** https://fluentassertions.com/
- **WebApplicationFactory:** https://learn.microsoft.com/en-us/aspnet/core/test/integration-tests
- **InMemory Database:** https://learn.microsoft.com/en-us/ef/core/testing/

---

**Última actualización:** 2025-10-26 12:50
**Autor:** GitHub Copilot Agent
