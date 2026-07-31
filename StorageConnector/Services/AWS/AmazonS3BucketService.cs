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
	public sealed class AmazonS3BucketService : IStorageProvider, IFaceRecognitionProvider
	{
		private readonly AmazonS3BucketsInitializer _amazonS3BucketsInitializer;
		private readonly ILogger<AmazonS3BucketService> _logger;

		public AmazonS3BucketService(AmazonS3BucketsInitializer amazonS3BucketsInitializer, ILogger<AmazonS3BucketService> logger)
		{
			_amazonS3BucketsInitializer = amazonS3BucketsInitializer;
			_logger = logger;
		}

		public Task<UploadInfo> GenerateDirectUploadInfo(
			CountryIsoCode countryOfResidenceIsoCode,
			CloudFileName fileReferenceWithPath,
			string contentType,
			int expiryInMinutes = IStorageProvider.DefaultExpiryInMinutes,
			CancellationToken cancellationToken = default)
		{
			cancellationToken.ThrowIfCancellationRequested();

			if (!HasAccounts)
			{
				_logger.LogError("No AmazonS3 account");
				throw new InvalidOperationException("No AmazonS3 account found");
			}

			var (bucketName, clients) = SelectBucket(countryOfResidenceIsoCode);
			var request = new GetPreSignedUrlRequest
			{
				BucketName = bucketName,
				Key = fileReferenceWithPath.Value,
				Verb = HttpVerb.PUT,
				Expires = DateTime.UtcNow.AddMinutes(expiryInMinutes),

				// Signing the content type makes the header we hand back in UploadInfo.Headers binding:
				// the client must send exactly this type or S3 rejects the upload. Previously the header
				// was advertised but unsigned, so anything could be uploaded under, say, a .png key
				// (finding M11).
				ContentType = contentType
			};

			return Task.FromResult(new UploadInfo()
			{
				FileName = fileReferenceWithPath,
				DirectUploadUrl = clients.AmazonS3Client.GetPreSignedURL(request),
				Headers = new Dictionary<string, string> { { "Content-Type", contentType } },
				HttpMethod = "PUT"
			});
		}

		/// <summary>
		/// Resolves the bucket serving a country, falling back to the first configured bucket when the
		/// country is unmapped or names a bucket that does not exist. Previously this used
		/// <c>First(predicate)</c>, which threw on a mistyped country map while Azure silently fell back
		/// -- the same configuration error behaved differently per provider (finding H10).
		/// </summary>
		private (string BucketName, AwsClients Clients) SelectBucket(CountryIsoCode countryIsoCode)
		{
			var settings = _amazonS3BucketsInitializer.AmazonS3BucketSettings;
			var clientsByBucket = _amazonS3BucketsInitializer.AccountNamesMappedToAmazonS3Client;

			if (settings is not null
				&& settings.CountryIsoCodeMapToAccountName.TryGetValue(countryIsoCode, out var mappedBucketName)
				&& clientsByBucket.TryGetValue(mappedBucketName, out var mappedClients))
			{
				return (mappedBucketName, mappedClients);
			}

			// HasAccounts() guarantees at least one entry, so this is safe.
			var first = clientsByBucket.First();
			return (first.Key, first.Value);
		}


		// Reports on usable clients rather than raw configuration rows. Previously this returned true
		// whenever an account was listed, even if no credentials resolved and the client map was empty,
		// so the first real call died with "Sequence contains no elements" (finding C6).
		public bool HasAccounts => _amazonS3BucketsInitializer.AccountNamesMappedToAmazonS3Client.Count > 0;

		// ------------------------------------------------------ Face recognition

		public bool SupportsFaceRecognition => HasAccounts;

		/// <summary>
		/// Downloads the image once and reuses the bytes. Every face operation needs them, and each
		/// used to re-fetch the object from S3 independently.
		/// </summary>
		private async Task<byte[]> DownloadImageBytes(
			CountryIsoCode regionCountryIsoCode,
			CloudFileName fileNameWithExtension,
			CancellationToken cancellationToken)
		{
			if (!HasAccounts)
			{
				_logger.LogError("No AmazonS3 account found");
				throw new InvalidOperationException("No AmazonS3 account found");
			}

			var (bucketName, clients) = SelectBucket(regionCountryIsoCode);

			using var getObjectResponse = await clients.AmazonS3Client
				.GetObjectAsync(bucketName, fileNameWithExtension.Value, cancellationToken).ConfigureAwait(false);
			using var memoryStream = new MemoryStream();
			await getObjectResponse.ResponseStream.CopyToAsync(memoryStream, cancellationToken).ConfigureAwait(false);

			return memoryStream.ToArray();
		}

		public async Task<int> CountFacesAsync(
			CountryIsoCode regionCountryIsoCode,
			CloudFileName fileNameWithExtension,
			CancellationToken cancellationToken = default)
		{
			var (_, clients) = SelectBucketForFaces(regionCountryIsoCode);
			var imageBytes = await DownloadImageBytes(regionCountryIsoCode, fileNameWithExtension, cancellationToken).ConfigureAwait(false);

			return await CountFacesInBytes(imageBytes, clients, fileNameWithExtension, cancellationToken).ConfigureAwait(false);
		}

		private async Task<int> CountFacesInBytes(
			byte[] imageBytes,
			AwsClients clients,
			CloudFileName fileNameWithExtension,
			CancellationToken cancellationToken)
		{
			using var stream = new MemoryStream(imageBytes);
			var detectFacesResponse = await clients.AmazonRekognitionClient.DetectFacesAsync(
				new DetectFacesRequest
				{
					Image = new Image { Bytes = stream },
					Attributes = ["DEFAULT"]
				},
				cancellationToken).ConfigureAwait(false);

			var quality = _amazonS3BucketsInitializer.FaceQuality;

			// Counted as int, not byte: Rekognition can return more than 255 faces and the previous
			// unchecked cast silently wrapped -- 300 faces reported as 44 (finding M5).
			var highQualityFaces = detectFacesResponse.FaceDetails
				.Where(face => face.Quality.Sharpness >= quality.MinSharpness
					&& face.Quality.Brightness >= quality.MinBrightness
					&& face.Confidence >= quality.MinConfidence)
				.ToList();

			if (highQualityFaces.Count != 1)
			{
				foreach (var face in detectFacesResponse.FaceDetails)
				{
					_logger.LogInformation(
						"Rejected or ambiguous face in {FileName} - Sharpness: {Sharpness}, Brightness: {Brightness}, Confidence: {Confidence}",
						fileNameWithExtension, face.Quality.Sharpness, face.Quality.Brightness, face.Confidence);
				}
			}

			return highQualityFaces.Count;
		}

		public async Task<IReadOnlySet<string>> FindMatchingFacesAsync(
			string faceCollectionName,
			CountryIsoCode regionCountryIsoCode,
			CloudFileName fileNameWithExtension,
			CancellationToken cancellationToken = default)
		{
			ArgumentException.ThrowIfNullOrWhiteSpace(faceCollectionName);

			var (_, clients) = SelectBucketForFaces(regionCountryIsoCode);
			var imageBytes = await DownloadImageBytes(regionCountryIsoCode, fileNameWithExtension, cancellationToken).ConfigureAwait(false);

			// Searching a collection that was never created is not a failure -- it simply has no matches.
			if (!await CollectionExists(faceCollectionName, clients.AmazonRekognitionClient, cancellationToken).ConfigureAwait(false))
			{
				return new HashSet<string>();
			}

			using var searchStream = new MemoryStream(imageBytes);

			try
			{
				var searchFacesResponse = await clients.AmazonRekognitionClient.SearchFacesByImageAsync(
					new SearchFacesByImageRequest
					{
						CollectionId = faceCollectionName,
						Image = new Image { Bytes = searchStream },
						MaxFaces = 4096,
						FaceMatchThreshold = _amazonS3BucketsInitializer.FaceQuality.MatchThreshold
					},
					cancellationToken).ConfigureAwait(false);

				return searchFacesResponse.FaceMatches
					.Select(match => match.Face.ExternalImageId)
					.Where(id => !string.IsNullOrEmpty(id))
					.ToHashSet(StringComparer.Ordinal);
			}
			catch (InvalidParameterException)
			{
				// Rekognition raises this when the image contains no detectable face. No face means no
				// matches, which is an answer rather than an error.
				return new HashSet<string>();
			}
		}

		public async Task<RegisteredFace> RegisterFaceAsync(
			string faceCollectionName,
			CountryIsoCode regionCountryIsoCode,
			CloudFileName fileNameWithExtension,
			string subjectId,
			CancellationToken cancellationToken = default)
		{
			ArgumentException.ThrowIfNullOrWhiteSpace(faceCollectionName);
			FaceSubjectId.Validate(subjectId, nameof(subjectId));

			var (_, clients) = SelectBucketForFaces(regionCountryIsoCode);
			var imageBytes = await DownloadImageBytes(regionCountryIsoCode, fileNameWithExtension, cancellationToken).ConfigureAwait(false);

			// Registering an image with no single clear face would store a template that can never be
			// matched back, so refuse rather than silently persist biometric data of no use.
			var faceCount = await CountFacesInBytes(imageBytes, clients, fileNameWithExtension, cancellationToken).ConfigureAwait(false);
			if (faceCount != 1)
			{
				throw new InvalidOperationException(
					$"Expected exactly one good-quality face in '{fileNameWithExtension}' to register, but found {faceCount}.");
			}

			await EnsureCollectionExists(faceCollectionName, clients.AmazonRekognitionClient, cancellationToken).ConfigureAwait(false);

			using var indexStream = new MemoryStream(imageBytes);
			var indexResponse = await clients.AmazonRekognitionClient.IndexFacesAsync(
				new IndexFacesRequest
				{
					CollectionId = faceCollectionName,
					Image = new Image { Bytes = indexStream },
					ExternalImageId = subjectId,
					DetectionAttributes = ["DEFAULT"]
				},
				cancellationToken).ConfigureAwait(false);

			var persistedFaceId = indexResponse.FaceRecords.FirstOrDefault()?.Face?.FaceId
				?? throw new InvalidOperationException(
					$"Rekognition stored no face for '{fileNameWithExtension}' in collection '{faceCollectionName}'.");

			_logger.LogInformation(
				"Registered a face template for subject {SubjectId} in collection {CollectionId}", subjectId, faceCollectionName);

			return new RegisteredFace { PersistedFaceId = persistedFaceId, SubjectId = subjectId };
		}

		public async Task<int> DeleteRegisteredFacesAsync(
			string faceCollectionName,
			CountryIsoCode regionCountryIsoCode,
			string subjectId,
			CancellationToken cancellationToken = default)
		{
			ArgumentException.ThrowIfNullOrWhiteSpace(faceCollectionName);
			FaceSubjectId.Validate(subjectId, nameof(subjectId));

			// Resolved from the country, exactly as RegisterFaceAsync does. Taking the first configured
			// bucket instead would query whichever Rekognition region happened to be listed first, find
			// nothing, and report a successful erasure while the template survived.
			var (_, clients) = SelectBucketForFaces(regionCountryIsoCode);

			if (!await CollectionExists(faceCollectionName, clients.AmazonRekognitionClient, cancellationToken).ConfigureAwait(false))
			{
				return 0;
			}

			// Page through the collection collecting every template belonging to this subject.
			var faceIds = new List<string>();
			string? nextToken = null;
			do
			{
				var listFaces = await clients.AmazonRekognitionClient.ListFacesAsync(
					new ListFacesRequest
					{
						CollectionId = faceCollectionName,
						MaxResults = 1000,
						NextToken = nextToken
					},
					cancellationToken).ConfigureAwait(false);

				faceIds.AddRange(listFaces.Faces
					.Where(face => string.Equals(face.ExternalImageId, subjectId, StringComparison.Ordinal))
					.Select(face => face.FaceId));

				nextToken = listFaces.NextToken;
			}
			while (!string.IsNullOrEmpty(nextToken));

			if (faceIds.Count == 0)
			{
				return 0;
			}

			// DeleteFaces accepts at most 1000 ids per call.
			var deleted = 0;
			foreach (var batch in faceIds.Chunk(1000))
			{
				var deleteResponse = await clients.AmazonRekognitionClient.DeleteFacesAsync(
					new DeleteFacesRequest
					{
						CollectionId = faceCollectionName,
						FaceIds = [.. batch]
					},
					cancellationToken).ConfigureAwait(false);

				deleted += deleteResponse.DeletedFaces.Count;
			}

			_logger.LogInformation(
				"Deleted {DeletedCount} face template(s) for subject {SubjectId} from collection {CollectionId}",
				deleted, subjectId, faceCollectionName);

			return deleted;
		}

		/// <summary>
		/// Resolves the clients for a face operation, failing clearly when AWS is not configured.
		/// </summary>
		private (string BucketName, AwsClients Clients) SelectBucketForFaces(CountryIsoCode regionCountryIsoCode)
		{
			if (!HasAccounts)
			{
				_logger.LogError("No AmazonS3 account found");
				throw new InvalidOperationException("No AmazonS3 account found");
			}

			return SelectBucket(regionCountryIsoCode);
		}

		private static async Task<bool> CollectionExists(
			string collectionId,
			AmazonRekognitionClient rekognitionClient,
			CancellationToken cancellationToken)
		{
			try
			{
				await rekognitionClient.DescribeCollectionAsync(
					new DescribeCollectionRequest { CollectionId = collectionId }, cancellationToken).ConfigureAwait(false);
				return true;
			}
			catch (ResourceNotFoundException)
			{
				return false;
			}
		}

		/// <summary>
		/// Creates the collection if it is missing. This used to call ListCollections on every single
		/// face lookup and race between the check and the create; creating and tolerating
		/// ResourceAlreadyExists is both cheaper and correct under concurrency (finding M7).
		/// </summary>
		private async Task EnsureCollectionExists(string collectionId, AmazonRekognitionClient rekognitionClient, CancellationToken cancellationToken)
		{
			try
			{
				await rekognitionClient.CreateCollectionAsync(
					new CreateCollectionRequest { CollectionId = collectionId }, cancellationToken).ConfigureAwait(false);

				_logger.LogInformation("Created Rekognition collection {CollectionId}", collectionId);
			}
			catch (ResourceAlreadyExistsException)
			{
				// Already present, which is the common case.
			}
		}

		public Task<DownloadInfo> GenerateDirectDownloadInfo(
			CountryIsoCode countryOfResidenceIsoCode,
			CloudFileName fileReferenceWithPath,
			int expiryInMinutes = IStorageProvider.DefaultExpiryInMinutes,
			CancellationToken cancellationToken = default)
		{
			cancellationToken.ThrowIfCancellationRequested();

			if (!HasAccounts)
			{
				_logger.LogError("No AmazonS3 account");
				throw new InvalidOperationException("No AmazonS3 account found");
			}

			var (bucketName, clients) = SelectBucket(countryOfResidenceIsoCode);
			var request = new GetPreSignedUrlRequest
			{
				BucketName = bucketName,
				Key = fileReferenceWithPath.Value,
				Verb = HttpVerb.GET,
				Expires = DateTime.UtcNow.AddMinutes(expiryInMinutes)
			};

			return Task.FromResult(new DownloadInfo()
			{
				DirectDownloadUrl = clients.AmazonS3Client.GetPreSignedURL(request)
			});
		}
	}
}
