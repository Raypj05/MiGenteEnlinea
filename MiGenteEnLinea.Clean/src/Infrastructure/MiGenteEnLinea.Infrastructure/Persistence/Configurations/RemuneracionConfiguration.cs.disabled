using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MiGenteEnLinea.Domain.Entities.Empleados;

namespace MiGenteEnLinea.Infrastructure.Persistence.Configurations;

/// <summary>
/// Configuración de EF Core para la entidad Remuneracion.
/// Mapea a la tabla Legacy "Remuneraciones".
/// </summary>
public class RemuneracionConfiguration : IEntityTypeConfiguration<Remuneracion>
{
    public void Configure(EntityTypeBuilder<Remuneracion> builder)
    {
        // Tabla
        builder.ToTable("Remuneraciones");

        // Primary Key
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id)
            .HasColumnName("id");

        // UserId (FK a Credencial)
        builder.Property(r => r.UserId)
            .IsRequired()
            .HasMaxLength(128)
            .HasColumnName("userID");

        // EmpleadoId (FK a Empleado)
        builder.Property(r => r.EmpleadoId)
            .IsRequired()
            .HasColumnName("empleadoID");

        // Descripcion
        builder.Property(r => r.Descripcion)
            .IsRequired()
            .HasMaxLength(500)
            .HasColumnName("descripcion");

        // Monto
        builder.Property(r => r.Monto)
            .IsRequired()
            .HasColumnType("decimal(18,2)")
            .HasColumnName("monto");

        // Índices
        builder.HasIndex(r => r.EmpleadoId)
            .HasDatabaseName("IX_Remuneraciones_EmpleadoId");

        builder.HasIndex(r => r.UserId)
            .HasDatabaseName("IX_Remuneraciones_UserId");
    }
}
