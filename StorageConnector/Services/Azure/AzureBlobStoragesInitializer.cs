using EarthCountriesInfo;
using Microsoft.Extensions.Configuration;

namespace StorageConnector.Services.Azure
{
	public sealed class AzureBlobStoragesInitializer
    {
		internal readonly AzureBlobStorageSettings AzureBlobStorageSettings;

		public AzureBlobStoragesInitializer(IConfiguration configuration)
        {
            var azureConfig = configuration.GetSection("StorageConnectors:Azure");
			AzureBlobStorageSettings = new AzureBlobStorageSettings
			{
				CountryIsoCodeMapToAccountName = ParseCountryIsoCodeMap(azureConfig.GetSection("CountryIsoCodeMapToAccountName").Get<Dictionary<string, string>>()),
				Accounts = azureConfig.GetRequiredSection("Accounts").Get<List<AzureAccount>>()
			};
		}

		private Dictionary<CountryIsoCode, string> ParseCountryIsoCodeMap(Dictionary<string, string>? rawMap)
		{
			var parsedMap = new Dictionary<CountryIsoCode, string>();

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

			return parsedMap;
		}
	}
}
