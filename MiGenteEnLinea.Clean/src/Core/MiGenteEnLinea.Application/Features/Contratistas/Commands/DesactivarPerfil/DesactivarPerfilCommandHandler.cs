using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MiGenteEnLinea.Application.Common.Interfaces;
using MiGenteEnLinea.Application.Common.Exceptions;

namespace MiGenteEnLinea.Application.Features.Contratistas.Commands.DesactivarPerfil;

/// <summary>
/// Handler: Desactiva el perfil de un contratista
/// </summary>
/// <remarks>
/// ✅ HTTP STATUS FIX (Oct 30, 2025): Changed InvalidOperationException to NotFoundException
/// to return proper 404 NotFound instead of 400 BadRequest for non-existent userId
/// </remarks>
public class DesactivarPerfilCommandHandler : IRequestHandler<DesactivarPerfilCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly ILogger<DesactivarPerfilCommandHandler> _logger;

    public DesactivarPerfilCommandHandler(
        IApplicationDbContext context,
        ILogger<DesactivarPerfilCommandHandler> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task Handle(DesactivarPerfilCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Desactivando perfil de contratista para userId: {UserId}", request.UserId);

        // 1. BUSCAR CONTRATISTA por userId
        var contratista = await _context.Contratistas
            .Where(c => c.UserId == request.UserId)
            .FirstOrDefaultAsync(cancellationToken);

        if (contratista == null)
        {
            _logger.LogWarning("Contratista no encontrado para userId: {UserId}", request.UserId);
            throw new NotFoundException("Contratista", request.UserId);
        }

        // 2. DESACTIVAR PERFIL usando Domain Method
        try
        {
            contratista.Desactivar();
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Error al desactivar perfil para userId: {UserId}", request.UserId);
            throw; // Re-throw (perfil ya está desactivado)
        }

        // 3. GUARDAR CAMBIOS
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Perfil de contratista desactivado exitosamente. ContratistaId: {ContratistaId}, UserId: {UserId}",
            contratista.Id, request.UserId);
    }
}
