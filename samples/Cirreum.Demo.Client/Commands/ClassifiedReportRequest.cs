namespace Cirreum.Demo.Client.Commands;

using Cirreum.Conductor;
using Cirreum.Demo.Client.Authorization;

public record ClassifiedReport();

[SecurityClearanceRequired(SecurityClearanceLevel.Confidential)]
public record ClassifiedReportRequest(
	string ReportType
) : IAuthorizableRequest<ClassifiedReport>;