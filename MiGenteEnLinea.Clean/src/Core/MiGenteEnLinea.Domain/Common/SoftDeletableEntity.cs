namespace MiGenteEnLinea.Domain.Common;

/// <summary>
/// Entidad base para soft delete (eliminación lógica).
/// Los registros no se eliminan físicamente, solo se marcan como eliminados.
/// NOTA: Hereda de AggregateRoot para soportar domain events (Oct 2025)
/// </summary>
public abstract class SoftDeletableEntity : AggregateRoot
{
    /// <summary>
    /// Indica si la entidad fue eliminada lógicamente
    /// </summary>
    public bool IsDeleted { get; private set; }

    /// <summary>
    /// Momento de la eliminación (UTC)
    /// </summary>
    public DateTime? DeletedAt { get; private set; }

    /// <summary>
    /// Usuario que eliminó la entidad
    /// </summary>
    public string? DeletedBy { get; private set; }

    /// <summary>
    /// Elimina lógicamente la entidad
    /// </summary>
    public void Delete(string userId)
    {
        if (IsDeleted) return;

        IsDeleted = true;
        DeletedAt = DateTime.UtcNow;
        DeletedBy = userId;
    }

    /// <summary>
    /// Restaura una entidad eliminada
    /// </summary>
    public void Undelete()
    {
        IsDeleted = false;
        DeletedAt = null;
        DeletedBy = null;
    }
}
