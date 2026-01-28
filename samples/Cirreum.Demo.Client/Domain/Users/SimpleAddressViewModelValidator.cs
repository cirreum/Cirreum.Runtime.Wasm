namespace Cirreum.Demo.Client.Domain.Users;

using FluentValidation;

public class SimpleAddressViewModelValidator : AbstractValidator<SimpleAddressViewModel> {
	public SimpleAddressViewModelValidator() {
		this.RuleFor(x => x.Street)
			.NotEmpty()
			.MaximumLength(100)
			.WithMessage("Street address is required and cannot exceed 100 characters");

		this.RuleFor(x => x.City)
			.NotEmpty()
			.MaximumLength(50)
			.WithMessage("City is required and cannot exceed 50 characters");

		this.RuleFor(x => x.State)
			.NotEmpty()
			.MaximumLength(2)
			.WithMessage("Please enter a valid 2-letter state code");

		this.RuleFor(x => x.PostalCode)
			.NotEmpty()
			.Matches(@"^\d{5}(-\d{4})?$")
			.WithMessage("Please enter a valid ZIP code (e.g., 12345 or 12345-6789)");

		this.RuleFor(x => x.Country)
			.MaximumLength(50)
			.When(x => !string.IsNullOrEmpty(x.Country))
			.WithMessage("Country name cannot exceed 50 characters");
	}
}