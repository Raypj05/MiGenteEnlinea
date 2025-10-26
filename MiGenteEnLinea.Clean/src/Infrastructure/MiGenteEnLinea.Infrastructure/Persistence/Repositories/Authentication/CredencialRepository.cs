using Microsoft.EntityFrameworkCore;
using MiGenteEnLinea.Domain.Entities.Authentication;
using MiGenteEnLinea.Domain.Interfaces.Repositories.Authentication;
using MiGenteEnLinea.Infrastructure.Persistence.Contexts;

namespace MiGenteEnLinea.Infrastructure.Persistence.Repositories.Authentication;

/// <summary>
/// Implementación del repositorio para la entidad Credencial
/// LOTE 1: Queries específicas de autenticación con optimizaciones
/// </summary>
public class CredencialRepository : Repository<Credencial>, ICredencialRepository
{
    public CredencialRepository(MiGenteDbContext context) : base(context)
    {
    }

    /// <summary>
    /// Obtiene credencial por email (case-insensitive)
    /// </summary>
    public async Task<Credencial?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .FirstOrDefaultAsync(c => c.Email.Value.ToLower() == email.ToLower(), cancellationToken);
    }

    /// <summary>
    /// Obtiene credencial por UserID único
    /// </summary>
    public async Task<Credencial?> GetByUserIdAsync(string userId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .FirstOrDefaultAsync(c => c.UserId == userId, cancellationToken);
    }

    /// <summary>
    /// Verifica existencia de email (case-insensitive) - optimizado sin traer entidad
    /// </summary>
    public async Task<bool> ExistsByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        // Normalize email before query to avoid EF Core translation issues with Value Object
        var normalizedEmail = Email.CreateUnsafe(email.ToLowerInvariant());
        return await _dbSet
            .AnyAsync(c => c.Email == normalizedEmail, cancellationToken);
    }

    /// <summary>
    /// Verifica si credencial está activa - optimizado con Select directo
    /// </summary>
    public async Task<bool> IsActivoAsync(string userId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(c => c.UserId == userId)
            .Select(c => c.Activo)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <summary>
    /// Obtiene credenciales inactivas que nunca fueron activadas (para reenvío de email)
    /// Ordenadas por fecha de creación para priorizar las más antiguas
    /// </summary>
    public async Task<IEnumerable<Credencial>> GetCredencialesInactivasAsync(CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(c => !c.Activo && c.FechaActivacion == null)
            .OrderBy(c => c.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    // TODO: Implementar cuando la entidad Credencial tenga propiedad Bloqueado
    // public async Task<IEnumerable<Credencial>> GetCredencialesBloqueadasAsync(CancellationToken cancellationToken = default)
    // {
    //     return await _dbSet
    //         .Where(c => c.Bloqueado)
    //         .ToListAsync(cancellationToken);
    // }
}
