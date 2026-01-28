namespace Cirreum.Demo.Client.Authorization;

using FluentValidation.Results;

/// <summary>
/// Example: Requires specific security clearance for resources marked with SecurityClearanceRequired attribute
/// </summary>
public class SecurityClearancePolicyValidator : AttributeValidatorBase<SecurityClearanceRequiredAttribute> {

	public override string PolicyName => "SecurityClearancePolicy";
	public override int Order => 100; // Run early
	public override DomainRuntimeType[] SupportedRuntimeTypes =>
		[DomainRuntimeType.BlazorWasm | DomainRuntimeType.Function];

	public override Task<ValidationResult> ValidateAsync<TResource>(
		AuthorizationContext<TResource> context,
		CancellationToken cancellationToken = default) {

		// No attribute, we skip
		var attribute = this.GetAttribute(context.Resource);
		if (attribute == null) {
			return Task.FromResult(new ValidationResult());
		}

		var requiredClearance = attribute.RequiredClearance;
		if (requiredClearance == SecurityClearanceLevel.Public) {
			return Task.FromResult(new ValidationResult());
		}

		var userClearanceStr = context.UserState.GetClaim("SecurityClearance");

		// Handle missing or empty clearance claim
		if (string.IsNullOrWhiteSpace(userClearanceStr)) {
			return Task.FromResult(new ValidationResult([
				new ValidationFailure("SecurityClearance",
				$"No security clearance found. Required: {requiredClearance}")
			]));
		}

		if (Enum.TryParse<SecurityClearanceLevel>(userClearanceStr, out var userClearance)) {
			if (userClearance < requiredClearance) {
				return Task.FromResult(new ValidationResult([
					new ValidationFailure("SecurityClearance",
					$"Insufficient security clearance. Required: {requiredClearance}, User has: {userClearance}")
				]));
			}
			return Task.FromResult(new ValidationResult());
		}

		// Invalid clearance value
		return Task.FromResult(new ValidationResult([
			new ValidationFailure("SecurityClearance",
			$"Invalid security clearance value: '{userClearanceStr}'. Required: {requiredClearance}")
		]));
	}

}