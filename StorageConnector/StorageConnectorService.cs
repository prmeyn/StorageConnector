using EarthCountriesInfo;
using Microsoft.Extensions.Logging;
using StorageConnector.Common;
using StorageConnector.Common.DTOs;
using StorageConnector.Services.AWS;
using StorageConnector.Services.Azure;
using StorageConnector.Services.GCP;

namespace StorageConnector
{
	public sealed class StorageConnectorService : IStorageProvider, IFaceRecognitionProvider
	{
		private readonly AzureBlobStorageService _azureBlobStorageService;
		private readonly AmazonS3BucketService _amazonS3BucketService;
		private readonly GCPStorageService _gcpStorageService;
		private readonly ILogger<StorageConnectorService> _logger;

		public StorageConnectorService(
			AzureBlobStorageService azureBlobStorageService,
			AmazonS3BucketService awsS3BucketService,
			GCPStorageService gcpStorageService,
			ILogger<StorageConnectorService> logger
			)
		{
			_azureBlobStorageService = azureBlobStorageService;
			_amazonS3BucketService = awsS3BucketService;
			_gcpStorageService = gcpStorageService;

			_logger = logger;
		}

		public bool HasAccounts =>
			_amazonS3BucketService.HasAccounts || _azureBlobStorageService.HasAccounts || _gcpStorageService.HasAccounts;

		/// <summary>
		/// The provider serving this deployment.
		///
		/// One cloud is configured per deployment; the country map selects an account WITHIN that
		/// provider rather than between providers. Resolving it in one place also removes the repeated
		/// HasAccounts probing that used to run up to six times per request (finding M1).
		/// </summary>
		private IStorageProvider ActiveProvider
		{
			get
			{
				if (_amazonS3BucketService.HasAccounts)
				{
					return _amazonS3BucketService;
				}

				if (_azureBlobStorageService.HasAccounts)
				{
					return _azureBlobStorageService;
				}

				if (_gcpStorageService.HasAccounts)
				{
					return _gcpStorageService;
				}

				_logger.LogError("StorageConnectorService has no accounts");
				throw new InvalidOperationException("StorageConnectorService has no accounts");
			}
		}

		public Task<UploadInfo> GenerateDirectUploadInfo(
			CountryIsoCode countryOfResidenceIsoCode,
			CloudFileName fileReferenceWithPath,
			string contentType,
			int expiryInMinutes = IStorageProvider.DefaultExpiryInMinutes,
			CancellationToken cancellationToken = default)
		{
			var extension = GetExtensionFromContentType(contentType);
			if (string.IsNullOrWhiteSpace(extension))
			{
				_logger.LogError("Unknown content type: {ContentType}", contentType);
				throw new InvalidOperationException($"Unknown Content Type: {contentType}");
			}

			var provider = ActiveProvider;

			// The resolved name is what the object is actually stored under, and it is returned to the
			// caller on UploadInfo.FileName so the upload stays addressable (finding C4).
			var resolvedFileName = fileReferenceWithPath.Value.EndsWith(extension, StringComparison.Ordinal)
				? fileReferenceWithPath
				: new CloudFileName($"{fileReferenceWithPath.Value}{extension}");

			return provider.GenerateDirectUploadInfo(countryOfResidenceIsoCode, resolvedFileName, contentType, expiryInMinutes, cancellationToken);
		}

		/// <summary>
		/// Returns the canonical file extension for a content type, or <c>null</c> when unrecognised.
		/// See <see cref="ContentTypeExtensions"/> for why this is a curated map rather than a reverse
		/// scan of the static-files provider.
		/// </summary>
		public static string? GetExtensionFromContentType(string contentType)
			=> ContentTypeExtensions.GetExtensionFromContentType(contentType);

		// ------------------------------------------------------ Face recognition

		public bool SupportsFaceRecognition =>
			(_amazonS3BucketService as IFaceRecognitionProvider).SupportsFaceRecognition
			|| (_azureBlobStorageService as IFaceRecognitionProvider).SupportsFaceRecognition;

		/// <summary>
		/// The configured provider, when it supports face recognition.
		/// </summary>
		private IFaceRecognitionProvider ActiveFaceProvider
		{
			get
			{
				if (ActiveProvider is IFaceRecognitionProvider { SupportsFaceRecognition: true } faceProvider)
				{
					return faceProvider;
				}

				_logger.LogError("Face recognition was requested but the configured provider does not support it");
				throw new NotSupportedException(
					"The configured storage provider does not support face recognition. AWS supports it; Azure " +
					"additionally requires a 'StorageConnectors:Azure:VisionAccount' section; Google Cloud Storage " +
					"does not support it at all.");
			}
		}

		public Task<int> CountFacesAsync(
			CountryIsoCode regionCountryIsoCode,
			CloudFileName fileNameWithExtension,
			CancellationToken cancellationToken = default)
			=> ActiveFaceProvider.CountFacesAsync(regionCountryIsoCode, fileNameWithExtension, cancellationToken);

		public Task<IReadOnlySet<string>> FindMatchingFacesAsync(
			string faceCollectionName,
			CountryIsoCode regionCountryIsoCode,
			CloudFileName fileNameWithExtension,
			CancellationToken cancellationToken = default)
			=> ActiveFaceProvider.FindMatchingFacesAsync(faceCollectionName, regionCountryIsoCode, fileNameWithExtension, cancellationToken);

		/// <summary>
		/// Stores a biometric template. See <see cref="IFaceRecognitionProvider.RegisterFaceAsync"/> for
		/// the obligations that come with calling it.
		/// </summary>
		public Task<RegisteredFace> RegisterFaceAsync(
			string faceCollectionName,
			CountryIsoCode regionCountryIsoCode,
			CloudFileName fileNameWithExtension,
			string subjectId,
			CancellationToken cancellationToken = default)
			=> ActiveFaceProvider.RegisterFaceAsync(faceCollectionName, regionCountryIsoCode, fileNameWithExtension, subjectId, cancellationToken);

		/// <summary>
		/// Erases stored biometric templates. Pass the same country used when registering — templates
		/// live in the recognition service of the account that country maps to.
		/// </summary>
		public Task<int> DeleteRegisteredFacesAsync(
			string faceCollectionName,
			CountryIsoCode regionCountryIsoCode,
			string subjectId,
			CancellationToken cancellationToken = default)
			=> ActiveFaceProvider.DeleteRegisteredFacesAsync(faceCollectionName, regionCountryIsoCode, subjectId, cancellationToken);

		public Task<DownloadInfo> GenerateDirectDownloadInfo(
			CountryIsoCode countryOfResidenceIsoCode,
			CloudFileName fileReferenceWithPath,
			int expiryInMinutes = IStorageProvider.DefaultExpiryInMinutes,
			CancellationToken cancellationToken = default)
			=> ActiveProvider.GenerateDirectDownloadInfo(countryOfResidenceIsoCode, fileReferenceWithPath, expiryInMinutes, cancellationToken);
	}
}
