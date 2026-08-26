namespace Cirreum.Runtime.Authentication;

using Microsoft.Extensions.Logging;

/// <summary>
/// Source-generated logging for the session credential source.
/// </summary>
internal static partial class SessionRemoteConnectionCredentialSourceLogger {

	[LoggerMessage(
		EventId = 1101,
		Level = LogLevel.Warning,
		Message = "Remote connection {ConnectionType} to {Endpoint} declares no scopes, so no credential was supplied. Set Scopes on the connection's options to the audience it authenticates against.")]
	public static partial void LogNoScopesDeclared(
		this ILogger logger,
		string connectionType,
		string endpoint);

	[LoggerMessage(
		EventId = 1102,
		Level = LogLevel.Warning,
		Message = "Remote connection {ConnectionType} could not obtain a token without user interaction ({Status}), so no credential was supplied")]
	public static partial void LogTokenUnavailable(
		this ILogger logger,
		string connectionType,
		string status);

}
