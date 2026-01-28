namespace Cirreum.Demo.Client.Domain.Customers.Queries;

using FluentValidation;

public record GetCustomer(string CustomerId) : IAuthorizableRequest<Customer>;

public class GetCustomerAuthorizor : AuthorizationValidatorBase<GetCustomer> {
	public GetCustomerAuthorizor() {
		this.HasAnyRole(
			ApplicationRoles.AppAdminRole,
			ApplicationRoles.AppManagerRole,
			ApplicationRoles.AppUserRole
		);
	}
}

public class GetCustomerValidator : AbstractValidator<GetCustomer> {
	public GetCustomerValidator() {
		this.RuleFor(x => x.CustomerId)
			.NotEmpty().WithMessage("Customer ID is required.");
	}
}