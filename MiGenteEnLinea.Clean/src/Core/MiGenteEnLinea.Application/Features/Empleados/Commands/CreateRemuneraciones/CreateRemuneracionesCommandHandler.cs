using MediatR;
using Microsoft.Extensions.Logging;
using MiGenteEnLinea.Application.Common.Interfaces;

namespace MiGenteEnLinea.Application.Features.Empleados.Commands.CreateRemuneraciones;

/// <summary>
/// Handler para CreateRemuneracionesCommand.
/// Implementa guardarOtrasRemuneraciones() del Legacy (EmpleadosService.cs línea 646-654).
/// </summary>
/// <remarks>
/// ESTRATEGIA: Usa ILegacyDataService para acceder a tabla Remuneraciones (entidad legacy no migrada a DDD)
/// - Batch insert con mejor performance
/// - Logging estructurado
/// - Valida datos en DTO validator
/// 
/// DIFERENCIAS CON LEGACY:
/// - Legacy: Sin validaciones (asume datos válidos)
/// - Clean: Validaciones en FluentValidation
/// - Legacy: No logging
/// - Clean: Logging estructurado en cada paso
/// 
/// PARIDAD LEGACY:
/// - Siempre retorna true (mismo comportamiento)
/// - Batch insert (AddRange + SaveChanges)
/// </remarks>
public class CreateRemuneracionesCommandHandler : IRequestHandler<CreateRemuneracionesCommand, bool>
{
    private readonly ILegacyDataService _legacyDataService;
    private readonly ILogger<CreateRemuneracionesCommandHandler> _logger;

    public CreateRemuneracionesCommandHandler(
        ILegacyDataService legacyDataService,
        ILogger<CreateRemuneracionesCommandHandler> logger)
    {
        _legacyDataService = legacyDataService;
        _logger = logger;
    }

    public async Task<bool> Handle(CreateRemuneracionesCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Creando {Count} remuneraciones para EmpleadoId: {EmpleadoId}, UserId: {UserId}",
            request.Remuneraciones.Count,
            request.EmpleadoId,
            request.UserId);

        // Delegar a LegacyDataService que maneja la entidad legacy Remuneracione
        await _legacyDataService.CreateRemuneracionesAsync(
            request.UserId,
            request.EmpleadoId,
            request.Remuneraciones,
            cancellationToken);

        _logger.LogInformation(
            "Remuneraciones guardadas exitosamente para EmpleadoId: {EmpleadoId}",
            request.EmpleadoId);

        return true;
    }
}
