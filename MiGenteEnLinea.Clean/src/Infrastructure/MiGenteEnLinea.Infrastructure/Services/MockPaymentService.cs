using Microsoft.Extensions.Logging;
using MiGenteEnLinea.Application.Common.Interfaces;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace MiGenteEnLinea.Infrastructure.Services;

/// <summary>
/// ⚠️ MOCK TEMPORAL: Implementación temporal de IPaymentService para permitir startup de la API.
/// TODO: Reemplazar con CardnetPaymentService completo en Fase 5 del plan de migración.
/// </summary>
public class MockPaymentService : IPaymentService
{
    private readonly ILogger<MockPaymentService> _logger;

    public MockPaymentService(ILogger<MockPaymentService> logger)
    {
        _logger = logger;
        _logger.LogWarning("⚠️ USANDO MOCK PAYMENT SERVICE - No procesa pagos reales");
    }

    public Task<string> GenerateIdempotencyKeyAsync(CancellationToken ct = default)
    {
        // ✅ Cardnet format: "ikey:{GUID}"
        var idempotencyKey = $"ikey:{Guid.NewGuid()}";
        _logger.LogInformation("Mock: Generando idempotency key: {Key}", idempotencyKey);
        return Task.FromResult(idempotencyKey);
    }

    public Task<PaymentResult> ProcessPaymentAsync(PaymentRequest request, CancellationToken ct = default)
    {
        _logger.LogWarning(
            "Mock: Simulando procesamiento de pago. Monto: {Amount}, Referencia: {Reference}",
            request.Amount,
            request.ReferenceNumber);

        // Simular respuesta exitosa
        var result = new PaymentResult
        {
            Success = true,
            ResponseCode = "00",
            ResponseDescription = "MOCK: Transacción aprobada (simulación)",
            ApprovalCode = "MOCK-" + Random.Shared.Next(100000, 999999),
            TransactionReference = "MOCK-TXN-" + Guid.NewGuid().ToString()[..8],
            IdempotencyKey = Guid.NewGuid().ToString()
        };

        return Task.FromResult(result);
    }

    public Task<PaymentGatewayConfig> GetConfigurationAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("Mock: Retornando configuración mock del gateway");

        var config = new PaymentGatewayConfig
        {
            MerchantId = "MOCK-MERCHANT-123",
            TerminalId = "MOCK-TERM-456",
            BaseUrl = "https://mock-gateway.test",
            IsTest = true
        };

        return Task.FromResult(config);
    }
}
