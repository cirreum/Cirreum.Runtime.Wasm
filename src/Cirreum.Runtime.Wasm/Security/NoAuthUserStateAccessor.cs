namespace Cirreum.Runtime.Security;

sealed class NoAuthUserStateAccessor : IUserStateAccessor {
	private static readonly IUserState AnonymousUserState = new ClientUser();
	private static readonly ValueTask<IUserState> AnonymousUserValueTask = new ValueTask<IUserState>(AnonymousUserState);
	public ValueTask<IUserState> GetUser() => AnonymousUserValueTask;
}