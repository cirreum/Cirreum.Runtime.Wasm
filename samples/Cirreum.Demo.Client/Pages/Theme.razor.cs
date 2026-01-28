namespace Cirreum.Demo.Client.Pages;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using System.ComponentModel.DataAnnotations;

public partial class Theme {

	private Accordion accordion1 = default!;
	private bool Expandable => (this.ExpandMode == AccordionExpandMode.Multiple);
	private void SetExpandable(bool value) {
		this.ExpandMode = value
			? AccordionExpandMode.Multiple
			: AccordionExpandMode.Single;
	}
	private void HandleAccordionItemsChanged(AccordionItem item) {
		this.Toastr.ShowInfo("Accordion Changed", item.Header, $"Expanded: {item.IsExpanded}");
	}
	private AccordionExpandMode ExpandMode { get; set; } = AccordionExpandMode.Single;

	private TabType tabType = TabType.Tabs;
	private TabPosition tabPosition = TabPosition.Top;
	private bool tabsFullWidth = false;
	private bool tabsJustified = false;

	private ElementReference PopoverLeftElement { get; set; }
	private ElementReference PopoverTopElement { get; set; }
	private ElementReference PopoverRightElement { get; set; }
	private ElementReference PopoverBottomElement { get; set; }

	private Popover PopoverLeft = default!;
	private Popover PopoverTop = default!;
	private Popover PopoverRight = default!;
	private Popover PopoverBottom = default!;

	private static void TogglePopover(Popover po) {
		if (po.IsOpen) {
			po.Close();
			return;
		}
		po.Open();
	}

	private static readonly Guid rootId = Guid.Empty;
	private static readonly Guid jasonId = Guid.NewGuid();
	private static readonly Guid tannerId = Guid.NewGuid();
	private static readonly Guid ritoId = Guid.NewGuid();
	private static readonly Guid scott2Id = Guid.NewGuid();
	private static readonly Guid glenId = Guid.NewGuid();
	private static readonly Guid jenId = Guid.NewGuid();
	private static readonly Guid scottId = Guid.NewGuid();
	private static readonly Guid natalieId = Guid.NewGuid();
	private static readonly Guid parkerId = Guid.NewGuid();
	private static readonly Guid heatherId = Guid.NewGuid();
	private static readonly Guid madalynId = Guid.NewGuid();
	private static readonly Guid tristanId = Guid.NewGuid();
	private static readonly Guid recordTxtId = Guid.NewGuid();
	private static readonly Guid chuckId = Guid.NewGuid();

	private static readonly string TenantId_01 = "Tenant01";
	private static readonly string TenantId_02 = "Tenant02";
	private static readonly string TenantId_03 = "Tenant03";

	private List<FileData> SourceFiles = [];
	private List<FileData> SelectedFolderData = [];
	private readonly List<FileData> SelectedData = [];
	private List<TreeViewModel> Items = [];
	private TreeViewModel? SelectedFolder { get; set; }

	private void SelectFolder(TreeViewModel? model) {

		if (model is null) {
			if (this.SelectedFolder is not null) {
				this.Toastr.Show($"Folder '{this.SelectedFolder.Name}' was un-selected.", "TreeView",
				$"Children: {this.SelectedFolder.Children.Count}.");
			} else {
				this.Toastr.Show($"Event triggered for an unknown reason?", "TreeView");
			}
		} else {
			this.Toastr.Show(
				$"Folder '{model.Name}' was selected.",
				"TreeView",
				$"Children: {model.Children.Count}.");
		}

		this.SelectedFolder = model;

		this.SetSelectedFolderData();

	}

	private string SelectedFolderName {
		get {
			if (this.SelectedFolder is not null) {
				return this.SelectedFolder.Name;
			}
			return "";
		}
	}

	private string SelectedFolderTitle {
		get {
			if (this.SelectedFolder is not null) {
				return $"User [{this.SelectedFolder.Name}]";
			}
			return "No User Selected";
		}
	}

	private void ContextMenuItemSelected(MenuItemEventArgs<TreeViewModel> item) {

		if (item.MenuItem.Value == "Delete") {
			if (this.TreeviewRef.RemoveNode(item.Context)) {
				var index = this.SelectedFolderData.FindIndex(d => d.Id == item.Context.NodeId);
				if (index >= 0) {
					this.SelectedFolderData.RemoveAt(index);
				}
				this.Toastr.ShowSuccess(
					$"Deleted folder {item.Context.Name}",
					"Folders",
					"Delete");
				return;
			}
			this.Toastr.ShowWarning(
				$"Failed to delete folder {item.Context.Name}",
				"Folders",
				"Delete");
			return;
		}

		this.Toastr.Show(
			$"Folder action '{item.MenuItem.Value}' was selected",
			"TreeView",
			item.Context.Name);

	}
	private void SetSelectedFolderData() {

		this.SelectedData.Clear();

		var id = this.SelectedFolder?.NodeId;
		this.SelectedFolderData = id is not null
			? [.. this.SourceFiles
				.Where(r => r.ParentId == id)
				.OrderBy(r => r.IsFile is false)
				.ThenBy(r => r.Name)]
			: [];

	}

	private void HandleNodeExpanded(TreeViewModel model) {
		this.Toastr.Show(
			$"Node '{model.Name}' was expanded",
			"TreeView",
			"Expand");
	}

	private void HandleNodeCollapsed(TreeViewModel model) {
		this.Toastr.Show(
			$"Node '{model.Name}' was collapsed",
			"TreeView",
			"Collapse");
	}


	private async Task RefreshDataSourceAsync() {

		await Task.Delay(3000);

		this.SetSelectedFolderData();

	}

	private async Task ExportDataGrid() {
		if (this.SelectedFolderData is not null) {
			var csvBytes = this.CsvBuilder.BuildFile(this.SelectedFolderData);
			await this.WasmFileSystem.DownloadFileAsync(csvBytes, "FileListing.csv", "text/csv");
		}
	}

	private void ClearFolders() {
		this.Items = [];
	}
	private void ResetFolders() {
		this.SourceFiles = GetFileData();
		this.Items = this.BuildTreeView(rootId);
	}

	private bool ContainsText { get; set; } = false;
	private bool CaseSensitive { get; set; } = false;
	private bool IncludeSelectedNodes { get; set; } = false;
	private bool IncludeHiddenNodes { get; set; } = false;
	private bool IncludeDisabled { get; set; } = false;
	private TreeView TreeviewRef { get; set; } = default!;
	private void ExpandAll() {
		this.TreeviewRef.ExpandAll();
	}
	private void CollapseAll() {
		this.TreeviewRef.CollapseAll();
	}
	private async Task SelectRandomNode() {
		var randomModel = this.Items.GetRandomElementRecursive();
		await this.TreeviewRef.SelectNodeAsync(randomModel);
	}
	private async Task HandleTreeViewSearchTextChanged(string newText) {
		if (newText.HasValue()) {
			var selected = await this.TreeviewRef.SelectNodeByName(
				newText,
				this.ContainsText,
				this.IncludeSelectedNodes,
				this.IncludeHiddenNodes,
				this.IncludeDisabled,
				this.CaseSensitive);
			if (!selected) {
				this.Toastr.Show("Node not found, nothing selected.", "TreeView", "Search");
			}
		}
	}

	private bool DGResponsive { get; set; } = true;
	private bool DGFlush { get; set; } = true;
	private bool DGPageable { get; set; } = false;
	private bool ShowIcons { get; set; } = true;
	private bool ShowTreeViewHeader { get; set; } = true;
	private bool ShowTreeViewFooter { get; set; } = true;
	private bool ShowTreeViewNodeHeaders { get; set; } = true;


	private List<TreeViewModel> BuildTreeView(Guid parentId) {

		var models = new List<TreeViewModel>();

		foreach (var row in this.SourceFiles.Where(f => f.ParentId.Equals(parentId) && f.IsFile is false)) {
			var model = this.CreateTreeViewModel(row);
			models.Add(new() {
				NodeId = Guid.NewGuid(),
				ParentId = parentId,
				Name = model.HasChildren ? "Has Children" : "No Children",
				IsHeaderNode = true,
				ShowHeaderNodeDivider = row.Name != "Glen"
			});
			models.Add(model);
		}

		return models;

	}

	private TreeViewModel CreateTreeViewModel(FileData row) {

		var children = this.SourceFiles
				.Where(r => r.ParentId.Equals(row.Id) && r.IsFile is false)
				.Select(r => this.CreateTreeViewModel(r))
				.ToList();

		if (children.Count > 0) {
			children.Insert(0, new() {
				NodeId = Guid.NewGuid(),
				ParentId = row.ParentId,
				Name = "Children",
				IsHeaderNode = true
			});
		}
		TreeViewModel model = new() {
			NodeId = row.Id,
			ParentId = row.ParentId,
			Name = row.Name,
			IsSelected = row.Name.Equals("Heather") || row.Name.Equals("Natalie") || row.Name.Equals("Tanner"),
			IsDisabled = row.Name.Equals("Rito"),
			Description = "path: " + row.Path,
			DisplayChildrenCount = row.IsFile is false && children.Count > 0,
			ItemIconCss = (selected, expanded) => {
				if (expanded) {
					return "bi bi-folder-fill";
				}
				return "bi bi-folder";
			},
			ItemIconColorCss = (selected, expanded) => selected ? "text-primary" : "",
			ItemIconImageUrl = row.Name == "Glen" ?
				(selected, expanded) => "favicon-16x16.png" :
				null,
			ItemBadgeColorCss = (selected, expanded) => children.Count == 1 ? "text-bg-danger" : "text-bg-primary",
			Children = children
		};

		return model;

	}

	private static List<FileData> GetFileData() {
		return [
			new FileData(glenId, rootId, TenantId_01, "/glen", "Glen", "File Folder", "", 0, false, false, null, DateTime.Today.Subtract(TimeSpan.FromDays(5)), DateTime.Today),
			new FileData(jenId, glenId, TenantId_01, "/glen/jen", "Jen", "File Folder", "", 0, false, false, null, DateTime.Today.Subtract(TimeSpan.FromDays(4)), DateTime.Today),
			new FileData(scottId, glenId, TenantId_01, "/glen/scott", "Scott", "File Folder", "", 0, false, false, null, DateTime.Today.Subtract(TimeSpan.FromDays(3)), DateTime.Today),
			new FileData(natalieId, scottId, TenantId_01, "/glen/scott/natalie", "Natalie", "File Folder", "", 0, false, false, null, DateTime.Today.Subtract(TimeSpan.FromDays(1)), DateTime.Today),
			new FileData(parkerId, scottId, TenantId_01, "/glen/scott/parker", "Parker", "File Folder", "", 0, false, false, null, DateTime.Today.Subtract(TimeSpan.FromDays(2)), DateTime.Today),
			new FileData(heatherId, glenId, TenantId_01, "/glen/heather", "Heather", "File Folder", "", 0, false, false, null, DateTime.Today.Subtract(TimeSpan.FromDays(3)), DateTime.Today),
			new FileData(madalynId, heatherId, TenantId_01, "/glen/heather/madalyn", "Madalyn", "File Folder", "", 0, false, false, null, DateTime.Today.Subtract(TimeSpan.FromDays(1)), DateTime.Today),
			new FileData(tristanId, heatherId, TenantId_01, "/glen/heather/tristan", "Tristan", "File Folder", "", 0, false, false, null, DateTime.Today.Subtract(TimeSpan.FromDays(2)), DateTime.Today),
			new FileData(recordTxtId, tristanId, TenantId_01, "/glen/heather/tristan/record.txt", "record.txt", "Text Document", ".txt", 234543, true, false, null, DateTime.Today.Subtract(TimeSpan.FromDays(2)), DateTime.Today),
			new FileData(jasonId, rootId, TenantId_02, "/jason", "Jason", "File Folder", "", 0, false, false, null, DateTime.Today.Subtract(TimeSpan.FromDays(5)), DateTime.Today),
			new FileData(ritoId, jasonId, TenantId_02, "/rito", "Rito", "File Folder", "", 0, false, false, null, DateTime.Today.Subtract(TimeSpan.FromDays(5)), DateTime.Today),
			new FileData(tannerId, jasonId, TenantId_02, "/tanner", "Tanner", "File Folder", "", 0, false, false, null, DateTime.Today.Subtract(TimeSpan.FromDays(5)), DateTime.Today),
			new FileData(scott2Id, tannerId, TenantId_02, "/scott2", "Scott 2", "File Folder", "", 0, false, false, null, DateTime.Today.Subtract(TimeSpan.FromDays(5)), DateTime.Today),
			new FileData(chuckId, rootId, TenantId_03, "/chuck", "Chuck", "File Folder", "", 0, false, false, null, DateTime.Today.Subtract(TimeSpan.FromDays(5)), DateTime.Today),
		];
	}

	private bool OffcanvasShow { get; set; } = false;
	bool offcanvasEnd;
	//bool addedNoscroll;
	internal void ToggleOffcanvasShow() {
		this.OffcanvasShow = !this.OffcanvasShow;
		this.StateHasChanged();
		if (this.OffcanvasShow) {
			this.JSApp.SetElementClassIfScrollbar("body", true, "noscroll");
			//this.addedNoscroll = true;
			return;
		}
		if (this.OffcanvasShow is false) {
			this.JSApp.RemoveElementClass("body", "noscroll");
			//this.addedNoscroll = false;
		}
	}

	private string OffCanvasCss => CssBuilder
		.Default("offcanvas")
			.AddClass("offcanvas-start", when: this.offcanvasEnd is false)
			.AddClass("offcanvas-end", when: this.offcanvasEnd is true)
			.AddClass("show", when: this.OffcanvasShow)
		.Build();
	private string OffCanvasBackdropCss => CssBuilder
		.Default("offcanvas-backdrop fade")
			.AddClass("show", when: this.OffcanvasShow)
			.AddClass("collapse", when: this.OffcanvasShow is false)
		.Build();

	private static ToastOptions ToastrOptions =>
		new ToastOptions() {
			StyleType = ToastStyleType.Info,
			DisableTimeout = true,
			Progress = 28
		};

	private void HandleActionMenuItemSelected(string value) {
		this.Toastr.ShowInfo($"Dropdown Item Selected: {value}", "Action", "Selection");
	}
	private void HandleSplitButtonClicked(IDropdown<string> dropdown) {
		this.Toastr.ShowInfo($"Split Button Action Clicked: {dropdown.SelectedValue}", "Action", dropdown.SelectedValue);
	}

	private EditContext MyEditContext { get; set; } = default!;

	private string RadioOption1Id { get; set; } = IdGenerator.Next;

	[Required(ErrorMessage = "Please choose an option...")]
	public string RadioOptionsValue { get; set; } = "";

	[Required(ErrorMessage = "Please agree to continue...")]
	[Range(typeof(bool), "true", "true",
		ErrorMessage = "Please set to continue")]
	public bool RequiredCheck { get; set; }

	[Required(ErrorMessage = "Please set to continue...")]
	[Range(typeof(bool), "true", "true",
		ErrorMessage = "Please set to continue")]
	public bool RequiredSwitch { get; set; }

	private bool InlineSwitch { get; set; }

	private bool IsDialogVisible { get; set; }
	private Dialog? _dialogRef;
	private async Task OnOpen() {
		await this._dialogRef!.ShowAsync();
	}
	private async Task OnClose() {
		await this._dialogRef!.HideAsync();
	}

	private void HandleValidSubmit() {
		this.Toastr.ShowSuccess("The form passed validation and would have been submitted.", "Form", "submitted");
	}
	private void HandleInvalidSubmit() {
		this.Toastr.ShowWarning("The form failed validation and would NOT have been submitted.", "Form", "submitted");
	}

	protected override void OnInitialized() {
		this.InvokeAsync(() => {
			this.ResetFolders();
		});
		this.MyEditContext = new EditContext(this);
		this.MyEditContext.SetFieldCssClassProvider(new ValidationFieldCssProvider());
	}
	//protected override void OnAfterRender(bool firstRender) {
	//	if (firstRender) {
	//		this._dialogRef!.HideAsync();
	//	}
	//}

}