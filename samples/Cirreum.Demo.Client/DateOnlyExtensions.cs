namespace Cirreum.Demo.Client;

public static class DateOnlyExtensions {

	public static int GetAge(this DateOnly dateOfBirth) {
		var today = DateOnly.FromDateTime(DateTime.UtcNow);
		var age = today.Year - dateOfBirth.Year;
		if (dateOfBirth > today.AddYears(-age)) {
			age--;
		}

		return age;
	}

	public static bool IsMinorAge(this DateOnly dateOfBirth) => dateOfBirth.GetAge() < 18;

}