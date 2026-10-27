namespace Cirreum.Runtime.Wasm.Tests.Swa;

using System.Diagnostics;

/// <summary>
/// Drives the shipped <c>build\swa\SwaConfig-CspGen.ps1</c> exactly as the MSBuild target
/// does — a fresh PowerShell process, <c>-NoProfile -ExecutionPolicy Bypass -File</c> — against
/// files written into a per-test temporary folder. The script is read from the repository,
/// not copied, so the tests exercise what the package ships.
/// </summary>
public sealed class CspGeneratorRunner : IDisposable {

	private static readonly string SwaDirectory = LocateSwaDirectory();

	public static string ScriptPath => Path.Combine(SwaDirectory, "SwaConfig-CspGen.ps1");
	public static string BasePolicyPath => Path.Combine(SwaDirectory, "csp.base.policy");
	public static string TemplatePath => Path.Combine(SwaDirectory, "staticwebapp.config.template.json");

	/// <summary>
	/// The PowerShell executables available on this machine. The target defaults to
	/// <c>powershell</c> (5.1) on Windows and <c>pwsh</c> elsewhere, and an app may force
	/// either, so the script must behave identically under both.
	/// </summary>
	public static TheoryData<string> Shells {
		get {
			var data = new TheoryData<string>();
			foreach (var shell in new[] { "pwsh", "powershell" }) {
				if (IsOnPath(shell)) {
					data.Add(shell);
				}
			}
			return data;
		}
	}

	public const string ImportMapBody = """{"imports":{"./_framework/dotnet.js":"./_framework/dotnet.abc123.js"}}""";

	public const string DefaultIndexHtml =
		$"""
		<!DOCTYPE html>
		<html><head>
		<script type="importmap">{ImportMapBody}</script>
		<script id="app-boot">window.appBoot = true;</script>
		</head><body><div id="app"></div></body></html>
		""";

	private readonly string _directory = Path.Combine(Path.GetTempPath(), "cirreum-csp-tests", Guid.NewGuid().ToString("N"));

	public CspGeneratorRunner() {
		Directory.CreateDirectory(this._directory);
	}

	/// <summary>Writes <paramref name="content"/> into the test folder and returns its full path.</summary>
	public string WriteFile(string name, string content) {
		var path = Path.Combine(this._directory, name);
		File.WriteAllText(path, content);
		return path;
	}

	public CspGeneratorResult Run(
		string shell,
		string? appPolicy = null,
		string? envPolicy = null,
		string? template = null,
		string? basePolicy = null,
		string? indexHtml = DefaultIndexHtml,
		string inlineScriptIds = "",
		bool generateCsp = true,
		string outputName = "staticwebapp.config.json") {

		var appPolicyPath = appPolicy is null ? Path.Combine(this._directory, "csp.policy") : this.WriteFile("csp.policy", appPolicy);
		var envPolicyPath = envPolicy is null ? string.Empty : this.WriteFile("csp.Staging.policy", envPolicy);
		var templatePath = template is null ? TemplatePath : this.WriteFile("staticwebapp.config.template.json", template);
		var basePolicyPath = basePolicy is null ? BasePolicyPath : this.WriteFile("csp.base.policy", basePolicy);
		var indexPath = indexHtml is null ? Path.Combine(this._directory, "index.html") : this.WriteFile("index.html", indexHtml);
		var outputPath = Path.Combine(this._directory, outputName);

		var start = new ProcessStartInfo(shell) {
			RedirectStandardOutput = true,
			RedirectStandardError = true,
			UseShellExecute = false,
			WorkingDirectory = this._directory,
		};
		start.ArgumentList.Add("-NoProfile");
		start.ArgumentList.Add("-ExecutionPolicy");
		start.ArgumentList.Add("Bypass");
		start.ArgumentList.Add("-File");
		start.ArgumentList.Add(ScriptPath);
		AddArgument(start, "-Template", templatePath);
		AddArgument(start, "-BasePolicy", basePolicyPath);
		AddArgument(start, "-AppPolicy", appPolicyPath);
		AddArgument(start, "-EnvPolicy", envPolicyPath);
		AddArgument(start, "-Output", outputPath);
		AddArgument(start, "-PublishedIndexHtml", indexPath);
		AddArgument(start, "-InlineScriptIds", inlineScriptIds);
		AddArgument(start, "-GenerateCsp", generateCsp ? "true" : "false");

		using var process = Process.Start(start) ?? throw new InvalidOperationException($"Could not start {shell}.");
		var stdout = process.StandardOutput.ReadToEndAsync();
		var stderr = process.StandardError.ReadToEndAsync();
		process.WaitForExit();

		return new CspGeneratorResult(process.ExitCode, stdout.Result, stderr.Result, outputPath);
	}

	public void Dispose() {
		try {
			Directory.Delete(this._directory, recursive: true);
		} catch (IOException) {
			// Best effort; the temp folder is per-run and harmless if it lingers.
		}
	}

	private static void AddArgument(ProcessStartInfo start, string name, string value) {
		start.ArgumentList.Add(name);
		start.ArgumentList.Add(value);
	}

	private static bool IsOnPath(string executable) {
		var path = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
		var names = OperatingSystem.IsWindows() ? new[] { executable + ".exe", executable } : [executable];
		return path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
			.Any(dir => names.Any(n => File.Exists(Path.Combine(dir, n))));
	}

	private static string LocateSwaDirectory() {
		var dir = new DirectoryInfo(AppContext.BaseDirectory);
		while (dir is not null) {
			var candidate = Path.Combine(dir.FullName, "build", "swa");
			if (File.Exists(Path.Combine(candidate, "SwaConfig-CspGen.ps1"))) {
				return candidate;
			}
			dir = dir.Parent;
		}
		throw new InvalidOperationException("Could not locate build\\swa\\SwaConfig-CspGen.ps1 above the test output directory.");
	}
}
