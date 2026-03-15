namespace Cirreum.Demo.Client.State;

public interface ITestInit : IApplicationState, IInitializable {
}
public class TestInit : ITestInit {

	public string DisplayName => "Test Init";

	public string InitializationMessage => "Look, i'm initializing...";

	public int Order => 1000;

	public async Task InitializeAsync(Action<string> updateStatus, CancellationToken cancellationToken = default) {
		Console.WriteLine("TestInit - Begin");
		await Task.Delay(2000, cancellationToken);
		Console.WriteLine("TestInit - End");
	}

	public bool ShouldInitialize(IUserState userState) => true;

}
