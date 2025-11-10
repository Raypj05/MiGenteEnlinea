using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MiGenteEnLinea.Application.Common.Interfaces;
using MiGenteEnLinea.Application.Features.Calificaciones.DTOs;

namespace MiGenteEnLinea.Application.Features.Calificaciones.Queries.GetCalificaciones;

public class GetCalificacionesQueryHandler : IRequestHandler<GetCalificacionesQuery, List<CalificacionVistaDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ILogger<GetCalificacionesQueryHandler> _logger;

    public GetCalificacionesQueryHandler(
        IApplicationDbContext context,
        ILogger<GetCalificacionesQueryHandler> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<List<CalificacionVistaDto>> Handle(GetCalificacionesQuery request, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Obteniendo calificaciones - Identificacion: {Identificacion}, UserId: {UserId}",
            request.Identificacion,
            request.UserId);

        // Query base: Join Calificaciones con VistaPerfil para obtener nombre del calificador
        var query = from calificacion in _context.Calificaciones.AsNoTracking()
                    join perfil in _context.VPerfiles.AsNoTracking()
                    on calificacion.EmpleadorUserId equals perfil.UserId
                    select new { calificacion, perfil };

        // Filtrar por identificación del contratista si se proporciona
        if (!string.IsNullOrWhiteSpace(request.Identificacion))
        {
            query = query.Where(x => x.calificacion.ContratistaIdentificacion == request.Identificacion);
        }

        // Filtrar por userId si se proporciona
        if (!string.IsNullOrWhiteSpace(request.UserId))
        {
            query = query.Where(x => x.calificacion.EmpleadorUserId == request.UserId);
        }

        var calificaciones = await query
            .OrderByDescending(x => x.calificacion.Fecha)
            .Select(x => new CalificacionVistaDto
            {
                CalificacionId = x.calificacion.Id,
                UserId = x.calificacion.EmpleadorUserId,
                Identificacion = x.calificacion.ContratistaIdentificacion,
                Puntuacion = (x.calificacion.Puntualidad + x.calificacion.Cumplimiento + 
                             x.calificacion.Conocimientos + x.calificacion.Recomendacion) / 4,
                Comentario = null, // Legacy field - no hay comentarios en domain model
                FechaCreacion = x.calificacion.Fecha,
                NombreCalificador = x.perfil.Nombre ?? string.Empty,
                ApellidoCalificador = x.perfil.Apellido ?? string.Empty
            })
            .ToListAsync(cancellationToken);

        _logger.LogInformation("Se encontraron {Count} calificaciones", calificaciones.Count);

        return calificaciones;
    }
}
