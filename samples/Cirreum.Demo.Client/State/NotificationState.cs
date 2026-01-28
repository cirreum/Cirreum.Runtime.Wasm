namespace Cirreum.Demo.Client.State;

public class NotificationState(
	IStateManager stateManager
) : ScopedNotificationState, INotificationState {

	private readonly List<Notification> _notifications = [];

	// Only show non-dismissed notifications
	public IReadOnlyList<Notification> Notifications =>
		_notifications.Where(n => !n.IsDismissed).ToList().AsReadOnly();

	// Count unread, non-dismissed notifications
	public int UnreadCount => _notifications.Count(n => !n.IsRead && !n.IsDismissed);

	public void AddNotification(Notification notification) {
		_notifications.Insert(0, notification);
		this.NotifyStateChanged();
	}

	public void MarkAsRead(string notificationId) {
		var notification = _notifications.FirstOrDefault(n => n.Id == notificationId);
		if (notification != null && !notification.IsRead) {
			var index = _notifications.IndexOf(notification);
			_notifications[index] = notification with { IsRead = true };
			this.NotifyStateChanged();
		}
	}

	public void MarkAllAsRead() {
		for (int i = 0; i < _notifications.Count; i++) {
			if (!_notifications[i].IsRead && !_notifications[i].IsDismissed) {
				_notifications[i] = _notifications[i] with { IsRead = true };
			}
		}
		this.NotifyStateChanged();
	}

	public void Dismiss(string notificationId) {
		var notification = _notifications.FirstOrDefault(n => n.Id == notificationId);
		if (notification != null) {
			var index = _notifications.IndexOf(notification);
			// Mark as read AND dismissed
			_notifications[index] = notification with { IsRead = true, IsDismissed = true };
			this.NotifyStateChanged();
		}
	}

	public void DismissAll() {
		for (int i = 0; i < _notifications.Count; i++) {
			if (!_notifications[i].IsDismissed) {
				_notifications[i] = _notifications[i] with { IsRead = true, IsDismissed = true };
			}
		}
		this.NotifyStateChanged();
	}

	// Keep for hard delete if needed
	public void RemoveNotification(string notificationId) {
		_notifications.RemoveAll(n => n.Id == notificationId);
		this.NotifyStateChanged();
	}

	public void ClearAll() {
		_notifications.Clear();
		this.NotifyStateChanged();
	}

	public void Refresh() {
		this.NotifyStateChanged();
	}

	protected override void OnStateHasChanged() {
		stateManager.NotifySubscribers<INotificationState>(this);
	}

}