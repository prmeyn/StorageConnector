using EarthCountriesInfo;

namespace StorageConnector.Services.Azure
{
	public sealed class AzureBlobStorageSettings
	{
		public required Dictionary<CountryIsoCode, string> CountryIsoCodeMapToAccountName { get; init; }
		public required List<AzureAccount> Accounts { get; init; }
	}

	public sealed class AzureAccount
	{
		public required string AccountName { get; init; }
		public required string AccountKey { get; init; }
		public required string ContainerName { get; init; }
	}
}
