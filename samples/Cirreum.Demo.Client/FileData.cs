namespace Cirreum.Demo.Client;

public record FileData(
	Guid Id,
	Guid ParentId,
	string TenantId,
	string Path,
	string Name,
	string Type,
	string FileExtension,
	long FileSize,
	bool IsFile,
	bool IsDeleted,
	DateTime? DeletedDate,
	DateTime UploadedDate,
	DateTime ModifiedDate
	) {

	// Not persisted...
	public List<FileData> Children { get; set; } = [];

}