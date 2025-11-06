using MiGenteEnLinea.Domain.Common;
using MiGenteEnLinea.Domain.Events.Pagos;

namespace MiGenteEnLinea.Domain.Entities.Pagos;

/// <summary>
/// Entidad que representa una línea de detalle en un recibo de pago por contratación
/// 
/// Esta entidad NO es un Aggregate Root, pertenece al agregado de EmpleadorRecibosHeaderContratacione.
/// Cada línea contiene un concepto específico del pago (ej: "Horas trabajadas", "Materiales", "Transporte")
/// y el monto asociado a ese concepto.
/// 
/// Ejemplos de uso:
/// - Recibo con múltiples conceptos: Servicios profesionales $5000, Materiales $500, Transporte $200
/// - Recibo de nómina: Salario base $3000, Bono $500, Horas extras $400
/// - Recibo de proyecto: Entregable 1 $2000, Entregable 2 $3000
/// </summary>
public class EmpleadorRecibosDetalleContratacione : AggregateRoot
{
    /// <summary>
    /// Identificador único de la línea de detalle
    /// </summary>
    public int DetalleId { get; private set; }

    /// <summary>
    /// ID del recibo de pago al que pertenece esta línea (FK)
    /// </summary>
    public int? PagoId { get; private set; }

    /// <summary>
    /// Concepto o descripción de esta línea del pago
    /// </summary>
    public string? Concepto { get; private set; }

    /// <summary>
    /// Monto de esta línea del pago
    /// </summary>
    public decimal? Monto { get; private set; }

    // Constructor privado para EF Core
    private EmpleadorRecibosDetalleContratacione() { }

    /// <summary>
    /// Constructor privado para creación controlada
    /// </summary>
    private EmpleadorRecibosDetalleContratacione(
        int? pagoId,
        string? concepto,
        decimal? monto)
    {
        if (!string.IsNullOrWhiteSpace(concepto) && concepto.Length > 90)
            throw new ArgumentException("El concepto no puede exceder 90 caracteres", nameof(concepto));

        if (monto.HasValue && monto.Value < 0)
            throw new ArgumentException("El monto no puede ser negativo", nameof(monto));

        if (monto.HasValue && monto.Value > 999999999.99m)
            throw new ArgumentException("El monto excede el máximo permitido (999,999,999.99)", nameof(monto));

        PagoId = pagoId;
        Concepto = concepto;
        Monto = monto;
    }

    /// <summary>
    /// Crea una nueva línea de detalle para un recibo (sobrecarga simplificada)
    /// </summary>
    public static EmpleadorRecibosDetalleContratacione Create(
        int pagoId,
        string concepto,
        decimal monto)
    {
        return Crear(pagoId, concepto, monto);
    }

    /// <summary>
    /// Crea una nueva línea de detalle para un recibo
    /// </summary>
    public static EmpleadorRecibosDetalleContratacione Crear(
        int pagoId,
        string concepto,
        decimal monto)
    {
        if (pagoId <= 0)
            throw new ArgumentException("El ID del pago debe ser mayor a cero", nameof(pagoId));

        if (string.IsNullOrWhiteSpace(concepto))
            throw new ArgumentException("El concepto es requerido", nameof(concepto));

        var detalle = new EmpleadorRecibosDetalleContratacione(pagoId, concepto, monto);

        detalle.RaiseDomainEvent(new DetalleReciboAgregadoEvent(
            detalle.DetalleId,
            detalle.PagoId,
            detalle.Concepto,
            detalle.Monto));

        return detalle;
    }

    /// <summary>
    /// Crea una línea de detalle sin monto (por definir)
    /// </summary>
    public static EmpleadorRecibosDetalleContratacione CrearSinMonto(
        int pagoId,
        string concepto)
    {
        if (pagoId <= 0)
            throw new ArgumentException("El ID del pago debe ser mayor a cero", nameof(pagoId));

        if (string.IsNullOrWhiteSpace(concepto))
            throw new ArgumentException("El concepto es requerido", nameof(concepto));

        var detalle = new EmpleadorRecibosDetalleContratacione(pagoId, concepto, null);

        detalle.RaiseDomainEvent(new DetalleReciboAgregadoEvent(
            detalle.DetalleId,
            detalle.PagoId,
            detalle.Concepto,
            detalle.Monto));

        return detalle;
    }

    /// <summary>
    /// Actualiza el concepto de la línea de detalle
    /// </summary>
    public void ActualizarConcepto(string nuevoConcepto)
    {
        if (string.IsNullOrWhiteSpace(nuevoConcepto))
            throw new ArgumentException("El concepto es requerido", nameof(nuevoConcepto));

        if (nuevoConcepto.Length > 90)
            throw new ArgumentException("El concepto no puede exceder 90 caracteres", nameof(nuevoConcepto));

        var conceptoAnterior = Concepto;
        Concepto = nuevoConcepto;

        RaiseDomainEvent(new DetalleReciboActualizadoEvent(
            DetalleId,
            PagoId,
            "Concepto",
            conceptoAnterior,
            nuevoConcepto));
    }

    /// <summary>
    /// Actualiza el monto de la línea de detalle
    /// </summary>
    public void ActualizarMonto(decimal nuevoMonto)
    {
        if (nuevoMonto < 0)
            throw new ArgumentException("El monto no puede ser negativo", nameof(nuevoMonto));

        if (nuevoMonto > 999999999.99m)
            throw new ArgumentException("El monto excede el máximo permitido (999,999,999.99)", nameof(nuevoMonto));

        var montoAnterior = Monto;
        Monto = nuevoMonto;

        RaiseDomainEvent(new MontoDetalleActualizadoEvent(
            DetalleId,
            PagoId,
            montoAnterior,
            nuevoMonto));
    }

    /// <summary>
    /// Asocia el detalle a un recibo específico
    /// </summary>
    public void AsociarAPago(int pagoId)
    {
        if (pagoId <= 0)
            throw new ArgumentException("El ID del pago debe ser mayor a cero", nameof(pagoId));

        var pagoAnterior = PagoId;
        PagoId = pagoId;

        RaiseDomainEvent(new DetalleReciboActualizadoEvent(
            DetalleId,
            PagoId,
            "PagoId",
            pagoAnterior?.ToString(),
            pagoId.ToString()));
    }

    /// <summary>
    /// Verifica si el detalle tiene monto definido
    /// </summary>
    public bool TieneMonto() => Monto.HasValue && Monto.Value > 0;

    /// <summary>
    /// Verifica si el detalle tiene concepto definido
    /// </summary>
    public bool TieneConcepto() => !string.IsNullOrWhiteSpace(Concepto);

    /// <summary>
    /// Obtiene el monto formateado con símbolo de moneda
    /// </summary>
    public string ObtenerMontoFormateado()
    {
        if (!Monto.HasValue)
            return "No especificado";

        return $"${Monto.Value:N2}";
    }

    /// <summary>
    /// Verifica si el detalle está completo (tiene concepto y monto)
    /// </summary>
    public bool EstaCompleto()
    {
        return TieneConcepto() && TieneMonto() && PagoId.HasValue;
    }
}
