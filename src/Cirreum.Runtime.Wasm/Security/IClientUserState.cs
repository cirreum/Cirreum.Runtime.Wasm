namespace Cirreum.Runtime.Security;

/// <summary>
/// WASM-specific user state contract that participates in async state notification.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="IClientUserState"/> extends <see cref="IUserState"/> with
/// <see cref="IAsyncApplicationState"/>, enabling async subscriber registration and
/// notification via <see cref="IStateManager.SubscribeAsync{TState}(Func{Task})"/> and
/// <see cref="IStateManager.NotifySubscribersAsync{TState}(CancellationToken)"/>.
/// </para>
/// <para>
/// <strong>Why a separate interface from IUserState:</strong>
/// </para>
/// <para>
/// <see cref="IUserState"/> is shared across all three Cirreum hosting environments —
/// WASM, Server, and Serverless. In Server and Serverless environments, user state is
/// resolved per-request via <see cref="IUserStateAccessor"/> with no notification
/// concern. Marking <see cref="IUserState"/> as <see cref="IAsyncApplicationState"/>
/// would be a misleading contract for those environments.
/// </para>
/// <para>
/// <see cref="IClientUserState"/> scopes the async notification contract precisely
/// to the WASM environment where it is meaningful. It is not referenced by Server
/// or Serverless packages.
/// </para>
/// <para>
/// <strong>Usage pattern in WASM:</strong>
/// </para>
/// <list type="bullet">
///   <item>
///     <strong>Read user data</strong> — resolve <see cref="IUserState"/> (all environments)
///   </item>
///   <item>
///     <strong>Subscribe to auth changes</strong> — use <see cref="IClientUserState"/> (WASM only)
///   </item>
///   <item>
///     <strong>Notify subscribers</strong> — call
///     <c>stateManager.NotifySubscribersAsync&lt;IClientUserState&gt;(clientUser)</c>
///   </item>
///   <item>
///     <strong>Mutate internal state</strong> — cast to <c>ClientUser</c> (WASM runtime internals only)
///   </item>
/// </list>
/// <para>
/// <strong>DI Registration:</strong>
/// </para>
/// <para>
/// A single <c>ClientUser</c> instance satisfies both <see cref="IUserState"/> and
/// <see cref="IClientUserState"/>. Register both interfaces pointing to the same scoped instance:
/// </para>
/// <code>
/// services.AddScoped&lt;ClientUser&gt;();
/// services.AddScoped&lt;IUserState&gt;(sp => sp.GetRequiredService&lt;ClientUser&gt;());
/// services.AddScoped&lt;IClientUserState&gt;(sp => sp.GetRequiredService&lt;ClientUser&gt;());
/// </code>
/// </remarks>
/// <example>
/// <code>
/// // Subscribe to authentication state changes in WASM
/// this._subscription = stateManager.SubscribeAsync&lt;IClientUserState&gt;(async state =>
/// {
///     if (state.IsAuthenticated)
///     {
///         await storage.SetAsync("lastUserId", state.Id);
///         navigation.NavigateTo(state.IsNewUser ? Routes.Onboard : Routes.Dashboard);
///     }
/// });
///
/// // Notify async subscribers after authentication completes
/// await stateManager.NotifySubscribersAsync&lt;IClientUserState&gt;(clientUser);
/// </code>
/// </example>
public interface IClientUserState : IUserState, IAsyncApplicationState;