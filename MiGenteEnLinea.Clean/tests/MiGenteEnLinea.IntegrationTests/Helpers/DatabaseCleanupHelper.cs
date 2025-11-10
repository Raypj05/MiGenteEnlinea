using Microsoft.EntityFrameworkCore;
using MiGenteEnLinea.Infrastructure.Persistence.Contexts;

namespace MiGenteEnLinea.IntegrationTests.Helpers;

/// <summary>
/// Helper para limpiar la base de datos antes de ejecutar test suites.
/// Trunca tablas en el orden correcto respetando foreign keys.
/// </summary>
public static class DatabaseCleanupHelper
{
    /// <summary>
    /// Limpia todas las tablas de test data manteniendo datos de referencia (planes, servicios, etc.)
    /// IMPORTANTE: El orden de DELETE debe respetar las foreign keys (children → parents)
    /// </summary>
    public static async Task CleanupTestDataAsync(MiGenteDbContext context)
    {
        // PASO 1: Deshabilitar ALL constraints temporalmente (más seguro que hacerlo tabla por tabla)
        await context.Database.ExecuteSqlRawAsync("EXEC sp_MSforeachtable 'ALTER TABLE ? NOCHECK CONSTRAINT ALL'");
        
        // PASO 2: Delete test data en orden correcto (children first, parents last)
        // Identificamos test data por userId que contiene "test"
        // ⚠️ IMPORTANTE: Solo se borran tablas que EXISTEN en las migraciones
        
        // 2.1: Tablas hijas de empleados (si existen)
        try
        {
            await context.Database.ExecuteSqlRawAsync(@"
                IF OBJECT_ID('dbo.Empleados_Notas', 'U') IS NOT NULL
                    DELETE FROM Empleados_Notas 
                    WHERE empleadoID IN (SELECT empleadoID FROM Empleados WHERE userID LIKE '%test%')
            ");
        }
        catch { /* Tabla no existe, continuar */ }
        
        // 2.2: Tablas de recibos (empleador payroll)
        try
        {
            await context.Database.ExecuteSqlRawAsync(@"
                IF OBJECT_ID('dbo.Empleador_Recibos_Detalle', 'U') IS NOT NULL
                    DELETE FROM Empleador_Recibos_Detalle 
                    WHERE reciboID IN (
                        SELECT reciboID FROM Empleador_Recibos_Header WHERE userID LIKE '%test%'
                    )
            ");
            
            await context.Database.ExecuteSqlRawAsync(@"
                IF OBJECT_ID('dbo.Empleador_Recibos_Header', 'U') IS NOT NULL
                    DELETE FROM Empleador_Recibos_Header WHERE userID LIKE '%test%'
            ");
        }
        catch { /* Tablas no existen, continuar */ }
        
        // 2.3: Tablas de contratistas_servicios
        try
        {
            await context.Database.ExecuteSqlRawAsync(@"
                IF OBJECT_ID('dbo.Contratistas_Servicios', 'U') IS NOT NULL
                    DELETE FROM Contratistas_Servicios 
                    WHERE contratistaID IN (SELECT contratistaID FROM Contratistas WHERE userID LIKE '%test%')
            ");
        }
        catch { /* Tabla no existe, continuar */ }
        
        // 2.4: Suscripciones (must delete before main entities to avoid FK violations)
        await context.Database.ExecuteSqlRawAsync(@"
            IF OBJECT_ID('dbo.Suscripciones', 'U') IS NOT NULL
                DELETE FROM Suscripciones WHERE userID LIKE '%test%'
        ");
        
        // 2.5: Tablas principales (nivel 2 - children ya borrados)
        await context.Database.ExecuteSqlRawAsync(@"
            IF OBJECT_ID('dbo.Empleados_Temporales', 'U') IS NOT NULL
                DELETE FROM Empleados_Temporales WHERE userID LIKE '%test%'
        ");
        
        await context.Database.ExecuteSqlRawAsync(@"
            IF OBJECT_ID('dbo.Empleados', 'U') IS NOT NULL
                DELETE FROM Empleados WHERE userID LIKE '%test%'
        ");
        
        await context.Database.ExecuteSqlRawAsync(@"
            IF OBJECT_ID('dbo.Contratistas', 'U') IS NOT NULL
                DELETE FROM Contratistas WHERE userID LIKE '%test%'
        ");
        
        await context.Database.ExecuteSqlRawAsync(@"
            IF OBJECT_ID('dbo.Ofertantes', 'U') IS NOT NULL
                DELETE FROM Ofertantes WHERE userID LIKE '%test%'
        ");
        
        await context.Database.ExecuteSqlRawAsync(@"
            IF OBJECT_ID('dbo.Perfiles', 'U') IS NOT NULL
                DELETE FROM Perfiles WHERE userID LIKE '%test%'
        ");
        
        // 2.6: Tabla padre (nivel 1 - todos los hijos ya borrados)
        await context.Database.ExecuteSqlRawAsync(@"
            IF OBJECT_ID('dbo.Credenciales', 'U') IS NOT NULL
                DELETE FROM Credenciales WHERE userID LIKE '%test%'
        ");
        
        // PASO 3: Re-habilitar ALL constraints
        await context.Database.ExecuteSqlRawAsync("EXEC sp_MSforeachtable 'ALTER TABLE ? CHECK CONSTRAINT ALL'");
    }
    
    /// <summary>
    /// Limpia TODA la base de datos (incluyendo datos de referencia) - usar con precaución.
    /// Solo para desarrollo local.
    /// </summary>
    public static async Task CleanupAllDataAsync(MiGenteDbContext context)
    {
        // PASO 1: Deshabilitar ALL constraints
        await context.Database.ExecuteSqlRawAsync("EXEC sp_MSforeachtable 'ALTER TABLE ? NOCHECK CONSTRAINT ALL'");
        
        // PASO 2: Truncar todas las tablas
        await context.Database.ExecuteSqlRawAsync(@"
            EXEC sp_MSforeachtable 'DELETE FROM ?'
        ");
        
        // PASO 3: Re-habilitar constraints
        await context.Database.ExecuteSqlRawAsync("EXEC sp_MSforeachtable 'ALTER TABLE ? CHECK CONSTRAINT ALL'");
        
        // PASO 4: Re-seed datos de referencia
        await Infrastructure.TestDataSeeder.SeedAllAsync(context);
    }
}
