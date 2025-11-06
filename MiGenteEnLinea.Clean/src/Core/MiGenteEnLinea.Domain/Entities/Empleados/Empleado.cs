using MiGenteEnLinea.Domain.Common;
using MiGenteEnLinea.Domain.Events.Empleados;

namespace MiGenteEnLinea.Domain.Entities.Empleados;

/// <summary>
/// Entidad que representa un empleado de un empleador.
/// Es un Aggregate Root que gestiona toda la información del empleado incluyendo
/// datos personales, laborales, salariales y contractuales.
/// </summary>
public sealed class Empleado : AggregateRoot
{
    /// <summary>
    /// Identificador único del empleado.
    /// </summary>
    public int EmpleadoId { get; private set; }

    /// <summary>
    /// Identificador del empleador (UserId del sistema).
    /// </summary>
    public string UserId { get; private set; } = null!;

    /// <summary>
    /// Fecha de registro del empleado en el sistema.
    /// </summary>
    public DateTime FechaRegistro { get; private set; }

    /// <summary>
    /// Fecha de inicio de labores del empleado.
    /// </summary>
    public DateOnly? FechaInicio { get; private set; }

    /// <summary>
    /// Número de identificación (cédula dominicana 11 dígitos o pasaporte).
    /// </summary>
    public string Identificacion { get; private set; } = null!;

    /// <summary>
    /// Nombre(s) del empleado.
    /// </summary>
    public string Nombre { get; private set; } = null!;

    /// <summary>
    /// Apellido(s) del empleado.
    /// </summary>
    public string Apellido { get; private set; } = null!;

    /// <summary>
    /// Alias o apodo del empleado (opcional).
    /// </summary>
    public string? Alias { get; private set; }

    /// <summary>
    /// Fecha de nacimiento del empleado.
    /// </summary>
    public DateOnly? Nacimiento { get; private set; }

    /// <summary>
    /// Estado civil del empleado (1=Soltero, 2=Casado, 3=Divorciado, 4=Viudo, 5=Unión Libre).
    /// </summary>
    public int? EstadoCivil { get; private set; }

    /// <summary>
    /// Dirección de residencia del empleado.
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
    /// Teléfono principal del empleado.
    /// </summary>
    public string? Telefono1 { get; private set; }

    /// <summary>
    /// Teléfono secundario del empleado.
    /// </summary>
    public string? Telefono2 { get; private set; }

    /// <summary>
    /// Posición o cargo del empleado en la empresa.
    /// </summary>
    public string? Posicion { get; private set; }

    /// <summary>
    /// Salario base del empleado.
    /// </summary>
    public decimal Salario { get; private set; }

    /// <summary>
    /// Período de pago (1=Semanal, 2=Quincenal, 3=Mensual).
    /// </summary>
    public int PeriodoPago { get; private set; }

    /// <summary>
    /// Nombre del contacto de emergencia.
    /// </summary>
    public string? ContactoEmergencia { get; private set; }

    /// <summary>
    /// Teléfono del contacto de emergencia.
    /// </summary>
    public string? TelefonoEmergencia { get; private set; }

    /// <summary>
    /// Indica si el empleado tiene contrato firmado.
    /// </summary>
    public bool TieneContrato { get; private set; }

    /// <summary>
    /// Descripción de la primera remuneración extra (bonos, comisiones, etc.).
    /// </summary>
    public string? RemuneracionExtra1 { get; private set; }

    /// <summary>
    /// Monto de la primera remuneración extra.
    /// </summary>
    public decimal? MontoExtra1 { get; private set; }

    /// <summary>
    /// Descripción de la segunda remuneración extra.
    /// </summary>
    public string? RemuneracionExtra2 { get; private set; }

    /// <summary>
    /// Monto de la segunda remuneración extra.
    /// </summary>
    public decimal? MontoExtra2 { get; private set; }

    /// <summary>
    /// Descripción de la tercera remuneración extra.
    /// </summary>
    public string? RemuneracionExtra3 { get; private set; }

    /// <summary>
    /// Monto de la tercera remuneración extra.
    /// </summary>
    public decimal? MontoExtra3 { get; private set; }

    /// <summary>
    /// Indica si el empleado está inscrito en el TSS (Tesorería de Seguridad Social).
    /// </summary>
    public bool InscritoTss { get; private set; }

    /// <summary>
    /// Cantidad de días a pagar en el período (para cálculos proporcionales).
    /// </summary>
    public int? DiasPago { get; private set; }

    /// <summary>
    /// Indica si el empleado está activo en la empresa.
    /// </summary>
    public bool Activo { get; private set; }

    /// <summary>
    /// Fecha de salida del empleado (cuando es dado de baja).
    /// </summary>
    public DateTime? FechaSalida { get; private set; }

    /// <summary>
    /// Motivo de baja del empleado (renuncia, despido, etc.).
    /// </summary>
    public string? MotivoBaja { get; private set; }

    /// <summary>
    /// Monto de prestaciones laborales calculadas.
    /// </summary>
    public decimal? Prestaciones { get; private set; }

    /// <summary>
    /// URL de la foto del empleado.
    /// </summary>
    public string? Foto { get; private set; }

    // Constructor privado para EF Core
    private Empleado()
    {
    }

    /// <summary>
    /// Crea un nuevo empleado.
    /// </summary>
    /// <param name="userId">Identificador del empleador.</param>
    /// <param name="identificacion">Número de cédula o pasaporte.</param>
    /// <param name="nombre">Nombre del empleado.</param>
    /// <param name="apellido">Apellido del empleado.</param>
    /// <param name="salario">Salario base.</param>
    /// <param name="periodoPago">Período de pago (1-3).</param>
    /// <returns>Nueva instancia de Empleado.</returns>
    public static Empleado Create(
        string userId,
        string identificacion,
        string nombre,
        string apellido,
        decimal salario,
        int periodoPago)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("El ID del empleador es requerido", nameof(userId));

        if (string.IsNullOrWhiteSpace(identificacion))
            throw new ArgumentException("La identificación es requerida", nameof(identificacion));

        if (string.IsNullOrWhiteSpace(nombre))
            throw new ArgumentException("El nombre es requerido", nameof(nombre));

        if (string.IsNullOrWhiteSpace(apellido))
            throw new ArgumentException("El apellido es requerido", nameof(apellido));

        if (salario <= 0)
            throw new ArgumentException("El salario debe ser mayor a cero", nameof(salario));

        if (periodoPago < 1 || periodoPago > 3)
            throw new ArgumentException("El período de pago debe ser 1 (Semanal), 2 (Quincenal) o 3 (Mensual)", nameof(periodoPago));

        var empleado = new Empleado
        {
            UserId = userId,
            Identificacion = identificacion.Trim(),
            Nombre = nombre.Trim(),
            Apellido = apellido.Trim(),
            Salario = salario,
            PeriodoPago = periodoPago,
            FechaRegistro = DateTime.UtcNow,
            Activo = true,
            TieneContrato = false,
            InscritoTss = false
        };

        empleado.RaiseDomainEvent(new EmpleadoCreadoEvent(
            empleado.EmpleadoId,
            empleado.UserId,
            empleado.NombreCompleto,
            empleado.Identificacion));

        return empleado;
    }

    /// <summary>
    /// Nombre completo del empleado (Nombre + Apellido).
    /// </summary>
    public string NombreCompleto => $"{Nombre} {Apellido}";

    /// <summary>
    /// Actualiza la información personal del empleado.
    /// </summary>
    public void ActualizarInformacionPersonal(
        string nombre,
        string apellido,
        DateOnly? nacimiento,
        int? estadoCivil,
        string? alias = null)
    {
        if (string.IsNullOrWhiteSpace(nombre))
            throw new ArgumentException("El nombre es requerido", nameof(nombre));

        if (string.IsNullOrWhiteSpace(apellido))
            throw new ArgumentException("El apellido es requerido", nameof(apellido));

        if (estadoCivil.HasValue && (estadoCivil < 1 || estadoCivil > 5))
            throw new ArgumentException("El estado civil debe estar entre 1 y 5", nameof(estadoCivil));

        Nombre = nombre.Trim();
        Apellido = apellido.Trim();
        Nacimiento = nacimiento;
        EstadoCivil = estadoCivil;
        Alias = alias?.Trim();
    }

    /// <summary>
    /// Actualiza la dirección del empleado.
    /// </summary>
    public void ActualizarDireccion(string? direccion, string? provincia, string? municipio)
    {
        Direccion = direccion?.Trim();
        Provincia = provincia?.Trim();
        Municipio = municipio?.Trim();
    }

    /// <summary>
    /// Actualiza la información de contacto del empleado.
    /// </summary>
    public void ActualizarContacto(
        string? telefono1,
        string? telefono2,
        string? contactoEmergencia,
        string? telefonoEmergencia)
    {
        Telefono1 = telefono1?.Trim();
        Telefono2 = telefono2?.Trim();
        ContactoEmergencia = contactoEmergencia?.Trim();
        TelefonoEmergencia = telefonoEmergencia?.Trim();
    }

    /// <summary>
    /// Actualiza la posición y salario del empleado.
    /// </summary>
    public void ActualizarPosicion(string? posicion, decimal salario, int periodoPago)
    {
        if (salario <= 0)
            throw new ArgumentException("El salario debe ser mayor a cero", nameof(salario));

        if (periodoPago < 1 || periodoPago > 3)
            throw new ArgumentException("El período de pago debe ser 1, 2 o 3", nameof(periodoPago));

        var salarioAnterior = Salario;
        Posicion = posicion?.Trim();
        Salario = salario;
        PeriodoPago = periodoPago;

        if (salarioAnterior != salario)
        {
            RaiseDomainEvent(new SalarioActualizadoEvent(
                EmpleadoId,
                UserId,
                NombreCompleto,
                salarioAnterior,
                salario,
                DateTime.UtcNow));
        }
    }

    /// <summary>
    /// Agrega una remuneración extra al empleado (bono, comisión, etc.).
    /// </summary>
    public void AgregarRemuneracionExtra(int numero, string descripcion, decimal monto)
    {
        if (numero < 1 || numero > 3)
            throw new ArgumentException("El número de remuneración extra debe ser 1, 2 o 3", nameof(numero));

        if (string.IsNullOrWhiteSpace(descripcion))
            throw new ArgumentException("La descripción es requerida", nameof(descripcion));

        if (monto < 0)
            throw new ArgumentException("El monto no puede ser negativo", nameof(monto));

        switch (numero)
        {
            case 1:
                RemuneracionExtra1 = descripcion.Trim();
                MontoExtra1 = monto;
                break;
            case 2:
                RemuneracionExtra2 = descripcion.Trim();
                MontoExtra2 = monto;
                break;
            case 3:
                RemuneracionExtra3 = descripcion.Trim();
                MontoExtra3 = monto;
                break;
        }
    }

    /// <summary>
    /// Elimina una remuneración extra.
    /// </summary>
    public void EliminarRemuneracionExtra(int numero)
    {
        if (numero < 1 || numero > 3)
            throw new ArgumentException("El número debe ser 1, 2 o 3", nameof(numero));

        switch (numero)
        {
            case 1:
                RemuneracionExtra1 = null;
                MontoExtra1 = null;
                break;
            case 2:
                RemuneracionExtra2 = null;
                MontoExtra2 = null;
                break;
            case 3:
                RemuneracionExtra3 = null;
                MontoExtra3 = null;
                break;
        }
    }

    /// <summary>
    /// Marca el contrato como firmado.
    /// </summary>
    public void MarcarContratoFirmado()
    {
        if (TieneContrato)
            return;

        TieneContrato = true;
        RaiseDomainEvent(new ContratoFirmadoEvent(EmpleadoId, UserId, NombreCompleto, DateTime.UtcNow));
    }

    /// <summary>
    /// Inscribe el empleado en el TSS.
    /// </summary>
    public void InscribirEnTss()
    {
        if (InscritoTss)
            return;

        InscritoTss = true;
    }

    /// <summary>
    /// Desactiva el empleado (baja).
    /// </summary>
    public void Desactivar(string motivoBaja, decimal? prestaciones = null)
    {
        DarDeBaja(DateTime.UtcNow, motivoBaja, prestaciones);
    }

    /// <summary>
    /// Desactiva el empleado estableciendo una fecha de salida explícita.
    /// </summary>
    public void DarDeBaja(DateTime fechaBaja, string motivoBaja, decimal? prestaciones = null)
    {
        if (!Activo)
            throw new InvalidOperationException("El empleado ya está inactivo");

        if (fechaBaja == default)
            throw new ArgumentException("La fecha de baja es requerida", nameof(fechaBaja));

        if (string.IsNullOrWhiteSpace(motivoBaja))
            throw new ArgumentException("El motivo de baja es requerido", nameof(motivoBaja));

        Activo = false;
        FechaSalida = fechaBaja;
        MotivoBaja = motivoBaja.Trim();
        Prestaciones = prestaciones;

        RaiseDomainEvent(new EmpleadoDesactivadoEvent(
            EmpleadoId,
            UserId,
            NombreCompleto,
            FechaSalida.Value,
            MotivoBaja));
    }

    /// <summary>
    /// Reactiva el empleado.
    /// </summary>
    public void Reactivar()
    {
        if (Activo)
            throw new InvalidOperationException("El empleado ya está activo");

        Activo = true;
        FechaSalida = null;
        MotivoBaja = null;
        Prestaciones = null;
    }

    /// <summary>
    /// Actualiza la fecha de inicio de labores.
    /// </summary>
    public void ActualizarFechaInicio(DateOnly fechaInicio)
    {
        if (fechaInicio > DateOnly.FromDateTime(DateTime.UtcNow))
            throw new ArgumentException("La fecha de inicio no puede ser futura", nameof(fechaInicio));

        FechaInicio = fechaInicio;
    }

    /// <summary>
    /// Calcula el salario mensual del empleado según el período de pago.
    /// </summary>
    public decimal CalcularSalarioMensual()
    {
        return PeriodoPago switch
        {
            1 => Salario * 4, // Semanal (aproximado)
            2 => Salario * 2, // Quincenal
            3 => Salario,     // Mensual
            _ => Salario
        };
    }

    /// <summary>
    /// Calcula el total de remuneraciones extras.
    /// </summary>
    public decimal CalcularTotalExtras()
    {
        return (MontoExtra1 ?? 0) + (MontoExtra2 ?? 0) + (MontoExtra3 ?? 0);
    }

    /// <summary>
    /// Calcula la edad del empleado en años.
    /// </summary>
    public int? CalcularEdad()
    {
        if (!Nacimiento.HasValue)
            return null;

        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);
        var edad = hoy.Year - Nacimiento.Value.Year;

        if (Nacimiento.Value > hoy.AddYears(-edad))
            edad--;

        return edad;
    }

    /// <summary>
    /// Calcula la antigüedad del empleado en años.
    /// </summary>
    public int? CalcularAntiguedad()
    {
        if (!FechaInicio.HasValue)
            return null;

        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);
        var antiguedad = hoy.Year - FechaInicio.Value.Year;

        if (FechaInicio.Value > hoy.AddYears(-antiguedad))
            antiguedad--;

        return antiguedad;
    }

    /// <summary>
    /// Actualiza la foto del empleado.
    /// </summary>
    public void ActualizarFoto(string? urlFoto)
    {
        Foto = urlFoto?.Trim();
    }

    /// <summary>
    /// Actualiza los días de pago del período actual.
    /// </summary>
    public void ActualizarDiasPago(int diasPago)
    {
        if (diasPago < 0)
            throw new ArgumentException("Los días de pago no pueden ser negativos", nameof(diasPago));

        DiasPago = diasPago;
    }
}
