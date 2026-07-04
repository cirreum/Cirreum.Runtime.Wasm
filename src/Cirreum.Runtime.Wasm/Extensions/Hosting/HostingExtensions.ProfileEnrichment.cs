namespace Cirreum.Runtime;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

public static partial class HostingExtensions {

	/// <summary>
	/// Registers the <typeparamref name="TEnricher"/> as the <see cref="IUserProfileEnricher"/>
	/// enrichment service.
	/// </summary>
	/// <typeparam name="TEnricher">
	/// The enricher implementation that populates extended profile properties from identity claims.
	/// </typeparam>
	/// <param name="builder">The profile enrichment builder.</param>
	/// <returns>The builder for method chaining.</returns>
	/// <remarks>
	/// The registered enricher is invoked by the <see cref="IInitializationOrchestrator"/>
	/// during Phase 1 of application initialization, after the application user has been loaded.
	/// </remarks>
	public static IUserProfileEnrichmentBuilder WithEnricher<TEnricher>(this IUserProfileEnrichmentBuilder builder)
			where TEnricher : class, IUserProfileEnricher {

		builder.Services.Replace(ServiceDescriptor.Scoped<IUserProfileEnricher, TEnricher>());
		return builder;
	}

	/// <summary>
	/// Registers the <see cref="ClaimsUserProfileEnricher"/> as the <see cref="IUserProfileEnricher"/>
	/// enrichment service.
	/// </summary>
	/// <param name="builder">The profile enrichment builder.</param>
	/// <returns>The builder for method chaining.</returns>
	public static IUserProfileEnrichmentBuilder WithClaimsEnrichment(this IUserProfileEnrichmentBuilder builder) {

		return builder.WithEnricher<ClaimsUserProfileEnricher>();

	}

}
