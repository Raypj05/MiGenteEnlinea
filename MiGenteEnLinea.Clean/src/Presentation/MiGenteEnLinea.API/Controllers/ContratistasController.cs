using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MiGenteEnLinea.Application.Features.Contratistas.Commands.ActivarPerfil;
using MiGenteEnLinea.Application.Features.Contratistas.Commands.AddServicio;
using MiGenteEnLinea.Application.Features.Contratistas.Commands.CreateContratista;
using MiGenteEnLinea.Application.Features.Contratistas.Commands.DesactivarPerfil;
using MiGenteEnLinea.Application.Features.Contratistas.Commands.RemoveServicio;
using MiGenteEnLinea.Application.Features.Contratistas.Commands.UpdateContratista;
using MiGenteEnLinea.Application.Features.Contratistas.Commands.UpdateContratistaImagen;
using MiGenteEnLinea.Application.Features.Contratistas.Queries.GetContratistaById;
using MiGenteEnLinea.Application.Features.Contratistas.Queries.GetContratistaByUserId;
using MiGenteEnLinea.Application.Features.Contratistas.Queries.GetCedulaByUserId;
using MiGenteEnLinea.Application.Features.Contratistas.Queries.GetServiciosContratista;
using MiGenteEnLinea.Application.Features.Contratistas.Queries.SearchContratistas;

namespace MiGenteEnLinea.API.Controllers;

/// <summary>
/// Controller: Gestión de contratistas (proveedores de servicios)
/// </summary>
/// <remarks>
/// LÓGICA LEGACY: ContratistasService.cs, index_contratista.aspx.cs
/// FUNCIONALIDAD: CRUD de contratistas + búsqueda + gestión de servicios
/// </remarks>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ContratistasController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<ContratistasController> _logger;

    public ContratistasController(IMediator mediator, ILogger<ContratistasController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    /// <summary>
    /// Crea un nuevo perfil de contratista
    /// </summary>
    /// <param name="command">Datos del contratista a crear</param>
    /// <returns>ID del contratista creado</returns>
    /// <response code="201">Contratista creado exitosamente</response>
    /// <response code="400">Datos inválidos o userId ya tiene perfil de contratista</response>
    [HttpPost]
    [ProducesResponseType(typeof(CreateContratistaResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateContratista([FromBody] CreateContratistaCommand command)
    {
        try
        {
            var contratistaId = await _mediator.Send(command);
            var response = new CreateContratistaResponse
            {
                ContratistaId = contratistaId,
                Message = "Contratista creado exitosamente"
            };

            return CreatedAtAction(
                nameof(GetContratistaById),
                new { contratistaId },
                response);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Error al crear contratista");
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Obtiene un contratista por su ID
    /// </summary>
    /// <param name="contratistaId">ID del contratista</param>
    /// <returns>Perfil público del contratista</returns>
    /// <response code="200">Contratista encontrado</response>
    /// <response code="404">Contratista no encontrado</response>
    [HttpGet("{contratistaId}")]
    [ProducesResponseType(typeof(Application.Features.Contratistas.Common.ContratistaDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetContratistaById(int contratistaId)
    {
        var query = new GetContratistaByIdQuery(contratistaId);
        var contratista = await _mediator.Send(query);

        if (contratista == null)
            return NotFound(new { error = $"No existe un contratista con ID {contratistaId}" });

        return Ok(contratista);
    }

    /// <summary>
    /// Obtiene un contratista por su userId (GUID de credenciales)
    /// </summary>
    /// <param name="userId">ID del usuario</param>
    /// <returns>Perfil del contratista asociado al usuario</returns>
    /// <response code="200">Contratista encontrado</response>
    /// <response code="404">No existe perfil de contratista para este usuario</response>
    [HttpGet("by-user/{userId}")]
    [ProducesResponseType(typeof(Application.Features.Contratistas.Common.ContratistaDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetContratistaByUserId(string userId)
    {
        var query = new GetContratistaByUserIdQuery(userId);
        var contratista = await _mediator.Send(query);

        if (contratista == null)
            return NotFound(new { error = $"No existe un perfil de contratista para el usuario {userId}" });

        return Ok(contratista);
    }

    /// <summary>
    /// Obtiene la cédula/identificación de un contratista por userId (GAP-013)
    /// </summary>
    /// <param name="userId">ID del usuario</param>
    /// <returns>Cédula/identificación del contratista</returns>
    /// <response code="200">Cédula encontrada</response>
    /// <response code="404">No existe contratista para este usuario o no tiene cédula</response>
    /// <remarks>
    /// Réplica de SuscripcionesService.obtenerCedula() del Legacy
    /// GAP-013: Endpoint simple para obtener identificación
    /// 
    /// Sample request:
    /// 
    ///     GET /api/contratistas/cedula/{userId}
    /// 
    /// Sample response:
    /// 
    ///     "00112345678"
    /// 
    /// </remarks>
    [HttpGet("cedula/{userId}")]
    [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCedulaByUserId(string userId)
    {
        var query = new GetCedulaByUserIdQuery { UserId = userId };
        var cedula = await _mediator.Send(query);

        if (string.IsNullOrWhiteSpace(cedula))
        {
            return NotFound(new
            {
                error = $"No se encontró cédula para el contratista con userId {userId}"
            });
        }

        return Ok(cedula);
    }

    /// <summary>
    /// Busca contratistas con filtros y paginación
    /// </summary>
    /// <param name="searchTerm">Término de búsqueda (busca en título, presentación, sector)</param>
    /// <param name="provincia">Filtro por provincia (opcional)</param>
    /// <param name="sector">Filtro por sector económico (opcional)</param>
    /// <param name="experienciaMinima">Años mínimos de experiencia (opcional)</param>
    /// <param name="soloActivos">Filtrar solo contratistas activos (default: true)</param>
    /// <param name="pageIndex">Número de página (default: 1)</param>
    /// <param name="pageSize">Tamaño de página (default: 10, max: 100)</param>
    /// <returns>Lista de contratistas con metadatos de paginación</returns>
    /// <response code="200">Búsqueda completada</response>
    [HttpGet]
    [ProducesResponseType(typeof(SearchContratistasResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> SearchContratistas(
        [FromQuery] string? searchTerm = null,
        [FromQuery] string? provincia = null,
        [FromQuery] string? sector = null,
        [FromQuery] int? experienciaMinima = null,
        [FromQuery] bool soloActivos = true,
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 10)
    {
        var query = new SearchContratistasQuery(
            searchTerm, provincia, sector, experienciaMinima, 
            soloActivos, pageIndex, pageSize);

        var result = await _mediator.Send(query);
        return Ok(result);
    }

    /// <summary>
    /// Obtiene todos los servicios ofrecidos por un contratista
    /// </summary>
    /// <param name="contratistaId">ID del contratista</param>
    /// <returns>Lista de servicios</returns>
    /// <response code="200">Lista de servicios obtenida</response>
    [HttpGet("{contratistaId}/servicios")]
    [ProducesResponseType(typeof(List<Application.Features.Contratistas.Common.ServicioContratistaDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetServiciosContratista(int contratistaId)
    {
        var query = new GetServiciosContratistaQuery(contratistaId);
        var servicios = await _mediator.Send(query);
        return Ok(servicios);
    }

    /// <summary>
    /// Actualiza el perfil de un contratista
    /// </summary>
    /// <param name="userId">ID del usuario (identifica al contratista)</param>
    /// <param name="request">Datos a actualizar (solo campos no nulos se actualizan)</param>
    /// <returns>Confirmación de actualización</returns>
    /// <response code="200">Perfil actualizado exitosamente</response>
    /// <response code="400">Datos inválidos o ningún campo proporcionado</response>
    /// <response code="404">Contratista no encontrado</response>
    [HttpPut("{userId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateContratista(string userId, [FromBody] UpdateContratistaRequest request)
    {
        try
        {
            var command = new UpdateContratistaCommand(
                userId,
                request.Titulo,
                request.Sector,
                request.Experiencia,
                request.Presentacion,
                request.Provincia,
                request.NivelNacional,
                request.Telefono1,
                request.Whatsapp1,
                request.Telefono2,
                request.Whatsapp2,
                request.Email
            );

            await _mediator.Send(command);

            return Ok(new { message = "Perfil actualizado exitosamente" });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Error al actualizar contratista");
            return NotFound(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Actualiza la imagen de perfil de un contratista
    /// </summary>
    /// <param name="userId">ID del usuario (identifica al contratista)</param>
    /// <param name="request">URL de la imagen ya subida</param>
    /// <returns>Confirmación de actualización</returns>
    /// <response code="200">Imagen actualizada exitosamente</response>
    /// <response code="400">URL inválida</response>
    /// <response code="404">Contratista no encontrado</response>
    /// <remarks>
    /// NOTA: Este endpoint espera que la imagen ya esté subida a storage.
    /// El frontend debe subir primero el archivo y luego enviar la URL.
    /// </remarks>
    [HttpPut("{userId}/imagen")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateContratistaImagen(string userId, [FromBody] UpdateImagenRequest request)
    {
        try
        {
            var command = new UpdateContratistaImagenCommand(userId, request.ImagenUrl);
            await _mediator.Send(command);

            return Ok(new { message = "Imagen actualizada exitosamente" });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Error al actualizar imagen");
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Activa el perfil de un contratista (lo hace visible públicamente)
    /// </summary>
    /// <param name="userId">ID del usuario (identifica al contratista)</param>
    /// <returns>Confirmación de activación</returns>
    /// <response code="200">Perfil activado exitosamente</response>
    /// <response code="400">Perfil ya estaba activo</response>
    /// <response code="404">Contratista no encontrado</response>
    [HttpPost("{userId}/activar")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ActivarPerfil(string userId)
    {
        try
        {
            var command = new ActivarPerfilCommand(userId);
            await _mediator.Send(command);

            return Ok(new { message = "Perfil activado exitosamente" });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Error al activar perfil");
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Desactiva el perfil de un contratista (lo oculta del público)
    /// </summary>
    /// <param name="userId">ID del usuario (identifica al contratista)</param>
    /// <returns>Confirmación de desactivación</returns>
    /// <response code="200">Perfil desactivado exitosamente</response>
    /// <response code="400">Perfil ya estaba desactivado</response>
    /// <response code="404">Contratista no encontrado</response>
    [HttpPost("{userId}/desactivar")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DesactivarPerfil(string userId)
    {
        try
        {
            var command = new DesactivarPerfilCommand(userId);
            await _mediator.Send(command);

            return Ok(new { message = "Perfil desactivado exitosamente" });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Error al desactivar perfil");
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Method #34: Obtiene todos los servicios de un contratista
    /// </summary>
    /// <param name="contratistaId">ID del contratista</param>
    /// <returns>Lista de servicios del contratista</returns>
    /// <response code="200">Servicios obtenidos exitosamente (puede ser lista vacía)</response>
    /// <response code="404">Contratista no encontrado</response>
    /// <remarks>
    /// Migrado desde: ContratistasService.getServicios(int contratistaID) - línea 33
    /// 
    /// **Legacy Code:**
    /// <code>
    /// public List&lt;Contratistas_Servicios&gt; getServicios(int contratistaID)
    /// {
    ///     using (migenteEntities db = new migenteEntities())
    ///     {
    ///         var result = db.Contratistas_Servicios.Where(x => x.contratistaID == contratistaID).ToList();
    ///         return result;
    ///     }
    /// }
    /// <summary>
    /// Agrega un nuevo servicio al perfil de un contratista
    /// </summary>
    /// <param name="contratistaId">ID del contratista</param>
    /// <param name="request">Detalle del servicio</param>
    /// <returns>ID del servicio creado</returns>
    /// <response code="201">Servicio agregado exitosamente</response>
    /// <response code="400">Datos inválidos</response>
    /// <response code="404">Contratista no encontrado</response>
    [HttpPost("{contratistaId}/servicios")]
    [ProducesResponseType(typeof(AddServicioResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AddServicio(int contratistaId, [FromBody] AddServicioRequest request)
    {
        try
        {
            var command = new AddServicioCommand(contratistaId, request.DetalleServicio);
            var servicioId = await _mediator.Send(command);

            var response = new AddServicioResponse
            {
                ServicioId = servicioId,
                Message = "Servicio agregado exitosamente"
            };

            return CreatedAtAction(
                nameof(GetServiciosContratista),
                new { contratistaId },
                response);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Error al agregar servicio");
            return NotFound(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Elimina un servicio del perfil de un contratista
    /// </summary>
    /// <param name="contratistaId">ID del contratista (validación de pertenencia)</param>
    /// <param name="servicioId">ID del servicio a eliminar</param>
    /// <returns>Confirmación de eliminación</returns>
    /// <response code="200">Servicio eliminado exitosamente</response>
    /// <response code="404">Servicio no encontrado o no pertenece al contratista</response>
    [HttpDelete("{contratistaId}/servicios/{servicioId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveServicio(int contratistaId, int servicioId)
    {
        try
        {
            var command = new RemoveServicioCommand(servicioId, contratistaId);
            await _mediator.Send(command);

            return Ok(new { message = "Servicio eliminado exitosamente" });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Error al eliminar servicio");
            return NotFound(new { error = ex.Message });
        }
    }
}

// ==================== REQUEST/RESPONSE DTOs ====================

/// <summary>
/// Response: Contratista creado
/// </summary>
public record CreateContratistaResponse
{
    public int ContratistaId { get; init; }
    public string Message { get; init; } = string.Empty;
}

/// <summary>
/// Request: Actualizar contratista
/// </summary>
public record UpdateContratistaRequest
{
    public string? Titulo { get; init; }
    public string? Sector { get; init; }
    public int? Experiencia { get; init; }
    public string? Presentacion { get; init; }
    public string? Provincia { get; init; }
    public bool? NivelNacional { get; init; }
    public string? Telefono1 { get; init; }
    public bool? Whatsapp1 { get; init; }
    public string? Telefono2 { get; init; }
    public bool? Whatsapp2 { get; init; }
    public string? Email { get; init; }
}

/// <summary>
/// Request: Actualizar imagen
/// </summary>
public record UpdateImagenRequest
{
    public string ImagenUrl { get; init; } = string.Empty;
}

/// <summary>
/// Request: Agregar servicio
/// </summary>
public record AddServicioRequest
{
    public string DetalleServicio { get; init; } = string.Empty;
}

/// <summary>
/// Response: Servicio agregado
/// </summary>
public record AddServicioResponse
{
    public int ServicioId { get; init; }
    public string Message { get; init; } = string.Empty;
}
