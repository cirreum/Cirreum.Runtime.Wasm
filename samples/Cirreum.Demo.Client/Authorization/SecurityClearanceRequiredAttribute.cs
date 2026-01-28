namespace Cirreum.Demo.Client.Authorization;

// Example attributes for the policy validators
[AttributeUsage(AttributeTargets.Class)]
public class SecurityClearanceRequiredAttribute(
	SecurityClearanceLevel requiredClearance
) : Attribute {
	public SecurityClearanceLevel RequiredClearance { get; } = requiredClearance;
}