using FluentValidation;

namespace GuarinoApp.Application.Modules.Stock.Commands.CrearPallet;
public class CrearPalletCommandValidator : AbstractValidator<CrearPalletCommand>
    {
    public CrearPalletCommandValidator()
        {
        RuleFor(v => v.VolcadoId).GreaterThan(0);
        RuleFor(v => v.Codigo).NotEmpty().MaximumLength(50);
        RuleFor(v => v.Estado).IsInEnum();
        }
    }
