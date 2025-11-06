using MiGenteEnLinea.Domain.Common;
using MiGenteEnLinea.Domain.Events.Nominas;

namespace MiGenteEnLinea.Domain.Entities.Nominas;

/// <summary>
/// Entidad que representa el encabezado de un recibo de pago (nómina).
/// Es un Aggregate Root que contiene una colección de ReciboDetalle.
/// Gestiona el proceso completo de generación, cálculo y pago de nómina.
/// </summary>
public sealed class ReciboHeader : AggregateRoot
{
    /// <summary>
    /// Identificador único del recibo de pago.
    /// </summary>
    public int PagoId { get; private set; }

    /// <summary>
    /// Identificador del empleador que genera el recibo.
    /// </summary>
    public string UserId { get; private set; } = null!;

    /// <summary>
    /// Identificador del empleado al que se le paga.
    /// </summary>
    public int EmpleadoId { get; private set; }

    /// <summary>
    /// Fecha de registro del recibo en el sistema.
    /// </summary>
    public DateTime FechaRegistro { get; private set; }

    /// <summary>
    /// Fecha efectiva del pago.
    /// </summary>
    public DateTime? FechaPago { get; private set; }

    /// <summary>
    /// Concepto general del pago (ej: "Pago Quincenal Enero 2025").
    /// </summary>
    public string ConceptoPago { get; private set; } = null!;

    /// <summary>
    /// Tipo de recibo (1=Nómina Regular, 2=Pago Extraordinario, 3=Liquidación).
    /// </summary>
    public int Tipo { get; private set; }

    /// <summary>
    /// Estado del recibo (1=Pendiente, 2=Pagado, 3=Anulado).
    /// </summary>
    public int Estado { get; private set; }

    /// <summary>
    /// Período de inicio al que corresponde este pago.
    /// </summary>
    public DateOnly? PeriodoInicio { get; private set; }

    /// <summary>
    /// Período de fin al que corresponde este pago.
    /// </summary>
    public DateOnly? PeriodoFin { get; private set; }

    /// <summary>
    /// Total de ingresos (calculado).
    /// </summary>
    public decimal TotalIngresos { get; private set; }

    /// <summary>
    /// Total de deducciones (calculado).
    /// </summary>
    public decimal TotalDeducciones { get; private set; }

    /// <summary>
    /// Neto a pagar (TotalIngresos - TotalDeducciones).
    /// </summary>
    public decimal NetoPagar { get; private set; }

    /// <summary>
    /// Colección de detalles (líneas) del recibo.
    /// Navigation property para EF Core (permite Include/ThenInclude).
    /// </summary>
    private List<ReciboDetalle> _detalles = new();
    public ICollection<ReciboDetalle> Detalles 
    { 
        get => _detalles;
        private set => _detalles = value?.ToList() ?? new List<ReciboDetalle>();
    }

    // Constructor privado para EF Core
    private ReciboHeader()
    {
    }

    /// <summary>
    /// Crea un nuevo recibo de pago (sobrecarga simplificada para tests/integraciones).
    /// </summary>
    /// <param name="userId">ID del empleador.</param>
    /// <param name="empleadoId">ID del empleado.</param>
    /// <param name="fechaPago">Fecha efectiva del pago.</param>
    /// <param name="conceptoPago">Concepto general del pago.</param>
    /// <returns>Nueva instancia de ReciboHeader.</returns>
    public static ReciboHeader Create(
        string userId,
        int empleadoId,
        DateTime fechaPago,
        string conceptoPago)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("El ID del usuario es requerido", nameof(userId));

        if (empleadoId <= 0)
            throw new ArgumentException("El ID del empleado debe ser mayor a cero", nameof(empleadoId));

        if (string.IsNullOrWhiteSpace(conceptoPago))
            throw new ArgumentException("El concepto de pago es requerido", nameof(conceptoPago));

        var recibo = new ReciboHeader
        {
            UserId = userId,
            EmpleadoId = empleadoId,
            ConceptoPago = conceptoPago.Trim(),
            Tipo = 1, // Nómina Regular por defecto
            Estado = 1, // Pendiente
            FechaRegistro = DateTime.UtcNow,
            FechaPago = fechaPago,
            TotalIngresos = 0,
            TotalDeducciones = 0,
            NetoPagar = 0
        };

        recibo.RaiseDomainEvent(new ReciboGeneradoEvent(
            recibo.PagoId,
            recibo.UserId,
            recibo.EmpleadoId,
            recibo.ConceptoPago,
            recibo.FechaRegistro));

        return recibo;
    }

    /// <summary>
    /// Crea un nuevo recibo de pago con opciones completas.
    /// </summary>
    /// <param name="userId">ID del empleador.</param>
    /// <param name="empleadoId">ID del empleado.</param>
    /// <param name="conceptoPago">Concepto general del pago.</param>
    /// <param name="tipo">Tipo de recibo (1-3).</param>
    /// <param name="periodoInicio">Inicio del período.</param>
    /// <param name="periodoFin">Fin del período.</param>
    /// <returns>Nueva instancia de ReciboHeader.</returns>
    public static ReciboHeader CreateWithOptions(
        string userId,
        int empleadoId,
        string conceptoPago,
        int tipo,
        DateOnly? periodoInicio = null,
        DateOnly? periodoFin = null)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("El ID del usuario es requerido", nameof(userId));

        if (empleadoId <= 0)
            throw new ArgumentException("El ID del empleado debe ser mayor a cero", nameof(empleadoId));

        if (string.IsNullOrWhiteSpace(conceptoPago))
            throw new ArgumentException("El concepto de pago es requerido", nameof(conceptoPago));

        if (tipo < 1 || tipo > 3)
            throw new ArgumentException("El tipo debe ser 1 (Regular), 2 (Extraordinario) o 3 (Liquidación)", nameof(tipo));

        if (periodoInicio.HasValue && periodoFin.HasValue && periodoInicio > periodoFin)
            throw new ArgumentException("El período de inicio no puede ser mayor al de fin");

        var recibo = new ReciboHeader
        {
            UserId = userId,
            EmpleadoId = empleadoId,
            ConceptoPago = conceptoPago.Trim(),
            Tipo = tipo,
            Estado = 1, // Pendiente
            FechaRegistro = DateTime.UtcNow,
            PeriodoInicio = periodoInicio,
            PeriodoFin = periodoFin,
            TotalIngresos = 0,
            TotalDeducciones = 0,
            NetoPagar = 0
        };

        recibo.RaiseDomainEvent(new ReciboGeneradoEvent(
            recibo.PagoId,
            recibo.UserId,
            recibo.EmpleadoId,
            recibo.ConceptoPago,
            recibo.FechaRegistro));

        return recibo;
    }

    /// <summary>
    /// Agrega una línea de ingreso al recibo.
    /// </summary>
    public void AgregarIngreso(string concepto, decimal monto)
    {
        if (Estado != 1)
            throw new InvalidOperationException("Solo se pueden agregar líneas a recibos en estado Pendiente");

        var detalle = ReciboDetalle.CreateIngreso(PagoId, concepto, monto, _detalles.Count + 1);
        _detalles.Add(detalle);

        RecalcularTotales();
    }

    /// <summary>
    /// Agrega una línea de deducción al recibo.
    /// </summary>
    public void AgregarDeduccion(string concepto, decimal monto)
    {
        if (Estado != 1)
            throw new InvalidOperationException("Solo se pueden agregar líneas a recibos en estado Pendiente");

        var detalle = ReciboDetalle.CreateDeduccion(PagoId, concepto, monto, _detalles.Count + 1);
        _detalles.Add(detalle);

        RecalcularTotales();
    }

    /// <summary>
    /// Elimina una línea del recibo.
    /// </summary>
    public void EliminarDetalle(int detalleId)
    {
        if (Estado != 1)
            throw new InvalidOperationException("Solo se pueden eliminar líneas de recibos en estado Pendiente");

        var detalle = _detalles.FirstOrDefault(d => d.DetalleId == detalleId);
        if (detalle == null)
            throw new InvalidOperationException($"No se encontró el detalle con ID {detalleId}");

        _detalles.Remove(detalle);
        RecalcularTotales();
        ReordenarDetalles();
    }

    /// <summary>
    /// Recalcula los totales del recibo basándose en los detalles.
    /// </summary>
    public void RecalcularTotales()
    {
        TotalIngresos = _detalles.Where(d => d.EsIngreso()).Sum(d => d.Monto);
        TotalDeducciones = Math.Abs(_detalles.Where(d => d.EsDeduccion()).Sum(d => d.Monto));
        NetoPagar = TotalIngresos - TotalDeducciones;

        if (NetoPagar < 0)
        {
            throw new InvalidOperationException(
                $"El neto a pagar no puede ser negativo. Ingresos: {TotalIngresos:C}, Deducciones: {TotalDeducciones:C}");
        }
    }

    /// <summary>
    /// Reordena los detalles después de eliminar alguno.
    /// </summary>
    private void ReordenarDetalles()
    {
        var orden = 1;
        foreach (var detalle in _detalles.OrderBy(d => d.Orden ?? int.MaxValue))
        {
            detalle.ActualizarOrden(orden++);
        }
    }

    /// <summary>
    /// Marca el recibo como pagado.
    /// </summary>
    public void MarcarComoPagado()
    {
        if (Estado == 2)
            throw new InvalidOperationException("El recibo ya está marcado como pagado");

        if (Estado == 3)
            throw new InvalidOperationException("No se puede pagar un recibo anulado");

        if (_detalles.Count == 0)
            throw new InvalidOperationException("No se puede pagar un recibo sin detalles");

        if (NetoPagar <= 0)
            throw new InvalidOperationException("El neto a pagar debe ser mayor a cero");

        Estado = 2; // Pagado
        FechaPago = DateTime.UtcNow;

        RaiseDomainEvent(new ReciboPagadoEvent(
            PagoId,
            UserId,
            EmpleadoId,
            NetoPagar,
            FechaPago.Value));
    }

    /// <summary>
    /// Anula el recibo.
    /// </summary>
    public void Anular(string motivo)
    {
        if (Estado == 3)
            throw new InvalidOperationException("El recibo ya está anulado");

        if (string.IsNullOrWhiteSpace(motivo))
            throw new ArgumentException("El motivo de anulación es requerido", nameof(motivo));

        Estado = 3; // Anulado

        RaiseDomainEvent(new ReciboAnuladoEvent(
            PagoId,
            UserId,
            EmpleadoId,
            motivo,
            DateTime.UtcNow));
    }

    /// <summary>
    /// Actualiza el concepto de pago.
    /// </summary>
    public void ActualizarConcepto(string nuevoConcepto)
    {
        if (Estado != 1)
            throw new InvalidOperationException("Solo se puede actualizar el concepto en recibos pendientes");

        if (string.IsNullOrWhiteSpace(nuevoConcepto))
            throw new ArgumentException("El concepto es requerido", nameof(nuevoConcepto));

        ConceptoPago = nuevoConcepto.Trim();
    }

    /// <summary>
    /// Actualiza el período del recibo.
    /// </summary>
    public void ActualizarPeriodo(DateOnly periodoInicio, DateOnly periodoFin)
    {
        if (Estado != 1)
            throw new InvalidOperationException("Solo se puede actualizar el período en recibos pendientes");

        if (periodoInicio > periodoFin)
            throw new ArgumentException("El período de inicio no puede ser mayor al de fin");

        PeriodoInicio = periodoInicio;
        PeriodoFin = periodoFin;
    }

    /// <summary>
    /// Verifica si el recibo está pendiente de pago.
    /// </summary>
    public bool EstaPendiente() => Estado == 1;

    /// <summary>
    /// Verifica si el recibo está pagado.
    /// </summary>
    public bool EstaPagado() => Estado == 2;

    /// <summary>
    /// Verifica si el recibo está anulado.
    /// </summary>
    public bool EstaAnulado() => Estado == 3;

    /// <summary>
    /// Obtiene la cantidad de días del período.
    /// </summary>
    public int? ObtenerDiasPeriodo()
    {
        if (!PeriodoInicio.HasValue || !PeriodoFin.HasValue)
            return null;

        return PeriodoFin.Value.DayNumber - PeriodoInicio.Value.DayNumber + 1;
    }

    /// <summary>
    /// Obtiene el estado como texto.
    /// </summary>
    public string ObtenerEstadoTexto()
    {
        return Estado switch
        {
            1 => "Pendiente",
            2 => "Pagado",
            3 => "Anulado",
            _ => "Desconocido"
        };
    }

    /// <summary>
    /// Obtiene el tipo como texto.
    /// </summary>
    public string ObtenerTipoTexto()
    {
        return Tipo switch
        {
            1 => "Nómina Regular",
            2 => "Pago Extraordinario",
            3 => "Liquidación",
            _ => "Desconocido"
        };
    }

    /// <summary>
    /// Valida si el recibo puede ser modificado.
    /// </summary>
    public bool PuedeSerModificado() => Estado == 1;

    /// <summary>
    /// Valida si el recibo puede ser eliminado.
    /// </summary>
    public bool PuedeSerEliminado() => Estado == 1 || Estado == 3;
}
