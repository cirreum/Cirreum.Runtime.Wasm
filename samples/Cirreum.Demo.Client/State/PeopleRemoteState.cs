namespace Cirreum.Demo.Client.State;

using System.Net.Http.Json;

public class PeopleRemoteState(
	IStateManager stateManager,
	IHttpClientFactory httpClientFactory
) : RemoteState, IPeopleRemoteState {

	public IReadOnlyList<PersonData> People { get; private set; } = [];

	public string DisplayName => "People Data";

	public string InitializationMessage => "Loading fake people data...";

	public int Order => 0;

	public Task InitializeAsync(
		Action<string> updateStatus,
		CancellationToken cancellationToken = default) {
		return this.LoadAsync(cancellationToken);
	}

	public bool ShouldInitialize(IUserState userState) => true;

	protected override async Task LoadCoreAsync(CancellationToken cancellationToken) {
		var client = httpClientFactory.CreateClient("FakeData");
		var result = await client.GetFromJsonAsync<IEnumerable<PersonData>>("FXImuy6I7yBj/data", cancellationToken: cancellationToken);
		if (result is null) {
			return;
		}
		this.People = [.. result];
	}

	protected override void OnStateHasChanged() {
		stateManager.NotifySubscribers(this);
	}

}