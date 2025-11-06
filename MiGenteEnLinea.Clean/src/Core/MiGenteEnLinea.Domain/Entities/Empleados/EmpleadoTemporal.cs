using MiGenteEnLinea.Domain.Common;
using MiGenteEnLinea.Domain.Events.Empleados;

namespace MiGenteEnLinea.Domain.Entities.Empleados;

/// <summary>
/// Entidad que representa un empleado temporal o contratación temporal.
/// Puede ser una persona física o jurídica contratada por un período específico.
/// </summary>
public sealed class EmpleadoTemporal : AggregateRoot
{
    /// <summary>
    /// Identificador único de la contratación temporal.
    /// </summary>
    public int ContratacionId { get; private set; }

    /// <summary>
    /// Identificador del empleador que realiza la contratación.
    /// </summary>
    public string UserId { get; private set; } = null!;

    /// <summary>
    /// Fecha de registro de la contratación temporal.
    /// </summary>
    public DateTime FechaRegistro { get; private set; }

    /// <summary>
    /// Tipo de contratación (1=Persona Física, 2=Persona Jurídica).
    /// </summary>
    public int Tipo { get; private set; }

    // Datos para Persona Jurídica (Tipo = 2)
    /// <summary>
    /// Nombre comercial de la empresa (solo para persona jurídica).
    /// </summary>
    public string? NombreComercial { get; private set; }

    /// <summary>
    /// RNC (Registro Nacional de Contribuyentes) de la empresa.
    /// </summary>
    public string? Rnc { get; private set; }

    /// <summary>
    /// Nombre del representante legal (para persona jurídica).
    /// </summary>
    public string? NombreRepresentante { get; private set; }

    /// <summary>
    /// Cédula del representante legal.
    /// </summary>
    public string? CedulaRepresentante { get; private set; }

    // Datos para Persona Física (Tipo = 1)
    /// <summary>
    /// Número de identificación (cédula o pasaporte) para persona física.
    /// </summary>
    public string? Identificacion { get; private set; }

    /// <summary>
    /// Nombre de la persona física.
    /// </summary>
    public string? Nombre { get; private set; }

    /// <summary>
    /// Apellido de la persona física.
    /// </summary>
    public string? Apellido { get; private set; }

    /// <summary>
    /// Alias o apodo.
    /// </summary>
    public string? Alias { get; private set; }

    // Datos Comunes
    /// <summary>
    /// Dirección del contratado.
    /// </summary>
    public string? Direccion { get; private set; }

    /// <summary>
    /// Provincia de residencia.
    /// </summary>
    public string? Provincia { get; private set; }

    /// <summary>
    /// Municipio de residencia.
    /// </summary>
    public string? Municipio { get; private set; }

    /// <summary>
    /// Teléfono principal.
    /// </summary>
    public string? Telefono1 { get; private set; }

    /// <summary>
    /// Teléfono secundario.
    /// </summary>
    public string? Telefono2 { get; private set; }

    /// <summary>
    /// URL de la foto o logo.
    /// </summary>
    public string? Foto { get; private set; }

    /// <summary>
    /// Indica si la contratación está activa.
    /// </summary>
    public bool Activo { get; private set; }

    // Constructor privado para EF Core
    private EmpleadoTemporal()
    {
    }

    /// <summary>
    /// Crea una contratación temporal de persona física (sobrecarga simplificada).
    /// Alias para CreatePersonaFisica para compatibilidad con código legacy.
    /// </summary>
    public static EmpleadoTemporal Create(
        string userId,
        string nombre,
        string apellido,
        string identificacion,
        string? telefono = null)
    {
        return CreatePersonaFisica(userId, identificacion, nombre, apellido, telefono);
    }

    /// <summary>
    /// Crea una contratación temporal de persona física.
    /// </summary>
    public static EmpleadoTemporal CreatePersonaFisica(
        string userId,
        string identificacion,
        string nombre,
        string apellido,
        string? telefono1 = null)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("El ID del usuario es requerido", nameof(userId));

        if (string.IsNullOrWhiteSpace(identificacion))
            throw new ArgumentException("La identificación es requerida", nameof(identificacion));

        if (string.IsNullOrWhiteSpace(nombre))
            throw new ArgumentException("El nombre es requerido", nameof(nombre));

        if (string.IsNullOrWhiteSpace(apellido))
            throw new ArgumentException("El apellido es requerido", nameof(apellido));

        var empleado = new EmpleadoTemporal
        {
            UserId = userId,
            Tipo = 1, // Persona Física
            Identificacion = identificacion.Trim(),
            Nombre = nombre.Trim(),
            Apellido = apellido.Trim(),
            Telefono1 = telefono1?.Trim(),
            FechaRegistro = DateTime.UtcNow,
            Activo = true
        };

        empleado.RaiseDomainEvent(new EmpleadoTemporalCreadoEvent(
            empleado.ContratacionId,
            empleado.UserId,
            empleado.ObtenerNombreCompleto(),
            empleado.Tipo));

        return empleado;
    }

    /// <summary>
    /// Crea una contratación temporal de persona jurídica.
    /// </summary>
    public static EmpleadoTemporal CreatePersonaJuridica(
        string userId,
        string nombreComercial,
        string rnc,
        string nombreRepresentante,
        string cedulaRepresentante,
        string? telefono1 = null)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("El ID del usuario es requerido", nameof(userId));

        if (string.IsNullOrWhiteSpace(nombreComercial))
            throw new ArgumentException("El nombre comercial es requerido", nameof(nombreComercial));

        if (string.IsNullOrWhiteSpace(rnc))
            throw new ArgumentException("El RNC es requerido", nameof(rnc));

        if (string.IsNullOrWhiteSpace(nombreRepresentante))
            throw new ArgumentException("El nombre del representante es requerido", nameof(nombreRepresentante));

        if (string.IsNullOrWhiteSpace(cedulaRepresentante))
            throw new ArgumentException("La cédula del representante es requerida", nameof(cedulaRepresentante));

        var empleado = new EmpleadoTemporal
        {
            UserId = userId,
            Tipo = 2, // Persona Jurídica
            NombreComercial = nombreComercial.Trim(),
            Rnc = rnc.Trim(),
            NombreRepresentante = nombreRepresentante.Trim(),
            CedulaRepresentante = cedulaRepresentante.Trim(),
            Telefono1 = telefono1?.Trim(),
            FechaRegistro = DateTime.UtcNow,
            Activo = true
        };

        empleado.RaiseDomainEvent(new EmpleadoTemporalCreadoEvent(
            empleado.ContratacionId,
            empleado.UserId,
            empleado.ObtenerNombreCompleto(),
            empleado.Tipo));

        return empleado;
    }

    /// <summary>
    /// Obtiene el nombre completo según el tipo de contratación.
    /// </summary>
    public string ObtenerNombreCompleto()
    {
        return Tipo switch
        {
            1 => $"{Nombre} {Apellido}",
            2 => NombreComercial ?? "Sin nombre",
            _ => "Desconocido"
        };
    }

    /// <summary>
    /// Actualiza la dirección del empleado temporal.
    /// </summary>
    public void ActualizarDireccion(string? direccion, string? provincia, string? municipio)
    {
        Direccion = direccion?.Trim();
        Provincia = provincia?.Trim();
        Municipio = municipio?.Trim();
    }

    /// <summary>
    /// Actualiza los teléfonos de contacto.
    /// </summary>
    public void ActualizarTelefonos(string? telefono1, string? telefono2)
    {
        Telefono1 = telefono1?.Trim();
        Telefono2 = telefono2?.Trim();
    }

    /// <summary>
    /// Actualiza el alias del empleado temporal.
    /// </summary>
    public void ActualizarAlias(string? alias)
    {
        Alias = alias?.Trim();
    }

    /// <summary>
    /// Actualiza la foto/logo.
    /// </summary>
    public void ActualizarFoto(string? urlFoto)
    {
        Foto = urlFoto?.Trim();
    }

    /// <summary>
    /// Desactiva la contratación temporal.
    /// </summary>
    public void Desactivar()
    {
        if (!Activo)
            throw new InvalidOperationException("La contratación temporal ya está inactiva");

        Activo = false;
        RaiseDomainEvent(new EmpleadoTemporalDesactivadoEvent(
            ContratacionId,
            UserId,
            ObtenerNombreCompleto(),
            DateTime.UtcNow));
    }

    /// <summary>
    /// Reactiva la contratación temporal.
    /// </summary>
    public void Reactivar()
    {
        if (Activo)
            throw new InvalidOperationException("La contratación temporal ya está activa");

        Activo = true;
    }

    /// <summary>
    /// Valida si es una persona física.
    /// </summary>
    public bool EsPersonaFisica() => Tipo == 1;

    /// <summary>
    /// Valida si es una persona jurídica.
    /// </summary>
    public bool EsPersonaJuridica() => Tipo == 2;

    /// <summary>
    /// Obtiene el identificador principal (Identificación para física, RNC para jurídica).
    /// </summary>
    public string? ObtenerIdentificadorPrincipal()
    {
        return Tipo == 1 ? Identificacion : Rnc;
    }

    /// <summary>
    /// Actualiza los datos de persona física.
    /// </summary>
    public void ActualizarDatosPersonaFisica(string nombre, string apellido, string identificacion)
    {
        if (Tipo != 1)
            throw new InvalidOperationException("Solo se puede actualizar datos de persona física en contrataciones de tipo 1");

        if (string.IsNullOrWhiteSpace(nombre))
            throw new ArgumentException("El nombre es requerido", nameof(nombre));

        if (string.IsNullOrWhiteSpace(apellido))
            throw new ArgumentException("El apellido es requerido", nameof(apellido));

        if (string.IsNullOrWhiteSpace(identificacion))
            throw new ArgumentException("La identificación es requerida", nameof(identificacion));

        Nombre = nombre.Trim();
        Apellido = apellido.Trim();
        Identificacion = identificacion.Trim();
    }

    /// <summary>
    /// Actualiza los datos de persona jurídica.
    /// </summary>
    public void ActualizarDatosPersonaJuridica(
        string nombreComercial,
        string rnc,
        string nombreRepresentante,
        string cedulaRepresentante)
    {
        if (Tipo != 2)
            throw new InvalidOperationException("Solo se puede actualizar datos de persona jurídica en contrataciones de tipo 2");

        if (string.IsNullOrWhiteSpace(nombreComercial))
            throw new ArgumentException("El nombre comercial es requerido", nameof(nombreComercial));

        if (string.IsNullOrWhiteSpace(rnc))
            throw new ArgumentException("El RNC es requerido", nameof(rnc));

        if (string.IsNullOrWhiteSpace(nombreRepresentante))
            throw new ArgumentException("El nombre del representante es requerido", nameof(nombreRepresentante));

        if (string.IsNullOrWhiteSpace(cedulaRepresentante))
            throw new ArgumentException("La cédula del representante es requerida", nameof(cedulaRepresentante));

        NombreComercial = nombreComercial.Trim();
        Rnc = rnc.Trim();
        NombreRepresentante = nombreRepresentante.Trim();
        CedulaRepresentante = cedulaRepresentante.Trim();
    }
}
