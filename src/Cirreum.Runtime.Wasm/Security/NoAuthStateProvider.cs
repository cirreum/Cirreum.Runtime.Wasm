namespace Cirreum.Runtime.Security;

using Microsoft.AspNetCore.Components.Authorization;

sealed class NoAuthStateProvider : AuthenticationStateProvider {

	private static readonly Task<AuthenticationState> _anonymousState =
		Task.FromResult(new AuthenticationState(AnonymousUser.Shared));

	public NoAuthStateProvider(ClientUser clientUser) {
		clientUser.SetAnonymous();
	}

	public override Task<AuthenticationState> GetAuthenticationStateAsync() {
		return _anonymousState;
	}

}