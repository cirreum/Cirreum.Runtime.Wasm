namespace Cirreum.Runtime.Security;

using Microsoft.AspNetCore.Components.Authorization;

sealed class NoAuthStateProvider : AuthenticationStateProvider {

	private readonly Task<AuthenticationState> _anonymousState;

	public NoAuthStateProvider() {
		// Create an anonymous user
		_anonymousState = Task.FromResult(new AuthenticationState(AnonymousUser.Shared));
	}

	public override Task<AuthenticationState> GetAuthenticationStateAsync() {
		return _anonymousState;
	}
}