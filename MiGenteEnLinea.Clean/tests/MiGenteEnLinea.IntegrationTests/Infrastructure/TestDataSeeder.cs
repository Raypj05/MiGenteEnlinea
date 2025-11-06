using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MiGenteEnLinea.Domain.Entities.Suscripciones;
using MiGenteEnLinea.Domain.Entities.Empleadores;
using MiGenteEnLinea.Domain.Entities.Contratistas;
using MiGenteEnLinea.Domain.Entities.Seguridad;
using MiGenteEnLinea.Domain.Entities.Authentication;
using MiGenteEnLinea.Domain.Entities.Nominas;
using MiGenteEnLinea.Infrastructure.Persistence.Contexts;
using MiGenteEnLinea.Infrastructure.Identity;
using MiGenteEnLinea.Application.Common.Interfaces;

namespace MiGenteEnLinea.IntegrationTests.Infrastructure;

/// <summary>
/// Seeder para crear datos de prueba realistas en la base de datos InMemory.
/// Crea un conjunto completo de entidades relacionadas: usuarios, planes, empleadores, contratistas, etc.
/// 
/// ✅ IMPORTANTE: Usa Identity PasswordHasher para consistencia con el sistema de autenticación
/// </summary>
public static class TestDataSeeder
{
    /// <summary>
    /// Password común para todos los usuarios de prueba
    /// Password en texto plano simple para testing
    /// </summary>
    public const string TestPasswordPlainText = "Test1234!";
    
    /// <summary>
    /// PasswordHasher de Identity para generar hashes consistentes
    /// Usa el MISMO hasher que el sistema de autenticación
    /// </summary>
    private static readonly PasswordHasher<ApplicationUser> PasswordHasher = new();
    
    /// <summary>
    /// Hash precomputado usando Identity PasswordHasher
    /// ⚠️ CRÍTICO: Este hash es FIJO y NO debe cambiar entre ejecuciones
    /// Fue generado con: PasswordHasher.HashPassword(new ApplicationUser { UserName = "test@test.com" }, "Test1234!")
    /// Si cambias el password, debes regenerar este hash con HashGeneratorTest.GenerateIdentityHash()
    /// </summary>
    public static readonly string TestPasswordHash = "AQAAAAIAAYagAAAAEPqQJV1/gxmRkRauH9xh/wye7xscHsWNOgVPPslSG2rxsbKIrwrvZVRYcrtj5UiSvQ==";

    /// <summary>
    /// Limpia toda la base de datos de prueba
    /// </summary>
    public static async Task ClearDatabaseAsync(IApplicationDbContext context)
    {
        // Orden importante: eliminar en orden inverso a las relaciones FK
        context.Empleados.RemoveRange(context.Empleados);
        context.DetalleContrataciones.RemoveRange(context.DetalleContrataciones);
        context.Calificaciones.RemoveRange(context.Calificaciones);
        context.ContratistasServicios.RemoveRange(context.ContratistasServicios);
        context.Contratistas.RemoveRange(context.Contratistas);
        context.Empleadores.RemoveRange(context.Empleadores);
        context.Suscripciones.RemoveRange(context.Suscripciones);
        context.Credenciales.RemoveRange(context.Credenciales);
        context.Perfiles.RemoveRange(context.Perfiles);
        context.PlanesEmpleadores.RemoveRange(context.PlanesEmpleadores);
        context.PlanesContratistas.RemoveRange(context.PlanesContratistas);

        await context.SaveChangesAsync();
    }

    /// <summary>
    /// Seed completo: Planes + Usuarios (Empleadores y Contratistas) + Deducciones TSS
    /// </summary>
    public static async Task SeedAllAsync(IApplicationDbContext context)
    {
        // 🔍 DEBUG: Log hash being used
        Console.WriteLine($"🔍 DEBUG TestDataSeeder: TestPasswordHash = {TestPasswordHash.Substring(0, 30)}...");
        Console.WriteLine($"🔍 DEBUG TestDataSeeder: TestPasswordHash Length = {TestPasswordHash.Length}");
        
        await SeedPlanesAsync(context);
        await SeedPlanesContratistasAsync(context);
        await SeedDeduccionesTssAsync(context);
        await SeedUsuariosAsync(context);
    }

    /// <summary>
    /// Crea 3 planes de prueba: Básico, Profesional, Empresarial (para Empleadores)
    /// </summary>
    public static async Task<List<PlanEmpleador>> SeedPlanesAsync(IApplicationDbContext context)
    {
        if (await context.PlanesEmpleadores.AnyAsync())
        {
            return await context.PlanesEmpleadores.ToListAsync();
        }

        var planes = new List<PlanEmpleador>
        {
            PlanEmpleador.Create(
                nombre: "Plan Básico",
                precio: 500.00m,
                limiteEmpleados: 5,
                mesesHistorico: 6,
                incluyeNomina: true),
            
            PlanEmpleador.Create(
                nombre: "Plan Profesional",
                precio: 1500.00m,
                limiteEmpleados: 20,
                mesesHistorico: 12,
                incluyeNomina: true),
            
            PlanEmpleador.Create(
                nombre: "Plan Empresarial",
                precio: 3500.00m,
                limiteEmpleados: 999,
                mesesHistorico: 24,
                incluyeNomina: true)
        };

        context.PlanesEmpleadores.AddRange(planes);
        await context.SaveChangesAsync();
        return planes;
    }

    /// <summary>
    /// Crea planes de suscripción para Contratistas
    /// Los contratistas son proveedores de servicios que buscan trabajos temporales
    /// </summary>
    public static async Task<List<PlanContratista>> SeedPlanesContratistasAsync(IApplicationDbContext context)
    {
        if (await context.PlanesContratistas.AnyAsync())
        {
            return await context.PlanesContratistas.ToListAsync();
        }

        var planes = new List<PlanContratista>
        {
            PlanContratista.Create("Plan Básico Contratista", 300.00m),
            PlanContratista.Create("Plan Profesional Contratista", 800.00m),
            PlanContratista.Create("Plan Premium Contratista", 1500.00m)
        };

        context.PlanesContratistas.AddRange(planes);
        await context.SaveChangesAsync(default);

        return planes;
    }

    /// <summary>
    /// Crea las deducciones TSS (Tesorería de la Seguridad Social) de República Dominicana
    /// Incluye: AFP (pensión), SFS (salud), SRL (riesgos laborales)
    /// </summary>
    public static async Task SeedDeduccionesTssAsync(IApplicationDbContext context)
    {
        if (await context.DeduccionesTss.AnyAsync())
        {
            return; // Ya existen deducciones TSS
        }

        var deducciones = new List<DeduccionTss>
        {
            DeduccionTss.Create(
                descripcion: "AFP (Fondo de Pensiones)", 
                porcentaje: 2.87m),
            
            DeduccionTss.Create(
                descripcion: "SFS (Seguro Familiar)", 
                porcentaje: 3.04m),
            
            DeduccionTss.Create(
                descripcion: "SRL (Riesgos Laborales)", 
                porcentaje: 1.20m),
            
            DeduccionTss.Create(
                descripcion: "INFOTEP (Formación Técnica)", 
                porcentaje: 1.00m)
        };

        context.DeduccionesTss.AddRange(deducciones);
        await context.SaveChangesAsync();
    }

    /// <summary>
    /// Crea múltiples empleadores y contratistas con IDs predecibles para tests
    /// IDs: test-empleador-001 a test-empleador-119, test-empleador-301 a test-empleador-307
    ///      test-contratista-201 a test-contratista-210, test-contratista-305
    /// </summary>
    public static async Task<(List<Empleador> empleadores, List<Contratista> contratistas)> SeedUsuariosAsync(IApplicationDbContext context)
    {
        // ✅ IDEMPOTENCIA: Check if data already exists to avoid duplicate key errors
        var existingEmpleadores = await context.Empleadores.AsNoTracking().ToListAsync();
        var existingContratistas = await context.Contratistas.AsNoTracking().ToListAsync();
        
        if (existingEmpleadores.Any() || existingContratistas.Any())
        {
            Console.WriteLine($"⏭️ Skipping seeding: {existingEmpleadores.Count} empleadores and {existingContratistas.Count} contratistas already exist in database");
            return (existingEmpleadores, existingContratistas);
        }
        
        var planes = await context.PlanesEmpleadores.ToListAsync();
        var planesContratistas = await context.PlanesContratistas.ToListAsync();
        
        if (!planes.Any())
        {
            planes = await SeedPlanesAsync(context);
        }
        
        if (!planesContratistas.Any())
        {
            planesContratistas = await SeedPlanesContratistasAsync(context);
        }

        var empleadores = new List<Empleador>();
        var contratistas = new List<Contratista>();

        // ========================================
        // SEED EMPLEADORES (001-011, 101-119, 301-307)
        // ========================================
        
        // Range 1: test-empleador-001 to test-empleador-011 (11 empleadores)
        for (int i = 1; i <= 11; i++)
        {
            var userId = $"test-empleador-{i:D3}";
            await SeedEmpleadorAsync(context, userId, $"Empleador{i:D3}", "Test", $"empleador{i:D3}@test.com", planes, empleadores);
        }
        
        // Range 2: test-empleador-101 to test-empleador-119 (19 empleadores)
        for (int i = 101; i <= 119; i++)
        {
            var userId = $"test-empleador-{i}";
            await SeedEmpleadorAsync(context, userId, $"Empleador{i}", "Test", $"empleador{i}@test.com", planes, empleadores);
        }
        
        // Range 3: test-empleador-301 to test-empleador-307 (7 empleadores)
        for (int i = 301; i <= 307; i++)
        {
            var userId = $"test-empleador-{i}";
            await SeedEmpleadorAsync(context, userId, $"Empleador{i}", "Test", $"empleador{i}@test.com", planes, empleadores);
        }

        // ========================================
        // SEED CONTRATISTAS (201-210, 305)
        // ========================================
        
        // Range 1: test-contratista-201 to test-contratista-210 (10 contratistas)
        for (int i = 201; i <= 210; i++)
        {
            var userId = $"test-contratista-{i}";
            await SeedContratistaAsync(context, userId, $"Contratista{i}", "Test", $"contratista{i}@test.com", planesContratistas, contratistas);
        }
        
        // Special case: test-contratista-305
        await SeedContratistaAsync(context, "test-contratista-305", "Contratista305", "Test", "contratista305@test.com", planesContratistas, contratistas);

        return (empleadores, contratistas);
    }
    
    /// <summary>
    /// Helper method to seed a single Empleador with predictable ID
    /// </summary>
    private static async Task SeedEmpleadorAsync(
        IApplicationDbContext context, 
        string userId, 
        string nombre, 
        string apellido, 
        string email,
        List<PlanEmpleador> planes,
        List<Empleador> empleadores)
    {
        // Crear perfil
        var perfil = Perfile.CrearPerfilEmpleador(
            userId: userId,
            nombre: nombre,
            apellido: apellido,
            email: email,
            telefono1: "809-555-0000");
        context.Perfiles.Add(perfil);
        await context.SaveChangesAsync();

        // Crear credencial
        var credencial = Credencial.Create(
            userId: userId,
            email: Domain.ValueObjects.Email.Create(email)!, // Test data has valid emails
            passwordHash: TestPasswordHash);
        credencial.Activar(); // Activar la cuenta
        context.Credenciales.Add(credencial);
        await context.SaveChangesAsync();

        // Crear empleador
        var empleador = Empleador.Create(
            userId: userId,
            habilidades: "Gestión de proyectos",
            experiencia: "10 años",
            descripcion: $"Empleador de prueba {userId}");
        context.Empleadores.Add(empleador);
        await context.SaveChangesAsync();

        // Suscripción activa (Plan Profesional)
        var suscripcion = Suscripcion.Create(
            userId: userId,
            planId: planes[1].PlanId, // Plan Profesional
            duracionMeses: 1);
        context.Suscripciones.Add(suscripcion);
        await context.SaveChangesAsync();

        empleadores.Add(empleador);
    }
    
    /// <summary>
    /// Helper method to seed a single Contratista with predictable ID
    /// </summary>
    private static async Task SeedContratistaAsync(
        IApplicationDbContext context, 
        string userId, 
        string nombre, 
        string apellido, 
        string email,
        List<PlanContratista> planesContratistas,
        List<Contratista> contratistas)
    {
        // Crear perfil
        var perfil = Perfile.CrearPerfilContratista(
            userId: userId,
            nombre: nombre,
            apellido: apellido,
            email: email,
            telefono1: "809-555-0000");
        context.Perfiles.Add(perfil);
        await context.SaveChangesAsync();

        // Crear credencial
        var credencial = Credencial.Create(
            userId: userId,
            email: Domain.ValueObjects.Email.Create(email)!, // Test data has valid emails
            passwordHash: TestPasswordHash);
        credencial.Activar(); // Activar la cuenta
        context.Credenciales.Add(credencial);
        await context.SaveChangesAsync();

        // Crear contratista
        var contratista = Contratista.Create(
            userId: userId,
            nombre: nombre,
            apellido: apellido,
            tipo: 1, // Persona Física
            titulo: "Contratista profesional",
            identificacion: $"ID{userId.Replace("test-contratista-", "")}",
            sector: "Construcción",
            experiencia: 5,
            presentacion: $"Contratista de prueba {userId}",
            telefono1: "809-555-0000",
            whatsapp1: true,
            provincia: "Santo Domingo",
            nivelNacional: false);
        context.Contratistas.Add(contratista);
        await context.SaveChangesAsync();

        // Suscripción activa (Plan Básico)
        var suscripcion = Suscripcion.Create(
            userId: userId,
            planId: planesContratistas[0].PlanId, // Plan Básico
            duracionMeses: 1);
        context.Suscripciones.Add(suscripcion);
        await context.SaveChangesAsync();

        contratistas.Add(contratista);
    }

    /// <summary>
    /// Obtiene un empleador por userId
    /// </summary>
    public static async Task<Empleador?> GetEmpleadorByUserIdAsync(IApplicationDbContext context, string userId)
    {
        return await context.Empleadores
            .FirstOrDefaultAsync(e => e.UserId == userId);
    }

    /// <summary>
    /// Obtiene un contratista por userId
    /// </summary>
    public static async Task<Contratista?> GetContratistaByUserIdAsync(IApplicationDbContext context, string userId)
    {
        return await context.Contratistas
            .FirstOrDefaultAsync(c => c.UserId == userId);
    }
}
