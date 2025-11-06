using FluentValidation;

namespace MiGenteEnLinea.Application.Features.Empleadores.Commands.CreateEmpleador;

/// <summary>
/// Validador: CreateEmpleadorCommand
/// </summary>
public sealed class CreateEmpleadorCommandValidator : AbstractValidator<CreateEmpleadorCommand>
{
    public CreateEmpleadorCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("UserId es requerido")
            .MaximumLength(100).WithMessage("UserId no puede exceder 100 caracteres");
            // ✅ REMOVED: Guid validation to support Legacy string userIds (e.g., "test-empleador-001")
            // Legacy system used simple strings, not GUIDs, for backward compatibility

        RuleFor(x => x.Habilidades)
            .MaximumLength(200).WithMessage("Habilidades no puede exceder 200 caracteres")
            .When(x => !string.IsNullOrWhiteSpace(x.Habilidades));

        RuleFor(x => x.Experiencia)
            .MaximumLength(200).WithMessage("Experiencia no puede exceder 200 caracteres")
            .When(x => !string.IsNullOrWhiteSpace(x.Experiencia));

        RuleFor(x => x.Descripcion)
            .MaximumLength(500).WithMessage("Descripcion no puede exceder 500 caracteres")
            .When(x => !string.IsNullOrWhiteSpace(x.Descripcion));
    }
}
