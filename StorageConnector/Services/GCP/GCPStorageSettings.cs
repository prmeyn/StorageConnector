using EarthCountriesInfo;

namespace StorageConnector.Services.GCP
{
	public sealed class GCPStorageSettings
	{
		public required Dictionary<CountryIsoCode, string> CountryIsoCodeMapToAccountName { get; init; }
		public required List<GCPAccount> Accounts { get; init; }
	}

	public sealed class GCPAccount
	{
		public required string BucketName { get; init; }
		public required string PrivateKey { get; init; }
		public required string ServiceAccountEmail { get; init; }
	}
}
