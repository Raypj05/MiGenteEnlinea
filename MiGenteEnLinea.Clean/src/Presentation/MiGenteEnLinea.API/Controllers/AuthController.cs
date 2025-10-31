using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MiGenteEnLinea.Application.Common.Exceptions;
using MiGenteEnLinea.Application.Features.Authentication.Commands.ActivateAccount;
using MiGenteEnLinea.Application.Features.Authentication.Commands.AddProfileInfo;
using MiGenteEnLinea.Application.Features.Authentication.Commands.ChangePassword;
using MiGenteEnLinea.Application.Features.Authentication.Commands.ChangePasswordById;
using MiGenteEnLinea.Application.Features.Authentication.Commands.DeleteUser;
using MiGenteEnLinea.Application.Features.Authentication.Commands.DeleteUserCredential;
using MiGenteEnLinea.Application.Features.Authentication.Commands.ForgotPassword;
using MiGenteEnLinea.Application.Features.Authentication.Commands.Login;
using MiGenteEnLinea.Application.Features.Authentication.Commands.RefreshToken;
using MiGenteEnLinea.Application.Features.Authentication.Commands.Register;
using MiGenteEnLinea.Application.Features.Authentication.Commands.ResendActivationEmail;
using MiGenteEnLinea.Application.Features.Authentication.Commands.ResetPassword;
using MiGenteEnLinea.Application.Features.Authentication.Commands.RevokeToken;
using MiGenteEnLinea.Application.Features.Authentication.Commands.UpdateCredencial;
using MiGenteEnLinea.Application.Features.Authentication.Commands.UpdateProfile;
using MiGenteEnLinea.Application.Features.Authentication.Commands.UpdateProfileExtended;
using MiGenteEnLinea.Application.Features.Authentication.DTOs;
using MiGenteEnLinea.Application.Features.Authentication.Queries.GetCredenciales;
using MiGenteEnLinea.Application.Features.Authentication.Queries.GetCuentaById;
using MiGenteEnLinea.Application.Features.Authentication.Queries.GetPerfil;
using MiGenteEnLinea.Application.Features.Authentication.Queries.GetPerfilByEmail;
using MiGenteEnLinea.Application.Features.Authentication.Queries.ValidarCorreo;
using MiGenteEnLinea.Application.Features.Authentication.Queries.ValidarCorreoCuentaActual;
using MiGenteEnLinea.Application.Features.Authentication.Queries.ValidateEmailBelongsToUser;

namespace MiGenteEnLinea.API.Controllers;

/// <summary>
/// Controller para autenticación y gestión de usuarios
/// </summary>
/// <remarks>
/// Migrado desde: LoginService.asmx.cs y SuscripcionesService.cs
/// Implementa LOTE 1: AUTHENTICATION & USER MANAGEMENT
/// </remarks>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class AuthController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<AuthController> _logger;

    public AuthController(IMediator mediator, ILogger<AuthController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    /// <summary>
    /// Autenticar usuario con email y contraseña (JWT)
    /// </summary>
    /// <param name="command">Credenciales de login</param>
    /// <returns>Tokens JWT y datos del usuario</returns>
    /// <response code="200">Login exitoso - Retorna access token, refresh token y datos del usuario</response>
    /// <response code="401">Credenciales inválidas o cuenta inactiva</response>
    /// <response code="400">Datos de entrada inválidos</response>
    /// <remarks>
    /// Sample request:
    /// 
    ///     POST /api/auth/login
    ///     {
    ///        "email": "usuario@example.com",
    ///        "password": "MiPassword123",
    ///        "ipAddress": "192.168.1.100"
    ///     }
    /// 
    /// Sample response:
    /// 
    ///     {
    ///        "accessToken": "eyJhbGciOiJIUzI1NiIs...",
    ///        "refreshToken": "a1b2c3d4e5f6...",
    ///        "accessTokenExpires": "2025-01-15T12:30:00Z",
    ///        "refreshTokenExpires": "2025-01-22T11:15:00Z",
    ///        "user": {
    ///            "userId": "550e8400-e29b-41d4-a716-446655440000",
    ///            "email": "usuario@example.com",
    ///            "nombreCompleto": "Juan Pérez",
    ///            "tipo": "1",
    ///            "planId": 2,
    ///            "vencimientoPlan": "2025-12-31T00:00:00Z",
    ///            "roles": ["Empleador"]
    ///        }
    ///     }
    /// 
    /// IMPORTANTE:
    /// - El ipAddress se obtiene automáticamente del HttpContext si no se provee
    /// - El access token expira en 15 minutos
    /// - El refresh token expira en 7 días
    /// - Guardar el refresh token de forma segura para renovar el access token
    /// </remarks>
    [HttpPost("login")]
    [ProducesResponseType(typeof(AuthenticationResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AuthenticationResultDto>> Login([FromBody] LoginCommand command)
    {
        _logger.LogInformation("POST /api/auth/login - Email: {Email}", command.Email);

        try
        {
            // Obtener IP del cliente si no se provee
            var ipAddress = string.IsNullOrEmpty(command.IpAddress)
                ? HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown"
                : command.IpAddress;

            // Crear comando con IP
            var loginCommand = command with { IpAddress = ipAddress };

            var result = await _mediator.Send(loginCommand);

            _logger.LogInformation("Login exitoso - UserId: {UserId}", result.User.UserId);
            return Ok(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning("Login fallido: {Message}", ex.Message);
            return Unauthorized(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Obtener el perfil completo de un usuario
    /// </summary>
    /// <param name="userId">ID del usuario (GUID)</param>
    /// <returns>Datos del perfil del usuario</returns>
    /// <response code="200">Perfil encontrado</response>
    /// <response code="404">Usuario no encontrado</response>
    [HttpGet("perfil/{userId}")]
    [ProducesResponseType(typeof(PerfilDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PerfilDto>> GetPerfil(string userId)
    {
        _logger.LogInformation("GET /api/auth/perfil/{UserId}", userId);

        var result = await _mediator.Send(new GetPerfilQuery(userId));

        if (result == null)
        {
            _logger.LogWarning("Perfil no encontrado para userId: {UserId}", userId);
            return NotFound(new { message = "Perfil no encontrado" });
        }

        return Ok(result);
    }

    /// <summary>
    /// Obtener perfil por email
    /// </summary>
    /// <param name="email">Email del usuario</param>
    /// <returns>Datos del perfil</returns>
    /// <response code="200">Perfil encontrado</response>
    /// <response code="404">Usuario no encontrado</response>
    [HttpGet("perfil/email/{email}")]
    [ProducesResponseType(typeof(PerfilDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PerfilDto>> GetPerfilByEmail(string email)
    {
        _logger.LogInformation("GET /api/auth/perfil/email/{Email}", email);

        var result = await _mediator.Send(new GetPerfilByEmailQuery(email));

        if (result == null)
        {
            _logger.LogWarning("Perfil no encontrado para email: {Email}", email);
            return NotFound(new { message = "Perfil no encontrado" });
        }

        return Ok(result);
    }

    /// <summary>
    /// Validar si un email ya existe en el sistema
    /// </summary>
    /// <param name="email">Email a validar</param>
    /// <returns>True si el email ya existe (NO disponible), false si está disponible</returns>
    /// <response code="200">Validación completada</response>
    /// <remarks>
    /// Retorna:
    /// - true: Email ya existe (NO disponible para registro)
    /// - false: Email disponible para registro
    /// </remarks>
    [HttpGet("validar-email/{email}")]
    [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
    public async Task<ActionResult<bool>> ValidarCorreo(string email)
    {
        _logger.LogInformation("GET /api/auth/validar-email/{Email}", email);

        var existe = await _mediator.Send(new ValidarCorreoQuery(email));

        return Ok(new { email, existe, disponible = !existe });
    }

    /// <summary>
    /// Obtener todas las credenciales de un usuario
    /// </summary>
    /// <param name="userId">ID del usuario (GUID)</param>
    /// <returns>Lista de credenciales del usuario</returns>
    /// <response code="200">Credenciales encontradas</response>
    [HttpGet("credenciales/{userId}")]
    [ProducesResponseType(typeof(List<CredencialDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<CredencialDto>>> GetCredenciales(string userId)
    {
        _logger.LogInformation("GET /api/auth/credenciales/{UserId}", userId);

        var result = await _mediator.Send(new GetCredencialesQuery(userId));

        return Ok(result);
    }

    /// <summary>
    /// Cambiar la contraseña de un usuario
    /// </summary>
    /// <param name="command">Datos para cambio de contraseña</param>
    /// <returns>Resultado de la operación</returns>
    /// <response code="200">Contraseña actualizada exitosamente</response>
    /// <response code="400">Datos inválidos</response>
    /// <response code="404">Usuario no encontrado</response>
    /// <remarks>
    /// Sample request:
    /// 
    ///     POST /api/auth/change-password
    ///     {
    ///        "email": "usuario@example.com",
    ///        "userId": "550e8400-e29b-41d4-a716-446655440000",
    ///        "newPassword": "NuevaPassword123"
    ///     }
    /// 
    /// </remarks>
    [HttpPost("change-password")]
    [Authorize]
    [ProducesResponseType(typeof(ChangePasswordResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ChangePasswordResult>> ChangePassword([FromBody] ChangePasswordCommand command)
    {
        _logger.LogInformation("POST /api/auth/change-password - Email: {Email}", command.Email);

        var result = await _mediator.Send(command);

        if (!result.Success)
        {
            _logger.LogWarning("Cambio de contraseña fallido: {Message}", result.Message);
            return NotFound(result);
        }

        _logger.LogInformation("Contraseña actualizada exitosamente para: {Email}", command.Email);
        return Ok(result);
    }

    /// <summary>
    /// Cambiar contraseña por ID de credencial (GAP-014)
    /// </summary>
    /// <param name="credencialId">ID de la credencial</param>
    /// <param name="command">Nueva contraseña</param>
    /// <returns>Resultado de la operación</returns>
    /// <response code="200">Contraseña actualizada exitosamente</response>
    /// <response code="400">Datos inválidos</response>
    /// <response code="404">Credencial no encontrada</response>
    /// <remarks>
    /// Réplica de SuscripcionesService.actualizarPassByID() del Legacy
    /// GAP-014: Cambia password usando credential ID (no userID ni email)
    /// 
    /// DIFERENCIA CON /api/auth/change-password:
    /// - /api/auth/change-password: Usa email + userId para identificar
    /// - /api/auth/credenciales/{id}/password: Usa ID de credencial directamente
    /// 
    /// USO TÍPICO:
    /// - Admin cambiando password de usuario
    /// - Reset password desde panel de administración
    /// 
    /// Sample request:
    /// 
    ///     PUT /api/auth/credenciales/123/password
    ///     {
    ///        "credencialId": 123,
    ///        "newPassword": "NuevaPassword123"
    ///     }
    /// 
    /// </remarks>
    [HttpPut("credenciales/{credencialId}/password")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> ChangePasswordById(
        int credencialId,
        [FromBody] ChangePasswordByIdCommand command)
    {
        // Validar que el ID de la ruta coincida con el del comando
        if (credencialId != command.CredencialId)
        {
            return BadRequest(new
            {
                message = "El ID de credencial en la ruta no coincide con el del comando"
            });
        }

        _logger.LogInformation(
            "PUT /api/auth/credenciales/{CredencialId}/password",
            credencialId);

        try
        {
            var success = await _mediator.Send(command);

            if (!success)
            {
                _logger.LogWarning(
                    "Cambio de contraseña por ID fallido - Credencial no encontrada: {CredencialId}",
                    credencialId);
                return NotFound(new
                {
                    message = $"No se encontró la credencial con ID {credencialId}"
                });
            }

            _logger.LogInformation(
                "Contraseña actualizada exitosamente por ID - CredencialId: {CredencialId}",
                credencialId);
            return Ok(new
            {
                message = "Contraseña actualizada exitosamente",
                credencialId
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error al cambiar contraseña por ID - CredencialId: {CredencialId}",
                credencialId);
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Actualizar credencial completa (password + email + activo) - GAP-012
    /// </summary>
    /// <param name="command">Datos de credencial a actualizar</param>
    /// <returns>Resultado de la operación</returns>
    /// <response code="200">Credencial actualizada exitosamente</response>
    /// <response code="400">Datos inválidos o email ya existe</response>
    /// <response code="404">Credencial no encontrada</response>
    /// <remarks>
    /// Réplica de SuscripcionesService.actualizarCredenciales() del Legacy
    /// GAP-012: Permite actualizar password, email y estado activo en una sola operación
    /// 
    /// IMPORTANTE:
    /// - Password es opcional (si se omite, no se actualiza)
    /// - Password se hashea automáticamente con BCrypt
    /// - Valida que el nuevo email no exista en otra credencial
    /// 
    /// Sample request:
    /// 
    ///     PUT /api/auth/credenciales
    ///     {
    ///        "userId": "550e8400-e29b-41d4-a716-446655440000",
    ///        "email": "nuevoemail@example.com",
    ///        "password": "NuevaPassword123",
    ///        "activo": true
    ///     }
    /// 
    /// Sample request (sin cambiar password):
    /// 
    ///     PUT /api/auth/credenciales
    ///     {
    ///        "userId": "550e8400-e29b-41d4-a716-446655440000",
    ///        "email": "nuevoemail@example.com",
    ///        "activo": false
    ///     }
    /// 
    /// </remarks>
    [HttpPut("credenciales")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> UpdateCredencial([FromBody] UpdateCredencialCommand command)
    {
        _logger.LogInformation(
            "PUT /api/auth/credenciales - UserId: {UserId}, Email: {Email}, Activo: {Activo}",
            command.UserId,
            command.Email,
            command.Activo);

        try
        {
            var success = await _mediator.Send(command);

            if (!success)
            {
                _logger.LogWarning(
                    "Actualización de credencial fallida - Usuario no encontrado o email duplicado. UserId: {UserId}",
                    command.UserId);
                return NotFound(new
                {
                    message = "No se pudo actualizar la credencial. El usuario no existe o el email ya está registrado."
                });
            }

            _logger.LogInformation(
                "Credencial actualizada exitosamente - UserId: {UserId}",
                command.UserId);
            return Ok(new
            {
                message = "Credencial actualizada exitosamente.",
                userId = command.UserId,
                email = command.Email,
                activo = command.Activo.ToString().ToLower()
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error al actualizar credencial - UserId: {UserId}",
                command.UserId);
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Registrar nuevo usuario en el sistema
    /// </summary>
    /// <param name="command">Datos de registro</param>
    /// <returns>ID del perfil creado</returns>
    /// <response code="201">Usuario registrado exitosamente</response>
    /// <response code="400">Datos inválidos o email ya existe</response>
    /// <response code="500">Error al procesar el registro</response>
    /// <remarks>
    /// Réplica de SuscripcionesService.GuardarPerfil() del Legacy
    /// 
    /// Crea:
    /// - Perfile (Empleador o Contratista según tipo)
    /// - Credencial con contraseña encriptada (BCrypt)
    /// - Contratista (solo si tipo=2)
    /// - Envía email de activación
    /// 
    /// Sample request:
    /// 
    ///     POST /api/auth/register
    ///     {
    ///        "email": "nuevo@example.com",
    ///        "password": "Password123",
    ///        "nombre": "Juan",
    ///        "apellido": "Pérez",
    ///        "tipo": 1,
    ///        "telefono1": "809-555-1234",
    ///        "telefono2": null,
    ///        "usuario": "juanp"
    ///     }
    /// 
    /// Valores de tipo:
    /// - 1 = Empleador
    /// - 2 = Contratista
    /// </remarks>
    [HttpPost("register")]
    [ProducesResponseType(typeof(RegisterResult), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<RegisterResult>> Register([FromBody] RegisterCommand command)
    {
        _logger.LogInformation("POST /api/auth/register - Email: {Email}, Tipo: {Tipo}", command.Email, command.Tipo);

        try
        {
            var result = await _mediator.Send(command);

            // ✅ Verificar si el registro fue exitoso ANTES de CreatedAtAction
            if (!result.Success)
            {
                _logger.LogWarning("Registro fallido: {Message}", result.Message);
                return BadRequest(new { message = result.Message });
            }

            _logger.LogInformation("Usuario registrado exitosamente - IdentityUserId: {IdentityUserId}, CredentialId: {CredentialId}", 
                result.IdentityUserId, result.CredentialId);

            return CreatedAtAction(
                nameof(GetPerfil),
                new { userId = result.IdentityUserId }, // ✅ IdentityUserId (GUID) para routing correcto
                result);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("Registro fallido: {Message}", ex.Message);
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Activar cuenta de usuario
    /// </summary>
    /// <param name="command">Datos de activación (UserId y Email)</param>
    /// <returns>Resultado de la activación</returns>
    /// <response code="200">Cuenta activada exitosamente</response>
    /// <response code="400">Datos inválidos o cuenta ya activa</response>
    /// <response code="404">Usuario no encontrado</response>
    /// <remarks>
    /// Réplica de Activar.aspx.cs del Legacy
    /// 
    /// Sample request:
    /// 
    ///     POST /api/auth/activate
    ///     {
    ///        "userId": "550e8400-e29b-41d4-a716-446655440000",
    ///        "email": "usuario@example.com"
    ///     }
    /// 
    /// </remarks>
    [HttpPost("activate")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> ActivateAccount([FromBody] ActivateAccountCommand command)
    {
        _logger.LogInformation("POST /api/auth/activate - UserId: {UserId}, Email: {Email}", command.UserId, command.Email);

        try
        {
            var success = await _mediator.Send(command);

            if (!success)
            {
                _logger.LogWarning("Activación fallida - Usuario no encontrado o ya activo: {UserId}", command.UserId);
                return BadRequest(new { message = "No se pudo activar la cuenta. La cuenta ya está activa o los datos son incorrectos." });
            }

            _logger.LogInformation("Cuenta activada exitosamente - UserId: {UserId}", command.UserId);
            return Ok(new { message = "Cuenta activada exitosamente. Ya puede iniciar sesión." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al activar cuenta - UserId: {UserId}", command.UserId);
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Reenviar email de activación de cuenta (GAP-011)
    /// </summary>
    /// <param name="command">Datos para reenvío (UserId o Email)</param>
    /// <returns>Confirmación de envío</returns>
    /// <response code="200">Email reenviado exitosamente</response>
    /// <response code="400">Datos inválidos</response>
    /// <response code="404">Usuario no encontrado o ya activo</response>
    /// <remarks>
    /// Réplica de SuscripcionesService.enviarCorreoActivacion() y Registrar.aspx.cs EnviarCorreoActivacion()
    /// GAP-011: Implementación completa con soporte para userID o email
    /// 
    /// Sample request (con userId):
    /// 
    ///     POST /api/auth/resend-activation
    ///     {
    ///        "userId": "550e8400-e29b-41d4-a716-446655440000",
    ///        "email": "usuario@example.com",
    ///        "host": "https://migente.com"
    ///     }
    /// 
    /// Sample request (solo email):
    /// 
    ///     POST /api/auth/resend-activation
    ///     {
    ///        "email": "usuario@example.com",
    ///        "host": "https://migente.com"
    ///     }
    /// 
    /// </remarks>
    [HttpPost("resend-activation")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> ResendActivationEmail([FromBody] ResendActivationEmailCommand command)
    {
        _logger.LogInformation(
            "POST /api/auth/resend-activation - UserId: {UserId}, Email: {Email}",
            command.UserId ?? "NULL",
            command.Email);

        try
        {
            var success = await _mediator.Send(command);

            if (!success)
            {
                _logger.LogWarning(
                    "Reenvío de activación fallido - Usuario no encontrado o ya activo. UserId: {UserId}, Email: {Email}",
                    command.UserId ?? "NULL",
                    command.Email);
                return NotFound(new
                {
                    message = "No se pudo reenviar el email. El usuario no existe o la cuenta ya está activa."
                });
            }

            _logger.LogInformation(
                "Email de activación reenviado exitosamente - Email: {Email}",
                command.Email);
            return Ok(new
            {
                message = "Email de activación reenviado exitosamente. Por favor revisa tu correo."
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error al reenviar email de activación - Email: {Email}",
                command.Email);
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Actualizar perfil completo de usuario (Perfile + PerfilesInfo)
    /// </summary>
    /// <param name="userId">ID del usuario (GUID)</param>
    /// <param name="command">Datos a actualizar (Perfile + PerfilesInfo opcionales)</param>
    /// <returns>Resultado de la actualización</returns>
    /// <response code="200">Perfil actualizado exitosamente</response>
    /// <response code="400">Datos inválidos</response>
    /// <response code="404">Usuario no encontrado</response>
    /// <remarks>
    /// Migrado desde: LoginService.actualizarPerfil(perfilesInfo info, Cuentas cuenta) (línea 136)
    /// 
    /// LEGACY BEHAVIOR:
    /// - Actualiza 2 entidades con 2 DbContexts separados
    /// - db.Entry(info).State = Modified (perfilesInfo)
    /// - db1.Entry(cuenta).State = Modified (Cuentas)
    /// 
    /// CLEAN ARCHITECTURE:
    /// - Actualiza Perfile (antes Cuentas) + PerfilesInfo en 1 transacción
    /// - Usa domain methods para garantizar invariantes
    /// - PerfilesInfo es opcional (solo se actualiza si se proveen datos)
    /// 
    /// USO:
    /// - Actualizar información básica del perfil (nombre, email, teléfonos)
    /// - Actualizar información adicional (identificación, dirección, presentación)
    /// - Actualizar foto de perfil
    /// - Actualizar información del gerente (empresas)
    /// 
    /// Sample request (solo info básica):
    /// 
    ///     PUT /api/auth/perfil-completo/550e8400-e29b-41d4-a716-446655440000
    ///     {
    ///        "userId": "550e8400-e29b-41d4-a716-446655440000",
    ///        "nombre": "Juan Carlos",
    ///        "apellido": "Pérez González",
    ///        "email": "juan.perez@example.com",
    ///        "telefono1": "809-555-1234",
    ///        "telefono2": "809-555-5678",
    ///        "usuario": "juancp"
    ///     }
    /// 
    /// Sample request (con info adicional):
    /// 
    ///     PUT /api/auth/perfil-completo/550e8400-e29b-41d4-a716-446655440000
    ///     {
    ///        "userId": "550e8400-e29b-41d4-a716-446655440000",
    ///        "nombre": "Juan Carlos",
    ///        "apellido": "Pérez González",
    ///        "email": "juan.perez@example.com",
    ///        "telefono1": "809-555-1234",
    ///        "identificacion": "00112345678",
    ///        "tipoIdentificacion": 1,
    ///        "direccion": "Calle Principal #123",
    ///        "presentacion": "Profesional con experiencia...",
    ///        "nombreComercial": "Mi Empresa SRL",
    ///        "cedulaGerente": "00198765432",
    ///        "nombreGerente": "María",
    ///        "apellidoGerente": "García"
    ///     }
    /// 
    /// </remarks>
    [HttpPut("perfil-completo/{userId}")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> UpdateProfileExtended(string userId, [FromBody] UpdateProfileExtendedCommand command)
    {
        if (userId != command.UserId)
        {
            return BadRequest(new { message = "El UserId del path no coincide con el del body" });
        }

        _logger.LogInformation("PUT /api/auth/perfil-completo/{UserId} - Email: {Email}", userId, command.Email);

        try
        {
            var success = await _mediator.Send(command);

            if (!success)
            {
                _logger.LogWarning("Actualización de perfil extendido fallida - Usuario no encontrado: {UserId}", userId);
                return NotFound(new { message = "Usuario no encontrado" });
            }

            _logger.LogInformation("Perfil extendido actualizado exitosamente - UserId: {UserId}", userId);
            return Ok(new { message = "Perfil actualizado exitosamente" });
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning("Error de validación al actualizar perfil extendido: {Message}", ex.Message);
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Actualizar perfil básico de usuario (solo Perfile)
    /// </summary>
    /// <param name="userId">ID del usuario (GUID)</param>
    /// <param name="command">Datos a actualizar</param>
    /// <returns>Resultado de la actualización</returns>
    /// <response code="200">Perfil actualizado exitosamente</response>
    /// <response code="400">Datos inválidos</response>
    /// <response code="404">Usuario no encontrado</response>
    /// <remarks>
    /// Versión simplificada que solo actualiza información básica (Perfile)
    /// Para actualizar también PerfilesInfo, usar PUT /api/auth/perfil-completo/{userId}
    /// 
    /// Sample request:
    /// 
    ///     PUT /api/auth/perfil/550e8400-e29b-41d4-a716-446655440000
    ///     {
    ///        "userId": "550e8400-e29b-41d4-a716-446655440000",
    ///        "nombre": "Juan Carlos",
    ///        "apellido": "Pérez González",
    ///        "email": "juan.perez@example.com",
    ///        "telefono1": "809-555-1234",
    ///        "telefono2": "809-555-5678",
    ///        "usuario": "juancp"
    ///     }
    /// 
    /// </remarks>
    [HttpPut("perfil/{userId}")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> UpdateProfile(string userId, [FromBody] UpdateProfileCommand command)
    {
        if (userId != command.UserID)
        {
            return BadRequest(new { message = "El UserId del path no coincide con el del body" });
        }

        _logger.LogInformation("PUT /api/auth/perfil/{UserId} - Email: {Email}", userId, command.Email);

        try
        {
            var success = await _mediator.Send(command);

            if (!success)
            {
                _logger.LogWarning("Actualización de perfil fallida - Usuario no encontrado: {UserId}", userId);
                return NotFound(new { message = "Usuario no encontrado" });
            }

            _logger.LogInformation("Perfil actualizado exitosamente - UserId: {UserId}", userId);
            return Ok(new { message = "Perfil actualizado exitosamente" });
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning("Error de validación al actualizar perfil: {Message}", ex.Message);
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Renovar access token usando refresh token (Token Refresh)
    /// </summary>
    /// <param name="command">Refresh token actual</param>
    /// <returns>Nuevos tokens (access token + refresh token)</returns>
    /// <response code="200">Tokens renovados exitosamente</response>
    /// <response code="401">Refresh token inválido, expirado o revocado</response>
    /// <response code="400">Datos inválidos</response>
    /// <remarks>
    /// IMPORTANTE: Token Rotation (seguridad)
    /// - El refresh token viejo se revoca automáticamente
    /// - Se retorna un nuevo refresh token
    /// - Cada refresh token solo puede usarse UNA VEZ
    /// 
    /// USO:
    /// - Cuando el access token expira (15 minutos)
    /// - NO se requieren credenciales nuevamente
    /// - Experiencia de usuario fluida
    /// 
    /// Sample request:
    /// 
    ///     POST /api/auth/refresh
    ///     {
    ///        "refreshToken": "a1b2c3d4e5f6g7h8...",
    ///        "ipAddress": "192.168.1.100"
    ///     }
    /// 
    /// Sample response (mismo formato que Login):
    /// 
    ///     {
    ///        "accessToken": "eyJhbGciOiJIUzI1NiIs... (NUEVO)",
    ///        "refreshToken": "x9y8z7w6v5u4... (NUEVO)",
    ///        "accessTokenExpires": "2025-01-15T13:00:00Z",
    ///        "refreshTokenExpires": "2025-01-22T12:45:00Z",
    ///        "user": { ... }
    ///     }
    /// 
    /// </remarks>
    [HttpPost("refresh")]
    [ProducesResponseType(typeof(AuthenticationResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AuthenticationResultDto>> RefreshToken([FromBody] RefreshTokenCommand command)
    {
        _logger.LogInformation("POST /api/auth/refresh - IP: {IpAddress}", command.IpAddress);

        try
        {
            // Obtener IP del cliente si no se provee
            var ipAddress = string.IsNullOrEmpty(command.IpAddress)
                ? HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown"
                : command.IpAddress;

            // Crear comando con IP
            var refreshCommand = command with { IpAddress = ipAddress };

            var result = await _mediator.Send(refreshCommand);

            _logger.LogInformation("Refresh token exitoso - UserId: {UserId}", result.User.UserId);
            return Ok(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning("Refresh token fallido: {Message}", ex.Message);
            return Unauthorized(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Revocar refresh token (Logout)
    /// </summary>
    /// <param name="command">Refresh token a revocar</param>
    /// <returns>Resultado de la operación</returns>
    /// <response code="204">Token revocado exitosamente</response>
    /// <response code="401">Refresh token inválido</response>
    /// <response code="400">Datos inválidos</response>
    /// <remarks>
    /// USO:
    /// - Logout de usuario (invalida el refresh token)
    /// - Cambio de contraseña (revocar todos los tokens)
    /// - Revocación por admin (seguridad)
    /// 
    /// IMPORTANTE:
    /// - El refresh token revocado NO puede volver a usarse
    /// - El access token actual sigue válido hasta que expire (max 15 min)
    /// - Para logout inmediato, el cliente debe descartar el access token
    /// - La operación es idempotente (revocar token ya revocado no falla)
    /// 
    /// Sample request:
    /// 
    ///     POST /api/auth/revoke
    ///     {
    ///        "refreshToken": "a1b2c3d4e5f6g7h8...",
    ///        "ipAddress": "192.168.1.100",
    ///        "reason": "User logout"
    ///     }
    /// 
    /// </remarks>
    [HttpPost("revoke")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> RevokeToken([FromBody] RevokeTokenCommand command)
    {
        _logger.LogInformation("POST /api/auth/revoke - IP: {IpAddress}", command.IpAddress);

        try
        {
            // Obtener IP del cliente si no se provee
            var ipAddress = string.IsNullOrEmpty(command.IpAddress)
                ? HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown"
                : command.IpAddress;

            // Crear comando con IP
            var revokeCommand = command with { IpAddress = ipAddress };

            await _mediator.Send(revokeCommand);

            _logger.LogInformation("Refresh token revocado exitosamente");
            return NoContent();
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning("Revoke token fallido: {Message}", ex.Message);
            return Unauthorized(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Eliminar credencial específica de un usuario
    /// </summary>
    /// <param name="userId">ID del usuario (GUID)</param>
    /// <param name="credentialId">ID de la credencial a eliminar</param>
    /// <returns>204 No Content si se eliminó exitosamente</returns>
    /// <response code="204">Credencial eliminada exitosamente</response>
    /// <response code="400">Validación falló (ej: última credencial activa, userId inválido)</response>
    /// <response code="404">Credencial no encontrada o no pertenece al usuario</response>
    /// <response code="401">No autorizado</response>
    /// <remarks>
    /// Migrado desde: LoginService.borrarUsuario(string userID, int credencialID)
    /// 
    /// USO:
    /// - Usuario elimina email alternativo
    /// - Admin elimina credencial comprometida
    /// - Limpieza de credenciales duplicadas
    /// 
    /// IMPORTANTE:
    /// - No se puede eliminar la ÚLTIMA credencial activa del usuario
    /// - Usuario necesita al menos 1 credencial activa para mantener acceso
    /// - Se puede eliminar credencial inactiva sin restricciones
    /// - Solo el propio usuario o admin pueden eliminar credenciales
    /// 
    /// MEJORA sobre Legacy:
    /// - Legacy no validaba última credencial (podía dejar usuario sin acceso)
    /// - Clean Architecture previene este error con validación explícita
    /// 
    /// Sample request:
    /// 
    ///     DELETE /api/auth/users/550e8400-e29b-41d4-a716-446655440000/credentials/5
    /// 
    /// </remarks>
    [HttpDelete("users/{userId}/credentials/{credentialId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> DeleteUserCredential(
        string userId,
        int credentialId)
    {
        _logger.LogInformation(
            "DELETE /api/auth/users/{UserId}/credentials/{CredentialId}",
            userId,
            credentialId);

        try
        {
            var command = new DeleteUserCredentialCommand(userId, credentialId);
            await _mediator.Send(command);

            _logger.LogInformation(
                "Credencial {CredentialId} eliminada exitosamente para usuario {UserId}",
                credentialId,
                userId);

            return NoContent();
        }
        catch (NotFoundException ex)
        {
            _logger.LogWarning("Credencial no encontrada: {Message}", ex.Message);
            return NotFound(new { message = ex.Message });
        }
        catch (ValidationException ex)
        {
            _logger.LogWarning("Validación falló: {Message}", ex.Message);
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Obtener perfil (Cuenta) por su ID
    /// </summary>
    /// <param name="cuentaId">ID de la cuenta (PerfilId)</param>
    /// <returns>Datos del perfil</returns>
    /// <response code="200">Perfil encontrado</response>
    /// <response code="404">Perfil no encontrado</response>
    /// <remarks>
    /// Migrado desde: LoginService.getPerfilByID(int cuentaID) (línea 179)
    /// 
    /// USO:
    /// - Obtener datos de un perfil específico por su ID numérico
    /// - Equivalente a GetPerfil pero usando cuentaID en vez de userId (GUID)
    /// 
    /// Sample request:
    /// 
    ///     GET /api/auth/cuenta/5
    /// 
    /// </remarks>
    [HttpGet("cuenta/{cuentaId}")]
    [ProducesResponseType(typeof(PerfilDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PerfilDto>> GetCuentaById(int cuentaId)
    {
        _logger.LogInformation("GET /api/auth/cuenta/{CuentaId}", cuentaId);

        var result = await _mediator.Send(new GetCuentaByIdQuery(cuentaId));

        if (result == null)
        {
            _logger.LogWarning("Perfil no encontrado para cuentaId: {CuentaId}", cuentaId);
            return NotFound(new { message = "Perfil no encontrado" });
        }

        return Ok(result);
    }

    /// <summary>
    /// Agregar información adicional del perfil (perfilesInfo)
    /// </summary>
    /// <param name="command">Datos adicionales del perfil</param>
    /// <returns>ID del registro creado</returns>
    /// <response code="201">Información de perfil agregada exitosamente</response>
    /// <response code="400">Datos de entrada inválidos</response>
    /// <response code="500">Error interno del servidor</response>
    /// <remarks>
    /// Migrado desde: LoginService.agregarPerfilInfo(perfilesInfo info)
    /// 
    /// USO:
    /// - Agregar información de identificación (cédula, RNC, pasaporte)
    /// - Registrar empresa con nombre comercial
    /// - Agregar dirección, presentación y foto de perfil
    /// - Registrar información del gerente (solo empresas)
    /// 
    /// TIPOS DE PERFIL:
    /// 1. **Persona Física**: Solo requiere Identificacion (cédula/pasaporte)
    /// 2. **Empresa**: Requiere Identificacion (RNC) + NombreComercial
    /// 
    /// IMPORTANTE:
    /// - Legacy NO valida si ya existe un perfil para el usuario (permite duplicados)
    /// - Clean Architecture MANTIENE este comportamiento (paridad 100%)
    /// - TipoIdentificacion: 1=Cédula, 2=Pasaporte, 3=RNC
    /// - FotoPerfil se guarda como byte[] (base64 en JSON)
    /// - InformacionGerente es opcional (solo empresas)
    /// 
    /// Sample request (Persona Física):
    /// 
    ///     POST /api/auth/profile-info
    ///     {
    ///        "userId": "550e8400-e29b-41d4-a716-446655440000",
    ///        "identificacion": "00112345678",
    ///        "tipoIdentificacion": 1,
    ///        "direccion": "Calle Principal #123, Santo Domingo",
    ///        "presentacion": "Profesional con 10 años de experiencia..."
    ///     }
    /// 
    /// Sample request (Empresa):
    /// 
    ///     POST /api/auth/profile-info
    ///     {
    ///        "userId": "550e8400-e29b-41d4-a716-446655440000",
    ///        "identificacion": "12345678901",
    ///        "tipoIdentificacion": 3,
    ///        "nombreComercial": "Mi Empresa SRL",
    ///        "direccion": "Av. Winston Churchill #456",
    ///        "cedulaGerente": "00198765432",
    ///        "nombreGerente": "Juan",
    ///        "apellidoGerente": "Pérez",
    ///        "direccionGerente": "Calle Secundaria #789"
    ///     }
    /// 
    /// </remarks>
    [HttpPost("profile-info")]
    [ProducesResponseType(typeof(int), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<int>> AddProfileInfo([FromBody] AddProfileInfoCommand command)
    {
        _logger.LogInformation(
            "POST /api/auth/profile-info - UserId: {UserId}, Identificacion: {Identificacion}",
            command.UserId,
            command.Identificacion);

        try
        {
            var perfilInfoId = await _mediator.Send(command);

            _logger.LogInformation(
                "Información de perfil agregada exitosamente - PerfilInfoId: {PerfilInfoId}, UserId: {UserId}",
                perfilInfoId,
                command.UserId);

            return CreatedAtAction(
                nameof(AddProfileInfo),
                new { id = perfilInfoId },
                new { id = perfilInfoId, message = "Información de perfil agregada exitosamente" });
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning("Error de validación al agregar perfil info: {Message}", ex.Message);
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al agregar información de perfil para usuario {UserId}", command.UserId);
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                new { message = "Error interno al procesar la solicitud" });
        }
    }

    /// <summary>
    /// Method #47: Validar si un correo electrónico pertenece a la cuenta actual del usuario
    /// </summary>
    /// <param name="email">Correo electrónico a validar</param>
    /// <param name="userId">ID del usuario propietario de la cuenta</param>
    /// <returns>true si el correo pertenece al usuario, false si no</returns>
    /// <response code="200">Validación exitosa (true/false)</response>
    /// <response code="400">Parámetros inválidos</response>
    /// <remarks>
    /// Migrado desde: SuscripcionesService.validarCorreoCuentaActual(string correo, string userID) - línea 220
    /// 
    /// **Ejemplo de Request:**
    /// 
    ///     GET /api/auth/validar-correo-cuenta?email=usuario@ejemplo.com&amp;userId=550e8400-e29b-41d4-a716-446655440000
    /// 
    /// **Business Rules:**
    /// - Valida que el correo exista Y pertenezca al userID específico
    /// - Usado antes de cambios de email para verificar propiedad
    /// - Previene conflictos cuando usuario intenta cambiar a email de otra cuenta
    /// - Retorna true solo si email existe y pertenece al usuario
    /// 
    /// **Use Cases:**
    /// - Validación en formulario de cambio de email
    /// - Verificación de propiedad de cuenta
    /// - Prevención de duplicados
    /// </remarks>
    [HttpGet("validar-correo-cuenta")]
    [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ValidarCorreoCuentaActual(
        [FromQuery] string email,
        [FromQuery] string userId)
    {
        _logger.LogInformation(
            "GET /api/auth/validar-correo-cuenta?email={Email}&userId={UserId}",
            email,
            userId);

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(userId))
        {
            return BadRequest(new { message = "Email y userId son requeridos" });
        }

        try
        {
            var query = new ValidarCorreoCuentaActualQuery(email, userId);
            var esValido = await _mediator.Send(query);

            _logger.LogInformation(
                "Validación correo-cuenta: Email={Email}, UserId={UserId}, Resultado={Resultado}",
                email,
                userId,
                esValido ? "VÁLIDO" : "INVÁLIDO");

            return Ok(new { esValido, message = esValido 
                ? "El correo pertenece al usuario" 
                : "El correo no pertenece al usuario o no existe" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al validar correo-cuenta: Email={Email}, UserId={UserId}", email, userId);
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                new { message = "Error interno al procesar la solicitud" });
        }
    }

    /// <summary>
    /// Validar si un correo electrónico pertenece a un usuario específico (userID)
    /// </summary>
    /// <param name="email">Correo electrónico a validar</param>
    /// <param name="userId">ID del usuario propietario de la suscripción</param>
    /// <returns>Resultado de validación con flag booleano</returns>
    /// <response code="200">Validación exitosa con resultado (true/false)</response>
    /// <response code="400">Parámetros inválidos</response>
    /// <response code="500">Error interno del servidor</response>
    /// <remarks>
    /// Migrado desde: SuscripcionesService.validarCorreoCuentaActual(string correo, string userID) - línea 220
    /// 
    /// **LÓGICA LEGACY EXACTA:**
    /// ```csharp
    /// public Cuentas validarCorreoCuentaActual(string correo, string userID)
    /// {
    ///     using (var db = new migenteEntities())
    ///     {
    ///         var result = db.Cuentas.Where(x => x.Email == correo && x.userID==userID)
    ///                                .Include(a => a.perfilesInfo)
    ///                                .FirstOrDefault();
    ///         if (result != null) { return result; }
    ///     };
    ///     return null;
    /// }
    /// ```
    /// 
    /// **CASO DE USO REAL (MiPerfilEmpleador.aspx.cs línea 250):**
    /// - Usuario intenta crear nueva credencial en su suscripción
    /// - Sistema valida si el email YA EXISTE en esa suscripción (userID)
    /// - Si `result != null` → Error: "Este Correo ya Existe en esta Suscripcion"
    /// - Si `result == null` → Permite crear credencial con ese email
    /// 
    /// **⚠️ NOTA IMPORTANTE SOBRE NOMBRE DEL MÉTODO:**
    /// 
    /// El nombre Legacy "validarCorreoCuentaActual" es CONFUSO:
    /// - Sugiere "excluir cuenta actual" (validar en OTRAS cuentas)
    /// - Pero la implementación valida INCLUSIÓN (email pertenece a userID)
    /// 
    /// La lógica real es: **"¿Este email ya está registrado en la suscripción de este usuario?"**
    /// - `true` = Email YA EXISTE en esa suscripción → NO permitir crear otra credencial
    /// - `false` = Email NO EXISTE en esa suscripción → Permitir crear credencial
    /// 
    /// **DIFERENCIA CON ValidarCorreoCuentaActual (línea 1090):**
    /// - **ValidarCorreoCuentaActual (línea 1090):** DEPRECADO - query sin lógica útil
    /// - **ValidateEmailBelongsToUser (este método):** Implementación correcta con nombre clarificado
    /// 
    /// **EJEMPLOS DE USO:**
    /// 
    /// **Ejemplo 1: Email ya existe en la suscripción**
    /// ```
    /// GET /api/auth/validate-email-belongs-to-user?email=admin@migente.com&userId=123
    /// 
    /// Response 200 OK:
    /// {
    ///   "pertenece": true,
    ///   "message": "El correo pertenece al usuario"
    /// }
    /// 
    /// → Interpretación: No se puede crear otra credencial con ese email
    /// ```
    /// 
    /// **Ejemplo 2: Email disponible en la suscripción**
    /// ```
    /// GET /api/auth/validate-email-belongs-to-user?email=nuevo@ejemplo.com&userId=123
    /// 
    /// Response 200 OK:
    /// {
    ///   "pertenece": false,
    ///   "message": "El correo no pertenece al usuario o no existe"
    /// }
    /// 
    /// → Interpretación: Se puede crear credencial con ese email
    /// ```
    /// 
    /// **VALIDACIONES:**
    /// - Email: requerido, formato válido, máximo 100 caracteres
    /// - UserId: requerido, no vacío
    /// 
    /// **HISTORIA:**
    /// - GAP-015 identificado en audit de Legacy
    /// - Implementado como ValidateEmailBelongsToUserQuery con nombre clarificado
    /// - Mantiene comportamiento Legacy pero con mejor semántica
    /// </remarks>
    [HttpGet("validate-email-belongs-to-user")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> ValidateEmailBelongsToUser(
        [FromQuery] string email,
        [FromQuery] string userId)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(userId))
        {
            return BadRequest(new { message = "Email y userId son requeridos" });
        }

        try
        {
            var query = new ValidateEmailBelongsToUserQuery 
            { 
                Email = email, 
                UserID = userId 
            };

            var pertenece = await _mediator.Send(query);

            _logger.LogInformation(
                "GAP-015: Email {Email} validado para userID {UserId}: {Resultado}",
                email,
                userId,
                pertenece ? "PERTENECE" : "NO PERTENECE");

            return Ok(new 
            { 
                pertenece, 
                message = pertenece 
                    ? "El correo pertenece al usuario" 
                    : "El correo no pertenece al usuario o no existe" 
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex, 
                "Error al validar email-belongs-to-user: Email={Email}, UserId={UserId}", 
                email, 
                userId);
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                new { message = "Error interno al procesar la solicitud" });
        }
    }

    /// <summary>
    /// Eliminar usuario del sistema (hard delete)
    /// </summary>
    /// <param name="userId">ID del usuario (GUID)</param>
    /// <param name="credencialId">ID de la credencial a eliminar</param>
    /// <returns>204 No Content si se eliminó exitosamente</returns>
    /// <response code="204">Usuario eliminado exitosamente</response>
    /// <response code="404">Usuario no encontrado</response>
    /// <response code="400">Parámetros inválidos</response>
    /// <remarks>
    /// Migrado desde: LoginService.borrarUsuario(string userID, int credencialID) (línea 131-138)
    /// 
    /// **LÓGICA LEGACY EXACTA:**
    /// ```csharp
    /// public void borrarUsuario(string userID, int credencialID)
    /// {
    ///     using (var db = new migenteEntities())
    ///     {
    ///         var result = db.Credenciales.Where(a => a.userID == userID && a.id==credencialID).FirstOrDefault();
    ///         db.Credenciales.Remove(result);
    ///         db.SaveChanges();
    ///     }
    /// }
    /// ```
    /// 
    /// **COMPORTAMIENTO:**
    /// - Hard delete (no soft delete)
    /// - Busca por userID + credencialID (doble clave)
    /// - Confía en FK constraints de base de datos para cascada
    /// - NO valida última credencial activa (puede dejar usuario sin acceso)
    /// 
    /// **⚠️ DIFERENCIA CON DeleteUserCredential:**
    /// - **DeleteUserCredential (línea 654):** Endpoint moderno, valida última credencial, NO permite eliminar última activa
    /// - **DeleteUser (este):** Réplica exacta del Legacy, NO valida, permite eliminar cualquier credencial
    /// 
    /// **USO:**
    /// - Migración desde Legacy (paridad 100%)
    /// - Compatibilidad con código existente
    /// - ⚠️ Para nuevos desarrollos, preferir `DELETE /api/auth/users/{userId}/credentials/{credentialId}`
    /// 
    /// **GAP-001 COMPLETADO:** ✅
    /// 
    /// Sample request:
    /// 
    ///     DELETE /api/auth/users/550e8400-e29b-41d4-a716-446655440000?credencialId=5
    /// 
    /// </remarks>
    [HttpDelete("users/{userId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> DeleteUser(
        string userId,
        [FromQuery] int credencialId)
    {
        _logger.LogInformation(
            "DELETE /api/auth/users/{UserId}?credencialId={CredencialId}",
            userId,
            credencialId);

        if (string.IsNullOrWhiteSpace(userId) || credencialId <= 0)
        {
            return BadRequest(new { message = "UserId y credencialId son requeridos" });
        }

        try
        {
            var command = new DeleteUserCommand { UserID = userId, CredencialID = credencialId };
            await _mediator.Send(command);

            _logger.LogInformation(
                "Usuario eliminado exitosamente - UserId: {UserId}, CredencialId: {CredencialId}",
                userId,
                credencialId);

            return NoContent();
        }
        catch (NotFoundException ex)
        {
            _logger.LogWarning("Usuario no encontrado: {Message}", ex.Message);
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al eliminar usuario - UserId: {UserId}", userId);
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                new { message = "Error interno al procesar la solicitud" });
        }
    }

    #region Password Recovery Endpoints

    /// <summary>
    /// Solicitar recuperación de contraseña (envía token por email)
    /// </summary>
    /// <param name="command">Email del usuario</param>
    /// <returns>Confirmación de envío</returns>
    /// <response code="200">Token enviado por email</response>
    /// <response code="404">Usuario no encontrado</response>
    /// <response code="400">Email inválido</response>
    [HttpPost("forgot-password")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> ForgotPassword([FromBody] MiGenteEnLinea.Application.Features.Authentication.Commands.ForgotPassword.ForgotPasswordCommand command)
    {
        _logger.LogInformation("POST /api/auth/forgot-password - Email: {Email}", command.Email);

        // Handler throws NotFoundException if email not found
        // GlobalExceptionHandlerMiddleware will convert to 404
        await _mediator.Send(command);

        _logger.LogInformation("Token de recuperación enviado - Email: {Email}", command.Email);
        return Ok(new { message = "Se ha enviado un código de recuperación a su email." });
    }

    /// <summary>
    /// Resetear contraseña con token de recuperación
    /// </summary>
    /// <param name="command">Email, token y nueva contraseña</param>
    /// <returns>Confirmación de cambio</returns>
    /// <response code="200">Contraseña cambiada exitosamente</response>
    /// <response code="400">Token inválido o expirado</response>
    /// <response code="404">Usuario no encontrado</response>
    [HttpPost("reset-password")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> ResetPassword([FromBody] MiGenteEnLinea.Application.Features.Authentication.Commands.ResetPassword.ResetPasswordCommand command)
    {
        _logger.LogInformation("POST /api/auth/reset-password - Email: {Email}", command.Email);

        try
        {
            var success = await _mediator.Send(command);

            if (!success)
            {
                _logger.LogWarning("ResetPassword fallido - Token inválido o expirado: {Email}", command.Email);
                return BadRequest(new { message = "Token inválido o expirado." });
            }

            _logger.LogInformation("Contraseña reseteada exitosamente - Email: {Email}", command.Email);
            return Ok(new { message = "Contraseña cambiada exitosamente. Ya puede iniciar sesión." });
        }
        catch (NotFoundException ex)
        {
            _logger.LogWarning("ResetPassword - Usuario no encontrado: {Message}", ex.Message);
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al resetear contraseña - Email: {Email}", command.Email);
            return BadRequest(new { message = ex.Message });
        }
    }

    #endregion

    #region User Management Endpoints

    /// <summary>
    /// Eliminar usuario (soft delete - marca Activo = false)
    /// </summary>
    /// <param name="command">UserID y CredencialID</param>
    /// <returns>Confirmación de eliminación</returns>
    /// <response code="200">Usuario eliminado</response>
    /// <response code="404">Usuario no encontrado</response>
    [HttpPost("delete-user")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> DeleteUserSoft([FromBody] DeleteUserCommand command)
    {
        _logger.LogInformation("POST /api/auth/delete-user - UserID: {UserID}", command.UserID);

        try
        {
            var success = await _mediator.Send(command);

            if (!success)
            {
                _logger.LogWarning("DeleteUser fallido - Usuario no encontrado: {UserID}", command.UserID);
                return NotFound(new { message = "Usuario no encontrado." });
            }

            _logger.LogInformation("Usuario eliminado exitosamente - UserID: {UserID}", command.UserID);
            return Ok(new { message = "Usuario eliminado exitosamente." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al eliminar usuario - UserID: {UserID}", command.UserID);
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Cambiar contraseña por ID de credencial (GAP-014)
    /// </summary>
    /// <param name="command">CredencialId y nueva contraseña</param>
    /// <returns>Confirmación de cambio</returns>
    /// <response code="200">Contraseña cambiada</response>
    /// <response code="404">Credencial no encontrada</response>
    [HttpPost("change-password-by-id")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> ChangePasswordById([FromBody] ChangePasswordByIdCommand command)
    {
        _logger.LogInformation("POST /api/auth/change-password-by-id - CredencialId: {CredencialId}", command.CredencialId);

        try
        {
            var success = await _mediator.Send(command);

            if (!success)
            {
                _logger.LogWarning("ChangePasswordById fallido - Credencial no encontrada: {CredencialId}", command.CredencialId);
                return NotFound(new { message = "Credencial no encontrada." });
            }

            _logger.LogInformation("Contraseña cambiada exitosamente - CredencialId: {CredencialId}", command.CredencialId);
            return Ok(new { message = "Contraseña cambiada exitosamente." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al cambiar contraseña - CredencialId: {CredencialId}", command.CredencialId);
            return BadRequest(new { message = ex.Message });
        }
    }

    #endregion
}

