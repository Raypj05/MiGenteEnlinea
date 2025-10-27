using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MiGenteEnLinea.Application.Common.Interfaces;
using MiGenteEnLinea.Domain.Interfaces;
using MiGenteEnLinea.Domain.Interfaces.Repositories;
using MiGenteEnLinea.Domain.Interfaces.Repositories.Authentication;
using MiGenteEnLinea.Domain.Interfaces.Repositories.Calificaciones;
using MiGenteEnLinea.Domain.Interfaces.Repositories.Contratistas;
using MiGenteEnLinea.Domain.Interfaces.Repositories.Empleadores;
using MiGenteEnLinea.Domain.Interfaces.Repositories.Empleados;
using MiGenteEnLinea.Domain.Interfaces.Repositories.Pagos;
using MiGenteEnLinea.Domain.Interfaces.Repositories.Suscripciones;
using MiGenteEnLinea.Infrastructure.Identity;
using MiGenteEnLinea.Infrastructure.Identity.Services;
using MiGenteEnLinea.Infrastructure.Options;
using MiGenteEnLinea.Infrastructure.Persistence.Contexts;
using MiGenteEnLinea.Infrastructure.Persistence.Interceptors;
using MiGenteEnLinea.Infrastructure.Persistence.Repositories;
using MiGenteEnLinea.Infrastructure.Persistence.Repositories.Authentication;
using MiGenteEnLinea.Infrastructure.Persistence.Repositories.Calificaciones;
using MiGenteEnLinea.Infrastructure.Persistence.Repositories.Contratistas;
using MiGenteEnLinea.Infrastructure.Persistence.Repositories.Empleadores;
using MiGenteEnLinea.Infrastructure.Persistence.Repositories.Empleados;
using MiGenteEnLinea.Infrastructure.Persistence.Repositories.Pagos;
using MiGenteEnLinea.Infrastructure.Persistence.Repositories.Suscripciones;
using MiGenteEnLinea.Infrastructure.Services;
using MiGenteEnLinea.Infrastructure.Services.Documents;
using Polly;
using Polly.Extensions.Http;
using Microsoft.AspNetCore.Identity;

namespace MiGenteEnLinea.Infrastructure;

/// <summary>
/// Extensión para registrar todos los servicios de Infrastructure en el contenedor DI
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // ========================================
        // DATABASE CONTEXT
        // ========================================
        services.AddDbContext<MiGenteDbContext>((serviceProvider, options) =>
        {
            var auditInterceptor = serviceProvider.GetRequiredService<AuditableEntityInterceptor>();

            options.UseSqlServer(
                configuration.GetConnectionString("DefaultConnection"),
                sqlOptions =>
                {
                    // Retry policy para conexiones intermitentes
                    sqlOptions.EnableRetryOnFailure(
                        maxRetryCount: 3,
                        maxRetryDelay: TimeSpan.FromSeconds(30),
                        errorNumbersToAdd: null);

                    // Timeout de comandos
                    sqlOptions.CommandTimeout(60);

                    // Assembly de migrations (para separar migrations en Infrastructure)
                    sqlOptions.MigrationsAssembly(typeof(MiGenteDbContext).Assembly.FullName);
                })
                .AddInterceptors(auditInterceptor)
                .EnableSensitiveDataLogging(false) // Solo en desarrollo
                .EnableDetailedErrors(false); // Solo en desarrollo
        });

        // Registrar interfaz para Application Layer
        services.AddScoped<IApplicationDbContext>(provider => provider.GetRequiredService<MiGenteDbContext>());

        // ========================================
        // IDENTITY SERVICES
        // ========================================
        
        // ASP.NET Core Identity Configuration
        services.AddIdentity<ApplicationUser, IdentityRole>(options =>
        {
            // Password settings (alineadas con Legacy para migración suave)
            options.Password.RequireDigit = false; // Legacy no requería dígitos
            options.Password.RequireLowercase = false;
            options.Password.RequireUppercase = false;
            options.Password.RequireNonAlphanumeric = false;
            options.Password.RequiredLength = 6; // Mínimo 6 caracteres (Legacy)
            
            // User settings
            options.User.RequireUniqueEmail = true;
            
            // Lockout settings (protección contra brute force)
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            options.Lockout.MaxFailedAccessAttempts = 5;
            options.Lockout.AllowedForNewUsers = true;
            
            // Sign-in settings
            options.SignIn.RequireConfirmedEmail = false; // Legacy: Activo flag en Credenciales
            options.SignIn.RequireConfirmedPhoneNumber = false;
        })
        .AddEntityFrameworkStores<MiGenteDbContext>()
        .AddDefaultTokenProviders(); // Para reset password, email confirmation, etc.

        // CRITICAL: Registrar UserManager<IdentityUser> alias para Application Layer
        // Application layer usa IdentityUser (base class) para no depender de Infrastructure
        // DI resolverá correctamente a ApplicationUser en runtime
        services.AddScoped<UserManager<IdentityUser>>(sp => 
        {
            var appUserManager = sp.GetRequiredService<UserManager<ApplicationUser>>();
            // Cast is safe because ApplicationUser inherits from IdentityUser
            return (UserManager<IdentityUser>)(object)appUserManager;
        });

        // JWT Settings Configuration
        services.Configure<JwtSettings>(configuration.GetSection(JwtSettings.SectionName));
        
        // JWT Token Service
        services.AddScoped<IJwtTokenService, JwtTokenService>();
        
        // Identity Service (abstracción de UserManager para Application layer)
        services.AddScoped<IIdentityService, IdentityService>(); // ✅ ASP.NET Core Identity
        
        // Current User Service (obtiene usuario autenticado desde HttpContext)
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        
        // BCrypt Password Hasher (para Legacy migration - Credenciales table)
        services.AddScoped<Application.Common.Interfaces.IPasswordHasher, BCryptPasswordHasher>();

        // ========================================
        // INTERCEPTORS
        // ========================================
        services.AddScoped<AuditableEntityInterceptor>();

        // ========================================
        // REPOSITORIES (Generic Repository Pattern) - LOTE 0 SIMPLIFIED
        // ========================================
        // LOTE 0 Foundation: Base Repository + Unit of Work
        services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        // Legacy Data Service (para tablas no migradas a DDD - LOTE 6.0.3)
        services.AddScoped<ILegacyDataService, LegacyDataService>();

        // LOTE 0: Only core Rich Domain Model repositories (6 total)
        // Authentication
        services.AddScoped<ICredencialRepository, CredencialRepository>();
        // Empleadores
        services.AddScoped<IEmpleadorRepository, EmpleadorRepository>();
        // Contratistas
        services.AddScoped<IContratistaRepository, ContratistaRepository>();
        // Empleados
        services.AddScoped<IEmpleadoRepository, EmpleadoRepository>();
        // Suscripciones
        services.AddScoped<ISuscripcionRepository, SuscripcionRepository>();
        // Calificaciones
        services.AddScoped<ICalificacionRepository, CalificacionRepository>();

        // TODO LOTES 1-8: Uncomment as they're added to IUnitOfWork
        // services.AddScoped<IReciboHeaderRepository, ReciboHeaderRepository>();
        // services.AddScoped<IReciboDetalleRepository, ReciboDetalleRepository>();
        // services.AddScoped<IConceptoNominaRepository, ConceptoNominaRepository>();
        // services.AddScoped<IDeduccionTssRepository, DeduccionTssRepository>();
        // services.AddScoped<IPlanEmpleadorRepository, PlanEmpleadorRepository>();
        // services.AddScoped<IPlanContratistaRepository, PlanContratistaRepository>();
        // services.AddScoped<IVentaRepository, VentaRepository>();
        // services.AddScoped<ITransaccionRepository, TransaccionRepository>();
        // services.AddScoped<IContratacionRepository, ContratacionRepository>();
        // services.AddScoped<IContratoServicioRepository, ContratoServicioRepository>();
        // services.AddScoped<IServicioOfertadoRepository, ServicioOfertadoRepository>();
        // services.AddScoped<IRolRepository, RolRepository>();
        // services.AddScoped<IPermisoRepository, PermisoRepository>();
        // services.AddScoped<INacionalidadRepository, NacionalidadRepository>();
        // services.AddScoped<IProvinciaRepository, ProvinciaRepository>();
        // services.AddScoped<IMunicipioRepository, MunicipioRepository>();
        // services.AddScoped<ISectorRepository, SectorRepository>();
        // services.AddScoped<ITipoServicioRepository, TipoServicioRepository>();
        // services.AddScoped<IEstadoCivilRepository, EstadoCivilRepository>();
        // services.AddScoped<INivelAcademicoRepository, NivelAcademicoRepository>();
        // services.AddScoped<ITipoCuentaRepository, TipoCuentaRepository>();
        // services.AddScoped<IBancoRepository, BancoRepository>();
        // services.AddScoped<ITipoIdentificacionRepository, TipoIdentificacionRepository>();
        // services.AddScoped<IConfiguracionSistemaRepository, ConfiguracionSistemaRepository>();
        // services.AddScoped<IParametroSistemaRepository, ParametroSistemaRepository>();

        // ========================================
        // EXTERNAL SERVICES
        // ========================================
        
        // HttpClient para Padrón Nacional con retry policy
        services.AddHttpClient("PadronAPI", (serviceProvider, client) =>
        {
            var config = serviceProvider.GetRequiredService<IConfiguration>();
            var baseUrl = config["PadronAPI:BaseUrl"];
            
            if (!string.IsNullOrEmpty(baseUrl))
            {
                client.BaseAddress = new Uri(baseUrl);
            }
            
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.Add("Accept", "application/json");
        })
        .AddPolicyHandler(GetRetryPolicy());

        // Memory Cache para Padrón (tokens + consultas)
        services.AddMemoryCache();

        // Padrón Service
        services.Configure<PadronSettings>(configuration.GetSection("PadronAPI"));
        services.AddScoped<IPadronService, PadronService>();

        // Nómina Calculator Service (Nota: Ya está registrado en Application layer DI)
        // services.AddScoped<INominaCalculatorService, NominaCalculatorService>();

        // ========================================
        // PAYMENT GATEWAY (CARDNET)
        // ========================================
        
        // Configuración de Cardnet
        services.Configure<CardnetSettings>(configuration.GetSection("Cardnet"));

        // HttpClient para Cardnet con retry policy y circuit breaker
        services.AddHttpClient("CardnetAPI", (serviceProvider, client) =>
        {
            var config = serviceProvider.GetRequiredService<IConfiguration>();
            var baseUrl = config["Cardnet:BaseUrl"];
            
            if (!string.IsNullOrEmpty(baseUrl))
            {
                client.BaseAddress = new Uri(baseUrl);
            }
            
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.Add("Accept", "application/json");
            client.DefaultRequestHeaders.Add("Content-Type", "application/json");
        })
        .AddPolicyHandler(GetRetryPolicy()) // Retry 3 veces
        .AddPolicyHandler(GetCircuitBreakerPolicy()); // Circuit breaker después de 5 fallos

        // =====================================================================
        // EMAIL SERVICE (GAP-021 - CRITICAL BLOCKER)
        // Servicio para envío de emails vía SMTP usando MailKit
        // =====================================================================
        services.Configure<EmailSettings>(configuration.GetSection("EmailSettings"));
        services.AddScoped<IEmailService, EmailService>();

        // =====================================================================
        // CURRENT USER SERVICE (LOTE 2)
        // Servicio para obtener información del usuario autenticado desde JWT
        // =====================================================================
        services.AddHttpContextAccessor();
        services.AddScoped<MiGenteEnLinea.Application.Common.Interfaces.ICurrentUserService, Identity.CurrentUserService>();

        // =====================================================================
        // PDF SERVICE (PLAN 5 - LOTE 5.3)
        // Generación de PDFs desde HTML (contratos, recibos, autorizaciones TSS)
        // =====================================================================
        services.AddScoped<IPdfService, PdfService>();

        // =====================================================================
        // IMAGE SERVICE (PLAN 5 - LOTE 5.3)
        // Procesamiento de imágenes (resize, compress, convert format)
        // =====================================================================
        services.AddScoped<IImageService, ImageService>();

        // =====================================================================
        // NUMBER TO WORDS CONVERTER SERVICE (GAP-020 - COMPLETADO)
        // Conversión de números a letras en español para PDFs legales
        // ✅ Migrado de Legacy NumeroEnLetras.cs (extension method → Service pattern)
        // ✅ Usado en: Contratos, Recibos, Autorizaciones TSS
        // =====================================================================
        services.AddScoped<INumeroEnLetrasService, NumeroEnLetrasService>();

        // Uso: decimal salario = 5250.50m; string texto = salario.ConvertirALetras();
        // =====================================================================

        // =====================================================================
        // MOCK SERVICES (TEMPORAL - API Startup Fix)
        // ⚠️ TODO: Reemplazar con implementaciones reales cuando estén disponibles
        // =====================================================================
        
        // ✅ LOTE 1 COMPLETADO: CardnetPaymentService implementado
        services.AddScoped<IPaymentService, CardnetPaymentService>();
        
        services.AddScoped<INominaCalculatorService, MockNominaCalculatorService>();

        // TODO: Agregar cuando se migren del legacy
        // services.AddScoped<IFileStorageService, FileStorageService>();

        return services;
    }

    /// <summary>
    /// Política de reintentos con backoff exponencial para llamadas HTTP.
    /// 3 intentos: 0s → 2s → 4s → 8s (máximo 14s total).
    /// </summary>
    private static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy()
    {
        return HttpPolicyExtensions
            .HandleTransientHttpError() // 5xx, 408, network failures
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                onRetry: (outcome, timespan, retryAttempt, context) =>
                {
                    // Log retry attempts (opcional)
                    Console.WriteLine($"[Retry {retryAttempt}] Reintenando después de {timespan.TotalSeconds}s...");
                });
    }

    /// <summary>
    /// Circuit Breaker policy para evitar saturar servicios externos con errores.
    /// Abre el circuito después de 5 fallos consecutivos y lo mantiene abierto por 30 segundos.
    /// </summary>
    private static IAsyncPolicy<HttpResponseMessage> GetCircuitBreakerPolicy()
    {
        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .CircuitBreakerAsync(
                handledEventsAllowedBeforeBreaking: 5,
                durationOfBreak: TimeSpan.FromSeconds(30),
                onBreak: (outcome, duration) =>
                {
                    Console.WriteLine($"[Circuit Breaker] Circuito abierto por {duration.TotalSeconds}s debido a múltiples fallos.");
                },
                onReset: () =>
                {
                    Console.WriteLine("[Circuit Breaker] Circuito cerrado, reanudando llamadas.");
                });
    }
}
