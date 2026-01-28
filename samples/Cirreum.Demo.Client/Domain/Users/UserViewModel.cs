namespace Cirreum.Demo.Client.Domain.Users;

using Cirreum.Components.ViewModels;

/// <summary>
/// An example POCO view model that demonstrates typical property patterns.
/// </summary>
public class UserViewModel : ViewModel {
	private static readonly DateOnly DefaultBirthday = new(1970, 9, 22);

	public UserViewModel() {
		// Initialize default values
		this.RequestedNumbers = ["222", "22", "2"];
		this.FirstName = "Glen";
		this.LastName = "Banta";
		this.Email = "glen@corracing.com";
		this.Birthday = DefaultBirthday;
	}


	public SimpleAddress HomeAddress { get; set; } = new SimpleAddress();


	// By using a backing field, and defining the set body,
	// we can trigger field change validation
	private int _counter;
	public int Counter {
		get => this._counter;
		set {
			this._counter = value;
			var fieldIdentifier = this.GetFieldIdentifier(nameof(this.Counter));
			if (fieldIdentifier.HasValue) {
				this.EditContext.NotifyFieldChanged(fieldIdentifier.Value);
			}
		}
	}

	public void Increment() => this.Counter++;
	public void Decrement() => this.Counter--;

	public string FirstName { get; set; } = "";
	public string LastName { get; set; } = "";
	public string Email { get; set; } = "";

	private DateOnly _birthday;
	public DateOnly Birthday {
		get => this._birthday;
		set {
			this._birthday = value;
			this.IsMinor = value.IsMinorAge();
		}
	}

	public bool IsMinor { get; private set; }
	public string[] RequestedNumbers { get; set; } = [];
	public string Settings { get; set; } = "";

	/// <inheritdoc/>
	protected override void ResetProperties() {
		this._counter = 0; // reset the backing field
		this.FirstName = "Glen";
		this.LastName = "Banta";
		this.Email = "glen@corracing.com";
		this._birthday = DefaultBirthday; // reset the backing field
		this.IsMinor = false;
		this.RequestedNumbers = ["222", "22", "2"];
		this.Settings = "";
		this.HomeAddress = new SimpleAddress {
			Street = "",
			City = "",
			State = "",
			PostalCode = "",
			Country = null
		};
	}

}