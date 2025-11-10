using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MiGenteEnLinea.Application.Features.Nominas.Commands.EnviarRecibosEmailLote;
using MiGenteEnLinea.Application.Features.Nominas.Commands.ExportarNominaCsv;
using MiGenteEnLinea.Application.Features.Nominas.Commands.GenerarRecibosPdfLote;
using MiGenteEnLinea.Application.Features.Nominas.Commands.ProcesarNominaLote;
using MiGenteEnLinea.Application.Features.Nominas.Commands.ProcessContractPayment;
using MiGenteEnLinea.Application.Features.Nominas.DTOs;
using MiGenteEnLinea.Application.Features.Nominas.Queries.GetNominaResumen;
using System.Security.Claims;

namespace MiGenteEnLinea.API.Controllers;

/// <summary>
/// Controller para gestión avanzada de nómina.
/// 
/// FUNCIONALIDADES:
/// - Procesamiento de nómina en lote (batch processing)
/// - Generación masiva de recibos en PDF
/// - Resúmenes y estadísticas de nómina por período
/// - Exportación de datos a Excel
/// 
/// WORKFLOW TÍPICO:
/// 1. Empleador procesa nómina del período (POST /api/nominas/procesar-lote)
/// 2. Sistema genera recibos para todos los empleados
/// 3. Empleador genera PDFs (POST /api/nominas/generar-pdfs)
/// 4. Empleador consulta resumen (GET /api/nominas/resumen)
/// 5. Opcional: Exportar a Excel o enviar por email
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class NominasController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<NominasController> _logger;

    public NominasController(
        IMediator mediator,
        ILogger<NominasController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    /// <summary>
    /// Procesa nómina para múltiples empleados en lote.
    /// </summary>
    /// <param name="command">Datos del lote de nómina</param>
    /// <returns>Resultado del procesamiento con contadores y errores</returns>
    /// <response code="200">Nómina procesada (puede tener errores parciales)</response>
    /// <response code="400">Datos inválidos</response>
    /// <response code="401">No autenticado</response>
    /// <response code="404">Empleador no encontrado</response>
    /// <remarks>
    /// Ejemplo de request:
    /// 
    ///     POST /api/nominas/procesar-lote
    ///     {
    ///       "empleadorId": 1,
    ///       "periodo": "2025-01",
    ///       "fechaPago": "2025-01-15",
    ///       "empleados": [
    ///         {
    ///           "empleadoId": 101,
    ///           "salario": 25000.00,
    ///           "conceptos": [
    ///             { "concepto": "Bono Productividad", "monto": 5000, "esDeduccion": false },
    ///             { "concepto": "Préstamo", "monto": 2000, "esDeduccion": true }
    ///           ]
    ///         }
    ///       ],
    ///       "notas": "Nómina quincenal enero 2025"
    ///     }
    /// 
    /// Respuesta exitosa incluye:
    /// - recibosCreados: Cantidad de recibos generados exitosamente
    /// - empleadosProcesados: Cantidad de empleados procesados
    /// - totalPagado: Monto total neto pagado
    /// - reciboIds: Lista de IDs de recibos generados
    /// - errores: Lista de errores si algunos empleados fallaron
    /// </remarks>
    [HttpPost("procesar-lote")]
    [ProducesResponseType(typeof(ProcesarNominaLoteResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProcesarNominaLoteResult>> ProcesarLote(
        [FromBody] ProcesarNominaLoteCommand command)
    {
        _logger.LogInformation(
            "Processing payroll batch - Employer: {EmpleadorId}, Period: {Periodo}, Employees: {Count}",
            command.EmpleadorId,
            command.Periodo,
            command.Empleados.Count);

        try
        {
            var result = await _mediator.Send(command);

            if (result.Errores.Count > 0)
            {
                _logger.LogWarning(
                    "Payroll batch completed with errors - Success: {Success}, Failed: {Failed}",
                    result.EmpleadosProcesados,
                    result.Errores.Count);
            }

            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Employer not found");
            return NotFound(new { error = ex.Message });
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Validation error processing payroll");
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Genera PDFs de recibos de nómina en lote.
    /// </summary>
    /// <param name="command">Lista de IDs de recibos a generar</param>
    /// <returns>PDFs generados con metadata</returns>
    /// <response code="200">PDFs generados (puede tener errores parciales)</response>
    /// <response code="400">Datos inválidos</response>
    /// <response code="401">No autenticado</response>
    /// <remarks>
    /// Ejemplo de request:
    /// 
    ///     POST /api/nominas/generar-pdfs
    ///     {
    ///       "reciboIds": [1001, 1002, 1003, 1004],
    ///       "incluirDetalleCompleto": true
    ///     }
    /// 
    /// Respuesta incluye:
    /// - pdfsExitosos/pdfsFallidos: Contadores
    /// - pdfsGenerados: Array de objetos con:
    ///   * reciboId
    ///   * empleadoId, empleadoNombre
    ///   * pdfBytes (base64 encoded PDF)
    ///   * periodo, fechaGeneracion
    ///   * tamanioBytes
    /// - errores: Lista de errores si algunos PDFs fallaron
    /// 
    /// NOTA: Los PDFs se retornan como byte arrays. El cliente debe:
    /// 1. Convertir base64 a bytes
    /// 2. Guardar como archivo .pdf
    /// 3. O mostrar en visor PDF del navegador
    /// </remarks>
    [HttpPost("generar-pdfs")]
    [ProducesResponseType(typeof(GenerarRecibosPdfLoteResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<GenerarRecibosPdfLoteResult>> GenerarPdfs(
        [FromBody] GenerarRecibosPdfLoteCommand command)
    {
        _logger.LogInformation(
            "Generating PDFs batch - Receipts: {Count}",
            command.ReciboIds.Count);

        try
        {
            var result = await _mediator.Send(command);

            if (result.Errores.Count > 0)
            {
                _logger.LogWarning(
                    "PDF generation completed with errors - Success: {Success}, Failed: {Failed}",
                    result.PdfsExitosos,
                    result.PdfsFallidos);
            }

            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Validation error generating PDFs");
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Obtiene resumen de nómina por período.
    /// </summary>
    /// <param name="empleadorId">ID del empleador</param>
    /// <param name="periodo">Período (ej: "2025-01")</param>
    /// <param name="fechaInicio">Fecha inicio del período (alternativa)</param>
    /// <param name="fechaFin">Fecha fin del período (alternativa)</param>
    /// <param name="incluirDetalleEmpleados">Incluir detalle por empleado</param>
    /// <returns>Resumen con totales, deducciones, estadísticas</returns>
    /// <response code="200">Resumen generado exitosamente</response>
    /// <response code="400">Parámetros inválidos</response>
    /// <response code="401">No autenticado</response>
    /// <response code="404">Empleador no encontrado</response>
    /// <remarks>
    /// Ejemplos de uso:
    /// 
    ///     GET /api/nominas/resumen?empleadorId=1&amp;periodo=2025-01
    ///     GET /api/nominas/resumen?empleadorId=1&amp;fechaInicio=2025-01-01&amp;fechaFin=2025-01-31
    ///     GET /api/nominas/resumen?empleadorId=1&amp;periodo=2025-Q1&amp;incluirDetalleEmpleados=true
    /// 
    /// Respuesta incluye:
    /// - totalEmpleados: Cantidad de empleados con pagos en el período
    /// - totalSalarioBruto/totalDeducciones/totalSalarioNeto: Sumas totales
    /// - deduccionesPorTipo: Dictionary con breakdown (AFP, SFS, ISR, etc.)
    /// - recibosGenerados/recibosAnulados: Contadores
    /// - promedioSalarioBruto/promedioSalarioNeto: Métricas
    /// - detalleEmpleados: Array opcional con detalle por empleado
    /// </remarks>
    [HttpGet("resumen")]
    [ProducesResponseType(typeof(NominaResumenDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<NominaResumenDto>> GetResumen(
        [FromQuery] int empleadorId,
        [FromQuery] string? periodo = null,
        [FromQuery] DateTime? fechaInicio = null,
        [FromQuery] DateTime? fechaFin = null,
        [FromQuery] bool incluirDetalleEmpleados = true)
    {
        _logger.LogInformation(
            "Getting payroll summary - Employer: {EmpleadorId}, Period: {Periodo}",
            empleadorId,
            periodo ?? $"{fechaInicio:yyyy-MM-dd} to {fechaFin:yyyy-MM-dd}");

        var query = new GetNominaResumenQuery
        {
            EmpleadorId = empleadorId,
            Periodo = periodo ?? string.Empty,
            FechaInicio = fechaInicio,
            FechaFin = fechaFin,
            IncluirDetalleEmpleados = incluirDetalleEmpleados
        };

        try
        {
            var resumen = await _mediator.Send(query);
            return Ok(resumen);
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Employer not found");
            return NotFound(new { error = ex.Message });
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Validation error getting summary");
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Descarga un PDF específico de recibo.
    /// </summary>
    /// <param name="reciboId">ID del recibo</param>
    /// <returns>Archivo PDF para descarga</returns>
    /// <response code="200">PDF generado exitosamente</response>
    /// <response code="404">Recibo no encontrado</response>
    /// <remarks>
    /// Este endpoint retorna el PDF directamente como archivo descargable.
    /// El navegador abrirá el visor PDF o descargará el archivo.
    /// 
    /// Ejemplo:
    ///     GET /api/nominas/recibo/1001/pdf
    /// 
    /// Response headers:
    /// - Content-Type: application/pdf
    /// - Content-Disposition: attachment; filename="recibo-1001.pdf"
    /// </remarks>
    [HttpGet("recibo/{reciboId}/pdf")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DescargarReciboPdf(int reciboId)
    {
        _logger.LogInformation("Downloading PDF for receipt: {ReciboId}", reciboId);

        var command = new GenerarRecibosPdfLoteCommand
        {
            ReciboIds = new List<int> { reciboId },
            IncluirDetalleCompleto = true
        };

        try
        {
            var result = await _mediator.Send(command);

            if (result.PdfsGenerados.Count == 0 || result.Errores.Count > 0)
            {
                return NotFound(new { error = $"No se pudo generar el PDF para el recibo {reciboId}" });
            }

            var pdf = result.PdfsGenerados.First();
            return File(pdf.PdfBytes, "application/pdf", $"recibo-{reciboId}.pdf");
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Receipt not found");
            return NotFound(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Envía recibos de nómina por email en lote.
    /// </summary>
    /// <param name="command">Lista de recibos a enviar con configuración</param>
    /// <returns>Resultado del envío con contadores y errores</returns>
    /// <response code="200">Emails procesados (puede tener errores parciales)</response>
    /// <response code="400">Datos inválidos</response>
    /// <response code="401">No autenticado</response>
    /// <remarks>
    /// Ejemplo de request:
    /// 
    ///     POST /api/nominas/enviar-emails
    ///     {
    ///       "reciboIds": [1001, 1002, 1003],
    ///       "asunto": "Recibo de Pago - Enero 2025",
    ///       "mensajeAdicional": "Feliz año nuevo. Gracias por su dedicación.",
    ///       "incluirDetalleCompleto": true,
    ///       "copiarEmpleador": false
    ///     }
    /// 
    /// Respuesta incluye:
    /// - emailsEnviados/emailsFallidos: Contadores
    /// - recibosEnviados: Array con status por recibo
    ///   * reciboId, empleadoId, empleadoNombre, empleadoEmail
    ///   * enviado (true/false)
    ///   * fechaEnvio, errorMensaje
    ///   * tamanoPdf (bytes)
    /// - errores: Lista de errores detallados
    /// - totalBytesEnviados: Tamaño total de PDFs
    /// 
    /// FUNCIONALIDAD:
    /// - Genera PDF de cada recibo automáticamente
    /// - Envía email con PDF embebido como download link
    /// - HTML email con formato profesional
    /// - Fallback a texto plano
    /// - Continúa procesando aunque algunos fallen
    /// - Validaciones: email configurado en empleado, recibo existe
    /// 
    /// LÍMITES:
    /// - Máximo 100 recibos por lote (validador)
    /// - Para cantidades mayores, usar múltiples llamadas
    /// 
    /// NOTA TÉCNICA:
    /// - PDF se embebe en HTML como base64 data URI
    /// - En futuro: migrar a attachments nativos (SMTP)
    /// </remarks>
    [HttpPost("enviar-emails")]
    [ProducesResponseType(typeof(EnviarRecibosEmailLoteResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<EnviarRecibosEmailLoteResult>> EnviarEmails(
        [FromBody] EnviarRecibosEmailLoteCommand command)
    {
        _logger.LogInformation(
            "Sending batch emails - Receipts: {Count}",
            command.ReciboIds.Count);

        try
        {
            var result = await _mediator.Send(command);

            if (result.Errores.Count > 0)
            {
                _logger.LogWarning(
                    "Email batch completed with errors - Success: {Success}, Failed: {Failed}",
                    result.EmailsEnviados,
                    result.EmailsFallidos);
            }

            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Validation error sending emails");
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Obtiene estadísticas de salud del servicio de nómina.
    /// </summary>
    /// <returns>Información de estado y versión</returns>
    /// <response code="200">Estado del servicio</response>
    [HttpGet("health")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult Health()
    {
        return Ok(new
        {
            service = "Nominas API",
            status = "Healthy", // Changed from "healthy" to "Healthy" for consistency with DashboardController
            version = "2.0.0",
            timestamp = DateTime.UtcNow,
            features = new[]
            {
                "Batch Payroll Processing",
                "PDF Generation",
                "Payroll Summary",
                "Statistics & Reports",
                "Email Distribution",
                "CSV Export"
            }
        });
    }

    /// <summary>
    /// Exporta nómina de un período a CSV.
    /// </summary>
    /// <param name="periodo">Período en formato YYYY-MM (ej: 2025-01)</param>
    /// <param name="incluirAnulados">Incluir recibos anulados</param>
    /// <returns>Archivo CSV con nómina del período</returns>
    /// <response code="200">CSV generado exitosamente</response>
    /// <response code="400">Período inválido</response>
    /// <response code="401">No autenticado</response>
    /// <remarks>
    /// Ejemplo de uso:
    /// 
    ///     GET /api/nominas/exportar-csv?periodo=2025-01&amp;incluirAnulados=false
    /// 
    /// El archivo CSV incluye:
    /// - Header row con columnas: PagoID, EmpleadoID, FechaPago, Periodo, SalarioBruto, etc.
    /// - Una fila por recibo con datos principales
    /// - Filas adicionales por cada deducción (concepto y monto)
    /// 
    /// Formato del período:
    /// - YYYY-MM: Mes específico (ej: "2025-01")
    /// 
    /// El archivo se descarga con nombre:
    /// - Nomina_YYYY_MM_timestamp.csv
    /// 
    /// NOTA: El CSV usa codificación UTF-8 y puede abrirse en Excel.
    /// </remarks>
    [HttpGet("exportar-csv")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ExportarCsv(
        [FromQuery] string periodo,
        [FromQuery] bool incluirAnulados = false)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        _logger.LogInformation(
            "Exporting payroll CSV - User: {UserId}, Period: {Periodo}",
            userId,
            periodo);

        var command = new ExportarNominaCsvCommand
        {
            UserId = userId ?? string.Empty,
            Periodo = periodo,
            IncluirAnulados = incluirAnulados
        };

        try
        {
            var result = await _mediator.Send(command);

            _logger.LogInformation(
                "CSV export completed - Receipts: {Count}, Size: {Bytes}",
                result.TotalRecibos,
                result.FileContent.Length);

            return File(result.FileContent, result.ContentType, result.FileName);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Validation error exporting CSV");
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Procesa el pago de una contratación de servicio temporal (GAP-005).
    /// </summary>
    /// <param name="command">Datos del pago (header y detalles)</param>
    /// <returns>ID del pago generado</returns>
    /// <response code="200">Pago procesado exitosamente, retorna pagoID</response>
    /// <response code="400">Datos inválidos</response>
    /// <response code="404">Contratación no encontrada</response>
    /// <remarks>
    /// Endpoint implementado para GAP-005: ProcessContractPayment
    /// 
    /// LÓGICA LEGACY: EmpleadosService.procesarPagoContratacion() (líneas 168-204)
    /// 
    /// COMPORTAMIENTO:
    /// 1. Valida que la contratación exista y esté activa
    /// 2. Crea header de recibo (Empleador_Recibos_Header_Contrataciones)
    /// 3. Crea detalles de recibo (Empleador_Recibos_Detalle_Contrataciones)
    /// 4. Si primer detalle tiene Concepto == "Pago Final":
    ///    - Actualiza DetalleContrataciones.estatus = 2 (Completada)
    ///    - Usa método DDD DetalleContratacion.Completar()
    /// 5. Retorna pagoID generado
    /// 
    /// EJEMPLO REQUEST:
    /// 
    ///     POST /api/nominas/contrataciones/procesar-pago
    ///     {
    ///       "userId": "123",
    ///       "contratacionId": 45,
    ///       "detalleId": 12,
    ///       "fechaRegistro": "2025-01-15T10:00:00Z",
    ///       "fechaPago": "2025-01-15T10:00:00Z",
    ///       "conceptoPago": "Pago por servicios profesionales",
    ///       "tipo": 1,
    ///       "detalles": [
    ///         {
    ///           "concepto": "Horas trabajadas",
    ///           "monto": 5000.00
    ///         },
    ///         {
    ///           "concepto": "Materiales",
    ///           "monto": 500.00
    ///         }
    ///       ]
    ///     }
    /// 
    /// TIPOS DE PAGO:
    /// - 1 = Pago único / Adelanto
    /// - 2 = Pago Final (actualiza estatus a Completada)
    /// 
    /// NOTAS:
    /// - Si Concepto del primer detalle == "Pago Final" → estatus 2 (Legacy behavior)
    /// - Usa métodos factory DDD: EmpleadorRecibosHeaderContratacione.Crear()
    /// - Usa comportamiento DDD: DetalleContratacion.Completar()
    /// </remarks>
    [HttpPost("contrataciones/procesar-pago")]
    [ProducesResponseType(typeof(int), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ProcesarPagoContratacion([FromBody] ProcessContractPaymentCommand command)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        _logger.LogInformation(
            "Processing contract payment - User: {UserId}, ContractID: {ContratacionId}, Type: {Tipo}",
            userId,
            command.ContratacionId,
            command.Tipo);

        try
        {
            var pagoId = await _mediator.Send(command);

            _logger.LogInformation(
                "Contract payment processed successfully - PagoID: {PagoId}, ContractID: {ContratacionId}",
                pagoId,
                command.ContratacionId);

            return Ok(new { pagoId });
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid contract payment data");
            return BadRequest(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Contract not found or inactive");
            return NotFound(ex.Message);
        }
    }
}
