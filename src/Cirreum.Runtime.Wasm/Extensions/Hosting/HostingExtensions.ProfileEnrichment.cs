namespace Cirreum.Runtime;

using Cirreum.Runtime.Authentication.PostProcessors;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

public static partial class HostingExtensions {

	/// <summary>
	/// Registers the <typeparamref name="TEnricher"/> as the <see cref="IUserProfileEnricher"/>
	/// enrichment service.
	/// </summary>
	/// <typeparam name="TEnricher"></typeparam>
	/// <param name="builder"></param>
	/// <returns></returns>
	public static IUserProfileEnrichmentBuilder WithEnricher<TEnricher>(this IUserProfileEnrichmentBuilder builder)
			where TEnricher : class, IUserProfileEnricher {

		builder.Services.Replace(ServiceDescriptor.Scoped<IUserProfileEnricher, TEnricher>());

		// Auto-Enrich after authentication
		builder.Services.TryAddEnumerable(
			ServiceDescriptor
				.Scoped<IAuthenticationPostProcessor, ProfileEnrichmentProcessor>());

		return builder;
	}

	/// <summary>
	/// Registers the <see cref="ClaimsUserProfileEnricher"/> as the <see cref="IUserProfileEnricher"/>
	/// enrichment service.
	/// </summary>
	/// <param name="builder"></param>
	/// <returns></returns>
	public static IUserProfileEnrichmentBuilder WithClaimsEnrichment(this IUserProfileEnrichmentBuilder builder) {

		return builder.WithEnricher<ClaimsUserProfileEnricher>();

	}

}