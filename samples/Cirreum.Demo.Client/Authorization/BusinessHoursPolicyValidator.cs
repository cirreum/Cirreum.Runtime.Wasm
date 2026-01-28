namespace Cirreum.Demo.Client.Authorization;

using FluentValidation.Results;

/// <summary>
/// Example: Time-based access control
/// </summary>
public class BusinessHoursPolicyValidator : AttributeValidatorBase<BusinessHoursOnlyAttribute> {

	public override string PolicyName => "BusinessHoursPolicy";

	public override int Order => 50;

	public override DomainRuntimeType[] SupportedRuntimeTypes => [DomainRuntimeType.BlazorWasm];

	public override Task<ValidationResult> ValidateAsync<TResource>(
		AuthorizationContext<TResource> context,
		CancellationToken cancellationToken = default) {

		// No attribute, we skip
		var attribute = this.GetAttribute(context.Resource);
		if (attribute == null) {
			return Task.FromResult(new ValidationResult());
		}

		var currentTime = context.Timestamp.ToLocalTime().TimeOfDay;
		var businessStart = TimeSpan.FromHours(8); // 8 AM
		var businessEnd = TimeSpan.FromHours(17);  // 5 PM

		if (currentTime < businessStart || currentTime > businessEnd) {
			return Task.FromResult(new ValidationResult([
				new ValidationFailure(
					propertyName: "AccessTime",
					errorMessage: "Access to this resource is only allowed during business hours (8 AM - 5 PM)")
			]));
		}

		return Task.FromResult(new ValidationResult());
	}
}
