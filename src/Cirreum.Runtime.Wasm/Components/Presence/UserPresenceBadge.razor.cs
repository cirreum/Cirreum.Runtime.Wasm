namespace Cirreum.Components.Presence;

using Cirreum.Presence;
using Microsoft.AspNetCore.Components;

/// <summary>
/// The <see cref="UserPresenceBadge"/> component is used to display a status indicator such as available, away, or busy.
/// </summary>
public partial class UserPresenceBadge {

	/// <summary>
	/// Child content of component, the content that the badge will be applied to.
	/// </summary>
	[Parameter]
	public RenderFragment? ChildContent { get; set; }

	/// <summary>
	/// Gets or sets the title to show on hover.
	/// If not provided, the <see cref="StatusTitle"/> will be used.
	/// </summary>
	[Parameter]
	public string? Title { get; set; }

	/// <summary>
	/// Gets or sets the title to show on hover. If not provided, the status will be used.
	/// </summary>
	[Parameter]
	public string? StatusTitle { get; set; }

	/// <summary>
	/// Gets or sets the status to show. See <see cref="PresenceStatus"/> for options.
	/// </summary>
	[Parameter]
	public PresenceStatus? Status { get; set; }

	/// <summary>
	/// Gets or sets the <see cref="Status"/> size to use.
	/// Default is Small.
	/// </summary>
	[Parameter]
	public PresenceBadgeSize Size { get; set; } = PresenceBadgeSize.Small;

	/// <summary>
	/// Modifies the display to indicate that the user is out of office. 
	/// This can be combined with any status to display an out-of-office version of that status.
	/// </summary>
	[Parameter]
	public bool OutOfOffice { get; set; } = false;

	private string? ResolvedTitle =>
		(string.IsNullOrEmpty(this.Title) ?
		(string.IsNullOrEmpty(this.StatusTitle) ?
		"" :
		this.StatusTitle) :
		this.Title);

	private MarkupString GetIconInstance() {

		if (this.Status is null) {
			return new MarkupString();
		}

		var iconSvg = this.Status switch {
			PresenceStatus.Available => this.OutOfOffice
								 ? UserPresenceIcons.OpenAvailable
								 : UserPresenceIcons.NormalAvailable,

			PresenceStatus.Busy => this.OutOfOffice
								 ? UserPresenceIcons.OpenBusy
								 : UserPresenceIcons.NormalBusy,

			PresenceStatus.OutOfOffice => this.OutOfOffice
								 ? UserPresenceIcons.OpenOutOfOffice
								 : UserPresenceIcons.NormalOutOfOffice,

			PresenceStatus.Away => this.OutOfOffice
								 ? UserPresenceIcons.OpenAway
								 : UserPresenceIcons.NormalAway,

			PresenceStatus.Offline => this.OutOfOffice
								 ? UserPresenceIcons.OpenOffline
								 : UserPresenceIcons.NormalOffline,

			PresenceStatus.DoNotDisturb => this.OutOfOffice
								 ? UserPresenceIcons.OpenDoNotDisturb
								 : UserPresenceIcons.NormalDoNotDisturb,

			PresenceStatus.Unknown => this.OutOfOffice
								 ? UserPresenceIcons.OpenUnknown
								 : UserPresenceIcons.NormalUnknown,

			_ => UserPresenceIcons.NormalUnknown

		};

		return (MarkupString)iconSvg.Replace("{size}", ((int)this.Size).ToString());

	}

}