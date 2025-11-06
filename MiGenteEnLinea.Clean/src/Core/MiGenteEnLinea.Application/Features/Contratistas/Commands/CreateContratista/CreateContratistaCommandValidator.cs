using FluentValidation;

namespace MiGenteEnLinea.Application.Features.Contratistas.Commands.CreateContratista;

/// <summary>
/// Validator: Validaciones de input para CreateContratistaCommand
/// </summary>
public class CreateContratistaCommandValidator : AbstractValidator<CreateContratistaCommand>
{
    public CreateContratistaCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("UserId es requerido")
            .MaximumLength(100).WithMessage("UserId no puede exceder 100 caracteres");
            // ✅ REMOVED: Guid validation to support Legacy string userIds (e.g., "test-contratista-201")
            // Legacy system used simple strings, not GUIDs, for backward compatibility

        RuleFor(x => x.Nombre)
            .NotEmpty().WithMessage("Nombre es requerido")
            .MaximumLength(20).WithMessage("Nombre no puede exceder 20 caracteres");

        RuleFor(x => x.Apellido)
            .NotEmpty().WithMessage("Apellido es requerido")
            .MaximumLength(50).WithMessage("Apellido no puede exceder 50 caracteres");

        RuleFor(x => x.Tipo)
            .InclusiveBetween(1, 2).WithMessage("Tipo debe ser 1 (Persona Física) o 2 (Empresa)");

        RuleFor(x => x.Titulo)
            .MaximumLength(70).WithMessage("Titulo no puede exceder 70 caracteres")
            .When(x => !string.IsNullOrEmpty(x.Titulo));

        RuleFor(x => x.Identificacion)
            .MaximumLength(20).WithMessage("Identificacion no puede exceder 20 caracteres")
            .When(x => !string.IsNullOrEmpty(x.Identificacion));

        RuleFor(x => x.Sector)
            .MaximumLength(40).WithMessage("Sector no puede exceder 40 caracteres")
            .When(x => !string.IsNullOrEmpty(x.Sector));

        RuleFor(x => x.Experiencia)
            .GreaterThanOrEqualTo(0).WithMessage("Experiencia no puede ser negativa")
            .When(x => x.Experiencia.HasValue);

        RuleFor(x => x.Presentacion)
            .MaximumLength(250).WithMessage("Presentacion no puede exceder 250 caracteres")
            .When(x => !string.IsNullOrEmpty(x.Presentacion));

        RuleFor(x => x.Telefono1)
            .MaximumLength(16).WithMessage("Telefono1 no puede exceder 16 caracteres")
            .When(x => !string.IsNullOrEmpty(x.Telefono1));

        RuleFor(x => x.Provincia)
            .MaximumLength(50).WithMessage("Provincia no puede exceder 50 caracteres")
            .When(x => !string.IsNullOrEmpty(x.Provincia));
    }
}
