using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using MiGenteEnLinea.Application.Common.Interfaces;
using MiGenteEnLinea.Infrastructure.Persistence.Contexts;
using MiGenteEnLinea.Infrastructure.Persistence.Interceptors;

namespace MiGenteEnLinea.IntegrationTests.Infrastructure;

/// <summary>
/// Factory para crear un servidor de prueba con SQL Server real y servicios mock.
/// CONFIGURACIÓN CRÍTICA:
/// - Usa SQL Server Docker (localhost,1433) con base de datos MiGenteTestDB
/// - AuditableEntityInterceptor para CreatedAt/UpdatedAt automáticos
/// - Mock de IEmailService para no enviar emails reales
/// - Mock de IPaymentService para simular pagos Cardnet
/// - Mock de IPadronService para simular consultas al padrón
/// - Base de datos persistente entre tests para validar relaciones FK reales
/// </summary>
public class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    public Mock<IEmailService> EmailServiceMock { get; private set; } = new();
    public Mock<IPaymentService> PaymentServiceMock { get; private set; } = new();
    public Mock<IPadronService> PadronServiceMock { get; private set; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Configurar environment como Testing
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((context, config) =>
        {
            // Cargar appsettings.Testing.json
            config.AddJsonFile("appsettings.Testing.json", optional: false, reloadOnChange: false);
        });

        builder.ConfigureServices((context, services) =>
        {
            // ========================================
            // PASO 1: Remover DbContext de producción
            // ========================================
            var dbContextDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<MiGenteDbContext>));
            
            if (dbContextDescriptor != null)
            {
                services.Remove(dbContextDescriptor);
            }

            // ========================================
            // PASO 2: Reemplazar ICurrentUserService con mock para tests
            // (El interceptor depende de este servicio)
            // ========================================
            services.RemoveAll<ICurrentUserService>();

            // Mock de ICurrentUserService - retorna "TestUser" para CreatedBy/UpdatedBy
            services.AddScoped<ICurrentUserService>(sp =>
            {
                var mockUserService = new Mock<ICurrentUserService>();
                mockUserService.Setup(x => x.UserId).Returns("TestUser");
                mockUserService.Setup(x => x.Email).Returns("test@integrationtest.com");
                mockUserService.Setup(x => x.IsAuthenticated).Returns(true);
                mockUserService.Setup(x => x.IsInRole(It.IsAny<string>())).Returns(true);
                return mockUserService.Object;
            });

            // ========================================
            // PASO 3: Reemplazar servicios externos con mocks
            // ========================================
            
            // Mock Email Service
            services.RemoveAll<IEmailService>();
            EmailServiceMock = new Mock<IEmailService>();
            EmailServiceMock
                .Setup(x => x.SendEmailAsync(
                    It.IsAny<string>(), 
                    It.IsAny<string>(), 
                    It.IsAny<string>(), 
                    It.IsAny<string>(), 
                    It.IsAny<string>()))
                .Returns(Task.CompletedTask);
            EmailServiceMock
                .Setup(x => x.SendActivationEmailAsync(
                    It.IsAny<string>(), 
                    It.IsAny<string>(), 
                    It.IsAny<string>()))
                .Returns(Task.CompletedTask);
            services.AddScoped(_ => EmailServiceMock.Object);

            // Mock Payment Service (Cardnet)
            services.RemoveAll<IPaymentService>();
            PaymentServiceMock = new Mock<IPaymentService>();
            PaymentServiceMock
                .Setup(x => x.GenerateIdempotencyKeyAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(Guid.NewGuid().ToString());
            PaymentServiceMock
                .Setup(x => x.ProcessPaymentAsync(It.IsAny<PaymentRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new PaymentResult
                {
                    Success = true,
                    ResponseCode = "00",
                    ResponseDescription = "Test payment approved",
                    ApprovalCode = "AUTH-" + DateTime.Now.Ticks.ToString().Substring(0, 6),
                    TransactionReference = "TEST-TXN-" + Guid.NewGuid().ToString().Substring(0, 8),
                    IdempotencyKey = Guid.NewGuid().ToString()
                });
            services.AddScoped(_ => PaymentServiceMock.Object);

            // Mock Padrón Service
            services.RemoveAll<IPadronService>();
            PadronServiceMock = new Mock<IPadronService>();
            PadronServiceMock
                .Setup(x => x.ConsultarCedulaAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new PadronModel
                {
                    Cedula = "00100000000",
                    Nombres = "JUAN ANTONIO",
                    Apellido1 = "PEREZ",
                    Apellido2 = "GOMEZ",
                    FechaNacimiento = new DateTime(1990, 1, 15),
                    EstadoCivil = "Soltero",
                    Ocupacion = "Ingeniero"
                });
            services.AddScoped(_ => PadronServiceMock.Object);

            // ========================================
            // PASO 4: Asegurar que AuditableEntityInterceptor está registrado
            // ========================================
            var interceptorDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(AuditableEntityInterceptor));
            
            if (interceptorDescriptor == null)
            {
                services.AddScoped<AuditableEntityInterceptor>();
            }

            // ========================================
            // PASO 5: Agregar DbContext con SQL Server real + AuditableEntityInterceptor
            // ⚠️ CRÍTICO: Debe incluir .AddInterceptors() igual que producción
            // ========================================
            var connectionString = context.Configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found in appsettings.Testing.json");

            services.AddDbContext<MiGenteDbContext>((serviceProvider, options) =>
            {
                options.UseSqlServer(connectionString, sqlOptions =>
                {
                    sqlOptions.EnableRetryOnFailure(
                        maxRetryCount: 3,
                        maxRetryDelay: TimeSpan.FromSeconds(5),
                        errorNumbersToAdd: null);
                });
                options.EnableSensitiveDataLogging();
                options.EnableDetailedErrors();
                
                // ⚠️ CRÍTICO: Agregar interceptor para auditoría automática
                var auditInterceptor = serviceProvider.GetRequiredService<AuditableEntityInterceptor>();
                options.AddInterceptors(auditInterceptor);
            });

            // ========================================
            // PASO 6: Asegurar que la base de datos está creada con migraciones
            // ========================================
            var sp = services.BuildServiceProvider();
            using var scope = sp.CreateScope();
            var scopedServices = scope.ServiceProvider;
            var db = scopedServices.GetRequiredService<MiGenteDbContext>();
            
            // Aplicar migraciones pendientes
            db.Database.Migrate();
        });
    }

    /// <summary>
    /// Resetea los mocks para el siguiente test
    /// </summary>
    public void ResetMocks()
    {
        EmailServiceMock.Reset();
        PaymentServiceMock.Reset();
        PadronServiceMock.Reset();
    }
}
