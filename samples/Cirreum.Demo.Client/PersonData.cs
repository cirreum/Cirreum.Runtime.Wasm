namespace Cirreum.Demo.Client;

using System;
using System.ComponentModel;

public class PersonData {

	public int Id { get; set; }
	public Name Name { get; set; } = new Name();
	public string FullName { get; set; } = "";
	public string Company { get; set; } = "";
	public string Email { get; set; } = "";
	public string Address { get; set; } = "";
	public bool? Paid { get; set; }
	public decimal Balance { get; set; }
	public CreditCard CreditCardType { get; set; }
	public DateTime Registered { get; set; }
	public DateTimeOffset? ActiveDataOffset { get; set; }
	public string Summary { get; set; } = RandomSentenceGenerator.Generate();
	/// <summary>
	/// Returns row CSS if price over 3500
	/// </summary>
	public string? RowClass => this.Balance > 10000 ? "table-danger" : null;

}

public class Name {
	public string First { get; set; } = "";
	public string Last { get; set; } = "";
	public override string ToString() {
		return $"{this.First} {this.Last}";
	}
}

public enum CreditCard {
	none = 0,
	[Description("Master Card")]
	MasterCard = 1,
	Visa = 2
}


/*


	"id": 1,
	"name": {
	  "first": "Trujillo",
	  "last": "Cooley"
	},
	"fullName": "Trujillo Cooley",
	"company": "Vicon",
	"email": "trujillo.cooley@vicon.me",
	"address": "823 Sunnyside Avenue, Lynn, Illinois, 9409",
	"paid": false,
	"balance": "2400.57",
	"creditCardType": 1,
	"registered": "2014-01-03T08:07:49Z",
	"activeDataOffset": "2018-06-27T07:08:42Z"


*/