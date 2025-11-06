using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace MiGenteEnLinea.API.Controllers;

/// <summary>
/// Controller para funcionalidades utilitarias del sistema.
/// </summary>
/// <remarks>
/// Provee herramientas auxiliares como conversión de números a texto,
/// formateo de documentos, etc.
/// </remarks>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class UtilitariosController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<UtilitariosController> _logger;

    public UtilitariosController(
        IMediator mediator,
        ILogger<UtilitariosController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    /// <summary>
    /// Convierte un número decimal a texto en español (para documentos legales).
    /// </summary>
    /// <param name="numero">Número a convertir (ej: 1250.50)</param>
    /// <param name="incluirMoneda">Si es true, agrega "PESOS DOMINICANOS XX/100"</param>
    /// <returns>Texto representando el número en palabras</returns>
    /// <response code="200">Conversión exitosa</response>
    /// <response code="400">Número fuera de rango o inválido</response>
    /// <remarks>
    /// Migrado desde: NumeroEnLetras.cs (extension method NumerosALetras)
    /// 
    /// **USO EN LEGACY:**
    /// - Generación de PDFs de contratos
    /// - Recibos de nómina
    /// - Documentos legales que requieren montos en texto
    /// 
    /// **EJEMPLOS:**
    /// 
    /// **Ejemplo 1: Con moneda (documentos legales)**
    /// ```
    /// GET /api/utilitarios/numero-a-letras?numero=1250.50&amp;incluirMoneda=true
    /// 
    /// Response 200 OK:
    /// {
    ///   "numero": 1250.50,
    ///   "texto": "MIL DOSCIENTOS CINCUENTA PESOS DOMINICANOS 50/100"
    /// }
    /// ```
    /// 
    /// **Ejemplo 2: Sin moneda (solo número)**
    /// ```
    /// GET /api/utilitarios/numero-a-letras?numero=123&amp;incluirMoneda=false
    /// 
    /// Response 200 OK:
    /// {
    ///   "numero": 123,
    ///   "texto": "CIENTO VEINTITRES"
    /// }
    /// ```
    /// 
    /// **Ejemplo 3: Números grandes**
    /// ```
    /// GET /api/utilitarios/numero-a-letras?numero=1500000.25&amp;incluirMoneda=true
    /// 
    /// Response 200 OK:
    /// {
    ///   "numero": 1500000.25,
    ///   "texto": "UN MILLON QUINIENTOS MIL PESOS DOMINICANOS 25/100"
    /// }
    /// ```
    /// 
    /// **RANGO SOPORTADO:**
    /// - Mínimo: 0
    /// - Máximo: 999,999,999,999,999 (billones)
    /// 
    /// **FORMATO MONEDA:**
    /// - Parte entera: Texto en mayúsculas
    /// - Decimales: Formato "XX/100" (ej: 50/100, 00/100)
    /// - Moneda: "PESOS DOMINICANOS" (hardcoded para RD)
    /// </remarks>
    [HttpGet("numero-a-letras")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ConvertirNumeroALetras(
        [FromQuery] decimal numero,
        [FromQuery] bool incluirMoneda = false) // Default false: solo número sin moneda
    {
        try
        {
            _logger.LogInformation(
                "GAP-020: Convirtiendo número {Numero} a letras. IncluirMoneda={IncluirMoneda}",
                numero,
                incluirMoneda);

            // Crear query y enviar a MediatR
            var query = new Application.Features.Utilitarios.Queries.ConvertirNumeroALetras.ConvertirNumeroALetrasQuery
            {
                Numero = numero,
                IncluirMoneda = incluirMoneda
            };

            var texto = await _mediator.Send(query);

            _logger.LogInformation(
                "Conversión exitosa: {Numero} → {Texto}",
                numero,
                texto);

            // FIX: Retornar todas las propiedades como string para Dictionary<string, string>
            return Ok(new
            {
                numero = numero.ToString("0.##"), // Convert to string with max 2 decimals
                texto,
                incluirMoneda = incluirMoneda.ToString().ToLower() // Convert bool to "true"/"false"
            });
        }
        catch (FluentValidation.ValidationException validationEx)
        {
            // FIX: Manejar ValidationException explícitamente para retornar 400
            _logger.LogWarning(validationEx, "Errores de validación al convertir número: {Numero}", numero);
            
            var errors = validationEx.Errors
                .Select(e => new { field = e.PropertyName, message = e.ErrorMessage })
                .ToList();

            return BadRequest(new
            {
                message = "Errores de validación",
                errors
            });
        }
        catch (ArgumentOutOfRangeException ex)
        {
            _logger.LogWarning(ex, "Número fuera de rango: {Numero}", numero);
            return BadRequest(new
            {
                message = "Número fuera de rango soportado",
                numero,
                error = ex.Message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al convertir número a letras: {Numero}", numero);
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                new { message = "Error interno al procesar la solicitud" });
        }
    }
}
