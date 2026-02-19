using FluentValidation;
using NexusFlow.AppCore.Features.Purchasing.Suppliers.Commands;
using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.AppCore.Features.Purchasing.Suppliers.Validators
{
    public class CreateSupplierValidator : AbstractValidator<CreateSupplierCommand>
    {
        public CreateSupplierValidator()
        {
            RuleFor(x => x.Supplier.Name)
                .NotEmpty().WithMessage("Company Name is required.")
                .MaximumLength(200);

            RuleFor(x => x.Supplier.TaxRegNo)
                .NotEmpty().WithMessage("Tax ID / VAT Number is mandatory for tax compliance.");

            RuleFor(x => x.Supplier.Email)
                .NotEmpty().EmailAddress().WithMessage("A valid primary email is required.");

            RuleFor(x => x.Supplier.CurrencyCode)
                .NotEmpty().Length(3).WithMessage("Currency Code must be 3 characters (e.g., USD).");

            RuleFor(x => x.Supplier.PaymentTermId)
                .GreaterThan(0).WithMessage("Please select a valid Payment Term.");

            // Conditional Validation: If Swift Code is provided, IBAN is usually recommended for EU
            When(x => x.Supplier.Country == "GB" || x.Supplier.Country == "DE" || x.Supplier.Country == "FR", () => {
                RuleFor(x => x.Supplier.BankIBAN)
                    .NotEmpty().WithMessage("IBAN is required for European vendors.");
            });
        }
    }
}
