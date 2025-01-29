using Microsoft.Extensions.DependencyInjection;
using StorageConnector.Services.Azure;

namespace StorageConnector
{
    public static class SeviceCollectionExtensions
	{
		public static void AddStorageConnectors(this IServiceCollection services)
		{
			services.AddSingleton<AzureBlobStoragesInitializer>();
			services.AddSingleton<AzureBlobStorageService>();
			services.AddSingleton<StorageConnectorService>();
		}
	}
}
