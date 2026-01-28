namespace Cirreum.Demo.Client.Domain.Users;

using System.Threading;
using System.Threading.Tasks;

public class SampleAppUserLoader : IApplicationUserLoader<SampleAppUser> {

	private static SampleAppUser? _instance;

	public Task<Result<SampleAppUser>> TryLoadUserAsync(IServiceProvider _, string identityId, CancellationToken cancellationToken) {
		_instance ??= new SampleAppUser(Guid.NewGuid().ToString(), identityId, "Sample User", "sample@test.com");
		return Task.FromResult(Result<SampleAppUser>.Success(_instance));
	}

}