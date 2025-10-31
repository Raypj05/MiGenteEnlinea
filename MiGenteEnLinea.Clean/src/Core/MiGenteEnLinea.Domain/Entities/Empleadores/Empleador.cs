using MiGenteEnLinea.Domain.Common;
using MiGenteEnLinea.Domain.Events.Empleadores;

namespace MiGenteEnLinea.Domain.Entities.Empleadores;

/// <summary>
/// Entidad Empleador - Representa el perfil de un empleador en el sistema
/// Un empleador es una empresa o persona que publica ofertas de trabajo y gestiona empleados.
/// 
/// MAPEO CON LEGACY:
/// - Tabla: Ofertantes (nombre legacy plural - mantener para compatibilidad)
/// - Columnas: ofertanteID, fechaPublicacion, userID, habilidades, experiencia, descripcion, foto
/// 
/// NOTAS DE NEGOCIO:
/// - Un userId (Credencial) puede ser empleador O contratista (relación 1:1)
/// - Los empleadores publican ofertas laborales y gestionan nómina
/// - Habilidades y experiencia son campos descriptivos del negocio/empresa
/// 
/// SOFT DELETE:
/// - Hereda de SoftDeletableEntity para eliminación lógica (Oct 2025)
/// - Método Delete(userId) marca como eliminado sin borrado físico
/// </summary>
public sealed class Empleador : SoftDeletableEntity
{
    /// <summary>
    /// Identificador único del empleador
    /// </summary>
    public int Id { get; private set; }

    /// <summary>
    /// Fecha en que se publicó/creó el perfil de empleador
    /// </summary>
    public DateTime? FechaPublicacion { get; private set; }

    /// <summary>
    /// Identificador del usuario (FK a Credencial.UserId)
    /// Relaciona al empleador con sus credenciales de acceso
    /// </summary>
    public string UserId { get; private set; }

    /// <summary>
    /// Habilidades o capacidades de la empresa empleadora
    /// Ejemplo: "Construcción, Gestión de proyectos, Supervisión"
    /// Máximo: 200 caracteres
    /// </summary>
    public string? Habilidades { get; private set; }

    /// <summary>
    /// Años de experiencia o trayectoria de la empresa
    /// Ejemplo: "15 años en el sector construcción"
    /// Máximo: 200 caracteres
    /// </summary>
    public string? Experiencia { get; private set; }

    /// <summary>
    /// Descripción general del empleador/empresa
    /// Ejemplo: "Empresa líder en construcción residencial..."
    /// Máximo: 500 caracteres
    /// </summary>
    public string? Descripcion { get; private set; }

    /// <summary>
    /// Foto de perfil o logo de la empresa (byte array)
    /// TODO: Migrar a Azure Blob Storage en el futuro (guardar solo URL)
    /// Tamaño máximo recomendado: 5MB
    /// </summary>
    public byte[]? Foto { get; private set; }

    /// <summary>
    /// Constructor privado para EF Core
    /// </summary>
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor
    private Empleador() { }
#pragma warning restore CS8618

    /// <summary>
    /// Constructor privado para lógica de creación
    /// </summary>
    private Empleador(
        string userId,
        string? habilidades = null,
        string? experiencia = null,
        string? descripcion = null)
    {
        UserId = userId ?? throw new ArgumentNullException(nameof(userId));
        Habilidades = habilidades?.Trim();
        Experiencia = experiencia?.Trim();
        Descripcion = descripcion?.Trim();
        FechaPublicacion = DateTime.UtcNow;
    }

    /// <summary>
    /// Factory Method: Crea un nuevo perfil de empleador
    /// </summary>
    /// <param name="userId">ID del usuario (debe existir en Credenciales)</param>
    /// <param name="habilidades">Habilidades de la empresa (opcional, max 200 caracteres)</param>
    /// <param name="experiencia">Experiencia de la empresa (opcional, max 200 caracteres)</param>
    /// <param name="descripcion">Descripción general (opcional, max 500 caracteres)</param>
    /// <returns>Nueva instancia de Empleador</returns>
    /// <exception cref="ArgumentException">Si los datos no cumplen validaciones</exception>
    public static Empleador Create(
        string userId,
        string? habilidades = null,
        string? experiencia = null,
        string? descripcion = null)
    {
        // Validaciones de negocio
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("UserId es requerido", nameof(userId));

        if (habilidades?.Length > 200)
            throw new ArgumentException("Habilidades no puede exceder 200 caracteres", nameof(habilidades));

        if (experiencia?.Length > 200)
            throw new ArgumentException("Experiencia no puede exceder 200 caracteres", nameof(experiencia));

        if (descripcion?.Length > 500)
            throw new ArgumentException("Descripcion no puede exceder 500 caracteres", nameof(descripcion));

        var empleador = new Empleador(userId, habilidades, experiencia, descripcion);

        // Levantar evento de dominio
        empleador.RaiseDomainEvent(new EmpleadorCreadoEvent(empleador.Id, userId));

        return empleador;
    }

    /// <summary>
    /// DOMAIN METHOD: Actualiza la información del perfil del empleador
    /// </summary>
    /// <param name="habilidades">Nuevas habilidades (opcional)</param>
    /// <param name="experiencia">Nueva experiencia (opcional)</param>
    /// <param name="descripcion">Nueva descripción (opcional)</param>
    /// <exception cref="ArgumentException">Si los datos exceden longitudes máximas</exception>
    public void ActualizarPerfil(
        string? habilidades = null,
        string? experiencia = null,
        string? descripcion = null)
    {
        // Validaciones
        if (habilidades?.Length > 200)
            throw new ArgumentException("Habilidades no puede exceder 200 caracteres", nameof(habilidades));

        if (experiencia?.Length > 200)
            throw new ArgumentException("Experiencia no puede exceder 200 caracteres", nameof(experiencia));

        if (descripcion?.Length > 500)
            throw new ArgumentException("Descripcion no puede exceder 500 caracteres", nameof(descripcion));

        // Actualizar solo los campos que no sean null
        if (habilidades != null)
            Habilidades = habilidades.Trim();

        if (experiencia != null)
            Experiencia = experiencia.Trim();

        if (descripcion != null)
            Descripcion = descripcion.Trim();

        // Levantar evento de dominio
        RaiseDomainEvent(new PerfilActualizadoEvent(Id));
    }

    /// <summary>
    /// DOMAIN METHOD: Actualiza la foto/logo del empleador
    /// </summary>
    /// <param name="foto">Imagen en formato byte array</param>
    /// <exception cref="ArgumentException">Si la foto está vacía o excede el tamaño máximo</exception>
    public void ActualizarFoto(byte[] foto)
    {
        if (foto == null || foto.Length == 0)
            throw new ArgumentException("Foto no puede estar vacía", nameof(foto));

        // Validar tamaño máximo (5MB)
        const int maxSizeBytes = 5 * 1024 * 1024; // 5MB
        if (foto.Length > maxSizeBytes)
        {
            var maxSizeMB = maxSizeBytes / (1024 * 1024);
            throw new ArgumentException($"Foto no puede exceder {maxSizeMB}MB. Tamaño actual: {foto.Length / (1024 * 1024)}MB", nameof(foto));
        }

        Foto = foto;

        // Levantar evento de dominio
        RaiseDomainEvent(new FotoActualizadaEvent(Id));
    }

    /// <summary>
    /// DOMAIN METHOD: Elimina la foto del perfil
    /// </summary>
    public void EliminarFoto()
    {
        Foto = null;
    }

    /// <summary>
    /// DOMAIN METHOD: Valida si el empleador puede publicar ofertas laborales
    /// </summary>
    /// <returns>True si cumple con los requisitos mínimos</returns>
    /// <remarks>
    /// Lógica de negocio: Un empleador puede publicar si tiene al menos:
    /// - UserId válido
    /// - Perfil con descripción básica (para que los contratistas sepan de la empresa)
    /// </remarks>
    public bool PuedePublicarOfertas()
    {
        // Lógica básica: debe tener UserId y alguna descripción
        return !string.IsNullOrWhiteSpace(UserId) && 
               !string.IsNullOrWhiteSpace(Descripcion);
    }

    /// <summary>
    /// DOMAIN METHOD: Valida si el perfil está completo
    /// </summary>
    /// <returns>True si tiene toda la información básica</returns>
    public bool PerfilCompleto()
    {
        return !string.IsNullOrWhiteSpace(UserId) &&
               !string.IsNullOrWhiteSpace(Habilidades) &&
               !string.IsNullOrWhiteSpace(Experiencia) &&
               !string.IsNullOrWhiteSpace(Descripcion);
    }

    /// <summary>
    /// DOMAIN METHOD: Obtiene un resumen corto del perfil
    /// </summary>
    /// <returns>Descripción truncada a 100 caracteres</returns>
    public string ObtenerResumen()
    {
        if (string.IsNullOrWhiteSpace(Descripcion))
            return "Sin descripción";

        return Descripcion.Length <= 100 
            ? Descripcion 
            : Descripcion.Substring(0, 97) + "...";
    }
}
