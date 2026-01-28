namespace Cirreum.Demo.Client.Pages;

using Cirreum.Demo.Client.Shared;
using System;

public partial class Dialogs {

	// Dialogs

	void OpenWindow() {
		this.Dialogr.ShowWindow<SampleDialog>("Window", "bi bi-window text-info");
	}

	void OpenDialogDraggableNoHeader() {
		var options = DialogOptions.NoHeader
			.WithPosition(DialogPosition.Top)
			.WithModal()
			.WithBackgroundCancel()
			.WithDraggability(DefaultFocusType.First);
		this.Dialogr.Show<SurveyPrompt>("Demo", new ComponentParameters {
			{ nameof(SurveyPrompt.MessagePrefix), "I'm a draggable modal" } },
			options);
	}

	string SelectedDialogWidth { get; set; } = "none";
	void OnDialogWidthSelected(string value) {
		this.SelectedDialogWidth = value;
	}
	void OpenDialogWidthModal() {

		var opt = DialogOptions.AsWindow
			.WithFixedWidth(this.SelectedDialogWidth)
			.WithMaxWidth(this.SelectedDialogSize)
			.WithPosition(this.SelectedDialogPosition);

		this.Dialogr.Show<SampleDialog>(
			$"Width: {this.SelectedDialogWidth}, Max-Width: {this.SelectedDialogSize}",
			new ComponentParameters { { nameof(SampleDialog.DefaultPosition), this.SelectedDialogPosition } },
			opt);

	}

	DialogSize SelectedDialogSize { get; set; } = DialogSize.Default;
	void OnDialogSizeSelected(DialogSize value) {
		this.SelectedDialogSize = value;
	}
	void OpenDialogSizedModal() {

		var opt = DialogOptions.AsScrollableWindow
			.WithFixedWidth(this.SelectedDialogWidth)
			.WithMaxWidth(this.SelectedDialogSize)
			.WithPosition(this.SelectedDialogPosition)
			.WithTitleIcon("bi bi-bank text-warning");

		this.Dialogr.Show<WideDialog>(
			$"Width: {this.SelectedDialogWidth}, Max-Width: {this.SelectedDialogSize}",
			opt);

	}

	DialogPosition SelectedDialogPosition { get; set; } = DialogPosition.Center;
	void OnDialogPositionSelected(DialogPosition value) {
		this.SelectedDialogPosition = value;
	}
	void OpenDialogPositionedModal() {
		this.Dialogr.Show<SampleDialog>(
			"Positioned",
			new ComponentParameters { { nameof(SampleDialog.DefaultPosition), this.SelectedDialogPosition } },
			DialogOptions.AsModal.WithPosition(this.SelectedDialogPosition));
	}


	void OpenCustomDialog() {
		this.Dialogr.ShowWindow<CustomDialog>(
			"Custom",
			new ComponentParameters { { nameof(CustomDialog.ChildContent), this.customContent } },
			"600px");
	}

	async void OpenMultipleDialogs() {

		var options = DialogOptions.AsModal;

		foreach (var item in Enum.GetValues<DialogPosition>()) {
			options.Position = item;
			this.Dialogr.Show<SampleDialog>($"Demo {options.Position}", options);
			await Task.Delay(100);
		}

	}

	void OpenDialogWithOutAnimation() {

		this.Dialogr.Show<SampleDialog>(
			"No Animation",
			DialogOptions.NoAnimation);

	}

	async void OpenQuestionPrompt() {
		var parameters = new ComponentParameters {
			{ nameof(QuestionPrompt.QuestionText) ,"Are you sure?" }
		};
		var dialog = this.Dialogr.ShowPrompt<QuestionPrompt>("Prompt", parameters);
		var result = await dialog.Result;
		if (result.Confirmed is false) {
			this.Toastr.ShowWarning("The user cancelled.", "Prompt", "Question");
			return;
		}

		parameters = new ComponentParameters {
			{ nameof(QuestionPrompt.QuestionText) ,"Are you REALLY sure?" }
		};
		dialog = this.Dialogr.ShowPrompt<QuestionPrompt>("Prompt Again!", parameters);
		result = await dialog.Result;
		if (result.Confirmed) {
			this.Toastr.ShowInfo("User confirmed twice!", "Prompt", "Question");
			return;
		}

		this.Toastr.ShowWarning("The user cancelled!", "Prompt", "Question");

	}

	void OpenAlertPrompt() {
		this.Dialogr.ShowAlert(
			"Hey something happened!",
			BackgroundColor.Danger,
			"exclamation-triangle-fill");
	}

	async Task AddEventModal() {
		var modal = this.Dialogr
			.ShowWindow<AddEventModal>(
				title: "Add Event",
				titleIcon: "bi bi-calendar2-event text-info",
				width: this.SelectedDialogWidth,
				maxWidth: this.SelectedDialogSize);
		await modal.Result;
	}

	void ShowScrollable() {
		this.Dialogr.ShowWindow<ScrollableDialog>
			("Scrollable", "bi bi-tools text-info", scrollable: true);
	}

	// Toasts

	private bool ShowCopyToClipboard { get; set; } = true;

	ToastStyleType SelectedToastType { get; set; } = ToastStyleType.Default;
	void OnToastTypeSelected(ToastStyleType value) {
		this.SelectedToastType = value;
	}

	ToastPosition SelectedToastPosition { get; set; } = ToastPosition.TopRight;
	void OnToastPositionSelected(ToastPosition value) {
		this.SelectedToastPosition = value;
	}

	void ShowPrimary() {
		this.Toastr.ShowPrimary(
			"I am the Primary Appearance!", "Primary", "11 minutes", o => {
				o.ShowCopyToClipboard = this.ShowCopyToClipboard;
				o.Position = this.SelectedToastPosition;
				o.OnClick = this.HandleToastClicked;
			});
	}
	void ShowSecondary() {
		this.Toastr.ShowSecondary(
			"I am the Secondary Appearance!", "Secondary", "11 minutes", o => {
				o.ShowCopyToClipboard = this.ShowCopyToClipboard;
				o.Position = this.SelectedToastPosition;
				o.OnClick = this.HandleToastClicked;
			});
	}
	void ShowDanger() {
		this.Toastr.ShowDanger(
			"This is dangerous stuff!", "Danger", "That was Close!", o => {
				o.ShowCopyToClipboard = this.ShowCopyToClipboard;
				o.Position = this.SelectedToastPosition;
				o.OnClick = this.HandleToastClicked;
			});
	}
	void ShowWarning() {
		this.Toastr.ShowWarning(
			"You've been warned!", "Warning", "Wait what?", o => {
				o.ShowCopyToClipboard = this.ShowCopyToClipboard;
				o.Position = this.SelectedToastPosition;
				o.OnClick = this.HandleToastClicked;
			});
	}
	void ShowInfo() {
		this.Toastr.ShowInfo(
			"Here some good information", "Information", "11 Minutes", o => {
				o.ShowCopyToClipboard = this.ShowCopyToClipboard;
				o.Position = this.SelectedToastPosition;
				o.OnClick = this.HandleToastClicked;
			});
	}
	void ShowSuccess() {
		this.Toastr.ShowSuccess(
			"Yeah, you did it!", "Success", "Way to go!", o => {
				o.ShowCopyToClipboard = this.ShowCopyToClipboard;
				o.Position = this.SelectedToastPosition;
				o.OnClick = this.HandleToastClicked;
			});
	}
	void ShowDark() {
		this.Toastr.ShowDark(
			"My that is dark...", "Dark", "11 minutes", o => {
				o.ShowCopyToClipboard = this.ShowCopyToClipboard;
				o.Position = this.SelectedToastPosition;
				o.OnClick = this.HandleToastClicked;
			});
	}
	void ShowLight() {
		this.Toastr.ShowLight(
			"Oh, that is light...", "Light", "11 minutes", o => {
				o.ShowCopyToClipboard = this.ShowCopyToClipboard;
				o.Position = this.SelectedToastPosition;
				o.OnClick = this.HandleToastClicked;
			});
	}
	void ShowAmbientDefaultToast() {
		this.Toastr.Show(
			"This is my ambient appearance", "Ambient", "Default", o => {
				o.ShowCopyToClipboard = this.ShowCopyToClipboard;
				o.StyleType = ToastStyleType.Default;
				o.Position = this.SelectedToastPosition;
				o.OnClick = this.HandleToastClicked;
			});
	}
	void ShowLongTitleToast() {
		this.Toastr.Show(
			"This is my long titled toast title", "This is my long titled toast title and should overflow the title area", "Default", o => {
				o.ShowCopyToClipboard = this.ShowCopyToClipboard;
				o.StyleType = this.SelectedToastType;
				o.Position = this.SelectedToastPosition;
				o.OnClick = this.HandleToastClicked;
			});
	}
	void ShowDefaultToast() {
		this.Toastr.Show(
			"This is the application configured default", "App", "Default", o => {
				o.ShowCopyToClipboard = this.ShowCopyToClipboard;
				o.Position = this.SelectedToastPosition;
				o.OnClick = this.HandleToastClicked;
			});
	}

	void ShowDisableTimeoutToast() {
		this.Toastr.Show(
			"You will be here for a while... click the close button", "No Timeout", $"{this.SelectedToastType}", o => {
				o.ShowCopyToClipboard = this.ShowCopyToClipboard;
				o.DisableTimeout = true;
				o.StyleType = this.SelectedToastType;
				o.Position = this.SelectedToastPosition;
				o.OnClick = this.HandleToastClicked;
			});
	}
	void ShowDisableProgressToast() {
		this.Toastr.Show(
			"I will expire momentarily...", "No Progress", $"{this.SelectedToastType}", o => {
				o.ShowCopyToClipboard = this.ShowCopyToClipboard;
				o.ShowProgressBar = false;
				o.StyleType = this.SelectedToastType;
				o.Position = this.SelectedToastPosition;
				o.OnClick = this.HandleToastClicked;
			});
	}
	void ShowDisableProgressAndTimeoutToast() {
		this.Toastr.Show(
			"You will be here for a while, but won't know it!", "No Timeout or Progress", $"{this.SelectedToastType}", o => {
				o.ShowCopyToClipboard = this.ShowCopyToClipboard;
				o.StyleType = this.SelectedToastType;
				o.DisableTimeout = true;
				o.ShowProgressBar = false;
				o.Position = this.SelectedToastPosition;
				o.OnClick = this.HandleToastClicked;
			});
	}
	void HandleToastClicked(ToastInstance instance) {
		instance.Subtitle = "Changed!";
		instance.Title = instance.Title.Replace(" [Clicked]", "") + " [Clicked]";
	}

	readonly Guid CustomToastId = Guid.NewGuid();
	void ShowUploadFileProgressToast() {
		this.uploadProgress = 0;
		this.Toastr.Show(
			this.uploadToastMessage,
			"MyTestFile.pdf",
			"Uploading...",
			configure: o => {
				o.ToastId = CustomToastId;
				o.ShowCopyToClipboard = false;
				o.StyleType = this.SelectedToastType;
				o.DisableTimeout = true;
				o.ShowProgressBar = false;
				o.Progress = 0;
				o.Position = this.SelectedToastPosition;
				o.OnClosed += (instance) => {
					this.isOpen = false;
					this.isUpdating = false;
					this.StateHasChanged();
				};
			});
		this.isOpen = true;
	}
	private CancellationTokenSource _cancel = new();
	private int uploadProgress = 0;
	private bool isOpen;
	private bool isUpdating;
	private async Task UpdateUploadFileProgressToast() {
		if (this.isUpdating) {
			return;
		}
		this.isUpdating = true;
		this.StateHasChanged();

		for (var i = 0; i < 101; i++) {
			if (this.isOpen) {
				this.uploadProgress = i;
				this.Toastr.UpdateToast(CustomToastId);
				await Task.Delay(100, this._cancel.Token);
			}
		}

		if (this.isOpen) {
			this.isUpdating = false;
			this.StateHasChanged();
		}
	}
	private void ResetUploadFileProgressToast() {
		if (!this.isUpdating || this._cancel.IsCancellationRequested) {
			return;
		}

		this._cancel.Cancel();
		this.uploadProgress = 0;
		if (this.isOpen) {
			this.Toastr.UpdateToast(CustomToastId);
		}

		this._cancel = new();
		this.isUpdating = false;
		this.StateHasChanged();
	}
	private void CloseUploadFileProgressToast() {
		this.Toastr.Close(CustomToastId);
	}

}