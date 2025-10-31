using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MiGenteEnLinea.Application.Common.Interfaces;
using MiGenteEnLinea.Application.Common.Exceptions;

namespace MiGenteEnLinea.Application.Features.Contratistas.Commands.ActivarPerfil;

/// <summary>
/// Handler: Activa el perfil de un contratista
/// </summary>
/// <remarks>
/// ✅ HTTP STATUS FIX (Oct 30, 2025): Changed InvalidOperationException to NotFoundException
/// to return proper 404 NotFound instead of 400 BadRequest for non-existent userId
/// </remarks>
public class ActivarPerfilCommandHandler : IRequestHandler<ActivarPerfilCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly ILogger<ActivarPerfilCommandHandler> _logger;

    public ActivarPerfilCommandHandler(
        IApplicationDbContext context,
        ILogger<ActivarPerfilCommandHandler> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task Handle(ActivarPerfilCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Activando perfil de contratista para userId: {UserId}", request.UserId);

        // 1. BUSCAR CONTRATISTA por userId
        var contratista = await _context.Contratistas
            .Where(c => c.UserId == request.UserId)
            .FirstOrDefaultAsync(cancellationToken);

        if (contratista == null)
        {
            _logger.LogWarning("Contratista no encontrado para userId: {UserId}", request.UserId);
            throw new NotFoundException("Contratista", request.UserId);
        }

        // 2. ACTIVAR PERFIL usando Domain Method
        try
        {
            contratista.Activar();
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Error al activar perfil para userId: {UserId}", request.UserId);
            throw; // Re-throw (perfil ya está activo)
        }

        // 3. GUARDAR CAMBIOS
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Perfil de contratista activado exitosamente. ContratistaId: {ContratistaId}, UserId: {UserId}",
            contratista.Id, request.UserId);
    }
}
