using System;
using System.Collections.Generic;
using MiGenteEnLinea.Infrastructure.Persistence.Entities.Generated;
using MiGenteEnLinea.Domain.Entities.Authentication;
using MiGenteEnLinea.Domain.Entities.Empleadores;
using MiGenteEnLinea.Domain.Entities.Contratistas;
using MiGenteEnLinea.Domain.Entities.Suscripciones;
using MiGenteEnLinea.Domain.Entities.Calificaciones;
using MiGenteEnLinea.Domain.Entities.Nominas;
using MiGenteEnLinea.Domain.Entities.Empleados;
using MiGenteEnLinea.Domain.Entities.Pagos;
using MiGenteEnLinea.Domain.Entities.Catalogos;
using MiGenteEnLinea.Domain.Entities.Contrataciones;
using MiGenteEnLinea.Domain.Entities.Seguridad;
using MiGenteEnLinea.Domain.Entities.Configuracion;
using MiGenteEnLinea.Domain.ReadModels;
using Microsoft.EntityFrameworkCore;
using MiGenteEnLinea.Application.Common.Interfaces;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using MiGenteEnLinea.Infrastructure.Identity;

namespace MiGenteEnLinea.Infrastructure.Persistence.Contexts;

/// <summary>
/// DbContext principal que combina:
/// 1. ASP.NET Core Identity (autenticación/autorización)
/// 2. Entidades de negocio DDD (Legacy migradas)
/// 3. Read Models (Views de base de datos)
/// </summary>
public partial class MiGenteDbContext : IdentityDbContext<ApplicationUser>, IApplicationDbContext
{
    public MiGenteDbContext(DbContextOptions<MiGenteDbContext> options)
        : base(options)
    {
    }

    // ========================================
    // EXPLICIT INTERFACE IMPLEMENTATION FOR IApplicationDbContext
    // ========================================
    // Expone propiedades DbContext con nombres de interfaz para Application Layer
    DbSet<Credencial> IApplicationDbContext.Credenciales => CredencialesRefactored;
    DbSet<PasswordResetToken> IApplicationDbContext.PasswordResetTokens => PasswordResetTokens;
    DbSet<VistaPerfil> IApplicationDbContext.VPerfiles => VistasPerfil;
    DbSet<Domain.Entities.Seguridad.Perfile> IApplicationDbContext.Perfiles => Perfiles;
    DbSet<Domain.Entities.Contratistas.Contratista> IApplicationDbContext.Contratistas => Contratistas;
    DbSet<Empleador> IApplicationDbContext.Empleadores => Empleadores;
    // Suscripciones y PlanesEmpleadores ya coinciden con los nombres de interfaz

    // ========================================
    // ASP.NET CORE IDENTITY ENTITIES
    // ========================================
    // ApplicationUser ya está definido por IdentityDbContext<ApplicationUser>
    // AspNetUsers, AspNetRoles, AspNetUserRoles, etc. se crean automáticamente
    
    /// <summary>
    /// Refresh tokens para autenticación JWT
    /// </summary>
    public virtual DbSet<RefreshToken> RefreshTokens { get; set; }

    // ========================================
    // DATABASE ENTITIES
    // ========================================

    // Legacy scaffolded entity (kept for reference)
    // public virtual DbSet<Calificacione> CalificacionesLegacy { get; set; }

    // DDD Refactored entity (replaces Calificacione)
    public virtual DbSet<Calificacion> Calificaciones { get; set; }

    // Legacy scaffolded entity (kept for reference)
    // public virtual DbSet<Infrastructure.Persistence.Entities.Generated.ConfigCorreo> ConfigCorreosLegacy { get; set; }

    // DDD Refactored entity (replaces legacy ConfigCorreo)
    public virtual DbSet<Domain.Entities.Configuracion.ConfigCorreo> ConfigCorreos { get; set; }

    /// <summary>
    /// Configuración del bot OpenAI (tabla: OpenAi_Config)
    /// ⚠️ SECURITY WARNING: Contiene API keys sensibles
    /// </summary>
    public virtual DbSet<Domain.Entities.Configuracion.OpenAiConfig> OpenAiConfigs { get; set; }

    // Legacy scaffolded entity (kept for reference)
    // public virtual DbSet<Infrastructure.Persistence.Entities.Generated.Contratista> ContratistasLegacy { get; set; }

    // DDD Refactored entity (replaces legacy Contratista)
    public virtual DbSet<Domain.Entities.Contratistas.Contratista> Contratistas { get; set; }

    // Legacy scaffolded entity (kept for reference)
    // public virtual DbSet<ContratistasFoto> ContratistasFotosLegacy { get; set; }

    // DDD Refactored entity (replaces ContratistasFoto)
    public virtual DbSet<ContratistaFoto> ContratistasFotos { get; set; }

    // Legacy scaffolded entity (kept for reference)
    // public virtual DbSet<ContratistasServicio> ContratistasServiciosLegacy { get; set; }

    // DDD Refactored entity (replaces ContratistasServicio)
    public virtual DbSet<ContratistaServicio> ContratistasServicios { get; set; }

    // Legacy scaffolded entity (kept for reference)
    // public virtual DbSet<Credenciale> Credenciales { get; set; }

    // DDD Refactored entity (replaces Credenciale)
    public virtual DbSet<Credencial> CredencialesRefactored { get; set; }
    
    // Password Reset Tokens (nueva tabla para seguridad)
    public virtual DbSet<PasswordResetToken> PasswordResetTokens { get; set; }

    // Legacy scaffolded entity (kept for reference)
    // public virtual DbSet<DeduccionesTss> DeduccionesTsses { get; set; }

    // DDD Refactored entity (replaces DeduccionesTss)
    public virtual DbSet<DeduccionTss> DeduccionesTss { get; set; }

    // Legacy scaffolded entity (kept for reference)
    // public virtual DbSet<Ofertante> OfertantesLegacy { get; set; }

    // DDD Refactored entity (replaces Ofertante)
    public virtual DbSet<Empleador> Empleadores { get; set; }

    // Legacy scaffolded entity (kept for reference)
    // public virtual DbSet<DetalleContratacione> DetalleContratacionesLegacy { get; set; }

    // DDD Refactored entity (replaces DetalleContratacione)
    public virtual DbSet<DetalleContratacion> DetalleContrataciones { get; set; }

    // Legacy scaffolded entity (kept for reference)
    // public virtual DbSet<Infrastructure.Persistence.Entities.Generated.Empleado> EmpleadosLegacy { get; set; }

    // DDD Refactored entity (replaces legacy Empleado)
    public virtual DbSet<Domain.Entities.Empleados.Empleado> Empleados { get; set; }

    // DDD Refactored entity (replaces legacy Remuneracione)
    public virtual DbSet<Domain.Entities.Empleados.Remuneracion> Remuneraciones { get; set; }

    // Legacy scaffolded entity (kept for reference - OBSOLETE, use ReciboDetalle DDD)
    // public virtual DbSet<EmpleadorRecibosDetalle> EmpleadorRecibosDetallesLegacy { get; set; }

    // DDD Refactored entity (replaces EmpleadorRecibosDetalle)
    public virtual DbSet<ReciboDetalle> RecibosDetalle { get; set; }

    // Legacy scaffolded entity (kept for reference)
    // public virtual DbSet<Infrastructure.Persistence.Entities.Generated.EmpleadorRecibosDetalleContratacione> EmpleadorRecibosDetalleContratacionesLegacy { get; set; }

    // DDD Refactored entity (replaces legacy EmpleadorRecibosDetalleContratacione)
    public virtual DbSet<Domain.Entities.Pagos.EmpleadorRecibosDetalleContratacione> EmpleadorRecibosDetalleContrataciones { get; set; }

    // Legacy scaffolded entity (kept for reference - OBSOLETE, use ReciboHeader DDD)
    // public virtual DbSet<EmpleadorRecibosHeader> EmpleadorRecibosHeadersLegacy { get; set; }

    // DDD Refactored entity (replaces EmpleadorRecibosHeader)
    public virtual DbSet<ReciboHeader> RecibosHeader { get; set; }

    // Legacy scaffolded entity (kept for reference)
    // public virtual DbSet<Infrastructure.Persistence.Entities.Generated.EmpleadorRecibosHeaderContratacione> EmpleadorRecibosHeaderContratacionesLegacy { get; set; }

    // DDD Refactored entity (replaces legacy EmpleadorRecibosHeaderContratacione)
    public virtual DbSet<Domain.Entities.Pagos.EmpleadorRecibosHeaderContratacione> EmpleadorRecibosHeaderContrataciones { get; set; }

    // Legacy scaffolded entity (kept for reference)
    // public virtual DbSet<EmpleadosNota> EmpleadosNotasLegacy { get; set; }

    // DDD Refactored entity (replaces EmpleadosNota)
    public virtual DbSet<EmpleadoNota> EmpleadosNotas { get; set; }

    // Legacy scaffolded entity (kept for reference - OBSOLETE, use EmpleadoTemporal DDD)
    // public virtual DbSet<EmpleadosTemporale> EmpleadosTemporalesLegacy { get; set; }

    // DDD Refactored entity (replaces EmpleadosTemporale)
    public virtual DbSet<EmpleadoTemporal> EmpleadosTemporales { get; set; }

    // Commented out - using Empleadores instead (DDD refactored)
    // public virtual DbSet<Ofertante> Ofertantes { get; set; }

    // Legacy scaffolded entity (kept for reference)
    // public virtual DbSet<Infrastructure.Persistence.Entities.Generated.PaymentGateway> PaymentGatewaysLegacy { get; set; }

    // DDD Refactored entity (replaces legacy PaymentGateway)
    public virtual DbSet<Domain.Entities.Pagos.PaymentGateway> PaymentGateways { get; set; }

    // Legacy scaffolded entity (kept for reference)
    // public virtual DbSet<Infrastructure.Persistence.Entities.Generated.Perfile> PerfilesLegacy { get; set; }

    // DDD Refactored entity (replaces legacy Perfile)
    public virtual DbSet<Domain.Entities.Seguridad.Perfile> Perfiles { get; set; }

    // Legacy scaffolded entity (kept for reference)
    // public virtual DbSet<Infrastructure.Persistence.Entities.Generated.PerfilesInfo> PerfilesInfosLegacy { get; set; }

    // DDD Refactored entity (replaces legacy PerfilesInfo)
    public virtual DbSet<Domain.Entities.Seguridad.PerfilesInfo> PerfilesInfos { get; set; }

    // Legacy scaffolded entity (kept for reference)
    // public virtual DbSet<Infrastructure.Persistence.Entities.Generated.Permiso> PermisosLegacy { get; set; }

    // DDD Refactored entity (replaces legacy Permiso)
    public virtual DbSet<Domain.Entities.Seguridad.Permiso> Permisos { get; set; }

    // Legacy scaffolded entity (kept for reference)
    // public virtual DbSet<PlanesContratista> PlanesContratistasLegacy { get; set; }

    // DDD Refactored entity (replaces PlanesContratista)
    public virtual DbSet<PlanContratista> PlanesContratistas { get; set; }

    // Legacy scaffolded entity (kept for reference)
    // public virtual DbSet<PlanesEmpleadore> PlanesEmpleadoresLegacy { get; set; }

    // DDD Refactored entity (replaces PlanesEmpleadore)
    public virtual DbSet<PlanEmpleador> PlanesEmpleadores { get; set; }

    // Legacy scaffolded entity (kept for reference)
    // public virtual DbSet<Infrastructure.Persistence.Entities.Generated.Provincia> ProvinciasLegacy { get; set; }

    // DDD Refactored entity (replaces legacy Provincia)
    public virtual DbSet<Domain.Entities.Catalogos.Provincia> Provincias { get; set; }

    // Legacy scaffolded entity (kept for reference)
    // public virtual DbSet<Sectore> SectoresLegacy { get; set; }

    // DDD Refactored entity (replaces Sectore)
    public virtual DbSet<Sector> Sectores { get; set; }

    // Legacy scaffolded entity (kept for reference)
    // public virtual DbSet<Infrastructure.Persistence.Entities.Generated.Servicio> ServiciosLegacy { get; set; }

    // DDD Refactored entity (replaces legacy Servicio)
    public virtual DbSet<Domain.Entities.Catalogos.Servicio> Servicios { get; set; }

    // Legacy scaffolded entity (kept for reference)
    // public virtual DbSet<Suscripcione> SuscripcionesLegacy { get; set; }

    // DDD Refactored entity (replaces Suscripcione)
    public virtual DbSet<Suscripcion> Suscripciones { get; set; }

    // ========================================
    // DATABASE VIEWS (Read-Only Models)
    // ========================================
    // Views are read-only database views mapped to simplified read models.
    // They do NOT support INSERT/UPDATE/DELETE operations.
    // Located in Domain.ReadModels namespace.

    // Legacy scaffolded view (kept for reference)
    // public virtual DbSet<Vcalificacione> VcalificacionesLegacy { get; set; }

    // Read Model for VCalificaciones view (replaces Vcalificacione)
    public virtual DbSet<VistaCalificacion> VistasCalificacion { get; set; }

    // Legacy scaffolded view (kept for reference)
    // public virtual DbSet<VcontratacionesTemporale> VcontratacionesTemporalesLegacy { get; set; }

    // Read Model for VContratacionesTemporales view (replaces VcontratacionesTemporale)
    public virtual DbSet<VistaContratacionTemporal> VistasContratacionTemporal { get; set; }

    // Legacy scaffolded view (kept for reference)
    // public virtual DbSet<Vcontratista> VcontratistasLegacy { get; set; }

    // Read Model for VContratistas view (replaces Vcontratista)
    public virtual DbSet<VistaContratista> VistasContratista { get; set; }

    // Legacy scaffolded view (kept for reference)
    // public virtual DbSet<Vempleado> VempleadosLegacy { get; set; }

    // Read Model for VEmpleados view (replaces Vempleado)
    public virtual DbSet<VistaEmpleado> VistasEmpleado { get; set; }

    // Legacy scaffolded view (kept for reference)
    // public virtual DbSet<Vpago> VpagosLegacy { get; set; }

    // Read Model for VPagos view (replaces Vpago)
    public virtual DbSet<VistaPago> VistasPago { get; set; }

    // Legacy scaffolded view (kept for reference)
    // public virtual DbSet<VpagosContratacione> VpagosContratacionesLegacy { get; set; }

    // Read Model for VPagosContrataciones view (replaces VpagosContratacione)
    public virtual DbSet<VistaPagoContratacion> VistasPagoContratacion { get; set; }

    // Legacy scaffolded view (kept for reference)
    // public virtual DbSet<Vperfile> VperfilesLegacy { get; set; }

    // Read Model for VPerfiles view (replaces Vperfile)
    public virtual DbSet<VistaPerfil> VistasPerfil { get; set; }

    // Legacy scaffolded view (kept for reference)
    // public virtual DbSet<VpromedioCalificacion> VpromedioCalificacionLegacy { get; set; }

    // Read Model for VPromedioCalificacion view (replaces VpromedioCalificacion)
    public virtual DbSet<VistaPromedioCalificacion> VistasPromedioCalificacion { get; set; }

    // Legacy scaffolded view (kept for reference)
    // public virtual DbSet<Vsuscripcione> VsuscripcionesLegacy { get; set; }

    // Read Model for VSuscripciones view (replaces Vsuscripcione)
    public virtual DbSet<VistaSuscripcion> VistasSuscripcion { get; set; }

    // ========================================
    // END DATABASE VIEWS
    // ========================================

    // Legacy scaffolded views (kept for reference - replaced by VistasXxx DbSets above)
    // public virtual DbSet<Vcalificacione> VcalificacionesLegacy { get; set; }
    // public virtual DbSet<VcontratacionesTemporale> VcontratacionesTemporalesLegacy { get; set; }
    // public virtual DbSet<Vcontratista> VcontratistasLegacy { get; set; }
    // public virtual DbSet<Vempleado> VempleadosLegacy { get; set; }
    // public virtual DbSet<Vpago> VpagosLegacy { get; set; }
    // public virtual DbSet<VpagosContratacione> VpagosContratacionesLegacy { get; set; }
    // public virtual DbSet<Vperfile> VperfilesLegacy { get; set; }
    // public virtual DbSet<VpromedioCalificacion> VpromedioCalificacionLegacy { get; set; }
    // public virtual DbSet<Vsuscripcione> VsuscripcionesLegacy { get; set; }

    // Legacy scaffolded entity (kept for reference)
    // public virtual DbSet<Infrastructure.Persistence.Entities.Generated.Venta> VentasLegacy { get; set; }

    // DDD Refactored entity (replaces legacy Venta)
    public virtual DbSet<Domain.Entities.Pagos.Venta> Ventas { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // CRITICAL: Call base method to configure Identity tables
        base.OnModelCreating(modelBuilder);

        // ========================================
        // ASP.NET CORE IDENTITY CONFIGURATION
        // ========================================
        
        // Customize Identity table names (optional - can keep defaults)
        modelBuilder.Entity<ApplicationUser>(entity =>
        {
            entity.ToTable("AspNetUsers");
            // ApplicationUser custom properties already defined in class
        });

        // Configure RefreshToken entity
        modelBuilder.Entity<RefreshToken>(entity =>
        {
            entity.ToTable("RefreshTokens");
            
            entity.HasKey(e => e.Id);
            
            // Token must be unique
            entity.HasIndex(e => e.Token)
                .IsUnique()
                .HasDatabaseName("IX_RefreshTokens_Token");
            
            // Relationship: ApplicationUser (1) -> RefreshTokens (many)
            entity.HasOne(e => e.User)
                .WithMany(u => u.RefreshTokens)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade) // Delete tokens when user is deleted
                .HasConstraintName("FK_RefreshTokens_AspNetUsers_UserId");
            
            // String length constraints
            entity.Property(e => e.Token).HasMaxLength(200).IsRequired();
            entity.Property(e => e.UserId).HasMaxLength(450).IsRequired(); // AspNetUsers.Id is nvarchar(450)
            entity.Property(e => e.ReplacedByToken).HasMaxLength(200);
            entity.Property(e => e.ReasonRevoked).HasMaxLength(500);
            entity.Property(e => e.CreatedByIp).HasMaxLength(50);
            entity.Property(e => e.RevokedByIp).HasMaxLength(50);
            
            // Default values
            entity.Property(e => e.Created).HasDefaultValueSql("GETUTCDATE()");
        });

        // ========================================
        // DOMAIN ENTITIES CONFIGURATION
        // ========================================
        
        // Ignore domain events - they should NOT be persisted to database
        modelBuilder.Ignore<MiGenteEnLinea.Domain.Common.DomainEvent>();
        
        // ========================================
        // IGNORE LEGACY SCAFFOLDED ENTITIES
        // ========================================
        // These entities were generated from the database scaffold but have been
        // replaced by DDD-refactored entities. We explicitly ignore them to prevent
        // EF Core from attempting to map them to the same tables as their replacements.
        
        modelBuilder.Ignore<Infrastructure.Persistence.Entities.Generated.Calificacione>();
        modelBuilder.Ignore<Infrastructure.Persistence.Entities.Generated.ConfigCorreo>();
        modelBuilder.Ignore<Infrastructure.Persistence.Entities.Generated.Contratista>();
        modelBuilder.Ignore<Infrastructure.Persistence.Entities.Generated.ContratistasFoto>();
        modelBuilder.Ignore<Infrastructure.Persistence.Entities.Generated.ContratistasServicio>();
        modelBuilder.Ignore<Infrastructure.Persistence.Entities.Generated.Credenciale>();
        modelBuilder.Ignore<Infrastructure.Persistence.Entities.Generated.DeduccionesTss>();
        modelBuilder.Ignore<Infrastructure.Persistence.Entities.Generated.DetalleContratacione>();
        modelBuilder.Ignore<Infrastructure.Persistence.Entities.Generated.Empleado>();
        modelBuilder.Ignore<Infrastructure.Persistence.Entities.Generated.EmpleadorRecibosDetalle>();
        modelBuilder.Ignore<Infrastructure.Persistence.Entities.Generated.EmpleadorRecibosDetalleContratacione>();
        modelBuilder.Ignore<Infrastructure.Persistence.Entities.Generated.EmpleadorRecibosHeader>();
        modelBuilder.Ignore<Infrastructure.Persistence.Entities.Generated.EmpleadorRecibosHeaderContratacione>();
        modelBuilder.Ignore<Infrastructure.Persistence.Entities.Generated.EmpleadosNota>();
        modelBuilder.Ignore<Infrastructure.Persistence.Entities.Generated.EmpleadosTemporale>();
        modelBuilder.Ignore<Infrastructure.Persistence.Entities.Generated.Ofertante>();
        modelBuilder.Ignore<Infrastructure.Persistence.Entities.Generated.PaymentGateway>();
        modelBuilder.Ignore<Infrastructure.Persistence.Entities.Generated.Perfile>();
        modelBuilder.Ignore<Infrastructure.Persistence.Entities.Generated.PerfilesInfo>();
        modelBuilder.Ignore<Infrastructure.Persistence.Entities.Generated.Permiso>();
        modelBuilder.Ignore<Infrastructure.Persistence.Entities.Generated.PlanesContratista>();
        modelBuilder.Ignore<Infrastructure.Persistence.Entities.Generated.PlanesEmpleadore>();
        modelBuilder.Ignore<Infrastructure.Persistence.Entities.Generated.Provincia>();
        
        // ✅ MIGRATED TO DDD: Use Domain.Entities.Empleados.Remuneracion (DDD) instead of Generated.Remuneracione
        modelBuilder.Ignore<Infrastructure.Persistence.Entities.Generated.Remuneracione>(); // Legacy entity ignored
        
        modelBuilder.Ignore<Infrastructure.Persistence.Entities.Generated.Sectore>();
        modelBuilder.Ignore<Infrastructure.Persistence.Entities.Generated.Servicio>();
        modelBuilder.Ignore<Infrastructure.Persistence.Entities.Generated.Suscripcione>();
        modelBuilder.Ignore<Infrastructure.Persistence.Entities.Generated.Vcalificacione>();
        modelBuilder.Ignore<Infrastructure.Persistence.Entities.Generated.VcontratacionesTemporale>();
        modelBuilder.Ignore<Infrastructure.Persistence.Entities.Generated.Vcontratista>();
        modelBuilder.Ignore<Infrastructure.Persistence.Entities.Generated.Vempleado>();
        modelBuilder.Ignore<Infrastructure.Persistence.Entities.Generated.Vpago>();
        modelBuilder.Ignore<Infrastructure.Persistence.Entities.Generated.VpagosContratacione>();
        modelBuilder.Ignore<Infrastructure.Persistence.Entities.Generated.Vperfile>();
        modelBuilder.Ignore<Infrastructure.Persistence.Entities.Generated.VpromedioCalificacion>();
        modelBuilder.Ignore<Infrastructure.Persistence.Entities.Generated.Vsuscripcione>();
        modelBuilder.Ignore<Infrastructure.Persistence.Entities.Generated.Venta>();
        
        // Apply all configurations from assembly
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(MiGenteDbContext).Assembly);

        // Legacy Contratista relationships (commented out - using refactored version)
        // The refactored Contratista configuration handles these relationships
        /*
        modelBuilder.Entity<ContratistasFoto>(entity =>
        {
            entity.HasOne(d => d.Contratista).WithMany(p => p.ContratistasFotos).HasConstraintName("FK_Contratistas_Fotos_Contratistas");
        });

        modelBuilder.Entity<ContratistasServicio>(entity =>
        {
            entity.HasOne(d => d.Contratista).WithMany(p => p.ContratistasServicios).HasConstraintName("FK_Contratistas_Servicios_Contratistas");
        });
        */

        // Legacy Credenciale mapping (commented out - using refactored version)
        // modelBuilder.Entity<Credenciale>(entity =>
        // {
        //     entity.Property(e => e.Activo).HasDefaultValue(false);
        // });

        // Legacy DeduccionesTss mapping (commented out - using refactored version)
        // modelBuilder.Entity<DeduccionesTss>(entity =>
        // {
        //     entity.HasKey(e => e.Id).HasName("PK_Deducciones");
        // });

        // Legacy DetalleContratacione mapping (commented out - using refactored DetalleContratacion version)
        // modelBuilder.Entity<DetalleContratacione>(entity =>
        // {
        //     entity.HasOne(d => d.Contratacion).WithMany(p => p.DetalleContrataciones).HasConstraintName("FK_DetalleContrataciones_EmpleadosTemporales");
        // });

        // Legacy EmpleadorRecibosDetalle mapping (commented out - using refactored ReciboDetalle version)
        // modelBuilder.Entity<EmpleadorRecibosDetalle>(entity =>
        // {
        //     entity.HasOne(d => d.Pago).WithMany(p => p.EmpleadorRecibosDetalles).HasConstraintName("FK_Empleador_Recibos_Detalle_Empleador_Recibos_Header");
        // });

        // Legacy EmpleadorRecibosDetalleContratacione mapping (commented out - using DDD refactored version with Fluent API)
        // modelBuilder.Entity<EmpleadorRecibosDetalleContratacione>(entity =>
        // {
        //     entity.HasOne(d => d.Pago).WithMany(p => p.EmpleadorRecibosDetalleContrataciones).HasConstraintName("FK_Empleador_Recibos_Detalle_Contrataciones_Empleador_Recibos_Header_Contrataciones");
        // });

        // Legacy EmpleadorRecibosHeader mapping (commented out - using refactored ReciboHeader version)
        // modelBuilder.Entity<EmpleadorRecibosHeader>(entity =>
        // {
        //     entity.HasOne(d => d.Empleado).WithMany(p => p.EmpleadorRecibosHeaders).HasConstraintName("FK_Empleador_Recibos_Header_Empleados");
        // });

        // Legacy EmpleadorRecibosHeaderContratacione mapping (commented out - using DDD refactored version with Fluent API)
        // modelBuilder.Entity<EmpleadorRecibosHeaderContratacione>(entity =>
        // {
        //     entity.HasOne(d => d.Contratacion).WithMany(p => p.EmpleadorRecibosHeaderContrataciones).HasConstraintName("FK_Empleador_Recibos_Header_Contrataciones_EmpleadosTemporales");
        // });

        // Legacy Ofertante mapping (commented out - using refactored Empleador version)
        // modelBuilder.Entity<Ofertante>(entity =>
        // {
        //     entity.HasKey(e => e.OfertanteId).HasName("PK__Ofertant__B6039B8F8B329CD8");
        // });

        // Legacy Perfile mapping (commented out - using refactored Perfile version)
        // modelBuilder.Entity<Perfile>(entity =>
        // {
        //     entity.Property(e => e.FechaCreacion).HasDefaultValueSql("(getdate())");
        // });

        // Legacy PerfilesInfo mapping (commented out - using refactored PerfilesInfo version)
        // modelBuilder.Entity<PerfilesInfo>(entity =>
        // {
        //     entity.HasOne(d => d.Perfil).WithMany(p => p.PerfilesInfos).HasConstraintName("FK_perfilesInfo_Perfiles");
        // });

        // Legacy PlanesContratista mapping (commented out - using refactored PlanContratista version)
        // modelBuilder.Entity<PlanesContratista>(entity =>
        // {
        //     entity.Property(e => e.PlanId).ValueGeneratedNever();
        // });

        // Legacy PlanesEmpleadore mapping (commented out - using refactored PlanEmpleador version)
        // modelBuilder.Entity<PlanesEmpleadore>(entity =>
        // {
        //     entity.HasKey(e => e.PlanId).HasName("PK_Planes");
        //     entity.Property(e => e.Empleados).HasDefaultValue(0);
        //     entity.Property(e => e.Historico).HasDefaultValue(0);
        //     entity.Property(e => e.Nomina).HasDefaultValue(false);
        // });

        // Legacy view mappings (commented out - replaced by configurations in ReadModels/ folder)
        // These are now handled by IEntityTypeConfiguration classes applied via ApplyConfigurationsFromAssembly above.
        // modelBuilder.Entity<Vcalificacione>(entity =>
        // {
        //     entity.ToView("VCalificaciones");
        // });
        //
        // modelBuilder.Entity<VcontratacionesTemporale>(entity =>
        // {
        //     entity.ToView("VContratacionesTemporales");
        // });
        //
        // modelBuilder.Entity<Vcontratista>(entity =>
        // {
        //     entity.ToView("VContratistas");
        // });
        //
        // modelBuilder.Entity<Vempleado>(entity =>
        // {
        //     entity.ToView("VEmpleados");
        //     entity.Property(e => e.EmpleadoId).ValueGeneratedOnAdd();
        // });
        //
        // modelBuilder.Entity<Vpago>(entity =>
        // {
        //     entity.ToView("VPagos");
        // });
        //
        // modelBuilder.Entity<VpagosContratacione>(entity =>
        // {
        //     entity.ToView("VPagosContrataciones");
        // });
        //
        // modelBuilder.Entity<Vperfile>(entity =>
        // {
        //     entity.ToView("VPerfiles");
        // });
        //
        // modelBuilder.Entity<VpromedioCalificacion>(entity =>
        // {
        //     entity.ToView("VPromedioCalificacion");
        // });
        //
        // modelBuilder.Entity<Vsuscripcione>(entity =>
        // {
        //     entity.ToView("VSuscripciones");
        // });

        // ============================================
        // GLOBAL QUERY FILTERS (Soft Delete)
        // Agregado: Oct 2025
        // ============================================
        // Empleador: Excluir eliminados lógicamente
        modelBuilder.Entity<Empleador>()
            .HasQueryFilter(e => !e.IsDeleted);

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
