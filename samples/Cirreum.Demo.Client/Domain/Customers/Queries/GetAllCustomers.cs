namespace Cirreum.Demo.Client.Domain.Customers.Queries;

public record class GetAllCustomers() : IAuthorizableRequest<IEnumerable<Customer>>;

public class GetAllCustomersAuthorizor : AuthorizationValidatorBase<GetAllCustomers> {
	public GetAllCustomersAuthorizor() {
		this.HasAnyRole(
			ApplicationRoles.AppAdminRole,
			ApplicationRoles.AppManagerRole,
			ApplicationRoles.AppUserRole
		);
	}
}