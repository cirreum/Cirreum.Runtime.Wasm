namespace Cirreum.Components.Authorization;

using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

/// <summary>
/// A component that monitors DOM user activity and automatically extends session timeout.
/// Drop this component into App.razor to enable automatic session extension based on
/// user interactions.
/// </summary>
/// <remarks>
/// <para>
/// Subscribes to <see cref="ISessionManager"/> events to automatically start and stop
/// monitoring in response to session lifecycle changes. Delegates activity detection to
/// a JavaScript module that throttles DOM events and calls back via
/// <see cref="RecordActivity"/>.
/// </para>
/// <para>
/// Session stage changes optionally adjust throttling multipliers to relax monitoring
/// during safe zones and tighten it during watch zones.
/// </para>
/// </remarks>
public partial class SessionActivityMonitor : ComponentBase, IAsyncDisposable {

	[Inject] private IJSRuntime JS { get; set; } = default!;
	[Inject] private ISessionManager SessionManager { get; set; } = default!;
	[Inject] private ILogger<SessionActivityMonitor> Logger { get; set; } = default!;

	/// <summary>
	/// Gets or sets whether the activity monitor should automatically start once the
	/// session has started. Default is <see langword="true"/>.
	/// </summary>
	[Parameter] public bool AutoStart { get; set; } = true;

	/// <summary>
	/// Gets or sets the base throttling delay for completion events (keyup, pointerup)
	/// in milliseconds. Default is 1000ms.
	/// </summary>
	[Parameter] public int BaseCompletionThrottleMs { get; set; } = 1000;

	/// <summary>
	/// Gets or sets the base throttling delay for engagement events (scroll, pointermove)
	/// in milliseconds. Default is 2000ms.
	/// </summary>
	[Parameter] public int BaseEngagementThrottleMs { get; set; } = 2000;

	/// <summary>
	/// Gets or sets whether the monitor should automatically adjust throttling based on
	/// session stage multipliers. Default is <see langword="true"/>.
	/// </summary>
	[Parameter] public bool UseStageBasedThrottling { get; set; } = true;

	/// <summary>
	/// Gets or sets the minimum throttling delay in milliseconds, regardless of stage
	/// multipliers. Default is 100ms.
	/// </summary>
	[Parameter] public int MinThrottleMs { get; set; } = 100;

	/// <summary>
	/// Gets or sets the maximum throttling delay in milliseconds, regardless of stage
	/// multipliers. Default is 30000ms (30 seconds).
	/// </summary>
	[Parameter] public int MaxThrottleMs { get; set; } = 30000;

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
	private ThrottleConfig _initialConfig = new(0, 0);
	private bool _isStarted;

	// -------------------------------------------------------------------------
	// Lifecycle
	// -------------------------------------------------------------------------

	protected override async Task OnInitializedAsync() {
		if (this.UseStageBasedThrottling) {
			this.SessionManager.SessionStageChanged += this.OnSessionStageChanged;
		}
		this.SessionManager.SessionStarted += this.OnSessionStarted;
		this.SessionManager.SessionExpired += this.OnSessionExpired;
		await base.OnInitializedAsync();
	}

	protected override async Task OnAfterRenderAsync(bool firstRender) {
		if (firstRender) {
			await this.InitializeJavaScript();
		}
	}

	// -------------------------------------------------------------------------
	// Public API
	// -------------------------------------------------------------------------

	/// <summary>
	/// Manually starts activity monitoring.
	/// </summary>
	public async Task StartMonitoring() {
		if (this._jsModule is null || this._isStarted) {
			return;
		}
		try {
			await this._jsModule.InvokeVoidAsync("ActivityMonitor.start");
			this._isStarted = true;
			Log.MonitorStarted(this.Logger, this._initialConfig.CompletionThrottleMs, this._initialConfig.EngagementThrottleMs);
			await this.OnMonitorStarted.InvokeAsync();
		} catch (Exception ex) {
			Log.StartFailed(this.Logger, ex);
		}
	}

	/// <summary>
	/// Manually stops activity monitoring.
	/// </summary>
	public async Task StopMonitoring() {
		if (this._jsModule is null || !this._isStarted) {
			return;
		}
		try {
			await this._jsModule.InvokeVoidAsync("ActivityMonitor.stop");
			this._isStarted = false;
			Log.MonitorStopped(this.Logger);
			await this.OnMonitorStopped.InvokeAsync();
		} catch (Exception ex) {
			Log.StopFailed(this.Logger, ex);
		}
	}

	/// <summary>
	/// Updates the throttling configuration.
	/// </summary>
	public async Task UpdateThrottling(int? completionThrottleMs = null, int? engagementThrottleMs = null) {
		if (this._jsModule is null) {
			return;
		}
		try {
			var config = new {
				completionThrottleMs = this.ApplyLimits(completionThrottleMs ?? this.BaseCompletionThrottleMs),
				engagementThrottleMs = this.ApplyLimits(engagementThrottleMs ?? this.BaseEngagementThrottleMs)
			};
			await this._jsModule.InvokeVoidAsync("ActivityMonitor.updateThrottling", config);
			Log.ThrottlingUpdated(this.Logger, config.completionThrottleMs, config.engagementThrottleMs);
		} catch (Exception ex) {
			Log.ThrottlingUpdateFailed(this.Logger, ex);
		}
	}

	/// <summary>
	/// Called by JavaScript when user activity is detected.
	/// </summary>
	[JSInvokable]
	public async Task RecordActivity() {
		try {
			this.SessionManager.ExtendSession();
			Log.ActivityDetected(this.Logger);
			if (this.OnActivityDetected.HasDelegate) {
				await this.OnActivityDetected.InvokeAsync();
			}
		} catch (Exception ex) {
			Log.ActivityRecordFailed(this.Logger, ex);
		}
	}

	// -------------------------------------------------------------------------
	// Session Event Handlers
	// Uses InvokeAsync to marshal back to the Blazor sync context from the
	// timer-fired sync Action event — avoids async void while staying on circuit.
	// -------------------------------------------------------------------------

	private void OnSessionStarted() {
		if (this.AutoStart && !this._isStarted) {
			_ = this.InvokeAsync(this.StartMonitoring);
		}
	}

	private void OnSessionExpired() {
		if (this._isStarted) {
			_ = this.InvokeAsync(this.StopMonitoring);
		}
	}

	private void OnSessionStageChanged(SessionStage stage) {
		if (!this.UseStageBasedThrottling || !this._isStarted) {
			return;
		}
		_ = this.InvokeAsync(async () => {
			try {
				var multiplier = stage.GetDomThrottleMultiplier(1);
				await this.UpdateThrottlingWithMultiplier(multiplier);
				Log.StageThrottlingAdjusted(this.Logger, stage.Name, multiplier);
			} catch (Exception ex) {
				Log.StageThrottlingFailed(this.Logger, ex, stage.Name);
			}
		});
	}

	// -------------------------------------------------------------------------
	// Initialization
	// -------------------------------------------------------------------------

	private static readonly string ModuleUri = "./_content/Cirreum.Services.Wasm/sessionActivityMonitor.js";

	private async Task InitializeJavaScript() {
		try {
			this._dotNetRef = DotNetObjectReference.Create(this);
			this._jsModule = await this.JS.InvokeAsync<IJSObjectReference>("import", ModuleUri);
			this._initialConfig = this.CreateInitialConfig();
			await this._jsModule.InvokeVoidAsync("ActivityMonitor.init", this._dotNetRef, this._initialConfig);
		} catch (Exception ex) {
			Log.InitFailed(this.Logger, ex);
		}
	}

	// -------------------------------------------------------------------------
	// Helpers
	// -------------------------------------------------------------------------

	private record ThrottleConfig(int CompletionThrottleMs, int EngagementThrottleMs);

	private ThrottleConfig CreateInitialConfig() {
		var multiplier = this.GetCurrentStageMultiplier();
		return new ThrottleConfig(
			this.ApplyLimits((int)(this.BaseCompletionThrottleMs * multiplier)),
			this.ApplyLimits((int)(this.BaseEngagementThrottleMs * multiplier))
		);
	}

	private async Task UpdateThrottlingWithMultiplier(double multiplier) {
		if (this._jsModule is null) {
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

	private int ApplyLimits(int value) =>
		Math.Max(this.MinThrottleMs, Math.Min(this.MaxThrottleMs, value));

	// -------------------------------------------------------------------------
	// Disposal
	// -------------------------------------------------------------------------

	public async ValueTask DisposeAsync() {
		try {
			if (this.UseStageBasedThrottling) {
				this.SessionManager.SessionStageChanged -= this.OnSessionStageChanged;
			}
			this.SessionManager.SessionStarted -= this.OnSessionStarted;
			this.SessionManager.SessionExpired -= this.OnSessionExpired;

			if (this._jsModule is not null) {
				await this._jsModule.InvokeVoidAsync("ActivityMonitor.destroy");
				await this._jsModule.DisposeAsync();
			}

			this._dotNetRef?.Dispose();
		} catch (Exception ex) {
			Log.DisposalFailed(this.Logger, ex);
		}
	}

	// -------------------------------------------------------------------------
	// Logging
	// -------------------------------------------------------------------------

	private static partial class Log {
		[LoggerMessage(Level = LogLevel.Debug, Message = "Session activity monitoring started — completion throttle: {CompletionMs}ms, engagement throttle: {EngagementMs}ms")]
		internal static partial void MonitorStarted(ILogger logger, int completionMs, int engagementMs);

		[LoggerMessage(Level = LogLevel.Error, Message = "Failed to start session activity monitoring.")]
		internal static partial void StartFailed(ILogger logger, Exception ex);

		[LoggerMessage(Level = LogLevel.Debug, Message = "Session activity monitoring stopped.")]
		internal static partial void MonitorStopped(ILogger logger);

		[LoggerMessage(Level = LogLevel.Error, Message = "Failed to stop session activity monitoring.")]
		internal static partial void StopFailed(ILogger logger, Exception ex);

		[LoggerMessage(Level = LogLevel.Debug, Message = "Activity monitoring throttling updated — completion: {CompletionMs}ms, engagement: {EngagementMs}ms")]
		internal static partial void ThrottlingUpdated(ILogger logger, int completionMs, int engagementMs);

		[LoggerMessage(Level = LogLevel.Error, Message = "Failed to update activity monitoring throttling.")]
		internal static partial void ThrottlingUpdateFailed(ILogger logger, Exception ex);

		[LoggerMessage(Level = LogLevel.Trace, Message = "User activity detected, session extended.")]
		internal static partial void ActivityDetected(ILogger logger);

		[LoggerMessage(Level = LogLevel.Error, Message = "Failed to record user activity.")]
		internal static partial void ActivityRecordFailed(ILogger logger, Exception ex);

		[LoggerMessage(Level = LogLevel.Debug, Message = "Session stage changed to '{StageName}', throttling adjusted with multiplier: {Multiplier}")]
		internal static partial void StageThrottlingAdjusted(ILogger logger, string stageName, double multiplier);

		[LoggerMessage(Level = LogLevel.Error, Message = "Failed to adjust throttling for stage change to '{StageName}'.")]
		internal static partial void StageThrottlingFailed(ILogger logger, Exception ex, string stageName);

		[LoggerMessage(Level = LogLevel.Error, Message = "Failed to initialize session activity monitor JavaScript module.")]
		internal static partial void InitFailed(ILogger logger, Exception ex);

		[LoggerMessage(Level = LogLevel.Error, Message = "Error during SessionActivityMonitor disposal.")]
		internal static partial void DisposalFailed(ILogger logger, Exception ex);
	}

}