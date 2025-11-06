using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MiGenteEnLinea.Domain.Entities.Empleados;

namespace MiGenteEnLinea.Infrastructure.Persistence.Configurations;

/// <summary>
/// Configuración EF Core para la entidad Remuneracion (DDD).
/// 
/// ✅ MIGRADO DE LEGACY: Generated.Remuneracione → Domain.Empleados.Remuneracion
/// 
/// Mapea a tabla existente: Remuneraciones
/// 
/// RELATIONSHIPS:
/// - FK: userID → Credenciales.UserId (Empleador)
/// - FK: empleadoID → Empleados.EmpleadoID
/// 
/// LEGACY SQL SCHEMA:
/// CREATE TABLE [dbo].[Remuneraciones](
///     [id] [int] IDENTITY(1,1) NOT NULL,
///     [userID] [varchar](50) NOT NULL,
///     [empleadoID] [int] NOT NULL,
///     [descripcion] [varchar](100) NOT NULL,
///     [monto] [decimal](18, 2) NOT NULL,
///     CONSTRAINT [PK_Remuneraciones] PRIMARY KEY CLUSTERED ([id])
/// )
/// </summary>
public class RemuneracionConfiguration : IEntityTypeConfiguration<Remuneracion>
{
    public void Configure(EntityTypeBuilder<Remuneracion> builder)
    {
        // Table mapping
        builder.ToTable("Remuneraciones");

        // Primary key
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        // Properties
        builder.Property(r => r.UserId)
            .IsRequired()
            .HasMaxLength(50)
            .HasColumnName("userID")
            .HasColumnType("varchar(50)");

        builder.Property(r => r.EmpleadoId)
            .IsRequired()
            .HasColumnName("empleadoID");

        builder.Property(r => r.Descripcion)
            .IsRequired()
            .HasMaxLength(100)
            .HasColumnName("descripcion")
            .HasColumnType("varchar(100)");

        builder.Property(r => r.Monto)
            .IsRequired()
            .HasColumnName("monto")
            .HasColumnType("decimal(18, 2)");

        // Foreign Keys (Shadow FK pattern - no navigation properties in entity)
        
        // FK to Credenciales (Empleador)
        // CRITICAL: UserId (string) references Credencial.UserId (alternate key), NOT Credencial.Id (int PK)
        builder.HasOne<Domain.Entities.Authentication.Credencial>()
            .WithMany()
            .HasForeignKey(r => r.UserId)
            .HasPrincipalKey(c => c.UserId) // ✅ SPECIFY: Use UserId (string), not Id (int)
            .HasConstraintName("FK_Remuneraciones_Credenciales")
            .OnDelete(DeleteBehavior.Cascade);

        // FK to Empleados
        builder.HasOne<Empleado>()
            .WithMany()
            .HasForeignKey(r => r.EmpleadoId)
            .HasConstraintName("FK_Remuneraciones_Empleados")
            .OnDelete(DeleteBehavior.Restrict); // Prevent cascade delete to preserve history

        // Indexes
        builder.HasIndex(r => r.UserId)
            .HasDatabaseName("IX_Remuneraciones_UserId");

        builder.HasIndex(r => r.EmpleadoId)
            .HasDatabaseName("IX_Remuneraciones_EmpleadoId");
    }
}
