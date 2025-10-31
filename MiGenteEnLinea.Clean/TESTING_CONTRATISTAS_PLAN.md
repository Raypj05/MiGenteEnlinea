# 🧪 Testing Plan: ContratistasController

**Created:** October 30, 2025  
**Branch:** `feature/integration-tests-rewrite`  
**Target:** 24+ tests (120% minimum target of 20 tests)  
**Strategy:** Follow proven EmpleadoresController pattern

---

## 📊 Controller Analysis

### Endpoints Available (13 endpoints)

| Method | Endpoint | Purpose | Command/Query |
|--------|----------|---------|---------------|
| POST | `/api/contratistas` | Create contratista profile | CreateContratistaCommand |
| GET | `/api/contratistas/{contratistaId}` | Get by ID | GetContratistaByIdQuery |
| GET | `/api/contratistas/by-user/{userId}` | Get by userId | GetContratistaByUserIdQuery |
| GET | `/api/contratistas/cedula/{userId}` | Get cedula only | GetCedulaByUserIdQuery |
| GET | `/api/contratistas` | Search with filters | SearchContratistasQuery |
| GET | `/api/contratistas/{contratistaId}/servicios` | Get servicios list | GetServiciosContratistaQuery |
| PUT | `/api/contratistas/{userId}` | Update profile | UpdateContratistaCommand |
| PUT | `/api/contratistas/{userId}/imagen` | Update imagen URL | UpdateContratistaImagenCommand |
| POST | `/api/contratistas/{userId}/activar` | Activate profile | ActivarPerfilCommand |
| POST | `/api/contratistas/{userId}/desactivar` | Deactivate profile | DesactivarPerfilCommand |
| POST | `/api/contratistas/{contratistaId}/servicios` | Add servicio | AddServicioCommand |
| DELETE | `/api/contratistas/{contratistaId}/servicios/{servicioId}` | Remove servicio | RemoveServicioCommand |

---

## 🎯 Test Plan (24 tests target)

### Phase 1: Basic CRUD Operations (8 tests) - Target Checkpoint 1

#### 1. Create Tests (3 tests)
1. **CreateContratista_WithValidData_ReturnsCreatedAndId** ✅
   - Register Contratista user → Login → POST contratista
   - Verify HTTP 201 Created
   - Verify response: `{ contratistaId: int, message: string }`
   - Verify contratistaId > 0

2. **CreateContratista_WithoutAuthentication_ReturnsUnauthorized** ✅
   - POST contratista without JWT token
   - Verify HTTP 401 Unauthorized

3. **CreateContratista_DuplicateUserId_ReturnsBadRequest** ✅
   - Create contratista for userId
   - Try to create again with same userId
   - Verify HTTP 400 Bad Request
   - Verify error message contains "ya tiene perfil"

#### 2. Read Tests (3 tests)
4. **GetContratistaById_WithValidId_ReturnsContratistaDto** ✅
   - Create contratista → GET by contratistaId
   - Verify HTTP 200 OK
   - Verify ContratistaDto structure (all properties)

5. **GetContratistaById_WithNonExistentId_ReturnsNotFound** ✅
   - GET with contratistaId = 999999 (non-existent)
   - Verify HTTP 404 Not Found

6. **GetContratistaByUserId_WithValidUserId_ReturnsProfile** ✅
   - Create contratista → GET /api/contratistas/by-user/{userId}
   - Verify HTTP 200 OK
   - Verify ContratistaDto matches created data

#### 3. Update Tests (2 tests)
7. **UpdateContratista_WithValidData_UpdatesSuccessfully** ✅
   - Create contratista → PUT with updated fields
   - Verify HTTP 200 OK
   - Verify response: `{ message: "actualizado exitosamente" }`
   - GET and verify changes applied

8. **UpdateContratista_WithoutAuthentication_ReturnsUnauthorized** ✅
   - PUT contratista without JWT token
   - Verify HTTP 401 Unauthorized

---

### Phase 2: Delete Operations + Authorization (8 tests) - Target Checkpoint 2 (16 tests)

#### 4. Soft Delete Tests (3 tests)
9. **DesactivarPerfil_WithValidUserId_DeactivatesSuccessfully** ✅
   - Create contratista → POST /{userId}/desactivar
   - Verify HTTP 200 OK
   - GET contratista → verify `Activo = false`

10. **ActivarPerfil_AfterDesactivar_ActivatesSuccessfully** ✅
    - Create contratista → Desactivar → Activar
    - Verify HTTP 200 OK each time
    - GET contratista → verify `Activo = true`

11. **DesactivarPerfil_WithNonExistentUserId_ReturnsNotFound** ✅
    - POST /{nonExistentUserId}/desactivar
    - Verify HTTP 404 Not Found

#### 5. Authorization Tests (3 tests)
12. **UpdateContratista_OtherUserProfile_ReturnsForbidden** ✅
    - Create User A contratista → Login as User B → PUT User A profile
    - Verify HTTP 403 Forbidden
    - Verify error message about permissions

13. **CreateContratista_AsEmpleador_ShouldCreateSuccessfully** ✅
    - Register as Empleador → Create Contratista profile
    - Verify HTTP 201 Created (users can have both profiles)

14. **DesactivarPerfil_WithoutAuthentication_ReturnsUnauthorized** ✅
    - POST /{userId}/desactivar without JWT token
    - Verify HTTP 401 Unauthorized

#### 6. Search Tests (2 tests)
15. **SearchContratistas_WithFilters_ReturnsFilteredResults** ✅
    - Create 2+ contratistas with different sectors
    - GET /api/contratistas?sector=Construccion
    - Verify SearchContratistasResult structure
    - Verify only matching sector returned

16. **SearchContratistas_WithPagination_ReturnsPagedResults** ✅
    - Create 5+ contratistas
    - GET /api/contratistas?pageIndex=1&pageSize=2
    - Verify pagination metadata (TotalRecords, PageSize, etc.)
    - Verify only 2 contratistas in result

---

### Phase 3: Servicios Management (4 tests) - Target Checkpoint 3 (20 tests)

#### 7. Servicios Tests (4 tests)
17. **AddServicio_WithValidData_CreatesSuccessfully** ✅
    - Create contratista → POST /{contratistaId}/servicios
    - Verify HTTP 201 Created
    - Verify response: `{ servicioId: int, message: string }`

18. **GetServiciosContratista_ReturnsListOfServicios** ✅
    - Create contratista → Add 2 servicios
    - GET /{contratistaId}/servicios
    - Verify HTTP 200 OK
    - Verify array with 2 servicios

19. **RemoveServicio_WithValidId_RemovesSuccessfully** ✅
    - Create contratista → Add servicio → DELETE /{contratistaId}/servicios/{servicioId}
    - Verify HTTP 200 OK
    - GET servicios → verify servicio removed

20. **RemoveServicio_WithNonExistentId_ReturnsNotFound** ✅
    - DELETE /{contratistaId}/servicios/999999
    - Verify HTTP 404 Not Found

---

### Phase 4: Business Logic + Image Upload (4 tests) - Target Checkpoint 4 (24 tests)

#### 8. Image Upload Tests (2 tests)
21. **UpdateContratistaImagen_WithValidUrl_UpdatesSuccessfully** ✅
    - Create contratista → PUT /{userId}/imagen with URL
    - Verify HTTP 200 OK
    - GET contratista → verify ImagenUrl updated

22. **UpdateContratistaImagen_WithEmptyUrl_ReturnsBadRequest** ✅
    - PUT /{userId}/imagen with empty/null URL
    - Verify HTTP 400 Bad Request

#### 9. Business Validation Tests (2 tests)
23. **GetCedulaByUserId_WithValidUserId_ReturnsCedula** ✅
    - Create contratista with cedula → GET /api/contratistas/cedula/{userId}
    - Verify HTTP 200 OK
    - Verify cedula string returned (11 digits)

24. **UpdateContratista_WithMaxLengthFields_UpdatesSuccessfully** ✅
    - Update contratista with max length strings
    - Titulo: 200 chars, Presentacion: 500 chars, Sector: 100 chars
    - Verify HTTP 200 OK
    - GET and verify all fields saved

---

## 🔍 Key Business Rules to Validate

### From Validators (to be read)

1. **CreateContratistaCommandValidator:**
   - UserId required, valid GUID
   - Cedula required, 11 digits, unique
   - MaxLength validations (Titulo, Presentacion, Sector, etc.)

2. **UpdateContratistaCommandValidator:**
   - "At least one field must be provided" rule
   - MaxLength validations

3. **AddServicioCommandValidator:**
   - DetalleServicio required, MaxLength validation

### Expected Behaviors

- ✅ Only one contratista profile per userId
- ✅ Cedula must be unique across system
- ✅ Soft delete via Activar/Desactivar (Activo flag)
- ✅ Servicios belong to contratista (ownership validation)
- ✅ Cross-user authorization checks (user can only edit own profile)
- ✅ Image URL validation (non-empty string)

---

## 📋 Test Implementation Order

### Sprint 1: Basic CRUD (Day 1 - ~1 hour)
- Implement tests 1-8
- Create `ContratistasControllerTests.cs`
- Setup helpers: `CreateContratistaAsync`, `LoginAsContratistaAsync`
- **Target:** 8/8 tests passing ✅

### Sprint 2: Authorization + Search (Day 1 - ~45 min)
- Implement tests 9-16
- Add authorization validation tests
- Add search/pagination tests
- **Target:** 16/16 tests passing ✅

### Sprint 3: Servicios Management (Day 2 - ~30 min)
- Implement tests 17-20
- Test AddServicio/RemoveServicio commands
- **Target:** 20/20 tests passing ✅

### Sprint 4: Business Logic + Images (Day 2 - ~30 min)
- Implement tests 21-24
- Test image URL updates
- Test cedula retrieval
- Test max length validations
- **Target:** 24/24 tests passing ✅ (120% minimum)

---

## 🛠️ Helper Methods Needed

```csharp
// From IntegrationTestBase (already exists):
protected async Task<(string UserId, string Email)> RegisterUserAsync(...)
protected async Task LoginAsync(string email, string password)

// New helpers to create in ContratistasControllerTests:
private async Task<int> CreateContratistaAsync(string userId, string cedula = "00112345678")
private async Task<int> AddServicioAsync(int contratistaId, string detalle)
```

---

## 📊 Success Metrics

| Metric | Target | Status |
|--------|--------|--------|
| Tests Implemented | 24 | ⏳ Pending |
| Tests Passing | 24 (100%) | ⏳ Pending |
| Endpoint Coverage | 13/13 (100%) | ⏳ Pending |
| Compilation | ✅ Success | ⏳ Pending |
| Execution Time | <25 seconds | ⏳ Pending |

---

## 🎯 Expected Discoveries (Like EmpleadoresController)

1. **FluentValidation Status:** Already known NOT configured (from Empleadores testing)
2. **Response Formats:** JSON structure validation (camelCase vs PascalCase)
3. **Authorization Gaps:** Cross-user edit protection (already fixed in Empleadores, apply here)
4. **Soft Delete:** Verify Activar/Desactivar uses `Activo` flag correctly
5. **Servicios Ownership:** Verify RemoveServicio validates contratista ownership

---

## 📝 Next Steps

1. ✅ Read `CreateContratistaCommandValidator.cs` for validation rules
2. ✅ Read `UpdateContratistaCommandValidator.cs` for update rules
3. ✅ Create `ContratistasControllerTests.cs` file
4. ✅ Implement Phase 1 (8 CRUD tests)
5. Run tests and document results in `TESTING_CONTRATISTAS_CHECKPOINT_1.md`

---

**Ready to start implementation!** 🚀
