using System.Reflection;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using MiGenteEnLinea.Application.Common.Behaviors;
using MiGenteEnLinea.Application.Features.Dashboard.Services;

namespace MiGenteEnLinea.Application;

/// <summary>
/// Extensión para registrar todos los servicios de Application en el contenedor DI
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // ========================================
        // MEDIATR (CQRS Pattern)
        // ========================================
        services.AddMediatR(config =>
        {
            config.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly());

            // Validation behavior - ejecuta FluentValidation automáticamente
            config.AddOpenBehavior(typeof(ValidationBehavior<,>));
            
            // TODO: Agregar behaviors adicionales cuando se implementen
            // config.AddOpenBehavior(typeof(LoggingBehavior<,>));
            // config.AddOpenBehavior(typeof(PerformanceBehavior<,>));
        });

        // ========================================
        // FLUENT VALIDATION
        // ========================================
        services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());

        // ========================================
        // AUTOMAPPER (Object Mapping)
        // ========================================
        services.AddAutoMapper(Assembly.GetExecutingAssembly());

        // ========================================
        // MEMORY CACHE & DASHBOARD CACHING
        // ========================================
        services.AddMemoryCache();
        services.AddScoped<IDashboardCacheService, DashboardCacheService>();

        return services;
    }
}
