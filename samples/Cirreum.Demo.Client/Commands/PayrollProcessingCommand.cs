namespace Cirreum.Demo.Client.Commands;

using Cirreum.Conductor;
using Cirreum.Demo.Client.Authorization;

[BusinessHoursOnly]
public class PayrollProcessingCommand : IAuthorizableRequest {
	public int PayPeriodId { get; set; }
}