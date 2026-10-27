namespace Cirreum.Runtime.Wasm.Tests.Swa;

/// <summary>
/// Merging and normalization in the shipped SWA config generator: baseline, app policy and
/// environment overlay union per directive, <c>'none'</c> is dropped once a directive gains
/// sources, single-value directives replace, and the emitted header is deterministic.
/// Runs under every PowerShell on the machine. Split from the diagnostics and output
/// classes so xUnit runs the three in parallel; each test pays a shell start-up.
/// </summary>
public sealed class SwaConfigCspMergeTests : IDisposable {

	private readonly CspGeneratorRunner _runner = new();

	public static TheoryData<string> Shells => CspGeneratorRunner.Shells;

	public void Dispose() => this._runner.Dispose();

	[Theory]
	[MemberData(nameof(Shells))]
	public void Baseline_alone_generates_the_framework_policy_without_warnings(string shell) {
		var result = this._runner.Run(shell);

		result.ExitCode.Should().Be(0, result.StdOut + result.StdErr);
		result.WarningCodes.Should().BeEmpty("the framework's own choices are not reported against the framework");
		var directives = result.Directives;
		directives["default-src"].Should().Be("'self'");
		directives["connect-src"].Should().Be("'self'");
		directives["object-src"].Should().Be("'none'");
		directives["base-uri"].Should().Be("'self'");
		directives["frame-ancestors"].Should().Be("'self'");
		directives["upgrade-insecure-requests"].Should().BeEmpty("a valueless directive emits its name alone");
		directives["script-src"].Should().Contain("'wasm-unsafe-eval'");
	}

	[Theory]
	[MemberData(nameof(Shells))]
	public void App_policy_unions_with_the_baseline_rather_than_replacing_it(string shell) {
		var result = this._runner.Run(shell, appPolicy: "connect-src https://api.example.com;");

		result.ExitCode.Should().Be(0, result.StdOut);
		result.Directives["connect-src"].Should().Be("'self' https://api.example.com",
			"the baseline's 'self' must survive the app's addition or the runtime cannot fetch _framework/*");
	}

	[Theory]
	[MemberData(nameof(Shells))]
	public void Environment_overlay_merges_on_top_of_base_and_app(string shell) {
		var result = this._runner.Run(shell,
			appPolicy: "connect-src https://api.example.com;",
			envPolicy: "connect-src https://api-staging.example.com; frame-src https://idp.example.com;");

		result.ExitCode.Should().Be(0, result.StdOut);
		result.Directives["connect-src"].Should().Be("'self' https://api-staging.example.com https://api.example.com");
		result.Directives["frame-src"].Should().Be("https://idp.example.com");
	}

	[Theory]
	[MemberData(nameof(Shells))]
	public void Missing_app_and_environment_policies_are_not_errors(string shell) {
		var result = this._runner.Run(shell, appPolicy: null, envPolicy: null);

		result.ExitCode.Should().Be(0, result.StdOut);
		result.StdOut.Should().Contain("No app policy at:");
		result.OutputExists.Should().BeTrue();
	}

	[Theory]
	[MemberData(nameof(Shells))]
	public void Comment_line_above_a_directive_does_not_swallow_it(string shell) {
		var result = this._runner.Run(shell, appPolicy:
			"""
			# the API
			connect-src https://api.example.com;
			// images
			img-src https://images.example.com;
			""");

		result.ExitCode.Should().Be(0, result.StdOut);
		result.Directives["connect-src"].Should().Contain("https://api.example.com");
		result.Directives["img-src"].Should().Contain("https://images.example.com");
	}

	[Theory]
	[MemberData(nameof(Shells))]
	public void Duplicate_sources_across_layers_emit_once(string shell) {
		var result = this._runner.Run(shell,
			appPolicy: "connect-src https://api.example.com 'self';",
			envPolicy: "connect-src HTTPS://API.EXAMPLE.COM;");

		result.ExitCode.Should().Be(0, result.StdOut);
		result.Directives["connect-src"].Split(' ').Should().HaveCount(2);
	}

	[Theory]
	[MemberData(nameof(Shells))]
	public void Sources_added_to_a_none_directive_remove_the_none_and_warn(string shell) {
		var result = this._runner.Run(shell, appPolicy: "object-src https://plugins.example.com;");

		result.ExitCode.Should().Be(0, result.StdOut);
		result.Directives["object-src"].Should().Be("https://plugins.example.com");
		result.WarningCodes.Should().Contain("CSP0009");
		result.LinesFor("CSP0009").Single().Should().Contain("csp.policy").And.Contain("object-src");
	}

	[Theory]
	[MemberData(nameof(Shells))]
	public void A_lone_none_is_preserved(string shell) {
		var result = this._runner.Run(shell, appPolicy: "frame-src 'none';");

		result.ExitCode.Should().Be(0, result.StdOut);
		result.Directives["frame-src"].Should().Be("'none'");
		result.WarningCodes.Should().NotContain("CSP0009");
	}

	[Theory]
	[MemberData(nameof(Shells))]
	public void Single_value_directive_is_replaced_not_unioned(string shell) {
		var result = this._runner.Run(shell,
			appPolicy: "report-to app-group;",
			envPolicy: "report-to staging-group;");

		result.ExitCode.Should().Be(0, result.StdOut);
		result.Directives["report-to"].Should().Be("staging-group");
		result.WarningCodes.Should().Contain("CSP0012");
	}

	[Theory]
	[MemberData(nameof(Shells))]
	public void Output_is_deterministic_regardless_of_declaration_order(string shell) {
		var forward = this._runner.Run(shell,
			appPolicy: "connect-src https://a.example.com https://b.example.com; img-src https://c.example.com;",
			outputName: "forward.json");
		var reversed = this._runner.Run(shell,
			appPolicy: "img-src https://c.example.com; connect-src https://b.example.com https://a.example.com;",
			outputName: "reversed.json");

		forward.ExitCode.Should().Be(0, forward.StdOut);
		reversed.ExitCode.Should().Be(0, reversed.StdOut);
		forward.OutputText.Should().Be(reversed.OutputText);
		forward.Directives["connect-src"].Should().Be("'self' https://a.example.com https://b.example.com");
	}
}
