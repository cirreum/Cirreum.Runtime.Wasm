namespace Cirreum.Demo.Client.Commands;

using System.Threading;
using System.Threading.Tasks;

public record TransientCommand(int Delay) : IOperation<string>;

public class TestTransientDisposables : IOperationHandler<TransientCommand, string> {

	public async Task<Result<string>> HandleAsync(TransientCommand request, CancellationToken cancellationToken) {
		await Task.Delay(request.Delay, cancellationToken);
		Console.WriteLine($"TestTransientDisposables => Handle({request.Delay})");
		return Result<string>.Success("Unknown");
	}

}