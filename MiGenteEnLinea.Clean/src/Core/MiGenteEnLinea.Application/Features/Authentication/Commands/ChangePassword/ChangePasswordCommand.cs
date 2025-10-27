using MediatR;
using MiGenteEnLinea.Application.Features.Authentication.DTOs;

namespace MiGenteEnLinea.Application.Features.Authentication.Commands.ChangePassword;

/// <summary>
/// Command para cambiar la contraseña de un usuario
/// </summary>
/// <remarks>
/// Migrado desde: SuscripcionesService.cs -> actualizarPass(Credenciales c)
/// Identity requiere validar la contraseña actual antes de cambiarla
/// </remarks>
public record ChangePasswordCommand(
    string Email,
    string UserId,
    string CurrentPassword,
    string NewPassword
) : IRequest<ChangePasswordResult>;
