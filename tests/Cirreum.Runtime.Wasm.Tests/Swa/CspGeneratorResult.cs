namespace Cirreum.Runtime.Wasm.Tests.Swa;

using System.Text.Json;
using System.Text.RegularExpressions;

/// <summary>
/// One run of <c>SwaConfig-CspGen.ps1</c>: the process outcome plus accessors over the
/// generated <c>staticwebapp.config.json</c> and the MSBuild-canonical diagnostics the
/// script wrote to standard output.
/// </summary>
public sealed partial record CspGeneratorResult(int ExitCode, string StdOut, string StdErr, string OutputPath) {

	[GeneratedRegex(@"warning (CSP\d{4}):", RegexOptions.Multiline)]
	private static partial Regex WarningPattern();

	[GeneratedRegex(@"error (CSP\d{4}):", RegexOptions.Multiline)]
	private static partial Regex ErrorPattern();

	public bool OutputExists => File.Exists(this.OutputPath);

	public string OutputText => File.ReadAllText(this.OutputPath);

	/// <summary>Codes of every CSP warning the script emitted, in order, duplicates kept.</summary>
	public IReadOnlyList<string> WarningCodes =>
		[.. WarningPattern().Matches(this.StdOut).Select(m => m.Groups[1].Value)];

	/// <summary>Codes of every CSP error the script emitted.</summary>
	public IReadOnlyList<string> ErrorCodes =>
		[.. ErrorPattern().Matches(this.StdOut).Select(m => m.Groups[1].Value)];

	/// <summary>Standard-output lines carrying the given diagnostic code.</summary>
	public IReadOnlyList<string> LinesFor(string code) =>
		[.. this.StdOut.Split('\n').Where(l => l.Contains($"{code}:", StringComparison.Ordinal)).Select(l => l.TrimEnd('\r'))];

	/// <summary>
	/// The <c>Content-Security-Policy</c> global header of the generated config, or
	/// <see langword="null"/> when the config has none.
	/// </summary>
	public string? Csp {
		get {
			using var doc = JsonDocument.Parse(this.OutputText);
			if (!doc.RootElement.TryGetProperty("globalHeaders", out var headers)) {
				return null;
			}
			return headers.TryGetProperty("Content-Security-Policy", out var csp) ? csp.GetString() : null;
		}
	}

	/// <summary>The generated policy split into <c>directive → source list</c>.</summary>
	public IReadOnlyDictionary<string, string> Directives {
		get {
			var csp = this.Csp ?? throw new InvalidOperationException("The generated config has no Content-Security-Policy header.");
			var result = new Dictionary<string, string>(StringComparer.Ordinal);
			foreach (var part in csp.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)) {
				var space = part.IndexOf(' ');
				if (space < 0) {
					result[part] = string.Empty;
				} else {
					result[part[..space]] = part[(space + 1)..];
				}
			}
			return result;
		}
	}
}
