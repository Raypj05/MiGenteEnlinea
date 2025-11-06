using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MiGenteEnLinea.Domain.Entities.Nominas;

namespace MiGenteEnLinea.Infrastructure.Persistence.Configurations;

/// <summary>
/// Configuración de Entity Framework Core para la entidad ReciboHeader.
/// Mapea a la tabla legacy "Empleador_Recibos_Header".
/// </summary>
public sealed class ReciboHeaderConfiguration : IEntityTypeConfiguration<ReciboHeader>
{
    public void Configure(EntityTypeBuilder<ReciboHeader> builder)
    {
        // Tabla
        builder.ToTable("Empleador_Recibos_Header");

        // Primary Key
        builder.HasKey(r => r.PagoId);
        builder.Property(r => r.PagoId)
            .HasColumnName("pagoID")
            .ValueGeneratedOnAdd();

        // Propiedades requeridas
        builder.Property(r => r.UserId)
            .IsRequired()
            .HasColumnName("userID")
            .HasMaxLength(250) // Debe coincidir con Credenciales.userID
            .IsUnicode(false);

        builder.Property(r => r.EmpleadoId)
            .IsRequired()
            .HasColumnName("empleadoID");

        builder.Property(r => r.FechaRegistro)
            .IsRequired()
            .HasColumnName("fechaRegistro")
            .HasColumnType("datetime");

        builder.Property(r => r.ConceptoPago)
            .IsRequired()
            .HasColumnName("conceptoPago")
            .HasMaxLength(50)
            .IsUnicode(false);

        builder.Property(r => r.Tipo)
            .IsRequired()
            .HasColumnName("tipo")
            .HasDefaultValue(1);

        builder.Property(r => r.Estado)
            .IsRequired()
            .HasColumnName("estado")
            .HasDefaultValue(1);

        // Propiedades opcionales
        builder.Property(r => r.FechaPago)
            .HasColumnName("fechaPago")
            .HasColumnType("datetime");

        builder.Property(r => r.PeriodoInicio)
            .HasColumnName("periodo_inicio");

        builder.Property(r => r.PeriodoFin)
            .HasColumnName("periodo_fin");

        builder.Property(r => r.TotalIngresos)
            .IsRequired()
            .HasColumnName("total_ingresos")
            .HasColumnType("decimal(12, 2)")
            .HasDefaultValue(0);

        builder.Property(r => r.TotalDeducciones)
            .IsRequired()
            .HasColumnName("total_deducciones")
            .HasColumnType("decimal(12, 2)")
            .HasDefaultValue(0);

        builder.Property(r => r.NetoPagar)
            .IsRequired()
            .HasColumnName("neto_pagar")
            .HasColumnType("decimal(12, 2)")
            .HasDefaultValue(0);

        // Campos de auditoría (heredados de AggregateRoot/AuditableEntity)
        builder.Property(r => r.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired(false);

        builder.Property(r => r.CreatedBy)
            .HasColumnName("created_by")
            .HasMaxLength(100)
            .IsUnicode(false)
            .IsRequired(false);

        builder.Property(r => r.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired(false);

        builder.Property(r => r.UpdatedBy)
            .HasColumnName("updated_by")
            .HasMaxLength(100)
            .IsUnicode(false)
            .IsRequired(false);

        // Relaciones - Configurar navegación Detalles para soporte de Include()
        builder.HasMany(r => r.Detalles)
            .WithOne()
            .HasForeignKey(d => d.PagoId)
            .OnDelete(DeleteBehavior.Cascade);

        // Índices
        builder.HasIndex(r => r.UserId)
            .HasDatabaseName("IX_ReciboHeader_UserId");

        builder.HasIndex(r => r.EmpleadoId)
            .HasDatabaseName("IX_ReciboHeader_EmpleadoId");

        builder.HasIndex(r => r.Estado)
            .HasDatabaseName("IX_ReciboHeader_Estado");

        builder.HasIndex(r => r.FechaRegistro)
            .HasDatabaseName("IX_ReciboHeader_FechaRegistro");

        builder.HasIndex(r => r.FechaPago)
            .HasDatabaseName("IX_ReciboHeader_FechaPago");

        builder.HasIndex(r => new { r.UserId, r.Estado })
            .HasDatabaseName("IX_ReciboHeader_UserId_Estado");

        builder.HasIndex(r => new { r.EmpleadoId, r.FechaRegistro })
            .HasDatabaseName("IX_ReciboHeader_EmpleadoId_FechaRegistro");

        // Ignorar eventos de dominio
        builder.Ignore(r => r.Events);
    }
}
