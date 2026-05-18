using FluentValidation;
using GadiSewa.Application.DTOs.Parts;

namespace GadiSewa.Application.Validators.Parts;

public sealed class CreatePartRequestDtoValidator : AbstractValidator<CreatePartRequestDto>
{
    public CreatePartRequestDtoValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.PartNumber)
            .NotEmpty()
            .MaximumLength(80);

        RuleFor(x => x.UnitPrice)
            .GreaterThanOrEqualTo(0);

        RuleFor(x => x.StockQuantity)
            .GreaterThanOrEqualTo(0);

        RuleFor(x => x.ReorderLevel)
            .GreaterThanOrEqualTo(0);
    }
}