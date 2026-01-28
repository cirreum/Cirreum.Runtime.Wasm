namespace Cirreum.Runtime.Authentication.PostProcessors;

using Cirreum.Runtime.Security;
using Cirreum.Security;
using Microsoft.Extensions.DependencyInjection;
using System.Threading;
using System.Threading.Tasks;

sealed class ProfileEnrichmentProcessor : IAuthenticationPostProcessor {

	public int Order { get; } = 200;

	public async Task ProcessAsync(IServiceProvider serviceProvider, IUserState userState, CancellationToken cancellationToken = default) {
		var enricher = serviceProvider.GetService<IUserProfileEnricher>();
		if (enricher != null && userState is ClientUser clientUser) {
			try {
				await enricher.EnrichProfileAsync(clientUser.Profile, clientUser.Identity!);
			} catch {
			} finally {
				clientUser.SetEnrichmentCompleted();
			}
		}
	}
}