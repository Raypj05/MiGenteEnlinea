using System;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MiGenteEnLinea.Application.Common.Exceptions;
using MiGenteEnLinea.Application.Common.Interfaces;
using RemuneracionEntity = MiGenteEnLinea.Domain.Entities.Empleados.Remuneracion;

namespace MiGenteEnLinea.Application.Features.Empleados.Commands.DeleteRemuneracion;

/// <summary>
/// Handler para eliminar remuneración
/// Migrado desde: EmpleadosService.quitarRemuneracion(string userID, int id)
/// 
/// Legacy: 
/// var toDelete = db.Remuneraciones.Where(x => x.userID == userID && x.id == id).FirstOrDefault();
/// if (toDelete!=null) {
///     db.Remuneraciones.Remove(toDelete);
///     db.SaveChanges();
/// }
/// 
/// ✅ MIGRADO A EF CORE (Domain entities, no Generated)
/// </summary>
public class DeleteRemuneracionCommandHandler : IRequestHandler<DeleteRemuneracionCommand, Unit>
{
    private readonly IApplicationDbContext _context;

    public DeleteRemuneracionCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Unit> Handle(DeleteRemuneracionCommand request, CancellationToken cancellationToken)
    {
        var remuneracion = await _context.Remuneraciones
            .FirstOrDefaultAsync(x => x.Id == request.RemuneracionId, cancellationToken)
            ?? throw new NotFoundException(nameof(RemuneracionEntity), request.RemuneracionId);

        if (!string.Equals(remuneracion.UserId, request.UserId, StringComparison.OrdinalIgnoreCase))
        {
            throw new ForbiddenAccessException("No tienes permiso para eliminar esta remuneración.");
        }

        _context.Remuneraciones.Remove(remuneracion);
        await _context.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
