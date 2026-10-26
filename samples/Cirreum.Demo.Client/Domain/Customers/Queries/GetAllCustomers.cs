namespace Cirreum.Demo.Client.Domain.Customers.Queries;

public record class GetAllCustomers() : IAuthorizableOperation<IEnumerable<Customer>>;

public class GetAllCustomersAuthorizor : AuthorizerBase<GetAllCustomers> {
	public GetAllCustomersAuthorizor() {
		this.HasAnyRole(
			ApplicationRoles.AppAdminRole,
			ApplicationRoles.AppManagerRole,
			ApplicationRoles.AppUserRole
		);
	}
}