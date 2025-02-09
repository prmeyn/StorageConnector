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
			if (await HasAccounts())
			{
				var bucketNameToClient = _amazonS3BucketsInitializer.AccountNamesMappedToAmazonS3Client.First();
				if (_amazonS3BucketsInitializer.AmazonS3BucketSettings.CountryIsoCodeMapToAccountName.TryGetValue(countryOfResidenceIsoCode, out string bucketName))
				{
					bucketNameToClient = _amazonS3BucketsInitializer.AccountNamesMappedToAmazonS3Client.First(b => b.Key == bucketName);
				}
				var request = new GetPreSignedUrlRequest
				{
					BucketName = bucketNameToClient.Key,
					Key = fileReferenceWithPath.ToString(),
					Verb = HttpVerb.PUT,
					Expires = DateTime.UtcNow.AddMinutes(expiryInMinutes)
				};

				return new UploadInfo()
				{
					DirectUploadUrl = bucketNameToClient.Value.GetPreSignedURL(request),
					Headers = new Dictionary<string, string> { { "Content-Type", contentType } },
					HttpMethod = "PUT"
				};
			}
			_logger.LogError("No AmazonS3 account");
			throw new InvalidOperationException("No AmazonS3 account found");
		}

		public Task<HashSet<string>> GetMatchingFacesUserDataHashSet(string faceListName, CountryIsoCode regionCountryIsoCode, CloudFileName fileNameWithExtension, string userData)
		{
			throw new NotImplementedException();
		}

		public Task<byte?> GetNumberOfFacesOnImage(CountryIsoCode regionCountryIsoCode, CloudFileName fileNameWithExtension)
		{
			throw new NotImplementedException();
		}

		public async Task<bool> HasAccounts() => _amazonS3BucketsInitializer?.AmazonS3BucketSettings?.Accounts?.Any() ?? false;
	}
}
