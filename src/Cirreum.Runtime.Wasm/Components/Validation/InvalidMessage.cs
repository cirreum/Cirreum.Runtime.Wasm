namespace Cirreum.Components.Validation;

using Cirreum.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Rendering;
using System.Linq.Expressions;

/// <summary>
/// Displays a list of validation messages for a specified field within a cascaded <see cref="EditContext"/>.
/// </summary>
public class InvalidMessage<TValue> : ComponentBase, IDisposable {

	private EditContext? _previousEditContext;
	private Expression<Func<TValue>>? _previousFieldAccessor;
	private readonly EventHandler<ValidationStateChangedEventArgs>? _validationStateChangedHandler;
	private FieldIdentifier _fieldIdentifier;

	/// <summary>
	/// Gets or sets a collection of additional attributes that will be applied to the created <c>div</c> element.
	/// </summary>
	[Parameter(CaptureUnmatchedValues = true)] public IReadOnlyDictionary<string, object>? AdditionalAttributes { get; set; }

	[CascadingParameter] EditContext CurrentEditContext { get; set; } = default!;

	/// <summary>
	/// Specifies the field for which validation messages should be displayed.
	/// </summary>
	[Parameter] public Expression<Func<TValue>>? For { get; set; }

	/// <summary>`
	/// Constructs an instance of <see cref="InvalidMessage{TValue}"/>.
	/// </summary>
	public InvalidMessage() {
		this._validationStateChangedHandler = (sender, eventArgs) => this.StateHasChanged();
	}

	/// <inheritdoc />
	protected override void OnParametersSet() {
		if (this.CurrentEditContext == null) {
			throw new InvalidOperationException($"{this.GetType()} requires a cascading parameter " +
				$"of type {nameof(EditContext)}. For example, you can use {this.GetType()} inside " +
				$"an {nameof(EditForm)}.");
		}

		if (this.CurrentEditContext != this._previousEditContext) {
			this.DetachValidationStateChangedListener();
			this.CurrentEditContext.OnValidationStateChanged += this._validationStateChangedHandler;
			this._previousEditContext = this.CurrentEditContext;
		}

		if (this.For == null) {
			// Not possible except if you manually specify T
			throw new InvalidOperationException($"{this.GetType()} requires a value for the " +
				$"{nameof(this.For)} parameter.");
		} else if (this.For != this._previousFieldAccessor) {
			FieldIdentifier? identifier = null;
			var fieldIdProvider = this._previousEditContext.Model as IFieldIdentifierProvider;
			if (fieldIdProvider is not null) {
				var memberExpression = this.For.Body as MemberExpression
					?? throw new ArgumentException("Expression must be a member access", nameof(this.For));
				var propertyPath = InvalidMessage<TValue>.GetPropertyPath(memberExpression);  // Gets "HomeAddress.City"
				identifier = fieldIdProvider.GetFieldIdentifier(propertyPath);
			}
			if (!identifier.HasValue) {
				identifier = FieldIdentifier.Create(this.For);
			}
			if (identifier.HasValue) {
				this._fieldIdentifier = identifier.Value;
				this._previousFieldAccessor = this.For;
			}
		}

	}

	/// <inheritdoc />
	protected override void BuildRenderTree(RenderTreeBuilder builder) {
		foreach (var message in this.CurrentEditContext.GetValidationMessages(this._fieldIdentifier)) {
			builder.OpenElement(0, "div");
			builder.AddAttribute(1, "class", "invalid-message");
			builder.AddMultipleAttributes(2, this.AdditionalAttributes);

			builder.OpenElement(200, "i");
			builder.AddAttribute(201, "class", "bi bi-exclamation-circle-fill");
			builder.AddAttribute(202, "aria-hidden", "true");
			builder.CloseElement();

			builder.OpenElement(300, "span");
			builder.AddContent(301, message);
			builder.CloseElement();

			builder.CloseElement();
		}
	}

	/// <summary>
	/// Called to dispose this instance.
	/// </summary>
	/// <param name="disposing"><see langword="true"/> if called within <see cref="IDisposable.Dispose"/>.</param>
	protected virtual void Dispose(bool disposing) {
	}

	void IDisposable.Dispose() {
		this.DetachValidationStateChangedListener();
		this.Dispose(disposing: true);
	}

	private void DetachValidationStateChangedListener() {
		if (this._previousEditContext != null) {
			this._previousEditContext.OnValidationStateChanged -= this._validationStateChangedHandler;
		}
	}

	private static string GetPropertyPath(Expression expression) {
		var segments = new List<string>();
		var current = expression;

		while (current is MemberExpression memberExp) {
			// Stop if we hit a constant (instance reference)
			if (memberExp.Expression is ConstantExpression) {
				break;
			}

			segments.Add(memberExp.Member.Name);
			current = memberExp.Expression;
		}

		segments.Reverse();
		return string.Join(".", segments);
	}

}