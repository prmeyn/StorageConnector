using Microsoft.Extensions.Configuration;
using StorageConnector.Common;

namespace StorageConnector.Services.GCP
{
	public sealed class GCPStoragesInitializer
	{
		internal readonly GCPStorageSettings GCPStorageSettings;

		public GCPStoragesInitializer(IConfiguration configuration)
		{
			var azureConfig = configuration.GetSection($"{ConstantStrings.StorageConnectorsConfigName}:GCP");
			GCPStorageSettings = new GCPStorageSettings
			{
				CountryIsoCodeMapToAccountName = (azureConfig.GetSection("CountryIsoCodeMapToAccountName").Get<Dictionary<string, string>>()).ParseCountryIsoCodeMap(),
				Accounts = azureConfig.GetRequiredSection("Accounts").Get<List<GCPAccount>>()
			};
		}
	}
}
