namespace Cirreum.Runtime.Security;

using System.Security.Claims;

internal sealed class ClientUser : UserStateBase {

	internal void SetEnrichmentCompleted() {
		this.EnrichmentComplete();
	}

	internal void SetAuthenticatedPrincipal(ClaimsPrincipal principal) {
		ArgumentNullException.ThrowIfNull(principal);
		this._principal = principal;

		if (this._principal.Identity is not ClaimsIdentity claimsIdentity) {
			throw new InvalidOperationException($"{nameof(principal)} Identity is null or not a ClaimsIdentity.");
		}
		this._identity = claimsIdentity;

		this._isAuthenticated = this._identity.IsAuthenticated;
		if (!this._isAuthenticated) {
			throw new InvalidOperationException("Cannot initialize from an unauthenticated user. Use SetAnonymous method.");
		}
		this._profile = new UserProfile(this._principal, TimeZoneInfo.Local.Id);
		if (!this.SessionStartTime.HasValue) {
			this.StartSession();
		}
	}

	internal void SetAnonymous() {
		this._isAuthenticated = false;
		this._principal = AnonymousUser.Shared;
		this._identity = AnonymousUser.Shared.Identity;
		this._profile = UserProfile.Anonymous;
		this.ClearApplicationUser();
		this.EndSession();
	}

	internal void SetAuthenticationLibrary(AuthenticationLibraryType authenticationLibraryType) {
		this._authenticationType = authenticationLibraryType;
	}

	internal void SetAppUser(IApplicationUser? applicationUser) {
		this.SetApplicationUser(applicationUser);
	}

}