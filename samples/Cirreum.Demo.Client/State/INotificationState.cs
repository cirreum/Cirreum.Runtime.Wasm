namespace Cirreum.Demo.Client.State;

public interface INotificationState : IScopedNotificationState {

	IReadOnlyList<Notification> Notifications { get; }
	int UnreadCount { get; }

	void AddNotification(Notification notification);
	void MarkAsRead(string notificationId);
	void MarkAllAsRead();
	void RemoveNotification(string notificationId);
	void ClearAll();
	void Refresh();
}