namespace Cirreum.Components.ViewModels;

/// <summary>
/// A convenient base View Model with dynamic bindable properties that are backed by the
/// <typeparamref name="TState"/> storage service, including type-safe access, UI notifications
/// and validation.
/// </summary>
/// <typeparam name="TViewModel">The concrete implementation of the view model.</typeparam>
/// <typeparam name="TState">The type of state storage service.</typeparam>
/// <remarks>
/// Constructs a new view model instance.
/// </remarks>
/// <param name="state">The state storage service.</param>
/// <param name="scope">The name in which the data will be scoped to.</param>
public abstract class MemoryStateViewModel<TViewModel, TState>(
	TState state,
	string scope
) : StateViewModel<TViewModel, TState>(state, scope)
	where TViewModel : MemoryStateViewModel<TViewModel, TState>
	where TState : IMemoryState {
}