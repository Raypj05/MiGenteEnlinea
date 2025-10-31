using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MiGenteEnLinea.Application.Common.Exceptions;
using MiGenteEnLinea.Application.Common.Interfaces;
using MiGenteEnLinea.Domain.Interfaces.Repositories;

namespace MiGenteEnLinea.Application.Features.Authentication.Commands.ResendActivationEmail;

/// <summary>
/// Handler para ResendActivationEmailCommand
/// Réplica EXACTA de SuscripcionesService.enviarCorreoActivacion() del Legacy
/// GAP-011: Soporta enviar con userID o solo con email
/// </summary>
public sealed class ResendActivationEmailCommandHandler : IRequestHandler<ResendActivationEmailCommand, bool>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IEmailService _emailService;
    private readonly ILogger<ResendActivationEmailCommandHandler> _logger;

    public ResendActivationEmailCommandHandler(
        IUnitOfWork unitOfWork,
        IEmailService emailService,
        ILogger<ResendActivationEmailCommandHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _emailService = emailService;
        _logger = logger;
    }

    /// <summary>
    /// Reenvía email de activación a un usuario
    /// 
    /// Legacy behavior (SuscripcionesService.cs líneas 74-87):
    /// - Si recibe Cuentas (p != null): usa ese objeto
    /// - Si recibe solo userID (p == null): query db.Cuentas.Where(x => x.userID == userID).FirstOrDefault()
    /// - Construye URL: host + "/Activar.aspx?userID=" + userID + "&amp;email=" + email
    /// - Llama EmailSender.SendEmailRegistro(Nombre, Email, "Bienvenido a Mi Gente", url)
    /// 
    /// Clean behavior:
    /// - Si recibe UserId: query Perfiles by userId
    /// - Si NO recibe UserId: query Perfiles by email
    /// - Construye URL igual que Legacy
    /// - Llama IEmailService.SendActivationEmailAsync()
    /// </summary>
    public async Task<bool> Handle(ResendActivationEmailCommand request, CancellationToken cancellationToken)
    {
        // ================================================================================
        // PASO 1: OBTENER PERFIL (por userId o por email)
        // ================================================================================
        // Legacy líneas 77-80: if (p == null) { p = db.Cuentas.Where(x => x.userID == userID).FirstOrDefault(); }
        Domain.Entities.Seguridad.Perfile? perfil = null;

        if (!string.IsNullOrWhiteSpace(request.UserId))
        {
            // Buscar por UserId
            perfil = await _unitOfWork.Perfiles.GetByUserIdAsync(request.UserId, cancellationToken);
        }
        else
        {
            // Buscar por Email
            var perfiles = await _unitOfWork.Perfiles.GetAllAsync(cancellationToken);
            perfil = perfiles.FirstOrDefault(p => p.Email == request.Email);
        }

        if (perfil == null)
        {
            _logger.LogWarning(
                "No se encontró perfil para reenvío de activación. UserId: {UserId}, Email: {Email}",
                request.UserId ?? "NULL",
                request.Email);
            return false;
        }

        // ================================================================================
        // PASO 2: VERIFICAR QUE EL USUARIO NO ESTÉ YA ACTIVO
        // ================================================================================
        // Si la credencial ya está activa, no tiene sentido reenviar el email
        var credencial = await _unitOfWork.Credenciales
            .GetByUserIdAsync(perfil.UserId, cancellationToken);

        if (credencial == null)
        {
            _logger.LogWarning(
                "No se encontró credencial para userId: {UserId}",
                perfil.UserId);
            return false; // Usuario no existe
        }

        if (credencial.Activo)
        {
            _logger.LogWarning(
                "Usuario ya está activo, no se puede reenviar email. UserId: {UserId}",
                perfil.UserId);
            
            // Lanzar BadRequestException para que el controller devuelva 400
            throw new BadRequestException("La cuenta ya está activa. No es necesario reenviar el email de activación.");
        }

        // ================================================================================
        // PASO 3: CONSTRUIR URL DE ACTIVACIÓN
        // ================================================================================
        // Legacy línea 83: string url = host + "/Activar.aspx?userID=" + perfil.userID + "&email=" + email;
        // Clean: Mismo formato pero con sintaxis interpolada
        var activationUrl = $"{request.Host}/Activar.aspx?userID={perfil.UserId}&email={request.Email}";

        // ================================================================================
        // PASO 4: ENVIAR EMAIL
        // ================================================================================
        // Legacy líneas 84-85:
        // EmailSender sender = new EmailSender();
        // sender.SendEmailRegistro(perfil.Nombre, perfil.Email, "Bienvenido a Mi Gente", url);
        try
        {
            var nombreCompleto = $"{perfil.Nombre} {perfil.Apellido}";

            await _emailService.SendActivationEmailAsync(
                toEmail: request.Email,
                toName: nombreCompleto,
                activationUrl: activationUrl
            );

            _logger.LogInformation(
                "Email de activación reenviado exitosamente. UserId: {UserId}, Email: {Email}",
                perfil.UserId,
                request.Email);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error al reenviar email de activación. UserId: {UserId}, Email: {Email}",
                perfil.UserId,
                request.Email);

            // Legacy no maneja excepciones, pero Clean sí debe loggear
            return false;
        }
    }
}
