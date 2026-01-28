namespace Cirreum.Runtime.Authentication;

using Microsoft.Extensions.Logging;
using System;

/// <summary>
/// Extension methods for ILogger using source generator pattern
/// </summary>
internal static partial class CommonClaimsPrincipalFactoryLogger {

	[LoggerMessage(
		EventId = 1001,
		Level = LogLevel.Information,
		Message = "Authentication: User Account Created for {NewUser}")]
	public static partial void LogCreateUser(this ILogger logger, string newUser);

	[LoggerMessage(
		EventId = 1002,
		Level = LogLevel.Information,
		Message = "Authentication: User State Changed to {NewUser}")]
	public static partial void LogUserStateChanged(this ILogger logger, string newUser);

	[LoggerMessage(
		EventId = 1003,
		Level = LogLevel.Debug,
		Message = "Enriching user profile")]
	public static partial void LogEnrichingUserProfile(this ILogger logger);

	[LoggerMessage(
		EventId = 1004,
		Level = LogLevel.Error,
		Message = "Error enriching user profile")]
	public static partial void LogUserProfileEnrichmentError(this ILogger logger, Exception ex);

	[LoggerMessage(
		EventId = 1005,
		Level = LogLevel.Debug,
		Message = "Update presence")]
	public static partial void LogUpdatingPresence(this ILogger logger);

	[LoggerMessage(
		EventId = 1006,
		Level = LogLevel.Error,
		Message = "Error updating user presence.")]
	public static partial void LogUserPresenceUpdateError(this ILogger logger, Exception ex);

	[LoggerMessage(
		EventId = 1007,
		Level = LogLevel.Error,
		Message = "CreateUserAsync Error: {Message}")]
	public static partial void LogCreateUserError(this ILogger logger, Exception ex, string message);

	// Overload for the CreateUserError method that uses the exception message
	public static void LogCreateUserError(this ILogger logger, Exception ex) {
		logger.LogCreateUserError(ex, ex.Message);
	}

	[LoggerMessage(
		EventId = 1008,
		Level = LogLevel.Error,
		Message = "Error in claims extender {ExtenderType}")]
	public static partial void LogClaimsExtenderError(this ILogger logger, Exception ex, string extenderType);

	[LoggerMessage(
	  EventId = 1009,
	  Level = LogLevel.Warning,
	  Message = "State notification failed after authentication for user {UserName}")]
	public static partial void LogStateNotificationError(this ILogger logger, Exception ex, string userName);

	[LoggerMessage(
		EventId = 1010,
		Level = LogLevel.Error,
		Message = "User state processing failed after authentication for user {UserName}")]
	public static partial void LogUserStateProcessingError(this ILogger logger, Exception ex, string userName);

}