namespace Cirreum.Components.Validation;

using FluentValidation;
using FluentValidation.Internal;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

/// <summary>
/// Integrates FluentValidation with Blazor forms, supporting validation severity levels
/// and automatic validator resolution from DI.
/// </summary>
public class FluentValidationValidator : ComponentBase, IDisposable {

	[Inject]
	private IServiceProvider ServiceProvider { get; set; } = null!;

	[CascadingParameter]
	private EditContext? CurrentEditContext { get; set; }
	private EditContext? _originalEditContext;

	/// <summary>
	/// If true, validation will consider warnings as validation failures. Default: <see langword="true"/>
	/// </summary>
	[Parameter]
	public bool TreatWarningsAsErrors { get; set; } = true;

	/// <summary>
	/// Callback invoked when the validation state changes, providing information about warnings.
	/// </summary>
	[Parameter]
	public EventCallback<ValidationSeverityResult> OnValidationStateChanged { get; set; }

	private IValidator? _validator;
	private ValidationMessageStore _messageStore = null!;

	protected override void OnInitialized() {
		if (this.CurrentEditContext == null) {
			throw new InvalidOperationException(
				$"{nameof(FluentValidationValidator)} requires a cascading parameter of type {nameof(EditContext)}.");
		}
		this._originalEditContext = this.CurrentEditContext;

		// Auto-resolve the validator based on the model type
		var modelType = this._originalEditContext.Model.GetType();
		var validatorType = typeof(IValidator<>).MakeGenericType(modelType);
		this._validator = this.ServiceProvider.GetService(validatorType) as IValidator;

		if (this._validator == null) {
			return;
		}

		this._messageStore = new ValidationMessageStore(this._originalEditContext);

		this._originalEditContext.OnValidationRequested += this.ValidateModel;
		this._originalEditContext.OnFieldChanged += this.ValidateField;

	}

	/// <inheritdoc />
	protected override void OnParametersSet() {
		// Copied from https://github.com/dotnet/AspNetCore/blob/main/src/Components/Forms/src/DataAnnotationsValidator.cs
		if (this.CurrentEditContext != this._originalEditContext) {
			throw new InvalidOperationException($"{this.GetType()} does not support changing the " +
				$"{nameof(EditContext)} dynamically.");
		}
	}

	private void ValidateModel(object? sender, ValidationRequestedEventArgs args) {
		if (this._validator == null) {
			return;
		}
		if (this._originalEditContext == null) {
			return;
		}

		this._messageStore.Clear();
		var context = new ValidationContext<object>(this._originalEditContext.Model);
		var validationResult = this._validator.Validate(context);

		var hasErrors = false;
		var hasWarnings = false;

		var fieldIdProvider = this._originalEditContext.Model as IFieldIdentifierProvider;
		foreach (var error in validationResult.Errors) {

			FieldIdentifier? identifier = null;
			if (fieldIdProvider is not null) {
				identifier = fieldIdProvider.GetFieldIdentifier(error.PropertyName);
			}
			if (!identifier.HasValue) {
				identifier = new FieldIdentifier(this._originalEditContext.Model, error.PropertyName);
			}
			if (!identifier.HasValue) {
				continue;
			}
			this._messageStore.Add(identifier.Value, error.ErrorMessage);

			if (error.Severity == Severity.Error) {
				hasErrors = true;
			} else if (error.Severity == Severity.Warning) {
				hasWarnings = true;
			}
		}

		this._originalEditContext.NotifyValidationStateChanged();

		// Notify the validation state including severity information
		this.OnValidationStateChanged.InvokeAsync(new ValidationSeverityResult(
			hasErrors,
			hasWarnings,
			hasErrors || (this.TreatWarningsAsErrors && hasWarnings)
		));

	}

	private void ValidateField(object? sender, FieldChangedEventArgs args) {
		if (this._validator == null) {
			return;
		}

		var fieldIdentifier = args.FieldIdentifier;
		var propertyChain = new PropertyChain();
		var selector = new MemberNameValidatorSelector([fieldIdentifier.FieldName]);

		var context = new ValidationContext<object>(
			fieldIdentifier.Model,
			propertyChain,
			selector
		);

		this._messageStore.Clear(fieldIdentifier);
		var validationResult = this._validator.Validate(context);

		var hasErrors = false;
		var hasWarnings = false;

		foreach (var error in validationResult.Errors) {
			if (string.Equals(error.PropertyName, fieldIdentifier.FieldName, StringComparison.OrdinalIgnoreCase)) {
				this._messageStore.Add(fieldIdentifier, error.ErrorMessage);

				if (error.Severity == Severity.Error) {
					hasErrors = true;
				} else if (error.Severity == Severity.Warning) {
					hasWarnings = true;
				}
			}
		}

		this._originalEditContext?.NotifyValidationStateChanged();

		// Notify about the field validation state
		this.OnValidationStateChanged.InvokeAsync(new ValidationSeverityResult(
			hasErrors,
			hasWarnings,
			hasErrors || (this.TreatWarningsAsErrors && hasWarnings)
		));
	}

	public void Dispose() {
		this._messageStore.Clear();
		if (this._originalEditContext != null) {
			this._originalEditContext.OnValidationRequested -= this.ValidateModel;
			this._originalEditContext.OnFieldChanged -= this.ValidateField;
			this._originalEditContext.NotifyValidationStateChanged();
		}
	}

}