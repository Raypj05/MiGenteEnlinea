using AutoMapper;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MiGenteEnLinea.Application.Common.Interfaces;
using MiGenteEnLinea.Application.Features.Contrataciones.DTOs;
using MiGenteEnLinea.Domain.Entities.Contrataciones;

namespace MiGenteEnLinea.Application.Features.Contrataciones.Queries.GetContrataciones;

/// <summary>
/// Handler para obtener lista de contrataciones con filtros.
/// </summary>
public class GetContratacionesQueryHandler : IRequestHandler<GetContratacionesQuery, List<ContratacionDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly IMapper _mapper;
    private readonly ILogger<GetContratacionesQueryHandler> _logger;

    public GetContratacionesQueryHandler(
        IApplicationDbContext context,
        IMapper mapper,
        ILogger<GetContratacionesQueryHandler> logger)
    {
        _context = context;
        _mapper = mapper;
        _logger = logger;
    }

    public async Task<List<ContratacionDto>> Handle(
        GetContratacionesQuery request, 
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Getting contrataciones with filters - Status: {Estatus}, Page: {Page}, Size: {Size}",
            request.Estatus,
            request.PageNumber,
            request.PageSize);

        // Query base
        var query = _context.DetalleContrataciones.AsQueryable();

        // Aplicar filtros
        if (request.ContratacionId.HasValue)
        {
            query = query.Where(c => c.ContratacionId == request.ContratacionId.Value);
        }

        if (request.Estatus.HasValue)
        {
            query = query.Where(c => c.Estatus == request.Estatus.Value);
        }

        if (request.SoloPendientes == true)
        {
            query = query.Where(c => c.Estatus == 1); // Pendiente
        }

        if (request.SoloActivas == true)
        {
            // Activas = Aceptada (2) o EnProgreso (5)
            query = query.Where(c => c.Estatus == 2 || c.Estatus == 5);
        }

        if (request.SoloNoCalificadas == true)
        {
            query = query.Where(c => c.Calificado == false && c.Estatus == 4); // Completadas sin calificar
        }

        if (request.FechaInicioDesde.HasValue)
        {
            query = query.Where(c => c.FechaInicio >= request.FechaInicioDesde.Value);
        }

        if (request.FechaInicioHasta.HasValue)
        {
            query = query.Where(c => c.FechaInicio <= request.FechaInicioHasta.Value);
        }

        if (request.MontoMinimo.HasValue)
        {
            query = query.Where(c => c.MontoAcordado >= request.MontoMinimo.Value);
        }

        if (request.MontoMaximo.HasValue)
        {
            query = query.Where(c => c.MontoAcordado <= request.MontoMaximo.Value);
        }

        // Ordenar por fecha de inicio (más recientes primero)
        query = query.OrderByDescending(c => c.FechaInicio);

        // Paginación
        var skip = (request.PageNumber - 1) * request.PageSize;
        query = query.Skip(skip).Take(request.PageSize);

        // Ejecutar query
        var contrataciones = await query.ToListAsync(cancellationToken);

        _logger.LogInformation(
            "Retrieved {Count} contrataciones",
            contrataciones.Count);

        // Mapear a DTOs
        var dtos = _mapper.Map<List<ContratacionDto>>(contrataciones);

        return dtos;
    }
}
