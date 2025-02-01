using Amazon.Runtime;
using Amazon.S3;
using Microsoft.Extensions.Configuration;

namespace StorageConnector.Services.AWS
{
	public sealed class AmazonS3BucketsInitializer
	{
		internal readonly AmazonS3BucketSettings AmazonS3BucketSettings;
		internal readonly BasicAWSCredentials BasicAWSCredentials;
		internal readonly Dictionary<string, AmazonS3Client> AccountNamesMappedToAmazonS3Client = [];

		public AmazonS3BucketsInitializer(IConfiguration configuration)
		{
		}
	}
}
