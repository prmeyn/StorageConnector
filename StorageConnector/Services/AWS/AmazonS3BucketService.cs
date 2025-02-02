using Amazon.S3.Model;
using Amazon.S3;
using EarthCountriesInfo;
using Microsoft.Extensions.Logging;
using StorageConnector.Common;
using StorageConnector.Common.DTOs;

namespace StorageConnector.Services.AWS
{
	public sealed class AmazonS3BucketService : IStorageProvidor
	{
		private readonly AmazonS3BucketsInitializer _amazonS3BucketsInitializer;
		private readonly ILogger<AmazonS3BucketService> _logger;

		public AmazonS3BucketService(AmazonS3BucketsInitializer amazonS3BucketsInitializer, ILogger<AmazonS3BucketService> logger)
		{
			_amazonS3BucketsInitializer = amazonS3BucketsInitializer;
			_logger = logger;
		}

		public async Task<UploadInfo> GenerateDirectUploadInfo(CountryIsoCode countryOfResidenceIsoCode, CloudFileName fileReferenceWithPath, string contentType, int expiryInMinutes = 60)
		{
			var x = _amazonS3BucketsInitializer.AccountNamesMappedToAmazonS3Client.First();
			var request = new GetPreSignedUrlRequest
			{
				BucketName = x.Key,
				Key = fileReferenceWithPath.ToString(),
				Verb = HttpVerb.PUT,
				Expires = DateTime.UtcNow.AddMinutes(expiryInMinutes)
			};

			return new UploadInfo()
			{
				DirectUploadUrl = x.Value.GetPreSignedURL(request),
				Headers = new Dictionary<string, string> { { "Content-Type", contentType } },
				HttpMethod = "PUT"
			};
		}
	}
}
