namespace Cirreum.Demo.Client.Authorization;

/// <summary>
/// Specifies that the attributed class is intended to be used only during business hours.
/// </summary>
/// <remarks>
/// This attribute is used to indicate that the functionality of the attributed class is restricted  to
/// business hours.
/// </remarks>
[AttributeUsage(AttributeTargets.Class)]
public class BusinessHoursOnlyAttribute : Attribute;