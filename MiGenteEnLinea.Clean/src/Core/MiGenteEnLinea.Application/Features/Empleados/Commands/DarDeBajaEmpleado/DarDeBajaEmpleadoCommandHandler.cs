using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MiGenteEnLinea.Application.Common.Exceptions;
using MiGenteEnLinea.Application.Common.Interfaces;

namespace MiGenteEnLinea.Application.Features.Empleados.Commands.DarDeBajaEmpleado;

public class DarDeBajaEmpleadoCommandHandler : IRequestHandler<DarDeBajaEmpleadoCommand, bool>
{
    private readonly IApplicationDbContext _context;
    private readonly ILegacyDataService _legacyDataService;
    private readonly ILogger<DarDeBajaEmpleadoCommandHandler> _logger;

    public DarDeBajaEmpleadoCommandHandler(
        IApplicationDbContext context,
        ILegacyDataService legacyDataService,
        ILogger<DarDeBajaEmpleadoCommandHandler> logger)
    {
        _context = context;
        _legacyDataService = legacyDataService;
        _logger = logger;
    }

    public async Task<bool> Handle(DarDeBajaEmpleadoCommand request, CancellationToken cancellationToken)
    {
        // Validate employee exists and belongs to user (ownership validation)
        var empleado = await _context.Empleados
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.EmpleadoId == request.EmpleadoId, cancellationToken);

        if (empleado == null)
        {
            _logger.LogWarning("Empleado no encontrado: {EmpleadoId}", request.EmpleadoId);
            throw new NotFoundException($"Empleado con ID {request.EmpleadoId} no encontrado");
        }

        // Check ownership - empleado must belong to the user
        if (empleado.UserId != request.UserId)
        {
            _logger.LogWarning(
                "Usuario {UserId} intentó dar de baja empleado {EmpleadoId} que pertenece a {OwnerId}",
                request.UserId,
                request.EmpleadoId,
                empleado.UserId);
            throw new ForbiddenAccessException("No tienes permiso para dar de baja este empleado");
        }

        _logger.LogInformation(
            "Dando de baja empleado: {EmpleadoId}, Fecha: {FechaBaja}, Motivo: {Motivo}",
            request.EmpleadoId,
            request.FechaBaja,
            request.Motivo);

        var result = await _legacyDataService.DarDeBajaEmpleadoAsync(
            request.EmpleadoId,
            request.UserId,
            request.FechaBaja,
            request.Prestaciones,
            request.Motivo,
            cancellationToken);

        if (!result)
        {
            _logger.LogWarning("No se pudo dar de baja el empleado: {EmpleadoId}", request.EmpleadoId);
            throw new BadRequestException("No se pudo dar de baja el empleado");
        }

        _logger.LogInformation("Empleado dado de baja exitosamente: {EmpleadoId}", request.EmpleadoId);
        
        return result;
    }
}
