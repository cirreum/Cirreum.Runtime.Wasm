namespace Cirreum.Demo.Client.Domain.Users;

public record SampleAppUser(
	string Id,
	string ExternalId,
	string DisplayName,
	string Email
) : IApplicationUser;