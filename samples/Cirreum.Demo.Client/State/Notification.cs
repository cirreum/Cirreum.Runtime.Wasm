namespace Cirreum.Demo.Client.State;

public record Notification(
	string Id,
	string Title,
	string Message,
	NotificationType Type,
	DateTime Timestamp,
	bool IsRead = false,
	bool IsDismissed = false,  // Add this
	string? ActionUrl = null,
	string? ActionText = null
) {
	public static Notification Create(
		string title,
		string message,
		NotificationType type = NotificationType.Info,
		string? actionUrl = null,
		string? actionText = null) {
		return new Notification(
			Guid.NewGuid().ToString(),
			title,
			message,
			type,
			DateTime.UtcNow,
			false,
			false,  // Not dismissed initially
			actionUrl,
			actionText
		);
	}
}