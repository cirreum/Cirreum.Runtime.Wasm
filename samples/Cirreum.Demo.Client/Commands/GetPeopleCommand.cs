namespace Cirreum.Demo.Client.Commands;

using Cirreum.Demo.Client.Authorization;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;

[BusinessHoursOnly]
public record GetPeopleCommand(int Delay = -1) : IAuthorizableRequest<PersonData[]>;

public class GetPeopleAuthorizor : AuthorizationValidatorBase<GetPeopleCommand> {

	public GetPeopleAuthorizor() {
		this.HasRole(ApplicationRoles.AppUserRole);
	}

}

public class GetPeopleCommandHandler(IHttpClientFactory httpClientFactory) : IRequestHandler<GetPeopleCommand, PersonData[]> {

	public async Task<Result<PersonData[]>> HandleAsync(GetPeopleCommand request, CancellationToken token) {
		var client = httpClientFactory.CreateClient("FakeData");
		try {
			if (request.Delay > 0) {
				await Task.Delay(request.Delay, token);
			}
			return (await client.GetFromJsonAsync<PersonData[]>("FXImuy6I7yBj/data", token)) ?? [];
		} catch {
			return Result<PersonData[]>.Success([]);
		}
	}

}