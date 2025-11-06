using MiGenteEnLinea.Domain.Common;
using MiGenteEnLinea.Domain.Events.Contrataciones;

namespace MiGenteEnLinea.Domain.Entities.Contrataciones;

/// <summary>
/// Representa una contratación específica entre un empleador y un contratista.
/// Es el aggregate root que gestiona todo el ciclo de vida de una contratación:
/// desde la creación, negociación, ejecución hasta la finalización y calificación.
/// </summary>
public sealed class DetalleContratacion : AggregateRoot
{
    /// <summary>
    /// Identificador único del detalle de contratación
    /// </summary>
    public int DetalleId { get; private set; }

    /// <summary>
    /// ID de la contratación padre (relación con EmpleadoTemporal legacy).
    /// Nullable para soportar contrataciones independientes.
    /// </summary>
    public int? ContratacionId { get; private set; }

    /// <summary>
    /// Descripción breve del trabajo a realizar.
    /// Ejemplo: "Reparación de plomería - Baño principal"
    /// </summary>
    public string DescripcionCorta { get; private set; } = string.Empty;

    /// <summary>
    /// Descripción detallada del trabajo, alcance, materiales, etc.
    /// Ejemplo: "Reparar tubería rota en baño principal, reemplazar llave mezcladora, 
    /// instalar nuevo lavamanos. Incluye materiales y mano de obra."
    /// </summary>
    public string? DescripcionAmpliada { get; private set; }

    /// <summary>
    /// Fecha de inicio acordada para el trabajo
    /// </summary>
    public DateOnly FechaInicio { get; private set; }

    /// <summary>
    /// Fecha estimada o acordada de finalización
    /// </summary>
    public DateOnly FechaFinal { get; private set; }

    /// <summary>
    /// Monto total acordado para el trabajo (en pesos dominicanos)
    /// </summary>
    public decimal MontoAcordado { get; private set; }

    /// <summary>
    /// Esquema de pagos acordado.
    /// Ejemplos: "50% adelanto, 50% al finalizar", "Pago único al completar", 
    /// "Semanal", "Quincenal", "Por avance de obra"
    /// </summary>
    public string? EsquemaPagos { get; private set; }

    /// <summary>
    /// Estado actual de la contratación.
    /// 1 = Pendiente (propuesta enviada)
    /// 2 = Aceptada (contratista aceptó términos)
    /// 3 = En Progreso (trabajo iniciado)
    /// 4 = Completada (trabajo finalizado)
    /// 5 = Cancelada (cancelada por cualquier razón)
    /// 6 = Rechazada (contratista rechazó)
    /// </summary>
    public int Estatus { get; private set; }

    /// <summary>
    /// Indica si el empleador ya calificó al contratista por este trabajo
    /// </summary>
    public bool Calificado { get; private set; }

    /// <summary>
    /// ID de la calificación asociada (si ya fue calificado)
    /// </summary>
    public int? CalificacionId { get; private set; }

    /// <summary>
    /// Notas adicionales o comentarios sobre la contratación (opcional).
    /// </summary>
    public string? Notas { get; private set; }

    /// <summary>
    /// Motivo de cancelación o rechazo (cuando aplica)
    /// </summary>
    public string? MotivoCancelacion { get; private set; }

    /// <summary>
    /// Fecha real de inicio del trabajo (puede diferir de FechaInicio acordada)
    /// </summary>
    public DateTime? FechaInicioReal { get; private set; }

    /// <summary>
    /// Fecha real de finalización del trabajo
    /// </summary>
    public DateTime? FechaFinalizacionReal { get; private set; }

    /// <summary>
    /// Porcentaje de avance del trabajo (0-100)
    /// </summary>
    public int PorcentajeAvance { get; private set; }

    // Constantes de estado (valores Legacy para compatibilidad DB)
    // ⚠️ CRITICAL: These values MUST match database values from Legacy system
    private const int ESTADO_PENDIENTE = 1;      // Propuesta enviada
    private const int ESTADO_ACEPTADA = 2;       // Contratista aceptó
    private const int ESTADO_CANCELADA = 3;      // ✅ FIX: Was 5, should be 3 (Legacy value)
    private const int ESTADO_COMPLETADA = 4;     // Trabajo finalizado
    private const int ESTADO_EN_PROGRESO = 5;    // ✅ FIX: Was 3, should be 5 (Legacy value)
    private const int ESTADO_RECHAZADA = 6;      // Contratista rechazó

    // Constructor privado para EF Core
    private DetalleContratacion() { }

    /// <summary>
    /// Crea una nueva propuesta de contratación.
    /// </summary>
    /// <param name="descripcionCorta">Resumen del trabajo (máx 60 caracteres)</param>
    /// <param name="fechaInicio">Fecha de inicio propuesta</param>
    /// <param name="fechaFinal">Fecha de finalización propuesta</param>
    /// <param name="montoAcordado">Monto total (debe ser mayor a 0)</param>
    /// <param name="descripcionAmpliada">Descripción detallada (opcional, máx 250 caracteres)</param>
    /// <param name="esquemaPagos">Esquema de pagos (opcional, máx 50 caracteres)</param>
    /// <param name="contratacionId">ID de contratación padre (opcional)</param>
    /// <returns>Nueva instancia de DetalleContratacion en estado Pendiente</returns>
    public static DetalleContratacion Crear(
        string descripcionCorta,
        DateOnly fechaInicio,
        DateOnly fechaFinal,
        decimal montoAcordado,
        string? descripcionAmpliada = null,
        string? esquemaPagos = null,
        int? contratacionId = null)
    {
        if (string.IsNullOrWhiteSpace(descripcionCorta))
            throw new ArgumentException("La descripción corta es requerida", nameof(descripcionCorta));

        if (descripcionCorta.Length > 60)
            throw new ArgumentException("La descripción corta no puede exceder 60 caracteres", nameof(descripcionCorta));

        if (!string.IsNullOrWhiteSpace(descripcionAmpliada) && descripcionAmpliada.Length > 250)
            throw new ArgumentException("La descripción ampliada no puede exceder 250 caracteres", nameof(descripcionAmpliada));

        if (fechaFinal < fechaInicio)
            throw new ArgumentException("La fecha final no puede ser anterior a la fecha de inicio");

        if (montoAcordado <= 0)
            throw new ArgumentException("El monto acordado debe ser mayor a 0", nameof(montoAcordado));

        if (!string.IsNullOrWhiteSpace(esquemaPagos) && esquemaPagos.Length > 50)
            throw new ArgumentException("El esquema de pagos no puede exceder 50 caracteres", nameof(esquemaPagos));

        var contratacion = new DetalleContratacion
        {
            ContratacionId = contratacionId,
            DescripcionCorta = descripcionCorta.Trim(),
            DescripcionAmpliada = descripcionAmpliada?.Trim(),
            FechaInicio = fechaInicio,
            FechaFinal = fechaFinal,
            MontoAcordado = montoAcordado,
            EsquemaPagos = esquemaPagos?.Trim(),
            Estatus = ESTADO_PENDIENTE,
            Calificado = false,
            PorcentajeAvance = 0
        };

        contratacion.RaiseDomainEvent(new ContratacionCreadaEvent(
            contratacion.DetalleId,
            contratacion.DescripcionCorta,
            contratacion.FechaInicio,
            contratacion.FechaFinal,
            contratacion.MontoAcordado));

        return contratacion;
    }

    /// <summary>
    /// El contratista acepta la propuesta de contratación.
    /// Cambia estado de Pendiente → Aceptada
    /// </summary>
    public void Aceptar()
    {
        if (Estatus != ESTADO_PENDIENTE)
            throw new InvalidOperationException("Solo se pueden aceptar contrataciones pendientes");

        Estatus = ESTADO_ACEPTADA;
        RaiseDomainEvent(new ContratacionAceptadaEvent(DetalleId, MontoAcordado));
    }

    /// <summary>
    /// El contratista rechaza la propuesta.
    /// Cambia estado de Pendiente → Rechazada
    /// </summary>
    /// <param name="motivo">Motivo del rechazo</param>
    public void Rechazar(string motivo)
    {
        if (Estatus != ESTADO_PENDIENTE)
            throw new InvalidOperationException("Solo se pueden rechazar contrataciones pendientes");

        if (string.IsNullOrWhiteSpace(motivo))
            throw new ArgumentException("El motivo del rechazo es requerido", nameof(motivo));

        Estatus = ESTADO_RECHAZADA;
        MotivoCancelacion = motivo.Trim();

        RaiseDomainEvent(new ContratacionRechazadaEvent(DetalleId, motivo));
    }

    /// <summary>
    /// Inicia el trabajo de la contratación.
    /// Cambia estado de Aceptada → En Progreso
    /// </summary>
    public void IniciarTrabajo()
    {
        if (Estatus != ESTADO_ACEPTADA)
            throw new InvalidOperationException("Solo se puede iniciar una contratación aceptada");

        Estatus = ESTADO_EN_PROGRESO;
        FechaInicioReal = DateTime.Now;
        PorcentajeAvance = 0;

        RaiseDomainEvent(new ContratacionIniciadaEvent(DetalleId, FechaInicioReal.Value));
    }

    /// <summary>
    /// Actualiza el porcentaje de avance del trabajo.
    /// </summary>
    /// <param name="porcentaje">Porcentaje entre 0 y 100</param>
    public void ActualizarAvance(int porcentaje)
    {
        if (Estatus != ESTADO_EN_PROGRESO)
            throw new InvalidOperationException("Solo se puede actualizar avance de contrataciones en progreso");

        if (porcentaje < 0 || porcentaje > 100)
            throw new ArgumentException("El porcentaje debe estar entre 0 y 100", nameof(porcentaje));

        PorcentajeAvance = porcentaje;
    }

    /// <summary>
    /// Marca la contratación como completada.
    /// Cambia estado de En Progreso → Completada
    /// </summary>
    public void Completar()
    {
        if (Estatus != ESTADO_EN_PROGRESO)
            throw new InvalidOperationException("Solo se puede completar una contratación en progreso");

        Estatus = ESTADO_COMPLETADA;
        FechaFinalizacionReal = DateTime.Now;
        PorcentajeAvance = 100;

        RaiseDomainEvent(new ContratacionCompletadaEvent(
            DetalleId,
            FechaFinalizacionReal.Value,
            MontoAcordado));
    }

    /// <summary>
    /// Cancela la contratación.
    /// Puede cancelarse desde cualquier estado excepto Completada.
    /// </summary>
    /// <param name="motivo">Motivo de la cancelación</param>
    public void Cancelar(string motivo)
    {
        if (Estatus == ESTADO_COMPLETADA)
            throw new InvalidOperationException("No se puede cancelar una contratación completada");

        if (Estatus == ESTADO_CANCELADA)
            throw new InvalidOperationException("La contratación ya está cancelada");

        if (string.IsNullOrWhiteSpace(motivo))
            throw new ArgumentException("El motivo de cancelación es requerido", nameof(motivo));

        Estatus = ESTADO_CANCELADA;
        MotivoCancelacion = motivo.Trim();

        RaiseDomainEvent(new ContratacionCanceladaEvent(DetalleId, motivo));
    }

    /// <summary>
    /// Registra la calificación del empleador al contratista.
    /// </summary>
    /// <param name="calificacionId">ID de la calificación creada</param>
    public void RegistrarCalificacion(int calificacionId)
    {
        if (Estatus != ESTADO_COMPLETADA)
            throw new InvalidOperationException("Solo se puede calificar una contratación completada");

        if (Calificado)
            throw new InvalidOperationException("Esta contratación ya fue calificada");

        if (calificacionId <= 0)
            throw new ArgumentException("El ID de calificación debe ser mayor a 0", nameof(calificacionId));

        Calificado = true;
        CalificacionId = calificacionId;

        RaiseDomainEvent(new ContratacionCalificadaEvent(DetalleId, calificacionId));
    }

    /// <summary>
    /// Actualiza las descripciones de la contratación.
    /// Solo se puede actualizar si está en estado Pendiente o Aceptada.
    /// </summary>
    public void ActualizarDescripciones(string? nuevaDescripcionCorta = null, string? nuevaDescripcionAmpliada = null)
    {
        if (Estatus != ESTADO_PENDIENTE && Estatus != ESTADO_ACEPTADA)
            throw new InvalidOperationException("Solo se pueden actualizar descripciones en estados Pendiente o Aceptada");

        if (!string.IsNullOrWhiteSpace(nuevaDescripcionCorta))
        {
            if (nuevaDescripcionCorta.Length > 60)
                throw new ArgumentException("La descripción corta no puede exceder 60 caracteres");

            DescripcionCorta = nuevaDescripcionCorta.Trim();
        }

        if (nuevaDescripcionAmpliada != null)
        {
            if (nuevaDescripcionAmpliada.Length > 250)
                throw new ArgumentException("La descripción ampliada no puede exceder 250 caracteres");

            DescripcionAmpliada = nuevaDescripcionAmpliada.Trim();
        }
    }

    /// <summary>
    /// Actualiza las fechas de la contratación.
    /// Solo se puede actualizar si está en estado Pendiente o Aceptada.
    /// </summary>
    public void ActualizarFechas(DateOnly? nuevaFechaInicio = null, DateOnly? nuevaFechaFinal = null)
    {
        if (Estatus != ESTADO_PENDIENTE && Estatus != ESTADO_ACEPTADA)
            throw new InvalidOperationException("Solo se pueden actualizar fechas en estados Pendiente o Aceptada");

        var fechaInicio = nuevaFechaInicio ?? FechaInicio;
        var fechaFinal = nuevaFechaFinal ?? FechaFinal;

        if (fechaFinal < fechaInicio)
            throw new ArgumentException("La fecha final no puede ser anterior a la fecha de inicio");

        if (nuevaFechaInicio.HasValue)
            FechaInicio = nuevaFechaInicio.Value;

        if (nuevaFechaFinal.HasValue)
            FechaFinal = nuevaFechaFinal.Value;
    }

    /// <summary>
    /// Actualiza el monto acordado.
    /// Solo se puede actualizar si está en estado Pendiente o Aceptada.
    /// </summary>
    public void ActualizarMonto(decimal nuevoMonto)
    {
        if (Estatus != ESTADO_PENDIENTE && Estatus != ESTADO_ACEPTADA)
            throw new InvalidOperationException("Solo se puede actualizar el monto en estados Pendiente o Aceptada");

        if (nuevoMonto <= 0)
            throw new ArgumentException("El monto debe ser mayor a 0", nameof(nuevoMonto));

        MontoAcordado = nuevoMonto;
    }

    /// <summary>
    /// Agrega o actualiza notas sobre la contratación.
    /// </summary>
    public void ActualizarNotas(string? notas)
    {
        if (!string.IsNullOrWhiteSpace(notas) && notas.Length > 500)
            throw new ArgumentException("Las notas no pueden exceder 500 caracteres", nameof(notas));

        Notas = notas?.Trim();
    }

    // Métodos de consulta
    public bool EstaPendiente() => Estatus == ESTADO_PENDIENTE;
    public bool EstaAceptada() => Estatus == ESTADO_ACEPTADA;
    public bool EstaEnProgreso() => Estatus == ESTADO_EN_PROGRESO;
    public bool EstaCompletada() => Estatus == ESTADO_COMPLETADA;
    public bool EstaCancelada() => Estatus == ESTADO_CANCELADA;
    public bool EstaRechazada() => Estatus == ESTADO_RECHAZADA;
    public bool FueCalificada() => Calificado;
    public bool PuedeSerCalificada() => EstaCompletada() && !Calificado;
    public bool PuedeSerCancelada() => !EstaCompletada() && !EstaCancelada();
    public bool PuedeSerModificada() => EstaPendiente() || EstaAceptada();

    /// <summary>
    /// Obtiene el nombre del estado actual en texto.
    /// </summary>
    public string ObtenerNombreEstado() => Estatus switch
    {
        ESTADO_PENDIENTE => "Pendiente",
        ESTADO_ACEPTADA => "Aceptada",
        ESTADO_EN_PROGRESO => "En Progreso",
        ESTADO_COMPLETADA => "Completada",
        ESTADO_CANCELADA => "Cancelada",
        ESTADO_RECHAZADA => "Rechazada",
        _ => "Desconocido"
    };

    /// <summary>
    /// Calcula la duración estimada en días.
    /// </summary>
    public int CalcularDuracionEstimadaDias()
    {
        return FechaFinal.DayNumber - FechaInicio.DayNumber;
    }

    /// <summary>
    /// Calcula la duración real en días (si ya finalizó).
    /// </summary>
    public int? CalcularDuracionRealDias()
    {
        if (!FechaInicioReal.HasValue || !FechaFinalizacionReal.HasValue)
            return null;

        return (FechaFinalizacionReal.Value - FechaInicioReal.Value).Days;
    }

    /// <summary>
    /// Verifica si la contratación está retrasada según la fecha final acordada.
    /// </summary>
    public bool EstaRetrasada()
    {
        if (!EstaEnProgreso())
            return false;

        return DateOnly.FromDateTime(DateTime.Now) > FechaFinal;
    }
}
