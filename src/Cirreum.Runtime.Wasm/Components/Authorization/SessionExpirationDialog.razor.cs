namespace Cirreum.Components.Authorization;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;

/// <summary>
/// A UI component that displays session expiration dialogs and handles user responses.
/// Pairs with <see cref="SessionActivityMonitor"/> for complete session management.
/// </summary>
/// <remarks>
/// Subscribe to <see cref="OnSessionContinued"/> and <see cref="OnSessionEnded"/> to
/// respond to user choices. Drop into App.razor alongside
/// <see cref="SessionActivityMonitor"/>.
/// </remarks>
public partial class SessionExpirationDialog : ComponentBase, IDisposable {

	[Inject] private NavigationManager NavigationManager { get; set; } = default!;
	[Inject] private SessionOptions Options { get; set; } = default!;
	[Inject] private ISessionManager SessionManager { get; set; } = default!;

	/// <summary>
	/// Gets or sets the message displayed when the session expires.
	/// Falls back to <see cref="SessionOptions.SessionTimeoutMessage"/> if not set.
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
	/// Default is <see langword="false"/>.
	/// </summary>
	[Parameter] public bool ShowCountdown { get; set; } = false;

	/// <summary>
	/// Gets or sets the auto-logout delay in seconds when <see cref="ShowCountdown"/>
	/// is enabled. Default is 60 seconds. Set to 0 to disable auto-logout.
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

	// -------------------------------------------------------------------------
	// Lifecycle
	// -------------------------------------------------------------------------

	protected override void OnInitialized() {
		this.SessionManager.SessionExpired += this.OnSessionExpired;
	}

	public void Dispose() {
		this.SessionManager.SessionExpired -= this.OnSessionExpired;
		this.StopCountdown();
	}

	// -------------------------------------------------------------------------
	// Public API
	// -------------------------------------------------------------------------

	/// <summary>
	/// Shows the session expiration dialog using the configured timeout message.
	/// </summary>
	public Task ShowDialog() => this.ShowDialog(null);

	/// <summary>
	/// Manually shows the session expiration dialog.
	/// </summary>
	/// <param name="customMessage">
	/// Optional message to display. Falls back to <see cref="CustomTimeoutMessage"/>
	/// then <see cref="SessionOptions.SessionTimeoutMessage"/>.
	/// </param>
	public async Task ShowDialog(string? customMessage = null) {
		this._displayMessage =
			customMessage
			?? this.CustomTimeoutMessage
			?? this.Options.SessionTimeoutMessage;

		if (this._dialogRef is not null) {
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
		if (this._dialogRef is not null) {
			await this._dialogRef.HideAsync();
		}
	}

	// -------------------------------------------------------------------------
	// Session Event Handler
	// Uses InvokeAsync to marshal back to the Blazor sync context from the
	// timer-fired sync Action event — avoids async void while staying on circuit.
	// -------------------------------------------------------------------------

	private void OnSessionExpired() {
		_ = this.InvokeAsync(this.ShowDialog);
	}

	// -------------------------------------------------------------------------
	// Button Handlers
	// -------------------------------------------------------------------------

	private async Task HandleContinue() {
		await this.HideDialog();
		this.SessionManager.ExtendSession();
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

	// -------------------------------------------------------------------------
	// Countdown
	// -------------------------------------------------------------------------

	private void StartCountdown() {
		this._remainingSeconds = this.AutoLogoutSeconds;
		this._countdownTimer = new Timer(this.OnCountdownTick, null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
	}

	private void StopCountdown() {
		this._countdownTimer?.Dispose();
		this._countdownTimer = null;
	}

	// Uses InvokeAsync to marshal Timer thread-pool callback onto the Blazor
	// sync context. Avoids async void while keeping StateHasChanged and
	// HandleLogout on the correct context.
	private void OnCountdownTick(object? state) {
		_ = this.InvokeAsync(async () => {
			this._remainingSeconds--;
			this.StateHasChanged();
			if (this._remainingSeconds <= 0) {
				this.StopCountdown();
				await this.HandleLogout();
			}
		});
	}

}