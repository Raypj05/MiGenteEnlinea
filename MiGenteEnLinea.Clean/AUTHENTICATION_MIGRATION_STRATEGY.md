# 🔐 ESTRATEGIA DE MIGRACIÓN DE AUTENTICACIÓN

**Fecha:** 2025-10-26  
**Decisión Arquitectónica:** Usar ASP.NET Core Identity como sistema principal

---

## 🎯 DECISIÓN TOMADA

### ✅ Sistema Principal: ASP.NET Core Identity

**Tablas Identity (nuevas):**
- `AspNetUsers` - Usuarios
- `AspNetRoles` - Roles
- `AspNetUserRoles` - Relación usuarios-roles
- `AspNetUserClaims` - Claims personalizados
- `AspNetUserLogins` - Logins externos (OAuth)
- `AspNetUserTokens` - Tokens de verificación
- `RefreshTokens` - Tokens de renovación JWT (custom)

**Ventajas:**
- ✅ Sistema robusto y probado (Microsoft)
- ✅ Manejo automático de lockout
- ✅ Hash de passwords con PBKDF2
- ✅ Soporte para 2FA (futuro)
- ✅ OAuth/OpenID Connect (futuro)
- ✅ Roles y claims nativos

---

## 📋 SISTEMA LEGACY (Mantener para Business Logic)

**Tablas Legacy (existentes):**
- `Credenciales` - Credenciales Legacy (sincronizar)
- `Perfiles` - Datos de perfil (business logic)
- `Cuentas` - Información de cuentas (deprecated)
- `Contratistas` - Business logic
- `Suscripciones` - Business logic

**Uso:**
- ⚠️ **NO** para autenticación (Identity maneja esto)
- ✅ **SÍ** para lógica de negocio (perfiles, suscripciones, etc.)

---

## 🔄 SINCRONIZACIÓN BIDIRECCIONAL

### Durante el Registro (RegisterCommandHandler):

```csharp
// 1. Crear usuario en Identity
var identityUser = new ApplicationUser {
    Email = request.Email,
    UserName = request.Email,
    Tipo = request.Tipo.ToString(),
    NombreCompleto = $"{request.Nombre} {request.Apellido}"
};
await _userManager.CreateAsync(identityUser, request.Password);

// 2. Sincronizar con tablas Legacy (para business logic)
var perfil = Perfile.Create(identityUser.Id, request.Nombre, request.Apellido, ...);
await _unitOfWork.Perfiles.AddAsync(perfil);

var credencial = Credencial.Create(identityUser.Id, request.Email, ...);
await _unitOfWork.Credenciales.AddAsync(credencial);
```

### Durante el Login (IdentityService):

```csharp
// 1. Autenticar con Identity
var user = await _userManager.FindByEmailAsync(email);
var passwordValid = await _userManager.CheckPasswordAsync(user, password);

// 2. Obtener datos de business logic desde Legacy
var perfil = await _unitOfWork.Perfiles.GetByUserIdAsync(user.Id);
var suscripcion = await _unitOfWork.Suscripciones.GetByUserIdAsync(user.Id);

// 3. Generar JWT con datos combinados
var token = GenerateJwt(user.Id, user.Email, perfil.PlanId, suscripcion.Vencimiento);
```

---

## 🚧 ESTADO ACTUAL

### ✅ Completado
- ApplicationUser con propiedades Legacy
- RefreshToken entity
- JWT Token Service
- Identity DbContext configurado

### 🔴 Pendiente (CRÍTICO)
1. **RegisterCommandHandler** → Debe crear usuario en Identity + sincronizar Legacy
2. **IdentityService.LoginAsync** → Debe obtener datos de Legacy para JWT
3. **Migración de datos** → Copiar usuarios Legacy a AspNetUsers
4. **Configurar EmailService** → Para tests de integración

### ⚠️ Errores de Compilación Actuales
- `IdentityService.cs` tiene código Legacy mezclado (líneas 51, 54, 88, 96, 137)
- Necesita limpieza y refactor

---

## 📝 PRÓXIMOS PASOS

### PASO 1: Arreglar IdentityService (30 min)
- Eliminar código Legacy innecesario
- Verificar que compile sin errores

### PASO 2: Refactor RegisterCommandHandler (1 hora)
- Crear usuario en Identity primero
- Sincronizar con tablas Legacy
- Mantener compatibilidad con flujo actual

### PASO 3: Configurar EmailService (30 min)
- Obtener credenciales SMTP desde DB Legacy
- Configurar en appsettings.json
- Probar envío de emails de activación

### PASO 4: Ejecutar Tests de Integración (2 horas)
- Correr tests uno por uno
- Identificar problemas reales
- Corregir aplicación (NO los tests)

---

## 🎯 OBJETIVO FINAL

**Sistema Híbrido:**
- **ASP.NET Core Identity** → Autenticación, autorización, seguridad
- **Tablas Legacy** → Business logic, perfiles, suscripciones, nómina
- **Sincronización automática** → Mantener ambos sistemas actualizados
- **Migración gradual** → Ir deprecando tablas Legacy conforme avancemos

---

**Creado:** 2025-10-26 16:00  
**Última actualización:** 2025-10-26 16:00
