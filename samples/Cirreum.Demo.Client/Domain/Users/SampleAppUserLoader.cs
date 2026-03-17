namespace Cirreum.Demo.Client.Domain.Users;

public class SampleAppUserLoader : IApplicationUserResolver {

	private static SampleAppUser? _instance;

	public Task<IApplicationUser?> ResolveAsync(
		string externalUserId,
		CancellationToken cancellationToken = default) {
		_instance ??= new SampleAppUser(Guid.NewGuid().ToString(), externalUserId, "Sample User", "sample@test.com");
		return Task.FromResult<IApplicationUser?>(_instance);
	}

}
