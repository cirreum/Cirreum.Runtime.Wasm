namespace Cirreum.Runtime.Wasm.Tests.Swa;

/// <summary>
/// The risk and syntax diagnostics of the shipped SWA config generator: routine additions
/// stay silent, material widenings warn with a stable <c>CSP00xx</c> code attributed to the
/// policy file that made them, the framework baseline is never reported as risk, and an
/// unquoted keyword fails generation. Runs under every PowerShell on the machine.
/// </summary>
public sealed class SwaConfigCspDiagnosticsTests : IDisposable {

	private readonly CspGeneratorRunner _runner = new();

	public static TheoryData<string> Shells => CspGeneratorRunner.Shells;

	public void Dispose() => this._runner.Dispose();

	[Theory]
	[MemberData(nameof(Shells))]
	public void Routine_additions_produce_no_warnings(string shell) {
		var result = this._runner.Run(shell,
			appPolicy:
				"""
				connect-src https://api.example.com https://*.monitor.azure.com wss://hub.example.com;
				img-src https://images.example.com data: blob:;
				font-src https://fonts.gstatic.com data:;
				style-src https://fonts.googleapis.com;
				frame-src https://idp.example.com;
				frame-ancestors https://portal.example.com;
				media-src https://media.example.com;
				script-src 'self';
				""",
			envPolicy: "connect-src ws://localhost:* wss://localhost:* http://localhost:* https://localhost:*;");

		result.ExitCode.Should().Be(0, result.StdOut);
		result.WarningCodes.Should().BeEmpty(result.StdOut);
	}

	[Theory]
	[MemberData(nameof(Shells))]
	public void Unsafe_keywords_on_script_directives_warn_CSP0001(string shell) {
		var result = this._runner.Run(shell, appPolicy: "script-src 'unsafe-inline' 'unsafe-eval'; worker-src 'unsafe-eval';");

		result.ExitCode.Should().Be(0, result.StdOut);
		result.WarningCodes.Should().Equal("CSP0001", "CSP0001");
		result.LinesFor("CSP0001")[0].Should().Contain("'unsafe-inline' 'unsafe-eval'");
	}

	[Theory]
	[MemberData(nameof(Shells))]
	public void Unsafe_inline_on_style_directives_is_routine(string shell) {
		var result = this._runner.Run(shell, appPolicy: "style-src-elem 'unsafe-inline';");

		result.ExitCode.Should().Be(0, result.StdOut);
		result.WarningCodes.Should().BeEmpty("the baseline itself permits inline styles");
	}

	[Theory]
	[MemberData(nameof(Shells))]
	public void Broad_wildcards_warn_CSP0002(string shell) {
		var result = this._runner.Run(shell, appPolicy: "connect-src *; img-src https://*; frame-ancestors *;");

		result.ExitCode.Should().Be(0, result.StdOut);
		result.WarningCodes.Should().Equal("CSP0002", "CSP0002", "CSP0002");
	}

	[Theory]
	[MemberData(nameof(Shells))]
	public void Scheme_only_sources_warn_CSP0003_where_they_matter(string shell) {
		var result = this._runner.Run(shell, appPolicy: "connect-src https:; script-src data:; img-src data: blob:;");

		result.ExitCode.Should().Be(0, result.StdOut);
		result.WarningCodes.Should().Equal("CSP0003", "CSP0003");
		result.LinesFor("CSP0003").Should().Contain(l => l.Contains("connect-src") && l.Contains("https:"));
		result.LinesFor("CSP0003").Should().Contain(l => l.Contains("script-src") && l.Contains("data:"));
	}

	[Theory]
	[MemberData(nameof(Shells))]
	public void Widening_object_src_base_uri_and_default_src_warn(string shell) {
		var result = this._runner.Run(shell,
			appPolicy: "object-src https://plugins.example.com; base-uri https://cdn.example.com; default-src https://everything.example.com;");

		result.ExitCode.Should().Be(0, result.StdOut);
		result.WarningCodes.Should().Contain(["CSP0004", "CSP0005", "CSP0006"]);
	}

	[Theory]
	[MemberData(nameof(Shells))]
	public void Third_party_script_origin_warns_CSP0007(string shell) {
		var result = this._runner.Run(shell, appPolicy: "script-src https://cdn.jsdelivr.net/npm/; script-src-elem https://js.monitor.azure.com;");

		result.ExitCode.Should().Be(0, result.StdOut);
		result.WarningCodes.Should().Equal("CSP0007", "CSP0007");
	}

	[Theory]
	[MemberData(nameof(Shells))]
	public void Static_nonce_warns_CSP0008(string shell) {
		var result = this._runner.Run(shell, appPolicy: "script-src 'nonce-abc123';");

		result.ExitCode.Should().Be(0, result.StdOut);
		result.WarningCodes.Should().Equal("CSP0008");
	}

	[Theory]
	[MemberData(nameof(Shells))]
	public void Unknown_directive_valueless_with_sources_and_unknown_keyword_warn(string shell) {
		var result = this._runner.Run(shell,
			appPolicy: "bogus-src 'self'; upgrade-insecure-requests 'self'; connect-src 'unsafe-whatever' https://api.example.com;");

		result.ExitCode.Should().Be(0, result.StdOut);
		result.WarningCodes.Should().Equal("CSP0010", "CSP0011", "CSP0013");
		result.Directives["connect-src"].Should().Contain("https://api.example.com");
	}

	[Theory]
	[MemberData(nameof(Shells))]
	public void Warnings_are_attributed_to_the_originating_policy_file(string shell) {
		var result = this._runner.Run(shell,
			appPolicy: "connect-src https://api.example.com;",
			envPolicy: "script-src 'unsafe-eval';");

		result.ExitCode.Should().Be(0, result.StdOut);
		var line = result.LinesFor("CSP0001").Single();
		line.Should().StartWith(Path.Combine(Path.GetTempPath(), "cirreum-csp-tests"));
		line.Should().Contain("csp.Staging.policy : warning CSP0001:");
	}

	[Theory]
	[MemberData(nameof(Shells))]
	public void Baseline_choices_are_not_reported_as_risk(string shell) {
		var result = this._runner.Run(shell,
			basePolicy: "default-src 'self'; script-src 'self' 'unsafe-eval' https://cdn.example.com; object-src *;",
			appPolicy: "connect-src https://api.example.com;");

		result.ExitCode.Should().Be(0, result.StdOut);
		result.WarningCodes.Should().BeEmpty("risk is reported against app and environment policies only");
	}

	[Theory]
	[MemberData(nameof(Shells))]
	public void Unquoted_keyword_fails_generation_with_CSP0901(string shell) {
		var result = this._runner.Run(shell, appPolicy: "script-src unsafe-inline;");

		result.ExitCode.Should().NotBe(0);
		result.ErrorCodes.Should().Equal("CSP0901");
		result.LinesFor("CSP0901").Single().Should().Contain("unsafe-inline").And.Contain("csp.policy : error");
		result.OutputExists.Should().BeFalse("a policy the browser would misread must not be written");
	}

	[Theory]
	[MemberData(nameof(Shells))]
	public void Unquoted_keyword_in_the_baseline_is_also_an_error(string shell) {
		var result = this._runner.Run(shell, basePolicy: "default-src self;");

		result.ExitCode.Should().NotBe(0);
		result.ErrorCodes.Should().Equal("CSP0901");
	}
}
