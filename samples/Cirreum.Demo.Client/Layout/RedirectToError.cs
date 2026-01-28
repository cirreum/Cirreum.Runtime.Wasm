namespace Cirreum.Demo.Client.Layout;

using Microsoft.AspNetCore.Components;

public class RedirectToError(
	NavigationManager navigationManager,
	IJSAppModule jsApp
) : ComponentBase {

	// Static circuit breaker state (shared across all instances)
	private static bool _circuitOpen = false;
	private static DateTimeOffset _circuitResetTime = DateTimeOffset.MinValue;
	private static readonly TimeSpan _resetTimeout = TimeSpan.FromMinutes(5);
	private static int _failureCount = 0;
	private static readonly int _failureThreshold = 3;

	[Parameter, EditorRequired]
	public required Exception Exception { get; set; }

	protected override async Task OnInitializedAsync() {
		// Generate a unique error ID for reference
		var errorId = Guid.NewGuid().ToString("N");

		if (this.Exception != null) {

			// Check if circuit is open (preventing AppInsights calls)
			if (ShouldTryLogging()) {
				try {

					var props = new Dictionary<string, object?> {
						{ "Source", this.Exception.Source },
						{ "CurrentRoute", navigationManager.ToBaseRelativePath(navigationManager.Uri) },
						{ "ViewportWidth", jsApp.GetViewportDimensions().Width }
					};

					// Success - reset failure count
					ResetCircuitBreaker();

				} catch (Exception loggingException) {

					// Record a failure
					RecordFailure();

					// Only try the second attempt if the circuit is still closed
					if (ShouldTryLogging()) {
						try {

							// Create an AggregateException that includes both the original and logging exceptions
							var aggregateException = new AggregateException(
								"Error occurred while logging the original exception",
								[loggingException, this.Exception]
							);

							// Success on second attempt - reset failure count
							ResetCircuitBreaker();

						} catch (Exception fallbackException) {

							// Record a second failure
							RecordFailure();

							// Console logging as last resort
							this.LogToConsole(errorId, this.Exception, loggingException, fallbackException);

						}
					} else {
						// Circuit already open after first failure, log to console
						this.LogToConsole(errorId, this.Exception, loggingException);
					}
				}
			} else {
				// Circuit is open, bypass Application Insights completely
				this.LogToConsole(
					errorId,
					this.Exception,
					new Exception("Circuit breaker open - bypassing Application Insights"));
			}
		}

		// Navigate to error page with reference ID regardless of logging success
		navigationManager.NavigateTo($"/error?id={errorId}", true);

	}

	private static bool ShouldTryLogging() {
		// If circuit is open, check if we should try to reset
		if (_circuitOpen) {
			if (DateTimeOffset.UtcNow >= _circuitResetTime) {
				// Try a reset - move to half-open state by allowing the next attempt
				_circuitOpen = false;
				return true;
			}
			return false; // Circuit still open
		}
		return true; // Circuit closed, allow the attempt
	}

	private static void RecordFailure() {
		_failureCount++;
		if (_failureCount >= _failureThreshold) {
			// Open the circuit
			_circuitOpen = true;
			_circuitResetTime = DateTimeOffset.UtcNow.Add(_resetTimeout);
			Console.WriteLine($"Circuit breaker opened until {_circuitResetTime} UTC after {_failureCount} consecutive failures");
		}
	}

	private static void ResetCircuitBreaker() {
		_failureCount = 0;
		_circuitOpen = false;
	}

	private void LogToConsole(string errorId, params Exception[] exceptions) {
		var timestamp = DateTimeOffset.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fff");
		Console.WriteLine($"[{timestamp}] Error ID: {errorId}");

		for (var i = 0; i < exceptions.Length; i++) {
			var exception = exceptions[i];
			if (exception != null) {
				var type = i switch {
					0 => "Original",
					1 => "Logging",
					2 => "Fallback",
					_ => $"Additional-{i}"
				};

				Console.WriteLine($"[{timestamp}] {type} Exception: {exception.GetType().Name}: {exception.Message}");
				Console.WriteLine($"[{timestamp}] Stack: {exception.StackTrace}");
			}
		}

		Console.WriteLine($"[{timestamp}] Current route: {navigationManager.Uri}");
		Console.WriteLine("---------------------------------------------");
	}

}