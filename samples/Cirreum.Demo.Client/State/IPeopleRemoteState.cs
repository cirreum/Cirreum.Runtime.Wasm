namespace Cirreum.Demo.Client.State;

public interface IPeopleRemoteState : IInitializableRemoteState {
	IReadOnlyList<PersonData> People { get; }
}