namespace Cirreum.Components.ViewModels;

/// <summary>
/// A convenient base View Model with dynamic bindable properties that are persisted with a
/// <see cref="IPersistableStateContainer"/> service, and includes type-safe access, UI notifications and validation.
/// </summary>
/// <typeparam name="TViewModel">The concrete implementation of the view model.</typeparam>
/// <typeparam name="TState">The type of state storage service.</typeparam>
public abstract class PersistentStateViewModel<TViewModel, TState>(
	TState state,
	string scope
) : StateViewModel<TViewModel, TState>(state, scope)
	where TViewModel : PersistentStateViewModel<TViewModel, TState>
	where TState : IPersistableStateContainer;