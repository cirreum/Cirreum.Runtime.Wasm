namespace Cirreum.Demo.Client.Domain.Users;

using Cirreum.Components.ViewModels;

public class SimpleAddressViewModel : NestedStateViewModel<SimpleAddressViewModel> {

	public string Street => this.Get<string>();
	public Task SetStreet(string value) => this.Set(value);

	public string City => this.Get<string>();
	public Task SetCity(string value) => this.Set(value);

	public string PostalCode => this.Get<string>();
	public Task SetPostalCode(string value) => this.Set(value);

	public string State => this.Get<string>();
	public Task SetState(string value) => this.Set(value);

	public string Country => this.Get<string>();
	public Task SetCountry(string value) => this.Set(value);

	public LocationViewModel Location => this.GetNested<LocationViewModel>();

}

public class LocationViewModel : NestedStateViewModel<LocationViewModel> {
	public decimal Latitude => this.Get<decimal>();
	public Task SetLatitude(decimal value) => this.Set(value);

	public decimal Longitude => this.Get<decimal>();
	public Task SetLongitude(decimal value) => this.Set(value);
}