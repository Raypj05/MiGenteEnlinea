using MediatR;
using Microsoft.Extensions.Logging;
using MiGenteEnLinea.Domain.Entities.Empleados;
using MiGenteEnLinea.Domain.Entities.Nominas;
using MiGenteEnLinea.Domain.Interfaces.Repositories;

namespace MiGenteEnLinea.Application.Features.Nominas.Commands.ProcesarNominaLote;

/// <summary>
/// Handler para procesar nómina en lote (batch processing).
/// 
/// LÓGICA DE NEGOCIO:
/// 1. Valida que todos los empleados existan y pertenezcan al empleador
/// 2. Crea ReciboHeader + ReciboDetalle para cada empleado
/// 3. Calcula totales y deducciones automáticamente
/// 4. Opcionalmente genera PDFs y envía emails
/// 5. Registra errores individuales sin detener el proceso completo
/// </summary>
public class ProcesarNominaLoteCommandHandler : IRequestHandler<ProcesarNominaLoteCommand, ProcesarNominaLoteResult>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ProcesarNominaLoteCommandHandler> _logger;

    public ProcesarNominaLoteCommandHandler(
        IUnitOfWork unitOfWork,
        ILogger<ProcesarNominaLoteCommandHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<ProcesarNominaLoteResult> Handle(
        ProcesarNominaLoteCommand request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Procesando nómina en lote - Empleador: {EmpleadorId}, Período: {Periodo}, Empleados: {Count}",
            request.EmpleadorId,
            request.Periodo,
            request.Empleados.Count);

        var result = new ProcesarNominaLoteResult
        {
            ReciboIds = new List<int>(),
            Errores = new List<string>()
        };

        int recibosCreados = 0;
        int empleadosProcesados = 0;
        decimal totalPagado = 0;
        decimal totalDeducciones = 0;
        var reciboIds = new List<int>();
        var errores = new List<string>();

        // Validar que empleador existe
        var empleador = await _unitOfWork.Empleadores.GetByIdAsync(request.EmpleadorId);
        if (empleador == null)
        {
            errores.Add($"Empleador {request.EmpleadorId} no encontrado");
            return new ProcesarNominaLoteResult
            {
                RecibosCreados = 0,
                EmpleadosProcesados = 0,
                TotalPagado = 0,
                TotalDeducciones = 0,
                ReciboIds = new List<int>(),
                Errores = errores
            };
        }

        // Procesar cada empleado individualmente
        foreach (var empleadoItem in request.Empleados)
        {
            try
            {
                // Validar que empleado existe y pertenece al empleador
                var empleado = await _unitOfWork.Empleados.GetByIdAsync(empleadoItem.EmpleadoId);
                if (empleado == null)
                {
                    errores.Add($"Empleado {empleadoItem.EmpleadoId} no encontrado");
                    continue;
                }

                if (empleado.UserId != empleador.UserId)
                {
                    errores.Add($"Empleado {empleadoItem.EmpleadoId} no pertenece al empleador");
                    continue;
                }

                // Calcular totales para este empleado
                decimal ingresos = empleadoItem.Salario;
                decimal deducciones = 0;

                foreach (var concepto in empleadoItem.Conceptos)
                {
                    if (concepto.EsDeduccion)
                    {
                        deducciones += concepto.Monto;
                    }
                    else
                    {
                        ingresos += concepto.Monto;
                    }
                }

                decimal neto = ingresos - deducciones;

                // Crear ReciboHeader usando factory method con firmas correctas
                var reciboHeader = ReciboHeader.CreateWithOptions(
                    userId: empleador.UserId,
                    empleadoId: empleadoItem.EmpleadoId,
                    conceptoPago: $"Nómina {request.Periodo}",
                    tipo: 1, // Tipo 1 = Nómina Regular
                    periodoInicio: DateOnly.FromDateTime(request.FechaPago.AddDays(-14)), // Aproximado
                    periodoFin: DateOnly.FromDateTime(request.FechaPago)
                );

                // ✅ PASO 1: Guardar el header PRIMERO para obtener el PagoId
                await _unitOfWork.RecibosHeader.AddAsync(reciboHeader);
                await _unitOfWork.SaveChangesAsync(cancellationToken);

                // ✅ PASO 2: AHORA agregar ingresos y deducciones (con PagoId válido)
                // Salario base
                reciboHeader.AgregarIngreso("Salario Base", empleadoItem.Salario);

                // Conceptos adicionales
                foreach (var concepto in empleadoItem.Conceptos)
                {
                    if (concepto.EsDeduccion)
                    {
                        reciboHeader.AgregarDeduccion(concepto.Concepto, concepto.Monto);
                    }
                    else
                    {
                        reciboHeader.AgregarIngreso(concepto.Concepto, concepto.Monto);
                    }
                }

                // Recalcular totales
                reciboHeader.RecalcularTotales();

                // ✅ PASO 3: Guardar de nuevo para persistir detalles y totales actualizados
                await _unitOfWork.SaveChangesAsync(cancellationToken);

                // Actualizar contadores
                recibosCreados++;
                empleadosProcesados++;
                totalPagado += neto;
                totalDeducciones += deducciones;
                reciboIds.Add(reciboHeader.PagoId);

                _logger.LogInformation(
                    "Recibo creado - ID: {PagoId}, Empleado: {EmpleadoId}, Neto: {Monto}",
                    reciboHeader.PagoId,
                    empleadoItem.EmpleadoId,
                    neto);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error procesando empleado {EmpleadoId}",
                    empleadoItem.EmpleadoId);

                errores.Add($"Error procesando empleado {empleadoItem.EmpleadoId}: {ex.Message}");
            }
        }

        _logger.LogInformation(
            "Nómina lote procesada - Recibos: {Recibos}, Empleados: {Empleados}, Total: {Total}",
            recibosCreados,
            empleadosProcesados,
            totalPagado);

        return new ProcesarNominaLoteResult
        {
            RecibosCreados = recibosCreados,
            EmpleadosProcesados = empleadosProcesados,
            TotalPagado = totalPagado,
            TotalDeducciones = totalDeducciones,
            ReciboIds = reciboIds,
            Errores = errores
        };
    }
}
