namespace MiGenteEnLinea.Application.Common.Exceptions;

/// <summary>
/// Excepción lanzada cuando un usuario intenta realizar una operación sin los permisos necesarios.
/// HTTP 403 Forbidden
/// </summary>
/// <remarks>
/// Diferencia con UnauthorizedException (401):
/// - 401 Unauthorized: No autenticado (no token JWT válido)
/// - 403 Forbidden: Autenticado pero sin permisos para la operación
/// 
/// Casos de uso:
/// - Usuario intenta editar perfil de otro usuario
/// - Usuario sin rol Admin intenta operación administrativa
/// - Usuario intenta acceder a recurso que no le pertenece
/// </remarks>
public class ForbiddenAccessException : Exception
{
    public ForbiddenAccessException()
        : base("No tiene permisos para realizar esta operación.")
    {
    }

    public ForbiddenAccessException(string message)
        : base(message)
    {
    }

    public ForbiddenAccessException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
