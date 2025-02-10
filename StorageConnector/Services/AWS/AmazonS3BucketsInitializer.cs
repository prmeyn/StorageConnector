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
		internal readonly AmazonS3BucketSettings AmazonS3BucketSettings;

		internal readonly Dictionary<string, AwsClients> AccountNamesMappedToAmazonS3Client = [];

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
					var bucketRegion = RegionEndpoint.GetBySystemName(accounts.AwsRegion);
					var rekognitionRegion = RegionEndpoint.GetBySystemName(accounts.AwsRegionRekognition);
					if (accounts?.AwsCredentials?.HasCredentials ?? false)
					{
						var awsCredentials = new BasicAWSCredentials(accounts.AwsCredentials.AccessKey, accounts.AwsCredentials.SecretAccessKey);
						AccountNamesMappedToAmazonS3Client.Add(accounts.BucketName, new AwsClients() { AmazonS3Client = new AmazonS3Client(awsCredentials, bucketRegion), AmazonRekognitionClient = new AmazonRekognitionClient(awsCredentials, rekognitionRegion) });
					}
					else if (_commonBasicAWSCredentials is not null)
					{
						AccountNamesMappedToAmazonS3Client.Add(accounts.BucketName, new AwsClients() { AmazonS3Client = new AmazonS3Client(_commonBasicAWSCredentials, bucketRegion), AmazonRekognitionClient = new AmazonRekognitionClient(_commonBasicAWSCredentials, rekognitionRegion) });
					}
				}
			}
		}
	}
}
