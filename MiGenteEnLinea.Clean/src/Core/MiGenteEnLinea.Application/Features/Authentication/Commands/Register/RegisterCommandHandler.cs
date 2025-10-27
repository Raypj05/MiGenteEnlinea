using MediatR;
using Microsoft.Extensions.Logging;
using MiGenteEnLinea.Application.Common.Interfaces;
using MiGenteEnLinea.Application.Features.Authentication.DTOs;
using MiGenteEnLinea.Domain.Entities.Authentication;
using MiGenteEnLinea.Domain.Entities.Contratistas;
using MiGenteEnLinea.Domain.Entities.Seguridad;
using MiGenteEnLinea.Domain.Interfaces.Repositories;

namespace MiGenteEnLinea.Application.Features.Authentication.Commands.Register;

/// <summary>
/// Handler para RegisterCommand
/// ESTRATEGIA DE MIGRACIÓN (Opción A - Identity Primario):
/// 1. Crear usuario en ASP.NET Core Identity (tabla AspNetUsers) - PRIMARIO
/// 2. Sincronizar con tablas Legacy (Perfiles, Credenciales) - SECUNDARIO para lógica de negocio
/// 3. Crear Contratista automáticamente (GAP-010) - para compatibilidad
/// 4. Enviar email de activación
/// </summary>
public sealed class RegisterCommandHandler : IRequestHandler<RegisterCommand, RegisterResult>
{
    private readonly IIdentityService _identityService; // ✅ Sistema Identity (primario)
    private readonly IUnitOfWork _unitOfWork; // ✅ Tablas Legacy (sincronización)
    private readonly IPasswordHasher _passwordHasher; // ✅ Para sincronizar password con Credenciales Legacy
    private readonly IEmailService _emailService;
    private readonly ILogger<RegisterCommandHandler> _logger;

    public RegisterCommandHandler(
        IIdentityService identityService,
        IUnitOfWork unitOfWork,
        IPasswordHasher passwordHasher,
        IEmailService emailService,
        ILogger<RegisterCommandHandler> logger)
    {
        _identityService = identityService;
        _unitOfWork = unitOfWork;
        _passwordHasher = passwordHasher;
        _emailService = emailService;
        _logger = logger;
    }

    public async Task<RegisterResult> Handle(RegisterCommand request, CancellationToken cancellationToken)
    {
        // ================================================================================
        // PASO 1: VERIFICAR QUE EL EMAIL NO EXISTA (en Identity)
        // ================================================================================
        var emailExists = await _identityService.UserExistsAsync(request.Email);

        if (emailExists)
        {
            _logger.LogWarning("Intento de registro con email duplicado: {Email}", request.Email);
            return new RegisterResult
            {
                Success = false,
                UserId = null,
                Message = "El correo electrónico ya está registrado en el sistema"
            };
        }

        // ================================================================================
        // PASO 2: REGISTRAR USUARIO EN IDENTITY (PRIMARIO) ✅
        // ================================================================================
        // Identity crea el usuario en AspNetUsers con password hasheado automáticamente
        var nombreCompleto = $"{request.Nombre} {request.Apellido}";
        var tipo = request.Tipo == 1 ? "Empleador" : "Contratista";

        string userId;
        try
        {
            userId = await _identityService.RegisterAsync(
                email: request.Email,
                password: request.Password,
                nombreCompleto: nombreCompleto,
                tipo: tipo
            );

            _logger.LogInformation(
                "Usuario registrado en Identity. UserId: {UserId}, Email: {Email}, Tipo: {Tipo}",
                userId, request.Email, tipo);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al registrar usuario en Identity: {Email}", request.Email);
            return new RegisterResult
            {
                Success = false,
                UserId = null,
                Message = "Error al registrar usuario. Por favor, intenta nuevamente."
            };
        }

        // ================================================================================
        // PASO 3: SINCRONIZAR CON TABLAS LEGACY (para lógica de negocio existente) ✅
        // ================================================================================
        // Nota: Esto es temporal durante la migración. Eventualmente toda la lógica de negocio
        //       usará Identity y estas tablas se deprecarán.

        try
        {
            // 3.1 Crear Perfil (tabla Perfiles - usada en lógica de negocio)
            Perfile perfil;
            if (request.Tipo == 1)
            {
                perfil = Perfile.CrearPerfilEmpleador(
                    userId: userId,
                    nombre: request.Nombre,
                    apellido: request.Apellido,
                    email: request.Email,
                    telefono1: request.Telefono1,
                    telefono2: request.Telefono2
                );
            }
            else
            {
                perfil = Perfile.CrearPerfilContratista(
                    userId: userId,
                    nombre: request.Nombre,
                    apellido: request.Apellido,
                    email: request.Email,
                    telefono1: request.Telefono1,
                    telefono2: request.Telefono2
                );
            }

            await _unitOfWork.Perfiles.AddAsync(perfil, cancellationToken);

            // 3.2 Crear Credencial (tabla Credenciales - usada en lógica de negocio)
            var email = Domain.ValueObjects.Email.Create(request.Email);
            var credencial = Credencial.Create(
                userId: userId,
                email: email!,
                passwordHash: _passwordHasher.HashPassword(request.Password)
            );

            await _unitOfWork.Credenciales.AddAsync(credencial, cancellationToken);

            // 3.3 Crear Contratista (GAP-010 - Legacy siempre crea Contratista para todos)
            // Razón: En el sistema Legacy, todo usuario puede ofrecer servicios
            var contratista = Contratista.Create(
                userId: userId,
                nombre: request.Nombre,
                apellido: request.Apellido,
                tipo: 1, // Persona Física (hardcoded como en Legacy)
                telefono1: request.Telefono1
            );

            await _unitOfWork.Contratistas.AddAsync(contratista, cancellationToken);

            // 3.4 Guardar cambios Legacy
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Tablas Legacy sincronizadas para usuario: {UserId}",
                userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error al sincronizar tablas Legacy para usuario {UserId}. Usuario existe en Identity pero no en Legacy.",
                userId);

            // Usuario ya está en Identity, pero sincronización Legacy falló
            // Retornar éxito parcial - el usuario puede loguearse pero puede tener problemas con funcionalidades Legacy
            return new RegisterResult
            {
                Success = true,
                UserId = userId,
                Email = request.Email,
                Message = "Registro exitoso. Revisa tu correo para activar tu cuenta. (Nota: Algunas funcionalidades pueden requerir configuración adicional)"
            };
        }

        // ================================================================================
        // PASO 4: ENVIAR EMAIL DE ACTIVACIÓN ✅
        // ================================================================================
        try
        {
            var activationUrl = $"{request.Host}/Activar.aspx?userID={userId}&email={request.Email}";

            await _emailService.SendActivationEmailAsync(
                toEmail: request.Email,
                toName: nombreCompleto,
                activationUrl: activationUrl
            );

            _logger.LogInformation("Email de activación enviado a: {Email}", request.Email);
        }
        catch (Exception ex)
        {
            // NO fallar el registro si el email falla
            // El usuario ya está creado, solo el email falló
            _logger.LogError(ex, "Error al enviar email de activación a {Email}", request.Email);
        }

        // ================================================================================
        // PASO 5: RETORNAR RESULTADO EXITOSO ✅
        // ================================================================================
        return new RegisterResult
        {
            Success = true,
            UserId = userId,
            Email = request.Email,
            Message = "Registro exitoso. Por favor revisa tu correo electrónico para activar tu cuenta."
        };
    }
}
