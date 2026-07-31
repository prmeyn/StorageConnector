using Azure.AI.Vision.Face;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using EarthCountriesInfo;
using Microsoft.Extensions.Logging;
using StorageConnector.Common;
using StorageConnector.Common.DTOs;

namespace StorageConnector.Services.Azure
{
	public sealed class AzureBlobStorageService : IStorageProvider, IFaceRecognitionProvider
	{
		/// <summary>
		/// How far a generated SAS is backdated to tolerate clock drift between this host and the
		/// client that will use the URL (finding M10).
		/// </summary>
		private static readonly TimeSpan ClockSkewAllowance = TimeSpan.FromMinutes(5);

		private readonly AzureBlobStoragesInitializer _azureBlobStoragesInitializer;
		private readonly ILogger<AzureBlobStorageService> _logger;

		public AzureBlobStorageService(AzureBlobStoragesInitializer azureBlobStoragesInitializer, ILogger<AzureBlobStorageService> logger)
		{
			_azureBlobStoragesInitializer = azureBlobStoragesInitializer;
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

			var (azureAccount, blobServiceClient) = RequireAccount(countryOfResidenceIsoCode);

			var blobName = fileReferenceWithPath.Value;
			BlobClient blobClient = blobServiceClient
				.GetBlobContainerClient(azureAccount.ContainerName)
				.GetBlobClient(blobName);

			// Backdated: a SAS is invalid before StartsOn, so a client whose clock runs a few minutes
			// behind this host would otherwise be refused with a 403 (finding M10).
			var startsOn = DateTimeOffset.UtcNow - ClockSkewAllowance;

			BlobSasBuilder sasBuilder = new()
			{
				BlobContainerName = azureAccount.ContainerName,
				BlobName = blobName,
				Resource = "b", // b = blob
				StartsOn = startsOn,
				ExpiresOn = DateTimeOffset.UtcNow.AddMinutes(expiryInMinutes),
			};

			sasBuilder.SetPermissions(BlobSasPermissions.Write | BlobSasPermissions.Create);

			Uri sasUri = blobClient.GenerateSasUri(sasBuilder);

			return Task.FromResult(new UploadInfo()
			{
				FileName = fileReferenceWithPath,
				DirectUploadUrl = sasUri.ToString(),
				Headers = new Dictionary<string, string> { { "Content-Type", contentType }, { "x-ms-blob-type", "BlockBlob" } },
				HttpMethod = "PUT"
			});
		}

		/// <summary>
		/// Resolves the account serving a country together with its client, throwing a single clear
		/// error when neither is available.
		/// </summary>
		private (AzureAccount Account, BlobServiceClient Client) RequireAccount(CountryIsoCode countryIsoCode)
		{
			var azureAccount = SelectAzureAccount(countryIsoCode);

			if (azureAccount is null
				|| !_azureBlobStoragesInitializer.AccountNamesMappedToBlobServiceClient.TryGetValue(azureAccount.AccountName, out var blobServiceClient))
			{
				_logger.LogError("No Azure account found");
				throw new InvalidOperationException("No Azure account found");
			}

			return (azureAccount, blobServiceClient);
		}

		private AzureAccount? SelectAzureAccount(CountryIsoCode regionCountryIsoCode)
		{
			var settings = _azureBlobStoragesInitializer.AzureBlobStorageSettings;
			if (settings is null)
			{
				return null;
			}

			return AccountSelector.Select(
				settings.CountryIsoCodeMapToAccountName,
				settings.Accounts,
				account => account.AccountName,
				regionCountryIsoCode);
		}

		/// <summary>
		/// Returns the Face clients, throwing a clear error when the Face API was never configured
		/// rather than dereferencing null. Blob storage is usable without a <c>VisionAccount</c>
		/// section, so this is a supported configuration and not an internal failure.
		/// </summary>
		private (FaceClient Face, FaceAdministrationClient Administration) RequireFaceClients()
		{
			var face = _azureBlobStoragesInitializer.FaceClient;
			var administration = _azureBlobStoragesInitializer.FaceAdministrationClient;

			if (face is null || administration is null)
			{
				_logger.LogError("The Azure Face API was requested but is not configured");
				throw new InvalidOperationException(
					$"The Azure Face API is not configured. Add '{ConstantStrings.StorageConnectorsConfigName}:Azure:VisionAccount' " +
					"with an Endpoint and ApiKey to use face recognition.");
			}

			return (face, administration);
		}


		// Reports on usable clients rather than raw configuration rows: an account that failed to
		// produce a client must not be advertised as available (finding C6).
		public bool HasAccounts => _azureBlobStoragesInitializer.AccountNamesMappedToBlobServiceClient.Count > 0;

		// ------------------------------------------------------ Face recognition

		public bool SupportsFaceRecognition =>
			HasAccounts
			&& _azureBlobStoragesInitializer.FaceClient is not null
			&& _azureBlobStoragesInitializer.FaceAdministrationClient is not null;

		/// <summary>
		/// Downloads the image once. The previous implementation fetched the blob, then called a helper
		/// that fetched the very same blob again (finding M8).
		/// </summary>
		private async Task<BinaryData> DownloadImage(
			CountryIsoCode regionCountryIsoCode,
			CloudFileName fileNameWithExtension,
			CancellationToken cancellationToken)
		{
			var (azureAccount, blobServiceClient) = RequireAccount(regionCountryIsoCode);

			BlobClient blobClient = blobServiceClient
				.GetBlobContainerClient(azureAccount.ContainerName)
				.GetBlobClient(fileNameWithExtension.Value);

			var blobDownload = await blobClient.DownloadStreamingAsync(cancellationToken: cancellationToken).ConfigureAwait(false);

			using var memoryStream = new MemoryStream();
			await blobDownload.Value.Content.CopyToAsync(memoryStream, cancellationToken).ConfigureAwait(false);
			memoryStream.Position = 0;

			return BinaryData.FromStream(memoryStream);
		}

		public async Task<int> CountFacesAsync(
			CountryIsoCode regionCountryIsoCode,
			CloudFileName fileNameWithExtension,
			CancellationToken cancellationToken = default)
		{
			var faceClients = RequireFaceClients();
			var imageContent = await DownloadImage(regionCountryIsoCode, fileNameWithExtension, cancellationToken).ConfigureAwait(false);

			return await DetectFaceCount(faceClients.Face, imageContent, cancellationToken).ConfigureAwait(false);
		}

		private static async Task<int> DetectFaceCount(FaceClient faceClient, BinaryData imageContent, CancellationToken cancellationToken)
		{
			var result = await faceClient.DetectAsync(
				imageContent,
				detectionModel: FaceDetectionModel.Detection03,
				recognitionModel: FaceRecognitionModel.Recognition04,
				returnFaceId: false,
				cancellationToken: cancellationToken).ConfigureAwait(false);

			// int, not byte: Detection03 can return up to 1000 faces and the previous unchecked cast
			// silently wrapped past 255 (finding M5).
			return result.Value.Count;
		}

		/// <summary>
		/// Not implemented for Azure.
		///
		/// The previous code added the queried face to the list and then returned an empty match set
		/// unconditionally, so callers were told "no matches" no matter who was in the photo, while the
		/// README advertised Azure face matching as working (finding H4). Failing loudly is honest;
		/// silently returning no matches is not.
		/// </summary>
		public Task<IReadOnlySet<string>> FindMatchingFacesAsync(
			string faceCollectionName,
			CountryIsoCode regionCountryIsoCode,
			CloudFileName fileNameWithExtension,
			CancellationToken cancellationToken = default)
		{
			throw new NotSupportedException(
				"Face matching is not implemented for Azure. Counting and registering faces are supported; " +
				"configure AWS to search a collection.");
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

			var faceClients = RequireFaceClients();
			var imageContent = await DownloadImage(regionCountryIsoCode, fileNameWithExtension, cancellationToken).ConfigureAwait(false);

			// Matches the AWS behaviour: storing a template from an image without exactly one clear face
			// persists biometric data that can never be matched back. Both providers must guarantee the
			// same thing, or the same call means different things depending on configuration.
			var detected = await DetectFaceCount(faceClients.Face, imageContent, cancellationToken).ConfigureAwait(false);
			if (detected != 1)
			{
				throw new InvalidOperationException(
					$"Expected exactly one face in '{fileNameWithExtension}' to register, but found {detected}.");
			}

			var largeFaceListClient = faceClients.Administration.GetLargeFaceListClient(faceCollectionName);

			var addedFace = await largeFaceListClient
				.AddFaceAsync(imageContent, userData: subjectId, cancellationToken: cancellationToken).ConfigureAwait(false);

			_logger.LogInformation(
				"Registered a face template for subject {SubjectId} in list {FaceListName}", subjectId, faceCollectionName);

			return new RegisteredFace
			{
				PersistedFaceId = addedFace.Value.PersistedFaceId.ToString(),
				SubjectId = subjectId
			};
		}

		public async Task<int> DeleteRegisteredFacesAsync(
			string faceCollectionName,
			CountryIsoCode regionCountryIsoCode,
			string subjectId,
			CancellationToken cancellationToken = default)
		{
			ArgumentException.ThrowIfNullOrWhiteSpace(faceCollectionName);
			FaceSubjectId.Validate(subjectId, nameof(subjectId));

			var faceClients = RequireFaceClients();
			var largeFaceListClient = faceClients.Administration.GetLargeFaceListClient(faceCollectionName);

			// Enumerate the whole list FIRST, then delete. Deleting while paging is unsafe: the cursor is
			// the last id seen, so once that face is removed the cursor no longer exists and the service
			// can restart from the beginning -- an erasure loop that never terminates.
			var toDelete = new List<Guid>();
			Guid? cursor = null;

			while (true)
			{
				var page = await largeFaceListClient
					.GetFacesAsync(cursor?.ToString(), 1000, cancellationToken).ConfigureAwait(false);

				var faces = page.Value;
				if (faces.Count == 0)
				{
					break;
				}

				toDelete.AddRange(faces
					.Where(face => string.Equals(face.UserData, subjectId, StringComparison.Ordinal))
					.Select(face => face.PersistedFaceId));

				cursor = faces[^1].PersistedFaceId;
			}

			var deleted = 0;
			foreach (var persistedFaceId in toDelete)
			{
				await largeFaceListClient.DeleteFaceAsync(
					persistedFaceId,
					new global::Azure.RequestContext { CancellationToken = cancellationToken }).ConfigureAwait(false);

				deleted++;
			}

			_logger.LogInformation(
				"Deleted {DeletedCount} face template(s) for subject {SubjectId} from list {FaceListName}",
				deleted, subjectId, faceCollectionName);

			return deleted;
		}

		public Task<DownloadInfo> GenerateDirectDownloadInfo(
			CountryIsoCode countryOfResidenceIsoCode,
			CloudFileName fileReferenceWithPath,
			int expiryInMinutes = IStorageProvider.DefaultExpiryInMinutes,
			CancellationToken cancellationToken = default)
		{
			throw new NotSupportedException(
				"Direct download URLs are not implemented for Azure Blob Storage. Configure AWS, " +
				"or track https://github.com/prmeyn/StorageConnector/issues for support.");
		}
	}
}
