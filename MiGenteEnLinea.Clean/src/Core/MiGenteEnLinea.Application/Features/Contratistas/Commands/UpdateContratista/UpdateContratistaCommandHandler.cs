using MediatR;
using Microsoft.Extensions.Logging;
using MiGenteEnLinea.Application.Common.Interfaces;
using MiGenteEnLinea.Application.Common.Exceptions;
using MiGenteEnLinea.Domain.Interfaces.Repositories;
using MiGenteEnLinea.Domain.Interfaces.Repositories.Contratistas;
using MiGenteEnLinea.Domain.ValueObjects;

namespace MiGenteEnLinea.Application.Features.Contratistas.Commands.UpdateContratista;

/// <summary>
/// Handler: Actualiza el perfil de un contratista
/// </summary>
/// <remarks>
/// ✅ SECURITY FIX (Oct 30, 2025): Ownership validation implementada
/// - Verifica que el usuario solo pueda editar su propio perfil
/// - Admins pueden editar cualquier perfil (bypass)
/// - Lanza ForbiddenAccessException (403) si no tiene permisos
/// </remarks>
public class UpdateContratistaCommandHandler : IRequestHandler<UpdateContratistaCommand>
{
    private readonly IContratistaRepository _contratistaRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<UpdateContratistaCommandHandler> _logger;

    public UpdateContratistaCommandHandler(
        IContratistaRepository contratistaRepository,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        ILogger<UpdateContratistaCommandHandler> logger)
    {
        _contratistaRepository = contratistaRepository;
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    public async Task Handle(UpdateContratistaCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Actualizando perfil de contratista para userId: {UserId}", request.UserId);

        // 1. BUSCAR CONTRATISTA por userId usando Repository
        var contratista = await _contratistaRepository
            .GetByUserIdAsync(request.UserId, cancellationToken);

        if (contratista == null)
        {
            _logger.LogWarning("Contratista no encontrado para userId: {UserId}", request.UserId);
            throw new InvalidOperationException($"No existe un perfil de contratista para el usuario {request.UserId}");
        }

        // ============================================
        // 2. SECURITY CHECK - Ownership validation
        // ============================================
        var currentUserId = _currentUserService.UserId;
        var isAdmin = _currentUserService.IsInRole("Admin");

        // Verificar que el usuario actual sea el dueño del perfil O sea Admin
        if (currentUserId != request.UserId && !isAdmin)
        {
            _logger.LogWarning(
                "⚠️ INTENTO DE ACCESO NO AUTORIZADO: Usuario {CurrentUserId} intentó editar perfil de contratista {TargetUserId}",
                currentUserId, request.UserId);

            throw new ForbiddenAccessException("No tiene permisos para editar este perfil de contratista.");
        }

        _logger.LogInformation(
            "✅ Authorization check passed. CurrentUser: {CurrentUserId}, TargetUser: {TargetUserId}, IsAdmin: {IsAdmin}",
            currentUserId, request.UserId, isAdmin);

        // 3. ACTUALIZAR PERFIL BÁSICO (si hay cambios)
        bool hayCambiosPerfil = request.Titulo != null ||
                                request.Sector != null ||
                                request.Experiencia.HasValue ||
                                request.Presentacion != null ||
                                request.Provincia != null ||
                                request.NivelNacional.HasValue;

        if (hayCambiosPerfil)
        {
            contratista.ActualizarPerfil(
                titulo: request.Titulo,
                sector: request.Sector,
                experiencia: request.Experiencia,
                presentacion: request.Presentacion,
                provincia: request.Provincia,
                nivelNacional: request.NivelNacional
            );
        }

        // 3. ACTUALIZAR CONTACTO (si hay cambios)
        bool hayCambiosContacto = request.Telefono1 != null ||
                                  request.Whatsapp1.HasValue ||
                                  request.Telefono2 != null ||
                                  request.Whatsapp2.HasValue ||
                                  request.Email != null;

        if (hayCambiosContacto)
        {
            Email? email = null;
            if (request.Email != null)
            {
                email = Email.Create(request.Email);
            }

            contratista.ActualizarContacto(
                telefono1: request.Telefono1,
                whatsapp1: request.Whatsapp1,
                telefono2: request.Telefono2,
                whatsapp2: request.Whatsapp2,
                email: email
            );
        }

        // 4. GUARDAR CAMBIOS usando UnitOfWork
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Perfil de contratista actualizado exitosamente. ContratistaId: {ContratistaId}, UserId: {UserId}",
            contratista.Id, request.UserId);
    }
}
