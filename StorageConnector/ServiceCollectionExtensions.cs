using Microsoft.Extensions.DependencyInjection;
using StorageConnector.Services.AWS;
using StorageConnector.Services.Azure;
using StorageConnector.Services.GCP;

namespace StorageConnector
{
	// Renamed from "SeviceCollectionExtensions" (finding L1).
	public static class ServiceCollectionExtensions
	{
		/// <summary>
		/// Registers StorageConnector and its providers. Configuration is read from the
		/// <c>StorageConnectors</c> section of the application's <c>IConfiguration</c>.
		/// </summary>
		/// <param name="services">The service collection to register into.</param>
		/// <returns>
		/// The same <see cref="IServiceCollection"/>, so registration can be chained (finding L3).
		/// </returns>
		public static IServiceCollection AddStorageConnectors(this IServiceCollection services)
		{
			services.AddSingleton<AzureBlobStoragesInitializer>();
			services.AddSingleton<AzureBlobStorageService>();

			services.AddSingleton<AmazonS3BucketsInitializer>();
			services.AddSingleton<AmazonS3BucketService>();

			services.AddSingleton<GCPStoragesInitializer>();
			services.AddSingleton<GCPStorageService>();

			services.AddSingleton<StorageConnectorService>();

			return services;
		}
	}
}
