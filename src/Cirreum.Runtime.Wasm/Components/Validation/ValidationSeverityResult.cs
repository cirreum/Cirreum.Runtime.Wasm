namespace Cirreum.Components.Validation;

/// <summary>
/// Represents the result of a validation operation including severity information.
/// </summary>
public record ValidationSeverityResult(
	bool HasErrors,
	bool HasWarnings,
	bool IsInvalid
);