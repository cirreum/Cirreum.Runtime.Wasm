namespace Cirreum.Components.Authorization;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;

public class AuthenticationStateInitializer : ComponentBase {

	[CascadingParameter]
	private Task<AuthenticationState>? AuthenticationState { get; set; }

	protected override async Task OnParametersSetAsync() {
		if (this.AuthenticationState != null) {
			await this.AuthenticationState; // triggers GetAuthenticatedUser() → CreateUserAsync()
		}
	}

}