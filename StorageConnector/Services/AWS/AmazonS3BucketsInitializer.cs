using Amazon;
using Amazon.Rekognition;
using Amazon.Runtime;
using Amazon.S3;
using Microsoft.Extensions.Configuration;
using StorageConnector.Common;

namespace StorageConnector.Services.AWS
{
	public sealed class AmazonS3BucketsInitializer
	{
		private const string SectionName = $"{ConstantStrings.StorageConnectorsConfigName}:AWS";

		internal readonly AmazonS3BucketSettings? AmazonS3BucketSettings;

		internal readonly Dictionary<string, AwsClients> AccountNamesMappedToAmazonS3Client = [];

		/// <summary>Face quality thresholds; defaults apply when the section is absent (finding M9).</summary>
		internal readonly FaceQualitySettings FaceQuality = new();

		public AmazonS3BucketsInitializer(IConfiguration configuration)
		{
			var awsOptions = configuration.GetSection(SectionName);
			if (!awsOptions.Exists())
			{
				return;
			}

			FaceQuality = awsOptions.GetSection("FaceQuality").Get<FaceQualitySettings>() ?? new FaceQualitySettings();

			// Optional: credentials may instead be supplied per account.
			var commonCredentials = awsOptions.GetSection("AwsCredentials").Get<AwsCredentials>();
			var commonBasicCredentials = (commonCredentials?.HasCredentials ?? false)
				? new BasicAWSCredentials(commonCredentials.AccessKey, commonCredentials.SecretAccessKey)
				: null;

			var accounts = awsOptions.GetSection(ConstantStrings.AccountsConfigName).Get<List<AmazonS3Account>>();
			if (accounts is null || accounts.Count == 0)
			{
				throw StorageConnectorConfigurationException.MissingSetting(
					$"{SectionName}:{ConstantStrings.AccountsConfigName}",
					"At least one S3 bucket must be configured.");
			}

			AmazonS3BucketSettings = new AmazonS3BucketSettings
			{
				CountryIsoCodeMapToAccountName = awsOptions
					.GetSection(ConstantStrings.CountryIsoCodeMapToAccountNameConfigName)
					.Get<Dictionary<string, string>>()
					.ParseCountryIsoCodeMap($"{SectionName}:{ConstantStrings.CountryIsoCodeMapToAccountNameConfigName}"),
				Accounts = accounts,
				AwsCredentials = commonCredentials
			};

			for (var i = 0; i < accounts.Count; i++)
			{
				var account = accounts[i];
				var path = $"{SectionName}:{ConstantStrings.AccountsConfigName}:{i}";

				// `required` on AmazonS3Account is not enforced by the configuration binder, so these
				// arrive as null and would otherwise surface as an opaque
				// "Value cannot be null. (Parameter 'key')" thrown from inside the AWS SDK.
				if (string.IsNullOrWhiteSpace(account.BucketName))
				{
					throw StorageConnectorConfigurationException.MissingSetting($"{path}:BucketName");
				}

				if (string.IsNullOrWhiteSpace(account.AwsRegion))
				{
					throw StorageConnectorConfigurationException.MissingSetting($"{path}:AwsRegion");
				}

				// Rekognition is not offered in every region, so it may be pointed elsewhere. When it is
				// not specified, the bucket's own region is the sensible default.
				var rekognitionRegionName = string.IsNullOrWhiteSpace(account.AwsRegionRekognition)
					? account.AwsRegion
					: account.AwsRegionRekognition;

				// A bucket whose credentials could not be resolved used to be skipped silently, leaving
				// HasAccounts() reporting true over an empty client map and the first real call failing
				// with "Sequence contains no elements". Fail here instead, naming the bucket.
				var credentials = (account.AwsCredentials?.HasCredentials ?? false)
					? new BasicAWSCredentials(account.AwsCredentials.AccessKey, account.AwsCredentials.SecretAccessKey)
					: commonBasicCredentials;

				if (credentials is null)
				{
					throw new StorageConnectorConfigurationException(
						$"No AWS credentials are available for bucket '{account.BucketName}'. Supply them at " +
						$"'{path}:AwsCredentials', or for all buckets at '{SectionName}:AwsCredentials'.");
				}

				if (AccountNamesMappedToAmazonS3Client.ContainsKey(account.BucketName))
				{
					throw new StorageConnectorConfigurationException(
						$"Duplicate bucket name '{account.BucketName}' at '{path}:BucketName'. Bucket names must be unique.");
				}

				AccountNamesMappedToAmazonS3Client.Add(account.BucketName, new AwsClients
				{
					AmazonS3Client = new AmazonS3Client(credentials, RegionEndpoint.GetBySystemName(account.AwsRegion)),
					AmazonRekognitionClient = new AmazonRekognitionClient(credentials, RegionEndpoint.GetBySystemName(rekognitionRegionName))
				});
			}
		}
	}
}
