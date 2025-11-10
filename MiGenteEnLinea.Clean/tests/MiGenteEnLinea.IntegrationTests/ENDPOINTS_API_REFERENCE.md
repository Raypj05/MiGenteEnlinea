# 🎯 GUÍA COMPLETA DE ENDPOINTS - API TESTING REFERENCE

> **Propósito**: Documentación de todos los endpoints reales del API para escribir integration tests
> **Ubicación**: `src/Presentation/MiGenteEnLinea.API/Controllers/`
> **Base URL**: `http://localhost:5015/api`

---

## 📋 ÍNDICE DE CONTROLLERS

1. [AuthController](#authcontroller) - Autenticación y usuarios (11 endpoints)
2. [ContratistasController](#contratistascontroller) - Gestión de contratistas (18 endpoints)
3. [EmpleadoresController](#empleadorescontroller) - Gestión de empleadores (20 endpoints)
4. [EmpleadosController](#empleadoscontroller) - Gestión de empleados (37 endpoints)
5. [NominasController](#nominascontroller) - Procesamiento de nóminas (15 endpoints)
6. [SuscripcionesController](#suscripcionescontroller) - Planes y suscripciones (19 endpoints)
7. [ContratacionesController](#contratacionescontroller) - Contrataciones temporales (12 endpoints)
8. [CalificacionesController](#calificacionescontroller) - Sistema de calificaciones (5 endpoints)
9. [PagosController](#pagoscontroller) - Procesamiento de pagos (8 endpoints)
10. [UtilitariosController](#utilitarioscontroller) - Utilidades y catálogos (10 endpoints)

---

## 1️⃣ AuthController

**Ruta Base**: `/api/auth`  
**Autenticación**: No requerida (excepto perfil, logout)  
**Total Endpoints**: 11

### 🔐 POST /api/auth/register
**Descripción**: Registrar nuevo usuario (Empleador o Contratista)  
**Autenticación**: ❌ No requerida  
**Body**:
```json
{
  "email": "usuario@example.com",
  "password": "Password123!",
  "nombre": "Juan",
  "apellido": "Pérez",
  "tipo": 1,  // 1=Empleador, 2=Contratista
  "host": "https://localhost:5015"
}
```
**Response**: `201 Created`
```json
{
  "userId": 123,
  "identityUserId": "guid-here",
  "email": "usuario@example.com",
  "message": "Usuario registrado exitosamente"
}
```

### 🔑 POST /api/auth/login
**Descripción**: Autenticar usuario y obtener JWT tokens  
**Autenticación**: ❌ No requerida  
**Body**:
```json
{
  "email": "usuario@example.com",
  "password": "Password123!",
  "ipAddress": "192.168.1.100"
}
```
**Response**: `200 OK`
```json
{
  "accessToken": "eyJhbGc...",
  "refreshToken": "a1b2c3...",
  "accessTokenExpires": "2025-01-15T12:30:00Z",
  "refreshTokenExpires": "2025-01-22T11:15:00Z",
  "user": {
    "userId": "guid",
    "email": "usuario@example.com",
    "nombreCompleto": "Juan Pérez",
    "tipo": "1",
    "planId": 2,
    "roles": ["Empleador"]
  }
}
```

### ✅ POST /api/auth/activate
**Descripción**: Activar cuenta de usuario  
**Autenticación**: ❌ No requerida  
**Body**:
```json
{
  "userId": "guid-or-int",
  "email": "usuario@example.com"
}
```
**Response**: `200 OK`

### 🔄 POST /api/auth/refresh
**Descripción**: Renovar access token usando refresh token  
**Autenticación**: ❌ No requerida  
**Body**:
```json
{
  "refreshToken": "a1b2c3...",
  "ipAddress": "192.168.1.100"
}
```
**Response**: `200 OK` (mismo formato que login)

### 👤 GET /api/auth/perfil
**Descripción**: Obtener perfil del usuario autenticado  
**Autenticación**: ✅ Bearer Token requerido  
**Response**: `200 OK`
```json
{
  "userId": "guid",
  "email": "usuario@example.com",
  "nombre": "Juan",
  "apellido": "Pérez",
  "tipo": 1,
  "planId": 2,
  "activo": true
}
```

### 🔒 POST /api/auth/change-password
**Descripción**: Cambiar contraseña del usuario actual  
**Autenticación**: ✅ Bearer Token requerido  
**Body**:
```json
{
  "currentPassword": "OldPassword123!",
  "newPassword": "NewPassword456!"
}
```
**Response**: `200 OK`

### 📧 POST /api/auth/forgot-password
**Descripción**: Solicitar reset de contraseña por email  
**Autenticación**: ❌ No requerida  
**Body**:
```json
{
  "email": "usuario@example.com",
  "host": "https://localhost:5015"
}
```
**Response**: `200 OK`

### 🔓 POST /api/auth/reset-password
**Descripción**: Resetear contraseña con token de email  
**Autenticación**: ❌ No requerida  
**Body**:
```json
{
  "email": "usuario@example.com",
  "token": "reset-token-from-email",
  "newPassword": "NewPassword123!"
}
```
**Response**: `200 OK`

**Otros Endpoints**:
- `POST /api/auth/logout` - Cerrar sesión (revoca refresh token)
- `GET /api/auth/validar-correo?email={email}` - Verificar si email existe
- `PUT /api/auth/update-profile` - Actualizar perfil básico

---

## 2️⃣ ContratistasController

**Ruta Base**: `/api/contratistas`  
**Autenticación**: ✅ Requerida (Bearer Token)  
**Total Endpoints**: 18

### ➕ POST /api/contratistas
**Descripción**: Crear perfil de contratista  
**Body**:
```json
{
  "nombre": "Juan",
  "apellido": "Pérez",
  "identificacion": "00112233445",
  "titulo": "Plomero Profesional",
  "telefono1": "8095551234",
  "email": "juan@example.com",
  "activo": true
}
```
**Response**: `201 Created`
```json
{
  "contratistaId": 123,
  "message": "Contratista creado exitosamente"
}
```

### 🔍 GET /api/contratistas/{contratistaId}
**Descripción**: Obtener contratista por ID  
**Response**: `200 OK`
```json
{
  "contratistaId": 123,
  "userId": "guid",
  "nombre": "Juan",
  "apellido": "Pérez",
  "titulo": "Plomero Profesional",
  "presentacion": "Plomero con 10 años de experiencia",
  "telefono1": "8095551234",
  "email": "juan@example.com",
  "activo": true,
  "calificacionPromedio": 4.5,
  "totalCalificaciones": 20
}
```

### 🔍 GET /api/contratistas/by-user/{userId}
**Descripción**: Obtener contratista por userId  
**Response**: `200 OK` (mismo formato que anterior)

### 🔍 GET /api/contratistas/search
**Descripción**: Buscar contratistas por criterios  
**Query Params**:
- `nombre` (string, optional)
- `servicio` (int, optional) - ID del servicio
- `provincia` (int, optional) - ID de provincia
- `activo` (bool, optional) - default: true
**Response**: `200 OK`
```json
[
  {
    "contratistaId": 123,
    "nombre": "Juan Pérez",
    "titulo": "Plomero",
    "calificacionPromedio": 4.5
  }
]
```

### ✏️ PUT /api/contratistas/{contratistaId}
**Descripción**: Actualizar datos del contratista  
**Body**: (mismo formato que POST pero con contratistaId)  
**Response**: `200 OK`

### 🔴 PUT /api/contratistas/{contratistaId}/desactivar
**Descripción**: Desactivar perfil de contratista  
**Response**: `200 OK`

### 🟢 PUT /api/contratistas/{contratistaId}/activar
**Descripción**: Activar perfil de contratista  
**Response**: `200 OK`

### ➕ POST /api/contratistas/{contratistaId}/servicios
**Descripción**: Agregar servicio al contratista  
**Body**:
```json
{
  "contratistaId": 123,
  "servicioId": 1,
  "detalleServicio": "Reparación de tuberías"
}
```
**Response**: `200 OK`

### 🗑️ DELETE /api/contratistas/{contratistaId}/servicios/{servicioId}
**Descripción**: Remover servicio del contratista  
**Response**: `204 No Content`

### 📋 GET /api/contratistas/{contratistaId}/servicios
**Descripción**: Obtener servicios del contratista  
**Response**: `200 OK`
```json
[
  {
    "servicioId": 1,
    "nombreServicio": "Plomería",
    "detalleServicio": "Reparación de tuberías"
  }
]
```

### 🖼️ PUT /api/contratistas/{contratistaId}/imagen
**Descripción**: Actualizar foto de perfil  
**Content-Type**: `multipart/form-data`  
**Response**: `200 OK`

---

## 3️⃣ EmpleadoresController

**Ruta Base**: `/api/empleadores`  
**Autenticación**: ✅ Requerida  
**Total Endpoints**: 20

### ➕ POST /api/empleadores
**Descripción**: Crear perfil de empleador  
**Body**:
```json
{
  "nombre": "Carlos",
  "apellido": "Rodríguez",
  "nombreEmpresa": "Empresa Test SRL",
  "rnc": "123456789",
  "telefonoOficina": "8094441234",
  "email": "carlos@example.com",
  "planId": 1
}
```
**Response**: `201 Created`
```json
{
  "empleadorId": 123,
  "message": "Empleador creado exitosamente"
}
```

### 🔍 GET /api/empleadores/{empleadorId}
**Descripción**: Obtener empleador por ID  
**Response**: `200 OK`

### 🔍 GET /api/empleadores/by-user/{userId}
**Descripción**: Obtener empleador por userId  
**Response**: `200 OK`

### ✏️ PUT /api/empleadores/{empleadorId}
**Descripción**: Actualizar datos del empleador  
**Response**: `200 OK`

### 📋 GET /api/empleadores/{empleadorId}/empleados
**Descripción**: Obtener lista de empleados del empleador  
**Response**: `200 OK`
```json
[
  {
    "empleadoId": 456,
    "nombre": "Pedro López",
    "cedula": "00112233445",
    "salarioBase": 35000,
    "activo": true
  }
]
```

**Otros Endpoints**:
- `GET /api/empleadores/{empleadorId}/plan` - Obtener plan actual
- `PUT /api/empleadores/{empleadorId}/plan` - Cambiar plan
- `GET /api/empleadores/{empleadorId}/estadisticas` - Estadísticas del empleador

---

## 4️⃣ EmpleadosController

**Ruta Base**: `/api/empleados`  
**Autenticación**: ✅ Requerida  
**Total Endpoints**: 37

### ➕ POST /api/empleados
**Descripción**: Crear nuevo empleado  
**Body**:
```json
{
  "empleadorId": 123,
  "nombre": "Pedro",
  "apellido": "López",
  "cedula": "00112233445",
  "salarioBase": 35000,
  "cargo": "Operario",
  "fechaIngreso": "2025-01-15"
}
```
**Response**: `201 Created`

### 🔍 GET /api/empleados/{empleadoId}
**Descripción**: Obtener empleado por ID  
**Response**: `200 OK`

### ✏️ PUT /api/empleados/{empleadoId}
**Descripción**: Actualizar datos del empleado  
**Response**: `200 OK`

### 📊 POST /api/empleados/procesar-pago
**Descripción**: Procesar pago de nómina  
**Body**:
```json
{
  "empleadorId": 123,
  "periodo": "2025-01"
}
```
**Response**: `200 OK`

**Otros Endpoints**:
- `GET /api/empleados/{empleadoId}/recibos` - Obtener recibos de pago
- `POST /api/empleados/{empleadoId}/remuneraciones` - Agregar remuneraciones
- `POST /api/empleados/{empleadoId}/deducciones` - Agregar deducciones
- `PUT /api/empleados/{empleadoId}/dar-baja` - Dar de baja empleado

---

## 5️⃣ NominasController

**Ruta Base**: `/api/nominas`  
**Autenticación**: ✅ Requerida  
**Total Endpoints**: 15

### 📊 POST /api/nominas/procesar
**Descripción**: Procesar nómina mensual  
**Body**:
```json
{
  "empleadorId": 123,
  "periodo": "2025-01",
  "empleadoIds": [1, 2, 3]
}
```
**Response**: `200 OK`

### 📋 GET /api/nominas/{empleadorId}/periodo/{periodo}
**Descripción**: Obtener nómina de un periodo  
**Response**: `200 OK`

**Otros Endpoints**:
- `GET /api/nominas/{nominaId}/recibos` - Obtener recibos individuales
- `POST /api/nominas/{nominaId}/enviar-recibos` - Enviar recibos por email

---

## 🎯 HELPERS DE IntegrationTestBase

Para facilitar la creación de tests, usa estos métodos helper:

```csharp
// Crear contratista completo (register + login + perfil)
var (userId, email, token, contratistaId) = await CreateContratistaAsync(
    nombre: "Juan",
    apellido: "Pérez",
    titulo: "Plomero"
);

// Crear empleador completo
var (userId, email, token, empleadorId) = await CreateEmpleadorAsync(
    nombre: "Carlos",
    nombreEmpresa: "Empresa Test SRL"
);

// Login manual
var token = await LoginAsync("usuario@example.com", "Password123!");
SetAuthToken(token); // Configura token en HttpClient

// Generar datos únicos
var email = GenerateUniqueEmail("test");
var cedula = GenerateRandomIdentification();
var rnc = GenerateRandomRNC();
```

---

## 📝 EJEMPLO DE TEST COMPLETO

```csharp
[Collection("IntegrationTests")]
public class MiTestSuite : IntegrationTestBase
{
    public MiTestSuite(TestWebApplicationFactory factory) : base(factory) {}

    [Fact]
    public async Task CrearYBuscarContratista_DebeSerExitoso()
    {
        // Arrange - Crear contratista con nombre único
        var nombreUnico = $"Test_{Guid.NewGuid():N}";
        var (userId, email, token, id) = await CreateContratistaAsync(
            nombre: nombreUnico
        );

        // Act - Buscar por nombre
        var response = await Client.GetAsync(
            $"/api/contratistas/search?nombre={nombreUnico}"
        );

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var results = await response.Content
            .ReadFromJsonAsync<List<ContratistaDto>>();
        results.Should().Contain(c => c.Nombre == nombreUnico);
    }
}
```

---

## ✅ CHECKLIST PARA NUEVOS TESTS

1. ✅ Hereda de `IntegrationTestBase`
2. ✅ Usa `[Collection("IntegrationTests")]`
3. ✅ Crea datos usando helpers (`CreateContratistaAsync`, etc.)
4. ✅ Usa nombres/emails únicos con GUID
5. ✅ Verifica StatusCode + contenido de respuesta
6. ✅ No dependas de datos seed (excepto catálogos)
7. ✅ Tests independientes - no dependen de orden

---

**📌 NOTA**: Esta guía refleja los endpoints REALES implementados en `src/Presentation/MiGenteEnLinea.API/Controllers/`. Si un endpoint falla, el bug está en el Controller/Handler, NO en el test.
