using FluentValidation;
using System;

namespace MiGenteEnLinea.Application.Features.Empleados.Commands.CreateEmpleado;

/// <summary>
/// Validador para CreateEmpleadoCommand.
/// Implementa las mismas reglas de validación del Legacy.
/// </summary>
public class CreateEmpleadoCommandValidator : AbstractValidator<CreateEmpleadoCommand>
{
    public CreateEmpleadoCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("UserId es requerido")
            .MaximumLength(450);

        RuleFor(x => x.Identificacion)
            .NotEmpty().WithMessage("Identificación es requerida")
            .Length(11).WithMessage("La cédula debe tener 11 dígitos")
            .Matches(@"^[0-9]{11}$")
            .WithMessage("La cédula debe contener solo números");

        RuleFor(x => x.Nombre)
            .NotEmpty().WithMessage("Nombre es requerido")
            .MaximumLength(100);

        RuleFor(x => x.Apellido)
            .NotEmpty().WithMessage("Apellido es requerido")
            .MaximumLength(100);

        RuleFor(x => x.Alias)
            .MaximumLength(100)
            .When(x => !string.IsNullOrEmpty(x.Alias));

        RuleFor(x => x.EstadoCivil)
            .InclusiveBetween(1, 4)
            .WithMessage("Estado civil debe ser: 1=Soltero, 2=Casado, 3=Divorciado, 4=Viudo")
            .When(x => x.EstadoCivil.HasValue);

        RuleFor(x => x.Nacimiento)
            .LessThan(DateTime.Now.AddYears(-16))
            .WithMessage("Empleado debe tener al menos 16 años")
            .When(x => x.Nacimiento.HasValue);

        RuleFor(x => x.Telefono1)
            .Matches(@"^[0-9]{10}$")
            .WithMessage("Teléfono debe tener 10 dígitos")
            .When(x => !string.IsNullOrEmpty(x.Telefono1));

        RuleFor(x => x.Telefono2)
            .Matches(@"^[0-9]{10}$")
            .WithMessage("Teléfono debe tener 10 dígitos")
            .When(x => !string.IsNullOrEmpty(x.Telefono2));

        RuleFor(x => x.FechaInicio)
            .NotEmpty().WithMessage("Fecha de inicio es requerida")
            .LessThanOrEqualTo(DateTime.Now)
            .WithMessage("Fecha de inicio no puede ser futura");

        RuleFor(x => x.Salario)
            .GreaterThan(0).WithMessage("Salario debe ser mayor a 0")
            .LessThanOrEqualTo(10000000).WithMessage("Salario excede el límite permitido");

        RuleFor(x => x.PeriodoPago)
            .InclusiveBetween(1, 3)
            .WithMessage("Período de pago debe ser: 1=Semanal, 2=Quincenal, 3=Mensual");

        RuleFor(x => x.DiasPago)
            .InclusiveBetween(1, 31)
            .WithMessage("Días de pago debe estar entre 1 y 31")
            .When(x => x.DiasPago.HasValue);
    }
}
