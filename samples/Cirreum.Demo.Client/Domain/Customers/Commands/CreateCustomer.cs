namespace Cirreum.Demo.Client.Domain.Customers.Commands;

using FluentValidation;

public record CreateCustomer(
	string FirstName,
	string LastName
) : IAuthorizableRequest<string>;

public class CreateCustomerAuthorizor : AuthorizationValidatorBase<CreateCustomer> {
	public CreateCustomerAuthorizor() {
		this.HasAnyRole(
			ApplicationRoles.AppAdminRole,
			ApplicationRoles.AppManagerRole
		);
	}
}

public class CreateCustomerValidator : AbstractValidator<CreateCustomer> {
	public CreateCustomerValidator() {
		this.RuleFor(x => x.FirstName)
			.NotEmpty().WithMessage("First name is required.")
			.MaximumLength(50).WithMessage("First name cannot exceed 50 characters.");
		this.RuleFor(x => x.LastName)
			.NotEmpty().WithMessage("Last name is required.")
			.MaximumLength(50).WithMessage("Last name cannot exceed 50 characters.");
	}
}