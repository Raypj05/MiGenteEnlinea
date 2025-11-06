using MiGenteEnLinea.Domain.Common;

namespace MiGenteEnLinea.Domain.Entities.Nominas;

/// <summary>
/// Entidad que representa el detalle (línea) de un recibo de pago.
/// Contiene el concepto y monto de cada ítem del recibo.
/// No es un Aggregate Root, es parte del aggregate ReciboHeader.
/// </summary>
public sealed class ReciboDetalle : AuditableEntity
{
    /// <summary>
    /// Identificador único del detalle del recibo.
    /// </summary>
    public int DetalleId { get; private set; }

    /// <summary>
    /// Identificador del recibo header al que pertenece este detalle.
    /// </summary>
    public int PagoId { get; private set; }

    /// <summary>
    /// Concepto o descripción del ítem (ej: "Salario Base", "Bono Productividad", "Deducción AFP").
    /// </summary>
    public string Concepto { get; private set; } = null!;

    /// <summary>
    /// Monto del concepto. Positivo para ingresos, negativo para deducciones.
    /// </summary>
    public decimal Monto { get; private set; }

    /// <summary>
    /// Tipo de concepto (1=Ingreso, 2=Deducción).
    /// </summary>
    public int TipoConcepto { get; private set; }

    /// <summary>
    /// Orden de presentación en el recibo.
    /// </summary>
    public int? Orden { get; private set; }

    // Constructor privado para EF Core
    private ReciboDetalle()
    {
    }

    /// <summary>
    /// Crea una nueva línea de detalle para un recibo (sobrecarga simplificada).
    /// Infiere automáticamente si es ingreso (monto positivo) o deducción (monto negativo).
    /// </summary>
    /// <param name="pagoId">ID del recibo header.</param>
    /// <param name="concepto">Descripción del concepto.</param>
    /// <param name="monto">Monto del concepto (positivo=ingreso, negativo=deducción).</param>
    /// <returns>Nueva instancia de ReciboDetalle.</returns>
    public static ReciboDetalle Create(
        int pagoId,
        string concepto,
        decimal monto)
    {
        if (pagoId <= 0)
            throw new ArgumentException("El ID del pago debe ser mayor a cero", nameof(pagoId));

        if (string.IsNullOrWhiteSpace(concepto))
            throw new ArgumentException("El concepto es requerido", nameof(concepto));

        if (concepto.Length > 90)
            throw new ArgumentException("El concepto no puede exceder 90 caracteres", nameof(concepto));

        // Inferir tipo de concepto basado en el signo del monto
        var tipoConcepto = monto >= 0 ? 1 : 2; // 1=Ingreso, 2=Deducción

        var detalle = new ReciboDetalle
        {
            PagoId = pagoId,
            Concepto = concepto.Trim(),
            Monto = monto,
            TipoConcepto = tipoConcepto,
            Orden = null
        };

        return detalle;
    }

    /// <summary>
    /// Crea una nueva línea de detalle para un recibo con tipo explícito.
    /// </summary>
    /// <param name="pagoId">ID del recibo header.</param>
    /// <param name="concepto">Descripción del concepto.</param>
    /// <param name="monto">Monto del concepto.</param>
    /// <param name="tipoConcepto">Tipo (1=Ingreso, 2=Deducción).</param>
    /// <param name="orden">Orden de presentación.</param>
    /// <returns>Nueva instancia de ReciboDetalle.</returns>
    public static ReciboDetalle CreateWithType(
        int pagoId,
        string concepto,
        decimal monto,
        int tipoConcepto,
        int? orden = null)
    {
        if (pagoId <= 0)
            throw new ArgumentException("El ID del pago debe ser mayor a cero", nameof(pagoId));

        if (string.IsNullOrWhiteSpace(concepto))
            throw new ArgumentException("El concepto es requerido", nameof(concepto));

        if (concepto.Length > 90)
            throw new ArgumentException("El concepto no puede exceder 90 caracteres", nameof(concepto));

        if (tipoConcepto < 1 || tipoConcepto > 2)
            throw new ArgumentException("El tipo de concepto debe ser 1 (Ingreso) o 2 (Deducción)", nameof(tipoConcepto));

        // Para deducciones, el monto debe ser negativo o se convierte automáticamente
        var montoFinal = tipoConcepto == 2 && monto > 0 ? -monto : monto;

        var detalle = new ReciboDetalle
        {
            PagoId = pagoId,
            Concepto = concepto.Trim(),
            Monto = montoFinal,
            TipoConcepto = tipoConcepto,
            Orden = orden
        };

        return detalle;
    }

    /// <summary>
    /// Crea una línea de ingreso.
    /// </summary>
    public static ReciboDetalle CreateIngreso(int pagoId, string concepto, decimal monto, int? orden = null)
    {
        if (monto < 0)
            throw new ArgumentException("El monto de un ingreso no puede ser negativo", nameof(monto));

        return CreateWithType(pagoId, concepto, monto, 1, orden);
    }

    /// <summary>
    /// Crea una línea de deducción.
    /// </summary>
    public static ReciboDetalle CreateDeduccion(int pagoId, string concepto, decimal monto, int? orden = null)
    {
        if (monto < 0)
            throw new ArgumentException("El monto debe especificarse como positivo, se convertirá automáticamente a negativo", nameof(monto));

        return CreateWithType(pagoId, concepto, monto, 2, orden);
    }

    /// <summary>
    /// Actualiza el monto del detalle.
    /// </summary>
    public void ActualizarMonto(decimal nuevoMonto)
    {
        // Si es deducción y el monto es positivo, convertirlo a negativo
        if (TipoConcepto == 2 && nuevoMonto > 0)
            nuevoMonto = -nuevoMonto;

        Monto = nuevoMonto;
    }

    /// <summary>
    /// Actualiza el concepto.
    /// </summary>
    public void ActualizarConcepto(string nuevoConcepto)
    {
        if (string.IsNullOrWhiteSpace(nuevoConcepto))
            throw new ArgumentException("El concepto es requerido", nameof(nuevoConcepto));

        if (nuevoConcepto.Length > 90)
            throw new ArgumentException("El concepto no puede exceder 90 caracteres", nameof(nuevoConcepto));

        Concepto = nuevoConcepto.Trim();
    }

    /// <summary>
    /// Actualiza el orden de presentación.
    /// </summary>
    public void ActualizarOrden(int nuevoOrden)
    {
        if (nuevoOrden < 0)
            throw new ArgumentException("El orden no puede ser negativo", nameof(nuevoOrden));

        Orden = nuevoOrden;
    }

    /// <summary>
    /// Verifica si es un ingreso.
    /// </summary>
    public bool EsIngreso() => TipoConcepto == 1;

    /// <summary>
    /// Verifica si es una deducción.
    /// </summary>
    public bool EsDeduccion() => TipoConcepto == 2;

    /// <summary>
    /// Obtiene el monto absoluto (sin signo).
    /// </summary>
    public decimal ObtenerMontoAbsoluto() => Math.Abs(Monto);

    /// <summary>
    /// Obtiene el monto formateado como string (con 2 decimales).
    /// </summary>
    public string ObtenerMontoFormateado() => Monto.ToString("N2");
}
