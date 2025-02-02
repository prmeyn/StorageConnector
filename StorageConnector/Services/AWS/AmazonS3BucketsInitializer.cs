using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Microsoft.Extensions.Configuration;
using StorageConnector.Common;

namespace StorageConnector.Services.AWS
{
	public sealed class AmazonS3BucketsInitializer
	{
		internal readonly AmazonS3BucketSettings AmazonS3BucketSettings;

		internal readonly Dictionary<string, AmazonS3Client> AccountNamesMappedToAmazonS3Client = [];

		public AmazonS3BucketsInitializer(IConfiguration configuration)
		{
			var awsOptions = configuration.GetSection($"{ConstantStrings.StorageConnectorsConfigName}:AWS");

			if (awsOptions.Exists())
			{
				var commonAWSCredentialsOptions = awsOptions.GetSection("AwsCredentials").Get<AwsCredentials>();

				BasicAWSCredentials? _commonBasicAWSCredentials = null;

				if (commonAWSCredentialsOptions.HasCredentials)
				{
					_commonBasicAWSCredentials = new BasicAWSCredentials(commonAWSCredentialsOptions.AccessKey, commonAWSCredentialsOptions.SecretAccessKey);
				}


				AmazonS3BucketSettings = new AmazonS3BucketSettings
				{
					CountryIsoCodeMapToAccountName = (awsOptions.GetSection(ConstantStrings.CountryIsoCodeMapToAccountNameConfigName).Get<Dictionary<string, string>>()).ParseCountryIsoCodeMap(),
					Accounts = awsOptions.GetRequiredSection(ConstantStrings.AccountsConfigName).Get<List<AmazonS3Account>>()
				};

				foreach (var accounts in AmazonS3BucketSettings?.Accounts)
				{
					if (accounts?.AwsCredentials?.HasCredentials ?? false)
					{
						AccountNamesMappedToAmazonS3Client.Add(accounts.BucketName, new AmazonS3Client(
							new BasicAWSCredentials(accounts.AwsCredentials.AccessKey, accounts.AwsCredentials.SecretAccessKey),
							RegionEndpoint.GetBySystemName(accounts.AwsRegion)
						));
					}
					else if (_commonBasicAWSCredentials is not null)
					{
						AccountNamesMappedToAmazonS3Client.Add(accounts.BucketName, new AmazonS3Client(
							_commonBasicAWSCredentials,
							RegionEndpoint.GetBySystemName(accounts.AwsRegion)
						));
					}
				}
			}
		}
	}
}
