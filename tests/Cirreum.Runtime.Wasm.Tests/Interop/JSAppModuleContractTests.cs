namespace Cirreum.Runtime.Wasm.Tests.Interop;

using System.Text.RegularExpressions;

/// <summary>
/// Locks <c>JSAppModule</c> to the exports of <c>cirreum-interop.ts</c>. Every module-scoped
/// identifier the C# side invokes must be an exported function in the TypeScript source, and
/// the argument count the C# side passes must be one the function accepts. A mismatch on
/// either side fails silently at runtime (the browser reports an unknown function only when
/// the call is made, and a surplus argument is simply dropped), so this is the only place it
/// fails loudly.
/// </summary>
/// <remarks>
/// The TypeScript lives in <c>Cirreum.Components.WebAssembly</c>, a sibling checkout. The
/// test reads it from disk and is a no-op where that checkout is absent.
/// </remarks>
public sealed partial class JSAppModuleContractTests {

	[GeneratedRegex(@"^export function (?<name>\w+)\s*\((?<params>[^)]*)\)", RegexOptions.Multiline)]
	private static partial Regex ExportPattern();

	// this.Module.Invoke<T>("name", a, b) / this.Module.InvokeVoid("name", a, b)
	[GeneratedRegex(@"this\.Module\.Invoke(?:Void|<[^>]+>)\(\s*(?<id>""(?<name>\w+)""|(?<const>[A-Z_]+))\s*(?<args>(?:,[^;]*?)?)\);", RegexOptions.Singleline)]
	private static partial Regex ModuleCallPattern();

	[GeneratedRegex(@"private const string (?<const>[A-Z_]+) = ""(?<name>\w+)"";")]
	private static partial Regex ConstPattern();

	private static readonly string? TypeScriptPath = LocateTypeScript();
	private static readonly string ConsumerPath = LocateConsumer();

	[Fact]
	public void Every_module_call_names_an_export_and_passes_an_accepted_argument_count() {
		if (TypeScriptPath is null) {
			return; // Sibling Components checkout not present; nothing to compare against.
		}

		var exports = ParseExports(File.ReadAllText(TypeScriptPath));
		var consumer = File.ReadAllText(ConsumerPath);
		var constants = ConstPattern().Matches(consumer).ToDictionary(m => m.Groups["const"].Value, m => m.Groups["name"].Value);

		var calls = ModuleCallPattern().Matches(consumer);
		calls.Should().NotBeEmpty("the consumer should route its calls through the Module property");

		var problems = new List<string>();
		foreach (Match call in calls) {
			var name = call.Groups["name"].Success ? call.Groups["name"].Value : constants[call.Groups["const"].Value];
			var argCount = CountArguments(call.Groups["args"].Value);

			if (!exports.TryGetValue(name, out var signature)) {
				problems.Add($"{name}: not exported by cirreum-interop.ts");
				continue;
			}
			if (argCount < signature.Required || argCount > signature.Total) {
				problems.Add($"{name}: C# passes {argCount} argument(s), TypeScript accepts {signature.Required}..{signature.Total}");
			}
		}

		problems.Should().BeEmpty(string.Join(Environment.NewLine, problems));
	}

	[Fact]
	public void Every_export_is_consumed_or_deliberately_public() {
		if (TypeScriptPath is null) {
			return;
		}

		var exports = ParseExports(File.ReadAllText(TypeScriptPath)).Keys;
		var consumer = File.ReadAllText(ConsumerPath);

		var unused = exports.Where(e => !consumer.Contains($"\"{e}\"", StringComparison.Ordinal)).ToList();

		unused.Should().BeEmpty("an export nothing in JSAppModule calls is either dead or reachable only through " +
			"the arbitrary Invoke overloads, which target window rather than the module: " + string.Join(", ", unused));
	}

	private static Dictionary<string, (int Required, int Total)> ParseExports(string typeScript) {
		var result = new Dictionary<string, (int, int)>(StringComparer.Ordinal);
		foreach (Match m in ExportPattern().Matches(typeScript)) {
			var parameters = m.Groups["params"].Value
				.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
			var total = parameters.Length;
			var required = parameters.Count(p => !p.Contains('?') && !p.Contains('=') && !p.StartsWith("...", StringComparison.Ordinal));
			result[m.Groups["name"].Value] = (required, total);
		}
		return result;
	}

	private static int CountArguments(string args) {
		var trimmed = args.Trim().TrimStart(',').Trim();
		if (trimmed.Length == 0) {
			return 0;
		}
		// Arguments in the consumer are identifiers or simple member accesses; no nested commas.
		return trimmed.Split(',').Length;
	}

	private static string LocateConsumer() {
		var root = LocateRepoRoot();
		return Path.Combine(root, "src", "Cirreum.Runtime.Wasm", "Components", "Interop", "JSAppModule.cs");
	}

	private static string? LocateTypeScript() {
		var root = LocateRepoRoot();
		var candidate = Path.GetFullPath(Path.Combine(root, "..", "..", "Core", "Cirreum.Components.WebAssembly",
			"src", "Cirreum.Components.WebAssembly", "wwwroot", "js", "cirreum-interop.ts"));
		return File.Exists(candidate) ? candidate : null;
	}

	private static string LocateRepoRoot() {
		var dir = new DirectoryInfo(AppContext.BaseDirectory);
		while (dir is not null) {
			if (File.Exists(Path.Combine(dir.FullName, "Cirreum.Runtime.Wasm.slnx"))) {
				return dir.FullName;
			}
			dir = dir.Parent;
		}
		throw new InvalidOperationException("Could not locate the Cirreum.Runtime.Wasm repository root above the test output directory.");
	}
}
