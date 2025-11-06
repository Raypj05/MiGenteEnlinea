using MiGenteEnLinea.Domain.Common;
using MiGenteEnLinea.Domain.Events.Pagos;

namespace MiGenteEnLinea.Domain.Entities.Pagos;

/// <summary>
/// Aggregate Root que representa el encabezado de un recibo de pago por contratación de servicios
/// 
/// Esta entidad gestiona los pagos realizados a contratistas por servicios prestados.
/// Contiene información del pago (fechas, concepto, tipo) y puede tener múltiples líneas de detalle.
/// 
/// Flujo típico:
/// 1. Se crea el header con información básica del pago
/// 2. Se registra la fecha de pago cuando se procesa
/// 3. Se asocia a una contratación específica (EmpleadoTemporal)
/// 4. Se agregan líneas de detalle con conceptos y montos específicos
/// </summary>
public class EmpleadorRecibosHeaderContratacione : AggregateRoot
{
    /// <summary>
    /// Identificador único del recibo de pago
    /// </summary>
    public int PagoId { get; private set; }

    /// <summary>
    /// ID del usuario empleador que realiza el pago
    /// </summary>
    public string UserId { get; private set; } = string.Empty;

    /// <summary>
    /// ID de la contratación asociada (FK a EmpleadosTemporales)
    /// </summary>
    public int? ContratacionId { get; private set; }

    /// <summary>
    /// Fecha de registro del recibo en el sistema
    /// </summary>
    public DateTime? FechaRegistro { get; private set; }

    /// <summary>
    /// Fecha en que se realizó el pago
    /// </summary>
    public DateTime? FechaPago { get; private set; }

    /// <summary>
    /// Concepto o descripción del pago
    /// </summary>
    public string? ConceptoPago { get; private set; }

    /// <summary>
    /// Tipo de pago (1 = Pago único, 2 = Pago recurrente, 3 = Adelanto, 4 = Liquidación final)
    /// </summary>
    public int? Tipo { get; private set; }

    // Constructor privado para EF Core
    private EmpleadorRecibosHeaderContratacione() { }

    /// <summary>
    /// Constructor privado para creación controlada
    /// </summary>
    private EmpleadorRecibosHeaderContratacione(
        string userId,
        int? contratacionId,
        string? conceptoPago,
        int? tipo)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("El ID de usuario es requerido", nameof(userId));

        if (!string.IsNullOrWhiteSpace(conceptoPago) && conceptoPago.Length > 50)
            throw new ArgumentException("El concepto de pago no puede exceder 50 caracteres", nameof(conceptoPago));

        UserId = userId;
        ContratacionId = contratacionId;
        FechaRegistro = DateTime.UtcNow;
        ConceptoPago = conceptoPago;
        Tipo = tipo;
    }

    /// <summary>
    /// Crea un nuevo recibo de pago pendiente (sobrecarga simplificada con fecha de pago)
    /// Permite fechas históricas para importación de datos legacy.
    /// </summary>
    public static EmpleadorRecibosHeaderContratacione Create(
        int contratacionId,
        string userId,
        DateTime fechaPago,
        string conceptoPago)
    {
        var recibo = new EmpleadorRecibosHeaderContratacione(userId, contratacionId, conceptoPago, 1);
        
        // Para fechas históricas (importación legacy), establecer FechaRegistro = FechaPago
        // Esto evita validación que rechaza fechas pasadas
        if (fechaPago < DateTime.UtcNow)
        {
            recibo.FechaRegistro = fechaPago;
        }
        
        recibo.FechaPago = fechaPago; // Asignar directamente sin validación estricta

        recibo.RaiseDomainEvent(new ReciboContratacionCreadoEvent(
            recibo.PagoId,
            recibo.UserId,
            recibo.ContratacionId,
            recibo.ConceptoPago,
            recibo.Tipo));

        return recibo;
    }

    /// <summary>
    /// Crea un nuevo recibo de pago pendiente
    /// </summary>
    public static EmpleadorRecibosHeaderContratacione Crear(
        string userId,
        int contratacionId,
        string conceptoPago,
        int tipo)
    {
        var recibo = new EmpleadorRecibosHeaderContratacione(userId, contratacionId, conceptoPago, tipo);

        recibo.RaiseDomainEvent(new ReciboContratacionCreadoEvent(
            recibo.PagoId,
            recibo.UserId,
            recibo.ContratacionId,
            recibo.ConceptoPago,
            recibo.Tipo));

        return recibo;
    }

    /// <summary>
    /// Crea un recibo sin asociación a contratación específica (pago general)
    /// </summary>
    public static EmpleadorRecibosHeaderContratacione CrearPagoGeneral(
        string userId,
        string conceptoPago)
    {
        var recibo = new EmpleadorRecibosHeaderContratacione(userId, null, conceptoPago, 1);

        recibo.RaiseDomainEvent(new ReciboContratacionCreadoEvent(
            recibo.PagoId,
            recibo.UserId,
            recibo.ContratacionId,
            recibo.ConceptoPago,
            recibo.Tipo));

        return recibo;
    }

    /// <summary>
    /// Registra la fecha en que se realizó el pago
    /// </summary>
    public void RegistrarFechaPago(DateTime fechaPago)
    {
        if (fechaPago > DateTime.UtcNow)
            throw new InvalidOperationException("La fecha de pago no puede ser futura");

        if (FechaRegistro.HasValue && fechaPago < FechaRegistro.Value)
            throw new InvalidOperationException("La fecha de pago no puede ser anterior a la fecha de registro");

        var fechaAnterior = FechaPago;
        FechaPago = fechaPago;

        RaiseDomainEvent(new FechaPagoRegistradaEvent(
            PagoId,
            fechaAnterior,
            fechaPago));
    }

    /// <summary>
    /// Actualiza el concepto del pago
    /// </summary>
    public void ActualizarConcepto(string nuevoConcepto)
    {
        if (!string.IsNullOrWhiteSpace(nuevoConcepto) && nuevoConcepto.Length > 50)
            throw new ArgumentException("El concepto no puede exceder 50 caracteres", nameof(nuevoConcepto));

        var conceptoAnterior = ConceptoPago;
        ConceptoPago = nuevoConcepto;

        RaiseDomainEvent(new ConceptoPagoActualizadoEvent(
            PagoId,
            conceptoAnterior,
            nuevoConcepto));
    }

    /// <summary>
    /// Actualiza el tipo de pago
    /// </summary>
    public void ActualizarTipoPago(int nuevoTipo)
    {
        if (nuevoTipo < 1 || nuevoTipo > 4)
            throw new ArgumentException("Tipo de pago inválido. Debe ser 1-4", nameof(nuevoTipo));

        var tipoAnterior = Tipo;
        Tipo = nuevoTipo;

        RaiseDomainEvent(new TipoPagoActualizadoEvent(
            PagoId,
            tipoAnterior,
            nuevoTipo));
    }

    /// <summary>
    /// Asocia el recibo a una contratación específica
    /// </summary>
    public void AsociarContratacion(int contratacionId)
    {
        if (contratacionId <= 0)
            throw new ArgumentException("ID de contratación inválido", nameof(contratacionId));

        var contratacionAnterior = ContratacionId;
        ContratacionId = contratacionId;

        RaiseDomainEvent(new ContratacionAsociadaEvent(
            PagoId,
            contratacionAnterior,
            contratacionId));
    }

    /// <summary>
    /// Verifica si el pago ya fue registrado (tiene fecha de pago)
    /// </summary>
    public bool EstaPagado() => FechaPago.HasValue;

    /// <summary>
    /// Verifica si el pago está asociado a una contratación
    /// </summary>
    public bool TieneContratacion() => ContratacionId.HasValue;

    /// <summary>
    /// Obtiene la descripción del tipo de pago
    /// </summary>
    public string ObtenerDescripcionTipo()
    {
        return Tipo switch
        {
            1 => "Pago único",
            2 => "Pago recurrente",
            3 => "Adelanto",
            4 => "Liquidación final",
            _ => "No especificado"
        };
    }

    /// <summary>
    /// Verifica si el recibo está completo (tiene todas las fechas y concepto)
    /// </summary>
    public bool EstaCompleto()
    {
        return FechaRegistro.HasValue &&
               FechaPago.HasValue &&
               !string.IsNullOrWhiteSpace(ConceptoPago) &&
               Tipo.HasValue;
    }
}
