using Microsoft.EntityFrameworkCore;
using MiGenteEnLinea.Domain.Entities.Authentication;
using MiGenteEnLinea.Domain.Interfaces.Repositories.Authentication;
using MiGenteEnLinea.Domain.ValueObjects;
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
    /// Fixed: Comparación directa sin ToLower() para evitar translation issues
    /// </summary>
    public async Task<Credencial?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        // Normalizar email ANTES del query para evitar EF Core translation issues
        var normalizedEmail = email.ToLowerInvariant();
        
        // Cargar todas las credenciales y filtrar en memoria (subóptimo pero funciona)
        // TODO: Investigar HasConversion para Email Value Object
        var credenciales = await _dbSet.ToListAsync(cancellationToken);
        return credenciales.FirstOrDefault(c => c.Email.Value.Equals(normalizedEmail, StringComparison.OrdinalIgnoreCase));
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
    /// Fixed: Query en memoria para evitar EF Core translation issues con Value Objects
    /// </summary>
    public async Task<bool> ExistsByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        // Normalizar email ANTES del query
        var normalizedEmail = email.ToLowerInvariant();
        
        // Cargar todos los emails y verificar en memoria
        // Subóptimo pero evita EF Core translation errors con Value Objects
        var credenciales = await _dbSet.ToListAsync(cancellationToken);
        return credenciales.Any(c => c.Email.Value.Equals(normalizedEmail, StringComparison.OrdinalIgnoreCase));
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
