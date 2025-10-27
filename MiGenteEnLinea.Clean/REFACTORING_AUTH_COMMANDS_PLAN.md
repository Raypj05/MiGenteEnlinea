# Plan de Refactoring: Authentication Commands - Identity Primary + Legacy Sync

**Fecha:** 2025-10-26  
**Objetivo:** Refactorizar todos los Authentication Commands para usar ASP.NET Core Identity como sistema primario, manteniendo sincronización con tablas Legacy para compatibilidad.

---

## 📊 Estado Actual de Handlers

### ✅ YA REFACTORIZADOS (Identity Primary)

#### 1. RegisterCommandHandler ✅
- **Estado:** Refactorizado completamente
- **Estrategia:** Identity Primary + Legacy Sync
- **Flujo:**
  1. Crear usuario en Identity (`IIdentityService.RegisterAsync()`)
  2. Sincronizar con Legacy (Perfiles, Credenciales, Contratistas via IUnitOfWork)
  3. Enviar email de activación
- **Test:** ✅ PASS (`Register_AsEmpleador_CreatesUserAndProfile`)
- **Archivo:** `RegisterCommandHandler.cs`

#### 2. LoginCommandHandler ✅
- **Estado:** Ya usa `IIdentityService.LoginAsync()`
- **Estrategia:** Identity Primary
- **Flujo:**
  1. Autenticación vía Identity (verifica password, lockout, email confirmed)
  2. Genera JWT tokens
  3. Actualiza `UltimoLogin` en Identity
- **⚠️ Pendiente:** Sincronizar `UltimoAcceso` con tabla Legacy `Credenciales`
- **Archivo:** `LoginCommandHandler.cs`

---

## 🔄 REQUIEREN REFACTORING (Usan solo Legacy)

### 3. ActivateAccountCommandHandler ❌
- **Estado Actual:** Usa solo Legacy (ICredencialRepository + IUnitOfWork)
- **Problema:** No activa cuenta en Identity (AspNetUsers.EmailConfirmed)
- **Estrategia Propuesta:**
  1. ✅ Confirmar email en Identity (`IIdentityService.ConfirmEmailAsync(userId, token)`)
  2. ✅ Sincronizar con Legacy (`Credencial.Activar()` + IUnitOfWork)
- **Cambios Requeridos:**
  - Inyectar `IIdentityService` en el handler
  - Llamar a `ConfirmEmailAsync()` primero
  - Mantener sincronización Legacy
- **Archivo:** `ActivateAccountCommandHandler.cs`

### 4. ChangePasswordCommandHandler ❌
- **Estado Actual:** (Necesito revisar)
- **Estrategia Propuesta:**
  1. ✅ Cambiar password en Identity (`UserManager.ChangePasswordAsync()`)
  2. ✅ Sincronizar hash con Legacy (`Credencial.PasswordHash` via IUnitOfWork)
- **Archivo:** `ChangePasswordCommandHandler.cs`

### 5. ForgotPasswordCommandHandler ❌
- **Estado Actual:** (Necesito revisar)
- **Estrategia Propuesta:**
  1. ✅ Generar token en Identity (`UserManager.GeneratePasswordResetTokenAsync()`)
  2. ✅ Enviar email con token
- **Archivo:** (Buscar si existe)

### 6. ResetPasswordCommandHandler ❌
- **Estado Actual:** (Necesito revisar)
- **Estrategia Propuesta:**
  1. ✅ Resetear password en Identity (`UserManager.ResetPasswordAsync(token)`)
  2. ✅ Sincronizar hash con Legacy
- **Archivo:** (Buscar si existe)

---

## 📋 Orden de Refactoring

### Paso 1: ActivateAccountCommand (Alta Prioridad)
**Razón:** Es crítico porque sin activar en Identity, el usuario no puede hacer login (EmailConfirmed = false)

**Cambios:**
```csharp
// ANTES (Solo Legacy)
var credencial = await _credencialRepository.GetByUserIdAsync(request.UserId);
credencial.Activar();
await _unitOfWork.SaveChangesAsync();

// DESPUÉS (Identity + Legacy Sync)
// 1. Activar en Identity
await _identityService.ConfirmEmailAsync(request.UserId, request.Token);

// 2. Sincronizar con Legacy
var credencial = await _credencialRepository.GetByUserIdAsync(request.UserId);
credencial.Activar();
await _unitOfWork.SaveChangesAsync();
```

### Paso 2: ChangePasswordCommand (Media Prioridad)

**Cambios:**
```csharp
// DESPUÉS (Identity + Legacy Sync)
// 1. Cambiar en Identity
await _userManager.ChangePasswordAsync(user, currentPassword, newPassword);

// 2. Sincronizar con Legacy
var credencial = await _credencialRepository.GetByUserIdAsync(userId);
credencial.UpdatePassword(_passwordHasher.HashPassword(newPassword));
await _unitOfWork.SaveChangesAsync();
```

### Paso 3: ForgotPassword + ResetPassword (Baja Prioridad)

---

## 🎯 Testing Strategy

### Tests Actuales
- ✅ `Register_AsEmpleador_CreatesUserAndProfile` - PASS
- ⚠️ Validaciones de DB Legacy comentadas (InMemory DB issue)

### Tests Pendientes
- `Login_ValidCredentials_ReturnsTokens`
- `ActivateAccount_ValidToken_ActivatesUser`
- `ChangePassword_ValidCurrentPassword_UpdatesPassword`

### Configuración de Test Database Real (Después de Refactoring)
- Crear database `MiGenteEnLinea_IntegrationTests` en SQL Server
- Configurar `TestWebApplicationFactory` para usar SQL Server en lugar de InMemory
- Re-habilitar validaciones de Legacy en tests
- Ejecutar suite completa

---

## 🔒 Consideraciones de Seguridad

1. **Tokens de Activación:** El Legacy usa `userId + email` como "token", pero Identity usa tokens encriptados. Necesitamos soporte para ambos durante migración.

2. **Password Sync:** BCrypt work factor debe ser consistente (12) entre Identity y Legacy.

3. **Rollback Strategy:** Si Identity falla, no sincronizar Legacy. Si Legacy falla, usuario aún puede autenticarse con Identity.

---

## 📝 Notas de Implementación

### Patrón Consistente para Todos los Handlers

```csharp
public async Task Handle(Command request, CancellationToken ct)
{
    // PASO 1: OPERACIÓN PRIMARIA EN IDENTITY
    await _identityService.XXXAsync(...);
    
    // PASO 2: SINCRONIZACIÓN CON LEGACY (best effort)
    try 
    {
        var legacyEntity = await _unitOfWork.XXX.GetAsync(...);
        legacyEntity.XXX();
        await _unitOfWork.SaveChangesAsync(ct);
    }
    catch (Exception ex)
    {
        // NO fallar la operación si Legacy sync falla
        _logger.LogError(ex, "Error syncing with Legacy, but Identity operation succeeded");
    }
    
    return result;
}
```

### Inyección de Dependencias

Todos los handlers necesitan:
- `IIdentityService` (primario)
- `IUnitOfWork` (sync Legacy)
- `ILogger<T>` (logging)

---

## ✅ Checklist de Refactoring

### Por cada Command:
- [ ] Agregar `IIdentityService` al constructor
- [ ] Cambiar lógica principal a Identity
- [ ] Agregar sincronización Legacy en try-catch
- [ ] Actualizar logs para reflejar nueva estrategia
- [ ] Escribir/actualizar tests
- [ ] Documentar cambios en handler

### Al finalizar:
- [ ] Ejecutar suite completa de tests (con InMemory)
- [ ] Configurar Test Database SQL Server
- [ ] Re-ejecutar tests con SQL Server
- [ ] Validar performance (no debe degradarse)
- [ ] Actualizar documentación

---

**Siguiente Acción:** Refactorizar `ActivateAccountCommandHandler`
