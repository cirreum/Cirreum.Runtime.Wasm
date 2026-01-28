namespace Cirreum.Components.Authorization;

using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

/// <summary>
/// A component that monitors DOM user activity and automatically extends session timeout.
/// Drop this component into App.razor to enable automatic session extension based on user interactions.
/// </summary>
public partial class SessionActivityMonitor : ComponentBase, IAsyncDisposable {

	[Inject] private IJSRuntime JS { get; set; } = default!;
	[Inject] private ISessionManager SessionManager { get; set; } = default!;
	[Inject] private ILogger<SessionActivityMonitor> Logger { get; set; } = default!;

	/// <summary>
	/// Gets or sets whether the activity monitor should automatically start once the
	/// session has started.
	/// Default is true.
	/// </summary>
	[Parameter] public bool AutoStart { get; set; } = true;

	/// <summary>
	/// Gets or sets the base throttling delay for completion events (keyup, pointerup) in milliseconds.
	/// This value may be adjusted based on the current session stage. Default is 1000ms.
	/// </summary>
	[Parameter] public int BaseCompletionThrottleMs { get; set; } = 1000;

	/// <summary>
	/// Gets or sets the base throttling delay for engagement events (scroll, pointermove) in milliseconds.
	/// This value may be adjusted based on the current session stage. Default is 2000ms.
	/// </summary>
	[Parameter] public int BaseEngagementThrottleMs { get; set; } = 2000;

	/// <summary>
	/// Gets or sets whether the monitor should automatically adjust throttling based on session stages.
	/// When true, uses the DomThrottleMultiplier from stage metadata. Default is true.
	/// </summary>
	[Parameter] public bool UseStageBasedThrottling { get; set; } = true;

	/// <summary>
	/// Gets or sets the minimum throttling delay in milliseconds, regardless of stage multipliers.
	/// This prevents stages from making throttling too aggressive. Default is 100ms.
	/// </summary>
	[Parameter] public int MinThrottleMs { get; set; } = 100;

	/// <summary>
	/// Gets or sets the maximum throttling delay in milliseconds, regardless of stage multipliers.
	/// This prevents stages from making throttling too relaxed. Default is 30000ms (30 seconds).
	/// </summary>
	[Parameter] public int MaxThrottleMs { get; set; } = 30000;

	/// <summary>
	/// Gets or sets whether to log activity monitor operations for debugging.
	/// Default is false to avoid log noise in production.
	/// </summary>
	[Parameter] public bool EnableLogging { get; set; } = false;

	/// <summary>
	/// Occurs when user activity is detected and session extension is triggered.
	/// </summary>
	[Parameter] public EventCallback OnActivityDetected { get; set; }

	/// <summary>
	/// Occurs when the activity monitor starts.
	/// </summary>
	[Parameter] public EventCallback OnMonitorStarted { get; set; }

	/// <summary>
	/// Occurs when the activity monitor stops.
	/// </summary>
	[Parameter] public EventCallback OnMonitorStopped { get; set; }

	private IJSObjectReference? _jsModule;
	private DotNetObjectReference<SessionActivityMonitor>? _dotNetRef;
	private bool _isStarted;

	protected override async Task OnInitializedAsync() {

		// Subscribe to session stage changes for automatic throttling adjustment
		if (this.UseStageBasedThrottling) {
			this.SessionManager.SessionStageChanged += this.OnSessionStageChanged;
		}

		// Subscribe to session events for automatic start/stop
		this.SessionManager.SessionStarted += this.OnSessionStarted;
		this.SessionManager.SessionExpired += this.OnSessionExpired;

		await base.OnInitializedAsync();
	}

	protected override async Task OnAfterRenderAsync(bool firstRender) {
		if (firstRender) {
			await this.InitializeJavaScript();
		}
	}

	/// <summary>
	/// Manually starts activity monitoring.
	/// </summary>
	public async Task StartMonitoring() {
		if (this._jsModule == null || this._isStarted) {
			return;
		}

		try {

			await this._jsModule.InvokeVoidAsync("ActivityMonitor.start");

			this._isStarted = true;

			if (this.EnableLogging && this.Logger.IsEnabled(LogLevel.Debug)) {
				this.Logger.LogDebug("Session activity monitoring started with completion throttle: {CompletionMs}ms, engagement throttle: {EngagementMs}ms",
					this._initialConfig.CompletionThrottleMs, this._initialConfig.EngagementThrottleMs);
			}

			await this.OnMonitorStarted.InvokeAsync();
		} catch (Exception ex) {
			this.Logger.LogError(ex, "Failed to start session activity monitoring");
		}
	}

	/// <summary>
	/// Manually stops activity monitoring.
	/// </summary>
	public async Task StopMonitoring() {
		if (this._jsModule == null || !this._isStarted) {
			return;
		}

		try {
			await this._jsModule.InvokeVoidAsync("ActivityMonitor.stop");
			this._isStarted = false;

			if (this.EnableLogging) {
				this.Logger.LogDebug("Session activity monitoring stopped");
			}

			await this.OnMonitorStopped.InvokeAsync();
		} catch (Exception ex) {
			this.Logger.LogError(ex, "Failed to stop session activity monitoring");
		}
	}

	/// <summary>
	/// Updates the throttling configuration manually.
	/// </summary>
	public async Task UpdateThrottling(int? completionThrottleMs = null, int? engagementThrottleMs = null) {
		if (this._jsModule == null) {
			return;
		}

		try {
			var config = new {
				completionThrottleMs = this.ApplyLimits(completionThrottleMs ?? this.BaseCompletionThrottleMs),
				engagementThrottleMs = this.ApplyLimits(engagementThrottleMs ?? this.BaseEngagementThrottleMs)
			};

			await this._jsModule.InvokeVoidAsync("ActivityMonitor.updateThrottling", config);

			if (this.EnableLogging && this.Logger.IsEnabled(LogLevel.Debug)) {
				this.Logger.LogDebug("Activity monitoring throttling updated: completion {CompletionMs}ms, engagement {EngagementMs}ms",
					config.completionThrottleMs, config.engagementThrottleMs);
			}
		} catch (Exception ex) {
			this.Logger.LogError(ex, "Failed to update activity monitoring throttling");
		}
	}

	/// <summary>
	/// Called by JavaScript when user activity is detected.
	/// </summary>
	[JSInvokable]
	public async Task RecordActivity() {
		try {

			this.SessionManager.ExtendSession();

			if (this.EnableLogging) {
				this.Logger.LogTrace("User activity detected, session extended");
			}

			if (this.OnActivityDetected.HasDelegate) {
				await this.OnActivityDetected.InvokeAsync();
			}

		} catch (Exception ex) {
			this.Logger.LogError(ex, "Failed to record user activity");
		}
	}

	private static readonly string ModuleUri = "./_content/Cirreum.Services.Wasm/sessionActivityMonitor.js";
	private async Task InitializeJavaScript() {
		try {
			this._dotNetRef = DotNetObjectReference.Create(this);
			this._jsModule = await this.JS.InvokeAsync<IJSObjectReference>("import", ModuleUri);
			this._initialConfig = this.CreateInitialConfig();
			await this._jsModule.InvokeVoidAsync("ActivityMonitor.init", this._dotNetRef, this._initialConfig);

		} catch (Exception ex) {
			this.Logger.LogError(ex, "Failed to initialize session activity monitor JavaScript module");
		}
	}

	private ThrottleConfig _initialConfig = new(0, 0);
	private record ThrottleConfig(int CompletionThrottleMs, int EngagementThrottleMs);
	private ThrottleConfig CreateInitialConfig() {
		var multiplier = this.GetCurrentStageMultiplier();

		return new ThrottleConfig(
			this.ApplyLimits((int)(this.BaseCompletionThrottleMs * multiplier)),
			this.ApplyLimits((int)(this.BaseEngagementThrottleMs * multiplier))
		);
	}

	private async void OnSessionStageChanged(SessionStage stage) {

		if (!this.UseStageBasedThrottling || !this._isStarted) {
			return;
		}

		try {
			var multiplier = stage.GetDomThrottleMultiplier(1);
			await this.UpdateThrottlingWithMultiplier(multiplier);

			if (this.EnableLogging && this.Logger.IsEnabled(LogLevel.Debug)) {
				this.Logger.LogDebug("Session stage changed to '{StageName}', throttling adjusted with multiplier: {Multiplier}",
					stage.Name, multiplier);
			}
		} catch (Exception ex) {
			this.Logger.LogError(ex, "Failed to adjust throttling for stage change to '{StageName}'", stage.Name);
		}
	}

	private async void OnSessionStarted() {
		// Ensure we've started...
		if (this.AutoStart && !this._isStarted) {
			await this.StartMonitoring();
		}
	}

	private async void OnSessionExpired() {
		if (this._isStarted) {
			await this.StopMonitoring();
		}
	}

	private async Task UpdateThrottlingWithMultiplier(double multiplier) {
		if (this._jsModule == null) {
			return;
		}

		var completionMs = this.ApplyLimits((int)(this.BaseCompletionThrottleMs * multiplier));
		var engagementMs = this.ApplyLimits((int)(this.BaseEngagementThrottleMs * multiplier));

		await this.UpdateThrottling(completionMs, engagementMs);
	}

	private double GetCurrentStageMultiplier() {
		if (!this.UseStageBasedThrottling) {
			return 1.0;
		}

		return this.SessionManager.CurrentStage?.GetDomThrottleMultiplier(1) ?? 1.0;
	}

	private int ApplyLimits(int value) {
		return Math.Max(this.MinThrottleMs, Math.Min(this.MaxThrottleMs, value));
	}

	public async ValueTask DisposeAsync() {
		try {
			// Unsubscribe from events
			if (this.UseStageBasedThrottling) {
				this.SessionManager.SessionStageChanged -= this.OnSessionStageChanged;
			}
			this.SessionManager.SessionStarted -= this.OnSessionStarted;
			this.SessionManager.SessionExpired -= this.OnSessionExpired;

			// Stop monitoring and cleanup JavaScript
			if (this._jsModule != null) {
				await this._jsModule.InvokeVoidAsync("ActivityMonitor.destroy");
				await this._jsModule.DisposeAsync();
			}

			this._dotNetRef?.Dispose();
		} catch (Exception ex) {
			this.Logger.LogError(ex, "Error during SessionActivityMonitor disposal");
		}
	}
}