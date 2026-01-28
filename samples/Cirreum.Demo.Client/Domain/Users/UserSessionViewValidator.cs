namespace Cirreum.Demo.Client.Domain.Users;

using FluentValidation;

public class UserSessionViewValidator : AbstractValidator<UserSessionViewModel> {

	public UserSessionViewValidator() {

		this.RuleFor(static x => x.Counter)
			.GreaterThanOrEqualTo(1);

		this.RuleFor(static x => x.FirstName)
		   .NotEmpty()
		   .MaximumLength(50);

		this.RuleFor(static x => x.LastName)
			.NotEmpty()
			.MaximumLength(50);

		this.RuleFor(static x => x.Email)
			.NotEmpty()
			.EmailAddress();

		this.RuleFor(static x => x.Birthday)
			.IsValidDateOfBirth()
			.Must((m, v) => v.IsMinorAge() == m.IsMinor)
			.WithMessage("IsMinor must match the age (true for under 18, false for 18 or older)");

		this.RuleFor(static x => x.HomeAddress)
			.SetValidator(new SimpleAddressViewModelValidator());

	}

	public static int CalculateAgeWithMonthDay(DateTimeOffset birthDate) {
		var today = DateTime.Today;
		var age = today.Year - birthDate.Year;
		if (today < birthDate.AddYears(age)) {
			age--;
		}

		return age;
	}

}