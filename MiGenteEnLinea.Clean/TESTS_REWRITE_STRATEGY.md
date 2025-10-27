# 🧪 ESTRATEGIA DE REESCRITURA DE INTEGRATION TESTS

**Fecha:** 26 de Octubre 2025  
**Objetivo:** Reescribir 58 tests con estructuras correctas hasta compilar a 0 errores

---

## 📊 Estado Actual

- **Errores Actuales:** 218 (baseline confirmado)
- **Tests Totales:** 58 distribuidos en 4 archivos
- **Progreso:** 0/58 tests reescritos (0%)

---

## 🎯 ESTRATEGIA RECOMENDADA: Reescritura Incremental

Dado que reescribir 58 tests manualmente tomará 4-6 horas, propongo un enfoque más pragmático:

### FASE 1: Tests Críticos Mínimos (1-2 horas) ⭐ RECOMENDADO

Crear tests mínimos solo para flujos críticos de negocio:

#### 1.1 Authentication (5 tests mínimos)
```csharp
✅ Register_AsEmpleador_Success
✅ Login_WithValidCredentials_Success  
✅ ActivateAccount_Success
✅ RefreshToken_Success
✅ ChangePassword_Success
```

#### 1.2 Empleadores (3 tests mínimos)
```csharp
✅ CreateEmpleador_Success
✅ GetEmpleador_Success
✅ UpdateEmpleador_Success
```

#### 1.3 Contratistas (3 tests mínimos)
```csharp
✅ CreateContratista_Success
✅ GetContratista_Success
✅ UpdateContratista_Success
```

#### 1.4 Suscripciones (2 tests mínimos)
```csharp
✅ CreateSuscripcion_Success
✅ GetSuscripcionActiva_Success
```

**Total:** 13 tests (22% coverage) → **Compila a 0 errores** → **Validación funcional básica**

**Beneficio:** Rápido, permite continuar desarrollo, coverage mínimo funcional

---

### FASE 2: Completar Coverage (2-3 horas adicionales)

Una vez que FASE 1 compile y funcione, agregar tests faltantes:

- Edge cases (validación, errores, unauthorized, etc.)
- Tests de búsqueda y filtrado
- Tests de eliminación/desactivación
- Tests de pagos (ProcessPayment)

**Total:** 45 tests adicionales → **Coverage 80%+**

---

## 📋 COMANDOS COMPILADOS CORRECTOS

### Authentication Commands (✅ Verificados)

```csharp
// RegisterCommand
public sealed record RegisterCommand : IRequest<RegisterResult>
{
    public required string Email { get; init; }
    public required string Password { get; init; }
    public required string Nombre { get; init; }
    public required string Apellido { get; init; }
    public required int Tipo { get; init; } // 1=Empleador, 2=Contratista
    public string? Telefono1 { get; init; }
    public string? Telefono2 { get; init; }
    public required string Host { get; init; } // Para activation link
}

// LoginCommand
public record LoginCommand : IRequest<AuthenticationResultDto>
{
    public required string Email { get; init; }
    public required string Password { get; init; }
    public required string IpAddress { get; init; }
}

// ActivateAccountCommand
public sealed record ActivateAccountCommand : IRequest<bool>
{
    public required string UserId { get; init; }
    public required string Email { get; init; }
}

// ChangePasswordCommand (PRIMARY CONSTRUCTOR)
public record ChangePasswordCommand(
    string Email,
    string UserId,
    string NewPassword
) : IRequest<ChangePasswordResult>;

// RefreshTokenCommand (PRIMARY CONSTRUCTOR)
public record RefreshTokenCommand(
    string RefreshToken,
    string IpAddress
) : IRequest<AuthenticationResultDto>;

// RevokeTokenCommand (PRIMARY CONSTRUCTOR)
public record RevokeTokenCommand(
    string RefreshToken,
    string IpAddress
) : IRequest;
```

### Empleadores Commands (✅ Verificados)

```csharp
// CreateEmpleadorCommand (PRIMARY CONSTRUCTOR)
public record CreateEmpleadorCommand(
    string UserId,
    string? Habilidades = null,
    string? Experiencia = null,
    string? Descripcion = null
) : IRequest<int>;

// UpdateEmpleador - Necesita verificarse
// DeleteEmpleador - Necesita verificarse
```

### Contratistas Commands (✅ Verificados)

```csharp
// CreateContratistaCommand (PRIMARY CONSTRUCTOR)
public record CreateContratistaCommand(
    string UserId,
    string Nombre,
    string Apellido,
    int Tipo = 1,
    string? Titulo = null,
    string? Identificacion = null,
    string? Sector = null,
    int? Experiencia = null,
    string? Presentacion = null,
    string? Telefono1 = null,
    bool Whatsapp1 = false,
    string? Provincia = null
) : IRequest<int>;

// UpdateContratistaCommand (PRIMARY CONSTRUCTOR)
public record UpdateContratistaCommand(
    string UserId,
    string? Titulo = null,
    string? Sector = null,
    int? Experiencia = null,
    string? Presentacion = null,
    string? Provincia = null,
    bool? NivelNacional = null,
    string? Telefono1 = null,
    bool? Whatsapp1 = null,
    string? Telefono2 = null,
    bool? Whatsapp2 = null,
    string? Email = null
) : IRequest;
```

### Suscripciones Commands (✅ Verificados)

```csharp
// CreateSuscripcionCommand
public record CreateSuscripcionCommand : IRequest<int>
{
    public string UserId { get; init; }
    public int PlanId { get; init; }
    public DateTime? FechaInicio { get; init; }
}

// ProcesarVentaCommand (NO ProcessPaymentCommand!)
public record ProcesarVentaCommand : IRequest<int>
{
    public string UserId { get; init; }
    public int PlanId { get; init; }
    public string CardNumber { get; init; }
    public string Cvv { get; init; }
    public string ExpirationDate { get; init; } // MMYY
    public string? ClientIp { get; init; }
    public string? ReferenceNumber { get; init; }
    public string? InvoiceNumber { get; init; }
}
```

---

## 🚀 PRÓXIMA ACCIÓN RECOMENDADA

### Opción A: Implementar FASE 1 (Tests Mínimos) - ⭐ RECOMENDADO

**Tiempo:** 1-2 horas  
**Resultado:** 13 tests compilando y pasando, 0 errores de compilación  
**Ventaja:** Rápido, funcional, permite continuar desarrollo backend

**Comando para iniciar:**
```powershell
# Crear archivo nuevo con solo 13 tests críticos
# Compilar a 0 errores
# Ejecutar y verificar 13/13 passing
```

### Opción B: Reescritura Completa Manual (58 tests)

**Tiempo:** 4-6 horas  
**Resultado:** Coverage completo 80%+  
**Desventaja:** Muy lento, muchos tokens, puede tener bugs

---

## 📝 TEMPLATE DE TEST CORRECTO

```csharp
[Fact]
public async Task OperationName_Scenario_ExpectedResult()
{
    // Arrange
    await LoginAsync("user@test.com", TestDataSeeder.TestPasswordPlainText);
    
    var command = new SomeCommand(
        UserId: "user-id",
        Property: "value"
    ); // Primary constructor syntax!

    // Act
    var response = await Client.PostAsJsonAsync("/api/endpoint", command);

    // Assert
    response.IsSuccessStatusCode.Should().BeTrue();
    var result = await response.Content.ReadFromJsonAsync<ResultDto>();
    result.Should().NotBeNull();
    
    // DB Verification
    var entity = await AppDbContext.Entities // ✅ AppDbContext, NOT DbContext
        .FirstOrDefaultAsync(e => e.Id == result!.Id);
    entity.Should().NotBeNull();
}
```

---

**Decisión Usuario:** ¿Proceder con Opción A (tests mínimos) u Opción B (reescritura completa)?
