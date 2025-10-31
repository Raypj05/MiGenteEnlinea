using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MiGenteEnLinea.Application.Features.Empleadores.Commands.CreateEmpleador;
using MiGenteEnLinea.Application.Features.Empleadores.Commands.UpdateEmpleador;
using MiGenteEnLinea.Application.Features.Empleadores.Commands.UpdateEmpleadorFoto;
using MiGenteEnLinea.Application.Features.Empleadores.Commands.DeleteEmpleador;
using MiGenteEnLinea.Application.Features.Empleadores.Queries.GetEmpleadorById;
using MiGenteEnLinea.Application.Features.Empleadores.Queries.GetEmpleadorByUserId;
using MiGenteEnLinea.Application.Features.Empleadores.Queries.SearchEmpleadores;

namespace MiGenteEnLinea.API.Controllers;

/// <summary>
/// Controller: Gestión de Empleadores (CRUD completo)
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
[Produces("application/json")]
public class EmpleadoresController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<EmpleadoresController> _logger;

    public EmpleadoresController(IMediator mediator, ILogger<EmpleadoresController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    /// <summary>
    /// Crear un nuevo empleador
    /// </summary>
    /// <param name="command">Datos del empleador a crear</param>
    /// <returns>ID del empleador creado</returns>
    /// <response code="201">Empleador creado exitosamente</response>
    /// <response code="400">Datos inválidos o usuario ya tiene empleador</response>
    [HttpPost]
    [ProducesResponseType(typeof(CreateEmpleadorResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateEmpleador([FromBody] CreateEmpleadorCommand command)
    {
        try
        {
            var empleadorId = await _mediator.Send(command);

            return CreatedAtAction(
                nameof(GetEmpleadorById),
                new { empleadorId },
                new CreateEmpleadorResponse
                {
                    EmpleadorId = empleadorId,
                    Message = "Empleador creado exitosamente"
                });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("Error al crear empleador: {Message}", ex.Message);
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Obtener empleador por ID
    /// </summary>
    /// <param name="empleadorId">ID del empleador</param>
    /// <returns>Datos del empleador</returns>
    /// <response code="200">Empleador encontrado</response>
    /// <response code="404">Empleador no encontrado</response>
    [HttpGet("{empleadorId:int}")]
    [ProducesResponseType(typeof(Application.Features.Empleadores.DTOs.EmpleadorDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetEmpleadorById(int empleadorId)
    {
        var query = new GetEmpleadorByIdQuery(empleadorId);
        var result = await _mediator.Send(query);

        if (result == null)
            return NotFound(new { error = $"Empleador {empleadorId} no encontrado" });

        return Ok(result);
    }

    /// <summary>
    /// Obtener empleador por UserId
    /// </summary>
    /// <param name="userId">GUID del usuario (Credencial.UserId)</param>
    /// <returns>Datos del empleador</returns>
    /// <response code="200">Empleador encontrado</response>
    /// <response code="404">Empleador no encontrado</response>
    [HttpGet("by-user/{userId}")]
    [ProducesResponseType(typeof(Application.Features.Empleadores.DTOs.EmpleadorDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetEmpleadorByUserId(string userId)
    {
        var query = new GetEmpleadorByUserIdQuery(userId);
        var result = await _mediator.Send(query);

        if (result == null)
            return NotFound(new { error = $"Empleador no encontrado para usuario {userId}" });

        return Ok(result);
    }

    /// <summary>
    /// Buscar empleadores con paginación
    /// </summary>
    /// <param name="searchTerm">Término de búsqueda (opcional)</param>
    /// <param name="pageIndex">Número de página (default: 1)</param>
    /// <param name="pageSize">Tamaño de página (default: 10, max: 100)</param>
    /// <returns>Lista paginada de empleadores</returns>
    /// <response code="200">Búsqueda exitosa</response>
    [HttpGet]
    [ProducesResponseType(typeof(SearchEmpleadoresResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> SearchEmpleadores(
        [FromQuery] string? searchTerm = null,
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 10)
    {
        // Validar límites de paginación
        if (pageIndex < 1) pageIndex = 1;
        if (pageSize < 1) pageSize = 10;
        if (pageSize > 100) pageSize = 100;

        var query = new SearchEmpleadoresQuery(searchTerm, pageIndex, pageSize);
        var result = await _mediator.Send(query);

        return Ok(result);
    }

    /// <summary>
    /// Actualizar perfil de empleador
    /// </summary>
    /// <param name="userId">GUID del usuario</param>
    /// <param name="command">Datos a actualizar</param>
    /// <returns>Confirmación de actualización</returns>
    /// <response code="200">Empleador actualizado exitosamente</response>
    /// <response code="400">Datos inválidos</response>
    /// <response code="404">Empleador no encontrado</response>
    [HttpPut("{userId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateEmpleador(string userId, [FromBody] UpdateEmpleadorRequest request)
    {
        var command = new UpdateEmpleadorCommand(
            userId,
            request.Habilidades,
            request.Experiencia,
            request.Descripcion
        );

        try
        {
            await _mediator.Send(command);
            return Ok(new { message = "Empleador actualizado exitosamente" });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("Error al actualizar empleador: {Message}", ex.Message);
            return NotFound(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Actualizar foto/logo del empleador
    /// </summary>
    /// <param name="userId">GUID del usuario</param>
    /// <param name="file">Archivo de imagen (max 5MB)</param>
    /// <returns>Confirmación de actualización</returns>
    /// <response code="200">Foto actualizada exitosamente</response>
    /// <response code="400">Archivo inválido o muy grande</response>
    /// <response code="404">Empleador no encontrado</response>
    [HttpPut("{userId}/foto")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateEmpleadorFoto(string userId, IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { error = "Archivo de imagen es requerido" });

        // Validar tamaño (5MB)
        const int maxSizeBytes = 5 * 1024 * 1024;
        if (file.Length > maxSizeBytes)
            return BadRequest(new { error = $"El archivo excede el tamaño máximo permitido de {maxSizeBytes / (1024 * 1024)}MB" });

        // Leer archivo como byte array
        byte[] fotoBytes;
        using (var memoryStream = new MemoryStream())
        {
            await file.CopyToAsync(memoryStream);
            fotoBytes = memoryStream.ToArray();
        }

        var command = new UpdateEmpleadorFotoCommand(userId, fotoBytes);

        try
        {
            await _mediator.Send(command);
            return Ok(new { message = "Foto actualizada exitosamente" });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("Error al actualizar foto: {Message}", ex.Message);
            return NotFound(new { error = ex.Message });
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning("Foto inválida: {Message}", ex.Message);
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Eliminar empleador (FÍSICO - ⚠️ NO RECOMENDADO)
    /// </summary>
    /// <param name="userId">GUID del usuario</param>
    /// <returns>Confirmación de eliminación</returns>
    /// <response code="200">Empleador eliminado exitosamente</response>
    /// <response code="404">Empleador no encontrado</response>
    [HttpDelete("{userId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteEmpleador(string userId)
    {
        var command = new DeleteEmpleadorCommand(userId);

        try
        {
            await _mediator.Send(command);
            return Ok(new { message = "Empleador eliminado exitosamente" });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("Error al eliminar empleador: {Message}", ex.Message);
            return NotFound(new { error = ex.Message });
        }
    }
}

// ============================================
// DTOs para Request/Response
// ============================================

/// <summary>
/// Response al crear empleador
/// </summary>
public sealed class CreateEmpleadorResponse
{
    public int EmpleadorId { get; init; }
    public string Message { get; init; } = string.Empty;
}

/// <summary>
/// Request para actualizar empleador
/// </summary>
public sealed class UpdateEmpleadorRequest
{
    public string? Habilidades { get; init; }
    public string? Experiencia { get; init; }
    public string? Descripcion { get; init; }
}
