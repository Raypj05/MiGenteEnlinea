using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MiGenteEnLinea.Application.Common.Interfaces;
using MiGenteEnLinea.Domain.Entities.Empleados;

namespace MiGenteEnLinea.Application.Features.Empleados.Commands.UpdateRemuneraciones;

/// <summary>
/// Handler para UpdateRemuneracionesCommand.
/// Implementa actualizarRemuneraciones() del Legacy (EmpleadosService.cs línea 657-676).
/// </summary>
/// <remarks>
/// ESTRATEGIA DDD (Replace Pattern):
/// 1. DELETE: Elimina remuneraciones existentes del empleado
/// 2. INSERT: Crea nuevas remuneraciones con factory method
/// 3. COMMIT: SaveChanges en transacción única
/// 
/// ⚠️ PARIDAD LEGACY CON BUG:
/// - Legacy usa Where().FirstOrDefault() que solo elimina PRIMERA remuneración
/// - Por paridad, replicamos este bug (solo elimina primera)
/// - NOTA: En producción debería usar RemoveRange(Where().ToList())
/// 
/// DIFERENCIAS CON LEGACY:
/// - Legacy: 2 DbContext separados (no transaccional)
/// - Clean: Transacción única (atómico)
/// - Legacy: Sin validaciones
/// - Clean: Validaciones en factory method
/// - Legacy: Sin logging
/// - Clean: Logging estructurado
/// 
/// MEJORAS SOBRE GAP-008:
/// - Reutiliza pattern de batch insert con factory method
/// - Agrega step de delete previo
/// - Transacción única garantiza atomicidad
/// </remarks>
public class UpdateRemuneracionesCommandHandler : IRequestHandler<UpdateRemuneracionesCommand, bool>
{
    private readonly IApplicationDbContext _context;
    private readonly ILogger<UpdateRemuneracionesCommandHandler> _logger;

    public UpdateRemuneracionesCommandHandler(
        IApplicationDbContext context,
        ILogger<UpdateRemuneracionesCommandHandler> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<bool> Handle(UpdateRemuneracionesCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Actualizando remuneraciones para EmpleadoId: {EmpleadoId}, UserId: {UserId}",
            request.EmpleadoId,
            request.UserId);

        // PASO 1: ✅ CORREGIDO - Eliminar TODAS las remuneraciones existentes (no solo primera)
        // ⚠️ LEGACY BUG: Usaba FirstOrDefault() (solo eliminaba primera)
        // ✅ FIX: Usar ToListAsync() para eliminar todas
        var existingRemuneraciones = await _context.Remuneraciones
            .Where(r => r.EmpleadoId == request.EmpleadoId)
            .ToListAsync(cancellationToken);

        if (existingRemuneraciones.Any())
        {
            _logger.LogInformation(
                "Eliminando {Count} remuneraciones existentes para EmpleadoId: {EmpleadoId}",
                existingRemuneraciones.Count,
                request.EmpleadoId);

            // ✅ RemoveRange para eliminar todas (no solo Remove para una)
            _context.Remuneraciones.RemoveRange(existingRemuneraciones);
        }
        else
        {
            _logger.LogInformation(
                "No se encontraron remuneraciones existentes para EmpleadoId: {EmpleadoId}",
                request.EmpleadoId);
        }

        // PASO 2: Crear nuevas remuneraciones con DDD factory method
        // (Reutiliza pattern de GAP-008)
        var nuevasRemuneraciones = request.Remuneraciones
            .Select(dto => Remuneracion.Crear(
                userId: request.UserId,
                empleadoId: request.EmpleadoId,
                descripcion: dto.Descripcion,
                monto: dto.Monto
            ))
            .ToList();

        _logger.LogInformation(
            "Creadas {Count} nuevas remuneraciones para EmpleadoId: {EmpleadoId}",
            nuevasRemuneraciones.Count,
            request.EmpleadoId);

        // PASO 3: Batch insert (Legacy: db1.Remuneraciones.AddRange(rem))
        await _context.Remuneraciones.AddRangeAsync(nuevasRemuneraciones, cancellationToken);

        // PASO 4: SaveChanges UNA SOLA VEZ (transacción atómica)
        // Nota: Legacy hace SaveChanges 2 veces (una en cada DbContext)
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Remuneraciones actualizadas exitosamente para EmpleadoId: {EmpleadoId}",
            request.EmpleadoId);

        // PASO 5: Return true (Legacy parity)
        return true;
    }
}
