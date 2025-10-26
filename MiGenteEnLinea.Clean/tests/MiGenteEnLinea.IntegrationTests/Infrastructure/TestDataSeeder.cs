using Microsoft.EntityFrameworkCore;
using MiGenteEnLinea.Domain.Entities.Suscripciones;
using MiGenteEnLinea.Domain.Entities.Empleadores;
using MiGenteEnLinea.Domain.Entities.Contratistas;
using MiGenteEnLinea.Domain.Entities.Seguridad;
using MiGenteEnLinea.Domain.Entities.Authentication;
using MiGenteEnLinea.Infrastructure.Persistence.Contexts;
using MiGenteEnLinea.Application.Common.Interfaces;
using BCrypt.Net;

namespace MiGenteEnLinea.IntegrationTests.Infrastructure;

/// <summary>
/// Seeder para crear datos de prueba realistas en la base de datos InMemory.
/// Crea un conjunto completo de entidades relacionadas: usuarios, planes, empleadores, contratistas, etc.
/// </summary>
public static class TestDataSeeder
{
    /// <summary>
    /// Password común para todos los usuarios de prueba (hasheado con BCrypt)
    /// Password en texto plano: "Test@1234"
    /// </summary>
    public const string TestPasswordPlainText = "Test@1234";
    public static readonly string TestPasswordHash = BCrypt.Net.BCrypt.HashPassword(TestPasswordPlainText, 12);

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
    /// Seed completo: Planes + Usuarios (Empleadores y Contratistas)
    /// </summary>
    public static async Task SeedAllAsync(IApplicationDbContext context)
    {
        await SeedPlanesAsync(context);
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
    /// Crea 2 empleadores y 2 contratistas de prueba con credenciales activas
    /// </summary>
    public static async Task<(List<Empleador> empleadores, List<Contratista> contratistas)> SeedUsuariosAsync(IApplicationDbContext context)
    {
        var planes = await context.PlanesEmpleadores.ToListAsync();
        if (!planes.Any())
        {
            planes = await SeedPlanesAsync(context);
        }

        var empleadores = new List<Empleador>();
        var contratistas = new List<Contratista>();

        // ========================================
        // EMPLEADOR 1: Juan Pérez (Activo, Con Plan)
        // ========================================
        var userId1 = Guid.NewGuid().ToString();
        
        // Crear perfil
        var perfil1 = Perfile.CrearPerfilEmpleador(
            userId: userId1,
            nombre: "Juan",
            apellido: "Pérez",
            email: "juan.perez@test.com",
            telefono1: "809-555-0001");
        context.Perfiles.Add(perfil1);
        await context.SaveChangesAsync();

        // Crear credencial
        var credencial1 = Credencial.Create(
            userId: userId1,
            email: Domain.ValueObjects.Email.Create("juan.perez@test.com"),
            passwordHash: TestPasswordHash);
        credencial1.Activar(); // Activar la cuenta
        context.Credenciales.Add(credencial1);
        await context.SaveChangesAsync();

        // Crear empleador
        var empleador1 = Empleador.Create(
            userId: userId1,
            habilidades: "Gestión de proyectos, Supervisión de equipos",
            experiencia: "15 años en construcción",
            descripcion: "Empresa líder en construcción residencial y comercial en Santo Domingo");
        context.Empleadores.Add(empleador1);
        await context.SaveChangesAsync();

        // Suscripción activa para empleador1
        var suscripcion1 = Suscripcion.Create(
            userId: userId1,
            planId: planes[1].PlanId, // Plan Profesional
            duracionMeses: 1);
        context.Suscripciones.Add(suscripcion1);
        await context.SaveChangesAsync();

        empleadores.Add(empleador1);

        // ========================================
        // EMPLEADOR 2: María García (Activo, Sin Plan - para probar flujo de compra)
        // ========================================
        var userId2 = Guid.NewGuid().ToString();
        
        var perfil2 = Perfile.CrearPerfilEmpleador(
            userId: userId2,
            nombre: "María",
            apellido: "García",
            email: "maria.garcia@test.com",
            telefono1: "809-555-0002");
        context.Perfiles.Add(perfil2);
        await context.SaveChangesAsync();

        var credencial2 = Credencial.Create(
            userId: userId2,
            email: Domain.ValueObjects.Email.Create("maria.garcia@test.com"),
            passwordHash: TestPasswordHash);
        credencial2.Activar();
        context.Credenciales.Add(credencial2);
        await context.SaveChangesAsync();

        var empleador2 = Empleador.Create(
            userId: userId2,
            habilidades: "Desarrollo software, Cloud computing",
            experiencia: "10 años en tecnología",
            descripcion: "Empresa de desarrollo de software y consultoría tecnológica");
        context.Empleadores.Add(empleador2);
        await context.SaveChangesAsync();

        empleadores.Add(empleador2);

        // ========================================
        // CONTRATISTA 1: Carlos Rodríguez (Activo, Con Plan)
        // ========================================
        var userId3 = Guid.NewGuid().ToString();
        
        var perfil3 = Perfile.CrearPerfilContratista(
            userId: userId3,
            nombre: "Carlos",
            apellido: "Rodríguez",
            email: "carlos.rodriguez@test.com",
            telefono1: "809-555-0003");
        context.Perfiles.Add(perfil3);
        await context.SaveChangesAsync();

        var credencial3 = Credencial.Create(
            userId: userId3,
            email: Domain.ValueObjects.Email.Create("carlos.rodriguez@test.com"),
            passwordHash: TestPasswordHash);
        credencial3.Activar();
        context.Credenciales.Add(credencial3);
        await context.SaveChangesAsync();

        var contratista1 = Contratista.Create(
            userId: userId3,
            nombre: "Carlos",
            apellido: "Rodríguez",
            tipo: 1, // Persona Física
            titulo: "Plomero certificado con 10 años de experiencia",
            identificacion: "001-0000001-0",
            sector: "Construcción",
            experiencia: 10,
            presentacion: "Soy un plomero profesional especializado en instalaciones residenciales y comerciales",
            telefono1: "809-555-0003",
            whatsapp1: true,
            provincia: "Santo Domingo",
            nivelNacional: false);
        context.Contratistas.Add(contratista1);
        await context.SaveChangesAsync();

        // Crear plan contratista si no existe
        var planContratista = await context.PlanesContratistas.FirstOrDefaultAsync();
        if (planContratista == null)
        {
            planContratista = PlanContratista.Create(
                nombrePlan: "Plan Básico",
                precio: 300.00m);
            context.PlanesContratistas.Add(planContratista);
            await context.SaveChangesAsync();
        }

        // Suscripción activa para contratista1
        var suscripcion3 = Suscripcion.Create(
            userId: userId3,
            planId: planContratista.PlanId,
            duracionMeses: 1);
        context.Suscripciones.Add(suscripcion3);
        await context.SaveChangesAsync();

        contratistas.Add(contratista1);

        // ========================================
        // CONTRATISTA 2: Ana Martínez (Inactiva - para probar activación)
        // ========================================
        var userId4 = Guid.NewGuid().ToString();
        
        var perfil4 = Perfile.CrearPerfilContratista(
            userId: userId4,
            nombre: "Ana",
            apellido: "Martínez",
            email: "ana.martinez@test.com",
            telefono1: "809-555-0004");
        context.Perfiles.Add(perfil4);
        await context.SaveChangesAsync();

        var credencial4 = Credencial.Create(
            userId: userId4,
            email: Domain.ValueObjects.Email.Create("ana.martinez@test.com"),
            passwordHash: TestPasswordHash);
        // NO activar la cuenta (credencial4.Activo == false)
        context.Credenciales.Add(credencial4);
        await context.SaveChangesAsync();

        var contratista2 = Contratista.Create(
            userId: userId4,
            nombre: "Ana",
            apellido: "Martínez",
            tipo: 1,
            titulo: "Electricista",
            identificacion: "001-0000002-0",
            sector: "Servicios eléctricos",
            experiencia: 5,
            presentacion: "Electricista certificada con experiencia en instalaciones y reparaciones",
            telefono1: "809-555-0004",
            whatsapp1: true,
            provincia: "Santiago");
        context.Contratistas.Add(contratista2);
        await context.SaveChangesAsync();

        contratistas.Add(contratista2);

        return (empleadores, contratistas);
    }

    /// <summary>
    /// Obtiene el empleador Juan Pérez (activo con plan)
    /// </summary>
    public static async Task<Empleador> GetEmpleadorActivoAsync(IApplicationDbContext context)
    {
        var credencial = await context.Credenciales
            .FirstAsync(c => c.Email.Value == "juan.perez@test.com" && c.Activo);

        return await context.Empleadores
            .FirstAsync(e => e.UserId == credencial.UserId);
    }

    /// <summary>
    /// Obtiene el contratista Carlos Rodríguez (activo con plan)
    /// </summary>
    public static async Task<Contratista> GetContratistaActivoAsync(IApplicationDbContext context)
    {
        var credencial = await context.Credenciales
            .FirstAsync(c => c.Email.Value == "carlos.rodriguez@test.com" && c.Activo);

        return await context.Contratistas
            .FirstAsync(c => c.UserId == credencial.UserId);
    }
}
