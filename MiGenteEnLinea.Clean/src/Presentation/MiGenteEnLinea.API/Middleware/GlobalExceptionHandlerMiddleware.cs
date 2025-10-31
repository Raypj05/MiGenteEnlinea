using System.Net;
using System.Text.Json;
using FluentValidation;
using MiGenteEnLinea.Application.Common.Exceptions;
using AppValidationException = MiGenteEnLinea.Application.Common.Exceptions.ValidationException;

namespace MiGenteEnLinea.API.Middleware;

/// <summary>
/// Middleware global para manejo centralizado de excepciones.
/// Mapea excepciones personalizadas a códigos de estado HTTP apropiados.
/// </summary>
/// <remarks>
/// ARQUITECTURA: Clean Architecture Exception Handling
/// - NotFoundException → 404 Not Found
/// - ValidationException → 400 Bad Request
/// - BadRequestException → 400 Bad Request
/// - UnauthorizedAccessException → 401 Unauthorized
/// - ForbiddenAccessException → 403 Forbidden
/// - Otras excepciones → 500 Internal Server Error
/// 
/// BENEFITS:
/// - Código DRY (no repetir try-catch en cada controller)
/// - Respuestas consistentes en toda la API
/// - Logging centralizado de errores
/// - Oculta detalles sensibles en producción
/// </remarks>
public class GlobalExceptionHandlerMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionHandlerMiddleware> _logger;
    private readonly IWebHostEnvironment _env;

    public GlobalExceptionHandlerMiddleware(
        RequestDelegate next,
        ILogger<GlobalExceptionHandlerMiddleware> logger,
        IWebHostEnvironment env)
    {
        _next = next;
        _logger = logger;
        _env = env;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        // Log the exception with full details
        _logger.LogError(
            exception,
            "Unhandled exception occurred. Path: {Path}, Method: {Method}, User: {User}",
            context.Request.Path,
            context.Request.Method,
            context.User?.Identity?.Name ?? "Anonymous");

        // Map exception to HTTP status code
        var (statusCode, message, details) = MapException(exception);

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)statusCode;

        // Build response object
        var response = new ErrorResponse
        {
            StatusCode = (int)statusCode,
            Message = message,
            Details = _env.IsDevelopment() ? details : null, // Only show details in Development
            TraceId = context.TraceIdentifier,
            Path = context.Request.Path.ToString(),
            Timestamp = DateTime.UtcNow
        };

        var json = JsonSerializer.Serialize(response, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = _env.IsDevelopment()
        });

        await context.Response.WriteAsync(json);
    }

    /// <summary>
    /// Maps exceptions to HTTP status codes and messages.
    /// </summary>
    private (HttpStatusCode statusCode, string message, string? details) MapException(Exception exception)
    {
        return exception switch
        {
            // Domain/Application exceptions
            NotFoundException notFound => (
                HttpStatusCode.NotFound,
                notFound.Message,
                _env.IsDevelopment() ? notFound.StackTrace : null
            ),

            // FluentValidation exceptions (from ValidationBehavior)
            FluentValidation.ValidationException fluentValidation => (
                HttpStatusCode.BadRequest,
                "Ocurrieron uno o más errores de validación.",
                _env.IsDevelopment() 
                    ? string.Join("; ", fluentValidation.Errors.Select(e => $"{e.PropertyName}: {e.ErrorMessage}"))
                    : null
            ),

            // Application ValidationException
            AppValidationException validation => (
                HttpStatusCode.BadRequest,
                validation.Message,
                _env.IsDevelopment() ? validation.StackTrace : null
            ),

            BadRequestException badRequest => (
                HttpStatusCode.BadRequest,
                badRequest.Message,
                _env.IsDevelopment() ? badRequest.StackTrace : null
            ),

            // Security exceptions
            UnauthorizedAccessException unauthorized => (
                HttpStatusCode.Unauthorized,
                unauthorized.Message,
                _env.IsDevelopment() ? unauthorized.StackTrace : null
            ),

            ForbiddenAccessException forbidden => (
                HttpStatusCode.Forbidden,
                forbidden.Message,
                _env.IsDevelopment() ? forbidden.StackTrace : null
            ),

            // Catch-all for unexpected errors
            _ => (
                HttpStatusCode.InternalServerError,
                _env.IsDevelopment() 
                    ? exception.Message 
                    : "Ha ocurrido un error interno. Por favor, contacte al administrador.",
                _env.IsDevelopment() ? exception.StackTrace : null
            )
        };
    }
}

/// <summary>
/// Standard error response format for API.
/// </summary>
public class ErrorResponse
{
    public int StatusCode { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? Details { get; set; }
    public string TraceId { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
}
