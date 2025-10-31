using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MiGenteEnLinea.Application.Common.Interfaces;
using MiGenteEnLinea.Domain.Entities.Authentication;
using System.Security.Cryptography;

namespace MiGenteEnLinea.Application.Features.Authentication.Commands.ForgotPassword;

/// <summary>
/// Handler para solicitar recuperación de contraseña
/// </summary>
public sealed class ForgotPasswordCommandHandler : IRequestHandler<ForgotPasswordCommand, bool>
{
    private readonly IApplicationDbContext _context;
    private readonly IEmailService _emailService;
    private readonly ILogger<ForgotPasswordCommandHandler> _logger;

    public ForgotPasswordCommandHandler(
        IApplicationDbContext context,
        IEmailService emailService,
        ILogger<ForgotPasswordCommandHandler> logger)
    {
        _context = context;
        _emailService = emailService;
        _logger = logger;
    }

    public async Task<bool> Handle(ForgotPasswordCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("ForgotPassword: Email={Email}", request.Email);

        // Buscar usuario por email
        var credencial = await _context.Credenciales
            .Where(c => c.Email == request.Email && c.Activo)
            .FirstOrDefaultAsync(cancellationToken);

        if (credencial == null)
        {
            _logger.LogWarning("ForgotPassword: Email no encontrado o cuenta inactiva - {Email}", request.Email);
            throw new Common.Exceptions.NotFoundException("Usuario", request.Email);
        }

        // Generar token seguro (6 dígitos)
        var token = RandomNumberGenerator.GetInt32(100000, 999999).ToString();
        
        // Crear entidad PasswordResetToken
        var resetToken = PasswordResetToken.Create(
            credencial.UserId,
            request.Email,
            token,
            expirationMinutes: 15);

        // Guardar token en base de datos
        _context.PasswordResetTokens.Add(resetToken);
        await _context.SaveChangesAsync(cancellationToken);
        
        _logger.LogInformation(
            "ForgotPassword: Token guardado - TokenId={TokenId}, UserId={UserId}",
            resetToken.Id, credencial.UserId);

        // Enviar email con token
        var resetUrl = $"https://migenteonline.com/reset-password?email={request.Email}&token={token}";
        
        await _emailService.SendPasswordResetEmailAsync(
            request.Email,
            credencial.UserId,
            resetUrl);

        _logger.LogInformation("ForgotPassword: Email enviado exitosamente a {Email}", request.Email);
        return true;
    }
}
