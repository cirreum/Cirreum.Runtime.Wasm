namespace Cirreum.Demo.Client.Domain;

using FluentValidation;

public static class SharedRules {

	public static IRuleBuilderOptions<T, DateOnly> IsValidDateOfBirth<T>(this IRuleBuilder<T, DateOnly> ruleBuilder) {
		return ruleBuilder
			.Must(dob => dob <= DateOnly.FromDateTime(DateTime.UtcNow))
				.WithMessage("Date of birth cannot be in the future")
			.Must(dob => dob >= DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-70)))
				.WithMessage("Date of birth cannot be more than 70 years ago");
	}

	public static IRuleBuilderOptions<T, DateOnly> MustBeAtLeastAge<T>(this IRuleBuilder<T, DateOnly> ruleBuilder, int minimumAge) {
		return ruleBuilder
			.Must(dob => dob.GetAge() >= minimumAge)
				.WithMessage($"Person must be at least {minimumAge} years old");
	}

}