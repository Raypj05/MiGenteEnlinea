# PagosControllerTests - Implementation Plan
**Date:** November 10, 2025  
**Status:** 🚧 **READY TO START**  
**Focus:** Exhaustive Cardnet Payment Gateway Integration Testing  
**Target:** ~46 comprehensive tests  

---

## 🎯 Testing Strategy: Exhaustive Cardnet Integration

This test suite will validate the complete payment processing workflow with Dominican Republic's primary payment gateway **Cardnet**. Tests will cover:

1. **Idempotency mechanism** (prevent duplicate charges)
2. **Payment processing** (success, failure, edge cases)
3. **Card validation** (Luhn algorithm, CVV, expiration)
4. **Transaction lifecycle** (pending → approved/declined)
5. **Error handling** (timeout, network errors, invalid responses)
6. **Webhook handling** (async payment notifications)
7. **Subscription creation/renewal** (post-payment workflows)
8. **Free subscription processing** (zero-cost plans)
9. **Transaction history** (pagination, filtering, sorting)

---

## 📋 PagosController Endpoints

### Endpoint 1: Generate Idempotency Key
```http
GET /api/pagos/idempotency
Authorization: Bearer {token}

Response 200 OK:
{
  "idempotencyKey": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "generatedAt": "2025-11-10T15:30:00Z"
}
```

**Purpose:**
- Prevents duplicate charges if user refreshes payment page
- Cardnet rejects transactions with same idempotency_key
- Key valid for ~30 minutes

**Cardnet API:**
- URL: `https://ecommerce.cardnet.com.do/api/payment/idenpotency-keys`
- Method: GET
- Response format: `"ikey:{GUID}"`

**Legacy Code:** `PaymentService.cs` line 17-49

---

### Endpoint 2: Process Payment (Cardnet)
```http
POST /api/pagos/procesar
Authorization: Bearer {token}
Content-Type: application/json

{
  "userId": "test-user-001",
  "planId": 5,
  "tipoPlan": "Empleador",
  "cardNumber": "4111111111111111",
  "cardHolderName": "JUAN PEREZ",
  "expirationMonth": 12,
  "expirationYear": 2026,
  "cvv": "123",
  "amount": 5000.00,
  "idempotencyKey": "a1b2c3d4-e5f6-7890-abcd-ef1234567890"
}

Response 200 OK (Approved):
{
  "ventaId": 123,
  "message": "Pago procesado exitosamente",
  "transactionId": "CARD123456789",
  "authorizationCode": "OK1234",
  "responseCode": "00"
}

Response 400 Bad Request (Declined):
{
  "message": "Pago rechazado: Fondos insuficientes",
  "responseCode": "51",
  "tipo": "pago_rechazado"
}

Response 500 Internal Server Error (Gateway Error):
{
  "message": "Error al comunicarse con el gateway de pago. Por favor intente nuevamente.",
  "tipo": "error_gateway"
}
```

**Cardnet Response Codes:**
- `"00"` = Approved
- `"05"` = Do not honor (generic decline)
- `"14"` = Invalid card number
- `"51"` = Insufficient funds
- `"54"` = Expired card
- `"55"` = Incorrect CVV
- `"91"` = Issuer unavailable
- `"96"` = System malfunction

**Processing Flow:**
1. Validate card data (Luhn algorithm, CVV format, expiration date)
2. Call Cardnet API with encrypted card data
3. Create `Ventas` record with response details
4. If approved (`"00"`): Create/renew `Suscripciones` record
5. If declined: Return error with response code
6. If gateway error: Return 500 with retry message

**Legacy Code:** `PaymentService.cs` -> `procesarPago()`

---

### Endpoint 3: Process Free Subscription
```http
POST /api/pagos/sin-pago
Authorization: Bearer {token}
Content-Type: application/json

{
  "userId": "test-user-001",
  "planId": 1,
  "tipoPlan": "Empleador"
}

Response 200 OK:
{
  "ventaId": 124,
  "message": "Suscripción gratuita procesada exitosamente"
}

Response 400 Bad Request:
{
  "message": "El plan seleccionado no es gratuito"
}
```

**Use Cases:**
- Free trial plans (Precio = 0)
- Promotional campaigns
- Manual activations by support team
- Testing environments

**Validation:**
- Plan must have `Precio = 0`
- Creates `Ventas` record with `MetodoPago = "Sin Pago"`
- Automatically creates/renews subscription

**Legacy Code:** `SuscripcionesService.cs` -> `guardarSuscripcion()` (precio = 0)

---

### Endpoint 4: Get Transaction History
```http
GET /api/pagos/historial/{userId}?pageNumber=1&pageSize=10
Authorization: Bearer {token}

Response 200 OK:
{
  "transactions": [
    {
      "ventaId": 123,
      "fecha": "2025-11-10T15:30:00Z",
      "planId": 5,
      "nombrePlan": "Plan Premium Empleador",
      "monto": 5000.00,
      "metodoPago": "Tarjeta de Crédito",
      "estado": "Aprobado",
      "transactionId": "CARD123456789",
      "authorizationCode": "OK1234",
      "responseCode": "00"
    },
    {
      "ventaId": 122,
      "fecha": "2025-10-10T12:00:00Z",
      "planId": 3,
      "nombrePlan": "Plan Básico Empleador",
      "monto": 2500.00,
      "metodoPago": "Tarjeta de Crédito",
      "estado": "Rechazado",
      "responseCode": "51",
      "comentario": "Fondos insuficientes"
    }
  ],
  "totalCount": 45,
  "pageNumber": 1,
  "pageSize": 10,
  "totalPages": 5
}
```

**Features:**
- Pagination (default: 10 per page, max: 100)
- Returns ALL transactions (approved, declined, errors)
- Ordered by date descending (most recent first)
- Filters available: date range, status, payment method

**NEW FUNCTIONALITY** - Does not exist in Legacy

---

## 🧪 Test Suites Breakdown

### Suite 1: Idempotency Key Tests (3 tests)

**Purpose:** Validate idempotency key generation mechanism

#### Test 1.1: GetIdempotencyKey_WithoutAuth_ReturnsUnauthorized
```csharp
[Fact]
public async Task GetIdempotencyKey_WithoutAuth_ReturnsUnauthorized()
{
    // Act
    var response = await Client.WithoutAuth().GetAsync("/api/pagos/idempotency");

    // Assert
    response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
}
```

#### Test 1.2: GetIdempotencyKey_ReturnsUniqueKey
```csharp
[Fact]
public async Task GetIdempotencyKey_ReturnsUniqueKey()
{
    // Arrange
    var (userId, _, _, _) = await CreateEmpleadorAsync();

    // Act
    var response = await Client.AsEmpleador(userId)
        .GetAsync("/api/pagos/idempotency");

    // Assert
    response.StatusCode.Should().Be(HttpStatusCode.OK);
    
    var result = await response.Content.ReadFromJsonAsync<JsonElement>();
    result.TryGetProperty("idempotencyKey", out var key).Should().BeTrue();
    
    var keyString = key.GetString();
    keyString.Should().NotBeNullOrEmpty();
    Guid.TryParse(keyString, out _).Should().BeTrue(); // Valid GUID
}
```

#### Test 1.3: GetIdempotencyKey_MultipleCalls_ReturnsDifferentKeys
```csharp
[Fact]
public async Task GetIdempotencyKey_MultipleCalls_ReturnsDifferentKeys()
{
    // Arrange
    var (userId, _, _, _) = await CreateEmpleadorAsync();
    var client = Client.AsEmpleador(userId);

    // Act - Call twice
    var response1 = await client.GetAsync("/api/pagos/idempotency");
    var response2 = await client.GetAsync("/api/pagos/idempotency");

    // Assert
    var result1 = await response1.Content.ReadFromJsonAsync<JsonElement>();
    var result2 = await response2.Content.ReadFromJsonAsync<JsonElement>();
    
    var key1 = result1.GetProperty("idempotencyKey").GetString();
    var key2 = result2.GetProperty("idempotencyKey").GetString();
    
    key1.Should().NotBe(key2); // Different keys
}
```

---

### Suite 2: Process Payment Tests (20 tests)

**Purpose:** Exhaustive Cardnet payment gateway integration testing

#### Test 2.1: ProcesarPago_WithoutAuth_ReturnsUnauthorized
```csharp
[Fact]
public async Task ProcesarPago_WithoutAuth_ReturnsUnauthorized()
{
    var command = new
    {
        userId = "test-user",
        planId = 5,
        tipoPlan = "Empleador",
        cardNumber = "4111111111111111",
        cardHolderName = "JUAN PEREZ",
        expirationMonth = 12,
        expirationYear = 2026,
        cvv = "123",
        amount = 5000.00m
    };

    var response = await Client.WithoutAuth()
        .PostAsJsonAsync("/api/pagos/procesar", command);

    response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
}
```

#### Test 2.2: ProcesarPago_WithValidCard_ApprovedTransaction
```csharp
[Fact]
public async Task ProcesarPago_WithValidCard_ApprovedTransaction()
{
    // Arrange
    var (userId, _, _, _) = await CreateEmpleadorAsync();
    
    // Get idempotency key
    var idempResponse = await Client.AsEmpleador(userId)
        .GetAsync("/api/pagos/idempotency");
    var idempResult = await idempResponse.Content.ReadFromJsonAsync<JsonElement>();
    var idempotencyKey = idempResult.GetProperty("idempotencyKey").GetString();

    var command = new
    {
        userId,
        planId = 5, // Plan Premium Empleador
        tipoPlan = "Empleador",
        cardNumber = "4111111111111111", // Visa test card (always approved)
        cardHolderName = "JUAN PEREZ",
        expirationMonth = 12,
        expirationYear = 2026,
        cvv = "123",
        amount = 5000.00m,
        idempotencyKey
    };

    // Act
    var response = await Client.AsEmpleador(userId)
        .PostAsJsonAsync("/api/pagos/procesar", command);

    // Assert
    response.StatusCode.Should().Be(HttpStatusCode.OK);
    
    var result = await response.Content.ReadFromJsonAsync<JsonElement>();
    result.TryGetProperty("ventaId", out var ventaId).Should().BeTrue();
    ventaId.GetInt32().Should().BeGreaterThan(0);
    
    result.TryGetProperty("responseCode", out var responseCode).Should().BeTrue();
    responseCode.GetString().Should().Be("00"); // Approved
}
```

#### Test 2.3: ProcesarPago_WithInsufficientFunds_DeclinedTransaction
```csharp
[Fact]
public async Task ProcesarPago_WithInsufficientFunds_DeclinedTransaction()
{
    // Use Cardnet test card for insufficient funds: "51"
    // Test card: 4000000000000002 (simulates declined)
    
    // Assert
    response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    
    var result = await response.Content.ReadFromJsonAsync<JsonElement>();
    result.TryGetProperty("responseCode", out var code).Should().BeTrue();
    code.GetString().Should().Be("51"); // Insufficient funds
    
    result.TryGetProperty("tipo", out var tipo).Should().BeTrue();
    tipo.GetString().Should().Be("pago_rechazado");
}
```

#### Test 2.4: ProcesarPago_WithInvalidCardNumber_ReturnsBadRequest
```csharp
[Fact]
public async Task ProcesarPago_WithInvalidCardNumber_ReturnsBadRequest()
{
    // Invalid card: fails Luhn algorithm
    // Card: "1234567890123456"
    
    // Should fail BEFORE calling Cardnet
    response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    
    var result = await response.Content.ReadFromJsonAsync<JsonElement>();
    result.TryGetProperty("message", out var message).Should().BeTrue();
    message.GetString().Should().Contain("número de tarjeta inválido");
}
```

#### Test 2.5: ProcesarPago_WithExpiredCard_DeclinedTransaction
```csharp
[Fact]
public async Task ProcesarPago_WithExpiredCard_DeclinedTransaction()
{
    // Expired card: expirationMonth = 1, expirationYear = 2023
    
    // Should detect expiration BEFORE calling Cardnet
    response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    
    var result = await response.Content.ReadFromJsonAsync<JsonElement>();
    result.GetProperty("message").GetString().Should().Contain("expirada");
}
```

#### Test 2.6: ProcesarPago_WithInvalidCVV_ReturnsBadRequest
```csharp
[Fact]
public async Task ProcesarPago_WithInvalidCVV_ReturnsBadRequest()
{
    // Invalid CVV: "12" (2 digits instead of 3)
    
    response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    
    var result = await response.Content.ReadFromJsonAsync<JsonElement>();
    result.GetProperty("message").GetString().Should().Contain("CVV");
}
```

#### Test 2.7: ProcesarPago_WithSameIdempotencyKey_PreventsDuplicate
```csharp
[Fact]
public async Task ProcesarPago_WithSameIdempotencyKey_PreventsDuplicate()
{
    // Arrange - Process payment once
    var response1 = await ProcessPayment(idempotencyKey);
    response1.StatusCode.Should().Be(HttpStatusCode.OK);

    // Act - Retry with SAME idempotency key
    var response2 = await ProcessPayment(idempotencyKey); // Same key!

    // Assert - Cardnet should reject duplicate
    response2.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    
    var result = await response2.Content.ReadFromJsonAsync<JsonElement>();
    result.GetProperty("message").GetString()
        .Should().Contain("transacción duplicada");
}
```

#### Test 2.8: ProcesarPago_ApprovedTransaction_CreatesSuscripcion
```csharp
[Fact]
public async Task ProcesarPago_ApprovedTransaction_CreatesSuscripcion()
{
    // Arrange & Act - Process approved payment
    var paymentResponse = await ProcessApprovedPayment(userId, planId: 5);
    
    // Assert - Verify subscription was created
    var subsResponse = await Client.AsEmpleador(userId)
        .GetAsync($"/api/suscripciones/activa/{userId}");
    
    subsResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    
    var subs = await subsResponse.Content.ReadFromJsonAsync<JsonElement>();
    subs.GetProperty("planId").GetInt32().Should().Be(5);
    subs.GetProperty("activa").GetBoolean().Should().BeTrue();
}
```

#### Test 2.9: ProcesarPago_DeclinedTransaction_DoesNotCreateSuscripcion
```csharp
[Fact]
public async Task ProcesarPago_DeclinedTransaction_DoesNotCreateSuscripcion()
{
    // Arrange & Act - Process declined payment
    var paymentResponse = await ProcessDeclinedPayment(userId);
    
    paymentResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    
    // Assert - Verify subscription was NOT created
    var subsResponse = await Client.AsEmpleador(userId)
        .GetAsync($"/api/suscripciones/activa/{userId}");
    
    subsResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
}
```

#### Test 2.10: ProcesarPago_CardnetTimeout_ReturnsGatewayError
```csharp
[Fact]
public async Task ProcesarPago_CardnetTimeout_ReturnsGatewayError()
{
    // Simulate Cardnet timeout (mock CardnetPaymentService to throw timeout)
    
    response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
    
    var result = await response.Content.ReadFromJsonAsync<JsonElement>();
    result.GetProperty("tipo").GetString().Should().Be("error_gateway");
    result.GetProperty("message").GetString()
        .Should().Contain("Por favor intente nuevamente");
}
```

**Additional tests (2.11 - 2.20):**
- WithZeroAmount_ReturnsBadRequest
- WithNegativeAmount_ReturnsBadRequest
- WithInvalidPlanId_ReturnsNotFound
- WithEmptyCardNumber_ReturnsBadRequest
- WithEmptyCardHolderName_ReturnsBadRequest
- WithFutureExpirationDate_ProcessesSuccessfully
- CreatesVentaRecord_WithAllTransactionDetails
- UpdatesExistingSuscripcion_IfAlreadyExists
- LogsTransactionDetails_ForAuditing
- RateLimiting_EnforcedAt10RequestsPerMinute

---

### Suite 3: Free Subscription Tests (8 tests)

**Purpose:** Validate zero-cost subscription processing

Tests:
- ProcesarSinPago_WithoutAuth_ReturnsUnauthorized
- ProcesarSinPago_WithFreePlan_CreatesSuccessfully
- ProcesarSinPago_WithPaidPlan_ReturnsBadRequest
- ProcesarSinPago_CreatesVentaRecord_WithSinPagoMethod
- ProcesarSinPago_CreatesSuscripcion_Automatically
- ProcesarSinPago_WithInvalidUserId_ReturnsNotFound
- ProcesarSinPago_WithInvalidPlanId_ReturnsNotFound
- ProcesarSinPago_RenewsExistingSuscripcion_IfExists

---

### Suite 4: Transaction History Tests (8 tests)

**Purpose:** Validate pagination, filtering, and sorting of transaction history

Tests:
- GetHistorial_WithoutAuth_ReturnsUnauthorized
- GetHistorial_WithValidUserId_ReturnsPaginatedList
- GetHistorial_IncludesApprovedAndDeclined_Transactions
- GetHistorial_OrderedByDate_Descending
- GetHistorial_WithPageSize_RespectsLimit
- GetHistorial_WithPageNumber_ReturnsCorrectPage
- GetHistorial_ExceedsMaxPageSize_ClampsTo100
- GetHistorial_EmptyHistory_ReturnsEmptyList

---

### Suite 5: Cardnet Webhook Tests (7 tests)

**Purpose:** Validate async payment notifications from Cardnet

Tests:
- CardnetWebhook_WithValidSignature_ProcessesNotification
- CardnetWebhook_WithInvalidSignature_ReturnsUnauthorized
- CardnetWebhook_UpdatesVentaStatus_BasedOnNotification
- CardnetWebhook_DuplicateNotification_IdempotentProcessing
- CardnetWebhook_InvalidPayload_ReturnsBadRequest
- CardnetWebhook_TransactionApproved_ActivatesSuscripcion
- CardnetWebhook_TransactionDeclined_RollsBackSuscripcion

---

## 🛠️ Test Infrastructure Requirements

### 1. Mock CardnetPaymentService
```csharp
public class MockCardnetPaymentService : ICardnetPaymentService
{
    public bool SimulateTimeout { get; set; }
    public bool SimulateDecline { get; set; }
    public string ResponseCode { get; set; } = "00"; // Default: Approved

    public async Task<CardnetResponse> ProcessPaymentAsync(CardnetRequest request)
    {
        if (SimulateTimeout)
            throw new TimeoutException("Cardnet timeout");

        if (SimulateDecline)
            return new CardnetResponse
            {
                ResponseCode = "51", // Insufficient funds
                Message = "Fondos insuficientes",
                IsApproved = false
            };

        // Simulate approved transaction
        return new CardnetResponse
        {
            ResponseCode = "00",
            Message = "Aprobado",
            TransactionId = $"CARD{Guid.NewGuid():N}",
            AuthorizationCode = $"OK{Random.Shared.Next(1000, 9999)}",
            IsApproved = true
        };
    }
}
```

### 2. Test Card Numbers (Cardnet Test Environment)
```csharp
public static class TestCards
{
    // Approved cards
    public const string VisaApproved = "4111111111111111";
    public const string MastercardApproved = "5500000000000004";

    // Declined cards
    public const string InsufficientFunds = "4000000000000002"; // Code: 51
    public const string ExpiredCard = "4000000000000069"; // Code: 54
    public const string InvalidCard = "4000000000000127"; // Code: 14
    public const string DoNotHonor = "4000000000000036"; // Code: 05
}
```

### 3. Helper Methods
```csharp
private async Task<HttpResponseMessage> ProcessApprovedPayment(
    string userId, 
    int planId, 
    decimal amount = 5000m)
{
    // Get idempotency key
    var idempResponse = await Client.AsEmpleador(userId)
        .GetAsync("/api/pagos/idempotency");
    var idempKey = ...;

    var command = new
    {
        userId,
        planId,
        tipoPlan = "Empleador",
        cardNumber = TestCards.VisaApproved,
        cardHolderName = "JUAN PEREZ",
        expirationMonth = 12,
        expirationYear = 2026,
        cvv = "123",
        amount,
        idempotencyKey = idempKey
    };

    return await Client.AsEmpleador(userId)
        .PostAsJsonAsync("/api/pagos/procesar", command);
}
```

---

## 📊 Success Criteria

✅ All 46 tests passing (100%)  
✅ Zero flaky tests  
✅ Cardnet integration validated with test cards  
✅ Idempotency mechanism verified  
✅ Error handling comprehensive (all Cardnet codes tested)  
✅ Subscription lifecycle validated (create, renew, rollback)  
✅ Transaction history working (pagination, filtering)  
✅ Webhook processing validated (signature, idempotency)  
✅ Rate limiting enforced  
✅ Execution time < 25s per batch  

---

## 🎯 Implementation Order

1. **Suite 1: Idempotency (3 tests)** - Foundation for all payment tests
2. **Suite 3: Free Subscriptions (8 tests)** - Simpler flow, no Cardnet
3. **Suite 2: Cardnet Payments (20 tests)** - Core payment processing
4. **Suite 4: Transaction History (8 tests)** - Query/reporting functionality
5. **Suite 5: Webhooks (7 tests)** - Advanced async notifications

**Estimated Time:** 3-4 hours total  
**Next Session:** Start with Suite 1 (Idempotency tests)

---

**Plan Created:** November 10, 2025  
**Ready to implement:** Yes ✅  
**Dependencies:** TestWebApplicationFactory, MockCardnetPaymentService  
