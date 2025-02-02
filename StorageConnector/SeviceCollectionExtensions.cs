using EarthCountriesInfo;
using Microsoft.Extensions.DependencyInjection;
using StorageConnector.Services.AWS;
using StorageConnector.Services.Azure;
using StorageConnector.Services.GCP;

namespace StorageConnector
{
    public static class SeviceCollectionExtensions
	{
		public static void AddStorageConnectors(this IServiceCollection services)
		{
			services.AddSingleton<AzureBlobStoragesInitializer>();
			services.AddSingleton<AzureBlobStorageService>();

			services.AddSingleton<AmazonS3BucketsInitializer>();
			services.AddSingleton<AmazonS3BucketService>();

			services.AddSingleton<GCPStoragesInitializer>();
			services.AddSingleton<GCPStorageService>();

			services.AddSingleton<StorageConnectorService>();
		}

		public static Dictionary<CountryIsoCode, string> ParseCountryIsoCodeMap(this Dictionary<string, string>? rawMap)
		{
			var parsedMap = new Dictionary<CountryIsoCode, string>();

			if (rawMap?.Any() ?? false)
			{
				foreach (var kvp in rawMap)
				{
					if (Enum.TryParse(kvp.Key, ignoreCase: true, out CountryIsoCode countryIsoCode))
					{
						parsedMap[countryIsoCode] = kvp.Value;
					}
					else
					{
						// Handle invalid country codes (e.g., log a warning or throw an exception)
						throw new ArgumentException($"Invalid country ISO code: {kvp.Key}");
					}
				}
			}
			return parsedMap;
		}
	}
}
