namespace Cirreum.Demo.Client.Domain.Users;

using Cirreum.Components.ViewModels;
using Cirreum.Demo.Client;

/// <summary>
/// Constructs a new instance of the view model.
/// </summary>
/// <param name="session">The session state service.</param>
/// <remarks>
/// <para>
/// An example session-backed view model that demonstrates typical property patterns
/// and session state integration.
/// </para>
/// </remarks>
public class UserSessionViewModel(
	ISessionState session
) : SessionStateViewModel<UserSessionViewModel>(session, "global") {

	public int Counter => this.Get<int>();
	public Task SetCounter(int value) => this.Set(value);
	public Task Increment() {
		return this.SetCounter(this.Counter + 1);
	}
	public Task Decrement() {
		return this.SetCounter(this.Counter - 1);
	}

	public SimpleAddressViewModel HomeAddress => this.GetNested<SimpleAddressViewModel>();

	//public string HomeAddressStreet => this.Get<string>();
	//public Task SetHomeAddressStreet(string value) => this.Set(value);

	//public string HomeAddressCity => this.Get<string>();
	//public Task SetHomeAddressCity(string value) => this.Set(value);

	//public string HomeAddressState => this.Get<string>();
	//public Task SetHomeAddressState(string value) => this.Set(value);

	//public string HomeAddressPostalCode => this.Get<string>();
	//public Task SetHomeAddressPostalCode(string value) => this.Set(value);

	//public string HomeAddressCountry => this.Get<string>();
	//public Task SetHomeAddressCountry(string value) => this.Set(value);

	public string FirstName => this.Get<string>();
	public Task SetFirstName(string value) => this.Set(value);

	public string LastName => this.Get<string>();
	public Task SetLastName(string value) => this.Set(value);

	public string Email => this.Get<string>();
	public Task SetEmail(string value) => this.Set(value);

	public DateOnly Birthday => this.Get<DateOnly>();
	/// <summary>
	/// Sets the birthday and automatically updates the IsMinor status.
	/// </summary>
	/// <param name="value">The new birthday value</param>
	/// <remarks>
	/// This demonstrates how to handle dependent properties where changing one
	/// value should automatically update related state.
	/// </remarks>
	public async Task SetBirthday(DateOnly value) {
		await using var scope = this.CreateNotificationScope();
		await this.SetIsMinor(value.IsMinorAge());
		await this.Set(value);
	}
	private static readonly DateOnly DefaultBirthday = new DateOnly(1970, 9, 22);

	public bool IsMinor => this.Get<bool>();
	public Task SetIsMinor(bool value) => this.Set(value);

	public string[] RequestedNumbers => this.Get<string[]>();
	public Task SetRequestedNumbers(params string[] numbers) => this.Set(numbers);

	public string Settings => this.Get<string>();
	public Task SetSettings(string value) => this.Set(value);

	protected override void Configure() {
		this
			.CreateProperty(x => x.Settings, "")
			.CreateProperty(x => x.Counter, 0)
			.CreateProperty(x => x.FirstName, "Glen")
			.CreateProperty(x => x.LastName, "Banta")
			.CreateProperty(x => x.Email, "glen@corracing.com")
			.CreateProperty(x => x.Birthday, DefaultBirthday)
			.CreateProperty(x => x.IsMinor, false)
			.CreateProperty(x => x.RequestedNumbers, ["222", "22", "2"])
			//.CreateProperty(x => x.HomeAddressStreet, "")
			//.CreateProperty(x => x.HomeAddressCity, "")
			//.CreateProperty(x => x.HomeAddressState, "")
			//.CreateProperty(x => x.HomeAddressPostalCode, "")
			//.CreateProperty(x => x.HomeAddressCountry, "")
			.CreateNested(x => x.HomeAddress, address => address
				.Property(a => a.Street)
				.Property(a => a.City)
				.Property(a => a.State)
				.Property(a => a.PostalCode)
				.Property(a => a.Country)
				.Nested(a => a.Location, location => location
					.Property(l => l.Latitude, 0.0m)
					.Property(l => l.Longitude, 0.0m)
				)
			);
	}

}