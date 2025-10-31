using Microsoft.EntityFrameworkCore;
using MiGenteEnLinea.Application.Common.Interfaces;
using MiGenteEnLinea.Application.Features.Empleados.Commands.CreateRemuneraciones;
using MiGenteEnLinea.Application.Features.Empleados.Commands.CreateEmpleadoTemporal;
using MiGenteEnLinea.Application.Features.Empleados.Commands.CreateDetalleContratacion;
using MiGenteEnLinea.Application.Features.Empleados.Commands.UpdateDetalleContratacion;
using MiGenteEnLinea.Application.Features.Empleados.Commands.CalificarContratacion;
using MiGenteEnLinea.Application.Features.Empleados.Commands.ModificarCalificacion;
using MiGenteEnLinea.Application.Features.Empleados.DTOs;
using MiGenteEnLinea.Infrastructure.Persistence.Contexts;
using MiGenteEnLinea.Infrastructure.Persistence.Entities.Generated;
using System.Text;

namespace MiGenteEnLinea.Infrastructure.Services;

/// <summary>
/// Implementación de ILegacyDataService usando raw SQL
/// Accede a tablas Legacy sin necesidad de entidades DDD completas
/// </summary>
public class LegacyDataService : ILegacyDataService
{
    private readonly MiGenteDbContext _context;

    public LegacyDataService(MiGenteDbContext context)
    {
        _context = context;
    }

    public async Task<List<RemuneracionDto>> GetRemuneracionesAsync(
        string userId,
        int empleadoId,
        CancellationToken cancellationToken = default)
    {
        // Legacy: return db.Remuneraciones.Where(x => x.userID == userID && x.empleadoID == empleadoID).ToList();
        return await _context.Database
            .SqlQueryRaw<RemuneracionDto>(
                "SELECT id AS Id, userID AS UserId, empleadoID AS EmpleadoId, " +
                "descripcion AS Descripcion, monto AS Monto " +
                "FROM Remuneraciones WHERE userID = {0} AND empleadoID = {1}",
                userId, empleadoId)
            .ToListAsync(cancellationToken);
    }

    public async Task DeleteRemuneracionAsync(
        string userId,
        int remuneracionId,
        CancellationToken cancellationToken = default)
    {
        // ✅ EF Core: Query + Remove + SaveChanges
        var remuneracion = await _context.Set<Remuneracione>()
            .FirstOrDefaultAsync(r => r.UserId == userId && r.Id == remuneracionId, cancellationToken);

        if (remuneracion != null)
        {
            _context.Set<Remuneracione>().Remove(remuneracion);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task CreateRemuneracionesAsync(
        string userId,
        int empleadoId,
        List<RemuneracionItemDto> remuneraciones,
        CancellationToken cancellationToken = default)
    {
        // ✅ EF Core: AddRange + SaveChanges (batch insert)
        var entidades = remuneraciones.Select(rem => new Remuneracione
        {
            UserId = userId,
            EmpleadoId = empleadoId,
            Descripcion = rem.Descripcion,
            Monto = rem.Monto
        }).ToList();

        if (entidades.Any())
        {
            await _context.Set<Remuneracione>().AddRangeAsync(entidades, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task UpdateRemuneracionesAsync(
        string userId,
        int empleadoId,
        List<RemuneracionItemDto> remuneraciones,
        CancellationToken cancellationToken = default)
    {
        // ✅ EF Core: Query existing, RemoveRange, AddRange, SaveChanges (single transaction)
        // Step 1: Get existing remuneraciones for this empleadoId
        var existingRemuneraciones = await _context.Set<Remuneracione>()
            .Where(r => r.UserId == userId && r.EmpleadoId == empleadoId)
            .ToListAsync(cancellationToken);

        // Step 2: Remove existing
        if (existingRemuneraciones.Any())
        {
            _context.Set<Remuneracione>().RemoveRange(existingRemuneraciones);
        }

        // Step 3: Add new remuneraciones
        var nuevasEntidades = remuneraciones.Select(rem => new Remuneracione
        {
            UserId = userId,
            EmpleadoId = empleadoId,
            Descripcion = rem.Descripcion,
            Monto = rem.Monto
        }).ToList();

        if (nuevasEntidades.Any())
        {
            await _context.Set<Remuneracione>().AddRangeAsync(nuevasEntidades, cancellationToken);
        }

        // Step 4: Save all changes in single transaction
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<List<DeduccionTssDto>> GetDeduccionesTssAsync(CancellationToken cancellationToken = default)
    {
        // Legacy: return db.Deducciones_TSS.ToList();
        return await _context.Database
            .SqlQueryRaw<DeduccionTssDto>(
                "SELECT id AS Id, descripcion AS Descripcion, porcentaje AS Porcentaje " +
                "FROM Deducciones_TSS")
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> DarDeBajaEmpleadoAsync(
        int empleadoId,
        string userId,
        DateTime fechaBaja,
        decimal prestaciones,
        string motivo,
        CancellationToken cancellationToken = default)
    {
        // ✅ EF Core: Query + Update properties + SaveChanges
        var empleado = await _context.Set<Empleado>()
            .FirstOrDefaultAsync(e => e.EmpleadoId == empleadoId && e.UserId == userId, cancellationToken);

        if (empleado == null)
            return false;

        // Update properties (soft delete)
        empleado.Activo = false;
        empleado.FechaSalida = fechaBaja.Date; // DateTime? property
        empleado.MotivoBaja = motivo;
        empleado.Prestaciones = prestaciones;

        await _context.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task<bool> CancelarTrabajoAsync(
        int contratacionId,
        int detalleId,
        CancellationToken cancellationToken = default)
    {
        // ✅ EF Core: Query + Update single property + SaveChanges
        var detalle = await _context.Set<DetalleContratacione>()
            .FirstOrDefaultAsync(d => d.ContratacionId == contratacionId && d.DetalleId == detalleId, cancellationToken);

        if (detalle == null)
            return false;

        detalle.Estatus = 3; // Cancelled status

        await _context.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task<bool> EliminarReciboEmpleadoAsync(
        int pagoId,
        CancellationToken cancellationToken = default)
    {
        // ✅ EF Core: Include Detalle + Remove Header (cascade delete automatic if configured)
        var header = await _context.Set<EmpleadorRecibosHeader>()
            .Include(h => h.EmpleadorRecibosDetalles)
            .FirstOrDefaultAsync(h => h.PagoId == pagoId, cancellationToken);

        if (header == null)
            return false;

        _context.Set<EmpleadorRecibosHeader>().Remove(header);
        await _context.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task<bool> EliminarReciboContratacionAsync(
        int pagoId,
        CancellationToken cancellationToken = default)
    {
        // ✅ EF Core: Include Detalle + Remove Header (cascade delete automatic if configured)
        var header = await _context.Set<EmpleadorRecibosHeaderContratacione>()
            .Include(h => h.EmpleadorRecibosDetalleContrataciones)
            .FirstOrDefaultAsync(h => h.PagoId == pagoId, cancellationToken);

        if (header == null)
            return false;

        _context.Set<EmpleadorRecibosHeaderContratacione>().Remove(header);
        await _context.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task<ReciboContratacionDto?> GetReciboContratacionAsync(
        int pagoId,
        CancellationToken cancellationToken = default)
    {
        // Query identical to Legacy:
        // db.Empleador_Recibos_Header_Contrataciones.Where(x => x.pagoID == pagoID)
        //   .Include(h => h.Empleador_Recibos_Detalle_Contrataciones)
        //   .Include(f => f.EmpleadosTemporales).FirstOrDefault();

        var headerEntity = await _context
            .Set<EmpleadorRecibosHeaderContratacione>()
            .Where(x => x.PagoId == pagoId)
            .Include(h => h.EmpleadorRecibosDetalleContrataciones)
            .Include(f => f.Contratacion) // EmpleadoTemporal
            .FirstOrDefaultAsync(cancellationToken);

        if (headerEntity == null)
        {
            return null;
        }

        // Map to DTO
        var dto = new ReciboContratacionDto
        {
            PagoId = headerEntity.PagoId,
            UserId = headerEntity.UserId,
            ContratacionId = headerEntity.ContratacionId,
            FechaRegistro = headerEntity.FechaRegistro,
            FechaPago = headerEntity.FechaPago,
            ConceptoPago = headerEntity.ConceptoPago,
            Tipo = headerEntity.Tipo,
            Detalles = headerEntity.EmpleadorRecibosDetalleContrataciones
                .Select(d => new ReciboContratacionDetalleDto
                {
                    DetalleId = d.DetalleId,
                    PagoId = d.PagoId,
                    Concepto = d.Concepto,
                    Monto = d.Monto
                })
                .ToList()
        };

        // Map EmpleadoTemporal if exists
        if (headerEntity.Contratacion != null)
        {
            var emp = headerEntity.Contratacion;
            dto.EmpleadoTemporal = new EmpleadoTemporalSimpleDto
            {
                ContratacionId = emp.ContratacionId,
                Nombre = emp.Nombre,
                Apellido = emp.Apellido,
                Cedula = emp.Identificacion // In Legacy, "identificacion" is the cedula field
            };
        }

        return dto;
    }

    public async Task<bool> EliminarEmpleadoTemporalAsync(
        int contratacionId,
        CancellationToken cancellationToken = default)
    {
        // ✅ EF Core OPTIMIZED: Load all entities in one query with Include().ThenInclude()
        // Then remove in correct order (Detalles → Headers → EmpleadoTemporal)
        // Single SaveChanges = atomic transaction

        var empleadoTemporal = await _context.Set<EmpleadosTemporale>()
            .Include(et => et.EmpleadorRecibosHeaderContrataciones)
                .ThenInclude(h => h.EmpleadorRecibosDetalleContrataciones)
            .FirstOrDefaultAsync(et => et.ContratacionId == contratacionId, cancellationToken);

        if (empleadoTemporal == null)
            return false;

        // Remove all receipts (headers will cascade delete detalles if configured, otherwise explicit)
        if (empleadoTemporal.EmpleadorRecibosHeaderContrataciones?.Any() == true)
        {
            _context.Set<EmpleadorRecibosHeaderContratacione>()
                .RemoveRange(empleadoTemporal.EmpleadorRecibosHeaderContrataciones);
        }

        // Remove empleadoTemporal
        _context.Set<EmpleadosTemporale>().Remove(empleadoTemporal);

        // Single atomic transaction
        await _context.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task<List<PagoContratacionDto>> GetPagosContratacionesAsync(
        int contratacionId,
        int detalleId,
        CancellationToken cancellationToken = default)
    {
        // Legacy: SELECT from VPagosContrataciones view with filters
        var result = await _context
            .Set<VpagosContratacione>()
            .Where(x => x.ContratacionId == contratacionId && x.DetalleId == detalleId)
            .Select(x => new PagoContratacionDto
            {
                PagoId = x.PagoId,
                UserId = x.UserId,
                FechaRegistro = x.FechaRegistro,
                FechaPago = x.FechaPago,
                Expr1 = x.Expr1,
                Monto = x.Monto,
                ContratacionId = x.ContratacionId,
                DetalleId = x.DetalleId
            })
            .ToListAsync(cancellationToken);

        return result;
    }

    public async Task<int> CreateEmpleadoTemporalAsync(
        CreateEmpleadoTemporalCommand command,
        CancellationToken cancellationToken = default)
    {
        // Legacy: Uses 2 separate DbContexts (2 transactions)
        // Step 1: Create EmpleadoTemporal
        var empleadoTemporal = new EmpleadosTemporale
        {
            UserId = command.UserId,
            FechaRegistro = DateTime.Now,
            Tipo = command.Tipo,
            NombreComercial = command.NombreComercial,
            Rnc = command.Rnc,
            Nombre = command.Nombre,
            Apellido = command.Apellido,
            Identificacion = command.Identificacion,
            Telefono1 = command.Telefono,
            Direccion = command.Direccion
        };

        _context.Set<EmpleadosTemporale>().Add(empleadoTemporal);
        await _context.SaveChangesAsync(cancellationToken);

        int contratacionId = empleadoTemporal.ContratacionId;

        // Step 2: Create DetalleContrataciones (with the generated contratacionId)
        // Map Command properties to entity properties
        var detalle = new DetalleContratacione
        {
            ContratacionId = contratacionId,
            DescripcionCorta = command.Servicio, // "Servicio" maps to "DescripcionCorta"
            FechaInicio = command.FechaInicio.HasValue ? DateOnly.FromDateTime(command.FechaInicio.Value) : null,
            FechaFinal = command.FechaFin.HasValue ? DateOnly.FromDateTime(command.FechaFin.Value) : null,
            MontoAcordado = command.Pago,
            DescripcionAmpliada = command.LugarTrabajo, // Assuming LugarTrabajo maps to DescripcionAmpliada
            EsquemaPagos = command.HorarioTrabajo, // Assuming HorarioTrabajo maps to EsquemaPagos
            Estatus = command.Estatus ?? 1 // Default to 1 (active)
        };

        _context.Set<DetalleContratacione>().Add(detalle);
        await _context.SaveChangesAsync(cancellationToken);

        return contratacionId;
    }

    public async Task<int> CreateDetalleContratacionAsync(
        CreateDetalleContratacionCommand command,
        CancellationToken cancellationToken = default)
    {
        // Legacy: Simple INSERT into DetalleContrataciones
        var detalle = new DetalleContratacione
        {
            ContratacionId = command.ContratacionId,
            DescripcionCorta = command.DescripcionCorta,
            DescripcionAmpliada = command.DescripcionAmpliada,
            FechaInicio = command.FechaInicio.HasValue ? DateOnly.FromDateTime(command.FechaInicio.Value) : null,
            FechaFinal = command.FechaFin.HasValue ? DateOnly.FromDateTime(command.FechaFin.Value) : null,
            MontoAcordado = command.MontoAcordado,
            EsquemaPagos = command.EsquemaPagos,
            Estatus = command.Estatus ?? 1
        };

        _context.Set<DetalleContratacione>().Add(detalle);
        await _context.SaveChangesAsync(cancellationToken);

        return detalle.DetalleId;
    }

    public async Task<bool> UpdateDetalleContratacionAsync(
        UpdateDetalleContratacionCommand command,
        CancellationToken cancellationToken = default)
    {
        // Legacy: Find by contratacionID and update fields
        var detalle = await _context
            .Set<DetalleContratacione>()
            .Where(x => x.ContratacionId == command.ContratacionId)
            .FirstOrDefaultAsync(cancellationToken);

        if (detalle == null)
            return false;

        // Update all fields from command
        detalle.DescripcionCorta = command.DescripcionCorta;
        detalle.DescripcionAmpliada = command.DescripcionAmpliada;
        detalle.FechaInicio = command.FechaInicio.HasValue ? DateOnly.FromDateTime(command.FechaInicio.Value) : detalle.FechaInicio;
        detalle.FechaFinal = command.FechaFin.HasValue ? DateOnly.FromDateTime(command.FechaFin.Value) : detalle.FechaFinal;
        detalle.MontoAcordado = command.MontoAcordado ?? detalle.MontoAcordado;
        detalle.EsquemaPagos = command.EsquemaPagos;
        detalle.Estatus = command.Estatus ?? detalle.Estatus;

        await _context.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task<bool> CalificarContratacionAsync(
        int contratacionId,
        int calificacionId,
        CancellationToken cancellationToken = default)
    {
        // Legacy: Find DetalleContrataciones by contratacionID and set calificado=true + calificacionID
        var detalle = await _context
            .Set<DetalleContratacione>()
            .Where(x => x.ContratacionId == contratacionId)
            .FirstOrDefaultAsync(cancellationToken);

        if (detalle == null)
            return false;

        // Set calificado flag and assign calificacionID
        detalle.Calificado = true;
        detalle.CalificacionId = calificacionId;

        await _context.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task<bool> ModificarCalificacionAsync(
        ModificarCalificacionCommand command,
        CancellationToken cancellationToken = default)
    {
        // Legacy: Find Calificaciones by calificacionID and update all 9 fields
        var calificacion = await _context
            .Set<Calificacione>()
            .Where(x => x.CalificacionId == command.CalificacionId)
            .FirstOrDefaultAsync(cancellationToken);

        if (calificacion == null)
            return false;

        // Update all 9 fields from command
        calificacion.Identificacion = command.Identificacion ?? calificacion.Identificacion;
        calificacion.Conocimientos = command.Conocimientos ?? calificacion.Conocimientos;
        calificacion.Cumplimiento = command.Cumplimiento ?? calificacion.Cumplimiento;
        calificacion.Fecha = command.Fecha ?? calificacion.Fecha;
        calificacion.Nombre = command.Nombre ?? calificacion.Nombre;
        calificacion.Puntualidad = command.Puntualidad ?? calificacion.Puntualidad;
        calificacion.Recomendacion = command.Recomendacion ?? calificacion.Recomendacion;
        calificacion.Tipo = command.Tipo ?? calificacion.Tipo;
        calificacion.UserId = command.UserId ?? calificacion.UserId;

        await _context.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task<EmpleadoTemporalDto?> GetFichaTemporalesAsync(
        int contratacionId,
        string userId,
        CancellationToken cancellationToken = default)
    {
        // Legacy: Get EmpleadosTemporales with DetalleContrataciones included
        var empleadoTemporal = await _context
            .Set<EmpleadosTemporale>()
            .Where(x => x.UserId == userId && x.ContratacionId == contratacionId)
            .Select(e => new EmpleadoTemporalDto
            {
                ContratacionId = e.ContratacionId,
                UserId = e.UserId,
                FechaRegistro = e.FechaRegistro,
                Tipo = e.Tipo,
                NombreComercial = e.NombreComercial,
                Rnc = e.Rnc,
                Nombre = e.Nombre,
                Apellido = e.Apellido,
                Identificacion = e.Identificacion,
                Telefono1 = e.Telefono1,
                Direccion = e.Direccion,
                // Include DetalleContrataciones
                Detalle = _context.Set<DetalleContratacione>()
                    .Where(d => d.ContratacionId == e.ContratacionId)
                    .Select(d => new DetalleContratacionDto
                    {
                        DetalleId = d.DetalleId,
                        ContratacionId = d.ContratacionId,
                        DescripcionCorta = d.DescripcionCorta,
                        DescripcionAmpliada = d.DescripcionAmpliada,
                        FechaInicio = d.FechaInicio,
                        FechaFinal = d.FechaFinal,
                        MontoAcordado = d.MontoAcordado,
                        EsquemaPagos = d.EsquemaPagos,
                        Estatus = d.Estatus,
                        Calificado = d.Calificado,
                        CalificacionId = d.CalificacionId
                    })
                    .FirstOrDefault()
            })
            .FirstOrDefaultAsync(cancellationToken);

        return empleadoTemporal;
    }

    /// <summary>
    /// Obtiene todos los EmpleadosTemporales de un usuario con transformación de nombres
    /// Migrado de: EmpleadosService.obtenerTodosLosTemporales(string userID) - line 526
    /// 
    /// BUSINESS LOGIC (copied from Legacy):
    ///   - tipo == 1 (Individual): Nombre = Nombre + Apellido
    ///   - tipo == 2 (Business): Nombre = NombreComercial, Identificacion = Rnc
    /// </summary>
    public async Task<List<EmpleadoTemporalDto>> GetTodosLosTemporalesAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        // Legacy: Query EmpleadosTemporales by userID with Include
        var empleadosTemporales = await _context
            .Set<EmpleadosTemporale>()
            .Where(x => x.UserId == userId)
            .Select(e => new EmpleadoTemporalDto
            {
                ContratacionId = e.ContratacionId,
                UserId = e.UserId,
                FechaRegistro = e.FechaRegistro,
                Tipo = e.Tipo,
                NombreComercial = e.NombreComercial,
                Rnc = e.Rnc,
                Nombre = e.Nombre,
                Apellido = e.Apellido,
                Identificacion = e.Identificacion,
                Telefono1 = e.Telefono1,
                Direccion = e.Direccion,
                // Include DetalleContrataciones
                Detalle = _context.Set<DetalleContratacione>()
                    .Where(d => d.ContratacionId == e.ContratacionId)
                    .Select(d => new DetalleContratacionDto
                    {
                        DetalleId = d.DetalleId,
                        ContratacionId = d.ContratacionId,
                        DescripcionCorta = d.DescripcionCorta,
                        DescripcionAmpliada = d.DescripcionAmpliada,
                        FechaInicio = d.FechaInicio,
                        FechaFinal = d.FechaFinal,
                        MontoAcordado = d.MontoAcordado,
                        EsquemaPagos = d.EsquemaPagos,
                        Estatus = d.Estatus,
                        Calificado = d.Calificado,
                        CalificacionId = d.CalificacionId
                    })
                    .FirstOrDefault()
            })
            .ToListAsync(cancellationToken);

        // Legacy post-processing: Transform names based on tipo
        foreach (var empleado in empleadosTemporales)
        {
            if (empleado.Tipo == 1) // Individual
            {
                // Concatenate nombre + apellido
                empleado.Nombre = empleado.Nombre + " " + empleado.Apellido;
            }
            else if (empleado.Tipo == 2) // Business
            {
                // Use nombreComercial as nombre
                empleado.Nombre = empleado.NombreComercial;
                // Use rnc as identificacion
                empleado.Identificacion = empleado.Rnc;
            }
        }

        return empleadosTemporales;
    }

    /// <summary>
    /// Obtiene VistaContratacionTemporal por contratacionID y userID
    /// Migrado de: EmpleadosService.obtenerVistaTemporal(int contratacionID, string userID) - line 554
    /// </summary>
    public async Task<VistaContratacionTemporalDto?> GetVistaContratacionTemporalAsync(
        int contratacionId,
        string userId,
        CancellationToken cancellationToken = default)
    {
        // Legacy: Query VistaContratacionTemporal view
        var vista = await _context.VistasContratacionTemporal
            .Where(x => x.UserId == userId && x.ContratacionId == contratacionId)
            .Select(v => new VistaContratacionTemporalDto
            {
                ContratacionId = v.ContratacionId,
                UserId = v.UserId,
                FechaRegistro = v.FechaRegistro,
                Tipo = v.Tipo,
                NombreComercial = v.NombreComercial,
                Rnc = v.Rnc,
                Identificacion = v.Identificacion,
                Nombre = v.Nombre,
                Apellido = v.Apellido,
                Alias = v.Alias,
                Direccion = v.Direccion,
                Provincia = v.Provincia,
                Municipio = v.Municipio,
                Telefono1 = v.Telefono1,
                Telefono2 = v.Telefono2,
                DetalleId = v.DetalleId,
                Expr1 = v.Expr1,
                DescripcionCorta = v.DescripcionCorta,
                DescripcionAmpliada = v.DescripcionAmpliada,
                FechaInicio = v.FechaInicio,
                FechaFinal = v.FechaFinal,
                MontoAcordado = v.MontoAcordado,
                EsquemaPagos = v.EsquemaPagos,
                Estatus = v.Estatus,
                ComposicionNombre = v.ComposicionNombre,
                ComposicionId = v.ComposicionId,
                Conocimientos = v.Conocimientos,
                Puntualidad = v.Puntualidad,
                Recomendacion = v.Recomendacion,
                Cumplimiento = v.Cumplimiento
            })
            .FirstOrDefaultAsync(cancellationToken);

        return vista;
    }

    /// <summary>
    /// Method #21: Obtiene Empleador_Recibos_Header completo con Detalle y Empleado
    /// Migrado de: EmpleadosService.GetEmpleador_ReciboByPagoID(int pagoID) - line 212
    /// Legacy: db.Empleador_Recibos_Header.Where(x => x.pagoID == pagoID)
    ///         .Include(h => h.Empleador_Recibos_Detalle)
    ///         .Include(f => f.Empleados).FirstOrDefault()
    /// </summary>
    public async Task<ReciboHeaderCompletoDto?> GetReciboHeaderByPagoIdAsync(
        int pagoId,
        CancellationToken cancellationToken = default)
    {
        var recibo = await _context
            .Set<EmpleadorRecibosHeader>()
            .Where(x => x.PagoId == pagoId)
            .Select(h => new ReciboHeaderCompletoDto
            {
                // Map header fields
                PagoId = h.PagoId,
                UserId = h.UserId,
                EmpleadoId = h.EmpleadoId,
                FechaRegistro = h.FechaRegistro,
                FechaPago = h.FechaPago,
                ConceptoPago = h.ConceptoPago,
                Tipo = h.Tipo,
                
                // Nested Select for Detalles (1:N)
                Detalles = _context
                    .Set<EmpleadorRecibosDetalle>()
                    .Where(d => d.PagoId == h.PagoId)
                    .Select(d => new EmpleadorReciboDetalleDto
                    {
                        DetalleId = d.DetalleId,
                        PagoId = d.PagoId,
                        Concepto = d.Concepto,
                        Monto = d.Monto
                    })
                    .ToList(),
                
                // Nested Select for Empleado (1:1)
                Empleado = h.EmpleadoId.HasValue
                    ? _context
                        .Set<Empleado>()
                        .Where(e => e.EmpleadoId == h.EmpleadoId.Value)
                        .Select(e => new EmpleadoBasicoDto
                        {
                            EmpleadoId = e.EmpleadoId,
                            Nombre = e.Nombre,
                            Apellido = e.Apellido,
                            Identificacion = e.Identificacion
                        })
                        .FirstOrDefault()
                    : null
            })
            .FirstOrDefaultAsync(cancellationToken);
        
        return recibo;
    }
}

