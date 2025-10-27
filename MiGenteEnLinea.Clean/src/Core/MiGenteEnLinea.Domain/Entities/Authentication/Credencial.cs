using MiGenteEnLinea.Domain.Common;
using MiGenteEnLinea.Domain.Events.Authentication;
using MiGenteEnLinea.Domain.ValueObjects;

namespace MiGenteEnLinea.Domain.Entities.Authentication;

/// <summary>
/// Entidad Credencial - Gestiona la autenticación de usuarios
/// Esta entidad es un Aggregate Root que encapsula toda la lógica de autenticación.
/// 
/// MAPEO CON LEGACY:
/// - Tabla: Credenciales
/// - Columnas: id, userID, email, password, activo
/// </summary>
public sealed class Credencial : AggregateRoot
{
    /// <summary>
    /// Identificador único de la credencial
    /// </summary>
    public int Id { get; private set; }

    /// <summary>
    /// Identificador del usuario (GUID como string)
    /// Relaciona con tabla Cuentas o Ofertantes/Contratistas
    /// </summary>
    public string UserId { get; private set; }

    /// <summary>
    /// Email del usuario (normalizado a lowercase)
    /// </summary>
    private string _email;
    public Email Email
    {
        get => Email.CreateUnsafe(_email);
        private set => _email = value.Value;
    }

    /// <summary>
    /// Hash de la contraseña (NUNCA almacenar en texto plano)
    /// Legacy: usa Crypt.Encrypt() pero migraremos a BCrypt
    /// </summary>
    public string PasswordHash { get; private set; }

    /// <summary>
    /// Indica si la credencial está activa
    /// Los usuarios deben activar su cuenta vía email antes de poder usar el sistema
    /// </summary>
    public bool Activo { get; private set; }

    /// <summary>
    /// Fecha en que se activó la credencial
    /// </summary>
    public DateTime? FechaActivacion { get; private set; }

    /// <summary>
    /// Último acceso registrado
    /// </summary>
    public DateTime? UltimoAcceso { get; private set; }

    /// <summary>
    /// IP del último acceso (para auditoría de seguridad)
    /// </summary>
    public string? UltimaIp { get; private set; }

    /// <summary>
    /// Constructor privado para EF Core
    /// </summary>
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor
    private Credencial() { }
#pragma warning restore CS8618

    /// <summary>
    /// Constructor privado para lógica de creación
    /// </summary>
    private Credencial(
        string userId,
        Email email,
        string passwordHash,
        bool activo = false)
    {
        UserId = userId ?? throw new ArgumentNullException(nameof(userId));
        Email = email ?? throw new ArgumentNullException(nameof(email));
        PasswordHash = passwordHash ?? throw new ArgumentNullException(nameof(passwordHash));
        Activo = activo;
        FechaActivacion = activo ? DateTime.UtcNow : null;
    }

    /// <summary>
    /// Factory Method: Crea una nueva credencial (aún no activada)
    /// </summary>
    /// <param name="userId">GUID del usuario</param>
    /// <param name="email">Email válido</param>
    /// <param name="passwordHash">Hash de la contraseña (ya hasheado con BCrypt o Crypt)</param>
    /// <returns>Nueva instancia de Credencial</returns>
    public static Credencial Create(string userId, Email email, string passwordHash)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("UserId no puede estar vacío", nameof(userId));

        if (email == null)
            throw new ArgumentNullException(nameof(email));

        if (string.IsNullOrWhiteSpace(passwordHash))
            throw new ArgumentException("PasswordHash no puede estar vacío", nameof(passwordHash));

        var credencial = new Credencial(userId, email, passwordHash, activo: false);

        // Nota: NO lanzamos evento aquí porque la credencial aún no está activada
        return credencial;
    }

    /// <summary>
    /// Factory Method: Crea una credencial ya activada (para migraciones)
    /// </summary>
    public static Credencial CreateActivated(string userId, Email email, string passwordHash)
    {
        var credencial = new Credencial(userId, email, passwordHash, activo: true);
        credencial.FechaActivacion = DateTime.UtcNow;
        return credencial;
    }

    /// <summary>
    /// DOMAIN METHOD: Activa la credencial
    /// Se llama cuando el usuario hace clic en el link de activación del email
    /// </summary>
    public void Activar()
    {
        if (Activo)
            throw new InvalidOperationException("La credencial ya está activa");

        Activo = true;
        FechaActivacion = DateTime.UtcNow;

        // Levantar evento de dominio
        RaiseDomainEvent(new CredencialActivadaEvent(Id, UserId, _email));
    }

    /// <summary>
    /// DOMAIN METHOD: Desactiva la credencial
    /// Se usa cuando se suspende una cuenta o se elimina un usuario
    /// IDEMPOTENTE: No lanza excepción si ya está desactivada
    /// </summary>
    public void Desactivar()
    {
        // Idempotente: simplemente asegurar que Activo = false
        if (!Activo)
            return; // Ya está desactivada, no hacer nada

        Activo = false;
    }

    /// <summary>
    /// DOMAIN METHOD: Actualiza el hash de la contraseña
    /// Se usa cuando el usuario cambia su contraseña o recupera acceso
    /// </summary>
    /// <param name="nuevoPasswordHash">Nuevo hash (ya hasheado con BCrypt o Crypt)</param>
    public void ActualizarPasswordHash(string nuevoPasswordHash)
    {
        if (string.IsNullOrWhiteSpace(nuevoPasswordHash))
            throw new ArgumentException("El nuevo password hash no puede estar vacío", nameof(nuevoPasswordHash));

        PasswordHash = nuevoPasswordHash;

        // Levantar evento de dominio
        RaiseDomainEvent(new PasswordCambiadaEvent(Id, UserId, DateTime.UtcNow));
    }

    /// <summary>
    /// DOMAIN METHOD: Registra un acceso exitoso
    /// Se llama después de validar password correctamente
    /// </summary>
    /// <param name="ipAddress">IP desde donde se accedió (opcional)</param>
    public void RegistrarAcceso(string? ipAddress = null)
    {
        if (!Activo)
            throw new InvalidOperationException("No se puede registrar acceso en una credencial inactiva");

        UltimoAcceso = DateTime.UtcNow;
        UltimaIp = ipAddress;

        // Levantar evento de dominio
        RaiseDomainEvent(new AccesoRegistradoEvent(Id, UserId, UltimoAcceso.Value, ipAddress));
    }

    /// <summary>
    /// DOMAIN METHOD: Valida si la credencial puede usarse para login
    /// </summary>
    /// <returns>True si está activa, False si no</returns>
    public bool PuedeIniciarSesion()
    {
        return Activo;
    }

    /// <summary>
    /// DOMAIN METHOD: Actualiza el email (por si el usuario cambia su correo)
    /// </summary>
    public void ActualizarEmail(Email nuevoEmail)
    {
        if (nuevoEmail == null)
            throw new ArgumentNullException(nameof(nuevoEmail));

        Email = nuevoEmail;
    }

    /// <summary>
    /// DOMAIN METHOD: Actualiza la fecha del último login
    /// Usado por LegacyIdentityService para trackear logins exitosos
    /// </summary>
    public void ActualizarUltimoLogin(DateTime fechaLogin, string? ipAddress = null)
    {
        UltimoAcceso = fechaLogin;
        if (ipAddress != null)
        {
            UltimaIp = ipAddress;
        }
    }
}
