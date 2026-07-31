using EarthCountriesInfo;

namespace StorageConnector.Services.AWS
{
	public sealed class AmazonS3BucketSettings
	{
		public required Dictionary<CountryIsoCode, string> CountryIsoCodeMapToAccountName { get; init; }
		public required List<AmazonS3Account> Accounts { get; init; }
		public AwsCredentials? AwsCredentials { get; init; }
	}

	public sealed class AwsCredentials
	{
		public required string AccessKey { get; init; }
		public required string SecretAccessKey { get; init; }
		public bool HasCredentials => !string.IsNullOrWhiteSpace(AccessKey) && !string.IsNullOrWhiteSpace(SecretAccessKey);
	}

	public sealed class AmazonS3Account
	{
		public required string BucketName { get; init; }
		public required string AwsRegion { get; init; }

		/// <summary>
		/// Optional. Rekognition is not offered in every region, so it can be pointed at a different
		/// one; when unset, <see cref="AwsRegion"/> is used.
		/// </summary>
		public string? AwsRegionRekognition { get; init; }

		public AwsCredentials? AwsCredentials { get; init; }
	}
}
