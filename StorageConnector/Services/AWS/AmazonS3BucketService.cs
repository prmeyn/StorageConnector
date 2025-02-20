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
	public sealed class AmazonS3BucketService : IStorageProvider
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
			imageStream.Position = 0; // Reset stream again
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

			if (!await HasAccounts())
			{
				_logger.LogError("No AmazonS3 account found");
				throw new InvalidOperationException("No AmazonS3 account found");
			}

			// Retrieve the S3 client
			var bucketNameToClient = _amazonS3BucketsInitializer.AccountNamesMappedToAmazonS3Client.FirstOrDefault();


			if (_amazonS3BucketsInitializer.AmazonS3BucketSettings.CountryIsoCodeMapToAccountName.TryGetValue(regionCountryIsoCode, out string bucketName))
			{
				bucketNameToClient = _amazonS3BucketsInitializer.AccountNamesMappedToAmazonS3Client.FirstOrDefault(b => b.Key == bucketName);
			}

			if (bucketNameToClient.Equals(default(KeyValuePair<string, AmazonS3Client>)))
			{
				_logger.LogError("No valid S3 client found");
				throw new InvalidOperationException("No valid S3 client found");
			}

			try
			{
				// Retrieve image from S3
				var getObjectResponse = await bucketNameToClient.Value.AmazonS3Client.GetObjectAsync(bucketNameToClient.Key, fileNameWithExtension.ToString());
				using var memoryStream = new MemoryStream();
				await getObjectResponse.ResponseStream.CopyToAsync(memoryStream);

				// Ensure collection exists
				await EnsureCollectionExists(faceListName, bucketNameToClient.Value.AmazonRekognitionClient);

				memoryStream.Position = 0;

				// Call Rekognition to detect faces
				var detectFacesRequest = new DetectFacesRequest
				{
					Image = new Image { Bytes = memoryStream },
					Attributes = ["DEFAULT"]
				};

				var detectFacesResponse = await bucketNameToClient.Value.AmazonRekognitionClient.DetectFacesAsync(detectFacesRequest);

				const float MinSharpness = 50.0f;
				const float MinBrightness = 30.0f;
				//const float MaxBrightness = 70.0f; // Avoid overexposed faces
				const float MinFaceConfidence = 80.0f; // Minimum confidence for face detection

				var highQualityFaces = detectFacesResponse.FaceDetails
					.Where(face => face.Quality.Sharpness >= MinSharpness &&
								   face.Quality.Brightness >= MinBrightness &&
								   //face.Quality.Brightness <= MaxBrightness &&
								   face.Confidence >= MinFaceConfidence) // Ensure face detection confidence is high
					.ToList();

				numberOfFaces = (byte)highQualityFaces.Count;




				// If one face is detected, attempt face search
				var matchingUserData = new HashSet<string>();
				if (numberOfFaces == 1)
				{
					
					// Create a separate memory stream for face search
					memoryStream.Position = 0;
					var searchStream = new MemoryStream(memoryStream.ToArray());

					var searchFacesRequest = new SearchFacesByImageRequest
					{
						CollectionId = faceListName,
						Image = new Image { Bytes = searchStream },
						MaxFaces = 4096,
						FaceMatchThreshold = 98 // Confidence threshold
					};

					var searchFacesResponse = await bucketNameToClient.Value.AmazonRekognitionClient.SearchFacesByImageAsync(searchFacesRequest);

					// Extract matched face user data
					foreach (var match in searchFacesResponse.FaceMatches)
					{
						matchingUserData.Add(match.Face.ExternalImageId);
					}

					await AddFaceToCollection(faceListName, memoryStream, userData, bucketNameToClient.Value.AmazonRekognitionClient);
				}

				return new FaceInfo
				{
					NumberOfFaces = numberOfFaces,
					MatchingUserData = matchingUserData
				};
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error processing face recognition");
				throw; // Rethrow the exception for better error handling
			}
		}

	}
}
