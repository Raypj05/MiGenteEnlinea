using FluentValidation;

namespace MiGenteEnLinea.Application.Features.Empleados.Commands.DarDeBajaEmpleado;

public class DarDeBajaEmpleadoCommandValidator : AbstractValidator<DarDeBajaEmpleadoCommand>
{
    public DarDeBajaEmpleadoCommandValidator()
    {
        RuleFor(x => x.EmpleadoId)
            .GreaterThan(0)
            .WithMessage("El ID del empleado debe ser mayor que 0");

        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("El ID del usuario es requerido");

        RuleFor(x => x.FechaBaja)
            .NotEmpty()
            .WithMessage("La fecha de baja es requerida");
            // Removed .LessThanOrEqualTo(DateTime.Now) to allow programmed terminations (future dates)

        RuleFor(x => x.Prestaciones)
            .GreaterThanOrEqualTo(0)
            .WithMessage("Las prestaciones no pueden ser negativas");

        RuleFor(x => x.Motivo)
            .NotEmpty()
            .WithMessage("El motivo de baja es requerido")
            .MaximumLength(500)
            .WithMessage("El motivo no puede exceder 500 caracteres");
    }
}
