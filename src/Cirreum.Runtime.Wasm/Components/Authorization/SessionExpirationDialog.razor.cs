namespace Cirreum.Components.Authorization;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;

/// <summary>
/// A UI component that displays session expiration dialogs and handles user responses.
/// Pairs with SessionActivityMonitor for complete session management.
/// </summary>
public partial class SessionExpirationDialog : ComponentBase, IDisposable {

	[Inject] private NavigationManager NavigationManager { get; set; } = default!;
	[Inject] private SessionOptions Options { get; set; } = default!;
	[Inject] private ISessionManager SessionManager { get; set; } = default!;
#if DEBUG
	[Inject] private IUserState CurrentUser { get; set; } = default!;
#endif

	/// <summary>
	/// Gets or sets the message displayed when the session expires.
	/// If not set, uses the configured SessionOptions.SessionTimeoutMessage.
	/// </summary>
	[Parameter] public string? CustomTimeoutMessage { get; set; }

	/// <summary>
	/// Gets or sets the text for the continue button. Default is "Continue".
	/// </summary>
	[Parameter] public string ContinueButtonText { get; set; } = "Continue";

	/// <summary>
	/// Gets or sets the text for the logout button. Default is "Logout".
	/// </summary>
	[Parameter] public string LogoutButtonText { get; set; } = "Logout";

	/// <summary>
	/// Gets or sets whether to show a countdown timer in the dialog.
	/// Default is false.
	/// </summary>
	[Parameter] public bool ShowCountdown { get; set; } = false;

	/// <summary>
	/// Gets or sets the auto-logout delay in seconds when ShowCountdown is enabled.
	/// Default is 60 seconds. Set to 0 to disable auto-logout.
	/// </summary>
	[Parameter] public int AutoLogoutSeconds { get; set; } = 60;

	/// <summary>
	/// Occurs when the user chooses to continue their session.
	/// </summary>
	[Parameter] public EventCallback OnSessionContinued { get; set; }

	/// <summary>
	/// Occurs when the user chooses to logout or auto-logout occurs.
	/// </summary>
	[Parameter] public EventCallback OnSessionEnded { get; set; }

	private static readonly DialogOptions DialogOptions = new() {
		Animation = DialogAnimation.FadeIn(.25),
		Modal = true,
		BackgroundCancel = false,
		HideCloseButton = true
	};

	private Dialog? _dialogRef;
	private string _displayMessage = "";
	private Timer? _countdownTimer;
	private int _remainingSeconds;

	protected override void OnInitialized() {
		this.SessionManager.SessionExpired += this.OnSessionExpired;
	}

	/// <summary>
	/// Manually shows the session expiration dialog.
	/// </summary>
	public async Task ShowDialog(string? customMessage = null) {
		this._displayMessage =
			customMessage
			?? this.CustomTimeoutMessage
			?? this.Options.SessionTimeoutMessage;

		if (this._dialogRef != null) {
			await this._dialogRef.ShowAsync();

			if (this.ShowCountdown && this.AutoLogoutSeconds > 0) {
				this.StartCountdown();
			}
		}
	}

	/// <summary>
	/// Hides the session expiration dialog.
	/// </summary>
	public async Task HideDialog() {
		this.StopCountdown();

		if (this._dialogRef != null) {
			await this._dialogRef.HideAsync();
		}
	}

	private async void OnSessionExpired() {
		await this.ShowDialog();
	}

	private async Task HandleContinue() {
#if DEBUG
		Console.WriteLine($"[DIALOG] Before continue - Time remaining: {this.SessionManager.TimeRemaining}");
		Console.WriteLine($"[DIALOG] Current stage: {this.SessionManager.CurrentStage?.Name}");
		Console.WriteLine($"[DIALOG] User authenticated: {this.CurrentUser?.IsAuthenticated}");
#endif
		await this.HideDialog();

#if DEBUG
		Console.WriteLine("[DIALOG] Calling ExtendSession...");
#endif
		this.SessionManager.ExtendSession();

#if DEBUG
		Console.WriteLine($"[DIALOG] After ExtendSession - Time remaining: {this.SessionManager.TimeRemaining}");
		Console.WriteLine($"[DIALOG] New current stage: {this.SessionManager.CurrentStage?.Name}");
#endif

		await this.OnSessionContinued.InvokeAsync();
	}

	private async Task HandleLogout() {
		await this.HideDialog();
		await this.OnSessionEnded.InvokeAsync();
		var logoutUrl = this.Options.LogoutUrl;
		if (logoutUrl.IsEmpty()) {
			logoutUrl = SessionOptions.DefaultLogoutUrl;
		}
		this.NavigationManager.NavigateToLogout(logoutUrl, this.NavigationManager.BaseUri);
	}

	private void StartCountdown() {
		this._remainingSeconds = this.AutoLogoutSeconds;
		this._countdownTimer = new Timer(this.OnCountdownTick, null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
	}

	private void StopCountdown() {
		this._countdownTimer?.Dispose();
		this._countdownTimer = null;
	}

	private async void OnCountdownTick(object? state) {
		this._remainingSeconds--;

		await this.InvokeAsync(this.StateHasChanged);

		if (this._remainingSeconds <= 0) {
			this.StopCountdown();
			await this.InvokeAsync(this.HandleLogout);
		}
	}

	public void Dispose() {
		this.SessionManager.SessionExpired -= this.OnSessionExpired;
		this.StopCountdown();
	}

}