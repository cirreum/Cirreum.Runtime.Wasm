namespace Cirreum.Demo.Client.Domain.Users;

using System.Threading;
using System.Threading.Tasks;

public class SampleAppUserLoader : IApplicationUserFactory {

	private static SampleAppUser? _instance;

	public Task<Result<IApplicationUser>> CreateUserAsync(
		IUserState userState,
		CancellationToken cancellationToken = default) {
		_instance ??= new SampleAppUser(Guid.NewGuid().ToString(), userState.Id, "Sample User", "sample@test.com");
		return Task.FromResult(Result<IApplicationUser>.Success(_instance));
	}

}