using Amazon.Rekognition;
using Amazon.Rekognition.Model;
using Amazon.S3;
using Amazon.S3.Model;
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
					DirectUploadUrl = bucketNameToClient.Value.AmazonS3Client.GetPreSignedURL(request),
					Headers = new Dictionary<string, string> { { "Content-Type", contentType } },
					HttpMethod = "PUT"
				};
			}
			_logger.LogError("No AmazonS3 account");
			throw new InvalidOperationException("No AmazonS3 account found");
		}

		private async Task EnsureCollectionExists(string collectionId, AmazonRekognitionClient rekognitionClient)
		{
			var listCollectionsRequest = new ListCollectionsRequest();
			var listCollectionsResponse = await rekognitionClient.ListCollectionsAsync(listCollectionsRequest);

			if (!listCollectionsResponse.CollectionIds.Contains(collectionId))
			{
				var createCollectionRequest = new CreateCollectionRequest { CollectionId = collectionId };
				await rekognitionClient.CreateCollectionAsync(createCollectionRequest);
				Console.WriteLine($"Created collection: {collectionId}");
			}
		}


		private async Task AddFaceToCollection(string faceCollectionId, MemoryStream imageStream, string userData, AmazonRekognitionClient rekognitionClient)
		{
			try
			{
				var indexFacesRequest = new IndexFacesRequest
				{
					CollectionId = faceCollectionId,
					Image = new Image { Bytes = imageStream },
					ExternalImageId = userData, // Store user data as ExternalImageId
					DetectionAttributes = ["DEFAULT"]
				};

				await rekognitionClient.IndexFacesAsync(indexFacesRequest);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error indexing face");
			}
		}

		public async Task<bool> HasAccounts() => _amazonS3BucketsInitializer?.AmazonS3BucketSettings?.Accounts?.Any() ?? false;

		public async Task<FaceInfo> GetFaceInfo(string faceListName, CountryIsoCode regionCountryIsoCode, CloudFileName fileNameWithExtension, string userData)
		{
			byte numberOfFaces = 0;
			if (await HasAccounts())
			{
				var bucketNameToClient = _amazonS3BucketsInitializer.AccountNamesMappedToAmazonS3Client.First();
				if (_amazonS3BucketsInitializer.AmazonS3BucketSettings.CountryIsoCodeMapToAccountName.TryGetValue(regionCountryIsoCode, out string bucketName))
				{
					bucketNameToClient = _amazonS3BucketsInitializer.AccountNamesMappedToAmazonS3Client.First(b => b.Key == bucketName);
				}
				// Get image from S3
				var getObjectResponse = await bucketNameToClient.Value.AmazonS3Client.GetObjectAsync(bucketNameToClient.Key, fileNameWithExtension.ToString());
				using var memoryStream = new MemoryStream();
				await getObjectResponse.ResponseStream.CopyToAsync(memoryStream);
				memoryStream.Position = 0;

				try
				{
					

					// Call Rekognition to detect faces
					var detectFacesRequest = new DetectFacesRequest
					{
						Image = new Image { Bytes = memoryStream },
						Attributes = ["DEFAULT"]
					};

					var detectFacesResponse = await bucketNameToClient.Value.AmazonRekognitionClient.DetectFacesAsync(detectFacesRequest);
					numberOfFaces = (byte)detectFacesResponse.FaceDetails.Count();
				}
				catch (Exception ex)
				{
					_logger.LogError(ex, "Error detecting faces");
				}
				try
				{
	

					await EnsureCollectionExists(faceListName, bucketNameToClient.Value.AmazonRekognitionClient);


					// Search for matching faces in the Face Collection
					var searchFacesRequest = new SearchFacesByImageRequest
					{
						CollectionId = faceListName,
						Image = new Image { Bytes = memoryStream },
						MaxFaces = 4096,
						FaceMatchThreshold = 98 // Confidence threshold
					};

					var searchFacesResponse = await bucketNameToClient.Value.AmazonRekognitionClient.SearchFacesByImageAsync(searchFacesRequest);

					// Extract matching face user data
					var matchingUserData = new HashSet<string>();
					foreach (var match in searchFacesResponse.FaceMatches)
					{
						matchingUserData.Add(match.Face.ExternalImageId);
					}

					// If no match, add the new face to the collection
					await AddFaceToCollection(faceListName, memoryStream, userData, bucketNameToClient.Value.AmazonRekognitionClient);

					return new FaceInfo() {
						NumberOfFaces = numberOfFaces,
						MatchingUserData = matchingUserData
					};
				}
				catch (Exception ex)
				{
					_logger.LogError(ex, "Error searching faces");
				}
			}
			_logger.LogError("No AmazonS3 account");
			throw new InvalidOperationException("No AmazonS3 account found");
		}
	}
}
