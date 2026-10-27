namespace Cirreum.Runtime.Wasm.Tests.Swa;

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

/// <summary>
/// The output side of the shipped SWA config generator: inline-script hashing from the
/// published <c>index.html</c>, the <c>CirreumGenerateCsp=false</c> opt-out, and the two
/// guards that refuse to write a config SWA would misread (a surviving placeholder, invalid
/// JSON). Runs under every PowerShell on the machine.
/// </summary>
public sealed class SwaConfigCspOutputTests : IDisposable {

	private readonly CspGeneratorRunner _runner = new();

	public static TheoryData<string> Shells => CspGeneratorRunner.Shells;

	public void Dispose() => this._runner.Dispose();

	[Theory]
	[MemberData(nameof(Shells))]
	public void Importmap_is_always_hashed_and_opt_in_ids_on_request(string shell) {
		var withoutIds = this._runner.Run(shell, outputName: "without.json");
		var withIds = this._runner.Run(shell, inlineScriptIds: "app-boot", outputName: "with.json");

		withoutIds.ExitCode.Should().Be(0, withoutIds.StdOut);
		withIds.ExitCode.Should().Be(0, withIds.StdOut);

		var importMapHash = Sha256Token(CspGeneratorRunner.ImportMapBody);
		var bootHash = Sha256Token("window.appBoot = true;");

		withoutIds.Directives["script-src"].Should().Contain(importMapHash).And.NotContain(bootHash);
		withIds.Directives["script-src"].Should().Contain(importMapHash).And.Contain(bootHash);
	}

	[Theory]
	[MemberData(nameof(Shells))]
	public void Disabling_csp_removes_the_header_and_keeps_the_rest(string shell) {
		var result = this._runner.Run(shell, generateCsp: false, appPolicy: "script-src 'unsafe-eval';");

		result.ExitCode.Should().Be(0, result.StdOut);
		result.Csp.Should().BeNull();
		result.WarningCodes.Should().BeEmpty("policies are not analysed when no policy is emitted");
		result.OutputText.Should().Contain("Strict-Transport-Security").And.Contain("navigationFallback");
	}

	[Theory]
	[MemberData(nameof(Shells))]
	public void Surviving_placeholder_fails_generation_with_CSP0903(string shell) {
		var result = this._runner.Run(shell, generateCsp: false, template:
			"""
			{ "routes": [ { "route": "/__CSP__", "statusCode": 404 } ], "globalHeaders": { "Content-Security-Policy": "__CSP__" } }
			""");

		result.ExitCode.Should().NotBe(0);
		result.ErrorCodes.Should().Equal("CSP0903");
		result.OutputExists.Should().BeFalse();
	}

	[Theory]
	[MemberData(nameof(Shells))]
	public void Invalid_json_fails_generation_with_CSP0902(string shell) {
		var result = this._runner.Run(shell, template: """{ "globalHeaders": { "Content-Security-Policy": "__CSP__" }""");

		result.ExitCode.Should().NotBe(0);
		result.ErrorCodes.Should().Equal("CSP0902");
		result.OutputExists.Should().BeFalse();
	}

	[Theory]
	[MemberData(nameof(Shells))]
	public void Generated_config_is_valid_json_with_the_expected_shape(string shell) {
		var result = this._runner.Run(shell, appPolicy: "connect-src https://api.example.com;");

		result.ExitCode.Should().Be(0, result.StdOut);
		using var doc = JsonDocument.Parse(result.OutputText);
		doc.RootElement.TryGetProperty("navigationFallback", out _).Should().BeTrue();
		doc.RootElement.TryGetProperty("routes", out _).Should().BeTrue();
		doc.RootElement.GetProperty("globalHeaders").TryGetProperty("Strict-Transport-Security", out _).Should().BeTrue();
	}

	private static string Sha256Token(string content) =>
		$"'sha256-{Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(content)))}'";
}
