namespace Cirreum.Demo.Client.Pages;

using Cirreum.Components.ViewModels;
using Cirreum.Demo.Client.Domain.Users;
using Microsoft.AspNetCore.Components.Forms;

public partial class ViewModels {

	UserViewModel viewModel = default!;

	private void OnInvalidSubmit(EditContext context) {
		this.Dialogr.ShowAlert<FormInvalidAlert>(new() {
			{ nameof(FormInvalidAlert.Message), "Form is not valid" },
			{ nameof(FormInvalidAlert.ErrorMessages), context.GetValidationMessages() },
			{ nameof(FormInvalidAlert.Icon), "exclamation-circle-fill" },
			{ nameof(FormInvalidAlert.Color), BackgroundColor.Danger }
		});
	}

	private async Task OnValidSubmit(EditContext context) {
		var json = context.Model.ToJson();
		Console.WriteLine($"Form submitted with data: {json}");
		await Task.Delay(250);
		var vm = (IViewModel)context.Model;
		await vm.Reset();
		this.Toastr.ShowPrimary("Form was successfully submitted.", "Form", "Submission");
	}

	protected override void OnInitialized() {
		this.viewModel = new UserViewModel();
	}

	//// if you don't want the state to persist outside of this page
	//protected override void Dispose(bool disposing) {
	//	this.sessionViewModel.Clear();
	//	base.Dispose(disposing);
	//}

}