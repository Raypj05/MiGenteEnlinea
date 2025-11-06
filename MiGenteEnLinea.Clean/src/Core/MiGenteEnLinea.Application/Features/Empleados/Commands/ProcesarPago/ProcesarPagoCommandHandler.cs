using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MiGenteEnLinea.Application.Common.Exceptions;
using MiGenteEnLinea.Application.Common.Interfaces;
using MiGenteEnLinea.Domain.Entities.Empleados;
using MiGenteEnLinea.Domain.Entities.Nominas;

namespace MiGenteEnLinea.Application.Features.Empleados.Commands.ProcesarPago;

/// <summary>
/// Handler para procesar pago de nómina.
/// ⚠️ CRÍTICO: Mantiene patrón Legacy de 2 SaveChangesAsync() separados.
/// </summary>
public class ProcesarPagoCommandHandler : IRequestHandler<ProcesarPagoCommand, int>
{
    private readonly IApplicationDbContext _context;
    private readonly INominaCalculatorService _nominaCalculator;
    private readonly ILogger<ProcesarPagoCommandHandler> _logger;

    public ProcesarPagoCommandHandler(
        IApplicationDbContext context,
        INominaCalculatorService nominaCalculator,
        ILogger<ProcesarPagoCommandHandler> logger)
    {
        _context = context;
        _nominaCalculator = nominaCalculator;
        _logger = logger;
    }

    public async Task<int> Handle(ProcesarPagoCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Procesando pago para EmpleadoId={EmpleadoId}, Fecha={FechaPago}, Tipo={TipoConcepto}",
            request.EmpleadoId, request.FechaPago, request.TipoConcepto);

        // PASO 1: Validar que empleado existe y pertenece al empleador
        var empleado = await _context.Empleados
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.EmpleadoId == request.EmpleadoId && 
                                     e.UserId == request.UserId, 
                                     cancellationToken)
            ?? throw new NotFoundException(nameof(Empleado), request.EmpleadoId);

        // PASO 2: Validar que empleado esté activo
        if (!empleado.Activo)
        {
            throw new ValidationException(
                $"No se puede procesar pago para empleado inactivo (EmpleadoId={request.EmpleadoId})");
        }

        // PASO 3: Calcular nómina usando el servicio de cálculos
        var calculoNomina = await _nominaCalculator.CalcularNominaAsync(
            request.EmpleadoId,
            request.FechaPago,
            request.TipoConcepto,
            request.EsFraccion,
            request.AplicarTss,
            cancellationToken);

        _logger.LogInformation(
            "Nómina calculada: Percepciones={Percepciones:C}, Deducciones={Deducciones:C}, Neto={Neto:C}",
            calculoNomina.TotalPercepciones,
            calculoNomina.TotalDeducciones,
            calculoNomina.NetoPagar);

        // PASO 4: Crear header del recibo usando API correcta de ReciboHeader.Create()
        // ✅ API verificada: Create(userId, empleadoId, conceptoPago, tipo, periodoInicio?, periodoFin?)
        var conceptoPago = request.EsFraccion 
            ? $"Fracción {request.TipoConcepto} - {request.FechaPago:yyyy-MM-dd}"
            : $"{request.TipoConcepto} - {request.FechaPago:yyyy-MM-dd}";

        var header = ReciboHeader.CreateWithOptions(
            userId: request.UserId,
            empleadoId: request.EmpleadoId,
            conceptoPago: conceptoPago,
            tipo: 1, // 1=Regular (nómina estándar)
            periodoInicio: null, // Para nómina regular no se usa periodo
            periodoFin: null);

        // ⚠️ CRÍTICO: SaveChanges #1 - Guardar header primero para generar PagoId
        // Legacy: EmpleadosService.procesarPago() líneas 147-151
        // Razón: Se necesita PagoId auto-generado ANTES de insertar detalles
        await _context.RecibosHeader.AddAsync(header, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken); // ← SaveChanges #1

        _logger.LogInformation(
            "Recibo header guardado: PagoId={PagoId} (auto-generado)",
            header.PagoId);

        // PASO 5: Agregar percepciones y deducciones usando métodos del Aggregate Root
        // ✅ API verificada: ReciboHeader.AgregarIngreso(concepto, monto) y AgregarDeduccion(concepto, monto)
        // ReciboHeader es Aggregate Root que gestiona su colección de detalles
        // Legacy: EmpleadosService.procesarPago() líneas 153-161

        // Percepciones (salario, extras)
        foreach (var percepcion in calculoNomina.Percepciones)
        {
            header.AgregarIngreso(percepcion.Descripcion, percepcion.Monto);
        }

        // Deducciones (TSS - valores negativos)
        foreach (var deduccion in calculoNomina.Deducciones)
        {
            header.AgregarDeduccion(deduccion.Descripcion, Math.Abs(deduccion.Monto)); // AgregarDeduccion espera valor positivo
        }

        // ⚠️ CRÍTICO: SaveChanges #2 - Guardar detalles con PagoId ya asignado
        // Los detalles fueron agregados al Aggregate Root y se guardarán por EF tracking
        await _context.SaveChangesAsync(cancellationToken); // ← SaveChanges #2

        _logger.LogInformation(
            "Recibo completado: PagoId={PagoId}, Percepciones={CountPercepciones}, Deducciones={CountDeducciones}",
            header.PagoId,
            calculoNomina.Percepciones.Count,
            calculoNomina.Deducciones.Count);

        // PASO 6: Retornar PagoId generado
        return header.PagoId;
    }
}
